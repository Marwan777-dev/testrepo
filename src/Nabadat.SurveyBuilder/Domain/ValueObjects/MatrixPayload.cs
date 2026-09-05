namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// Single-select Matrix question payload (Custom columns / KPI-scale sub-types — research.md §5).
/// In the KPI-scale sub-type the question reflects a KPI (BR-8.3) and carries a <c>kpi_code</c> on
/// the question row. Not routing-eligible.
/// </summary>
/// <param name="Rows">Matrix row prompts.</param>
/// <param name="Columns">Matrix answer columns (Custom-columns mode).</param>
public sealed record MatrixPayload(
    IReadOnlyList<string> Rows,
    IReadOnlyList<string> Columns) : QuestionTypePayload;
