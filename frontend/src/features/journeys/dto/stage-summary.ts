/** A stage row as returned by `GET /api/v1/journeys/{id}/stages`, ordered by `sequenceNumber`. */
export interface StageSummary {
  stageId: string
  sequenceNumber: number
  name: string
  touchpointCount: number
}
