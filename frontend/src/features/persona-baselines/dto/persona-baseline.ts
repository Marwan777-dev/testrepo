import type { ModuleAssignment } from "./module-assignment"
import type { Persona } from "./persona"

/** A persona authorization-matrix baseline as returned by `GET /api/v1/persona-baselines`. */
export interface PersonaBaseline {
  baselineId: string
  personaId: Persona
  permissionModuleAssignments: ModuleAssignment[]
  defaultDataScopeRules: Record<string, string[]>
  isCustomised: boolean
  updatedAt: string
}
