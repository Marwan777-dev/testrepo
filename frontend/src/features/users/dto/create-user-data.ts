import type { Persona } from "./persona"

/** Request body for `createUser` (`POST /api/v1/users`). */
export interface CreateUserData {
  username: string
  persona: Persona
  /** Initial password the admin sets; the user signs in with it and enrols MFA on first login. */
  password: string
  organizationNodeId?: string | null
}
