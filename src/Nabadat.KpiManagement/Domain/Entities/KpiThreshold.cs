namespace Nabadat.KpiManagement.Domain.Entities;

/// <summary>
/// Performance-band thresholds for a KPI (tenant-schema table <c>kpi_thresholds</c>,
/// data-model.md §1.2). One row per KPI — <see cref="KpiId"/> is both the primary key and the
/// foreign key to <c>kpi_definitions.id</c>. The four values are strictly ascending
/// (<c>lower_bound &lt; x &lt; y &lt; upper_bound</c>, enforced by a CHECK constraint): the
/// <c>[lower_bound, x)</c> band is unsatisfactory, <c>[x, y)</c> average, <c>[y, upper_bound]</c>
/// satisfactory.
/// </summary>
public sealed class KpiThreshold
{
    /// <summary>PK and FK → <c>kpi_definitions.id</c> (ON DELETE RESTRICT).</summary>
    public Guid KpiId { get; set; }

    /// <summary>Scale floor (0 for normalised KPIs; −100 for NPS).</summary>
    public decimal LowerBound { get; set; }

    /// <summary>Unsatisfactory/average boundary.</summary>
    public decimal X { get; set; }

    /// <summary>Average/satisfactory boundary.</summary>
    public decimal Y { get; set; }

    /// <summary>Scale ceiling (100 for normalised KPIs and NPS).</summary>
    public decimal UpperBound { get; set; }
}
