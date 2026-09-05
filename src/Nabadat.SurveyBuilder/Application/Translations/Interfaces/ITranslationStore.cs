using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Application.Translations.Interfaces;

/// <summary>
/// Data-access port for the survey-translation aggregate (<c>survey_translations</c>, data-model.md
/// §2.7). Backs the US6 Translate workspace (per-locale get/save) and the FR-2.8 purge surface
/// consumed by US3 cascade/delete (when a question or a section's children are deleted, its
/// translation keys are removed across all locales so no orphan strings survive).
/// <para>Implemented by <c>TranslationStore</c> (T210) over <c>ITenantDbContext</c>. This replaced
/// the interim no-op <c>DeferredTranslationStore</c> (TODO-M01-003).</para>
/// </summary>
public interface ITranslationStore
{
    /// <summary>The saved bundle for one <c>(survey, locale)</c>, or null when the locale has no row yet.</summary>
    Task<SurveyTranslation?> GetAsync(Guid surveyId, string locale, CancellationToken ct = default);

    /// <summary>Every saved locale bundle for a survey (used for the coverage summary + FR-2.8 purge).</summary>
    Task<IReadOnlyList<SurveyTranslation>> GetBySurveyAsync(Guid surveyId, CancellationToken ct = default);

    Task AddAsync(SurveyTranslation translation, CancellationToken ct = default);

    Task UpdateAsync(SurveyTranslation translation, CancellationToken ct = default);

    /// <summary>Removes every translation key belonging to any of <paramref name="questionIds"/>, all locales (FR-2.8).</summary>
    Task PurgeQuestionKeysAsync(IReadOnlyCollection<Guid> questionIds, CancellationToken ct = default);
}
