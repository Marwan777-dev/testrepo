namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// Per-type display mode / sub-type (tenant-schema column <c>questions.subtype</c>, data-model.md
/// §2.4; authoritative Question Type Catalogue in spec.md). The valid sub-type set depends on the
/// parent <see cref="QuestionType"/>; <see cref="None"/> is used by types that have no display
/// variant (MultiSelect, Ranking, YesNo, Kpi). Which sub-types are required/valid for which type
/// is enforced by <c>QuestionValidator</c> (T045); this enum only enumerates the vocabulary.
/// </summary>
public enum QuestionSubType
{
    None,

    // Scale
    Labels,
    Stars,
    Smileys,
    Slider,

    // InputField
    Text,
    Paragraph,
    Number,
    Date,
    Time,
    DateTime,
    Month,

    // SingleSelect
    List,
    Dropdown,

    // Matrix
    CustomColumns,
    KpiScale,
}
