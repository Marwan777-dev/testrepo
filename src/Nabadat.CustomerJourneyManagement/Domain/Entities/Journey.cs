namespace Nabadat.CustomerJourneyManagement.Domain.Entities;

/// <summary>
/// Root entity representing a customer journey (tenant-schema table <c>journeys</c>).
/// Per DB-02/AD-02 there is intentionally NO <c>TenantId</c> property — isolation is
/// at the PostgreSQL schema level.
/// </summary>
public sealed class Journey
{
    public Guid JourneyId { get; set; }

    /// <summary>Case-insensitive unique per tenant for non-Archived journeys.</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Free-form tenant-defined value, e.g. <c>Purchase</c>, <c>Support</c>, <c>Onboarding</c>.</summary>
    public string JourneyType { get; set; } = string.Empty;

    /// <summary><c>Draft</c> | <c>Active</c> | <c>Inactive</c> | <c>Archived</c>. <c>Archived</c> is terminal.</summary>
    public string Status { get; set; } = "Draft";

    /// <summary>M-10 <c>user_id</c> of the creator (no FK across modules).</summary>
    public Guid CreatedBy { get; set; }

    /// <summary>M-10 <c>user_id</c> of the last editor; null until first edit.</summary>
    public Guid? UpdatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
