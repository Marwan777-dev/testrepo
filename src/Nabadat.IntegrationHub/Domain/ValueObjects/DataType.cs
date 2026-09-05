namespace Nabadat.IntegrationHub.Domain.ValueObjects;

/// <summary>
/// The thirteen normative parameter data types and the closed set of validation formats behind them
/// (FR-F0-04, VR-T01…T13). <b>The list is closed</b>: <c>Duration</c> and <c>Identifier</c> were
/// evaluated and rejected (<c>[PO-G17]</c>) and MUST NOT appear here, in the SCR-06 type select, or
/// anywhere else — a field-set guard test pins that.
///
/// <para>The type also determines mapping capability (BR-27 / <c>[PO-G25]</c>), which
/// <c>Parameter.MappingSupport</c> obeys server-side even if a client sends a contradicting value:
/// <see cref="List"/> is always mapping-enabled and not changeable; <see cref="Text"/>,
/// <see cref="Boolean"/> and <see cref="Url"/> may enable it (off by default); every other type
/// cannot.</para>
///
/// <para>Persisted as the snake_case wire value (<c>text</c>, …, <c>date_time</c>, …,
/// <c>geolocation</c>) via <c>DataTypeConverter</c> — NOT the PascalCase member name.</para>
/// </summary>
public enum DataType
{
    /// <summary>VR-T01 — UTF-8, max length default 255, optional regex.</summary>
    Text = 1,

    /// <summary>VR-T02 — integer or decimal, optional min/max.</summary>
    Number = 2,

    /// <summary>VR-T03 — <c>true</c>/<c>false</c> or <c>1</c>/<c>0</c>, case-insensitive.</summary>
    Boolean = 3,

    /// <summary>VR-T04 — RFC 5322 basic.</summary>
    Email = 4,

    /// <summary>VR-T05 — E.164 (<c>+</c> and 8–15 digits).</summary>
    Phone = 5,

    /// <summary>
    /// VR-T06 — UTF-8 ≤ 100 chars. Membership is <b>not</b> enforced at ingestion (BR-12): an unmapped
    /// value is accepted, stored raw, and queued for mapping; the mapping table is the sole source of
    /// List values and translates them at read time.
    /// </summary>
    List = 6,

    /// <summary>VR-T07 — numeric, must fall within the configured min/max inclusive (VR-F07); min/max/unit configured on type selection.</summary>
    Range = 7,

    /// <summary>VR-T08 — ISO 8601 <c>YYYY-MM-DD</c>.</summary>
    Date = 8,

    /// <summary>VR-T09 — "Date &amp; time": ISO 8601 with timezone.</summary>
    DateTime = 9,

    /// <summary>VR-T10 — decimal amount + ISO-4217 code, optional min/max on the amount.</summary>
    Currency = 10,

    /// <summary>VR-T11 — decimal, default bounds 0–100, configurable.</summary>
    Percentage = 11,

    /// <summary>VR-T12 — RFC 3986 absolute.</summary>
    Url = 12,

    /// <summary>VR-T13 — latitude −90…90, longitude −180…180.</summary>
    Geolocation = 13,
}
