using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Routing;

/// <summary>
/// T171 [US4] — decides whether a question may act as a routing source/target (FR-9.5). Pure; reads
/// only <see cref="Question.Type"/>, <see cref="Question.Subtype"/> and <see cref="Question.SetId"/>
/// (a non-null <c>SetId</c> ⇒ inside a Questions Set ⇒ ineligible) and delegates the type-vocabulary
/// decision to <see cref="QuestionRoutingRules.IsRoutingEligible"/>. Eligible standalone types are
/// Single Select, Scale (except the Slider sub-type), Yes/No and KPI.
/// </summary>
public sealed class RoutingEligibilityService
{
    /// <summary>True when <paramref name="question"/> is a standalone question of a routable type.</summary>
    public bool IsEligible(Question question) =>
        QuestionRoutingRules.IsRoutingEligible(question.Type, question.Subtype, insideSet: question.SetId is not null);
}
