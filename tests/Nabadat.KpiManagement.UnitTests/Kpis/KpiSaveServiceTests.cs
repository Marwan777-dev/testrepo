using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Nabadat.KpiManagement.Application.Events;
using Nabadat.KpiManagement.Application.Interfaces;
using Nabadat.KpiManagement.Application.Kpis.Interfaces;
using Nabadat.KpiManagement.Application.Perspectives.Interfaces;
using Nabadat.KpiManagement.Domain.Entities;
using Nabadat.KpiManagement.Domain.ValueObjects;
using NSubstitute;
using Xunit;
using Nabadat.KpiManagement.Application.Kpis.Dtos;
using Nabadat.KpiManagement.Application.Kpis.Services;

namespace Nabadat.KpiManagement.UnitTests.Kpis;

/// <summary>
/// T050 [US2] — unit tests for <c>KpiSaveService</c> (the atomic create/edit orchestrator),
/// covering the spec.md US-2 Required cases: create-valid persists the definition + threshold +
/// perspectives and emits one <c>settings.changed</c> "created" event; edit-full-name-only emits one
/// "updated" event carrying the single-field change; edit-validation-failure writes nothing and emits
/// nothing.
/// <para>
/// Contract pinned for the implementer (T057):
/// <list type="bullet">
///   <item>ctor <c>KpiSaveService(ITenantDbContext context, IKpiDefinitionService definitions,
///   IKpiThresholdService thresholds, IKpiPerspectiveService perspectives,
///   IValidator&lt;KpiDefinitionInput&gt; validator, KpiEventPublisher events, TimeProvider timeProvider)</c>
///   — depends on the per-entity service <em>ports</em> (the DB-08 mock seam), the FluentValidation
///   interface, the concrete M-06 <c>KpiEventPublisher</c>, and an injected <see cref="TimeProvider"/>.</item>
///   <item><c>Task&lt;KpiSaveResult&gt; SaveAsync(KpiSaveCommand command, CancellationToken ct = default)</c>.</item>
///   <item><c>KpiSaveMode { Create, Edit }</c>; <c>KpiSaveCommand(KpiSaveMode Mode, KpiDefinition Definition,
///   KpiThreshold Threshold, IReadOnlyList&lt;KpiPerspective&gt; Perspectives, Guid ActorId,
///   string ActorPersona, Guid CorrelationId)</c>; <c>KpiSaveResult(bool Succeeded, Guid KpiId, string? ErrorCode)</c>.</item>
///   <item>On success the whole write runs inside <c>ITenantDbContext.ExecuteAsync(Func&lt;Task&gt;, ct)</c>
///   (the single multi-write tx boundary); validation runs BEFORE the transaction opens, so a
///   validation failure never touches the context.</item>
///   <item>The audit event's <c>occurredAtUtc</c> comes from <c>timeProvider.GetUtcNow()</c> (time
///   injection rule) and its <c>newValue</c> payload carries the action token (<c>created</c>/<c>updated</c>).</item>
/// </list>
/// </para>
/// </summary>
public sealed class KpiSaveServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly ITenantDbContext _context = Substitute.For<ITenantDbContext>();
    private readonly DbSet<EventLog> _eventLogs = Substitute.For<DbSet<EventLog>>();
    private readonly IKpiDefinitionService _definitions = Substitute.For<IKpiDefinitionService>();
    private readonly IKpiThresholdService _thresholds = Substitute.For<IKpiThresholdService>();
    private readonly IKpiPerspectiveService _perspectives = Substitute.For<IKpiPerspectiveService>();
    private readonly IValidator<KpiDefinitionInput> _validator = Substitute.For<IValidator<KpiDefinitionInput>>();
    private readonly FakeTimeProvider _time = new(FixedNow);
    private readonly List<EventLog> _capturedEvents = [];

    public KpiSaveServiceTests()
    {
        _context.EventLogs.Returns(_eventLogs);
        _context.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        // ExecuteAsync runs the supplied unit of work inline (the real tx wrapper just commits it).
        _context.ExecuteAsync(Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((Func<Task>)ci[0]).Invoke());
        _eventLogs.Add(Arg.Do<EventLog>(e => _capturedEvents.Add(e)));
        // Default: validation passes (each test overrides for the failure case).
        _validator.Validate(Arg.Any<KpiDefinitionInput>()).Returns(new ValidationResult());
        _validator.ValidateAsync(Arg.Any<KpiDefinitionInput>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
    }

    private KpiSaveService CreateService() =>
        new(_context, _definitions, _thresholds, _perspectives, _validator, new KpiEventPublisher(_context), _time);

    [Fact]
    public async Task SaveAsync_persists_all_tables_and_emits_one_created_event_when_create_is_valid()
    {
        var definition = CustomDefinition();
        var command = new KpiSaveCommand(
            KpiSaveMode.Create, definition, ThresholdFor(definition.Id), [],
            ActorId: Guid.NewGuid(), ActorPersona: "P-01", CorrelationId: Guid.NewGuid());

        var result = await CreateService().SaveAsync(command);

        result.Succeeded.Should().BeTrue();
        result.KpiId.Should().Be(definition.Id);
        await _definitions.Received(1).AddAsync(definition, Arg.Any<CancellationToken>());
        await _thresholds.Received(1).UpsertAsync(Arg.Any<KpiThreshold>(), Arg.Any<CancellationToken>());
        await _perspectives.Received(1)
            .ReplaceAllAsync(definition.Id, Arg.Any<IEnumerable<KpiPerspective>>(), Arg.Any<CancellationToken>());
        _capturedEvents.Should().ContainSingle();
        _capturedEvents[0].NewValue.Should().Contain("created");
        _capturedEvents[0].OccurredAtUtc.Should().Be(FixedNow);
    }

    [Fact]
    public async Task SaveAsync_emits_one_updated_event_with_the_changed_field_when_only_full_name_changes()
    {
        var existing = CustomDefinition();
        existing.FullName = "Service Quality";
        _definitions.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);

        var edited = CustomDefinition();
        edited.Id = existing.Id;
        edited.FullName = "Renamed Service Quality";
        var command = new KpiSaveCommand(
            KpiSaveMode.Edit, edited, ThresholdFor(existing.Id), [],
            ActorId: Guid.NewGuid(), ActorPersona: "P-01", CorrelationId: Guid.NewGuid());

        var result = await CreateService().SaveAsync(command);

        result.Succeeded.Should().BeTrue();
        await _definitions.Received(1).UpdateAsync(Arg.Any<KpiDefinition>(), Arg.Any<CancellationToken>());
        await _definitions.DidNotReceive().AddAsync(Arg.Any<KpiDefinition>(), Arg.Any<CancellationToken>());
        _capturedEvents.Should().ContainSingle();
        _capturedEvents[0].NewValue.Should().Contain("updated");
        _capturedEvents[0].NewValue.Should().Contain("Renamed Service Quality");
    }

    [Fact]
    public async Task SaveAsync_writes_nothing_and_emits_nothing_when_validation_fails()
    {
        _validator.Validate(Arg.Any<KpiDefinitionInput>()).Returns(InvalidResult("target.required_when_active"));
        _validator.ValidateAsync(Arg.Any<KpiDefinitionInput>(), Arg.Any<CancellationToken>())
            .Returns(InvalidResult("target.required_when_active"));

        var definition = CustomDefinition();
        var command = new KpiSaveCommand(
            KpiSaveMode.Edit, definition, ThresholdFor(definition.Id), [],
            ActorId: Guid.NewGuid(), ActorPersona: "P-01", CorrelationId: Guid.NewGuid());

        var result = await CreateService().SaveAsync(command);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be("target.required_when_active");
        await _definitions.DidNotReceive().AddAsync(Arg.Any<KpiDefinition>(), Arg.Any<CancellationToken>());
        await _definitions.DidNotReceive().UpdateAsync(Arg.Any<KpiDefinition>(), Arg.Any<CancellationToken>());
        await _thresholds.DidNotReceive().UpsertAsync(Arg.Any<KpiThreshold>(), Arg.Any<CancellationToken>());
        await _perspectives.DidNotReceive()
            .ReplaceAllAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<KpiPerspective>>(), Arg.Any<CancellationToken>());
        _eventLogs.DidNotReceive().Add(Arg.Any<EventLog>());
    }

    private static KpiDefinition CustomDefinition() => new()
    {
        Id = Guid.NewGuid(),
        ShortName = "QUAL",
        FullName = "Service Quality",
        KpiType = KpiType.Custom,
        IsComposite = false,
        CalculationMethod = CalculationMethod.WeightedAverage,
        Scale = Scale.Scale1_5,
        RepresentationStyle = RepresentationStyle.Number,
        Target = 80m,
        IsActive = true,
    };

    private static KpiThreshold ThresholdFor(Guid kpiId) =>
        new() { KpiId = kpiId, LowerBound = 0m, X = 20m, Y = 70m, UpperBound = 100m };

    private static ValidationResult InvalidResult(string errorCode) =>
        new([new ValidationFailure("Target", "Target is required when the KPI is active.") { ErrorCode = errorCode }]);
}
