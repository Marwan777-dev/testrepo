using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nabadat.SurveyBuilder.Application.Surveys;
using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Nabadat.UserManagement.Api.Interfaces;

namespace Nabadat.SurveyBuilder.Api.Filters;

/// <summary>
/// Enforces both survey edit-locks before a settings mutation (T082 + TODO-M01-015). Applied to
/// <c>PUT /surveys/{id}</c> via <c>[ServiceFilter(typeof(EditLockFilter))]</c>:
/// <list type="bullet">
/// <item><b>BR-1.5</b> — an <c>Active</c> or <c>Paused</c> survey is not directly editable; it must
/// first be Returned to Draft (409 <c>survey.edit_locked</c>).</item>
/// <item><b>BR-15.1</b> — while a survey is <c>PendingReview</c> its <b>submitter</b> (a P-03) is
/// edit-locked (403 <c>survey.edit_locked_by_pending_review</c>), evaluated by
/// <see cref="EditLockPolicy"/>. The <b>reviewer</b> (P-01) may still edit before publishing; those
/// edits pass through with an <c>X-Warning: survey.edit_during_review</c> header so the UI can flag
/// them (contract § "Edit-lock behaviour on PendingReview").</item>
/// </list>
/// </summary>
public sealed class EditLockFilter : IAsyncActionFilter
{
    private readonly ISurveyStore _surveys;
    private readonly EditLockPolicy _editLock;
    private readonly ISessionContextAccessor _session;

    public EditLockFilter(ISurveyStore surveys, EditLockPolicy editLock, ISessionContextAccessor session)
    {
        _surveys = surveys;
        _editLock = editLock;
        _session = session;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.RouteData.Values.TryGetValue("id", out var raw)
            && Guid.TryParse(raw?.ToString(), out var surveyId))
        {
            var survey = await _surveys.GetAsync(surveyId, context.HttpContext.RequestAborted);
            if (survey is not null)
            {
                // BR-1.5 — Active/Paused surveys must be returned to Draft before editing.
                if (survey.Status is SurveyStatus.Active or SurveyStatus.Paused)
                {
                    context.Result = ErrorResult(
                        StatusCodes.Status409Conflict,
                        "survey.edit_locked",
                        "Return the survey to Draft before editing (BR-1.5).");
                    return;
                }

                // BR-15.1 — the P-03 submitter cannot edit their own survey while it is PendingReview.
                var callerRole = _session.Current?.Persona ?? "P-00";
                var callerUserId = _session.Current?.UserId ?? Guid.Empty;
                var lockState = new EditLockState(survey.Status, survey.SubmittedBy);
                var lockResult = _editLock.Evaluate(callerRole, callerUserId, lockState);
                if (!lockResult.CanEdit)
                {
                    context.Result = ErrorResult(
                        StatusCodes.Status403Forbidden,
                        lockResult.Reason!,
                        "You cannot edit this survey while it is pending review (BR-15.1).");
                    return;
                }

                // The reviewer (P-01) may edit while PendingReview — flag it for the UI (BR-15.1).
                if (survey.Status == SurveyStatus.PendingReview && callerRole == "P-01")
                {
                    context.HttpContext.Response.Headers["X-Warning"] = "survey.edit_during_review";
                }
            }
        }

        await next();
    }

    private static ObjectResult ErrorResult(int statusCode, string code, string message) =>
        new(new { error = new { code, message } }) { StatusCode = statusCode };
}
