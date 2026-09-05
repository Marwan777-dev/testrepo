namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// The eight M-01 question types (tenant-schema column <c>questions.type</c>, data-model.md §2.4;
/// authoritative Question Type Catalogue in spec.md). Seven answer types plus the <see cref="Kpi"/>
/// metric type. Routing eligibility, sentiment eligibility, and KPI-capability are derived from
/// the catalogue — see <see cref="QuestionRoutingRules"/>.
/// </summary>
public enum QuestionType
{
    Scale,
    InputField,
    SingleSelect,
    MultiSelect,
    YesNo,
    Matrix,
    Ranking,
    Kpi,
}
