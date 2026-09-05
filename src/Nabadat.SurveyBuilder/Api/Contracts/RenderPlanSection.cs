namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>A section in the render plan (contracts/surveys.md) with its ordered items (standalone questions + set samples).</summary>
public sealed record RenderPlanSection(Guid SectionId, IReadOnlyList<RenderPlanItem> Items);
