using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Translations;

/// <summary>
/// Walks a survey and its section/question graph and produces the flat English <b>source</b> bundle
/// (FR-11.1, research.md §10). The survey graph is passed in explicitly because <see cref="Survey"/>
/// holds no section/question navigation collections — the App layer loads them from the stores.
/// <para>Key namespace (contracts/translations.md): <c>survey.name</c>, <c>survey.welcome</c>,
/// <c>survey.thanks</c>, <c>section.{id}.title</c>, <c>question.{id}.text</c>,
/// <c>question.{id}.description</c>, <c>question.{id}.options.{i}.label</c>,
/// <c>question.{id}.scale_labels.{i}</c>, <c>question.{id}.comment_label</c>. Optional strings that
/// are null/empty are not emitted. <c>reason_items</c> are not extracted yet — the domain has no
/// reason-follow-up field (TODO-M01-004); add <c>question.{id}.reason_items.{i}</c> here when it lands.</para>
/// </summary>
public sealed class TranslatableStringExtractor
{
    /// <summary>The source locale every extracted bundle is stamped with (BR-3.2 fallback target).</summary>
    public const string SourceLocale = "en";

    public TranslationBundle Extract(Survey survey, IReadOnlyList<Section> sections, IReadOnlyList<Question> questions)
    {
        var keys = new Dictionary<string, string>();

        keys["survey.name"] = survey.NameEn;
        if (!string.IsNullOrEmpty(survey.WelcomeHtml))
        {
            keys["survey.welcome"] = survey.WelcomeHtml;
        }

        if (!string.IsNullOrEmpty(survey.ThanksHtml))
        {
            keys["survey.thanks"] = survey.ThanksHtml;
        }

        foreach (var section in sections)
        {
            keys[$"section.{section.Id}.title"] = section.Name;
        }

        foreach (var question in questions)
        {
            keys[$"question.{question.Id}.text"] = question.Text;

            if (!string.IsNullOrEmpty(question.Description))
            {
                keys[$"question.{question.Id}.description"] = question.Description;
            }

            AddOptionKeys(keys, question);
            AddScaleLabelKeys(keys, question);

            if (question.Comments)
            {
                keys[$"question.{question.Id}.comment_label"] = question.CommentLabel;
            }
        }

        return new TranslationBundle(SourceLocale, keys);
    }

    private static void AddOptionKeys(IDictionary<string, string> keys, Question question)
    {
        var options = question.TypePayload switch
        {
            SingleSelectPayload single => single.Options,
            MultiSelectPayload multi => multi.Options,
            _ => null,
        };

        if (options is null)
        {
            return;
        }

        for (var i = 0; i < options.Count; i++)
        {
            keys[$"question.{question.Id}.options.{i}.label"] = options[i];
        }
    }

    private static void AddScaleLabelKeys(IDictionary<string, string> keys, Question question)
    {
        if (question.TypePayload is not ScalePayload { Labels: { } labels })
        {
            return;
        }

        for (var i = 0; i < labels.Count; i++)
        {
            keys[$"question.{question.Id}.scale_labels.{i}"] = labels[i];
        }
    }
}
