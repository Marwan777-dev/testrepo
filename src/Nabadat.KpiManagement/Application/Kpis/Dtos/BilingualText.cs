namespace Nabadat.KpiManagement.Application.Kpis.Dtos;

/// <summary>
/// A bilingual (English + Arabic) free-text value carried across the M-06 read contract.
/// Used for the optional Min/Max Scale Description anchor labels (SRS FR-2.23a — "Both are
/// bilingual (EN + AR)"). Both members are required strings; an absent description is represented
/// by a null <see cref="BilingualText"/> reference on the owning DTO, not by empty members.
/// </summary>
public record BilingualText(string En, string Ar);
