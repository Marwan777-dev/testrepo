/** Compact permission snapshot embedded in the session (see data-model.md). */
export interface PermissionSnapshot {
  version: number
  modules: Record<string, string[]>
  customActions: string[]
  scopeAssignments: Record<string, string[]>
  hierarchyNodeId: string | null
  hierarchyDescendantIds: string[]
}
