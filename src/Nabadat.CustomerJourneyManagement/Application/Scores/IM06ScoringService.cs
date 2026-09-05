using Nabadat.Platform.Contracts.M16;

namespace Nabadat.CustomerJourneyManagement.Application.Scores;

/// <summary>
/// M-16's narrow consumer port for the M-06 scoring engine — the single upstream call
/// <see cref="JourneyScoreProviderService"/> makes to turn a journey's configuration into a
/// computed score tree. Declared <b>in-module</b> (there is no shared contracts project for the
/// dependency M-16 consumes), mirroring <c>IM11TenantService</c>: M-16 owns the abstraction of the
/// upstream it calls, and the composition root supplies the real M-06 adapter when that module is
/// present. Until then a throwing placeholder stands in (see <c>CustomerJourneyManagementServiceCollectionExtensions</c>), exactly
/// as M-11 is handled — a missing scoring engine surfaces as a failed computation, never silent data.
/// </summary>
public interface IM06ScoringService
{
    /// <summary>
    /// Computes the score tree for <paramref name="config"/> — the journey configuration produced by
    /// <see cref="Nabadat.Platform.Contracts.M16.IJourneyConfigReader"/>. M-06 owns the scoring
    /// algorithm; M-16 only forwards the config and persists the result. Throws when the engine is
    /// unavailable or computation fails — the caller starts no transaction and persists nothing on
    /// the failure path.
    /// </summary>
    Task<JourneyScoreResultDto> ComputeJourneyScoreAsync(JourneyConfigDto config, CancellationToken ct = default);
}
