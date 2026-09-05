namespace Nabadat.SurveyBuilder.Application.Templates;

/// <summary>
/// One per-locale translation bundle copied into a template snapshot (data-model.md §2.9:
/// <c>"translations": {"en": {keys}, "ar": {keys}}</c>). <see cref="Keys"/> is the flat
/// <c>survey_translations.keys</c> map for the locale — the same key namespace produced by
/// <c>TranslatableStringExtractor</c> (<c>survey.name</c>, <c>section.{id}.title</c>,
/// <c>question.{id}.text</c>, …). On instantiate, <see cref="TemplateInstantiator"/> remaps the
/// <c>section.{id}.*</c> / <c>question.{id}.*</c> ids onto the regenerated rows so the copied strings
/// stay attached to their (new) questions/sections (FR-7.4 copy-all).
/// </summary>
public sealed record TranslationBundleSnapshot(string Locale, IReadOnlyDictionary<string, string> Keys);
