using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// Read-only projection of a survey's channel-send rule count (T071), read from the M-02
/// <see cref="IChannelSurveyRulesReader"/>. Drives the FR-1.10 Pause confirmation.
/// </summary>
public sealed class RulesCountProjection
{
    private readonly IChannelSurveyRulesReader _reader;

    public RulesCountProjection(IChannelSurveyRulesReader reader) => _reader = reader;

    public Task<int> ReadAsync(Guid surveyId, CancellationToken ct = default) =>
        _reader.GetRulesCountAsync(surveyId, ct);

    /// <summary>Pausing an Active survey requires confirmation when any rule is bound (FR-1.10).</summary>
    public static bool RequiresPauseConfirmation(int rulesCount) => rulesCount > 0;
}
