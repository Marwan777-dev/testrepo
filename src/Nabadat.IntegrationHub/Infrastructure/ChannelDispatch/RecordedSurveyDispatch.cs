namespace Nabadat.IntegrationHub.Infrastructure.ChannelDispatch;

/// <summary>
/// One <c>ISurveyDispatchGateway.DispatchAsync</c> call captured by
/// <see cref="NullSurveyDispatchGateway"/>. Integration tests assert against this to prove SCN-01/02
/// handed off exactly once — notably the BR-18 idempotency case, where two identical requests must both be
/// logged but produce only <b>one</b> recorded dispatch.
/// </summary>
public sealed record RecordedSurveyDispatch(
    Guid SurveyId,
    Guid ServiceChannelId,
    IReadOnlyDictionary<string, string> Parameters,
    Guid RequestId);
