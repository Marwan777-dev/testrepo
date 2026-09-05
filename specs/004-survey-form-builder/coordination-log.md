# Cross-Module Coordination Log — Survey & Form Builder (M-01)

Tracks the cross-team/cross-module dependencies M-01 needs **other** modules to ship, and the
governance changes M-01 needs ratified. This is distinct from [TODO.md](../../TODO.md) (which
tracks M-01's *own* deferred stubs/gaps): entries here are actions owned by **another module's
team** or the **platform governance process**. Created by **T021**.

See also: [plan.md → Cross-module dependencies blocker table](./plan.md), [research.md §4](./research.md#4-cross-module-contracts),
[contracts/published-interface.md → Reverse dependencies](./contracts/published-interface.md).

Status values: `PENDING` (not started / owning module absent) → `IN PROGRESS` → `SHIPPED`
(port/impl exists and M-01 has swapped off its stub) → `RATIFIED` (governance items only).

---

## C-01 — `IResponsePurgeService` (M-04, **new port**) — owned by T021

- **Needed by**: M-01 US1 destructive Return-to-Draft (BR-1.6). Blocks that path **only**; the
  rest of US1 ships without it.
- **Port**: `Nabadat.ResponseCollection.Domain.Interfaces.IResponsePurgeService`
  ```csharp
  Task PurgeSurveyResponsesAsync(SurveyId surveyId, ActorId actorId, CorrelationId correlationId, CancellationToken ct);
  ```
  Semantics (research.md §4.5): hard-delete every response for the survey (live + M-07 post-expiry
  store) and invalidate every in-flight respondent session, then emit `survey.responses.purged`
  (see C-02). Called by M-01 **after** its own status→Draft transaction commits; on failure M-01
  compensates (reverts status) and surfaces 503.
- **Status**: **PENDING** — `Nabadat.ResponseCollection` (M-04) does not exist under `src/` yet;
  there is no M-04 owner to coordinate with in-repo. M-01 ships a narrow stub in the meantime
  (`DestructiveReturnToDraftService` returns 501 `survey.return_to_draft.purge_service_unavailable`
  when the port is unregistered) — tracked M-01-side by **[TODO-M01-001](../../TODO.md)**.
- **Resume**: when M-04 ships the port, register the impl in the host and remove the M-01 stub per
  TODO-M01-001's resume instructions.

## C-02 — `survey.responses.purged` event (M-04-sourced) — governance, see C-06

- **Needed by**: C-01's purge tail; downstream M-05/M-06/M-07 drop derived aggregates for the survey.
- **Payload**: `{ survey_id, purged_response_count, invalidated_session_count, actor_id, correlation_id }`.
- **Status**: **PENDING** — requires constitution AMENDMENT-012 §2 (filed by T022, see C-06) to be
  **ratified**, then M-04 to emit it. M-01-side audit emission that depends on ratification is
  tracked by **[TODO-M01-002](../../TODO.md)**.

## C-03 — `IChannelSurveyRulesReader` (M-02) — **not yet assigned to a task**

- **Needed by**: M-01 US1 `RulesCountProjection` (T071) — the Pause-with-active-rules confirmation
  (FR-1.10) reads the count of active channel rules referencing the survey.
- **Owner module**: `Nabadat.ChannelManagement` (M-02) — **does not exist under `src/` yet**.
- **Status**: **PENDING**. No owning module and no M-01 task creates/consumes it beyond T071's need.
  Flagged in T020/T021. Needs a task assignment (M-01 declares the port + stubs the count as 0, or
  waits for M-02).

## C-04 — `INotificationDispatcher` (M-09) — **not yet assigned to a task**

- **Needed by**: M-01 US2 Submit-for-Review reviewer broadcast (T116, Q7/FR-15.2).
- **Owner module**: `Nabadat.Notifications` (M-09) — **does not exist under `src/` yet**.
- **Status**: **PENDING**. Flagged in T020/T021; needs a task assignment (declare port + no-op stub,
  or wait for M-09).

## C-05 — `ITenantSettingsReader` + `ITenantDesignGuidelinesReader` (M-11) — **not yet assigned to a task**

- **Needed by**: M-01 US1 Appearance (T080/T050, F4 inherited-mode tokens) and tenant settings reads.
- **Owner module**: M-11 provisioning — **does not exist under `src/` yet** (the host
  `Nabadat.TenantAdmin` stands in for some tenant concerns but does not expose these readers).
- **Status**: **PENDING**. Tracked M-01-side by **[TODO-M01-006](../../TODO.md)** (which also records
  the reason M-01 must NOT reference the host for these — project-reference cycle). Resume: declare
  the ports in `Domain/Interfaces/`, reference the owning module (not the host), register the impl
  in the host.

## C-06 — Constitution AMENDMENT-012 (M-01 owned tables + 4 new events) — **FILED by T022**

- **Needed by**: BR-1.6 shipping to production, and T044/T102/T110/T124/T125 legally emitting
  `survey.created` / `survey.status.changed` / `survey.submitted_for_review`.
- **Status**: **FILED** (appended to `.specify/memory/constitution.md` as AMENDMENT-012 by T022) —
  **awaiting RATIFICATION** by the platform architect. Filing ≠ ratification; TODO-M01-002 stays
  blocked until ratified.
- **Resume**: platform architect reviews/ratifies AMENDMENT-012; on ratification, C-02 and
  TODO-M01-002 unblock.
