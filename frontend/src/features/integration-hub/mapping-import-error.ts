import type { MappingImportRowError } from "./dto"

/**
 * Thrown when an Excel mapping import is rejected (US7, VR-F09).
 *
 * Separate from `IntegrationHubApiError` because the import endpoint answers with its OWN shape:
 * `{ error: {...}, rows: [{ row, column, reason }] }` — the row-level report is a **top-level
 * array**, not the API-05 envelope's `error.details`. Parsing it as a normal envelope would drop
 * the report entirely, which is the single thing the user needs to fix their file.
 *
 * Import is strictly all-or-nothing: whenever this is thrown, **zero rows were applied**.
 */
export class MappingImportError extends Error {
  readonly status: number
  readonly code: string
  /** Per-row failures, in file order. Empty for a whole-file rejection (e.g. >10,000 rows). */
  readonly rows: MappingImportRowError[]

  constructor(status: number, code: string, message: string, rows: MappingImportRowError[]) {
    super(message)
    this.name = "MappingImportError"
    this.status = status
    this.code = code
    this.rows = rows
  }
}
