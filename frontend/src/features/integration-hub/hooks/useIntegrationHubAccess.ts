// Integration Hub access gate — the client-side projection of spec.md's Permissions Matrix.
//
// PRODUCT-SIDE, NOT IN THE CLICK-THROUGH. The click-through's copy of this hook returns "manage"
// for every area **by design** (its own header says so), so a client can walk the whole module
// without switching personas. The product must gate for real, so this file is deliberately a
// divergence from the design source of truth rather than drift from it.
//
// The two owning personas mirror each other (BR-24): P-07 (Tenant IT Admin) manages integrations
// and P-01 (CX Manager) manages the data model, and each sees the other's screens read-only.
// **Request logs are the single exception** — P-07 only, with no cross-persona read grant at all.
//
// This gate is a rendering convenience (FR-GBL-05), never the enforcement: every M-13 endpoint
// re-checks its permission key server-side, so a persona that reaches a screen by URL still gets
// a 403 from the API.

import { useMemo } from "react"

import { useSession } from "@/features/auth/hooks/useSession"

/** The five M-13 screens the matrix is keyed by. */
export type IntegrationHubArea =
  | "integrations"
  | "requestLogs"
  | "serviceChannels"
  | "parameters"
  | "mappings"

/** None (screen not offered at all) / ReadOnly (renders, write controls hidden) / Manage. */
export type AccessLevel = "none" | "readOnly" | "manage"

export interface IntegrationHubAccess {
  /** Raw level for an area. */
  levelFor: (area: IntegrationHubArea) => AccessLevel
  /** True when the screen may be rendered at all (ReadOnly or Manage). */
  canView: (area: IntegrationHubArea) => boolean
  /** True when write controls should be offered (Manage only). */
  canManage: (area: IntegrationHubArea) => boolean
  /** True when the screen renders but every write control is hidden (FR-GBL-05). */
  isReadOnly: (area: IntegrationHubArea) => boolean
  /**
   * False while the session is still hydrating. Callers MUST render a skeleton rather than a
   * decision on `false` — treating "not loaded yet" as "no grant" flashes the access-denied
   * state at a persona that is in fact allowed.
   */
  ready: boolean
  persona: string | undefined
}

const NONE: Record<IntegrationHubArea, AccessLevel> = {
  integrations: "none",
  requestLogs: "none",
  serviceChannels: "none",
  parameters: "none",
  mappings: "none",
}

/** spec.md → Permissions Matrix, one column per persona. */
const MATRIX: Record<string, Record<IntegrationHubArea, AccessLevel>> = {
  // P-07 Tenant IT Admin — owns the runtime edge.
  "P-07": {
    integrations: "manage",
    requestLogs: "manage",
    serviceChannels: "readOnly",
    parameters: "readOnly",
    mappings: "readOnly",
  },
  // P-01 CX Manager — owns the data model; no `m13.log.view` grant of any kind.
  "P-01": {
    integrations: "readOnly",
    requestLogs: "none",
    serviceChannels: "manage",
    parameters: "manage",
    mappings: "manage",
  },
}

export function useIntegrationHubAccess(): IntegrationHubAccess {
  const { session, loading } = useSession()
  const persona = session?.persona

  return useMemo(() => {
    const grants = (persona && MATRIX[persona]) || NONE
    const levelFor = (area: IntegrationHubArea) => grants[area]
    return {
      levelFor,
      canView: (area: IntegrationHubArea) => levelFor(area) !== "none",
      canManage: (area: IntegrationHubArea) => levelFor(area) === "manage",
      isReadOnly: (area: IntegrationHubArea) => levelFor(area) === "readOnly",
      ready: !loading,
      persona,
    }
  }, [persona, loading])
}
