using Nabadat.SurveyBuilder.Api.Contracts;
using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.Questions.Interfaces;
using Nabadat.SurveyBuilder.Application.Routing.Interfaces;
using Nabadat.SurveyBuilder.Application.Sections.Interfaces;
using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Routing;

/// <summary>
/// T175 [US4] — orchestrates F9 answer routing over the pure routing services (T171–T174) and the
/// <see cref="IRoutingMapStore"/>. Owns three surfaces:
/// <list type="bullet">
///   <item><see cref="ToggleRoutingAsync"/> — the survey-level switch; enforces the one-question-per-page
///   layout (FR-9.1) + confirmation, then applies the shuffle coupling via <see cref="LayoutRoutingCoupler"/>.</item>
///   <item><see cref="GetMapAsync"/> / <see cref="SaveMapAsync"/> — the per-question override map;
///   the save enforces source/target eligibility (FR-9.5) and no-cycles (<see cref="RoutingConflictDetector"/>),
///   then replaces the whole map atomically. Only overrides are stored — defaults (next-in-order) are
///   computed by <see cref="RoutingDefaultTargeter"/> and never persisted (research.md §6).</item>
///   <item><see cref="InvalidateRoutesToQuestionAsync"/> — FR-2.7 reset-to-default: drops every route
///   pointing at a deleted/ineligible question so the default reapplies.</item>
/// </list>
/// </summary>
public sealed class RoutingConfigurationService
{
    private readonly ISurveyStore _surveys;
    private readonly ISectionStore _sections;
    private readonly IQuestionStore _questions;
    private readonly IRoutingMapStore _routing;
    private readonly RoutingEligibilityService _eligibility;
    private readonly LayoutRoutingCoupler _coupler;
    private readonly RoutingConflictDetector _conflicts;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _timeProvider;

    public RoutingConfigurationService(
        ISurveyStore surveys,
        ISectionStore sections,
        IQuestionStore questions,
        IRoutingMapStore routing,
        RoutingEligibilityService eligibility,
        LayoutRoutingCoupler coupler,
        RoutingConflictDetector conflicts,
        ITenantDbContext context,
        TimeProvider timeProvider)
    {
        _surveys = surveys;
        _sections = sections;
        _questions = questions;
        _routing = routing;
        _eligibility = eligibility;
        _coupler = coupler;
        _conflicts = conflicts;
        _context = context;
        _timeProvider = timeProvider;
    }

    /// <summary>Reads a source question's sparse override rows (backs the GET routing editor).</summary>
    public Task<IReadOnlyList<RoutingMap>> GetMapAsync(Guid sourceQuestionId, CancellationToken ct = default) =>
        _routing.GetBySourceQuestionAsync(sourceQuestionId, ct);

    /// <summary>
    /// Applies the survey-level routing toggle. Requires <see cref="LayoutMode.Question"/>
    /// (<c>routing.layout_required</c>) and, when enabling, <paramref name="confirm"/>
    /// (<c>routing.confirmation_required</c>, FR-9.1). Enabling disables + locks shuffle.
    /// </summary>
    public async Task<Survey> ToggleRoutingAsync(Guid surveyId, bool enabled, bool confirm, Guid actorId, CancellationToken ct = default)
    {
        var survey = await _surveys.GetAsync(surveyId, ct)
            ?? throw new SurveyBuilderException("survey.not_found", 404, "Survey not found.");

        if (survey.Layout != LayoutMode.Question)
        {
            throw new SurveyBuilderException("routing.layout_required", 409, "Answer routing requires the one-question-per-page layout (FR-9.1).");
        }

        if (enabled)
        {
            if (!confirm)
            {
                throw new SurveyBuilderException("routing.confirmation_required", 409, "Enabling routing turns off and locks shuffle; confirm to proceed (FR-9.1).");
            }

            _coupler.OnRoutingEnabled(survey);
        }
        else
        {
            _coupler.OnRoutingDisabled(survey);
        }

        var now = _timeProvider.GetUtcNow();
        survey.UpdatedAt = now;
        survey.UpdatedBy = actorId;
        survey.IncrementRowVersion();

        await _context.ExecuteAsync(async () => await _surveys.UpdateAsync(survey, ct), ct);
        return survey;
    }

    /// <summary>
    /// Replaces the whole override map for <paramref name="questionId"/> atomically. Validates the
    /// layout, the source's eligibility (FR-9.5), every target's eligibility, and that no route points
    /// backward (<see cref="RoutingConflictDetector"/>). Bumps the source question's ETag. Returns the
    /// updated source question.
    /// </summary>
    public async Task<Question> SaveMapAsync(Guid surveyId, Guid questionId, IReadOnlyDictionary<string, string> map, CancellationToken ct = default)
    {
        var survey = await _surveys.GetAsync(surveyId, ct)
            ?? throw new SurveyBuilderException("survey.not_found", 404, "Survey not found.");

        if (survey.Layout != LayoutMode.Question)
        {
            throw new SurveyBuilderException("routing.layout_required", 409, "Answer routing requires the one-question-per-page layout (FR-9.1).");
        }

        var source = await _questions.GetAsync(questionId, ct)
            ?? throw new SurveyBuilderException("question.not_found", 404, "Question not found.");

        if (source.SetId is not null)
        {
            throw new SurveyBuilderException("routing.inside_set_forbidden", 400, "A question inside a Questions Set cannot route (FR-9.5).");
        }

        if (!_eligibility.IsEligible(source))
        {
            throw new SurveyBuilderException("routing.source_ineligible", 409, "This question type is not routing-eligible (FR-9.5).");
        }

        var ordered = await OrderedQuestionsAsync(surveyId, ct);
        var orderById = ordered.Select((q, index) => (q.Id, index)).ToDictionary(x => x.Id, x => x.index);
        var byId = ordered.ToDictionary(q => q.Id);
        var sourceOrder = orderById[source.Id];

        var now = _timeProvider.GetUtcNow();
        var rows = new List<RoutingMap>(map.Count);
        var edges = new List<RoutingEdge>(map.Count);

        foreach (var (answerKey, target) in map)
        {
            Guid? targetId;
            int? targetOrder;

            if (string.Equals(target, RoutingMapView.EndSentinel, StringComparison.Ordinal))
            {
                targetId = null;
                targetOrder = null;
            }
            else
            {
                if (!Guid.TryParse(target, out var parsed) || !byId.TryGetValue(parsed, out var targetQuestion))
                {
                    throw new SurveyBuilderException("routing.target_ineligible", 400, "The routing target does not exist in this survey (FR-9.5).");
                }

                if (targetQuestion.SetId is not null)
                {
                    throw new SurveyBuilderException("routing.target_ineligible", 400, "A question inside a Questions Set cannot be a routing target (FR-9.5).");
                }

                targetId = parsed;
                targetOrder = orderById[parsed];
            }

            edges.Add(new RoutingEdge(sourceOrder, answerKey, targetOrder));
            rows.Add(new RoutingMap
            {
                Id = Guid.NewGuid(),
                SurveyId = surveyId,
                SourceQuestionId = questionId,
                AnswerKey = answerKey,
                TargetQuestionId = targetId,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        if (_conflicts.Detect(edges).Kind == RoutingConflictKind.CycleDetected)
        {
            throw new SurveyBuilderException("routing.cycle_detected", 409, "A route points back to an earlier question, forming a loop.");
        }

        await _context.ExecuteAsync(async () =>
        {
            await _routing.DeleteBySourceQuestionAsync(questionId, ct); // replace the whole map
            foreach (var row in rows)
            {
                await _routing.AddAsync(row, ct);
            }

            source.UpdatedAt = now;
            source.IncrementRowVersion();
            await _questions.UpdateAsync(source, ct);
        }, ct);

        return source;
    }

    /// <summary>
    /// FR-2.7 reset-to-default: removes every override pointing at <paramref name="questionId"/> so the
    /// next-in-order default reapplies. Invoked when a question is deleted or becomes ineligible.
    /// </summary>
    public Task InvalidateRoutesToQuestionAsync(Guid questionId, CancellationToken ct = default) =>
        _routing.DeleteByTargetQuestionAsync(questionId, ct);

    /// <summary>
    /// Survey questions in global render order — sections by <see cref="Section.Order"/>, then questions
    /// by <see cref="Question.Order"/> — so <see cref="RoutingConflictDetector"/> can compare positions.
    /// </summary>
    private async Task<IReadOnlyList<Question>> OrderedQuestionsAsync(Guid surveyId, CancellationToken ct)
    {
        var sections = await _sections.GetBySurveyAsync(surveyId, ct);
        var sectionOrder = sections
            .OrderBy(s => s.Order)
            .Select((s, index) => (s.Id, index))
            .ToDictionary(x => x.Id, x => x.index);

        var questions = await _questions.GetBySurveyAsync(surveyId, ct);
        return questions
            .OrderBy(q => sectionOrder.TryGetValue(q.SectionId, out var index) ? index : int.MaxValue)
            .ThenBy(q => q.Order)
            .ToList();
    }
}
