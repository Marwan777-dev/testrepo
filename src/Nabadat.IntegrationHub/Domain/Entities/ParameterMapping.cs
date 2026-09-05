namespace Nabadat.IntegrationHub.Domain.Entities;

/// <summary>
/// A source value → bilingual display value translation for one mapping-enabled
/// <see cref="Parameter"/> (data-model.md §6), e.g. <c>S001</c> → "Account opening" / "فتح حساب".
///
/// <para><b>Read-time resolution</b> (BR-13 / FR-F0-05): reports, dashboards, and exports translate
/// stored source values through the <i>current</i> mapping table, so editing or deleting a mapping
/// retroactively relabels historical data by design. There is no version history — the audit trail is
/// the sole change record — and Replace-all is irreversible.</para>
///
/// <para>The mapping table is also the single source of List values, and membership is never validated
/// at ingestion (BR-12): an unmapped incoming value is accepted, stored raw, and queued as an
/// <see cref="UnmappedValueOccurrence"/>.</para>
/// </summary>
public sealed class ParameterMapping
{
    public Guid Id { get; set; }

    /// <summary>
    /// Intra-module FK → <see cref="Parameter"/>. Must reference a mapping-enabled parameter (BR-27) —
    /// validated at the service layer, not by a DB constraint, because the eligibility follows the
    /// parameter's data type.
    /// </summary>
    public Guid ParameterId { get; set; }

    /// <summary>
    /// The raw backend value. Unique within the parameter <b>case-insensitively</b> (VR-F08,
    /// Clarifications 2026-07-27) while preserving the entered casing.
    /// </summary>
    public string SourceValue { get; set; } = string.Empty;

    public string DisplayEn { get; set; } = string.Empty;

    /// <summary>Rendered RTL.</summary>
    public string DisplayAr { get; set; } = string.Empty;

    /// <summary>
    /// Always <c>"active"</c> in storage. The <c>draft</c> state exists <b>only</b> client-side, for
    /// SCR-07's inline add-row UX — a <c>POST</c> always creates an active row and <b>no draft row is
    /// ever persisted</b> (data-model.md §6). The column is kept so the wire/model vocabulary matches
    /// the spec's Status Lifecycle; a DB CHECK restricts it to the two literals.
    /// </summary>
    public string Status { get; set; } = ActiveStatus;

    /// <summary>The only status value ever written to storage.</summary>
    public const string ActiveStatus = "active";

    /// <summary>Client-side-only pre-save state; never persisted (see <see cref="Status"/>).</summary>
    public const string DraftStatus = "draft";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
