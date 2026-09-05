import type { PermissionSnapshot } from "./permission-snapshot"

/**
 * Returned when a session is created — by both `mfaVerify` and
 * `mfaEnrollConfirm`.
 */
export interface SessionTokenResponse {
  sessionToken: string
  userId: string
  expiresAtUtc: string
  permissionSnapshot: PermissionSnapshot
}
