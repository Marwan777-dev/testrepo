namespace Nabadat.KpiManagement.Domain.Entities;

/// <summary>
/// The tenant's Organization settings — exactly one row per tenant schema (tenant-schema table
/// <c>organization_settings</c>, data-model.md §2.1). M-06-owned (re-homed from the never-built
/// M-11, 2026-06-24). Holds the display <see cref="Name"/>, the opaque <see cref="LogoBlobRef"/>
/// storage handle (null when no logo is set), and the <see cref="Industry"/> (one of the canonical
/// six, enforced by the SQL <c>industry_valid</c> CHECK and the application validator pre-write).
/// </summary>
public sealed class OrganizationSettings
{
    public Guid Id { get; set; }

    /// <summary>Display name; required, ≤ 150 chars (FR-050).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Opaque <c>ILogoStore</c> storage key; null when no logo has been uploaded yet.</summary>
    public string? LogoBlobRef { get; set; }

    /// <summary>One of the canonical six industries (stored as its string name).</summary>
    public string Industry { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid UpdatedBy { get; set; }
}
