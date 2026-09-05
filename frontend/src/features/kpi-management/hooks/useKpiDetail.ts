// T067 [US2] — single-KPI fetch for the configuration page (edit mode). The `key` is the KPI's GUID
// id or its (case-insensitive) Short Name — the edit URL uses the Short Name (/kpi-management/cxi) and
// the backend GET resolves either. In create mode (`key` undefined) it stays idle with `kpi = null`.
// Mirrors the useKpiList loading/error shape.

import { useCallback, useEffect, useState } from "react"

import { getKpi, type KpiDetail } from "@/features/kpi-management/api"

export interface UseKpiDetailResult {
  kpi: KpiDetail | null
  loading: boolean
  error: boolean
  reload: () => Promise<void>
}

export function useKpiDetail(key: string | undefined): UseKpiDetailResult {
  const [kpi, setKpi] = useState<KpiDetail | null>(null)
  const [loading, setLoading] = useState(Boolean(key))
  const [error, setError] = useState(false)

  const reload = useCallback(async () => {
    if (!key) {
      setKpi(null)
      setLoading(false)
      setError(false)
      return
    }
    setLoading(true)
    setError(false)
    try {
      setKpi(await getKpi(key))
    } catch {
      setError(true)
      setKpi(null)
    } finally {
      setLoading(false)
    }
  }, [key])

  useEffect(() => {
    void reload()
  }, [reload])

  return { kpi, loading, error, reload }
}
