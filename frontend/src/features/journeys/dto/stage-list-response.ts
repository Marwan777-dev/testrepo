import type { StageSummary } from "./stage-summary"

/** Response of `GET /api/v1/journeys/{id}/stages`. Not cursor-paginated — all stages returned. */
export interface StageListResponse {
  stages: StageSummary[]
}
