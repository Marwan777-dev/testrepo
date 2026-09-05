# Quickstart — Survey & Form Builder (M-01)

**Feature branch**: `004-survey-form-builder`

**Purpose**: Runnable validation scenarios that prove the M-01 slice works end-to-end. Each scenario references the concrete route in [contracts/](./contracts/) and the entity in [data-model.md](./data-model.md); it does **not** duplicate DTO shapes or service-implementation code (those belong in `tasks.md` / the source tree).

**Prerequisites** — verified in the sequence below before running any scenario:

1. **Backend**:
   - `.NET 10 SDK` installed.
   - PostgreSQL 16+ reachable at `localhost:5432` (see `docker-compose.yml`) with the module `_Baseline.sql` applied for the target tenant (e.g. `tenant_dev`). Apply via `dotnet run --project tools/Nabadat.Migrations -- --target=tenant --tenant=dev` after building.
   - Elasticsearch 8+ reachable at `https://localhost:9200` (self-signed cert accepted by the app).
   - The following modules already booted with their published interfaces reachable in-process: **M-10** (users + `IPermissionChecker`), **M-11** (`ITenantSettingsReader`, `ITenantDesignGuidelinesReader`), **M-16** (`IJourneyReader` — needs at least one journey seeded), **M-06** (`IKpiCatalogReader` — needs at least CSAT + NPS active), **M-17** (`IEventLogWriter`), **M-09** (`INotificationDispatcher`), **M-04** (`IResponsePurgeService` + `IActiveSurveyReader` — see foundational blocker in [research.md § 4.5](./research.md#45-iresponsepurgeservice-m-04-new-port); scenarios 5 and 6 are skippable until this ships).
2. **Frontend**:
   - `frontend/` running via `npm run dev` (Vite on `http://localhost:5173`; `/api` proxied to the backend HTTPS URL — see CLAUDE.md § Backend Integration).
   - Signed-in P-01 test user via the seeded MFA flow (`E2E_BASE_URL` set for the E2E lane if running headless).

**Setup commands** (from repo root):

```powershell
# 1. Stop any locked backend process (constitution AMENDMENT-007 / CLAUDE.md dev workflow).
Get-Process -Name "Nabadat.TenantAdmin" -ErrorAction SilentlyContinue | Stop-Process -Force

# 2. Build backend + apply migrations.
dotnet build Nabadat.sln
dotnet run --project tools/Nabadat.Migrations -- --target=control-plane
dotnet run --project tools/Nabadat.Migrations -- --target=tenant --tenant=dev

# 3. Start backend.
dotnet run --project src/Nabadat.TenantAdmin

# 4. In another shell: start the SPA.
cd frontend
npm run dev
```

---

## Scenario 1 — Author a Draft, complete Settings, add a KPI question (US1 happy path)

**References**: [surveys.md](./contracts/surveys.md), [sections-and-sets.md](./contracts/sections-and-sets.md), [questions.md](./contracts/questions.md).

**Test / Run**:

1. Sign in as P-01 (`s.saif@qbs.jo` in dev) and open the sidebar entry **Surveys**. The Survey Library (F1) opens with `GET /api/v1/surveys` — an empty list on first run.
2. Click **Add Survey** → the F5 build-method chooser appears (three tiles). Choose **From scratch**.
3. Fill Survey Settings:
   - **Survey name (English)**: `Post-visit satisfaction`.
   - Leave **Bound journey** empty (survey_type stays `SeasonalRelational`).
   - Layout: `One page per section`.
   - Save on Continue.
4. `POST /api/v1/surveys` fires with `Idempotency-Key: <fresh uuid>`. Response `201` + `Location: /api/v1/surveys/{id}` + `ETag: W/"1"`.
5. Builder (F8) opens. Add a Section → `POST /sections` returns `201` + section ETag `W/"1"`.
6. Add a **Scale** question, subtype `Stars`, text `How would you rate your visit?`. `POST /questions` returns `201`. Persist a **KPI (CSAT)** question with `bound_journey_on = true`, `stage_id = null`, `touchpoint_id = null` — response 400 `kpi.touchpoint.requires_stage`? **No**: FR-8.4 says Touchpoint is optional; stage IS optional too if journey is unset (this survey has no journey bound). Attempt again with `bound_journey_on = false` — response 200 with warning header `X-Warning: kpi.binding_ignored_when_bound_journey_off` (BR-8.2).

**Expected outcome**:
- Survey row exists with `status = Draft`, `row_version = 3` (after 2 writes: create + settings save; the section/question writes bump the section/question ETags and *also* the survey's row_version, which is how the client detects concurrent edits).
- One audit-log entry per write via `IEventLogWriter` under a shared `correlation_id`.
- The library re-lists the survey with the correct Type / Journey / Status columns.

---

## Scenario 2 — Publish-gate rejects an empty survey (BR-1.7, Q9)

**References**: [surveys.md § POST /surveys/{id}/status](./contracts/surveys.md#post-apiv1surveysidstatus).

**Steps**:

1. From Scenario 1, before adding any Section, attempt `POST /api/v1/surveys/{id}/status` body `{ "to": "Active" }` with the current ETag.
2. Response `409 survey.publish.requires_content` with body:
   ```json
   { "error": { "code": "survey.publish.requires_content", "message": "…",
                "correlation_id": "…", "tenant_id": "…" ,
                "details": { "missing_sections": true, "missing_questions": true } } }
   ```
3. Add one Section but zero questions → same 409 with `missing_sections: false, missing_questions: true`.
4. Add one question → 200 + Survey view now `status = Active`.

**UI verification**: with the SPA open, the **Publish** button in the builder header is disabled with a tooltip "Add at least one section and one question before publishing" whenever the gate would fail (BR-1.7 non-modal — no confirmation dialog required).

---

## Scenario 3 — Pause an Active survey with connected rules (FR-1.10)

**References**: [surveys.md § POST /surveys/{id}/status](./contracts/surveys.md#post-apiv1surveysidstatus).

**Setup** (out-of-scope for M-01 to create rules; use a helper in `SurveyBuilderTestSeed.SeedRule(surveyId)` in the integration lane, or use the M-02 admin UI in the dev tenant):

1. Survey from Scenario 2 is Active. Attach two M-02 distribution rules to it.
2. `POST /api/v1/surveys/{id}/status` body `{ "to": "Paused" }` (no `confirm`).
3. Response `409 survey.pause.requires_rules_confirmation` with `details.rules_count = 2`.
4. UI opens the blocking Pause dialog showing "This survey has 2 distribution rules connected …" (FR-1.10 wording); user clicks **Pause survey**.
5. Client re-submits with `?confirm=true` + `Idempotency-Key`. Response `200` — status now `Paused`, rules preserved, `rules_count = 2`.

---

## Scenario 4 — Enable answer routing (F9, US4)

**References**: [questions.md § POST /surveys/{id}/routing](./contracts/questions.md#post-apiv1surveysidrouting).

**Steps**:

1. Return the Scenario 3 survey to Draft (via Scenario 5 below) or start a fresh one.
2. Set `layout = "single"` (default), then attempt `POST /api/v1/surveys/{id}/routing {enabled: true, confirm: true}` → `409 routing.layout_required`.
3. Change layout to `"question"` via `PUT /api/v1/surveys/{id}` (with `If-Match`).
4. `POST /routing {enabled: true, confirm: true}` → `200`; response body shows `routing_on = true, shuffle = false, shuffle_locked = true`.
5. Add a KPI (CSAT) question with layout eligible. Save `PUT /questions/{id}/routing { map: { "1": "__end" } }` — 200. Set `map["1"] = "__end"` means Score 1 → end survey (SRS key scenario).

**UI verification**: the F9 confirmation modal ("Enable question routing? — Cancel / Enable routing"), the "Routing set" badge on the KPI card, and the routing editor rows one-per-answer are all visible in the builder.

---

## Scenario 5 — Destructive Return-to-Draft-to-edit (BR-1.6, Q6)

**References**: [surveys.md § Status Transition Matrix](./contracts/surveys.md#post-apiv1surveysidstatus) · [research.md § 4.5](./research.md#45-iresponsepurgeservice-m-04-new-port).

**Prerequisite**: M-04 has shipped `IResponsePurgeService`. Otherwise the endpoint returns `501 survey.return_to_draft.purge_service_unavailable` and this scenario is skipped.

**Steps**:

1. Take an Active survey with at least one collected response (seed via M-04 test helper: `SurveyBuilderTestSeed.SeedResponse(surveyId, respondentId)`).
2. `POST /api/v1/surveys/{id}/status {to: "Draft"}` (no `confirm`, no `Idempotency-Key`).
3. Response `409 survey.return_to_draft.destructive_confirmation_required` with `details.responses_count = 1`.
4. UI opens `DestructiveReturnToDraftDialog` with message "**All 1 response collected for this survey will be permanently deleted, including any responses in the post-expiry store. Anyone currently mid-survey will not be able to submit. This cannot be undone.**" — user clicks **Return to Draft & delete responses**.
5. Client re-submits with `?confirm=true` + `Idempotency-Key`. Response `200`.
6. Verify:
   - Survey `status = Draft`; `row_version` incremented.
   - `GET /api/v1/surveys/{id}` shows `responses_count = 0`.
   - A single M-11 audit entry via M-17 records actor, timestamp, previous status `Active`, `purged_response_count = 1`.
   - `survey.responses.purged` event present in `event_log`.
   - Attempting to submit the seeded response now (via M-04's response API) returns the expiry-style rejection — the in-flight session is invalidated.

**Idempotency check**: re-issuing the same status change with the same `Idempotency-Key` returns the same 200 payload without re-executing the purge (APIs-constitution Article 7.1).

---

## Scenario 6 — Approval workflow with reviewer broadcast (US2, Q7)

**References**: [approval-workflow.md](./contracts/approval-workflow.md).

**Setup**: at least one P-01 user other than the P-03 submitter exists. In dev, seed with `SurveyBuilderTestSeed.SeedUser(role="P-01", email="reviewer@dev")`.

**Steps**:

1. Sign in as P-03. Create a Draft, add ≥1 section + ≥1 question.
2. `POST /api/v1/surveys/{id}/submit`. Response `200`; status → `PendingReview`, submit_by / submitted_at populated.
3. Check that the M-09 notification was broadcast to every P-01 in the tenant (Q7): query M-09's notification store (dev tool: `GET /api/v1/notifications?scope=me` while signed in as the reviewer) → one notification per P-01 with the deep-link `/surveys/{id}`.
4. As the reviewer P-01, follow the deep link (F3 Settings loads). Click **Publish**.
5. `POST /api/v1/surveys/{id}/publish` with `Idempotency-Key`. Response `200`; status → `Active`; `survey.published` in `event_log`.
6. As the submitter P-03, try to edit while the survey was in PendingReview (step 3–4) — every PUT returns `403 survey.edit_locked_by_pending_review` (BR-15.1).

---

## Scenario 7 — Save a survey as a template, then instantiate it (US5, Q4/BR-7.1)

**References**: [templates.md](./contracts/templates.md).

**Steps**:

1. From the Scenario 6 survey (now Active), `POST /api/v1/templates {source_survey_id: <id>, name_en: "Post-visit template", tags: ["visit","branch"]}` with `Idempotency-Key`. Response `201`.
2. `GET /api/v1/templates/{tid}/preview` returns the redacted Survey view; verify appearance + KPI bindings visible.
3. `POST /api/v1/templates/{tid}/instantiate {}` with `Idempotency-Key`. Response `201` with a new survey id.
4. `GET /api/v1/surveys/{newId}` — asserts:
   - `name_en = "Post-visit template"` (copied from the template default).
   - Sections / sets / questions / KPI bindings match the source.
   - `owner_user_id = caller`, `status = Draft`.
   - **No `template_id` field on the survey view** — snapshot-no-link (BR-7.1).
5. `DELETE /api/v1/templates/{tid}` (with `If-Match`). Response `200`.
6. `GET /api/v1/surveys/{newId}` still returns the instantiated survey intact — the template's deletion did not cascade (BR-7.1).

---

## Scenario 8 — Multi-channel preview (US7, F12)

**References**: [report-and-analytics.md § GET /surveys/{id}/preview](./contracts/report-and-analytics.md#get-apiv1surveysidpreview).

**Steps**:

1. Open the Preview page for the Scenario 7 instantiated survey. Default channel = Desktop web.
2. Switch to WhatsApp → the `LivePreviewFrame` re-renders with WhatsApp chrome; content identical.
3. Change layout to `question` in a background tab → the preview pagination reflects immediately (SC-003 100 ms budget).
4. Switch locale to Arabic → the frame renders RTL; `dir="rtl"` on the root; Arabic keys are the ones present in the survey's `translations`, fallback to English where missing (`LocaleFallbackPolicy`).

---

## Scenario 9 — Report + Analytics with a seeded fixture (US8/US9)

**References**: [report-and-analytics.md](./contracts/report-and-analytics.md).

**Setup**: use the E2E `SurveyBuilderApplicationFactory` fixture to seed `tenant_dev_analytics` and `tenant_dev_responses` with deterministic docs (see `Infrastructure/EsTestcontainer.cs`).

**Steps**:

1. Load the Report for the Scenario 6 survey. `GET /report?period=last_7_days` returns metric cards + KPI gauges + per-question payloads that match the fixture (unit-test cases in [spec.md US8](../spec.md)).
2. Change period to `last_month` — every card + chart recomputes; `PeriodResolver` picked the correct `from/to` window.
3. Click **show more** on a Text/Paragraph question — `GET /report/verbatims?question_id=… &limit=100` returns up to 100 newest-first.
4. Open Analytics. `GET /analytics?period=last_7_days&granularity=daily` returns the funnel + channels + trend series.
5. Delete the previous-period fixture docs and re-open — deltas are `null` (FR-14.5), no `+0%` misleading placeholder.

---

## Success criteria coverage

Every scenario above maps to at least one Success Criterion in [spec.md § Success Criteria](../spec.md#measurable-outcomes):

| Scenario | SC covered |
|---|---|
| 1 | SC-001 (time-to-first-Active), SC-002 (library load < 1.5 s), SC-008 (accessibility a11y scan on the builder). |
| 2 | Publish gate is enforced (BR-1.7 — new invariant introduced by Q9). |
| 3 | SC-006 (rule-pause miscommunication = 0). |
| 4 | Routing surface behaviour end-to-end. |
| 5 | SC-009 partial (post-expiry integrity — verified when M-04 ships the purge). |
| 6 | SC-005 (approval-lag). |
| 7 | SC-010 (template fidelity). |
| 8 | SC-003 (preview responsiveness). |
| 9 | SC-007 (report/analytics accuracy). |

**Verification cadence**: at each per-story checkpoint (CLAUDE.md Unit Test Policy rule 6), run the affected scenario above end-to-end after `dotnet test tests/Nabadat.SurveyBuilder.UnitTests && dotnet test tests/Nabadat.SurveyBuilder.IntegrationTests` are green.
