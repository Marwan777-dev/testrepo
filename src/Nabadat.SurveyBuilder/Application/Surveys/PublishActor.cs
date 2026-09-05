namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// The caller attempting to publish a survey — their persona <see cref="Role"/> (e.g. <c>P-01</c> /
/// <c>P-03</c>) and <see cref="UserId"/> (compared to the survey owner for the self-publish grant).
/// </summary>
public sealed record PublishActor(string Role, Guid UserId);
