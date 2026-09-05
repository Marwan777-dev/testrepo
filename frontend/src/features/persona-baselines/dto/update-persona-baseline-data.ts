import type { ModuleAssignment } from "./module-assignment"

/** Request body for `updatePersonaBaseline` (`PUT /api/v1/persona-baselines/{personaId}`). */
export interface UpdatePersonaBaselineData {
  permissionModuleAssignments: ModuleAssignment[]
}
