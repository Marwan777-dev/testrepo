namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// A fan-out notification descriptor: notify every user in <see cref="Scope"/> who holds
/// <see cref="Permission"/>, linking them to <see cref="DeepLink"/>, rendered from
/// <see cref="Template"/>. Produced by <see cref="ReviewNotificationBuilder"/> and handed to
/// <see cref="Nabadat.SurveyBuilder.Domain.Interfaces.INotificationDispatcher"/> (M-09) by the
/// approval orchestrator (T118).
/// </summary>
public sealed record NotificationBroadcast(string Scope, string Permission, string DeepLink, string Template);
