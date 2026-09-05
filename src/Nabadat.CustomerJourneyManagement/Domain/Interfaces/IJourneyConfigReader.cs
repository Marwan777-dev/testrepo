namespace Nabadat.Platform.Contracts.M16;

/// <summary>
/// Published interface: M-16 → M-06.
/// M-06 calls this to retrieve journey configuration (KPI bindings + structure) for score computation.
/// The <b>tenant-level</b> scoring parameters are NOT journey-scoped (SRS §4.2.9 / §11.7, Q11) and are
/// read separately via <see cref="IScoringConfigStore"/>. M-06 MUST NOT read M-16 tables directly.
/// </summary>
public interface IJourneyConfigReader
{
    /// <summary>
    /// Returns the journey configuration (KPI bindings + stage/touchpoint structure).
    /// Returns null if the journey does not exist.
    /// </summary>
    Task<JourneyConfigDto?> GetJourneyConfigAsync(Guid journeyId, CancellationToken ct = default);

    /// <summary>
    /// Returns all active journeys for the current tenant.
    /// Used by M-06 to batch-process score updates.
    /// </summary>
    Task<IReadOnlyList<JourneyConfigDto>> GetActiveJourneyConfigsAsync(CancellationToken ct = default);
}

public record JourneyConfigDto(
    Guid JourneyId,
    string Name,
    JourneyConfigStatus Status,
    IReadOnlyList<StageConfigDto> Stages
);

public record StageConfigDto(
    Guid StageId,
    int SequenceNumber,
    string Name,
    IReadOnlyList<TouchpointConfigDto> Touchpoints
);

public record TouchpointConfigDto(
    Guid TouchpointId,
    string Name,
    bool IsMoT,
    bool IsMandatory,
    bool IsMeasured,
    IReadOnlyList<KpiBindingConfigDto> KpiBindings
);

public record KpiBindingConfigDto(
    string KpiType,
    decimal Weight,
    bool IsPlatformStandard,
    ScoringDirection ScoringDirection
);

public enum JourneyConfigStatus { Draft, Active, Inactive, Archived }

public enum ScoringDirection { Ascending, Descending }
