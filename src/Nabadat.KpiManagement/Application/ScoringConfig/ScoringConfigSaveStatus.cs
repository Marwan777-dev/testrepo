namespace Nabadat.KpiManagement.Application.ScoringConfig;

/// <summary>Outcome kind of a <see cref="ScoringConfigUpdateService"/> save.</summary>
public enum ScoringConfigSaveStatus
{
    /// <summary>A field changed; M-16 persisted the row and emitted one <c>journey.scoring_config.updated</c> event.</summary>
    Updated,

    /// <summary>The payload matched current state; nothing was written and no event was emitted.</summary>
    Idempotent,

    /// <summary>Validation failed; the store was not touched.</summary>
    Failed,
}
