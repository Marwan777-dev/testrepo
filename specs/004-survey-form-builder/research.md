# Research Log — Survey & Form Builder (M-01)

**Feature branch**: `004-survey-form-builder`
**Purpose**: Resolve every technology, pattern, and integration unknown flagged in [plan.md § Technical Context](./plan.md#technical-context) so that Phase 1 (data model, contracts, quickstart) can be authored without deferred decisions. The nine `/speckit-clarify` questions (Q1–Q9) resolved *in spec.md* are not re-litigated here; they are load-bearing inputs.

Each decision below records **what was chosen**, **why**, and **what alternatives were rejected**.

---

## 1. HTML sanitiser for welcome / thank-you rich-text editor

**Context**: Q3 fixed the allowlist policy — Full HTML5 minus `<script>` / `on*` handlers / `javascript:` URLs, no `<iframe>` by default. Now the implementation library must be picked.

**Decision**: **Ganss.Xss (`HtmlSanitizer` NuGet, v9.x)**. Configured centrally in `Nabadat.SurveyBuilder.Infrastructure.HtmlSanitisation.GannsHtmlSanitiserAdapter` behind the `Application/HtmlSanitisation/Interfaces/IHtmlSanitiser` port. The allowlist is materialised as a static `SanitiserPolicyV1` record with a `PolicyVersion = 1` field; every survey save persists the applied `sanitiser_policy_version` on the row so an audit trail exists (Q3 requirement: "auditable and versioned").

**Rationale**:
- Ganss.Xss is battle-tested (13+ years of production use, > 200 M NuGet downloads), OWASP-cheatsheet-aligned, and actively maintained.
- Pure .NET; no external process; no cache; fits AD-03 (no caching layer).
- Configurable tag / attribute / CSS-property / URL-scheme allowlists match the Q3 shape 1:1.
- MIT-licensed — compatible with the platform's licensing posture and the FluentAssertions 6.12.x pin (CLAUDE.md rule 14).

**Alternatives rejected**:
- **AngleSharp + custom whitelister** — more flexible but re-implements what Ganss already does correctly; higher defect surface for a security-critical path.
- **Client-side DOMPurify only** — violates architecture-constitution Article 5 ("security enforced server-side"). Client-side may run *in addition to* Ganss for UX (immediate warning in the editor) but is never the boundary.
- **Regex-based stripping** — well-known XSS trap; rejected on principle.

**Sanitiser allowlist (v1)**:
- **Allowed tags**: `p`, `br`, `b`, `strong`, `i`, `em`, `u`, `h1`, `h2`, `h3`, `h4`, `h5`, `h6`, `ul`, `ol`, `li`, `a`, `blockquote`, `code`, `pre`, `span`, `div`, `hr`, `img` (only with allowlisted `src` schemes).
- **Allowed attributes**: `href` (on `<a>`, allowlisted schemes only), `title`, `target`, `rel`, `dir`, `lang`, `class` (limited to a whitelist of design-system tokens), `src` + `alt` (on `<img>`).
- **Allowed URL schemes**: `https`, `mailto`, `tel`. `http` is *stripped* (forces HTTPS per architecture-constitution Article 1.2). `javascript:` and `data:` (except `data:image/*` for `<img>`) are stripped.
- **Stripped unconditionally**: `<script>`, `<iframe>`, `<object>`, `<embed>`, `<style>`, `<link>`, `<meta>`, every `on*` event-handler attribute.

---

## 2. ETag strategy for optimistic locking (Q1)

**Context**: Q1 mandates optimistic locking on all M-01 write endpoints (`If-Match: <etag>` → 412 on mismatch). Two candidate implementations: DB row-version, or a monotonic app-managed integer.

**Decision**: **Monotonic `row_version` integer column** on each mutable aggregate root (`surveys.row_version`, `sections.row_version`, `questions_sets.row_version`, `questions.row_version`, `themes.row_version`, `templates.row_version`, `translations.row_version`). Every write increments it atomically inside `ITenantDbContext.ExecuteAsync`; the ETag returned to the client is `W/"<row_version>"` (weak ETag).

**Rationale**:
- Deterministic and cheap: a single `int4` compare inside the same transaction that performs the write; no dependence on PG's own concurrency tokens (which change semantics under transaction pooling — see database-constitution Article 3.3).
- Works across PgBouncer transaction pooling — no need for cross-statement `xmin` tracking.
- Trivially serialisable to a client Header; client can echo it back without further transformation.
- Matches the M-10 reference pattern for concurrency (already in production).

**Alternatives rejected**:
- **`xmin` system column as ETag** — breaks under transaction pooling (each `SELECT` may see a different snapshot); constitution-mandated pooling forbids it.
- **`updated_at` timestamp** — collisions on sub-millisecond writes; not monotonic across clock corrections.
- **EF `[Timestamp]` `byte[]`** — Npgsql provider translates to `xmin` — same problem.

**Application flow**:
1. Client `GET /api/v1/surveys/{id}` → response body + `ETag: W/"7"`.
2. Client mutates and sends `PUT /api/v1/surveys/{id}` with `If-Match: W/"7"`.
3. Server loads the row, checks `row_version == 7`; if match → apply mutation, set `row_version = 8`, return 200 + `ETag: W/"8"`; if mismatch → 412 + `{"error":{"code":"survey.conflict","message":"...","correlation_id":"...","tenant_id":"..."}}`.

**Compound writes** (Section + Questions Sets in the same `ExecuteAsync`): each aggregate root maintains its own `row_version`. A cross-aggregate write increments only the aggregates it actually touches. A `POST /surveys/{id}/sections` bumps only `surveys.row_version` (the parent) — child rows have their own ETags. See [contracts/](./contracts/) for per-endpoint ETag scopes.

---

## 3. Elasticsearch client & query patterns for Report / Analytics

**Context**: F13 (Report) and F14 (Analytics) read exclusively from `tenant_{tenantId}_analytics` and `tenant_{tenantId}_responses` (AD-04). M-01 needs an ES client, a query builder, and permission-safe result assembly.

**Decision**:
- **Client**: `Elastic.Clients.Elasticsearch` 8.x (the modern strongly-typed client; supersedes `NEST`). Injected via `Infrastructure/Elasticsearch/EsClientFactory.cs` as a singleton `ElasticsearchClient`.
- **Index resolution**: `tenant_{tenantId}_responses` and `tenant_{tenantId}_analytics` names are computed from `ICurrentTenant.TenantId` at request time; index names are **not** stored in code paths that could leak across tenants.
- **Query builder**: hand-written per-endpoint DSL in `Infrastructure/Elasticsearch/EsQueryBuilder.cs`; period filter always applied as the first `range` clause; permission/scope filters (`P-02` may see all-tenant analytics; `P-05` sees own-cases only) applied server-side *before* the query is dispatched (APIs-constitution Article 4.5).
- **HTTPS on port 9200** only (constitution AD-04 / APIs-constitution Article 8.3).

**Rationale**:
- Modern strongly-typed client provides compile-time schema safety and DTO shaping consistent with our EF DTO style.
- Hand-written per-endpoint queries beat a generic search-abstraction library for this small, well-scoped surface: only ~10 query shapes across F13/F14.
- Elasticsearch does no authorisation — the platform builds every filter clause in `nabadat-api` before dispatching (APIs-constitution Article 4.5).

**Alternatives rejected**:
- **NEST 7.x** — deprecated by Elastic in favour of `Elastic.Clients.Elasticsearch`; no upgrade path.
- **Raw HTTP with `HttpClient`** — cheap in the short term, painful long-term; loses schema validation.
- **PostgreSQL for analytics reads** — forbidden by AD-04.

**Fixture / integration testing**: `EsTestcontainer` in `IntegrationTests/Infrastructure/` spins up Elasticsearch 8.x per fixture, seeded via a helper that writes fixture response docs to `tenant_{tenantId}_responses` and pre-aggregated docs to `tenant_{tenantId}_analytics`. The Report / Analytics scenario tests then assert the shaped response. **The M-04 → ES ingest pipeline is out of scope**; the integration tests seed ES directly.

---

## 4. Cross-module contracts

M-01 depends on published interfaces from **six** modules. This section captures the exact method shape expected on each — the plan's Cross-module dependency list flags M-04 as new-work; the others already exist in their reference modules.

### 4.1 `IJourneyReader` (M-16, `Nabadat.CustomerJourneyManagement`)

**Purpose**: Enumerate the active journeys / stages / touchpoints and validate KPI bindings (FR-8.4, BR-8.5).

```csharp
namespace Nabadat.CustomerJourneyManagement.Domain.Interfaces;

public interface IJourneyReader
{
    Task<IReadOnlyList<JourneyView>> ListActiveJourneysAsync(CancellationToken ct);
    Task<JourneyView?> GetJourneyAsync(JourneyId id, CancellationToken ct);
    Task<IReadOnlyList<StageView>> ListStagesAsync(JourneyId journeyId, CancellationToken ct);
    Task<IReadOnlyList<TouchpointView>> ListTouchpointsAsync(JourneyId journeyId, StageId stageId, CancellationToken ct);

    // BR-8.5 — is the (kpi, journey, stage, touchpoint) combination still valid?
    Task<bool> IsBindingValidAsync(KpiCode kpi, JourneyId journeyId, StageId? stageId, TouchpointId? touchpointId, CancellationToken ct);
}
```

**Notes**: `JourneyView` / `StageView` / `TouchpointView` are lightweight read-model DTOs that M-16 already publishes for other modules (existing pattern from feature 002). M-01 depends on them by reference — no local copy.

### 4.2 `IKpiCatalogReader` (M-06, `Nabadat.KpiManagement`)

**Purpose**: Read active KPIs for the F8 palette + KPI question fields (`KPI`, `Perspective`, `Reason follow-up items`, scale definition for KPI-capable question types).

```csharp
namespace Nabadat.KpiManagement.Domain.Interfaces;

public interface IKpiCatalogReader
{
    Task<IReadOnlyList<KpiCatalogEntry>> ListActiveKpisAsync(CancellationToken ct);
    Task<KpiCatalogEntry?> GetKpiAsync(KpiCode code, CancellationToken ct);
    Task<IReadOnlyList<KpiPerspective>> ListPerspectivesAsync(KpiCode code, CancellationToken ct);
    Task<KpiScaleDefinition> GetScaleAsync(KpiCode code, CancellationToken ct);
}
```

Feature 003 shipped this on `Nabadat.KpiManagement`; confirm before US1 lands.

### 4.3 `ITenantSettingsReader` + `ITenantDesignGuidelinesReader` (M-11)

**Purpose**: Read the tenant-level `post_expiry_feedback_collection` flag (Q5) at each M-04 dispatch, and the inherited-mode appearance defaults for F4.

```csharp
namespace Nabadat.TenantAdmin.Domain.Interfaces; // M-11

public interface ITenantSettingsReader
{
    Task<TenantSetting<bool>> GetPostExpiryFeedbackCollectionAsync(CancellationToken ct);
    Task<TenantSetting<TimeZoneInfo>> GetTimeZoneAsync(CancellationToken ct);
    Task<TenantSetting<CultureInfo>> GetDefaultCultureAsync(CancellationToken ct);
}

public interface ITenantDesignGuidelinesReader
{
    Task<TenantDesignGuidelines> GetAsync(CancellationToken ct);
}
```

The `post_expiry_feedback_collection` value is read live by M-04 (Q5) — this port exists for M-01's own display of "post-expiry feedback: ON" in Survey Settings, not for enforcement.

### 4.4 `IPermissionChecker` (M-10, `Nabadat.UserManagement`)

Standard reference pattern from M-10:

```csharp
namespace Nabadat.UserManagement.Domain.Interfaces;

public interface IPermissionChecker
{
    Task<bool> HasPermissionAsync(PermissionCode code, CancellationToken ct);
    Task<bool> HasGrantAsync(GrantCode code, CancellationToken ct);   // "PublishOwnSurveys"
}
```

Q8 team-owned semantics are enforced at the M-01 endpoint filter — the `IPermissionChecker` returns whether the caller has *the base permission* (`SurveyEdit`); the M-01 filter then applies the Q8 rule that any P-03 in the tenant can edit any P-03-authored Draft (no per-owner check).

### 4.5 `IResponsePurgeService` (M-04, **new port**)

**Purpose**: Support BR-1.6 (Q6 destructive Return-to-Draft-to-edit) — hard-delete every Response for a survey plus invalidate every open in-flight session token, atomically from M-04's perspective.

```csharp
namespace Nabadat.ResponseCollection.Domain.Interfaces; // M-04

public interface IResponsePurgeService
{
    /// <summary>
    /// Hard-deletes every Response row for the given survey across both the live `responses`
    /// table and the M-07 post-expiry store, and invalidates every open in-flight session
    /// token for the survey. Emits `survey.responses.purged` (a new M-17 event — see plan.md
    /// § Cross-module dependency).
    /// </summary>
    Task<ResponsePurgeResult> PurgeSurveyResponsesAsync(SurveyId surveyId, ActorId actor, CorrelationId correlationId, CancellationToken ct);
}

public sealed record ResponsePurgeResult(int PurgedResponseCount, int InvalidatedSessionCount);
```

**Cross-module coordination**: M-04 must ship this port before US1's destructive-return path is unblocked. **Event catalogue impact**: introducing `survey.responses.purged` requires a constitution AMENDMENT (constitution § 4 — "New event types require a constitution amendment"). Track this as a **cross-module blocker** in `tasks.md`'s Foundational phase. Fallback until it lands: US1 backend still delivers every other status transition; the destructive Return-to-Draft is deferred and returns 501 `not_implemented` from `/status {"to":"Draft","fromActive":true}`, with an explanatory error message.

### 4.6 `IEventLogWriter` (M-17)

M-01 emits `survey.published` and `survey.archived`. Constitution Section 4 already registers these events with no downstream consumers at Phase 1, so no coordination is required beyond calling the port.

```csharp
namespace Nabadat.EventLog.Domain.Interfaces; // M-17

public interface IEventLogWriter
{
    Task WriteAsync(EventType type, object payload, ActorId actor, CorrelationId correlationId, CancellationToken ct);
}
```

### 4.7 `IFileStorageService` (shared file-storage adapter)

For F4 `survey_logo` uploads — ClamAV scan + CMK envelope encryption (database-constitution Article 6).

```csharp
public interface IFileStorageService
{
    Task<FileHandle> UploadAsync(Stream content, string fileName, string contentType, CancellationToken ct);
    Task<Stream> DownloadAsync(FileHandle handle, CancellationToken ct);
    Task DeleteAsync(FileHandle handle, CancellationToken ct);
}
```

The concrete implementation lives in a shared infrastructure library (already present); M-01 only references the port.

---

## 5. Question Type Catalogue → EF mapping strategy

**Context**: The spec's Question Type Catalogue is authoritative on the 8 question types (7 answer + KPI) × sub-types (Labels / Stars / Smileys / Slider for Scale; Text / Paragraph / Number / Date / Time / Date-Time / Month for Input Field; etc.). Per-type fields differ widely (Scale needs `slider_lower/higher/steps` OR `point_count + labels[]`; KPI needs `kpi_code + perspective + bound_journey`; Matrix needs `matrix_mode + rows[] + columns[]`; …).

**Decision**: **Single `questions` table + per-type discriminated payload column** (`jsonb`, validated at the application layer per database-constitution Article 4.6). The `questions` table has the common columns (`id`, `section_id`, `set_id` nullable, `order`, `type`, `subtype`, `text`, `description`, `required`, `comments`, `comment_label`, `comment_max_length`, `sentiment`, `row_version`, `created_at`, `updated_at`) plus a `type_payload jsonb` column that stores the per-type fields (validated by a discriminated union in `Application/Questions/QuestionValidator.cs` before persist).

**Rationale**:
- Single table = simple queries in `render-plan`, in the Report per-question view, and in the Section cascade (drag-and-drop reordering).
- `jsonb` with app-layer validation matches database-constitution Article 4.6 ("Columns by default, jsonb where justified") — per-type fields are exactly the "narrowly used, validated on publish" case that justifies jsonb.
- EF Core `HasConversion` maps the payload to a strongly-typed `QuestionTypePayload` polymorphic record (System.Text.Json with a `$type` discriminator).
- Avoids table-per-type inheritance (TPT) which would need joins on every render and every drag-and-drop reorder.

**Alternatives rejected**:
- **Table-per-type inheritance** — 8 tables, joins on every read; overkill for the small per-type diff.
- **Wide-column EAV** (single-value column per attribute, `null` where inapplicable) — 30+ mostly-null columns; a maintenance mess.
- **`hstore`** — less validated and harder to query than `jsonb`; no upside.

**Payload polymorphism**:
```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ScalePayload), "scale")]
[JsonDerivedType(typeof(InputFieldPayload), "input_field")]
[JsonDerivedType(typeof(SingleSelectPayload), "single_select")]
[JsonDerivedType(typeof(MultiSelectPayload), "multi_select")]
[JsonDerivedType(typeof(YesNoPayload), "yes_no")]
[JsonDerivedType(typeof(MatrixPayload), "matrix")]
[JsonDerivedType(typeof(RankingPayload), "ranking")]
[JsonDerivedType(typeof(KpiPayload), "kpi")]
public abstract record QuestionTypePayload;
```

`QuestionValidator.Validate(...)` returns `Valid`, `Invalid(errorCode)`, or `Warn+Strip(reason)` — matching the spec's unit-test coverage cases for US1.

---

## 6. Routing map storage & default targeting

**Context**: F9 answer routing associates each answer of an eligible question with a next target (another *standalone* question or `__end`). Routes belong to the question card; a "Routing set" badge appears when at least one route is set.

**Decision**: A separate `routing_maps` table keyed by `(question_id, answer_key)`, one row per non-default target. Missing rows imply the default (`nextInOrder`). Default targets are **not persisted** — persistence carries only overrides, so a survey that clones question ordering keeps sensible defaults automatically.

**Rationale**:
- Smaller storage footprint on the common case (most routes are defaults).
- On `question.delete`, cascade-delete `routing_maps` rows referencing the deleted question as source OR target (FR-2.7 default-reset behaviour); the *default* path (next-in-order) then applies transparently.
- On `question.reorder`, no persistence change is needed — defaults recompute from `order`.

**Alternatives rejected**:
- **`jsonb` embedded on `questions.type_payload`** — mixes independent concerns; makes routing hard to query in the render-plan endpoint (which streams route decisions to M-04).
- **Storing every answer's target (defaults included)** — bloats the table and needs a rewrite on every reorder.

---

## 7. Low-response ordering algorithm (FR-10.4)

**Context**: The FR-10.4 algorithm cascades from *Questions Set → Section → Survey*: within each Set, pick the lowest-response question(s) eligible to be sent; compare with standalone questions in the section to determine the section's lowest-response question; compare across sections to determine section order.

**Decision**: Implement `LowResponseOrderingService.OrderSections(sections, responseCounts)` as a pure in-memory function operating on a `IReadOnlyDictionary<QuestionId, long> responseCounts` fed by the `render-plan` endpoint from a per-survey response-count projection stored in Elasticsearch (`tenant_{tenantId}_analytics` → `question_response_counts` document per question). No PostgreSQL read on the dispatch path.

**Rationale**:
- Pure function → fully unit-testable per FR-10.4's example fixture (Section 3.10 in spec).
- ES read is O(N) where N = questions in the survey (≤ 100 by our performance goal); comfortably inside the 50 ms p95 for `render-plan`.
- Response counts are M-04's ingest by-product (already emitted to ES per AD-04); M-01 just reads them.

**Alternatives rejected**:
- **PostgreSQL count query per dispatch** — hot-path DB read; violates AD-04.
- **Per-dispatch full-response-scan** — quadratic; kills the 50 ms budget.

---

## 8. Frontend component reuse

**Purpose**: Enumerate reuse decisions per the CLAUDE.md Component Sourcing Rule ("Search the existing codebase FIRST").

Verified in `frontend/src/components/`:

| Existing component | Reused in |
|---|---|
| `ui/button.tsx` | every action button per CLAUDE.md's action-button hierarchy (one-blue rule) |
| `ui/dialog.tsx`, `ui/sheet.tsx` | destructive dialogs (Return-to-Draft, Pause with rules), Question settings drawer |
| `ui/select.tsx`, `ui/input.tsx`, `ui/textarea.tsx`, `ui/checkbox.tsx` | every F3 / F8 form field |
| `ui/table.tsx` | F1 Survey Library — bordered card + sticky header per CLAUDE.md |
| `ui/badge.tsx` | Comment field / Routing set / KPI · Stage → Touchpoint pill badges |
| `ui/card.tsx` | F3 / F4 / F8 / F13 / F14 surfaces — respects the 16px radius ceiling |
| `ui/tabs.tsx` (base-ui `Tabs.Panel`) | Templates picker (Customized vs Built-in) |
| `cx/kpi-flip-card` (if present) | F13 per-question KPI card visual |
| `cx/journey-map/*` (from feature 002) | F13 touchpoint counter metric card |
| `lib/utils.ts` `cn()` | every className merge |
| `lib/journey-data.ts` `perfColor` / `perfLevel` | F13 KPI gauge fill + per-question card colouring |
| App-level `SidebarProvider`, `TopBar` | reused as-is; the new sidebar entries for "Surveys" and "Templates" are registered under `nav.platform` with new i18n keys |

**Net-new components** (all justified by absent equivalents; each honours the design system):

| New component | Purpose |
|---|---|
| `QuestionPalette.tsx` | F8 draggable palette (7 answer types + KPI under Metric heading) |
| `QuestionCard.tsx` | F8 canvas card with sub-type / KPI binding / routing / comment / sentiment toggles |
| `KpiBindingEditor.tsx` | F8 layered Bound journey → Stage → Touchpoint editor (FR-8.4, BR-8.5) |
| `RoutingMapEditor.tsx` | F9 per-answer target selector |
| `SectionColumn.tsx` | F2/F8 section container (drag-and-drop parent) |
| `QuestionsSetCard.tsx` | F10 set settings sub-window inside a section |
| `AppearanceControls.tsx` | F4 controls list (Inherited vs Customize) |
| `LivePreviewFrame.tsx` | F4 + F12 preview iframe (Desktop / Mobile / WhatsApp / Email chrome) |
| `DestructiveReturnToDraftDialog.tsx` | BR-1.6 blocking confirmation with response count `N` |
| `PublishGateBanner.tsx` | BR-1.7 disabled-Publish surface with tooltip |
| `EtagConflictDialog.tsx` | Q1 stale ETag 412 handler |

**Design system enforcement pre-shipping US1**: run the two regex sweeps from CLAUDE.md § Theming self-review on every new `.tsx` file:
- `-\[#[0-9a-fA-F]{3,8}\]` → must return **0** matches.
- `style=\{\{[^}]*#[0-9a-fA-F]{6}` → judgment check; permitted only for fixed third-party mockups (e.g., WhatsApp chrome inside `LivePreviewFrame`).

---

## 9. Idempotency, ETag scope, and API-05 error codes

**Decision (idempotency)**: `Idempotency-Key` header applies to the *sensitive-write* subset of M-01 endpoints per APIs-constitution Article 7.1:
- `POST /api/v1/surveys` (create).
- `POST /api/v1/surveys/{id}/clone`.
- `POST /api/v1/surveys/{id}/status` **when the transition triggers a destructive purge** (BR-1.6 Active/Paused → Draft).
- `POST /api/v1/templates/{id}/instantiate`.
- `POST /api/v1/surveys/{id}/submit`, `.../publish`, `.../return-to-draft` (governance).

For each, the 24-hour idempotency window returns the same response body (and same audit-log entry — no double-audit) on retry.

**Decision (ETag scope)**: ETags are per aggregate root (`Survey`, `Section`, `QuestionsSet`, `Question`, `Theme`, `Template`, `Translation`); collection endpoints (`GET /api/v1/surveys`) do **not** carry an ETag. See the ETag matrix per endpoint in [contracts/](./contracts/).

**Decision (error codes)**: All M-01 error codes are prefixed by their surface, dot-namespaced:
- `survey.name_en.required`, `survey.name_en.max_length`
- `survey.conflict` (412 If-Match mismatch)
- `survey.status.invalid_transition`
- `survey.pause.requires_rules_confirmation` (409)
- `survey.publish.requires_content` (409, BR-1.7)
- `survey.archived.only_unarchive_allowed`
- `survey.return_to_draft.destructive_confirmation_required` (409, BR-1.6)
- `section.delete.requires_confirmation`, `section.not_found`
- `questionsset.count.exceeds_size`
- `question.subtype.required`, `question.type.invalid`
- `kpi.touchpoint.requires_stage`, `kpi.binding_invalid`
- `kpi.binding_ignored_when_bound_journey_off` (Warn+Strip — 200 with warning header)
- `routing.layout_required` (409 when layout ≠ question)
- `routing.source_ineligible`, `routing.target_ineligible`, `routing.inside_set_forbidden`
- `template.built_in_not_editable`
- `template.instantiate.tag_missing`
- `translation.locale.not_configured`
- `preview.channel.invalid`
- `report.period.invalid`
- `sanitiser.input_rejected` (500-level — indicates the sanitiser itself failed, distinct from a valid save producing sanitised output)

Every non-2xx wraps the code in the API-05 envelope `{"error":{"code":"...","message":"...","correlation_id":"...","tenant_id":"..."}}` with `details` when structured context (e.g., which invariant failed for the Publish gate) is useful.

---

## 10. Localisation model

**Decision**: The `Translation` entity stores per-locale bundles keyed by `(survey_id, locale)`, with `keys` as `jsonb`. Keys mirror the `TranslatableStringExtractor.Extract(...)` output: `survey.name`, `survey.welcome`, `survey.thanks`, `section.{sectionId}.title`, `question.{questionId}.text`, `question.{questionId}.description`, `question.{questionId}.options.{index}.label`, `question.{questionId}.scale_labels.{index}`, `question.{questionId}.comment_label`, `question.{questionId}.reason_items.{index}`.

**Fallback (LocaleFallbackPolicy)**: When a key is missing in the target locale, resolve to English — the source locale (spec Assumption). The Translate workspace surfaces the "missing keys" count so the author sees coverage without gating the save (`SC-004` is advisory, not a hard gate).

**Rationale**:
- Single row per (survey, locale) keeps the read path a single `SELECT` for a full bundle.
- `jsonb` keys map cleanly to a `.NET` `Dictionary<string,string>` for `EF Core` mapping via `HasConversion<JsonValueConverter<Dictionary<string,string>>>`.

---

## 11. Testing / CI implications

- **Docker required** for the integration lane (Testcontainers Postgres 16 + Elasticsearch 8). CI must publish both images before the integration lane runs.
- **`FluentAssertions 6.12.x` pin** (CLAUDE.md rule 14) — all new test projects reference this exact minor line.
- **Time**: every service takes `TimeProvider`; every unit-test class uses `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing 9.x`.
- **First-feature-in-module carve-out**: `Nabadat.SurveyBuilder.UnitTests` and `.IntegrationTests` are both created as the Foundational task before any US1 implementation task runs.
- **Red Checkpoint** (CLAUDE.md rule 7): every non-skipped backend user story emits `T0XXR` before its implementation subsection; the red commit records the failing unit-test transcript.
- **E2E lane** extends existing `tests/Nabadat.E2ETests/` — new `SurveyBuilder/` folder, one class per US, shared `Infrastructure/E2ETestBase.cs` reused.

---

## 12. Open items surfaced during Phase 0 (not blocking)

- **New event `survey.responses.purged`** — requires a constitution AMENDMENT to add to Section 4 (constitution §12.2 explicitly forbids silent additions). Drafted amendment text is in [contracts/published-interface.md](./contracts/published-interface.md) for review with the platform architect. Filed against tasks.md as a Foundational blocker.
- **AMENDMENT-011 corrected M-06's owned-tables list to `kpi_definitions, kpi_thresholds, kpi_perspectives, cxi_weights`** — confirm `IKpiCatalogReader` is already shipped by `Nabadat.KpiManagement`; if not, coordinate a small M-06 patch before US1.
- **M-01's owned-tables list in constitution Section 3** is `surveys, questions, question_bank, survey_versions, survey_templates`. **Q6 removes `survey_versions`** (no versioning column); `question_bank` is not part of Phase 1 of this feature (question banks are a M-06/library concern for the KPI catalogue, not M-01 authoring). A constitution AMENDMENT correction is required to align the module registry with the actual shipped schema. Draft text: "Corrects M-01's owned-tables entry to the actual Feature 004 set (9 tables): `surveys, sections, questions_sets, questions, routing_maps, themes, survey_translations, templates, template_snapshots`." (Per-question translation strings are jsonb keys inside `survey_translations`, not a separate `question_translations` table — `/speckit-analyze` 2026-07-15 corrected an earlier miscount here.)

Both AMENDMENT items are **coordination**, not spec defects — flagged and tracked in the Foundational phase of `tasks.md`.
