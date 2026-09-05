// Wire types for the frozen journey snapshot returned by
// `GET /api/v1/journeys/{id}/versions/{versionNumber}`. The payload is the serialized
// `snapshot_payload` captured at publish time (never recomputed), shaped by the backend
// `JourneySnapshotSerializer` — note it keys the journey type as `type` (not `journeyType`) and KPI
// bindings as `type` (not `kpiType`), and embeds the scoring + detection config inline. The
// controller grafts the two read-only markers `isSnapshot: true` and `snapshotVersion` on read.
//
// These types are dedicated to the snapshot contract (distinct from the live journey-detail tree),
// so they live together here rather than reusing `JourneyDetail`/`KpiBinding` whose field names differ.

/** A KPI binding inside a snapshot touchpoint (keyed `type`, unlike the live `KpiBinding.kpiType`). */
export interface SnapshotKpiBinding {
  type: string
  weight: number
  isPlatformStandard: boolean
}

export interface SnapshotTouchpoint {
  touchpointId: string
  name: string
  description?: string | null
  channels?: string[] | null
  importance?: string | null
  isMot?: boolean
  isMandatory?: boolean
  kpiBindings: SnapshotKpiBinding[]
}

export interface SnapshotStage {
  stageId: string
  sequenceNumber: number
  name: string
  description?: string | null
  customerGoal?: string | null
  expectedEmotion?: string | null
  durationHint?: string | null
  touchpoints: SnapshotTouchpoint[]
}

/**
 * Tenant-level strategic scoring parameters captured at publish (SRS §4.2.9 / §11.7, Q11 —
 * per-tenant, not per-journey). β is derived (1 − α).
 */
export interface SnapshotScoringConfig {
  alpha?: number | null
  beta?: number | null
  motMultiplier?: number | null
  nFloor?: number | null
  flagPercentile?: number | null
  rollingWindowDays?: number | null
}

export interface SnapshotDetectionConfig {
  painThreshold?: number | null
  happyThreshold?: number | null
}

/**
 * The full read-only journey snapshot for a published version. Shape mirrors the live journey tree
 * but is marked `isSnapshot: true` + `snapshotVersion`, uses `type` for the journey type, and carries
 * the scoring/detection config inline.
 */
export interface JourneyVersionSnapshot {
  journeyId: string
  name: string
  description?: string | null
  /** Journey type (the snapshot serializer keys this `type`, not `journeyType`). */
  type?: string | null
  status?: string | null
  scoringConfig?: SnapshotScoringConfig | null
  detectionConfig?: SnapshotDetectionConfig | null
  stages: SnapshotStage[]
  /** Always `true` on a snapshot read — the read-only marker grafted by the controller. */
  isSnapshot: boolean
  /** The version number this snapshot represents. */
  snapshotVersion: number
}
