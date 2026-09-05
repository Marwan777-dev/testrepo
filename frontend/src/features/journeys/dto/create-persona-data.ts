/**
 * Request body for `createPersona` (`POST /api/v1/personas`). The persona is created with status
 * `Draft`; it must be transitioned to `Active` before it can be bound to a journey (FR-005).
 */
export interface CreatePersonaData {
  /** 1–255 characters; required. */
  nameAr: string
  /** 1–255 characters; required. */
  nameEn: string
  descriptionAr?: string | null
  descriptionEn?: string | null
}
