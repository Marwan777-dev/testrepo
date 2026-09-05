using FluentValidation;
using Nabadat.KpiManagement.Application.Events;
using Nabadat.KpiManagement.Application.Interfaces;
using Nabadat.KpiManagement.Application.Kpis.Interfaces;
using Nabadat.KpiManagement.Application.Perspectives.Interfaces;
using Nabadat.KpiManagement.Domain.Entities;
using Nabadat.KpiManagement.Domain.ValueObjects;
using Nabadat.KpiManagement.Application.Kpis.Dtos;

namespace Nabadat.KpiManagement.Application.Kpis.Services;

/// <summary>
/// The atomic create/edit orchestrator for a KPI (US-2). Composes the per-entity services
/// (<see cref="IKpiDefinitionService"/> / <see cref="IKpiThresholdService"/> /
/// <see cref="IKpiPerspectiveService"/>) inside one <see cref="ITenantDbContext.ExecuteAsync(Func{Task}, CancellationToken)"/>
/// transaction (DB-08 — the single multi-write boundary, no unit-of-work type), so the definition,
/// its threshold, its full perspective set, and the M-17 <c>settings.changed</c> audit row all
/// commit or roll back together.
/// <para>
/// Validation runs <b>before</b> the transaction opens (a failure never touches the context).
/// Edit enforces the immutability rules: Short Name is immutable (FR-004,
/// <see cref="ShortNameImmutableCode"/>), and a standard KPI's scale / calculation method are locked
/// (FR-005, <see cref="FieldImmutableForStandardCode"/>). The scale-change-vs-bindings confirmation
/// (FR-017) lives in the controller, which owns the M-16 <see cref="KpiBindingUsageProbe"/> and the
/// <c>confirm_structural_change</c> flag. Time is injected via <see cref="TimeProvider"/>.
/// </para>
/// </summary>
public sealed class KpiSaveService
{
    public const string ShortNameImmutableCode = "KPI_SHORT_NAME_IMMUTABLE";
    public const string FieldImmutableForStandardCode = "KPI_FIELD_IMMUTABLE_FOR_STANDARD";
    public const string NotFoundCode = "KPI_NOT_FOUND";

    private readonly ITenantDbContext _context;
    private readonly IKpiDefinitionService _definitions;
    private readonly IKpiThresholdService _thresholds;
    private readonly IKpiPerspectiveService _perspectives;
    private readonly IValidator<KpiDefinitionInput> _validator;
    private readonly KpiEventPublisher _events;
    private readonly TimeProvider _timeProvider;

    public KpiSaveService(
        ITenantDbContext context,
        IKpiDefinitionService definitions,
        IKpiThresholdService thresholds,
        IKpiPerspectiveService perspectives,
        IValidator<KpiDefinitionInput> validator,
        KpiEventPublisher events,
        TimeProvider timeProvider)
    {
        _context = context;
        _definitions = definitions;
        _thresholds = thresholds;
        _perspectives = perspectives;
        _validator = validator;
        _events = events;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Validates the command and, when valid, persists the KPI definition + threshold + perspectives
    /// and emits one <c>settings.changed</c> event, all inside a single transaction. Returns a
    /// failure result (no writes, no event) when validation or an immutability rule fails.
    /// </summary>
    public async Task<KpiSaveResult> SaveAsync(KpiSaveCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var definition = command.Definition;

        var input = await BuildValidationInputAsync(definition, command.Threshold, ct);
        var validation = await _validator.ValidateAsync(input, ct);
        if (!validation.IsValid)
        {
            return new KpiSaveResult(false, definition.Id, validation.Errors.FirstOrDefault()?.ErrorCode);
        }

        KpiDefinition? existing = null;
        if (command.Mode == KpiSaveMode.Edit)
        {
            existing = await _definitions.GetByIdAsync(definition.Id, ct);
            if (existing is null)
            {
                return new KpiSaveResult(false, definition.Id, NotFoundCode);
            }

            var immutabilityError = CheckImmutability(existing, definition);
            if (immutabilityError is not null)
            {
                return new KpiSaveResult(false, definition.Id, immutabilityError);
            }
        }

        var occurredAt = _timeProvider.GetUtcNow();

        await _context.ExecuteAsync(async () =>
        {
            if (command.Mode == KpiSaveMode.Create)
            {
                await _definitions.AddAsync(definition, ct);
            }
            else
            {
                await _definitions.UpdateAsync(definition, ct);
            }

            command.Threshold.KpiId = definition.Id;
            await _thresholds.UpsertAsync(command.Threshold, ct);

            await _perspectives.ReplaceAllAsync(definition.Id, command.Perspectives, ct);

            var oldValue = existing is null ? null : Snapshot(existing);
            object newValue = command.Mode == KpiSaveMode.Create
                ? new { action = "created", kpi = Snapshot(definition) }
                : new { action = "updated", changes = Diff(existing!, definition) };

            await _events.PublishKpiSettingsChangedAsync(
                definition.Id,
                command.ActorId,
                command.ActorPersona,
                oldValue,
                newValue,
                occurredAt,
                command.CorrelationId,
                ct);
        }, ct);

        return new KpiSaveResult(true, definition.Id, null);
    }

    private async Task<KpiDefinitionInput> BuildValidationInputAsync(
        KpiDefinition definition,
        KpiThreshold threshold,
        CancellationToken ct)
    {
        var all = await _definitions.ListAllAsync(ct) ?? [];
        var existingNames = all
            .Where(k => k.Id != definition.Id)
            .Select(k => k.ShortName)
            .ToList();

        return new KpiDefinitionInput
        {
            ShortName = definition.ShortName,
            FullName = definition.FullName,
            IsStandard = definition.KpiType == KpiType.Standard,
            IsComposite = definition.IsComposite,
            CalculationMethod = definition.CalculationMethod,
            TopNValue = definition.TopNValue,
            Scale = definition.Scale,
            RepresentationStyle = definition.RepresentationStyle,
            Target = definition.Target,
            IsActive = definition.IsActive,
            LowerBound = threshold.LowerBound,
            X = threshold.X,
            Y = threshold.Y,
            UpperBound = threshold.UpperBound,
            ExistingShortNames = existingNames,
        };
    }

    /// <summary>Enforces FR-004 / FR-005 immutability on edit; returns the error code or null when clean.</summary>
    private static string? CheckImmutability(KpiDefinition existing, KpiDefinition edited)
    {
        if (!string.Equals(existing.ShortName?.Trim(), edited.ShortName?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return ShortNameImmutableCode;
        }

        if (existing.KpiType == KpiType.Standard &&
            (existing.Scale != edited.Scale || existing.CalculationMethod != edited.CalculationMethod))
        {
            return FieldImmutableForStandardCode;
        }

        return null;
    }

    private static object Snapshot(KpiDefinition d) => new
    {
        id = d.Id,
        shortName = d.ShortName,
        fullName = d.FullName,
        kpiType = d.KpiType.ToString(),
        calculationMethod = d.CalculationMethod.ToString(),
        scale = d.Scale?.ToString(),
        representationStyle = d.RepresentationStyle?.ToString(),
        target = d.Target,
        isActive = d.IsActive,
        showOnDashboard = d.ShowOnDashboard,
    };

    /// <summary>Builds a field → new-value map of the differences between <paramref name="before"/> and <paramref name="after"/>.</summary>
    private static Dictionary<string, object?> Diff(KpiDefinition before, KpiDefinition after)
    {
        var changes = new Dictionary<string, object?>();

        if (before.FullName != after.FullName) changes["fullName"] = after.FullName;
        if (before.CalculationMethod != after.CalculationMethod) changes["calculationMethod"] = after.CalculationMethod.ToString();
        if (before.Scale != after.Scale) changes["scale"] = after.Scale?.ToString();
        if (before.RepresentationStyle != after.RepresentationStyle) changes["representationStyle"] = after.RepresentationStyle?.ToString();
        if (before.TopNValue != after.TopNValue) changes["topNValue"] = after.TopNValue;
        if (before.Target != after.Target) changes["target"] = after.Target;
        if (before.IsActive != after.IsActive) changes["isActive"] = after.IsActive;
        if (before.ShowOnDashboard != after.ShowOnDashboard) changes["showOnDashboard"] = after.ShowOnDashboard;

        return changes;
    }
}
