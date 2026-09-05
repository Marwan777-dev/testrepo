using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;

namespace Nabadat.SurveyBuilder.Infrastructure.CrossModule;

/// <summary>
/// Placeholder <see cref="IChannelSurveyRulesReader"/> until M-02 ships its published reader (T020).
/// Returns 0 — no survey has bound send-rules yet — so the F1 Library renders and Pause never
/// requires confirmation. Swap for the real M-02 adapter in the host when M-02 lands.
/// </summary>
public sealed class DevChannelSurveyRulesReader : IChannelSurveyRulesReader
{
    public Task<int> GetRulesCountAsync(Guid surveyId, CancellationToken ct = default) => Task.FromResult(0);
}
