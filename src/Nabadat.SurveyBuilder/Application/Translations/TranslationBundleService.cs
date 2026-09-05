using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.Questions.Interfaces;
using Nabadat.SurveyBuilder.Application.Sections.Interfaces;
using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;
using Nabadat.SurveyBuilder.Application.Translations.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Application.Translations;

/// <summary>
/// F11 Translate workspace orchestration (contracts/translations.md): list per-locale coverage,
/// read a resolved locale bundle (target values + English fallback + missing keys), and save a target
/// bundle with merge semantics. The FR-2.8 question-delete key purge lives in
/// <see cref="ITranslationStore.PurgeQuestionKeysAsync"/> and is invoked by the US3 cascade/delete
/// services inside their own transaction — this service owns the get/put surface.
/// <para><b>Supported locales</b> are the Phase-1 fixed pair (<c>en</c>/<c>ar</c>, T-01). When the
/// M-11 <c>ITenantSettingsReader.GetSupportedLocalesAsync()</c> ships (TODO-M01-006), source this set
/// from it so a tenant can configure locales (and adding languages beyond en/ar — TODO-M01-005 — becomes
/// possible) instead of the constant here.</para>
/// </summary>
public sealed class TranslationBundleService
{
    private static readonly IReadOnlyList<string> SupportedLocales = new[] { "en", "ar" };

    private readonly ISurveyStore _surveys;
    private readonly ISectionStore _sections;
    private readonly IQuestionStore _questions;
    private readonly ITranslationStore _translations;
    private readonly TranslatableStringExtractor _extractor;
    private readonly TranslationBundleBuilder _builder;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _timeProvider;

    public TranslationBundleService(
        ISurveyStore surveys,
        ISectionStore sections,
        IQuestionStore questions,
        ITranslationStore translations,
        TranslatableStringExtractor extractor,
        TranslationBundleBuilder builder,
        ITenantDbContext context,
        TimeProvider timeProvider)
    {
        _surveys = surveys;
        _sections = sections;
        _questions = questions;
        _translations = translations;
        _extractor = extractor;
        _builder = builder;
        _context = context;
        _timeProvider = timeProvider;
    }

    /// <summary>Per-locale coverage for the workspace top selector (GET /translations).</summary>
    public async Task<IReadOnlyList<LocaleCoverage>> GetLocalesAsync(Guid surveyId, CancellationToken ct = default)
    {
        var (survey, source) = await BuildSourceAsync(surveyId, ct);
        var keysCount = source.Keys.Count;
        var rows = await _translations.GetBySurveyAsync(surveyId, ct);

        var coverage = new List<LocaleCoverage>();
        foreach (var locale in SupportedLocales)
        {
            if (locale == TranslatableStringExtractor.SourceLocale)
            {
                coverage.Add(new LocaleCoverage(locale, 100, keysCount, keysCount, survey.UpdatedAt));
                continue;
            }

            var row = rows.FirstOrDefault(r => r.Locale == locale);
            var translated = row is null
                ? 0
                : source.Keys.Keys.Count(key => row.Keys.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v));
            var percent = keysCount == 0 ? 100 : (int)Math.Round(translated * 100.0 / keysCount);
            coverage.Add(new LocaleCoverage(locale, percent, keysCount, translated, row?.UpdatedAt));
        }

        return coverage;
    }

    /// <summary>The resolved bundle for one locale (GET /translations/{locale}). Missing keys fall back to English.</summary>
    public async Task<TranslationBundleResult> GetBundleAsync(Guid surveyId, string locale, CancellationToken ct = default)
    {
        var (_, source) = await BuildSourceAsync(surveyId, ct);

        if (locale == TranslatableStringExtractor.SourceLocale)
        {
            // English is the source itself — every key present, nothing missing, no stored row/ETag.
            return new TranslationBundleResult(
                new ResolvedTranslationBundle(locale, source.Keys, Array.Empty<string>()),
                RowVersion: 0);
        }

        var row = await _translations.GetAsync(surveyId, locale, ct);
        var target = new TranslationBundle(locale, row?.Keys ?? new Dictionary<string, string>());
        var resolved = _builder.Build(source, target);
        return new TranslationBundleResult(resolved, row?.RowVersion ?? 0);
    }

    /// <summary>Save (merge) a target locale bundle (PUT /translations/{locale}); returns the new resolved view.</summary>
    public async Task<TranslationBundleResult> PutBundleAsync(
        Guid surveyId,
        string locale,
        IReadOnlyDictionary<string, string> incomingKeys,
        CancellationToken ct = default)
    {
        if (!SupportedLocales.Contains(locale))
        {
            throw new SurveyBuilderException(
                "translation.locale.not_configured", 400, $"Locale '{locale}' is not configured for this tenant.");
        }

        var (_, source) = await BuildSourceAsync(surveyId, ct);

        var unknownKeys = incomingKeys.Keys.Where(key => !source.Keys.ContainsKey(key)).ToArray();
        if (unknownKeys.Length > 0)
        {
            throw new SurveyBuilderException(
                "translation.key.unknown",
                400,
                "One or more keys do not correspond to a current source string.",
                new Dictionary<string, object> { ["unknown_keys"] = unknownKeys });
        }

        var now = _timeProvider.GetUtcNow();
        var row = await _translations.GetAsync(surveyId, locale, ct);

        await _context.ExecuteAsync(async () =>
        {
            if (row is null)
            {
                row = new SurveyTranslation
                {
                    Id = Guid.NewGuid(),
                    SurveyId = surveyId,
                    Locale = locale,
                    Keys = new Dictionary<string, string>(incomingKeys),
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                await _translations.AddAsync(row, ct);
            }
            else
            {
                foreach (var (key, value) in incomingKeys)
                {
                    row.Keys[key] = value; // merge — keys absent from the body are preserved
                }

                row.UpdatedAt = now;
                row.IncrementRowVersion();
                await _translations.UpdateAsync(row, ct);
            }
        }, ct);

        var target = new TranslationBundle(locale, row!.Keys);
        var resolved = _builder.Build(source, target);
        return new TranslationBundleResult(resolved, row.RowVersion);
    }

    private async Task<(Survey Survey, TranslationBundle Source)> BuildSourceAsync(Guid surveyId, CancellationToken ct)
    {
        var survey = await _surveys.GetAsync(surveyId, ct)
            ?? throw new SurveyBuilderException("survey.not_found", 404, "Survey not found.");
        var sections = await _sections.GetBySurveyAsync(surveyId, ct);
        var questions = await _questions.GetBySurveyAsync(surveyId, ct);
        var source = _extractor.Extract(survey, sections, questions);
        return (survey, source);
    }
}
