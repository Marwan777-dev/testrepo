namespace Nabadat.CustomerJourneyManagement.Domain.Entities;

/// <summary>
/// M-07 report metadata for a journey (tenant-schema table <c>report_contracts</c>, one
/// row per journey). The payload is rebuilt transactionally after any write to
/// <c>stages</c>, <c>touchpoints</c>, <c>kpi_bindings</c>, or <c>detection_configs</c>;
/// M-07 reads it via <c>IReportContractReader</c>.
/// </summary>
public sealed class ReportContract
{
    public Guid ReportContractId { get; set; }

    /// <summary>Owning journey (FK → <c>journeys.journey_id</c> ON DELETE CASCADE, UNIQUE).</summary>
    public Guid JourneyId { get; set; }

    /// <summary>Report metadata stored as opaque JSON (<c>jsonb</c> column).</summary>
    public string ContractPayload { get; set; } = string.Empty;

    /// <summary>UTC timestamp of the last contract rebuild.</summary>
    public DateTimeOffset GeneratedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
