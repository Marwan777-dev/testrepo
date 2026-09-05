namespace Nabadat.IntegrationHub.Infrastructure.ChannelDispatch;

/// <summary>
/// One <c>IResponseIngestionGateway.ForwardResponseAsync</c> call captured by
/// <see cref="NullResponseIngestionGateway"/>. Because there is no real M-04 to verify durability against
/// yet, asserting on this record is the <b>only</b> way an integration test can prove SCN-05 forwarded the
/// exact payload — including the BR-18 case where a retry must not produce a second forward.
/// </summary>
public sealed record RecordedResponseIngestion(
    Guid ServiceChannelId,
    string TransactionId,
    IReadOnlyDictionary<string, string> Parameters,
    object SurveyResponse);
