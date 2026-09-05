// T139 [US6] — Organization settings state for the section page. Loads the current settings, and
// exposes `saveSettings` (Name + Industry) and `uploadLogo`, each returning a discriminated result
// so the page can surface inline field errors (from the API-05 code) and success/sanitised toasts.
// Mirrors the M-06 useKpiDetail loading/error shape.

import { useCallback, useEffect, useState } from "react"

import {
  getOrganization,
  SettingsApiError,
  updateOrganization,
  uploadLogo as uploadLogoApi,
  type LogoUploadResult,
  type OrganizationSettings,
} from "@/features/settings/api"

export type SaveOutcome =
  | { ok: true; data: OrganizationSettings }
  | { ok: false; code?: string }

export type UploadOutcome =
  | { ok: true; result: LogoUploadResult }
  | { ok: false; code?: string }

export interface UseOrganizationSettingsResult {
  data: OrganizationSettings | null
  loading: boolean
  error: boolean
  saving: boolean
  uploading: boolean
  reload: () => Promise<void>
  saveSettings: (name: string, industry: string) => Promise<SaveOutcome>
  uploadLogo: (file: File) => Promise<UploadOutcome>
}

export function useOrganizationSettings(): UseOrganizationSettingsResult {
  const [data, setData] = useState<OrganizationSettings | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(false)
  const [saving, setSaving] = useState(false)
  const [uploading, setUploading] = useState(false)

  const reload = useCallback(async () => {
    setLoading(true)
    setError(false)
    try {
      setData(await getOrganization())
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

  const saveSettings = useCallback(async (name: string, industry: string): Promise<SaveOutcome> => {
    setSaving(true)
    try {
      const saved = await updateOrganization(name, industry)
      setData(saved)
      return { ok: true, data: saved }
    } catch (err) {
      return { ok: false, code: err instanceof SettingsApiError ? err.code : undefined }
    } finally {
      setSaving(false)
    }
  }, [])

  const uploadLogo = useCallback(async (file: File): Promise<UploadOutcome> => {
    setUploading(true)
    try {
      const result = await uploadLogoApi(file)
      // Refresh so the preview reflects the freshly persisted (sanitised) bytes.
      await reload()
      return { ok: true, result }
    } catch (err) {
      return { ok: false, code: err instanceof SettingsApiError ? err.code : undefined }
    } finally {
      setUploading(false)
    }
  }, [reload])

  return { data, loading, error, saving, uploading, reload, saveSettings, uploadLogo }
}
