using Nabadat.SurveyBuilder.Application.Appearance;
using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Questions.Interfaces;
using Nabadat.SurveyBuilder.Application.Sections.Interfaces;
using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;
using Nabadat.SurveyBuilder.Application.Translations;
using Nabadat.SurveyBuilder.Application.Translations.Interfaces;

namespace Nabadat.SurveyBuilder.Application.Preview;

/// <summary>
/// Assembles the F12 multi-channel preview payload (contracts/report-and-analytics.md § GET /preview):
/// a light-weight <c>GET /surveys/{id}</c> that also inlines the resolved theme tokens and the resolved
/// locale bundle. Reads the survey + its ordered sections/questions, resolves appearance via
/// <see cref="AppearanceService"/> (which correctly handles Inherited vs Customized, wrapping
/// <c>ITenantDesignGuidelinesReader</c>), and resolves the requested locale with English fallback via
/// <see cref="TranslationBundleBuilder"/>/<see cref="LocaleFallbackPolicy"/> (BR-3.2). Preview is a pure
/// read — no writes, no unit/integration lane (US7 declares both skipped).
/// </summary>
public sealed class PreviewPayloadBuilder
{
    private static readonly HashSet<string> ValidChannels =
        new(StringComparer.OrdinalIgnoreCase) { "desktop", "mobile", "whatsapp", "email" };

    private readonly ISurveyStore _surveys;
    private readonly ISectionStore _sections;
    private readonly IQuestionStore _questions;
    private readonly ITranslationStore _translations;
    private readonly TranslatableStringExtractor _extractor;
    private readonly TranslationBundleBuilder _builder;
    private readonly AppearanceService _appearance;

    public PreviewPayloadBuilder(
        ISurveyStore surveys,
        ISectionStore sections,
        IQuestionStore questions,
        ITranslationStore translations,
        TranslatableStringExtractor extractor,
        TranslationBundleBuilder builder,
        AppearanceService appearance)
    {
        _surveys = surveys;
        _sections = sections;
        _questions = questions;
        _translations = translations;
        _extractor = extractor;
        _builder = builder;
        _appearance = appearance;
    }

    public async Task<PreviewPayload> BuildAsync(Guid surveyId, string? channel, string? locale, CancellationToken ct = default)
    {
        var resolvedChannel = string.IsNullOrWhiteSpace(channel) ? "desktop" : channel.Trim();
        if (!ValidChannels.Contains(resolvedChannel))
        {
            throw new SurveyBuilderException("preview.channel.invalid", 400, $"Unknown preview channel '{channel}'.");
        }

        var resolvedLocale = string.IsNullOrWhiteSpace(locale) ? TranslatableStringExtractor.SourceLocale : locale.Trim();

        var survey = await _surveys.GetAsync(surveyId, ct)
            ?? throw new SurveyBuilderException("survey.not_found", 404, "Survey not found.");
        var sections = await _sections.GetBySurveyAsync(surveyId, ct);
        var questions = await _questions.GetBySurveyAsync(surveyId, ct);

        var source = _extractor.Extract(survey, sections, questions);
        ResolvedTranslationBundle translations;
        if (resolvedLocale == TranslatableStringExtractor.SourceLocale)
        {
            translations = new ResolvedTranslationBundle(resolvedLocale, source.Keys, Array.Empty<string>());
        }
        else
        {
            var row = await _translations.GetAsync(surveyId, resolvedLocale, ct);
            var target = new TranslationBundle(resolvedLocale, row?.Keys ?? new Dictionary<string, string>());
            translations = _builder.Build(source, target);
        }

        var theme = await _appearance.ResolveAsync(surveyId, ct);

        return new PreviewPayload(
            resolvedChannel.ToLowerInvariant(), resolvedLocale, survey, theme, sections, questions, translations);
    }
}
