namespace Nabadat.SurveyBuilder.Domain.Entities;

/// <summary>
/// A per-locale translation bundle for a survey (tenant-schema table <c>survey_translations</c>,
/// data-model.md §2.7, research.md §10). One row per <c>(survey_id, locale)</c>. <see cref="Keys"/>
/// is the flat jsonb map produced by <c>TranslatableStringExtractor</c> — e.g. <c>survey.name</c>,
/// <c>section.{id}.title</c>, <c>question.{id}.text</c>. Missing keys resolve to the English source
/// at render time via <c>LocaleFallbackPolicy</c> (BR-3.2).
/// </summary>
public sealed class SurveyTranslation
{
    public Guid Id { get; set; }

    /// <summary>Owning survey (intra-module FK, ON DELETE CASCADE).</summary>
    public Guid SurveyId { get; set; }

    /// <summary>BCP-47 tag — <c>en</c> and <c>ar</c> at Phase 1 (T-01); the design supports N locales.</summary>
    public string Locale { get; set; } = string.Empty;

    /// <summary>Flat map of translation key → target-locale value (jsonb <c>keys</c>).</summary>
    public Dictionary<string, string> Keys { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Monotonic ETag counter (research.md §2).</summary>
    public int RowVersion { get; set; } = 1;

    /// <summary>Bumps the ETag counter — call inside the write transaction on every mutation.</summary>
    public void IncrementRowVersion() => RowVersion++;
}
