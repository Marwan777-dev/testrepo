// M-06 KPI Management API client — thin endpoint functions over the shared `callJson` helper.
//
// US-1 ships `listKpis` (T037). US-2/US-3 add createKpi/updateKpi/getKpi/getKpiBindingUsage (T071)
// and the CXI weights surface. Wire types live in ./dto, the transport in ./http, the error in
// ./kpi-api-error; they are re-exported so callers import everything from
// "@/features/kpi-management/api". The catalogue endpoint serialises enum fields as their string
// member name (the controller projects via .ToString()); ./dto's `mapKpiListItem` maps the
// snake_case wire row to the camelCase domain row at this boundary (CLAUDE.md "Backend Integration").

import { callJson } from "./http"
import {
  mapBindingUsage,
  mapCxiWeights,
  mapKpiDetail,
  mapKpiListItem,
  toCxiWeightsRequestWire,
  toKpiConfigRequestWire,
  type BindingUsage,
  type BindingUsageWire,
  type CxiWeightItem,
  type CxiWeightUpdateItem,
  type CxiWeightsResponseWire,
  type KpiConfigResponseWire,
  type KpiDetail,
  type KpiListResponseWire,
  type KpiListResult,
  type KpiSaveInput,
  type KpiTypeFilter,
} from "./dto"

export type {
  ApiErrorEnvelope,
  BilingualText,
  BindingUsage,
  CalculationMethodKind,
  CxiWeightItem,
  CxiWeightUpdateItem,
  EmojiSet,
  KpiDetail,
  KpiListItem,
  KpiListResult,
  KpiPerspective,
  KpiSaveInput,
  KpiThresholdBand,
  KpiType,
  KpiTypeFilter,
  RepresentationStyle,
  Scale,
} from "./dto"
export { KpiApiError } from "./kpi-api-error"

export interface ListKpisParams {
  /** `All` (default) omits the `type` filter. */
  type?: KpiTypeFilter
  /** Defaults to the server default (true). Pass `false` to include inactive rows. */
  activeOnly?: boolean
  /** Trimmed, case-insensitive substring over short ∪ full name. */
  search?: string
  /** Opaque cursor from a previous page's `nextCursor`. */
  cursor?: string
  /** Page size (server clamps to 1..200; default 50). */
  limit?: number
}

/** Lists the tenant's KPI catalogue (cursor-paginated, canonically ordered). `GET /api/v1/kpis`. */
export async function listKpis(params: ListKpisParams = {}): Promise<KpiListResult> {
  const query = new URLSearchParams()
  if (params.type && params.type !== "All") query.set("type", params.type)
  if (params.activeOnly === false) query.set("active_only", "false")
  if (params.activeOnly === true) query.set("active_only", "true")
  const search = params.search?.trim()
  if (search) query.set("search", search)
  if (params.cursor) query.set("cursor", params.cursor)
  if (params.limit != null) query.set("limit", String(params.limit))

  const qs = query.toString()
  const res = await callJson<KpiListResponseWire>(`/kpis${qs ? `?${qs}` : ""}`)
  return {
    items: res.items.map(mapKpiListItem),
    nextCursor: res.next_cursor ?? null,
  }
}

/**
 * Reads one KPI's full configuration. `GET /api/v1/kpis/{key}` — `key` is the KPI's GUID id or its
 * (case-insensitive) Short Name, so the config page can load from a human-readable URL.
 */
export async function getKpi(key: string): Promise<KpiDetail> {
  const res = await callJson<KpiConfigResponseWire>(`/kpis/${encodeURIComponent(key)}`)
  return mapKpiDetail(res)
}

/** Creates a custom KPI. `POST /api/v1/kpis`. Returns the saved configuration. */
export async function createKpi(input: KpiSaveInput): Promise<KpiDetail> {
  const res = await callJson<KpiConfigResponseWire>("/kpis", {
    method: "POST",
    body: toKpiConfigRequestWire(input),
  })
  return mapKpiDetail(res)
}

/**
 * Updates an existing KPI. `PUT /api/v1/kpis/{id}`. Pass `confirmStructuralChange` to acknowledge a
 * Scale change that affects existing M-16 bindings (FR-017) — otherwise the server returns 409
 * `KPI_SCALE_CHANGE_AFFECTS_BINDINGS`.
 */
export async function updateKpi(
  id: string,
  input: KpiSaveInput,
  confirmStructuralChange = false,
): Promise<KpiDetail> {
  const qs = confirmStructuralChange ? "?confirm_structural_change=true" : ""
  const res = await callJson<KpiConfigResponseWire>(`/kpis/${encodeURIComponent(id)}${qs}`, {
    method: "PUT",
    body: toKpiConfigRequestWire(input),
  })
  return mapKpiDetail(res)
}

/** Probes M-16 for the KPI's binding-usage counts. `GET /api/v1/kpis/{id}/binding-usage`. */
export async function getKpiBindingUsage(id: string): Promise<BindingUsage> {
  const res = await callJson<BindingUsageWire>(`/kpis/${encodeURIComponent(id)}/binding-usage`)
  return mapBindingUsage(res)
}

/**
 * Activates or deactivates a KPI. `PATCH /api/v1/kpis/{id}/activation` (FR-026). Pass `confirm` to
 * acknowledge deactivating a KPI still bound to M-16 touchpoints — otherwise the server returns 409
 * `KPI_DEACTIVATION_REQUIRES_CONFIRMATION` with the binding-usage counts. A confirmed deactivation
 * cascades server-side (Show-on-Dashboard forced off, removal from every CXI it belonged to).
 */
export async function setKpiActivation(
  id: string,
  active: boolean,
  confirm = false,
): Promise<KpiDetail> {
  const res = await callJson<KpiConfigResponseWire>(`/kpis/${encodeURIComponent(id)}/activation`, {
    method: "PATCH",
    body: { active, confirm },
  })
  return mapKpiDetail(res)
}

/**
 * Full-replaces a CXI's weights table. `PUT /api/v1/kpis/{cxi_id}/weights`. Zero-weight entries are
 * dropped (BR-2.3); the server returns the recomputed table with effective percentages summing to
 * 100 (±0.1). Throws `KpiApiError` with `CXI_INSUFFICIENT_MEMBERS` / `CXI_MEMBER_NOT_ACTIVE` /
 * `CXI_CANNOT_INCLUDE_ITSELF` / `CXI_WEIGHT_INVALID` on a rejected save.
 */
export async function updateCxiWeights(
  cxiId: string,
  weights: CxiWeightUpdateItem[],
): Promise<CxiWeightItem[]> {
  const res = await callJson<CxiWeightsResponseWire>(`/kpis/${encodeURIComponent(cxiId)}/weights`, {
    method: "PUT",
    body: toCxiWeightsRequestWire(weights),
  })
  return mapCxiWeights(res)
}
