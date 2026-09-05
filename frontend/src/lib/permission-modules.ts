// The DOC-02 permission module catalogue as the UI needs it: the canonical module
// ids, whether each is CX-domain (P-01-exclusive — P-07 may not assign these), and
// the coarse access modes. Mirrors the backend DataLayerAuthorizationGuard's CX set.
// Shared across the users and persona-baselines features.

export interface PermissionModuleMeta {
  id: string
  /** True for the 7 CX-domain modules only P-01 may assign. */
  cx: boolean
}

export const PERMISSION_MODULES: PermissionModuleMeta[] = [
  { id: "SurveyBuilder", cx: true },
  { id: "ChannelManagement", cx: true },
  { id: "AudienceManagement", cx: true },
  { id: "AnalyticsAndReporting", cx: true },
  { id: "CaseManagement", cx: true },
  { id: "AlertsAndNotifications", cx: true },
  { id: "KpiConfiguration", cx: true },
  { id: "UserManagement", cx: false },
  { id: "TenantConfiguration", cx: false },
]

export const PERMISSION_MODES = ["View", "Manage", "Full"] as const
