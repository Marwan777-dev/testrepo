// T067 [US2] — create/update orchestration for the KPI config form. Wraps createKpi / updateKpi and
// classifies the outcome so the page can react:
//   • "saved"                    → navigate back to the catalogue (which refetches on mount);
//   • "needs-structural-confirm" → a Scale change hit existing M-16 bindings (409
//     KPI_SCALE_CHANGE_AFFECTS_BINDINGS). The page re-submits with confirm=true after the user
//     accepts the BindingUsageConfirmDialog (the dialog itself ships in US-5);
//   • "error"                    → any other failure (validation, conflict, network).
//
// The optimistic catalogue update is intentionally a refetch-on-return: the catalogue hook
// (useKpiList) reloads when KpiManagementPage remounts, so a saved KPI shows immediately without a
// shared store.

import { useCallback, useState } from "react"

import {
  KpiApiError,
  createKpi,
  updateKpi,
  type KpiDetail,
  type KpiSaveInput,
} from "@/features/kpi-management/api"

export type KpiSaveMode = "create" | "edit"

export type KpiSaveOutcome =
  | { status: "saved"; kpi: KpiDetail }
  | { status: "needs-structural-confirm" }
  | { status: "error"; error: KpiApiError }

export interface UseKpiSaveResult {
  save: (
    mode: KpiSaveMode,
    id: string | null,
    input: KpiSaveInput,
    confirmStructuralChange?: boolean,
  ) => Promise<KpiSaveOutcome>
  saving: boolean
  error: KpiApiError | null
  reset: () => void
}

const SCALE_CHANGE_CODE = "KPI_SCALE_CHANGE_AFFECTS_BINDINGS"

export function useKpiSave(): UseKpiSaveResult {
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<KpiApiError | null>(null)

  const save = useCallback<UseKpiSaveResult["save"]>(
    async (mode, id, input, confirmStructuralChange = false) => {
      setSaving(true)
      setError(null)
      try {
        const kpi =
          mode === "create"
            ? await createKpi(input)
            : await updateKpi(id ?? "", input, confirmStructuralChange)
        return { status: "saved", kpi }
      } catch (e) {
        const err =
          e instanceof KpiApiError ? e : new KpiApiError(0, { error: { code: "network_error", message: String(e) } })
        if (err.status === 409 && err.code === SCALE_CHANGE_CODE) {
          return { status: "needs-structural-confirm" }
        }
        setError(err)
        return { status: "error", error: err }
      } finally {
        setSaving(false)
      }
    },
    [],
  )

  const reset = useCallback(() => setError(null), [])

  return { save, saving, error, reset }
}
