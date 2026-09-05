# Cross-Module Coordination Log — Integration Hub (M-13)

Tracks the cross-team/cross-module dependencies M-13 needs **other** modules to ship, and the
governance changes M-13 needs ratified. Distinct from a module-local TODO list: entries here are
actions owned by **another module's team** or the **platform governance process**. Mirrors the
pattern established by Feature 004 (M-01) and Feature 005 (M-15). Created by `/speckit-plan`.

See also: [research.md §4-7](./research.md), [plan.md → Constitution Check](./plan.md),
[contracts/published-interfaces.md](./contracts/published-interfaces.md).

Status values: `PENDING` (not started / owning module absent) → `IN PROGRESS` → `SHIPPED`
(port/impl exists and M-13 has swapped off its stub) → `RATIFIED` (governance items only).

---

## C-01 — M-02 (Channels & Distribution) — survey resolution + dispatch hand-off

- **Needed by**: all five scenarios (survey resolution, BR-19) and SCN-01's dispatch hand-off
  (CMC-01). Blocks only the *live* resolution/dispatch behavior — the console CRUD (channels,
  parameters, integrations, mappings, logs) and the entire request-validation pipeline up to the
  point of needing a resolved survey all function and are fully testable without M-02.
- **Ports**: `Nabadat.IntegrationHub.Domain.Interfaces.ISurveyResolutionReader` (channel +
  transaction params → resolved `SurveyId`, or "not resolved"), `IntegrationHub.Domain.Interfaces
  .ISurveyDispatchGateway` (hand off a resolved survey + transaction context for delivery).
- **Status**: **PENDING** — `Nabadat.ChannelManagement`-or-equivalent (M-02) does not exist under
  `src/` yet; confirmed independently by `Nabadat.SurveyBuilder`'s own
  `IChannelSurveyRulesReader` doc comment ("the concrete implementation is supplied by M-02,
  which does not exist under `src/` yet"). M-13 ships deterministic stubs
  (`NullSurveyResolutionReader`, `NullSurveyDispatchGateway`) returning an explicit
  "not-configured" state so every user story is fully buildable and testable today.
- **Resume**: when M-02 ships real resolution/dispatch capability, implement the real adapters
  in `Nabadat.IntegrationHub` (or wherever the host wires them) and remove the stub registration.

## C-02 — M-04 (Response Collection) — SCN-05 hand-off + public survey renderer

- **Needed by**: SCN-05's response-ingestion hand-off (CMC-03) and today's ratified guarantee
  that M-04 must save every forwarded payload unconditionally (Clarifications 2026-07-27,
  **SC-016**). Also owns the actual respondent-facing, unauthenticated, origin-checked public
  render surface that SCN-04's embed URL ultimately points at (research.md §4.5) — **M-13 does
  not build this page**, only the authenticated call that returns its URL.
- **Port**: `Nabadat.IntegrationHub.Domain.Interfaces.IResponseIngestionGateway`.
- **Status**: **PENDING** — no `Nabadat.ResponseCollection`-or-equivalent project exists;
  confirmed independently by `Nabadat.SurveyBuilder`'s `ISurveyRenderService` doc comment
  ("Consumed by... M-04 (Response Collection) at response-start time"). M-13 ships a
  deterministic stub for the ingestion port; the SC-016 guarantee is recorded as a contract
  requirement on M-04's future real implementation, not something M-13 can enforce today absent
  a real M-04 to verify durability against.
- **Resume**: when M-04 ships, wire the real ingestion adapter and confirm SC-016 end-to-end;
  separately confirm the public survey-renderer surface exists (owned by M-04 or a dedicated
  "Survey renderer" frontend per constitution Section 1) before SCN-04 can be considered
  functionally complete beyond URL construction.

## C-03 — M-13 owned-tables registry correction (constitution Section 3 + DB-04)

- **Needed by**: keeping `constitution.md` accurate once the real schema ships.
- **Status**: **PENDING** — Section 3 still lists the Phase-1 placeholder reservation
  (`api_keys`, `webhook_configs`, `connector_configs`, `integration_log`, AD-06/DB-06). No
  baseline migration for these ever shipped. This feature's baseline creates the real tables
  directly (research.md §3): `integrations`, `credentials`, `service_channels`, `parameters`,
  `channel_parameter_assignments`, `parameter_mappings`, `unmapped_value_occurrences`,
  `integration_request_logs`.
- **Resume**: file an amendment (mirroring AMENDMENT-011/012) once
  `IntegrationHub_Baseline.sql` merges, correcting Section 3's M-13 row **and** adding
  `integration_request_logs` to DB-04's monthly-partitioned high-volume table list.

## C-04 — SRS naming defect: "M-03 Survey & Forms" should read M-01

- **Needed by**: preventing a future engineer from building or referencing a phantom "M-03"
  dependency for survey definitions/rendering.
- **Status**: **PENDING** (documentation correction, not a code dependency). The source SRS's
  CMC-02 names the survey-definitions/rendering owner "M-03 Survey & Forms." Per the
  constitution's own Module Registry (Section 3), **M-03 is "Audience and Contact Management,"**
  an unrelated module. The real owner is **M-01 "Survey and Form Builder"**
  (`Nabadat.SurveyBuilder`), which already exists and publishes the exact interface M-13 needs
  (`ISurveyRenderService`, research.md §4.2).
- **Resume**: no code action needed (the constitution is already correct); flag for whoever owns
  the SRS's next revision to correct the label. This plan and its tasks build against the
  correct target (M-01 / `Nabadat.SurveyBuilder`) regardless of the SRS's label.

## C-05 — M-09 (Notifications) — no action needed for Phase 1

- **Needed by**: nothing in v1 — operational alerting on integration failures is explicitly
  postponed in full; Phase 1 only logs failures (CMC-05). M-13's only obligation is emitting its
  own audit events, which M-09 may subscribe to later via M-17.
- **Status**: **PENDING**, non-blocking. `Nabadat.Notifications` (M-09) does not exist under
  `src/` yet. `Nabadat.UserManagement` already ships the reusable stub *pattern*
  (`IM09NotificationService` / `UnavailableM09NotificationService`) for whenever a future phase
  needs it — noted here so it isn't reinvented.

## C-06 — M-14 / M-15 / M-16 forward contract (parameter references)

- **Needed by**: SRS §12.4/CMC-07 — these modules "may reference M-13 parameters," participating
  in the BR-10 impact-warning guard.
- **Status**: **PENDING**, non-blocking. M-13 ships `IParameterCatalogReader` as a forward-only
  published skeleton (research.md §4.7), mirroring M-15's `IActionOverlayReader` precedent — no
  real consumer exists yet, since M-14/M-15/M-16's actual data-scope needs are currently served
  through M-10 directly (§4.1), not M-13.
