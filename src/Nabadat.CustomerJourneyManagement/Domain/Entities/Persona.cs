namespace Nabadat.CustomerJourneyManagement.Domain.Entities;

/// <summary>
/// A reusable customer archetype (tenant-schema table <c>personas</c>). Lifecycle:
/// <c>Draft</c> → <c>Active</c> ↔ <c>Inactive</c> → <c>Archived</c>. <c>Archived</c> is
/// irreversible, and archived personas cannot be bound to journeys.
/// </summary>
public sealed class Persona
{
    public Guid PersonaId { get; set; }

    /// <summary>Arabic label (فصحى).</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>English label.</summary>
    public string NameEn { get; set; } = string.Empty;

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    /// <summary><c>Draft</c> | <c>Active</c> | <c>Inactive</c> | <c>Archived</c>.</summary>
    public string Status { get; set; } = "Draft";

    /// <summary>M-10 <c>user_id</c> of the creator (P-01 only can create).</summary>
    public Guid CreatedBy { get; set; }

    /// <summary>M-10 <c>user_id</c> of the last editor; null until first edit.</summary>
    public Guid? UpdatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
