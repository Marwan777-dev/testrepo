namespace Nabadat.KpiManagement.Domain.ValueObjects;

/// <summary>
/// Which emoji glyph family renders a KPI question when
/// <see cref="RepresentationStyle"/> is <see cref="RepresentationStyle.Emoji"/>
/// (column <c>kpi_definitions.emoji_set</c>, <c>varchar(32)</c>, NULL otherwise).
/// <para>
/// Wire/storage form is the exact PascalCase member name (e.g. <c>"FaceClassic"</c>). Entities
/// model the column as nullable <see langword="string"/> per the M-16 reference; this enum is
/// the type-safe twin used by the emoji-set preview (per research.md R2's per-K slot rule).
/// Only these two sets ship in v1.
/// </para>
/// </summary>
public enum EmojiSet
{
    /// <summary>Classic faces (sad → happy) mapped one glyph per scale point.</summary>
    FaceClassic,

    /// <summary>Thumbs (down → up) mapped one glyph per scale point.</summary>
    HandThumbs,
}
