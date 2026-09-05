// Barrel of M-16 journey/stage/touchpoint wire types. One interface or union per file
// (project `feedback_one_type_per_file` convention); re-exported here so callers can import
// everything journeys-API from "@/features/journeys/api".

export type { ApiErrorEnvelope } from "./api-error-envelope"

// Enums / unions
export type { JourneyStatus } from "./journey-status"
export type { TouchpointImportance } from "./touchpoint-importance"

// Journey — list
export type { ListJourneysParams } from "./list-journeys-params"
export type { JourneySummary } from "./journey-summary"
export type { JourneyListResponse } from "./journey-list-response"

// Journey — create / update / status / detail
export type { CreateJourneyData } from "./create-journey-data"
export type { CreateJourneyResponse } from "./create-journey-response"
export type { UpdateJourneyData } from "./update-journey-data"
export type { UpdateJourneyResponse } from "./update-journey-response"
export type { ChangeStatusData } from "./change-status-data"
export type { StatusChangeResponse } from "./status-change-response"
export type { UpdatedAtResponse } from "./updated-at-response"
export type { JourneyDetail } from "./journey-detail"
export type { StageDetail } from "./stage-detail"
export type { TouchpointDetail } from "./touchpoint-detail"
export type { KpiBinding } from "./kpi-binding"
export type { PersonaBinding } from "./persona-binding"

// Stages
export type { AddStageData } from "./add-stage-data"
export type { AddStageResponse } from "./add-stage-response"
export type { StageSummary } from "./stage-summary"
export type { StageListResponse } from "./stage-list-response"
export type { UpdateStageData } from "./update-stage-data"
export type { UpdateStageResponse } from "./update-stage-response"
export type { ReorderStagesData } from "./reorder-stages-data"
export type { ReorderStagesResponse } from "./reorder-stages-response"

// Touchpoints
export type { AddTouchpointData } from "./add-touchpoint-data"
export type { AddTouchpointResponse } from "./add-touchpoint-response"
export type { UpdateTouchpointData } from "./update-touchpoint-data"
export type { UpdateTouchpointResponse } from "./update-touchpoint-response"

// KPI types & touchpoint KPI bindings (US-2)
export type { ScoringDirection } from "./scoring-direction"
export type { KpiType } from "./kpi-type"
export type { KpiTypesResponse } from "./kpi-types-response"
export type { SaveKpiBindingsData } from "./save-kpi-bindings-data"
export type { SavedKpiBinding } from "./saved-kpi-binding"
export type { SaveKpiBindingsResponse } from "./save-kpi-bindings-response"

// Scoring configuration (US-2)
export type { ScoringModelType } from "./scoring-model-type"
export type { StageWeightMode } from "./stage-weight-mode"
export type { ScoringConfig } from "./scoring-config"
export type { SaveScoringData } from "./save-scoring-data"
export type { SaveScoringResponse } from "./save-scoring-response"

// Detection configuration (US-4)
export type { DetectionStageOverride } from "./detection-stage-override"
export type { DetectionTouchpointOverride } from "./detection-touchpoint-override"
export type { DetectionConfig } from "./detection-config"
export type { SaveDetectionData } from "./save-detection-data"
export type { SaveDetectionResponse } from "./save-detection-response"

// Personas (US-3)
export type { PersonaStatus } from "./persona-status"
export type { ListPersonasParams } from "./list-personas-params"
export type { PersonaSummary } from "./persona-summary"
export type { PersonaListResponse } from "./persona-list-response"
export type { CreatePersonaData } from "./create-persona-data"
export type { CreatePersonaResponse } from "./create-persona-response"
export type { ChangePersonaStatusData } from "./change-persona-status-data"
export type { PersonaStatusChangeResponse } from "./persona-status-change-response"

// Journey versioning (US-3)
export type { JourneyVersionSummary } from "./journey-version-summary"
export type { JourneyVersionListResponse } from "./journey-version-list-response"
export type { ListVersionsParams } from "./list-versions-params"
export type { PublishVersionResponse } from "./publish-version-response"
export type {
  JourneyVersionSnapshot,
  SnapshotStage,
  SnapshotTouchpoint,
  SnapshotKpiBinding,
  SnapshotScoringConfig,
  SnapshotDetectionConfig,
} from "./journey-version-snapshot"
