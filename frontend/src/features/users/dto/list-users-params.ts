import type { Persona } from "./persona"
import type { UserStatus } from "./user-status"

/** Query parameters for `listUsers` (maps to the `GET /api/v1/users` query string). */
export interface ListUsersParams {
  pageSize?: number
  pageToken?: string
  status?: UserStatus
  persona?: Persona
  q?: string
}
