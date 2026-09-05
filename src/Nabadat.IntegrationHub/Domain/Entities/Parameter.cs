using Nabadat.IntegrationHub.Domain.ValueObjects;

namespace Nabadat.IntegrationHub.Domain.Entities;

/// <summary>
/// One field a caller may send on an inbound request — the tenant's parameter catalogue
/// (data-model.md §4): the 23 normative built-ins (FR-F0-10, all seeded enabled per BR-23) plus any
/// custom parameters. <see cref="ApiField"/> is the wire key; the five usage flags below govern where
/// the parameter may be used downstream ("Searchable" was removed, <c>[PO-G26]</c>).
///
/// <para><b>No hard-delete transition exists for either origin</b> (BR-09) — disable only.</para>
/// </summary>
public sealed class Parameter
{
    public Guid Id { get; set; }

    /// <summary>Required, ≤50 chars (VR-F05). Typing it auto-suggests the <see cref="ApiField"/>.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Required, ≤50 chars (VR-F05), rendered RTL.</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>
    /// The <c>snake_case</c> wire key the caller sends. Unique per tenant across built-in + custom +
    /// enabled + disabled (VR-F06) — a disabled parameter still reserves its field name. This is also
    /// the only value M-13 pushes cross-module, to M-10's data-scope endpoint (identifier-only, no FK).
    /// </summary>
    public string ApiField { get; set; } = string.Empty;

    /// <summary>
    /// One-way lock set once the first request carrying this field has been received (BR-11) —
    /// renaming after that would break the caller. Built-ins are <b>always</b> locked (BR-09).
    /// Independent axis from <see cref="Enabled"/>: re-enabling a disabled built-in never unlocks it.
    /// </summary>
    public bool ApiFieldLocked { get; set; }

    /// <summary>One of the 13 closed types (FR-F0-04). Read-only for built-ins — see <see cref="DataTypeLocked"/>.</summary>
    public DataType DataType { get; set; }

    /// <summary>
    /// Derived, never stored: built-in parameter types are read-only (<c>[PO-G27]</c>, BR-09), so the
    /// lock is a projection of <see cref="Origin"/> rather than a separate column that could drift.
    /// </summary>
    public bool DataTypeLocked => Origin == ParameterOrigin.BuiltIn;

    /// <summary>Populated only when <see cref="DataType"/> is <see cref="ValueObjects.DataType.Range"/>; must be &lt; <see cref="RangeMax"/> (VR-F07).</summary>
    public decimal? RangeMin { get; set; }

    /// <summary>Populated only when <see cref="DataType"/> is <see cref="ValueObjects.DataType.Range"/>; must be &gt; <see cref="RangeMin"/> (VR-F07).</summary>
    public decimal? RangeMax { get; set; }

    /// <summary>Optional Range unit label, e.g. "minutes".</summary>
    public string? RangeUnit { get; set; }

    /// <summary>Optional regex or per-type rule reference; a value failing it rejects the request with <c>E-1003</c>.</summary>
    public string? ValidationRule { get; set; }

    public ParameterOrigin Origin { get; set; } = ParameterOrigin.Custom;

    /// <summary>
    /// Enabled ⇄ Disabled. Disabling a parameter referenced by M-10 data-scope filters, rule builders,
    /// or a channel contract requires the BR-10 impact-warning flow — a confirmation, not a hard block.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Usage flag 1 — the <b>assignment default</b> only. The channel contract
    /// (<see cref="ChannelParameterAssignment.Required"/>) is authoritative on requiredness at request
    /// time (BR-08); this value merely seeds a new assignment.
    /// </summary>
    public bool RequiredByDefault { get; set; }

    /// <summary>Usage flag 2 — available as a filter (and, with a known value set, pushed to M-10's data scope).</summary>
    public bool Filterable { get; set; } = true;

    /// <summary>Usage flag 3.</summary>
    public bool ReportingVisibility { get; set; } = true;

    /// <summary>Usage flag 4.</summary>
    public bool DashboardVisibility { get; set; }

    /// <summary>
    /// Usage flag 5 — whether source-value → display-value mappings apply. <b>Derived from
    /// <see cref="DataType"/> per BR-27</b> (<c>[PO-G25]</c>) and enforced server-side even if a client
    /// sends a contradicting value: <c>list</c> → always <c>true</c> and locked; <c>text</c>/
    /// <c>boolean</c>/<c>url</c> → user-changeable, default <c>false</c>; every other type →
    /// <c>false</c> and locked (unavailable).
    /// </summary>
    public bool MappingSupport { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
