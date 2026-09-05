namespace Nabadat.KpiManagement.Application.Kpis.Dtos;

/// <summary>Whether a <see cref="KpiSaveCommand"/> inserts a new KPI or edits an existing one.</summary>
public enum KpiSaveMode
{
    /// <summary>Insert a new custom KPI.</summary>
    Create,

    /// <summary>Update an existing KPI (subject to the immutability rules FR-004 / FR-005).</summary>
    Edit,
}
