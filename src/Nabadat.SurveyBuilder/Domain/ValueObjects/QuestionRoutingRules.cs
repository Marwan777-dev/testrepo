namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// Routing-eligibility rules from the authoritative Question Type Catalogue (spec.md) + FR-9.5.
/// Companion to <see cref="QuestionType"/> / <see cref="QuestionSubType"/> (kept in its own file
/// per the one-type-per-file convention).
/// </summary>
public static class QuestionRoutingRules
{
    /// <summary>
    /// Whether a question can act as a routing source/target. Eligible types are Single Select,
    /// Scale (<b>except</b> the Slider sub-type), Yes/No, and KPI — and <b>only</b> when the
    /// question is standalone, i.e. not inside a Questions Set (FR-9.5).
    /// </summary>
    /// <param name="type">The question's type.</param>
    /// <param name="subType">The question's sub-type (used to exclude Scale/Slider).</param>
    /// <param name="insideSet">True when the question lives inside a Questions Set.</param>
    public static bool IsRoutingEligible(QuestionType type, QuestionSubType subType, bool insideSet)
    {
        if (insideSet)
        {
            // FR-9.5: set questions are never routing sources or targets.
            return false;
        }

        return type switch
        {
            QuestionType.Scale => subType != QuestionSubType.Slider, // Slider is not routable
            QuestionType.SingleSelect => true,
            QuestionType.YesNo => true,
            QuestionType.Kpi => true,
            _ => false,
        };
    }
}
