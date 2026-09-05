# Quickstart: M-13 Integration Hub

Validation guide for proving the feature works end-to-end. Not implementation code — see
`data-model.md` for entity shapes and `contracts/` for endpoint/interface details.

## Prerequisites

- Backend: `Nabadat.IntegrationHub` registered in `Nabadat.TenantAdmin` (composition root calls
  `AddIntegrationHubModule(...)`), `IntegrationHub_Baseline.sql` applied to the test tenant
  schema, the 23 built-in parameters seeded enabled (BR-23), `ISurveyResolutionReader` /
  `ISurveyDispatchGateway` / `IResponseIngestionGateway` bound to their `Null*` stubs by default
  (research.md §4.3/4.4) unless M-02/M-04 have shipped by the time this runs.
- `Nabadat.SurveyBuilder` (M-01) available and referencing at least one Active survey, so
  `ISurveyRenderService.GetActiveSurveyDefinitionAsync` returns real data for SCN-03 tests
  (research.md §4.2 — this is a real integration, not a stub).
- Frontend: `npm run dev` in `frontend/`, `E2E_BASE_URL` pointed at the dev server for the
  Playwright lane.
- Signed-in sessions: a P-07 (Tenant IT Administrator) test user for integration/credential/log
  scenarios, and a P-01 (CX Manager) test user for channel/parameter/mapping scenarios.

## Scenario 1 — Define a channel, create an integration, send a real request (US1 + US3 + US4)

1. `POST /api/v1/integration-hub/service-channels` (as P-01):
   ```json
   { "nameEn": "Self-Service Kiosk", "nameAr": "كشك الخدمة الذاتية",
     "channelId": "SELF-SERVICE-KIOSK",
     "contract": [ { "parameterApiField": "mobile", "supported": true, "required": true } ] }
   ```
   **Expect**: `201`, `channel_id_locked: false`.
2. `POST /api/v1/integration-hub/integrations` (as P-07):
   ```json
   { "name": "Kiosk — Survey Dispatch", "serviceChannelId": "<id from step 1>",
     "scenario": "dispatch", "credential": { "mechanism": "api_key", "keyLabel": "Kiosk Key" } }
   ```
   **Expect**: `201`, a show-once plaintext API key in the response, `integration.created` +
   `credential.generated` audit events.
3. Send `POST /v1/survey-requests/SELF-SERVICE-KIOSK` with header `X-Api-Key: <key from step 2>`
   and body `{ "mobile": "+962770123456", "transaction_id": "TXN-001" }`.
   **Expect**: `202 ACCEPTED` + `request_id`. Since `ISurveyResolutionReader` is stubbed
   (`NullSurveyResolutionReader` by default), the actual dispatch hand-off is a no-op recorded
   for test assertion — confirm this is the expected behaviour before asserting further
   (research.md §4.3 — real dispatch requires M-02).
4. `GET /api/v1/integration-hub/request-logs?integration_id=<id>` (as P-07). **Expect**: the
   request from step 3 appears within 60 seconds, PII-masked mobile number
   (`+9627•••••312`).

## Scenario 2 — Missing required parameter is rejected atomically (US4)

1. Repeat Scenario 1 step 3 but omit `mobile`.
2. **Expect**: `400 E-1002`, message *"Required parameter 'mobile' is missing for service
   channel SELF-SERVICE-KIOSK."* Confirm nothing was forwarded downstream (the dispatch stub's
   call-count assertion stays at its prior value).

## Scenario 3 — Case-insensitive uniqueness across the board (Clarifications 2026-07-27)

1. Attempt `POST /api/v1/integration-hub/service-channels` with `channelId: "self-service-kiosk"`
   (lowercase). **Expect**: `409` (VR-F04, case-insensitive).
2. Attempt `POST /api/v1/integration-hub/integrations` with `name: "kiosk — survey dispatch"`.
   **Expect**: `409` (VR-F01, case-insensitive per this feature's convention).
3. Add a mapping with `sourceValue: "S001"`, then attempt a second with `sourceValue: "s001"` on
   the same parameter. **Expect**: `409` (VR-F08, case-insensitive, Clarifications 2026-07-27).

## Scenario 4 — Real M-10 integration: parameter definitions pushed to the data-scope system

1. Create a custom List parameter with `mappingSupport: true` (or confirm it's forced on, BR-27)
   and add 2-3 mappings.
2. **Expect**: an outbound `POST /api/v1/authorization/scope/parameters` call reaches
   `Nabadat.UserManagement`'s real `M13ParameterContractAdapter` (verify via M-10's own
   `data_scope_parameter_definitions` table, or a captured HTTP call in a lower environment) —
   this is a **real, working cross-module integration** today, not a stub (research.md §4.1).

## Scenario 5 — Capacity guardrail enforcement (VR-F13, Clarifications 2026-07-27)

1. Seed a tenant at exactly 200 integrations.
2. Attempt to create a 201st. **Expect**: `400 validation.capacity_exceeded`, inline console
   error naming the limit (200) — not an inbound-API result code (this is a console-only create
   action, not caller-facing traffic).

## Scenario 6 — Bulk mapping import, all-or-nothing (US7)

1. `POST /api/v1/integration-hub/parameters/{id}/mappings/import` with a file of 214 valid rows
   + 1 row with an empty `source_value`, `mode: merge`.
2. **Expect**: `400`/`422` with a row-level report naming the bad row; a follow-up `GET` on the
   mappings confirms **zero** rows were applied (all-or-nothing, VR-F09).
3. Fix the file, re-import. **Expect**: `200`, all 214 rows applied.

## Scenario 7 — Cross-persona permission enforcement (US9)

1. As a P-01 session, `GET /api/v1/integration-hub/integrations` → **200** (read-only view
   allowed, BR-24).
2. As the same P-01 session, `POST /api/v1/integration-hub/integrations` → **403**, audited.
3. As the same P-01 session, `GET /api/v1/integration-hub/request-logs` → **403** (logs are
   P-07-exclusive — no cross-persona read grant here, unlike every other screen).

## E2E (browser) validation

```powershell
dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~IntegrationHub"
```

Requires the stack up (Postgres + `Nabadat.TenantAdmin` host + `npm run dev`) and
`E2E_BASE_URL` set. Expect every `[TestMethod]` named in each user story's "E2E Test Coverage"
block in `spec.md` to pass, with a screenshot + trace attached per test.

## Known-degraded behaviour until C-01/C-02 ship

Every scenario involving **actual survey delivery** (SCN-01's dispatch, SCN-02's link
resolution, SCN-04's embed content beyond URL construction) or **actual response storage**
(SCN-05 durability) runs against `Null*` stubs until M-02/M-04 ship (coordination-log.md
C-01/C-02). Expect "resolution returned nothing" / "no-op recorded" outcomes in that window —
this is documented, intentional stub behaviour, not a defect. **SCN-03 (JSON render) is the
exception** — it already integrates for real against `Nabadat.SurveyBuilder`'s
`ISurveyRenderService` today (research.md §4.2), so its quickstart scenario should show real
survey-definition JSON, not a stub placeholder.
