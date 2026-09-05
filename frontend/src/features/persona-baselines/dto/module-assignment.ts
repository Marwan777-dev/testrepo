/** A permission module grant (module id + the coarse modes allowed on it). */
export interface ModuleAssignment {
  moduleId: string
  allowedModes: string[]
}
