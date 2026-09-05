import type { CustomRule } from "./custom-rule"
import type { DataScopeAssignment } from "./data-scope-assignment"

/** Response of GET /api/v1/users/{id}/scope. */
export interface UserScope {
  organizationNodeId: string | null
  dataScopeAssignments: DataScopeAssignment[]
  customRules: CustomRule[]
}
