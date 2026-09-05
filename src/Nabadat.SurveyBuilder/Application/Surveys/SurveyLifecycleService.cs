using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.Surveys.Dtos;
using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// Orchestrates all self-serve survey status transitions (T073, Pause / Reactivate / Archive /
/// Unarchive / destructive Return-to-Draft) by composing the transition policy, publish gate, rules
/// projection, destructive-return service, and M-17 audit writer. Emits exactly one event per
/// transition: <c>survey.published</c> into Active, <c>survey.archived</c> into Archived, else
/// <c>survey.status.changed</c> (constitution AMENDMENT-012).
/// </summary>
public sealed class SurveyLifecycleService
{
    private readonly ISurveyStore _surveys;
    private readonly StatusTransitionPolicy _transitions;
    private readonly PublishGateService _publishGate;
    private readonly RulesCountProjection _rulesCount;
    private readonly SurveyTypeSyncService _typeSync;
    private readonly DestructiveReturnToDraftService _returnToDraft;
    private readonly IEventLogWriter _events;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _timeProvider;

    public SurveyLifecycleService(
        ISurveyStore surveys,
        StatusTransitionPolicy transitions,
        PublishGateService publishGate,
        RulesCountProjection rulesCount,
        SurveyTypeSyncService typeSync,
        DestructiveReturnToDraftService returnToDraft,
        IEventLogWriter events,
        ITenantDbContext context,
        TimeProvider timeProvider)
    {
        _surveys = surveys;
        _transitions = transitions;
        _publishGate = publishGate;
        _rulesCount = rulesCount;
        _typeSync = typeSync;
        _returnToDraft = returnToDraft;
        _events = events;
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<SurveyTransitionResult> ChangeStatusAsync(SurveyStatusChangeCommand command, CancellationToken ct = default)
    {
        var survey = await _surveys.GetAsync(command.SurveyId, ct)
            ?? throw new SurveyBuilderException("survey.not_found", 404, "Survey not found.");

        var current = survey.Status;
        var target = command.TargetStatus;
        var isDestructive = target == SurveyStatus.Draft && current is SurveyStatus.Active or SurveyStatus.Paused;

        // Destructive Return-to-Draft is delegated (BR-1.6) — it owns the purge + compensation.
        if (isDestructive)
        {
            if (!command.Confirm)
            {
                throw new SurveyBuilderException(
                    "survey.return_to_draft.destructive_confirmation_required", 409,
                    "Returning this survey to Draft permanently deletes all collected responses.");
            }

            var returned = await _returnToDraft.ReturnToDraftAsync(
                new ReturnToDraftCommand(command.SurveyId, command.ActorId, command.CorrelationId), ct);
            return new SurveyTransitionResult(SurveyStatus.Draft, returned.RowVersion);
        }

        // Archived is terminal except Unarchive → Draft (BR-1.3).
        if (current == SurveyStatus.Archived && target != SurveyStatus.Draft)
        {
            throw new SurveyBuilderException(
                "survey.archived.only_unarchive_allowed", 409, "An archived survey can only be unarchived to Draft.");
        }

        if (!_transitions.Allowed(current, target, command.ActorRole, isDestructive: false))
        {
            throw new SurveyBuilderException(
                "survey.status.invalid_transition", 409, $"Transition {current} → {target} is not permitted.");
        }

        // BR-1.7 publish gate (Draft/PendingReview → Active only).
        var gate = _publishGate.EnsureContent(
            await _surveys.GetContentCountsAsync(command.SurveyId, ct), current, target);
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

        // FR-1.10 Pause-with-rules confirmation.
        if (target == SurveyStatus.Paused && current == SurveyStatus.Active)
        {
            var rulesCount = await _rulesCount.ReadAsync(command.SurveyId, ct);
            if (RulesCountProjection.RequiresPauseConfirmation(rulesCount) && !command.Confirm)
            {
                throw new SurveyBuilderException(
                    "survey.pause.requires_rules_confirmation", 409,
                    "This survey has active send rules; confirm to pause.",
                    new Dictionary<string, object> { ["rules_count"] = rulesCount });
            }
        }

        var now = _timeProvider.GetUtcNow();
        await _context.ExecuteAsync(async () =>
        {
            survey.ChangeStatus(target, command.ActorId, now);
            await _surveys.UpdateAsync(survey, ct);
        }, ct);

        await _events.WriteAsync(BuildEvent(command, current, target), ct);

        return new SurveyTransitionResult(target, survey.RowVersion);
    }

    private static SurveyAuditEvent BuildEvent(SurveyStatusChangeCommand command, SurveyStatus from, SurveyStatus to)
    {
        var eventType = to switch
        {
            SurveyStatus.Active => "survey.published",
            SurveyStatus.Archived => "survey.archived",
            _ => "survey.status.changed",
        };

        return new SurveyAuditEvent(
            eventType,
            command.SurveyId,
            command.ActorId,
            command.CorrelationId,
            new Dictionary<string, object?>
            {
                ["from_status"] = from.ToString(),
                ["to_status"] = to.ToString(),
            });
    }
}
