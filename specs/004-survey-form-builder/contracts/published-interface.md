# Contract — M-01 Published Interface (consumed by M-02 / M-04)

**Related**: [plan.md § Cross-module dependency](../plan.md#cross-module-dependency-to-unblock-before-us1-ships) · [research.md § 4](../research.md#4-cross-module-contracts)

M-01 exposes a **published interface** in `Nabadat.SurveyBuilder.Domain.Interfaces` — the only sanctioned way for other modules to reach M-01 code at run time. This document is the contract that M-02 and M-04 code against.

**Rule (constitution AD-01)**: no module references `Nabadat.SurveyBuilder`'s concrete types, EF entities, or tables. The interfaces below live in `Domain/Interfaces/` and take value-type DTOs.

---

## `ISurveyRenderService`

Consumed by **M-02 (Channel Management)** at dispatch time and **M-04 (Response Collection)** at response start time.

```csharp
namespace Nabadat.SurveyBuilder.Domain.Interfaces;

public interface ISurveyRenderService
{
    /// <summary>
    /// Returns the exact section/set/question ordering the respondent should receive,
    /// including the low-response ordering (FR-10.4) if enabled, the per-respondent
    /// deterministic sample of Questions Sets, and the routing map. Called once per
    /// dispatch by M-02 and re-used by M-04 while collecting the response.
    /// </summary>
    Task<RenderPlan> GetRenderPlanAsync(SurveyId surveyId, RespondentContext respondent, CancellationToken ct);

    /// <summary>
    /// Returns the full survey authoring definition (settings, appearance, welcome/thanks,
    /// sections/sets/questions, translations bundle) needed by M-04 to render the survey
    /// UI to the respondent. Filtered to the active status only — returns null when the
    /// survey is not currently Active. M-04 is expected to cache this per (survey_id) for
    /// the life of a single dispatch batch — NOT across dispatches (any status change flips
    /// the eligibility).
    /// </summary>
    Task<SurveyDefinition?> GetActiveSurveyDefinitionAsync(SurveyId surveyId, LocaleCode locale, CancellationToken ct);
}

public sealed record SurveyId(Guid Value);
public sealed record RespondentContext(Guid RespondentId, LocaleCode PreferredLocale);
public sealed record LocaleCode(string Value); // BCP-47

public sealed record RenderPlan(
    SurveyId SurveyId,
    LayoutMode Layout,
    IReadOnlyList<RenderSection> Sections,
    IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, RoutingTarget>> RoutingMap);

public sealed record RenderSection(Guid SectionId, IReadOnlyList<RenderItem> Items);
public abstract record RenderItem;
public sealed record RenderQuestion(Guid QuestionId) : RenderItem;
public sealed record RenderSetSample(Guid SetId, IReadOnlyList<Guid> QuestionIds) : RenderItem;

public sealed record RoutingTarget(Guid? TargetQuestionId, bool EndsSurvey);
```

**Semantics**:

- The layout mode instructs M-04 how to paginate (`single` = all on one page; `section` = one page per section; `question` = one per page; `count` = N per page).
- `RenderSetSample.QuestionIds` is the pre-selected subset (by `random` seed derived from `respondent_id` + `survey_id`, OR by low-response order) — M-04 renders exactly those questions in that order.
- `RoutingMap` is complete: sparse per FR-9.5 (only routes that deviate from next-in-order default appear). M-04 defaults to next-in-order when a `(question, answer)` is absent.
- **Freshness**: the plan is computed at call time; there is no cache. M-02 receives a fresh plan for every dispatch; M-04 uses the plan handed off by M-02 for that dispatch.

---

## `IActiveSurveyReader`

Consumed by **M-04** to enforce the active-period lifecycle before accepting a response.

```csharp
public interface IActiveSurveyReader
{
    /// <summary>
    /// Returns whether the survey is currently Active AND within its active period.
    /// M-04 uses this at response-submission time to enforce BR-3.4 (before-start refuse)
    /// and the tenant `post_expiry_feedback_collection` handling (Q5 — M-04 reads the
    /// setting live from M-11 and combines with the returned state).
    /// </summary>
    Task<ActiveSurveyState> GetStateAsync(SurveyId surveyId, DateTimeOffset asOf, CancellationToken ct);
}

public sealed record ActiveSurveyState(
    SurveyStatus Status,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? ExpiresAt);
```

- `Status == Active` AND `asOf < ExpiresAt` (or `ExpiresAt is null`) → accept the response.
- Else — M-04 handles rejection / post-expiry routing per BR-3.1.

---

## `IActiveSurveyDefinitionProbe` (thin diagnostic helper)

Consumed by the M-02 admin surface to answer "is this survey deliverable?" without instantiating a full render plan.

```csharp
public interface IActiveSurveyDefinitionProbe
{
    Task<bool> IsDeliverableAsync(SurveyId surveyId, CancellationToken ct);
}
```

`true` when the survey is `Active` AND passes the Publish content-gate (BR-1.7). Kept small on purpose so M-02's admin rule builder can quickly filter the survey dropdown.

---

## Reverse dependencies (M-01 depends on these — declared for wiring only)

M-01 depends on the published interfaces of six other modules — enumerated in [research.md § 4](../research.md#4-cross-module-contracts). These are **not** M-01's contract to other modules; they are contracts M-01 consumes:

| Interface | Owner | Purpose |
|---|---|---|
| `IJourneyReader` | M-16 (`Nabadat.CustomerJourneyManagement`) | Validate journey/stage/touchpoint bindings on KPI questions (FR-8.4, BR-8.5). |
| `IKpiCatalogReader` | M-06 (`Nabadat.KpiManagement`) | Active KPI catalogue for the F8 palette + KPI question fields. |
| `ITenantSettingsReader`, `ITenantDesignGuidelinesReader` | M-11 (`Nabadat.TenantAdmin`) | Tenant settings + inherited-mode appearance defaults (F4). |
| `IPermissionChecker` | M-10 (`Nabadat.UserManagement`) | Base RBAC checks. |
| `IResponsePurgeService` | M-04 (**new**) | BR-1.6 destructive Return-to-Draft — hard-delete responses + invalidate in-flight sessions. **Requires constitution AMENDMENT to add `survey.responses.purged` to Section 4.** |
| `IEventLogWriter` | M-17 (`Nabadat.EventLog`) | Emit `survey.published`, `survey.archived`, audit entries. |
| `INotificationDispatcher` | M-09 (`Nabadat.Notifications`) | Broadcast reviewer notifications on Submit-for-Review (Q7, FR-15.2). |
| `IFileStorageService` | shared platform | F4 logo upload (ClamAV + CMK envelope). |

**Draft AMENDMENT text** (for the M-01 owner to raise with the platform architect before US1 lands):

> **AMENDMENT-012 — M-01 Owned Tables & New Events**
>
> 1. **Module Registry (Section 3)**. M-01's owned-tables entry is corrected from the placeholder list (`surveys, questions, question_bank, survey_versions, survey_templates`) to the actual Feature 004 set (9 tables, per data-model.md §2.1–2.9): `surveys, sections, questions_sets, questions, routing_maps, themes, survey_translations, templates, template_snapshots`. `question_bank` moves out of M-01's scope (M-06 catalogue owns question-bank concepts); `survey_versions` is dropped (Q6 destructive Return-to-Draft-to-edit means no `version` column is needed). **Correction (`/speckit-analyze` 2026-07-15): the previous draft of this list included a `question_translations` table that does not exist in data-model.md — removed; per-question translatable strings live as keys inside `survey_translations`.**
>
> 2. **Event Catalogue (Section 4)**. Registers four new events, all required by tasks.md's US1/US2 unit, integration and scenario tests (`SurveyLifecycleServiceTests`, `SurveyLifecycleFromDraftToActiveScenarioTests`, `SurveyApprovalWorkflowScenarioTests`, `SurveyLifecycleEndpointTests`) — **added by `/speckit-analyze` (2026-07-15); the original draft registered only the first row below, leaving the other three referenced by tests but never catalogued:**
>
>    | Event | Source Module | Downstream Modules |
>    |---|---|---|
>    | `survey.responses.purged` | `M-04` | `M-05`, `M-06`, `M-07` (each drops derived aggregates for the survey) |
>    | `survey.created` | `M-01` | — |
>    | `survey.status.changed` | `M-01` | — |
>    | `survey.submitted_for_review` | `M-01` | — |
>
>    `survey.responses.purged` is emitted by M-04 at the tail of `IResponsePurgeService.PurgeSurveyResponsesAsync(...)`; payload `{ survey_id, purged_response_count, invalidated_session_count, actor_id, correlation_id }` — introduced to support M-01's BR-1.6 (destructive Return-to-Draft-to-edit — see spec.md Q6 Session 2026-07-14). The other three are emitted by M-01 itself: `survey.created` on `POST /surveys`; `survey.status.changed` on every status transition (Pause/Reactivate/Archive/Unarchive, payload carries `{from, to}`); `survey.submitted_for_review` on Draft → Pending review (feeds M-09's reviewer broadcast, FR-15.2). None currently have downstream consumers registered at Phase 1 — same pattern as the existing `survey.published`/`survey.archived` rows.

This amendment MUST be filed and ratified before US1's destructive Return-to-Draft path ships (Foundational task in tasks.md); the three M-01-sourced events additionally block T044/T102/T110/T124/T125 from legally emitting them per constitution §12.2 ("a question not answered here is flagged for amendment, not silently resolved in the spec").
