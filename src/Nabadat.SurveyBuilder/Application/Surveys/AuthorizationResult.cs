namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// Result of <see cref="PublishAuthorizationService.AuthorizeAsync"/>: whether the actor is allowed
/// to publish and — when refused — the API-05 <see cref="DenialCode"/> (<c>survey.publish.forbidden</c>)
/// the endpoint returns (403).
/// </summary>
public sealed record AuthorizationResult(bool IsAuthorized, string? DenialCode);
