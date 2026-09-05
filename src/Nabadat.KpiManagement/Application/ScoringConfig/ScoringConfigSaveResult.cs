using Nabadat.Platform.Contracts.M16;

namespace Nabadat.KpiManagement.Application.ScoringConfig;

/// <summary>
/// Result of <see cref="ScoringConfigUpdateService.UpdateAsync"/>: the outcome kind, the resulting
/// tenant config (on <see cref="ScoringConfigSaveStatus.Updated"/>/<see cref="ScoringConfigSaveStatus.Idempotent"/>),
/// or the failing validation code (on <see cref="ScoringConfigSaveStatus.Failed"/>).
/// </summary>
public sealed record ScoringConfigSaveResult(
    ScoringConfigSaveStatus Status,
    ScoringConfigDto? Config,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static ScoringConfigSaveResult Updated(ScoringConfigDto config) =>
        new(ScoringConfigSaveStatus.Updated, config, null, null);

    public static ScoringConfigSaveResult Idempotent(ScoringConfigDto config) =>
        new(ScoringConfigSaveStatus.Idempotent, config, null, null);

    public static ScoringConfigSaveResult Failed(string code, string message) =>
        new(ScoringConfigSaveStatus.Failed, null, code, message);
}
