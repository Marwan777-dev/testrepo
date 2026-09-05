using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Domain.Interfaces;

/// <summary>
/// The M-01 published render contract (constitution AD-01) — the only sanctioned way for other
/// modules to reach M-01 at run time. Consumed by <b>M-02 (Channel Management)</b> at dispatch
/// time and <b>M-04 (Response Collection)</b> at response-start time. Per AD-01 no consumer
/// references M-01's concrete types, EF entities, or tables — this interface and its DTOs are
/// value-type only. Implemented in the Application layer by the render-plan services (T143/T144).
/// See <c>contracts/published-interface.md</c>.
/// </summary>
public interface ISurveyRenderService
{
    /// <summary>
    /// Returns the exact section/set/question ordering the respondent should receive — including
    /// low-response ordering (FR-10.4) when enabled, the per-respondent deterministic Questions Set
    /// sample, and the routing map. Called once per dispatch by M-02 and re-used by M-04 while
    /// collecting the response. Computed at call time — there is no cache.
    /// </summary>
    Task<RenderPlan> GetRenderPlanAsync(SurveyId surveyId, RespondentContext respondent, CancellationToken ct);

    /// <summary>
    /// Returns the full survey authoring definition (settings, appearance, welcome/thanks,
    /// sections/sets/questions, translations bundle) M-04 needs to render the survey UI. Filtered
    /// to Active status only — returns <c>null</c> when the survey is not currently Active. M-04
    /// caches this per <c>survey_id</c> for the life of a single dispatch batch, never across
    /// dispatches (any status change flips eligibility).
    /// </summary>
    Task<SurveyDefinition?> GetActiveSurveyDefinitionAsync(SurveyId surveyId, LocaleCode locale, CancellationToken ct);
}

/// <summary>Opaque M-01 survey identifier passed across the module boundary.</summary>
public sealed record SurveyId(Guid Value);

/// <summary>The respondent a render plan is being computed for (drives the deterministic sample).</summary>
public sealed record RespondentContext(Guid RespondentId, LocaleCode PreferredLocale);

/// <summary>A BCP-47 locale tag (e.g. <c>en</c>, <c>ar</c>).</summary>
public sealed record LocaleCode(string Value);

/// <summary>
/// The complete render plan for one dispatch: the layout mode M-04 paginates by, the ordered
/// sections, and the sparse routing map (<c>question_id → (answer_key → target)</c>) — only routes
/// that deviate from the next-in-order default appear (FR-9.5); M-04 defaults to next-in-order
/// when a <c>(question, answer)</c> is absent.
/// </summary>
public sealed record RenderPlan(
    SurveyId SurveyId,
    LayoutMode Layout,
    IReadOnlyList<RenderSection> Sections,
    IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, RoutingTarget>> RoutingMap);

/// <summary>One section's ordered items (standalone questions + Questions Set samples).</summary>
public sealed record RenderSection(Guid SectionId, IReadOnlyList<RenderItem> Items);

/// <summary>Base type for an ordered item within a section.</summary>
public abstract record RenderItem;

/// <summary>A standalone question to render.</summary>
public sealed record RenderQuestion(Guid QuestionId) : RenderItem;

/// <summary>
/// A Questions Set rendered as its pre-selected subset, in order — chosen by a seed derived from
/// <c>respondent_id + survey_id</c> (random mode) or by low-response order (FR-10.4).
/// </summary>
public sealed record RenderSetSample(Guid SetId, IReadOnlyList<Guid> QuestionIds) : RenderItem;

/// <summary>Where an answer routes: to a specific question, or the end of the survey.</summary>
public sealed record RoutingTarget(Guid? TargetQuestionId, bool EndsSurvey);

/// <summary>
/// The full survey authoring definition M-04 needs to render the survey UI to a respondent, in a
/// given <see cref="LocaleCode"/>.
/// <para><b>Under-specified in the contract.</b> <c>contracts/published-interface.md</c> names the
/// pieces this must carry (settings, appearance, welcome/thanks, sections/sets/questions,
/// translations bundle) but does NOT specify their field-level shape, and no other spec doc does.
/// This record therefore exposes only the pieces whose shape is unambiguous today; the rich
/// authoring content (appearance tokens, per-question authoring detail, the inlined translation
/// bundle) is finalized when <c>SurveyDefinitionAssembler</c> (T144, US3) is implemented against
/// M-04's concrete rendering needs. Tracked as <b>TODO-M01-008 (GAP)</b> — expanding this record
/// is an additive change to the published contract, so M-02/M-04 must re-compile against it.</para>
/// </summary>
public sealed record SurveyDefinition(
    SurveyId SurveyId,
    SurveyStatus Status,
    LocaleCode Locale,
    LayoutMode Layout,
    string? WelcomeHtml,
    string? ThanksHtml);
