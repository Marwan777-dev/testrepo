using Microsoft.EntityFrameworkCore;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.Translations.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Infrastructure.Persistence.Stores;

/// <summary>
/// EF implementation of <see cref="ITranslationStore"/> (T210) over <see cref="ITenantDbContext"/>.
/// Replaces the interim no-op <c>DeferredTranslationStore</c> (TODO-M01-003): <see cref="PurgeQuestionKeysAsync"/>
/// now really scrubs every <c>question.{id}.*</c> key from every locale bundle (FR-2.8), inside the
/// caller's <c>ExecuteAsync</c> transaction.
/// </summary>
public sealed class TranslationStore : ITranslationStore
{
    private readonly ITenantDbContext _context;

    public TranslationStore(ITenantDbContext context) => _context = context;

    public Task<SurveyTranslation?> GetAsync(Guid surveyId, string locale, CancellationToken ct = default) =>
        _context.SurveyTranslations.FirstOrDefaultAsync(t => t.SurveyId == surveyId && t.Locale == locale, ct);

    public async Task<IReadOnlyList<SurveyTranslation>> GetBySurveyAsync(Guid surveyId, CancellationToken ct = default) =>
        await _context.SurveyTranslations.Where(t => t.SurveyId == surveyId).ToListAsync(ct);

    public Task AddAsync(SurveyTranslation translation, CancellationToken ct = default)
    {
        _context.SurveyTranslations.Add(translation);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(SurveyTranslation translation, CancellationToken ct = default)
    {
        _context.SurveyTranslations.Update(translation);
        return Task.CompletedTask;
    }

    public async Task PurgeQuestionKeysAsync(IReadOnlyCollection<Guid> questionIds, CancellationToken ct = default)
    {
        if (questionIds.Count == 0)
        {
            return;
        }

        // A key belongs to a question when it is prefixed `question.{id}.` (text/description/options/
        // scale_labels/comment_label/…). Scrub those from every locale bundle for the affected surveys.
        var prefixes = questionIds.Select(id => $"question.{id}.").ToArray();

        var bundles = await _context.SurveyTranslations.ToListAsync(ct);
        foreach (var bundle in bundles)
        {
            var staleKeys = bundle.Keys.Keys
                .Where(key => prefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal)))
                .ToList();

            if (staleKeys.Count == 0)
            {
                continue;
            }

            foreach (var key in staleKeys)
            {
                bundle.Keys.Remove(key);
            }

            bundle.IncrementRowVersion();
            _context.SurveyTranslations.Update(bundle);
        }
    }
}
