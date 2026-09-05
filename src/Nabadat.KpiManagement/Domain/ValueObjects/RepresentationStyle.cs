namespace Nabadat.KpiManagement.Domain.ValueObjects;

/// <summary>
/// How a non-composite <see cref="Entities.KpiDefinition"/>'s question is rendered to the
/// respondent (column <c>kpi_definitions.representation_style</c>, <c>varchar(16)</c>, NULL for
/// composite KPIs).
/// <para>
/// Wire/storage form is the exact PascalCase member name (e.g. <c>"Number"</c>). Entities model
/// the column as nullable <see langword="string"/> per the M-16 reference; this enum is the
/// type-safe twin used by the question-preview component and validators. <c>Slider</c> is valid
/// only when the scale is 1–3 (FR); <c>Emoji</c> requires an <see cref="EmojiSet"/>.
/// </para>
/// </summary>
public enum RepresentationStyle
{
    /// <summary>Numbered chips across the scale.</summary>
    Number,

    /// <summary>Star rating.</summary>
    Stars,

    /// <summary>One emoji glyph per scale point (requires an <see cref="EmojiSet"/>).</summary>
    Emoji,

    /// <summary>Slider control; permitted only for the 1–3 scale.</summary>
    Slider,
}
