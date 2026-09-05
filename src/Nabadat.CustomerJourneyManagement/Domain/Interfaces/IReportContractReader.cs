namespace Nabadat.Platform.Contracts.M16;

/// <summary>
/// Published interface: M-16 → M-07.
/// M-07 calls this to retrieve report contract metadata.
/// M-07 MUST NOT read M-16 tables directly.
/// </summary>
public interface IReportContractReader
{
    /// <summary>
    /// Returns the report contract for a journey.
    /// Returns null if the journey does not exist or has no contract yet.
    /// </summary>
    Task<ReportContractDto?> GetReportContractAsync(Guid journeyId, CancellationToken ct = default);

    /// <summary>
    /// Returns report contracts for all active journeys in the current tenant.
    /// </summary>
    Task<IReadOnlyList<ReportContractDto>> GetActiveReportContractsAsync(CancellationToken ct = default);
}

public record ReportContractDto(
    Guid JourneyId,
    string JourneyName,
    DateTime GeneratedAt,
    IReadOnlyList<StageReportDto> Stages,
    IReadOnlyList<string> ScoreDimensions,
    DetectionConfigReportDto DetectionConfig
);

public record StageReportDto(
    Guid StageId,
    string Name,
    int SequenceNumber,
    IReadOnlyList<TouchpointReportDto> Touchpoints
);

public record TouchpointReportDto(
    Guid TouchpointId,
    string Name,
    bool IsMoT,
    IReadOnlyList<string> KpiTypes,
    bool IsMeasured
);

public record DetectionConfigReportDto(
    decimal? PainThreshold,
    decimal? HappyThreshold
);
