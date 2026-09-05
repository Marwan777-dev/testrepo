using Microsoft.AspNetCore.Mvc.Filters;

namespace Nabadat.SurveyBuilder.Api.Filters;

/// <summary>
/// Marker filter for the BR-1.7 publish content gate (T082). The authoritative enforcement lives in
/// <c>SurveyLifecycleService.ChangeStatusAsync</c> (it needs the target status from the request body
/// plus the live section/question counts, and returns the structured
/// <c>publish.requires_content</c> details), so this filter is a documented no-op placeholder kept
/// for symmetry with <see cref="EditLockFilter"/> and future pre-action checks. Applied to the
/// status endpoint via <c>[ServiceFilter(typeof(PublishGateFilter))]</c>.
/// </summary>
public sealed class PublishGateFilter : IAsyncActionFilter
{
    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next) => next();
}
