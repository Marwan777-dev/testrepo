/** Response of create/update of a custom authorization rule. */
export interface CustomRuleResponse {
  ruleId: string
  allowedActions: string[]
  parameterScopeAssignments: Record<string, string[]>
  createdAt: string
}
