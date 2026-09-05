using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Questions.Interfaces;
using Nabadat.SurveyBuilder.Application.QuestionsSets;
using Nabadat.SurveyBuilder.Application.QuestionsSets.Dtos;
using Nabadat.SurveyBuilder.Application.QuestionsSets.Interfaces;
using Nabadat.SurveyBuilder.Application.Routing.Interfaces;
using Nabadat.SurveyBuilder.Application.Sections.Interfaces;
using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using RenderPlanContract = Nabadat.SurveyBuilder.Domain.Interfaces.RenderPlan;

namespace Nabadat.SurveyBuilder.Application.RenderPlan;

/// <summary>
/// Implements the M-01 published <see cref="ISurveyRenderService"/> (T143). Composes the exact
/// section/set/question ordering a respondent receives per dispatch: each section's items are
/// ordered by their position, each Questions Set is rendered as its pre-selected subset (low-response
/// per FR-10.4, or a deterministic per-respondent random sample), and the sparse routing overrides
/// are projected into the render map. When low-response ordering is active, sections cascade into
/// least-answered-first order (research.md §7). Computed at call time — no cache (AD-04).
/// <c>GetActiveSurveyDefinitionAsync</c> delegates to <see cref="SurveyDefinitionAssembler"/> (T144).
/// </summary>
public sealed class SurveyRenderService : ISurveyRenderService
{
    private readonly ISurveyStore _surveys;
    private readonly ISectionStore _sections;
    private readonly IQuestionStore _questions;
    private readonly IQuestionsSetStore _sets;
    private readonly IRoutingMapStore _routing;
    private readonly IResponseCountReader _responseCounts;
    private readonly LowResponseOrderingService _ordering;
    private readonly SurveyDefinitionAssembler _assembler;

    public SurveyRenderService(
        ISurveyStore surveys,
        ISectionStore sections,
        IQuestionStore questions,
        IQuestionsSetStore sets,
        IRoutingMapStore routing,
        IResponseCountReader responseCounts,
        LowResponseOrderingService ordering,
        SurveyDefinitionAssembler assembler)
    {
        _surveys = surveys;
        _sections = sections;
        _questions = questions;
        _sets = sets;
        _routing = routing;
        _responseCounts = responseCounts;
        _ordering = ordering;
        _assembler = assembler;
    }

    public async Task<RenderPlanContract> GetRenderPlanAsync(SurveyId surveyId, RespondentContext respondent, CancellationToken ct)
    {
        var survey = await _surveys.GetAsync(surveyId.Value, ct);
        if (survey is null || survey.Status != SurveyStatus.Active)
        {
            // Indistinguishable-absence (APIs-constitution Article 4.6): the same 404 whether the
            // survey does not exist or is not currently Active (contracts/surveys.md render-plan).
            throw new SurveyBuilderException("survey.not_found", 404, "Survey not found or not active.");
        }

        var sections = await _sections.GetBySurveyAsync(surveyId.Value, ct);
        var allQuestions = await _questions.GetBySurveyAsync(surveyId.Value, ct);
        var responseCounts = await _responseCounts.GetResponseCountsAsync(surveyId.Value, ct);

        var standaloneBySection = allQuestions
            .Where(q => q.SetId is null)
            .GroupBy(q => q.SectionId)
            .ToDictionary(g => g.Key, g => g.OrderBy(q => q.Order).ToList());

        var membersBySet = allQuestions
            .Where(q => q.SetId is not null)
            .GroupBy(q => q.SetId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(q => q.Order).Select(q => q.Id).ToList());

        var renderSections = new List<RenderSection>(sections.Count);
        var orderingSections = new List<OrderingSection>(sections.Count);
        var lowResponseActive = survey.ShuffleMode == "low_response";

        foreach (var section in sections)
        {
            var sets = await _sets.GetBySectionAsync(section.Id, ct);
            var ordered = new List<(int Order, RenderItem Item)>();
            var eligible = new List<Guid>();

            if (standaloneBySection.TryGetValue(section.Id, out var standalone))
            {
                foreach (var question in standalone)
                {
                    ordered.Add((question.Order, new RenderQuestion(question.Id)));
                    eligible.Add(question.Id);
                }
            }

            foreach (var set in sets)
            {
                var members = membersBySet.TryGetValue(set.Id, out var m) ? m : new List<Guid>();
                IReadOnlyList<Guid> sample = set.SelectionMode == QuestionsSetSelectionMode.LowResponse
                    ? _ordering.PickCandidates(new OrderingSet(set.Id, members), set.Count, responseCounts)
                    : PickRandomSample(members, set.Count, respondent.RespondentId, surveyId.Value);

                if (set.SelectionMode == QuestionsSetSelectionMode.LowResponse)
                {
                    lowResponseActive = true;
                }

                ordered.Add((set.Order, new RenderSetSample(set.Id, sample)));
                eligible.AddRange(sample);
            }

            var items = ordered.OrderBy(x => x.Order).Select(x => x.Item).ToList();
            renderSections.Add(new RenderSection(section.Id, items));
            orderingSections.Add(new OrderingSection(section.Id, eligible));
        }

        if (lowResponseActive)
        {
            var sectionOrder = _ordering.OrderSections(orderingSections, responseCounts);
            renderSections = sectionOrder
                .Select(id => renderSections.First(s => s.SectionId == id))
                .ToList();
        }

        var routingMap = await BuildRoutingMapAsync(surveyId.Value, ct);

        return new RenderPlanContract(surveyId, survey.Layout, renderSections, routingMap);
    }

    public Task<SurveyDefinition?> GetActiveSurveyDefinitionAsync(SurveyId surveyId, LocaleCode locale, CancellationToken ct) =>
        _assembler.AssembleAsync(surveyId, locale, ct);

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, RoutingTarget>>> BuildRoutingMapAsync(
        Guid surveyId, CancellationToken ct)
    {
        var routes = await _routing.GetBySurveyAsync(surveyId, ct);
        return routes
            .GroupBy(r => r.SourceQuestionId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<string, RoutingTarget>)g.ToDictionary(
                    r => r.AnswerKey,
                    r => new RoutingTarget(r.TargetQuestionId, r.TargetQuestionId is null)));
    }

    /// <summary>
    /// A deterministic per-respondent random subset — the same respondent + survey always yields the
    /// same sample (research.md §7 "seed derived from respondent_id + survey_id"), so a re-render
    /// during one response is stable.
    /// </summary>
    private static IReadOnlyList<Guid> PickRandomSample(IReadOnlyList<Guid> memberIds, int count, Guid respondentId, Guid surveyId)
    {
        if (count <= 0 || memberIds.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        if (count >= memberIds.Count)
        {
            return memberIds;
        }

        var seed = respondentId.GetHashCode() ^ surveyId.GetHashCode();
        var rng = new Random(seed);
        return memberIds.OrderBy(_ => rng.Next()).Take(count).ToList();
    }
}
