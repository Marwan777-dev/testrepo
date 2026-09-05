import type { DataScopeAssignment } from "./data-scope-assignment"

/** Request body of PUT /api/v1/users/{id}/scope. */
export interface UpdateUserScopeData {
  organizationNodeId: string | null
  dataScopeAssignments: DataScopeAssignment[]
}
