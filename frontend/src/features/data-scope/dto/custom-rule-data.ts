/** Request body for create/update of a custom authorization rule. */
export interface CustomRuleData {
  allowedActions: string[]
  parameterScopeAssignments: Record<string, string[]>
}
