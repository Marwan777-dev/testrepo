import type { PersonaStatus } from "./persona-status"

/** A persona row as returned by `GET /api/v1/personas`. */
export interface PersonaSummary {
  personaId: string
  nameAr: string
  nameEn: string
  status: PersonaStatus
  /** Number of journeys this persona is currently bound to — gates the archive guard (FR-005). */
  journeyBindingCount: number
  /** ISO-8601 UTC timestamp of the last update. */
  updatedAt: string
}
