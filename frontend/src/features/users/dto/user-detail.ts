import type { ModuleAssignment } from "./module-assignment"
import type { UserSummary } from "./user-summary"

/** Response of `GET /api/v1/users/{userId}` — profile plus permission module grants. */
export interface UserDetail extends UserSummary {
  lastPermissionSnapshotVersion: number
  permissionModuleAssignments: ModuleAssignment[]
}
