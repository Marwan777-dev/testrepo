// US2 parameter-catalogue data hooks (T064).
//
// Unlike the service-channel catalogue (≤100 rows, VR-F13 — fetched whole and filtered in the
// browser), the parameter catalogue can hold **223** rows: the 23 seeded built-ins plus VR-F13's
// 200-custom ceiling. That is past the server's `limit` clamp of 200, so a fetch-all-and-filter
// hook would silently drop rows at the top of the range. Filtering therefore runs **server-side**
// (`origin`/`type`/`q` are AND-combined by `GET /parameters`), which also gives AC-S5-01 for free:
// the origin-tab counts ride on the response and are computed globally, so they do NOT move when
// the type filter or the search box narrows the rows.
//
// Search is debounced so a keystroke doesn't become a request.

import { useCallback, useEffect, useMemo, useRef, useState } from "react"

import {
  createParameter,
  listParameters,
  listServiceChannels,
  setParameterEnabled,
  updateParameter,
  type DataType,
  type Parameter,
  type ParameterCounts,
  type ParameterOrigin,
  type ParameterPatchResult,
  type ParameterSaveInput,
  type ServiceChannel,
} from "@/features/integration-hub/api"

/** 223 is the theoretical ceiling; the server clamps `limit` at 200, hence `truncated`. */
const FETCH_SIZE = 200
const SEARCH_DEBOUNCE_MS = 250

/** SCR-05's origin tabs. `all` is the absence of an `origin` query param, not a wire value. */
export type OriginTab = "all" | ParameterOrigin

/** SCR-05's type filter. `all` is the absence of a `type` query param. */
export type TypeFilter = "all" | DataType

export interface UseParametersResult {
  items: Parameter[]
  /**
   * Global origin counts (AC-S5-01) — deliberately NOT derived from `items`, which is the
   * filtered page. A count that moved when you typed in the search box would read as a bug.
   * `null` until the first response lands, so the tab pills render nothing rather than `0`.
   */
  counts: ParameterCounts | null
  /** True when rows existed past the fetched page — surfaced to the user, never silent. */
  truncated: boolean
  loading: boolean
  error: boolean
  origin: OriginTab
  setOrigin: (origin: OriginTab) => void
  search: string
  setSearch: (search: string) => void
  type: TypeFilter
  setType: (type: TypeFilter) => void
  isFiltered: boolean
  clearFilters: () => void
  reload: () => Promise<void>
  /**
   * BR-10's two-step disable. A disable on a referenced parameter resolves with
   * `requiresConfirmation` and the reference list, having written **nothing** — the caller renders
   * Dialog D-6 and calls again with `confirmDisable`. Rejects on transport failure.
   */
  setEnabled: (
    parameter: Parameter,
    enabled: boolean,
    confirmDisable?: boolean,
  ) => Promise<ParameterPatchResult>
}

/** SCR-05 list data (FR-S5-01…03). */
export function useParameters(): UseParametersResult {
  const [items, setItems] = useState<Parameter[]>([])
  const [counts, setCounts] = useState<ParameterCounts | null>(null)
  const [truncated, setTruncated] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(false)

  const [origin, setOrigin] = useState<OriginTab>("all")
  const [type, setType] = useState<TypeFilter>("all")
  const [search, setSearch] = useState("")
  const [debouncedSearch, setDebouncedSearch] = useState("")

  useEffect(() => {
    const handle = window.setTimeout(() => setDebouncedSearch(search), SEARCH_DEBOUNCE_MS)
    return () => window.clearTimeout(handle)
  }, [search])

  // Bumped by `reload()` so an explicit refresh re-runs the effect even when no filter moved.
  const [refreshToken, setRefreshToken] = useState(0)
  // Guards against an out-of-order response overwriting a newer one when filters change fast.
  const requestSeq = useRef(0)

  useEffect(() => {
    const seq = ++requestSeq.current
    let cancelled = false

    async function load() {
      setLoading(true)
      setError(false)
      try {
        const page = await listParameters({
          origin: origin === "all" ? undefined : origin,
          type: type === "all" ? undefined : type,
          q: debouncedSearch.trim() || undefined,
          limit: FETCH_SIZE,
        })
        if (cancelled || seq !== requestSeq.current) return
        setItems(page.items)
        setCounts(page.counts)
        setTruncated(page.nextCursor != null)
      } catch {
        if (cancelled || seq !== requestSeq.current) return
        setError(true)
        setItems([])
        setTruncated(false)
      } finally {
        if (!cancelled && seq === requestSeq.current) setLoading(false)
      }
    }

    void load()
    return () => {
      cancelled = true
    }
  }, [origin, type, debouncedSearch, refreshToken])

  const reload = useCallback(async () => {
    setRefreshToken((token) => token + 1)
  }, [])

  const setEnabled = useCallback(
    async (parameter: Parameter, enabled: boolean, confirmDisable = false) => {
      const result = await setParameterEnabled(parameter.id, enabled, confirmDisable)
      // Nothing was written when the server withheld the disable pending confirmation, so the
      // row must keep its current state until the user confirms.
      if (!result.requiresConfirmation) {
        setItems((prev) => prev.map((row) => (row.id === parameter.id ? result.parameter : row)))
      }
      return result
    },
    [],
  )

  const clearFilters = useCallback(() => {
    setOrigin("all")
    setType("all")
    setSearch("")
  }, [])

  const isFiltered = origin !== "all" || type !== "all" || search.trim() !== ""

  return {
    items,
    counts,
    truncated,
    loading,
    error,
    origin,
    setOrigin,
    search,
    setSearch,
    type,
    setType,
    isFiltered,
    clearFilters,
    reload,
    setEnabled,
  }
}

export interface UseParameterEditorResult {
  /** Active channels only — FR-S6-05's pills assign the parameter as *supported* on a channel. */
  channels: ServiceChannel[]
  /** False once the channel list has resolved; the drawer's fields render regardless. */
  channelsLoading: boolean
  saving: boolean
  /** Rejects with `IntegrationHubApiError` so the drawer can map API-05 codes onto its fields. */
  save: (input: ParameterSaveInput, id?: string) => Promise<Parameter>
}

/**
 * SCR-06 drawer data: the active service channels the assignment pills are built from, plus the
 * create/update round-trip. The parameter being edited comes from the already-loaded list row —
 * `GET /parameters` returns the full entity, so the drawer needs no second fetch.
 */
export function useParameterEditor(): UseParameterEditorResult {
  const [channels, setChannels] = useState<ServiceChannel[]>([])
  const [channelsLoading, setChannelsLoading] = useState(true)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    let cancelled = false

    async function load() {
      setChannelsLoading(true)
      try {
        const page = await listServiceChannels({ active: true, limit: FETCH_SIZE })
        if (!cancelled) setChannels(page.items)
      } catch {
        // A failed channel load must not block creating a parameter — the pills are optional
        // (FR-S6-05 fine-tunes in the channel's own contract), so degrade to "no channels".
        if (!cancelled) setChannels([])
      } finally {
        if (!cancelled) setChannelsLoading(false)
      }
    }

    void load()
    return () => {
      cancelled = true
    }
  }, [])

  const save = useCallback(async (input: ParameterSaveInput, id?: string) => {
    setSaving(true)
    try {
      if (!id) return await createParameter(input)
      // PATCH, not PUT: only the fields the drawer owns are sent, so a partial edit never
      // resets something the server knows and this client didn't render.
      const result = await updateParameter(id, input)
      return result.parameter
    } finally {
      setSaving(false)
    }
  }, [])

  return useMemo(
    () => ({ channels, channelsLoading, saving, save }),
    [channels, channelsLoading, saving, save],
  )
}
