/** A per-user fine-grained authorization rule as returned in the user's scope. */
export interface CustomRule {
  ruleId: string
  allowedActions: string[]
  parameterScopeAssignments: Record<string, string[]>
}
