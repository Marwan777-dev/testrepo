# Research: Customer Journey Mapping Module (M-16)

**Feature**: 002-customer-journey-mapping
**Date**: 2026-06-08
**Status**: Complete — all unknowns resolved

---

## 1. JourneyVersion Snapshot Storage Strategy

**Decision**: Store the version snapshot as a single `jsonb` blob (`snapshot_payload`) in the `journey_versions` table. No separate normalization of snapshot contents into child tables.

**Rationale**:
- A published version is an immutable historical record. Storing it as a `jsonb` blob guarantees it is fully self-contained and immune to future schema migrations on the live configuration tables.
- Historical retrieval is a single row read: `SELECT snapshot_payload FROM journey_versions WHERE journey_id = $1 AND version_number = $2`.
- The blob is written once (at publish time) and never updated. Read performance is excellent because no joins are required.
- The blob size is bounded: a journey with 20 stages × 30 touchpoints × 6 KPI bindings yields roughly 250–400 KB of JSON (within PostgreSQL `jsonb` practical limits and well under the 1 GB `jsonb` maximum).

**Snapshot contents** (written by `JourneySnapshotSerializer.Serialize`):
```json
{
  "journeyId": "uuid",
  "name": "string",
  "description": "string",
  "type": "string",
  "scoringConfig": { ... },
  "detectionConfig": { "painThreshold": 40, "happyThreshold": 75, "stageOverrides": [...], "touchpointOverrides": [...] },
  "stages": [
    {
      "stageId": "uuid",
      "sequenceNumber": 1,
      "name": "string",
      "touchpoints": [
        {
          "touchpointId": "uuid",
          "name": "string",
          "channels": ["IVR", "Web"],
          "importance": "High",
          "isMoT": true,
          "isMandatory": false,
          "kpiBindings": [
            { "type": "NPS", "weight": 60 },
            { "type": "CSAT", "weight": 40 }
          ]
        }
      ]
    }
  ]
}
```

**Alternatives considered**:
- **Separate snapshot tables** (e.g., `journey_version_stages`, `journey_version_touchpoints`): rejected — doubles the table count, complicates migration strategy, and provides no query benefit since snapshots are always retrieved whole.
- **File storage**: rejected — PostgreSQL `jsonb` is well-suited for structured document storage of this size; file storage adds operational complexity.

---

## 2. Concurrent Edit Notification (FR-018)

**Decision**: Client-side polling of a lightweight `GET /api/v1/journeys/{id}/updated-at` endpoint. The endpoint returns `{updatedAt, updatedByUserId, updatedByName}`. The `useJourneyUpdated` React hook polls every 15 seconds while the journey builder or any configuration page is open, comparing the returned `updatedAt` with the timestamp captured at page load. On mismatch, a non-blocking Sonner toast is displayed.

**Rationale**:
- WebSockets add infrastructure complexity (connection lifecycle, horizontal-scaling sticky sessions) that is disproportionate to the low-frequency nature of concurrent journey edits.
- Server-Sent Events (SSE) are simpler than WebSockets but still require a persistent server connection per open tab. At 50 active journeys per tenant, concurrent edit collisions are rare enough that polling every 15 s is adequate and cost-free at idle.
- Polling at 15 s means the notification appears at most 15 s after the remote edit — acceptable for the informational (non-blocking) use case defined in FR-018.

**Endpoint contract**:
- `GET /api/v1/journeys/{id}/updated-at` returns `200` with `{updatedAt, updatedByUserId, updatedByName}`.
- Required permission: `journey.read` (same as `GET /api/v1/journeys/{id}`).
- Response is < 100 bytes; no ETag required.
- Hook is torn down when the page unmounts (no leak).

**Alternatives considered**: WebSocket — rejected (infrastructure overhead); SSE — rejected (persistent connection not justified); ETag / `If-None-Match` polling — viable but semantically conflates HTTP caching with application-level change detection; lightweight timestamp polling is more explicit.

---

## 3. KPI Weight Sum = 100% Enforcement

**Decision**: Application-layer enforcement in `KpiWeightValidator.Validate(IReadOnlyList<KpiBindingRequest> bindings)`. The validator checks:
1. At least one binding exists.
2. All individual weights are `> 0` and `<= 100`.
3. The sum of all weights equals exactly `100`.

The validator is called by `KpiBindingService.SaveKpiBindingsAsync(touchpointId, bindings)` before any DB write. The save operation is a full replace (delete all existing bindings for the touchpoint + insert all new bindings) inside one transaction. Individual rows carry a PostgreSQL CHECK constraint `(weight > 0 AND weight <= 100)` as a backstop.

**Why not a DB-level constraint**: A sum-across-rows constraint requires a deferrable constraint or a trigger. Deferrable constraints are complex under PgBouncer transaction-pooling mode (session-level semantics required). Triggers make test setup brittle. Application-layer enforcement with a clean error message is simpler and sufficient.

**Error response** (422 Unprocessable Entity):
```json
{
  "error": {
    "code": "kpi.weight_sum_invalid",
    "message": "KPI weights must sum to 100%. Current sum: 85%",
    "correlation_id": "...",
    "tenant_id": "..."
  }
}
```

---

## 4. Detection Threshold Override Resolution

**Decision**: Three-level specificity chain implemented in `DetectionOverrideResolver.GetEffectiveThresholds(ScopeType entityType, Guid entityId, Guid journeyId)`:

```
1. Touchpoint-level override  →  scope_type='touchpoint', scope_id=touchpointId
2. Stage-level override       →  scope_type='stage', scope_id=stageId
3. Journey-level default      →  detection_configs.pain_threshold / happy_threshold
```

The resolver issues a single query:
```sql
SELECT scope_type, scope_id, pain_threshold, happy_threshold
FROM detection_threshold_overrides
WHERE detection_config_id = (SELECT detection_config_id FROM detection_configs WHERE journey_id = $1)
  AND scope_id = ANY($2)  -- $2 = [touchpointId, parentStageId]
```

Then applies: touchpoint override wins over stage override wins over journey default.

**Rationale**: The spec states "most specific override wins." Touchpoint is more specific than stage, which is more specific than journey. A single query with `ANY` lookup avoids N+1 when resolving multiple touchpoints in a detection pass.

---

## 5. IJourneyScoreProvider — M-06 Delegation Pattern

**Decision**: `JourneyScoreProviderService.GetScoresAsync(Guid journeyId)` executes synchronously:
1. Load journey config from `IJourneyConfigReader` (M-16's own implementation — direct DB read).
2. Call M-06's published interface: `IM06ScoringService.ComputeJourneyScore(JourneyConfigDto config)`.
3. Upsert result into `journey_scores` (INSERT ... ON CONFLICT UPDATE).
4. Call `M17EventPublisher.Publish("journey.score.updated", payload)` in the same transaction.
5. Return the score result to the caller.

Steps 3 and 4 are in the same `NpgsqlTransaction`. If the M-06 call fails (throws), the transaction is never opened; no partial state is written. If the transaction commit fails, the caller receives an error and retries.

**No M-04 subscription**: M-16 does NOT subscribe to `survey.response.submitted`. Score freshness is the caller's responsibility. The event catalogue in the constitution confirms `survey.response.submitted` has M-16 as a downstream — this is a future Phase 2 concern, not implemented in Phase 1. Phase 1 provides on-demand scoring only.

---

## 6. IJourneyConfigReader — In-Process Read Pattern

**Decision**: `JourneyConfigReaderService` (M-16's implementation of `IJourneyConfigReader`) reads directly from M-16's own PostgreSQL tables (same connection pool, same tenant schema) and returns a `JourneyConfigDto`. No caching — the DTO is constructed fresh on each call.

**Why no cache**: The constitution prohibits in-memory analytics caching (AD-03). Per-request caching (request-scoped DI) is acceptable and handled automatically by the DI container lifetime (`Scoped`). Cross-request caching would require Redis (forbidden) or an in-memory dictionary (forbidden per AD-03 for analytics data).

**M-06 call pattern**: M-06 calls `IJourneyConfigReader.GetJourneyConfig(journeyId)` synchronously before computing scores. The interface is registered in the DI container as `Scoped`; M-06 receives it through constructor injection without referencing M-16 concrete types.

---

## 7. Journey Name Case-Insensitive Uniqueness

**Decision**: PostgreSQL partial functional unique index on `journeys`:
```sql
CREATE UNIQUE INDEX idx_journeys_name_ci ON journeys (LOWER(name))
WHERE status <> 'Archived';
```

This enforces uniqueness for all non-Archived journeys (case-insensitive). Archived journeys are excluded by the `WHERE` clause, so their names become available for reuse.

**Application layer**: `JourneyNameUniquenessValidator.IsAvailableAsync(name, excludeJourneyId?)` does a `SELECT EXISTS(...)` check before writing, returning a clear validation error (not a raw PostgreSQL unique violation). This avoids presenting a generic DB error to the API caller.

---

## 8. Report Contract Structure

**Decision**: `ReportContractService.BuildContractAsync(journeyId)` constructs a `jsonb` payload stored in `report_contracts.contract_payload`. The structure exposes the journey's measurement dimensions to M-07:

```json
{
  "journeyId": "uuid",
  "journeyName": "string",
  "generatedAt": "ISO8601",
  "stages": [
    {
      "stageId": "uuid",
      "name": "string",
      "sequenceNumber": 1,
      "touchpoints": [
        {
          "touchpointId": "uuid",
          "name": "string",
          "isMoT": true,
          "kpiTypes": ["NPS", "CSAT"],
          "isMeasured": true
        }
      ]
    }
  ],
  "scoreDimensions": ["journey_score", "stage_score", "touchpoint_score", "kpi_score"],
  "detectionConfig": {
    "painThreshold": 40,
    "happyThreshold": 75
  }
}
```

The contract is rebuilt and stored on every write to stages, touchpoints, KPI bindings, or detection config (as a transactional side effect). `IReportContractReader.GetReportContract(journeyId)` returns the stored payload deserialized to `ReportContractDto`.

---

## 9. Per-Tenant Limit Reading from M-11

**Decision**: `JourneyLimitEnforcer` calls `IM11TenantService.GetJourneyLimits()` at request time. The interface returns `JourneyLimitsDto { MaxStagesPerJourney, MaxTouchpointsPerStage }`. The call is made once per request (Scoped DI), not cached across requests.

**Fallback**: If M-11 is unavailable (network or circuit-breaker open), `GetJourneyLimits()` throws. `JourneyLimitEnforcer` catches this, logs a warning, and uses the platform defaults (20 / 30). The journey operation proceeds. Rationale: a limit-check service failure should not block journey creation; misconfigured limits are an operational concern, not a hard blocker.

**Error messages** (from the spec):
- Stage limit: "You've reached the maximum of {maxStages} stages. Consider splitting this into multiple journeys."
- Touchpoint limit: "You've reached the maximum of {maxTouchpoints} touchpoints per stage."

---

## 10. Frontend Page Architecture

**Decision**: Feature module under `frontend/src/features/journeys/`. Six pages, each a full-page route:

| Page | Route | Description |
|------|-------|-------------|
| `JourneyListPage` | `/journeys` | Lists all journeys with status filter, search, create button |
| `JourneyBuilderPage` | `/journeys/:id/builder` | Drag-and-drop stage/touchpoint builder with inline edit |
| `KpiScoringPage` | `/journeys/:id/kpi-scoring` | KPI binding panel per touchpoint + scoring model config |
| `PersonaManagementPage` | `/journeys/personas` | Persona list, create, and lifecycle management |
| `VersionHistoryPage` | `/journeys/:id/versions` | Version list with read-only snapshot viewer |
| `DetectionRulesPage` | `/journeys/:id/detection` | Journey-level + stage-level threshold config |

Routes are registered in `app-sidebar.tsx` under the `nav.platform` group (CX configuration domain). `ROLE_NAV_KEYS` restricts `journeys.personas` to P-01 and `journeys.*` to P-01/P-02; P-03..P-08 have no journey access.

**Bilingual fields**: All user-visible text fields in persona entities carry `_ar`/`_en` pairs. The journey and stage `name`/`description` fields are stored as single multilingual strings (Arabic or English depending on tenant locale, not duplicated pairs — persona is the exception because it has a formal bilingual label requirement per FR-005).

---

## 11. M-17 Event Types for M-16

Events published by M-16 (all in the constitution's Event Catalogue):

| Trigger | Event type |
|---------|-----------|
| Journey status transition | `journey.status.changed` (not in catalogue → needs constitution amendment or use `settings.changed` as proxy — see note) |
| Persona status transition | `persona.status.changed` (same note) |
| Journey version published | `journey.version.published` (same note) |
| Score computed | `journey.score.updated` ✅ (in catalogue) |

**Note**: The constitution Event Catalogue (Section 4) lists only `journey.score.updated` for M-16. The spec (FR-015, FR-016) requires events for status transitions, persona changes, and version publishing. These event types must be added to the catalogue via a **constitution amendment** before implementation. The amendment is a foundational task in Phase A. Until ratified, these events are published with their intended names; the amendment is non-breaking (adding new event types to M-17's `event_log`).
