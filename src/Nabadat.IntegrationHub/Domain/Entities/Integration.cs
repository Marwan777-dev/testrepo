using Nabadat.IntegrationHub.Domain.ValueObjects;

namespace Nabadat.IntegrationHub.Domain.Entities;

/// <summary>
/// A provisioned inbound API surface: one authenticated endpoint a caller/source system uses to raise
/// survey requests in exactly one <see cref="ValueObjects.Scenario"/> (data-model.md §1). Owns at most
/// one Active <see cref="Credential"/> (BR-16) and accumulates <see cref="IntegrationRequestLog"/> rows.
///
/// <para><b>No delete transition exists</b> — Active ⇄ Inactive only (Status Lifecycle, BR-21); an
/// Inactive integration's endpoint rejects calls with <c>401 E-1401</c>.</para>
/// </summary>
public sealed class Integration
{
    public Guid Id { get; set; }

    /// <summary>Required, ≤120 chars, unique per tenant case-insensitively (VR-F01).</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Intra-module FK → <see cref="ServiceChannel"/> (Article 4.1). Only Active channels are selectable at create (FR-S2-02).</summary>
    public Guid ServiceChannelId { get; set; }

    /// <summary>
    /// Exactly one of SCN-01…05, <b>immutable after create</b> (BR-02) — an update attempting to change
    /// it is a 409. Modelled as a single column rather than a set precisely so BR-02 is structural.
    /// </summary>
    public Scenario Scenario { get; set; }

    /// <summary>
    /// Active ⇄ Inactive toggle (US10). The status is read straight off this column — unlike M-15's
    /// Actions there is no date-computed status anywhere in this module.
    /// </summary>
    public bool Active { get; set; } = true;

    /// <summary>
    /// SCN-04 embed whitelist (FR-S2-10) — populated only when
    /// <see cref="Scenario"/> is <see cref="ValueObjects.Scenario.IframeEmbed"/>. M-13 stores and exposes
    /// it; the separate unauthenticated rendering surface enforces it against the browser's origin.
    /// </summary>
    public string[]? AllowedOrigins { get; set; }

    /// <summary>
    /// SCN-02 link-expiry override in hours (FR-S2-10) — populated only when <see cref="Scenario"/> is
    /// <see cref="ValueObjects.Scenario.RedirectLink"/>; <c>null</c> means the FR-F0-08 default of 24 hours.
    /// </summary>
    public int? LinkExpiryOverrideHours { get; set; }

    /// <summary>M-10 user id — audit attribution only, not a cross-module FK (Article 4.1).</summary>
    public Guid CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
