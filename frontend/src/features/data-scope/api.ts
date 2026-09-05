// M-10 Data Scope & Custom Rules API client — thin endpoint functions over `callJson`.
//
// Endpoints follow specs/001-user-role-management/contracts/permissions-api.md. Wire
// types live in ./dto, the transport helper in ./http, the error type in
// ./data-scope-api-error. Those are re-exported here so callers can import everything
// data-scope-API from "@/features/data-scope/api".

import { callJson } from "./http"
import type { CustomRuleData, CustomRuleResponse, UpdateUserScopeData, UserScope } from "./dto"

export type * from "./dto"
export { DataScopeApiError } from "./data-scope-api-error"

/** Returns a user's hierarchy node, parameter scope assignments, and custom rules. */
export function getUserScope(userId: string): Promise<UserScope> {
  return callJson<UserScope>(`/users/${userId}/scope`)
}

/** Replaces a user's hierarchy node and parameter scope assignments (422 on invalid values). */
export function updateUserScope(userId: string, data: UpdateUserScopeData): Promise<UserScope> {
  return callJson<UserScope>(`/users/${userId}/scope`, { method: "PUT", body: data })
}

/** Creates a custom authorization rule for a user. */
export function createCustomRule(userId: string, data: CustomRuleData): Promise<CustomRuleResponse> {
  return callJson<CustomRuleResponse>(`/users/${userId}/custom-rules`, { method: "POST", body: data })
}

/** Updates an existing custom authorization rule. */
export function updateCustomRule(userId: string, ruleId: string, data: CustomRuleData): Promise<CustomRuleResponse> {
  return callJson<CustomRuleResponse>(`/users/${userId}/custom-rules/${ruleId}`, { method: "PUT", body: data })
}

/** Deletes a custom authorization rule. */
export function deleteCustomRule(userId: string, ruleId: string): Promise<void> {
  return callJson<void>(`/users/${userId}/custom-rules/${ruleId}`, { method: "DELETE" })
}
