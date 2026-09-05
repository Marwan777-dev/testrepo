// M-16 Customer Journey Mapping API client — thin endpoint functions over `callJson`.
//
// Covers every journey, stage, and touchpoint endpoint from
// specs/002-customer-journey-mapping/contracts/journeys-api.md (US-1 surface: T028 Journeys,
// T029 Stages, T030 Touchpoints controllers). Wire types live in ./dto, the transport helper
// in ./http, the error type in ./journeys-api-error. Those are re-exported here so callers can
// import everything journeys-API from "@/features/journeys/api".
//
// Wire note: `status` and `importance` arrive as plain strings (the controllers serialize the
// value objects to their string names), so no enum-integer normalization is required. The
// backend's API-05 envelope is `{ error: { code, message } }`; `JourneysApiError` exposes the
// `status` + `code` callers branch on (e.g. 409 `journey.name_conflict`, 403
// `journey.archived_immutable`).

import { callJson } from "./http"
import type {
  AddStageData,
  AddStageResponse,
  AddTouchpointData,
  AddTouchpointResponse,
  ChangePersonaStatusData,
  ChangeStatusData,
  CreateJourneyData,
  CreateJourneyResponse,
  CreatePersonaData,
  CreatePersonaResponse,
  DetectionConfig,
  JourneyDetail,
  JourneyListResponse,
  JourneyVersionListResponse,
  JourneyVersionSnapshot,
  KpiType,
  KpiTypesResponse,
  ListJourneysParams,
  ListPersonasParams,
  ListVersionsParams,
  PersonaListResponse,
  PersonaStatusChangeResponse,
  PublishVersionResponse,
  ReorderStagesData,
  ReorderStagesResponse,
  SaveDetectionData,
  SaveDetectionResponse,
  SaveKpiBindingsData,
  SaveKpiBindingsResponse,
  SaveScoringData,
  SaveScoringResponse,
  ScoringConfig,
  StageListResponse,
  StatusChangeResponse,
  UpdateJourneyData,
  UpdateJourneyResponse,
  UpdateStageData,
  UpdateStageResponse,
  UpdateTouchpointData,
  UpdateTouchpointResponse,
  UpdatedAtResponse,
} from "./dto"

export type * from "./dto"
export { JourneysApiError } from "./journeys-api-error"

// ── Journeys ────────────────────────────────────────────────────────────────

/** Lists tenant journeys (cursor-paginated) with an optional status filter. */
export function listJourneys(params: ListJourneysParams = {}): Promise<JourneyListResponse> {
  const query = new URLSearchParams()
  if (params.status) query.set("status", params.status)
  if (params.pageSize != null) query.set("page_size", String(params.pageSize))
  if (params.pageToken) query.set("page_token", params.pageToken)
  const qs = query.toString()
  return callJson<JourneyListResponse>(`/journeys${qs ? `?${qs}` : ""}`)
}

/** Creates a new journey (status `Draft`). 409 `journey.name_conflict` on a duplicate name. */
export function createJourney(data: CreateJourneyData): Promise<CreateJourneyResponse> {
  return callJson<CreateJourneyResponse>("/journeys", { method: "POST", body: data })
}

/** Returns the full journey tree (journey → stages → touchpoints → KPI bindings). */
export function getJourney(journeyId: string): Promise<JourneyDetail> {
  return callJson<JourneyDetail>(`/journeys/${journeyId}`)
}

/** Updates journey metadata. 403 `journey.archived_immutable` when the journey is Archived. */
export function updateJourney(
  journeyId: string,
  data: UpdateJourneyData,
): Promise<UpdateJourneyResponse> {
  return callJson<UpdateJourneyResponse>(`/journeys/${journeyId}`, { method: "PUT", body: data })
}

/** Transitions the journey lifecycle status (P-01 only). */
export function changeJourneyStatus(
  journeyId: string,
  data: ChangeStatusData,
): Promise<StatusChangeResponse> {
  return callJson<StatusChangeResponse>(`/journeys/${journeyId}/status`, {
    method: "PATCH",
    body: data,
  })
}

/** Returns the journey's last-update timestamp — polled by the concurrent-edit hook. */
export function getJourneyUpdatedAt(journeyId: string): Promise<UpdatedAtResponse> {
  return callJson<UpdatedAtResponse>(`/journeys/${journeyId}/updated-at`)
}

// ── Stages ──────────────────────────────────────────────────────────────────

/** Appends a stage to a journey (returns the assigned `sequenceNumber`). */
export function addStage(journeyId: string, data: AddStageData): Promise<AddStageResponse> {
  return callJson<AddStageResponse>(`/journeys/${journeyId}/stages`, { method: "POST", body: data })
}

/** Lists a journey's stages ordered by `sequenceNumber`, each with its touchpoint count. */
export function listStages(journeyId: string): Promise<StageListResponse> {
  return callJson<StageListResponse>(`/journeys/${journeyId}/stages`)
}

/** Updates stage metadata. */
export function updateStage(
  journeyId: string,
  stageId: string,
  data: UpdateStageData,
): Promise<UpdateStageResponse> {
  return callJson<UpdateStageResponse>(`/journeys/${journeyId}/stages/${stageId}`, {
    method: "PUT",
    body: data,
  })
}

/** Deletes a stage. 409 `journey.stage_has_touchpoints` when the stage still owns touchpoints. */
export function deleteStage(journeyId: string, stageId: string): Promise<void> {
  return callJson<void>(`/journeys/${journeyId}/stages/${stageId}`, { method: "DELETE" })
}

/** Replaces the journey's full stage ordering with the supplied complete sequence of IDs. */
export function reorderStages(
  journeyId: string,
  data: ReorderStagesData,
): Promise<ReorderStagesResponse> {
  return callJson<ReorderStagesResponse>(`/journeys/${journeyId}/stages/reorder`, {
    method: "PUT",
    body: data,
  })
}

// ── Touchpoints ───────────────────────────────────────────────────────────────

/** Adds a touchpoint to a stage. 422 `journey.touchpoint_limit_reached` on tenant limit. */
export function addTouchpoint(
  stageId: string,
  data: AddTouchpointData,
): Promise<AddTouchpointResponse> {
  return callJson<AddTouchpointResponse>(`/stages/${stageId}/touchpoints`, {
    method: "POST",
    body: data,
  })
}

/** Updates touchpoint metadata. */
export function updateTouchpoint(
  touchpointId: string,
  data: UpdateTouchpointData,
): Promise<UpdateTouchpointResponse> {
  return callJson<UpdateTouchpointResponse>(`/touchpoints/${touchpointId}`, {
    method: "PUT",
    body: data,
  })
}

/** Deletes a touchpoint and its KPI bindings. */
export function deleteTouchpoint(touchpointId: string): Promise<void> {
  return callJson<void>(`/touchpoints/${touchpointId}`, { method: "DELETE" })
}

// ── KPI types ─────────────────────────────────────────────────────────────────

/**
 * Lists the tenant's KPI types — the six platform-standard built-ins plus any tenant-defined
 * ones — flattened into a single list with `isPlatformStandard` set on each. Drives the KPI
 * weight editor's type picker. (`GET /api/v1/kpi-types`.)
 */
export async function getKpiTypes(): Promise<KpiType[]> {
  const res = await callJson<KpiTypesResponse>("/kpi-types")
  return [
    ...res.platformStandardTypes.map((kt) => ({ ...kt, isPlatformStandard: true })),
    ...res.tenantDefinedTypes.map((kt) => ({ ...kt, isPlatformStandard: false })),
  ]
}

// ── Touchpoint KPI bindings ─────────────────────────────────────────────────────

/**
 * Full-replace save of a touchpoint's KPI bindings (`PUT /api/v1/touchpoints/{id}/kpis`). An
 * empty `kpiBindings` set marks the touchpoint unmeasured. The 200 response carries the
 * non-blocking `npsWarning` flag. Failures: 422 `kpi.weight_sum_invalid` / `kpi.duplicate_type`
 * / `kpi.individual_weight_invalid` / `kpi.unknown_type`; 403 `journey.archived_immutable`.
 */
export function saveKpiBindings(
  touchpointId: string,
  data: SaveKpiBindingsData,
): Promise<SaveKpiBindingsResponse> {
  return callJson<SaveKpiBindingsResponse>(`/touchpoints/${touchpointId}/kpis`, {
    method: "PUT",
    body: data,
  })
}

// ── Scoring configuration ───────────────────────────────────────────────────────

/**
 * Returns the journey's strategic scoring configuration (`GET /api/v1/journeys/{id}/scoring`).
 * Throws `JourneysApiError` with status 404 + `journey.no_scoring_config` when none has been
 * saved — callers treat that as "use defaults" rather than an error.
 */
export function getScoring(journeyId: string): Promise<ScoringConfig> {
  return callJson<ScoringConfig>(`/journeys/${journeyId}/scoring`)
}

/**
 * Saves (upserts) the journey's scoring configuration (`PUT /api/v1/journeys/{id}/scoring`).
 * `modelType` is forwarded to M-06 unvalidated; `normalizationParams` is stored verbatim.
 * 403 `journey.archived_immutable` when the journey is Archived.
 */
export function saveScoring(
  journeyId: string,
  data: SaveScoringData,
): Promise<SaveScoringResponse> {
  return callJson<SaveScoringResponse>(`/journeys/${journeyId}/scoring`, {
    method: "PUT",
    body: data,
  })
}

// ── Detection configuration (US-4) ────────────────────────────────────────────────

/**
 * Returns the journey's pain/happy detection configuration including all overrides
 * (`GET /api/v1/journeys/{id}/detection`). Throws `JourneysApiError` with status 404 +
 * `journey.no_detection_config` when none has been saved — callers treat that as "use defaults"
 * rather than an error.
 */
export function getDetection(journeyId: string): Promise<DetectionConfig> {
  return callJson<DetectionConfig>(`/journeys/${journeyId}/detection`)
}

/**
 * Saves (full-replace upsert) the journey's detection configuration
 * (`PUT /api/v1/journeys/{id}/detection`). Journey-level `painThreshold`/`happyThreshold` are
 * required (both in `[0, 100]`, pain strictly below happy); override thresholds may be `null` to
 * inherit the parent level. Failures: 422 `detection.threshold_invalid` (pain ≥ happy) /
 * `detection.out_of_range` / `detection.unknown_stage` / `detection.unknown_touchpoint`;
 * 403 `journey.archived_immutable`.
 */
export function saveDetection(
  journeyId: string,
  data: SaveDetectionData,
): Promise<SaveDetectionResponse> {
  return callJson<SaveDetectionResponse>(`/journeys/${journeyId}/detection`, {
    method: "PUT",
    body: data,
  })
}

// ── Personas (US-3) ─────────────────────────────────────────────────────────────

/**
 * Lists the tenant's personas (cursor-paginated) with an optional status filter
 * (`GET /api/v1/personas`). The journey binding selector calls this with `status: "Active"`
 * to populate the list of bindable personas (FR-005).
 */
export function listPersonas(params: ListPersonasParams = {}): Promise<PersonaListResponse> {
  const query = new URLSearchParams()
  if (params.status) query.set("status", params.status)
  if (params.pageSize != null) query.set("page_size", String(params.pageSize))
  if (params.pageToken) query.set("page_token", params.pageToken)
  const qs = query.toString()
  return callJson<PersonaListResponse>(`/personas${qs ? `?${qs}` : ""}`)
}

/** Creates a new persona (status `Draft`). P-01 only (`POST /api/v1/personas`). */
export function createPersona(data: CreatePersonaData): Promise<CreatePersonaResponse> {
  return callJson<CreatePersonaResponse>("/personas", { method: "POST", body: data })
}

/**
 * Transitions a persona's lifecycle status (P-01 only,
 * `PATCH /api/v1/personas/{id}/status`). Failures: 422 `persona.invalid_transition` /
 * `persona.archived_terminal`; 409 `persona.archive_blocked_active_bindings` when archiving a
 * persona that still has active journey bindings.
 */
export function changePersonaStatus(
  personaId: string,
  data: ChangePersonaStatusData,
): Promise<PersonaStatusChangeResponse> {
  return callJson<PersonaStatusChangeResponse>(`/personas/${personaId}/status`, {
    method: "PATCH",
    body: data,
  })
}

// ── Journey versions (US-3) ─────────────────────────────────────────────────────

/**
 * Publishes the current journey configuration as the next immutable version snapshot
 * (`POST /api/v1/journeys/{id}/publish`). P-01 only. Failures: 422 `journey.no_stages` (empty
 * journey), 403 `journey.archived_immutable`.
 */
export function publishJourneyVersion(journeyId: string): Promise<PublishVersionResponse> {
  return callJson<PublishVersionResponse>(`/journeys/${journeyId}/publish`, {
    method: "POST",
    body: {},
  })
}

/** Lists a journey's published versions, newest first (`GET /api/v1/journeys/{id}/versions`). */
export function listJourneyVersions(
  journeyId: string,
  params: ListVersionsParams = {},
): Promise<JourneyVersionListResponse> {
  const query = new URLSearchParams()
  if (params.pageSize != null) query.set("page_size", String(params.pageSize))
  if (params.pageToken) query.set("page_token", params.pageToken)
  const qs = query.toString()
  return callJson<JourneyVersionListResponse>(`/journeys/${journeyId}/versions${qs ? `?${qs}` : ""}`)
}

/**
 * Returns the full frozen snapshot for a published version
 * (`GET /api/v1/journeys/{id}/versions/{versionNumber}`) — the journey tree exactly as captured at
 * publish time, marked `isSnapshot: true` with `snapshotVersion`. 404 `journey.version_not_found`
 * when the version doesn't exist.
 */
export function getJourneyVersion(
  journeyId: string,
  versionNumber: number,
): Promise<JourneyVersionSnapshot> {
  return callJson<JourneyVersionSnapshot>(`/journeys/${journeyId}/versions/${versionNumber}`)
}
