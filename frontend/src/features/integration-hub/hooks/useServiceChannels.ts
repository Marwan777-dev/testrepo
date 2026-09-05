// US1 service-channel data hooks (T040).
//
// The catalogue is bounded by VR-F13 at ≤100 channels per tenant, so — like the KPI catalogue —
// we fetch one large page and filter client-side rather than round-tripping per keystroke. The
// server clamps `limit` to 1…200, which covers the ceiling in a single request.

import { useCallback, useEffect, useMemo, useState } from "react"

import {
  createServiceChannel,
  getServiceChannel,
  listParameters,
  listServiceChannels,
  updateServiceChannel,
  type Parameter,
  type ServiceChannel,
  type ServiceChannelSaveInput,
} from "@/features/integration-hub/api"

const FETCH_SIZE = 200

export interface UseServiceChannelsResult {
  /** Rows after the search filter. */
  items: ServiceChannel[]
  /** Every channel, independent of the filter — drives the "n channels" count. */
  allItems: ServiceChannel[]
  activeCount: number
  /** True when more rows existed beyond the fetched page (surfaced, never silent). */
  truncated: boolean
  loading: boolean
  error: boolean
  search: string
  setSearch: (search: string) => void
  isFiltered: boolean
  reload: () => Promise<void>
}

/** SCR-03 list data. */
export function useServiceChannels(): UseServiceChannelsResult {
  const [allItems, setAllItems] = useState<ServiceChannel[]>([])
  const [truncated, setTruncated] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(false)
  const [search, setSearch] = useState("")

  const reload = useCallback(async () => {
    setLoading(true)
    setError(false)
    try {
      const page = await listServiceChannels({ limit: FETCH_SIZE })
      setAllItems(page.items)
      setTruncated(page.nextCursor != null)
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

  const items = useMemo(() => {
    const q = search.trim().toLowerCase()
    if (!q) return allItems
    return allItems.filter(
      (c) =>
        c.nameEn.toLowerCase().includes(q) ||
        c.nameAr.toLowerCase().includes(q) ||
        c.channelId.toLowerCase().includes(q),
    )
  }, [allItems, search])

  const activeCount = useMemo(() => allItems.filter((c) => c.active).length, [allItems])

  return {
    items,
    allItems,
    activeCount,
    truncated,
    loading,
    error,
    search,
    setSearch,
    isFiltered: search.trim() !== "",
    reload,
  }
}

export interface UseServiceChannelFormResult {
  /** Undefined in create mode, or while the edit-mode fetch is in flight. */
  channel: ServiceChannel | undefined
  /** Enabled parameters only — SCR-04 lists active parameters (FR-S4-04). */
  parameters: Parameter[]
  loading: boolean
  loadError: boolean
  saving: boolean
  /** Rejects with `IntegrationHubApiError` so the form can map codes onto fields. */
  save: (input: ServiceChannelSaveInput) => Promise<ServiceChannel>
}

/**
 * SCR-04 form data: the channel being edited (if any) plus the enabled parameter catalogue the
 * contract table is built from. `channelId` undefined ⇒ create mode.
 */
export function useServiceChannelForm(channelId?: string): UseServiceChannelFormResult {
  const [channel, setChannel] = useState<ServiceChannel | undefined>(undefined)
  const [parameters, setParameters] = useState<Parameter[]>([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(false)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    let cancelled = false

    async function load() {
      setLoading(true)
      setLoadError(false)
      try {
        const [parameterPage, loaded] = await Promise.all([
          listParameters({ limit: FETCH_SIZE }),
          channelId ? getServiceChannel(channelId) : Promise.resolve(undefined),
        ])
        if (cancelled) return
        // Only active parameters are offered in the contract (SCR-04 card description).
        setParameters(parameterPage.items.filter((p) => p.enabled))
        setChannel(loaded)
      } catch {
        if (!cancelled) setLoadError(true)
      } finally {
        if (!cancelled) setLoading(false)
      }
    }

    void load()
    return () => {
      cancelled = true
    }
  }, [channelId])

  const save = useCallback(
    async (input: ServiceChannelSaveInput) => {
      setSaving(true)
      try {
        return channelId
          ? await updateServiceChannel(channelId, input)
          : await createServiceChannel(input)
      } finally {
        setSaving(false)
      }
    },
    [channelId],
  )

  return { channel, parameters, loading, loadError, saving, save }
}
