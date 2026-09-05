using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Surveys;
using Nabadat.SurveyBuilder.Application.Surveys.Dtos;
using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Nabadat.SurveyBuilder.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Surveys;

/// <summary>
/// T044 [US1] — unit tests for <c>SurveyLifecycleService</c>, verifying that every self-serve status
/// transition emits <b>exactly one</b> M-17 audit event through the <c>IEventLogWriter</c> port with
/// the correct <c>EventType</c> (a transition into Active emits <c>survey.published</c>; Archive emits
/// <c>survey.archived</c> — constitution AMENDMENT-012 / T022).
/// <para>
/// Contract pinned for the implementer (T073):
/// <list type="bullet">
///   <item><c>SurveyLifecycleService</c> lives in <c>Application/Surveys/</c> and orchestrates the
///   self-serve transitions by composing <c>ISurveyStore</c>, <c>StatusTransitionPolicy</c>,
///   <c>PublishGateService</c>, <c>RulesCountProjection</c>, <c>SurveyTypeSyncService</c>,
///   <c>DestructiveReturnToDraftService</c>, <c>IEventLogWriter</c>, <c>ITenantDbContext</c>, and an
///   injected <see cref="TimeProvider"/>.</item>
///   <item><c>Task&lt;SurveyTransitionResult&gt; ChangeStatusAsync(SurveyStatusChangeCommand command,
///   CancellationToken ct = default)</c> where
///   <c>SurveyStatusChangeCommand(Guid SurveyId, SurveyStatus TargetStatus, Guid ActorId,
///   string ActorRole, Guid CorrelationId, bool Confirm = false)</c>.</item>
///   <item><c>ISurveyStore</c> (in <c>Application/Surveys/Interfaces/</c>) exposes at least
///   <c>Task&lt;Survey?&gt; GetAsync(Guid, CancellationToken)</c>,
///   <c>Task&lt;SurveyContentCounts&gt; GetContentCountsAsync(Guid, CancellationToken)</c>, and
///   <c>Task UpdateAsync(Survey, CancellationToken)</c>.</item>
///   <item><c>IEventLogWriter</c> (M-17 port, in <c>Domain/Interfaces/</c>):
///   <c>Task WriteAsync(SurveyAuditEvent auditEvent, CancellationToken ct = default)</c> with
///   <c>SurveyAuditEvent</c> carrying a <c>string EventType</c> (+ survey/actor/correlation ids and
///   payload).</item>
///   <item><see cref="Survey"/> gains (T053) a settable <c>SurveyStatus Status</c>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class SurveyLifecycleServiceTests
{
    private readonly ISurveyStore _surveys = Substitute.For<ISurveyStore>();
    private readonly IChannelSurveyRulesReader _rulesReader = Substitute.For<IChannelSurveyRulesReader>();
    private readonly IResponsePurgeService _purge = Substitute.For<IResponsePurgeService>();
    private readonly IEventLogWriter _events = Substitute.For<IEventLogWriter>();
    private readonly RecordingTenantDbContext _context = new();

    private SurveyLifecycleService CreateService() => new(
        _surveys,
        new StatusTransitionPolicy(),
        new PublishGateService(),
        new RulesCountProjection(_rulesReader),
        new SurveyTypeSyncService(),
        new DestructiveReturnToDraftService(_surveys, _purge, _events, _context, TestTime.Provider()),
        _events,
        _context,
        TestTime.Provider());

    [Fact]
    public async Task ChangeStatusAsync_emits_exactly_one_survey_published_event_when_a_draft_is_published()
    {
        var survey = new Survey { Id = Guid.NewGuid(), Status = SurveyStatus.Draft };
        _surveys.GetAsync(survey.Id, Arg.Any<CancellationToken>()).Returns(survey);
        _surveys.GetContentCountsAsync(survey.Id, Arg.Any<CancellationToken>())
            .Returns(new SurveyContentCounts(SectionsCount: 1, QuestionsCount: 1));

        var service = CreateService();
        await service.ChangeStatusAsync(new SurveyStatusChangeCommand(
            survey.Id, SurveyStatus.Active, ActorId: Guid.NewGuid(),
            ActorRole: SurveyStatusTransitions.ManagerRole, CorrelationId: Guid.NewGuid()));

        await _events.Received(1).WriteAsync(
            Arg.Is<SurveyAuditEvent>(e => e.EventType == "survey.published"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeStatusAsync_emits_exactly_one_survey_archived_event_when_a_survey_is_archived()
    {
        var survey = new Survey { Id = Guid.NewGuid(), Status = SurveyStatus.Active };
        _surveys.GetAsync(survey.Id, Arg.Any<CancellationToken>()).Returns(survey);
        _surveys.GetContentCountsAsync(survey.Id, Arg.Any<CancellationToken>())
            .Returns(new SurveyContentCounts(SectionsCount: 1, QuestionsCount: 1));

        var service = CreateService();
        await service.ChangeStatusAsync(new SurveyStatusChangeCommand(
            survey.Id, SurveyStatus.Archived, ActorId: Guid.NewGuid(),
            ActorRole: SurveyStatusTransitions.ManagerRole, CorrelationId: Guid.NewGuid()));

        await _events.Received(1).WriteAsync(
            Arg.Is<SurveyAuditEvent>(e => e.EventType == "survey.archived"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeStatusAsync_stamps_activated_at_with_the_clock_when_a_draft_is_published()
    {
        var survey = new Survey { Id = Guid.NewGuid(), Status = SurveyStatus.Draft };
        _surveys.GetAsync(survey.Id, Arg.Any<CancellationToken>()).Returns(survey);
        _surveys.GetContentCountsAsync(survey.Id, Arg.Any<CancellationToken>())
            .Returns(new SurveyContentCounts(SectionsCount: 1, QuestionsCount: 1));

        var service = CreateService();
        await service.ChangeStatusAsync(new SurveyStatusChangeCommand(
            survey.Id, SurveyStatus.Active, ActorId: Guid.NewGuid(),
            ActorRole: SurveyStatusTransitions.ManagerRole, CorrelationId: Guid.NewGuid()));

        // FR-3.4 — entering Active records the active-period start instant.
        survey.ActivatedAt.Should().Be(TestTime.Anchor);
    }

    [Fact]
    public async Task ChangeStatusAsync_leaves_activated_at_null_on_a_non_active_transition()
    {
        var survey = new Survey { Id = Guid.NewGuid(), Status = SurveyStatus.Active, ActivatedAt = null };
        _surveys.GetAsync(survey.Id, Arg.Any<CancellationToken>()).Returns(survey);
        _surveys.GetContentCountsAsync(survey.Id, Arg.Any<CancellationToken>())
            .Returns(new SurveyContentCounts(SectionsCount: 1, QuestionsCount: 1));

        var service = CreateService();
        await service.ChangeStatusAsync(new SurveyStatusChangeCommand(
            survey.Id, SurveyStatus.Archived, ActorId: Guid.NewGuid(),
            ActorRole: SurveyStatusTransitions.ManagerRole, CorrelationId: Guid.NewGuid()));

        // Only a transition INTO Active stamps the start instant.
        survey.ActivatedAt.Should().BeNull();
    }
}
