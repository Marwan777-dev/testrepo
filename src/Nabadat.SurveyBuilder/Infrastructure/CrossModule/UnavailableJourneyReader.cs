using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Domain.Interfaces;

namespace Nabadat.SurveyBuilder.Infrastructure.CrossModule;

/// <summary>
/// Placeholder <see cref="IJourneyReader"/> until M-16 exposes its published reader (T020). Fails
/// loudly (501) rather than fabricating journey/stage/touchpoint validity — so a survey with no
/// bound journey and non-KPI questions still works, while binding a journey or a KPI stage/touchpoint
/// is refused until M-16 wires the real adapter in the host.
/// </summary>
public sealed class UnavailableJourneyReader : IJourneyReader
{
    public Task<bool> JourneyExistsAsync(Guid journeyId, CancellationToken ct = default) =>
        throw new SurveyBuilderException("survey.journey_reader_unavailable", 501,
            "Journey validation (M-16) is not available yet.");

    public Task<bool> IsBindingValidAsync(string kpiCode, Guid? stageId, Guid? touchpointId, CancellationToken ct = default) =>
        throw new SurveyBuilderException("survey.journey_reader_unavailable", 501,
            "Journey binding validation (M-16) is not available yet.");
}
