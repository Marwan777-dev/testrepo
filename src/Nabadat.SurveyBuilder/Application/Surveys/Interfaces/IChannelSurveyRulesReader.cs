namespace Nabadat.SurveyBuilder.Application.Surveys.Interfaces;

/// <summary>
/// Cross-module port M-01 consumes from <b>M-02 (Channels &amp; Dispatch)</b> to read how many
/// channel-send rules reference a survey — used to decide whether Pause needs the FR-1.10
/// confirmation and to surface <c>rules_count</c> in the F1 Library row.
/// <para><b>Declared here per T020;</b> the concrete implementation is supplied by M-02 (which does
/// not exist under <c>src/</c> yet) and wired in the host composition root. Until then a no-op stub
/// returning 0 is registered (see the composition root); the unit-tested projection mocks it.</para>
/// </summary>
public interface IChannelSurveyRulesReader
{
    /// <summary>Number of channel-send rules bound to <paramref name="surveyId"/>.</summary>
    Task<int> GetRulesCountAsync(Guid surveyId, CancellationToken ct = default);
}
