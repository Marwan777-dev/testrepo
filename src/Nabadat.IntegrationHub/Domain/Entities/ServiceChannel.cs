namespace Nabadat.IntegrationHub.Domain.Entities;

/// <summary>
/// A business point of contact a transaction came through — a kiosk, a call centre, a portal
/// (data-model.md §3). The channel is the root of M-13's transaction data model: an
/// <see cref="Integration"/> attaches to exactly one, and the channel's
/// <see cref="ChannelParameterAssignment"/> rows form the parameter contract that is authoritative on
/// requiredness at request time (BR-08).
///
/// <para><b>No delete transition exists</b> (BR-07) — deactivate only. Inactive channels reject
/// inbound requests with <c>E-1004</c> and are hidden from new-integration selection, but stay listed.</para>
/// </summary>
public sealed class ServiceChannel
{
    public Guid Id { get; set; }

    /// <summary>English display name — required, ≤50 chars, unique per tenant case-insensitively (VR-F02).</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Arabic display name — required (VR-F03), rendered RTL.</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>
    /// The only mandatory inbound API path parameter (BR-03). <c>[A-Za-z0-9-]+</c>, ≤19 chars, unique
    /// per tenant <b>case-insensitively</b> — but stored and matched in the URL <b>exactly as
    /// entered</b> (VR-F04), which is why the schema carries both a <c>lower(channel_id)</c> unique
    /// index and a literal-cased index for the hot resolve path.
    /// </summary>
    public string ChannelId { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Active ⇄ Inactive (BR-07). Independent axis from <see cref="ChannelIdLocked"/>.</summary>
    public bool Active { get; set; } = true;

    /// <summary>
    /// One-way lock set on the channel's first 2xx request (BR-05): once <c>true</c>,
    /// <see cref="ChannelId"/> is immutable server-side regardless of what a stale client sends
    /// (a post-lock edit attempt is a 409). Renaming EN/AR never touches the ID (BR-06). Because a
    /// locked channel has by definition received traffic, this flag also enforces BR-07's
    /// "channels with traffic history cannot be deleted".
    /// </summary>
    public bool ChannelIdLocked { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
