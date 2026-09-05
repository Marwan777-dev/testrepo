/**
 * Request body for `createJourney` (`POST /api/v1/journeys`). The journey is created with
 * status `Draft`. Persona binding (`personaIds`) is added in US-3 and is intentionally absent
 * here — the US-1 create dialog collects name/description/journeyType only.
 */
export interface CreateJourneyData {
  /** 1–255 characters; unique per tenant, case-insensitive, excluding Archived journeys. */
  name: string
  description?: string | null
  journeyType: string
}
