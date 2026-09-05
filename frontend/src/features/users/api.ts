// M-10 User Management API client — thin endpoint functions over `callJson`.
//
// Endpoints follow specs/001-user-role-management/contracts/users-api.md. Wire
// types live in ./dto, the transport helper in ./http, the error type in
// ./users-api-error. Those are re-exported here so callers can import everything
// users-API from "@/features/users/api".
//
// Wire note: `status` and `persona` arrive as strings (the controller serializes via
// UserStatus.ToWire()), so no enum-integer normalization is required here.

import { callJson } from "./http"
import type {
  CreateUserData,
  ListUsersParams,
  ModuleAssignment,
  UpdateUserData,
  UserDetail,
  UserListResponse,
  UserSummary,
} from "./dto"

export type * from "./dto"
export { UsersApiError } from "./users-api-error"

/** Lists tenant users (cursor-paginated) with optional status/persona/search filters. */
export function listUsers(params: ListUsersParams = {}): Promise<UserListResponse> {
  const query = new URLSearchParams()
  if (params.pageSize != null) query.set("page_size", String(params.pageSize))
  if (params.pageToken) query.set("page_token", params.pageToken)
  if (params.status) query.set("status", params.status)
  if (params.persona) query.set("persona", params.persona)
  if (params.q) query.set("q", params.q)
  const qs = query.toString()
  return callJson<UserListResponse>(qs ? `?${qs}` : "")
}

/** Invites a new tenant user (P-01/P-07 only). */
export function createUser(data: CreateUserData): Promise<UserSummary> {
  return callJson<UserSummary>("", { method: "POST", body: data })
}

/** Returns a user's profile plus permission module assignments. */
export function getUser(id: string): Promise<UserDetail> {
  return callJson<UserDetail>(`/${id}`)
}

/** Updates a user's profile (persona change is P-01-only). */
export function updateUser(id: string, data: UpdateUserData): Promise<void> {
  return callJson<void>(`/${id}`, { method: "PUT", body: data })
}

/** Soft-deletes a user and revokes their sessions. */
export function deactivateUser(id: string): Promise<void> {
  return callJson<void>(`/${id}/deactivate`, { method: "POST" })
}

/** Re-activates an inactive user. */
export function reactivateUser(id: string): Promise<void> {
  return callJson<void>(`/${id}/reactivate`, { method: "POST" })
}

/** Manually unlocks a locked account. */
export function unlockUser(id: string): Promise<void> {
  return callJson<void>(`/${id}/unlock`, { method: "POST" })
}

/** Admin-triggered MFA reset (forces re-enrollment). */
export function resetMfa(id: string): Promise<void> {
  return callJson<void>(`/${id}/mfa-reset`, { method: "POST" })
}

/** Admin-triggered password reset (notifies the user via M-09). */
export function adminPasswordReset(id: string): Promise<void> {
  return callJson<void>(`/${id}/password-reset`, { method: "POST" })
}

/** Replaces a user's full set of permission module assignments. */
export function updatePermissions(id: string, assignments: ModuleAssignment[]): Promise<void> {
  return callJson<void>(`/${id}/permissions`, { method: "PUT", body: { assignments } })
}
