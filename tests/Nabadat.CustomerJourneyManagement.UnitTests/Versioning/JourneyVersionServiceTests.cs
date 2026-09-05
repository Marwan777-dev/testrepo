using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Versioning;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using NSubstitute;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.UnitTests.Versioning;

/// <summary>
/// Unit tests for <see cref="JourneyVersionService"/> (T067 / US-3) — the publish/read orchestration
/// for immutable journey versions. Authored FIRST (red→green); they pin the contract the T067
/// implementation must satisfy:
/// <list type="bullet">
///   <item>An <c>IJourneySnapshotBuilder</c> seam (<c>Task&lt;JourneySnapshotInput?&gt;
///   BuildAsync(Guid journeyId, CancellationToken = default)</c>, null when the journey does not
///   exist) loads the journey tree via raw tenant-schema SQL — kept behind a port so the publish
///   orchestration is unit-testable without a database (the raw read is integration-tested, like
///   <c>JourneyConfigReaderService</c>). The real <see cref="JourneySnapshotSerializer"/> turns that
///   input into the stored blob.</item>
///   <item><c>JourneyVersionService(IJourneySnapshotBuilder, JourneySnapshotSerializer,
///   IVersionDataService, ITransactionRunner, IM17EventPublisher, TimeProvider)</c>.</item>
///   <item><c>Task&lt;ServiceResult&lt;int&gt;&gt; PublishJourneyVersionAsync(Guid journeyId,
///   ActorContext, CancellationToken = default)</c> — serializes the snapshot, writes a
///   <c>journey_versions</c> row at <c>max(version_number)+1</c>, and publishes
///   <c>journey.version.published</c> in the SAME transaction (FR-015); returns the new number.</item>
///   <item><c>Task&lt;ServiceResult&lt;JourneyVersion&gt;&gt; GetJourneyVersionAsync(Guid journeyId,
///   int versionNumber, CancellationToken = default)</c> — returns the stored snapshot verbatim, or
///   <c>journey.version_not_found</c>.</item>
/// </list>
/// </summary>
public sealed class JourneyVersionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly ActorContext Actor = new(
        UserId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Persona: "P-01",
        CorrelationId: Guid.Parse("55555555-5555-5555-5555-555555555555"));

    private readonly IJourneySnapshotBuilder _snapshots = Substitute.For<IJourneySnapshotBuilder>();
    private readonly IVersionDataService _versions = Substitute.For<IVersionDataService>();
    private readonly IM17EventPublisher _events = Substitute.For<IM17EventPublisher>();
    private readonly FakeTimeProvider _time = new(Now);

    private JourneyVersionService CreateSut() => new(
        _snapshots,
        new JourneySnapshotSerializer(),
        _versions,
        TestSupport.FakeTenantDb.Immediate(),
        _events,
        _time);

    private static JourneySnapshotInput SnapshotInputFor(Guid journeyId)
    {
        var journey = new Journey { JourneyId = journeyId, Name = "Customer Onboarding", JourneyType = "Onboarding", Status = "Active" };
        var stage = new Stage { StageId = Guid.NewGuid(), JourneyId = journeyId, SequenceNumber = 1, Name = "Awareness" };
        return new JourneySnapshotInput(
            journey,
            ScoringConfig: null,
            DetectionConfig: null,
            Stages: [new StageSnapshotInput(stage, [])]);
    }

    [Fact]
    public async Task PublishJourneyVersionAsync_writes_next_version_number_and_publishes_event_in_one_transaction()
    {
        var journeyId = Guid.NewGuid();
        _snapshots.BuildAsync(journeyId, Arg.Any<CancellationToken>()).Returns(SnapshotInputFor(journeyId));
        // Three versions already exist → the new one must be number 4.
        _versions.GetMaxVersionNumberAsync(journeyId, Arg.Any<CancellationToken>()).Returns(3);

        var result = await CreateSut().PublishJourneyVersionAsync(journeyId, Actor);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(4);
        await _versions.Received(1).CreateAsync(
            Arg.Is<JourneyVersion>(v =>
                v.JourneyId == journeyId
                && v.VersionNumber == 4
                && v.PublishedBy == Actor.UserId
                && !string.IsNullOrEmpty(v.SnapshotPayload)),
            Arg.Any<CancellationToken>());
        await _events.Received(1).PublishAsync(
            Arg.Is<CustomerJourneyManagementEvent>(e =>
                e.EventType == CustomerJourneyManagementEventTypes.JourneyVersionPublished
                && e.ActorId == Actor.UserId
                && e.CorrelationId == Actor.CorrelationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishJourneyVersionAsync_fails_with_not_found_and_writes_nothing_when_journey_is_absent()
    {
        var journeyId = Guid.NewGuid();
        _snapshots.BuildAsync(journeyId, Arg.Any<CancellationToken>()).Returns((JourneySnapshotInput?)null);

        var result = await CreateSut().PublishJourneyVersionAsync(journeyId, Actor);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("journey.not_found");
        await _versions.DidNotReceive().CreateAsync(
            Arg.Any<JourneyVersion>(), Arg.Any<CancellationToken>());
        await _events.DidNotReceive().PublishAsync(
            Arg.Any<CustomerJourneyManagementEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetJourneyVersionAsync_returns_the_exact_stored_snapshot_payload()
    {
        // GetJourneyVersion returns the frozen blob captured at publish time — never a freshly
        // recomputed tree — so later edits to the live journey can never alter a historical version.
        var journeyId = Guid.NewGuid();
        const string storedPayload = """{"journeyId":"x","name":"Customer Onboarding","stages":[]}""";
        var stored = new JourneyVersion
        {
            VersionId = Guid.NewGuid(),
            JourneyId = journeyId,
            VersionNumber = 2,
            PublishedBy = Actor.UserId,
            PublishedAt = Now,
            SnapshotPayload = storedPayload,
        };
        _versions.GetByVersionNumberAsync(journeyId, 2, Arg.Any<CancellationToken>()).Returns(stored);

        var result = await CreateSut().GetJourneyVersionAsync(journeyId, 2);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SnapshotPayload.Should().Be(storedPayload);
        result.Value.VersionNumber.Should().Be(2);
    }

    [Fact]
    public async Task GetJourneyVersionAsync_fails_with_version_not_found_when_absent()
    {
        var journeyId = Guid.NewGuid();
        _versions.GetByVersionNumberAsync(journeyId, 99, Arg.Any<CancellationToken>())
            .Returns((JourneyVersion?)null);

        var result = await CreateSut().GetJourneyVersionAsync(journeyId, 99);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("journey.version_not_found");
    }
}
