namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// One ordered item in a render-plan section (contracts/surveys.md): either a standalone
/// <c>question</c> (carries <see cref="QuestionId"/>) or a <c>set</c> sample (carries
/// <see cref="SetId"/> + its pre-selected <see cref="Questions"/>). The <see cref="Kind"/>
/// discriminator matches the contract's <c>"question"</c> / <c>"set"</c> tokens.
/// </summary>
public sealed record RenderPlanItem(string Kind, Guid? QuestionId, Guid? SetId, IReadOnlyList<Guid>? Questions)
{
    public static RenderPlanItem Question(Guid questionId) => new("question", questionId, null, null);

    public static RenderPlanItem Set(Guid setId, IReadOnlyList<Guid> questions) => new("set", null, setId, questions);
}
