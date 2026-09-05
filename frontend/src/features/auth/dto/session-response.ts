import type { PermissionSnapshot } from "./permission-snapshot"

/** Response of `GET /api/v1/auth/session` — current session + permissions. */
export interface SessionResponse {
  userId: string
  persona: string
  expiresAtUtc: string
  permissionSnapshot: PermissionSnapshot
}
