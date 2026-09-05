using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.Surveys.Dtos;
using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// Destructive Return-to-Draft (T072, BR-1.6). For an Active/Paused survey it flips status to Draft
/// inside <c>ITenantDbContext.ExecuteAsync</c>, then purges responses via the M-04
/// <see cref="IResponsePurgeService"/> after the commit; if the purge fails it compensates (reverts
/// the status) and rethrows. The non-destructive PendingReview → Draft path (FR-15.4) does NOT purge.
/// The audit event carries <c>purged_response_count</c>.
/// </summary>
public sealed class DestructiveReturnToDraftService
{
    private readonly ISurveyStore _surveys;
    private readonly IResponsePurgeService _purge;
    private readonly IEventLogWriter _events;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _timeProvider;

    public DestructiveReturnToDraftService(
        ISurveyStore surveys,
        IResponsePurgeService purge,
        IEventLogWriter events,
        ITenantDbContext context,
        TimeProvider timeProvider)
    {
        _surveys = surveys;
        _purge = purge;
        _events = events;
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<ReturnToDraftResult> ReturnToDraftAsync(ReturnToDraftCommand command, CancellationToken ct = default)
    {
        var survey = await _surveys.GetAsync(command.SurveyId, ct)
            ?? throw new SurveyBuilderException("survey.not_found", 404, "Survey not found.");

        var priorStatus = survey.Status;
        var priorActivatedAt = survey.ActivatedAt;
        var destructive = priorStatus is SurveyStatus.Active or SurveyStatus.Paused;

        await _context.ExecuteAsync(async () =>
        {
            survey.ChangeStatus(SurveyStatus.Draft, command.ActorId, _timeProvider.GetUtcNow());
            survey.SubmittedBy = null;
            survey.SubmittedAt = null;
            survey.ReviewedBy = null;
            survey.ReviewedAt = null;
            survey.ReviewRemarks = null;
            await _surveys.UpdateAsync(survey, ct);
        }, ct);

        var purgedCount = 0;
        if (destructive)
        {
            try
            {
                purgedCount = await _purge.PurgeSurveyResponsesAsync(
                    command.SurveyId, command.ActorId, command.CorrelationId, ct);
            }
            catch
            {
                // Compensate — the purge failed after the status commit, so revert to the prior status.
                await _context.ExecuteAsync(async () =>
                {
                    survey.ChangeStatus(priorStatus, command.ActorId, _timeProvider.GetUtcNow());
                    // Restore the original activation instant — a rollback is not a fresh start (FR-3.4).
                    survey.ActivatedAt = priorActivatedAt;
                    await _surveys.UpdateAsync(survey, ct);
                }, ct);
                throw;
            }
        }

        await _events.WriteAsync(
            new SurveyAuditEvent(
                "survey.status.changed",
                command.SurveyId,
                command.ActorId,
                command.CorrelationId,
                new Dictionary<string, object?>
                {
                    ["from_status"] = priorStatus.ToString(),
                    ["to_status"] = SurveyStatus.Draft.ToString(),
                    ["purged_response_count"] = purgedCount,
                }),
            ct);

        return new ReturnToDraftResult(destructive, purgedCount, survey.RowVersion);
    }
}
