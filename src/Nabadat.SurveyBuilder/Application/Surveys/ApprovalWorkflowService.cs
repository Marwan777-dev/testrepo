using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.Surveys.Dtos;
using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// Orchestrates the US2 approval workflow (T118): submit-for-review, publish, and non-destructive
/// return-to-draft. It composes the pure US2 policies — <see cref="ApprovalStateMachine"/> (target
/// status), <see cref="PublishAuthorizationService"/> (grant/ownership gate), <see cref="PublishGateService"/>
/// (BR-1.7 content gate, which also guards submit), <see cref="ReviewNotificationBuilder"/> +
/// <see cref="INotificationDispatcher"/> (Q7 reviewer broadcast) and <see cref="AuditEventFactory"/>
/// (M-17 audit payload) — persists the transition inside a single
/// <see cref="ITenantDbContext.ExecuteAsync"/> boundary, then emits exactly one audit event per action.
/// The destructive Active/Paused → Draft path is NOT here — it lives in <see cref="SurveyLifecycleService"/>
/// (BR-1.6).
/// </summary>
public sealed class ApprovalWorkflowService
{
    private readonly ISurveyStore _surveys;
    private readonly ApprovalStateMachine _stateMachine;
    private readonly PublishAuthorizationService _publishAuth;
    private readonly PublishGateService _publishGate;
    private readonly ReviewNotificationBuilder _notificationBuilder;
    private readonly INotificationDispatcher _dispatcher;
    private readonly AuditEventFactory _auditFactory;
    private readonly IEventLogWriter _events;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _timeProvider;

    public ApprovalWorkflowService(
        ISurveyStore surveys,
        ApprovalStateMachine stateMachine,
        PublishAuthorizationService publishAuth,
        PublishGateService publishGate,
        ReviewNotificationBuilder notificationBuilder,
        INotificationDispatcher dispatcher,
        AuditEventFactory auditFactory,
        IEventLogWriter events,
        ITenantDbContext context,
        TimeProvider timeProvider)
    {
        _surveys = surveys;
        _stateMachine = stateMachine;
        _publishAuth = publishAuth;
        _publishGate = publishGate;
        _notificationBuilder = notificationBuilder;
        _dispatcher = dispatcher;
        _auditFactory = auditFactory;
        _events = events;
        _context = context;
        _timeProvider = timeProvider;
    }

    /// <summary>P-03 submits a Draft for review (FR-15.1): Draft → PendingReview, lock + notify reviewers.</summary>
    public async Task<SurveyTransitionResult> SubmitAsync(SubmitForReviewCommand command, CancellationToken ct = default)
    {
        var survey = await LoadAsync(command.SurveyId, ct);

        if (survey.Status != SurveyStatus.Draft)
        {
            throw new SurveyBuilderException(
                "survey.status.invalid_transition", 409,
                $"Only a Draft survey can be submitted for review (current: {survey.Status}).");
        }

        // BR-1.7 also gates submit (defensive: refuse submitting a survey that would fail at publish).
        await EnsurePublishContentAsync(command.SurveyId, SurveyStatus.Draft, ct);

        var outcome = _stateMachine.Submit(survey.Status, command.ActorRole);
        var now = _timeProvider.GetUtcNow();

        await _context.ExecuteAsync(async () =>
        {
            survey.SubmittedBy = command.ActorId;
            survey.SubmittedAt = now;
            survey.ChangeStatus(outcome.NewStatus, command.ActorId, now);
            await _surveys.UpdateAsync(survey, ct);
        }, ct);

        await _events.WriteAsync(
            ToAuditEvent(_auditFactory.Submitted(new SurveyId(command.SurveyId), command.ActorId, command.CorrelationId)), ct);

        // Q7 broadcast: one notification per tenant user holding survey.publish, deep-linked to Settings.
        var broadcast = _notificationBuilder.Build(new SurveyId(command.SurveyId), command.ActorId);
        await _dispatcher.BroadcastAsync(broadcast.Scope, broadcast.Permission, broadcast.DeepLink, broadcast.Template, ct);

        return new SurveyTransitionResult(outcome.NewStatus, survey.RowVersion);
    }

    /// <summary>Publish (FR-15.5): reviewer, or grant-holding author of their own survey, moves it to Active.</summary>
    public async Task<SurveyTransitionResult> PublishAsync(PublishSurveyCommand command, CancellationToken ct = default)
    {
        var survey = await LoadAsync(command.SurveyId, ct);

        if (survey.Status is not (SurveyStatus.Draft or SurveyStatus.PendingReview))
        {
            throw new SurveyBuilderException(
                "survey.status.invalid_transition", 409,
                $"Only a Draft or Pending-review survey can be published (current: {survey.Status}).");
        }

        var authorization = await _publishAuth.AuthorizeAsync(
            new PublishActor(command.ActorRole, command.ActorId),
            new SurveyApprovalInfo(survey.Status, survey.OwnerUserId), ct);
        if (!authorization.IsAuthorized)
        {
            throw new SurveyBuilderException(
                authorization.DenialCode ?? "survey.publish.forbidden", 403,
                "You are not permitted to publish this survey.");
        }

        var previous = survey.Status;
        await EnsurePublishContentAsync(command.SurveyId, previous, ct); // BR-1.7 content gate.

        var now = _timeProvider.GetUtcNow();
        await _context.ExecuteAsync(async () =>
        {
            survey.ReviewedBy = command.ActorId;
            survey.ReviewedAt = now;
            if (!string.IsNullOrWhiteSpace(command.Remarks))
            {
                survey.ReviewRemarks = command.Remarks;
            }

            survey.ChangeStatus(SurveyStatus.Active, command.ActorId, now);
            await _surveys.UpdateAsync(survey, ct);
        }, ct);

        await _events.WriteAsync(
            ToAuditEvent(_auditFactory.Published(new SurveyId(command.SurveyId), command.ActorId, command.CorrelationId, previous, command.Remarks)), ct);

        return new SurveyTransitionResult(SurveyStatus.Active, survey.RowVersion);
    }

    /// <summary>Non-destructive return-to-draft (FR-15.3): reviewer sends a PendingReview survey back with remarks.</summary>
    public async Task<SurveyTransitionResult> ReturnToDraftAsync(ReturnForRevisionCommand command, CancellationToken ct = default)
    {
        var survey = await LoadAsync(command.SurveyId, ct);

        if (survey.Status != SurveyStatus.PendingReview)
        {
            throw new SurveyBuilderException(
                "survey.status.invalid_transition", 409,
                $"Only a Pending-review survey can be returned to Draft here (current: {survey.Status}). "
                + "The destructive Active/Paused → Draft path uses POST /status.");
        }

        if (string.IsNullOrWhiteSpace(command.Remarks))
        {
            throw new SurveyBuilderException(
                "survey.return_to_draft.remarks_required", 400,
                "Reviewer remarks are required when returning a survey to Draft (FR-15.3).");
        }

        var outcome = _stateMachine.ReturnToDraft(survey.Status, command.ActorRole, command.Remarks);
        var now = _timeProvider.GetUtcNow();

        await _context.ExecuteAsync(async () =>
        {
            survey.ReviewedBy = command.ActorId;
            survey.ReviewedAt = now;
            survey.ReviewRemarks = command.Remarks;
            survey.ChangeStatus(outcome.NewStatus, command.ActorId, now);
            await _surveys.UpdateAsync(survey, ct);
        }, ct);

        await _events.WriteAsync(
            ToAuditEvent(_auditFactory.ReturnedToDraft(new SurveyId(command.SurveyId), command.ActorId, command.CorrelationId, command.Remarks)), ct);

        return new SurveyTransitionResult(outcome.NewStatus, survey.RowVersion);
    }

    private async Task<Domain.Entities.Survey> LoadAsync(Guid surveyId, CancellationToken ct) =>
        await _surveys.GetAsync(surveyId, ct)
        ?? throw new SurveyBuilderException("survey.not_found", 404, "Survey not found.");

    private async Task EnsurePublishContentAsync(Guid surveyId, SurveyStatus current, CancellationToken ct)
    {
        var gate = _publishGate.EnsureContent(
            await _surveys.GetContentCountsAsync(surveyId, ct), current, SurveyStatus.Active);
        if (gate.Gated && !gate.IsSatisfied)
        {
            throw new SurveyBuilderException(
                gate.ErrorCode!, 409, "The survey needs at least one section and one question before publishing.",
                new Dictionary<string, object>
                {
                    ["missing_sections"] = gate.MissingSections,
                    ["missing_questions"] = gate.MissingQuestions,
                });
        }
    }

    // Map the US2 ApprovalAuditEvent (T117) onto the M-17 SurveyAuditEvent the IEventLogWriter port accepts.
    private static SurveyAuditEvent ToAuditEvent(ApprovalAuditEvent e) =>
        new(e.EventType, e.Survey.Value, e.ActorId, e.CorrelationId,
            new Dictionary<string, object?>
            {
                ["from_status"] = e.PreviousStatus.ToString(),
                ["to_status"] = e.NewStatus.ToString(),
                ["remarks"] = e.Remarks,
            });
}
