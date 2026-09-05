namespace Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;

/// <summary>One M-09 broadcast captured by <see cref="CapturingNotificationDispatcher"/>.</summary>
public sealed record CapturedBroadcast(string Scope, string Permission, string DeepLink, string Template);
