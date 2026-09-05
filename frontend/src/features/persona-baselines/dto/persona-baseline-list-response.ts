import type { PersonaBaseline } from "./persona-baseline"

/** Response of `GET /api/v1/persona-baselines` — all baselines for the tenant. */
export interface PersonaBaselineListResponse {
  items: PersonaBaseline[]
}
