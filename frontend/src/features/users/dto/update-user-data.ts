import type { Persona } from "./persona"

/** Request body for `updateUser` (`PUT /api/v1/users/{userId}`). */
export interface UpdateUserData {
  persona?: Persona
  organizationNodeId?: string | null
}
