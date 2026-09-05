using Nabadat.SurveyBuilder.Application.Templates;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// Template metadata + a <b>redacted</b> snapshot summary (contracts/templates.md GET /templates/{id}) —
/// section/question counts and a has-KPI-bindings flag, not the full snapshot (use the preview
/// endpoint for that).
/// </summary>
public sealed record TemplateView(
    Guid Id,
    TemplateClass Class,
    string NameEn,
    string? NameAr,
    string? Description,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Sectors,
    string? PreviewThumbnailFileHandle,
    int SectionCount,
    int QuestionCount,
    bool HasKpiBindings,
    DateTimeOffset UpdatedAt,
    Guid? UpdatedBy,
    int RowVersion)
{
    public static TemplateView From(Template t, SurveySnapshot? snapshot)
    {
        var allQuestions = snapshot is null
            ? Enumerable.Empty<QuestionSnapshot>()
            : snapshot.Sections.SelectMany(s => s.Questions.Concat(s.Sets.SelectMany(set => set.Questions)));
        var questionList = allQuestions.ToList();

        return new TemplateView(
            t.Id,
            t.Class,
            t.NameEn,
            t.NameAr,
            t.Description,
            t.Tags,
            t.Sectors,
            t.PreviewThumbnailFileHandle,
            snapshot?.Sections.Count ?? 0,
            questionList.Count,
            questionList.Any(q => q.KpiCode is not null || q.StageId is not null || q.TouchpointId is not null),
            t.UpdatedAt,
            t.UpdatedBy,
            t.RowVersion);
    }
}
