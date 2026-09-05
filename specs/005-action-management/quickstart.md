# Quickstart: M-15 Action Management

Validation guide for proving the feature works end-to-end. Not implementation code — see
`data-model.md` for entity shapes and `contracts/` for endpoint/interface details.

## Prerequisites

- Backend: `Nabadat.ActionManagement` registered in `Nabadat.TenantAdmin` (composition root
  calls `AddActionManagementModule(...)`), `ActionManagement_Baseline.sql` applied to the test
  tenant schema, `IKpiScoreReader` bound to `NullKpiScoreReader` (default — no M-06 score engine
  yet, research.md §4) or the real adapter if C-01 has shipped by the time this runs.
- A tenant with M-06's KPI catalogue seeded (at least `NPS` and `CSAT` Active) — reuse the
  Feature 003 seed fixture (`Nabadat.KpiManagement.IntegrationTests` seeding helpers) so
  `IKpiConfigReader.GetActiveAsync()` returns a non-empty list.
- Frontend: `npm run dev` in `frontend/`, `E2E_BASE_URL` pointed at the dev server if running the
  Playwright E2E lane (`tests/Nabadat.E2ETests`).
- A signed-in session as a P-01 (CX Program Manager) test user for the write scenarios below,
  and a P-06 (Executive/Viewer) session for the read-only scenario.

## Scenario 1 — Create an Action and see it on the All Actions page (US1 + US2)

1. `POST /api/v1/settings/actions` (as P-01) — confirm defaults `{ max_upper_threshold: 20,
   slider_padding: 3 }` on a fresh tenant (no call needed if defaults are acceptable).
2. `POST /api/v1/actions`:
   ```json
   {
     "action_name": "Training of Call Center Agents",
     "action_start_date": "2026-07-23",
     "action_end_date": "2026-08-06",
     "targets": [ { "kpi_id": "<NPS id>", "target_date": "2026-09-15",
                    "lower_threshold": 3.0, "upper_threshold": 6.0 } ]
   }
   ```
   **Expect**: `201`, `status: "active"` (start date = today ≤ today ≤ latest target date),
   `action.created` + `baseline.captured` (or `null` baseline + `NoBaselineScoreException`
   surfaced as ERR-5 if `NullKpiScoreReader` is bound and the caller requires a real baseline —
   confirm which behaviour is wired before asserting the happy path).
3. Open `/actions` in the browser (or `GET /api/v1/actions?tab=active`). **Expect**: the new
   Action's card in the Active tab, tab count incremented, newest-created-first ordering
   (FR-110).
4. Open `/actions/:id` (SCR-03). **Expect**: header shows `target_start_date = 2026-08-07`
   (`action_end_date + 1`, BR-006) with the derivation tooltip; one Target row for NPS.

## Scenario 2 — Retro-dated Action born Completed (US10)

1. `POST /api/v1/actions` with `action_start_date` / `action_end_date` / the single Target's
   `target_date` all in the past (`latest_target_date < today`).
2. **Expect**: `201`, `status: "completed"`. Immediately attempt `PUT /api/v1/actions/{id}`
   (any field change). **Expect**: `409 action.read_only` (BR-023, ERR-11) — proves the
   born-Completed read-only guard fires without any elapsed time.

## Scenario 3 — Archive / Unarchive continuity (US6)

1. `POST /api/v1/actions/{id}/archive` on the Active Action from Scenario 1. **Expect**: `200`,
   `status: "archived"`, `action.archived` audit event.
2. `GET /api/v1/actions/{id}`. **Expect**: the underlying date-computed shape is unchanged
   (still what would be "active" if unarchived) — Archived is presentation-only (BR-009).
3. `POST /api/v1/actions/{id}/unarchive`. **Expect**: `200`, `status` recomputed from dates
   (back to `"active"`), `action.unarchived` event.

## Scenario 4 — Target deactivation and the last-remaining-Target guard (US7 + R-17)

1. On an Action with exactly one Target, `PATCH .../targets/{targetId}` `{ "active": false }`.
   **Expect**: `200`, `deactivation_source: "manual"`.
2. `DELETE .../targets/{targetId}`. **Expect**: `409 action.requires_target` — the last
   remaining Target (any state) cannot be deleted (R-17), even though it is deactivated.
3. Add a second Target, retry deactivate+delete on the first. **Expect**: `200`, Target removed,
   the KPI freed for reuse in a new Target on the same Action.

## Scenario 5 — Settings guard (US9)

1. Note the largest `upper_threshold` saved across the tenant's Targets (e.g. `6.0` from
   Scenario 1).
2. `PUT /api/v1/settings/actions` `{ "max_upper_threshold": 5 }`. **Expect**: `400
   settings.x_below_saved_upper`, message names the largest U (6).
3. `PUT /api/v1/settings/actions` `{ "max_upper_threshold": 30 }`. **Expect**: `200`,
   `settings.X_changed` audit event with `{ old: 20, new: 30 }`.

## Scenario 6 — Permission boundary (PERM-01)

1. As a P-06 (Executive/Viewer) session, `GET /api/v1/actions` and `GET /api/v1/actions/{id}`
   **succeed** (read access, PERM-01).
2. As the same session, `POST /api/v1/actions` or `PATCH .../targets/{id}`. **Expect**: `403`
   (ERR-3) even if the request is well-formed — proves server-side enforcement independent of
   the client hiding the write controls.

## E2E (browser) validation

Run the Playwright lane once the frontend pages exist:

```powershell
dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~ActionManagement"
```

Requires the stack up (Postgres + `Nabadat.TenantAdmin` host + `npm run dev`) and
`E2E_BASE_URL` set per the `e2e-testing` skill. Expect all `[TestMethod]`s enumerated in each
user story's "E2E Test Coverage" block in `spec.md` to pass, with a screenshot + trace attached
per test.

## Known-degraded behaviour until C-01 ships

Every scenario above involving a **live or historical KPI score** (baseline capture, current
score display, outcome evaluation against a real score) runs against `NullKpiScoreReader` until
M-06's score-computation engine ships (coordination-log.md C-01). Expect `null`/"no score"
states rather than real numbers in that window — this is the documented, intentional stub
behaviour, not a defect.
