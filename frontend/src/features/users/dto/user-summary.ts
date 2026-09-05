import type { Persona } from "./persona"
import type { UserStatus } from "./user-status"

/** A user row as returned by `GET /api/v1/users` and write-endpoint responses. */
export interface UserSummary {
  userId: string
  username: string
  persona: Persona
  status: UserStatus
  isMfaEnrolled: boolean
  organizationNodeId?: string | null
  createdAt: string
  updatedAt: string
}
