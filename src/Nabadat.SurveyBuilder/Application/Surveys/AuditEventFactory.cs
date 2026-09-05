using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// T117 [US2] — builds exactly one <see cref="ApprovalAuditEvent"/> per approval-workflow action with
/// the correct constitution-registered event type (AMENDMENT-012) and full payload shape (actor,
/// timestamp, remarks, correlation id, previous/new status) for the M-17 audit log (FR-15.6). The
/// timestamp comes from the injected <see cref="TimeProvider"/> (Unit Test Policy rule 8) so it is
/// deterministic under test. The orchestrator (T118) emits the returned event via M-17's
/// <c>IEventLogWriter</c>.
/// </summary>
public sealed class AuditEventFactory
{
    private const string SubmittedForReviewEvent = "survey.submitted_for_review";
    private const string PublishedEvent = "survey.published";
    private const string StatusChangedEvent = "survey.status.changed";

    private readonly TimeProvider _clock;

    public AuditEventFactory(TimeProvider clock) => _clock = clock;

    /// <summary>A Draft submitted for review (Draft → PendingReview).</summary>
    public ApprovalAuditEvent Submitted(SurveyId survey, Guid actorId, Guid correlationId)
        => new(
            EventType: SubmittedForReviewEvent,
            Survey: survey,
            ActorId: actorId,
            CorrelationId: correlationId,
            PreviousStatus: SurveyStatus.Draft,
            NewStatus: SurveyStatus.PendingReview,
            Remarks: null,
            OccurredAt: _clock.GetUtcNow());

    /// <summary>A survey published to Active (from Draft direct, or PendingReview after review).</summary>
    public ApprovalAuditEvent Published(SurveyId survey, Guid actorId, Guid correlationId, SurveyStatus previousStatus, string? remarks)
        => new(
            EventType: PublishedEvent,
            Survey: survey,
            ActorId: actorId,
            CorrelationId: correlationId,
            PreviousStatus: previousStatus,
            NewStatus: SurveyStatus.Active,
            Remarks: remarks,
            OccurredAt: _clock.GetUtcNow());

    /// <summary>A PendingReview survey returned to its author with remarks (PendingReview → Draft).</summary>
    public ApprovalAuditEvent ReturnedToDraft(SurveyId survey, Guid actorId, Guid correlationId, string remarks)
        => new(
            EventType: StatusChangedEvent,
            Survey: survey,
            ActorId: actorId,
            CorrelationId: correlationId,
            PreviousStatus: SurveyStatus.PendingReview,
            NewStatus: SurveyStatus.Draft,
            Remarks: remarks,
            OccurredAt: _clock.GetUtcNow());
}
