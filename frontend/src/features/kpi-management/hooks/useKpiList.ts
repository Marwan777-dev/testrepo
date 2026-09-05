// US-1 catalogue data hook. The KPI catalogue is bounded (≤ ~60 rows/tenant, research.md R8), so —
// like the journeys list — we fetch the full set once and apply the Type / Active-only / Search
// filters client-side. This keeps the "[X] Active KPIs" subtitle stable (it counts active rows
// across the whole catalogue, not just the filtered view) and needs no polling or SSE: activation
// changes made this session are reflected by `mutateActivation` updating the local cache in place
// (FR-008 / SC-011).

import { useCallback, useEffect, useMemo, useState } from "react"

import { listKpis, type KpiListItem, type KpiTypeFilter } from "@/features/kpi-management/api"

// One page covers the catalogue; the server clamps to 200.
const FETCH_SIZE = 200

export interface UseKpiListResult {
  /** Rows after applying the Type / Active-only / Search filters. */
  items: KpiListItem[]
  /** Active KPIs across the whole catalogue (independent of the current filters). */
  activeCount: number
  /** True when more rows existed beyond the fetched page (surfaced as a note, never silent). */
  truncated: boolean
  loading: boolean
  error: boolean
  type: KpiTypeFilter
  setType: (type: KpiTypeFilter) => void
  activeOnly: boolean
  setActiveOnly: (activeOnly: boolean) => void
  search: string
  setSearch: (search: string) => void
  /** True when any filter is narrowing the list (drives the filtered-empty state). */
  isFiltered: boolean
  clearFilters: () => void
  reload: () => Promise<void>
  /** Same-session local-cache update when a KPI is (de)activated elsewhere (no refetch). */
  mutateActivation: (kpiId: string, active: boolean) => void
}

export function useKpiList(): UseKpiListResult {
  const [allItems, setAllItems] = useState<KpiListItem[]>([])
  const [truncated, setTruncated] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(false)

  const [type, setType] = useState<KpiTypeFilter>("All")
  const [activeOnly, setActiveOnly] = useState(true)
  const [search, setSearch] = useState("")

  const reload = useCallback(async () => {
    setLoading(true)
    setError(false)
    try {
      // Load everything (inactive included) so the active count and the Active-only toggle are
      // both driven from one fetched set.
      const result = await listKpis({ activeOnly: false, limit: FETCH_SIZE })
      setAllItems(result.items)
      setTruncated(result.nextCursor != null)
    } catch {
      setError(true)
      setAllItems([])
      setTruncated(false)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void reload()
  }, [reload])

  const activeCount = useMemo(() => allItems.filter((k) => k.isActive).length, [allItems])

  const items = useMemo(() => {
    const q = search.trim().toLowerCase()
    return allItems.filter((k) => {
      if (type !== "All" && k.kpiType !== type) return false
      // Standard KPIs are always present regardless of active status (the Active-only toggle never
      // hides them) — UNLESS the user is searching, in which case they follow the normal filters and
      // a non-matching standard KPI is hidden like any other row.
      const exemptFromActiveOnly = k.kpiType === "Standard" && q === ""
      if (activeOnly && !k.isActive && !exemptFromActiveOnly) return false
      if (q && !`${k.shortName} ${k.fullName}`.toLowerCase().includes(q)) return false
      return true
    })
  }, [allItems, type, activeOnly, search])

  const isFiltered = type !== "All" || !activeOnly || search.trim() !== ""

  const clearFilters = useCallback(() => {
    setType("All")
    setActiveOnly(true)
    setSearch("")
  }, [])

  const mutateActivation = useCallback((kpiId: string, active: boolean) => {
    setAllItems((prev) =>
      prev.map((k) =>
        k.id === kpiId
          ? { ...k, isActive: active, showOnDashboard: active ? k.showOnDashboard : false }
          : k,
      ),
    )
  }, [])

  return {
    items,
    activeCount,
    truncated,
    loading,
    error,
    type,
    setType,
    activeOnly,
    setActiveOnly,
    search,
    setSearch,
    isFiltered,
    clearFilters,
    reload,
    mutateActivation,
  }
}
