namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// KPI question payload — the KPI-defined representation (research.md §5). The bound KPI code,
/// perspective, and journey binding live in flat <c>questions</c> columns; this payload carries the
/// presentation options. Routing-eligible when standalone.
/// </summary>
/// <param name="Representation">KPI-defined scale representation (e.g. the catalogue's display mode).</param>
/// <param name="AllowNa">Adds a not-applicable choice to the KPI scale (Field Definitions, F8).</param>
public sealed record KpiPayload(
    string? Representation = null,
    bool AllowNa = false) : QuestionTypePayload;
