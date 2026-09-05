// T104 [US4] — Customer Journey ScoringConfig state for the section page. Loads the current tenant
// parameters and exposes `save`, returning a discriminated outcome so the page can surface inline
// field errors (from the API-05 code) or a success toast. Mirrors useOrganizationSettings.

import { useCallback, useEffect, useState } from "react"

import {
  getScoringConfig,
  SettingsApiError,
  updateScoringConfig,
  type ScoringConfig,
  type ScoringConfigInput,
} from "@/features/settings/api"

export type ScoringSaveOutcome =
  | { ok: true; data: ScoringConfig }
  | { ok: false; code?: string }

export interface UseScoringConfigResult {
  data: ScoringConfig | null
  loading: boolean
  error: boolean
  saving: boolean
  reload: () => Promise<void>
  save: (input: ScoringConfigInput) => Promise<ScoringSaveOutcome>
}

export function useScoringConfig(): UseScoringConfigResult {
  const [data, setData] = useState<ScoringConfig | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(false)
  const [saving, setSaving] = useState(false)

  const reload = useCallback(async () => {
    setLoading(true)
    setError(false)
    try {
      setData(await getScoringConfig())
    } catch {
      setError(true)
      setData(null)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void reload()
  }, [reload])

  const save = useCallback(async (input: ScoringConfigInput): Promise<ScoringSaveOutcome> => {
    setSaving(true)
    try {
      const saved = await updateScoringConfig(input)
      setData(saved)
      return { ok: true, data: saved }
    } catch (err) {
      return { ok: false, code: err instanceof SettingsApiError ? err.code : undefined }
    } finally {
      setSaving(false)
    }
  }, [])

  return { data, loading, error, saving, reload, save }
}
