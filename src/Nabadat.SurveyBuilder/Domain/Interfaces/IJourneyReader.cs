namespace Nabadat.SurveyBuilder.Domain.Interfaces;

/// <summary>
/// Cross-module port M-01 consumes from <b>M-16 (Customer Journey Management)</b> to validate a
/// survey's bound journey and a KPI question's stage/touchpoint binding (research.md §4.1,
/// data-model.md §4). Published-interface only — M-01 never references M-16's concrete types.
/// <para><b>Declared here per T020;</b> the concrete implementation is supplied by M-16 and wired in
/// the host composition root. The unit-tested policies take it as a mockable dependency.</para>
/// </summary>
public interface IJourneyReader
{
    /// <summary>Returns <c>true</c> when a journey with <paramref name="journeyId"/> exists in the tenant.</summary>
    Task<bool> JourneyExistsAsync(Guid journeyId, CancellationToken ct = default);

    /// <summary>
    /// Returns <c>true</c> when the (<paramref name="kpiCode"/>, <paramref name="stageId"/>,
    /// <paramref name="touchpointId"/>) binding is valid for the survey's bound journey (FR-8.4,
    /// BR-8.5). A null stage/touchpoint is valid (the binding is simply less specific).
    /// </summary>
    Task<bool> IsBindingValidAsync(string kpiCode, Guid? stageId, Guid? touchpointId, CancellationToken ct = default);
}
