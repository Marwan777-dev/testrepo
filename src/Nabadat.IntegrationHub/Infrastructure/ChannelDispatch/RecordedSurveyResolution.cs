namespace Nabadat.IntegrationHub.Infrastructure.ChannelDispatch;

/// <summary>
/// One <c>ISurveyResolutionReader.ResolveSurveyIdAsync</c> call captured by
/// <see cref="NullSurveyResolutionReader"/>, so integration tests can assert the pipeline asked M-02 the
/// right question even though no real M-02 exists to answer it.
/// </summary>
public sealed record RecordedSurveyResolution(
    Guid ServiceChannelId,
    IReadOnlyDictionary<string, string> Parameters);
