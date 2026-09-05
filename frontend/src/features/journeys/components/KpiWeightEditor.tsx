// KPI weight editor (US-2, T055): edits one touchpoint's KPI bindings. Each binding is a row —
// a KPI-type picker (sourced from `GET /api/v1/kpi-types`, with already-chosen types filtered
// out so duplicates can't form) plus an integer weight input. A live sum indicator turns red
// whenever the weights don't total exactly 100%, and Save is disabled while the set is invalid.
// An empty set is valid and saves an *unmeasured* touchpoint (FR-008). When NPS is among the
// selected types a non-blocking informational banner appears (mirrors the server `npsWarning`).
//
// Reusable: pass `kpiTypes` from a parent that renders many editors (T056 KPI scoring page) to
// avoid one `GET /kpi-types` per touchpoint; omit it and the editor fetches the catalog itself.
// RTL-first, design-system styled (logical properties, D-scale tokens for the validity signal).

import { useCallback, useEffect, useState } from "react"
import { useTranslation } from "react-i18next"
import { Info, Loader2, Plus, Trash2 } from "lucide-react"

import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Skeleton } from "@/components/ui/skeleton"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { cn } from "@/lib/utils"
import {
  getKpiTypes,
  saveKpiBindings,
  JourneysApiError,
  type KpiBinding,
  type KpiType,
  type SaveKpiBindingsResponse,
} from "@/features/journeys/api"

/** Platform-standard NPS type key — its presence drives the non-blocking info banner. */
const NPS_KEY = "NPS"

// A single editable binding row. `id` is a stable client key (types can repeat transiently while
// the user is mid-selection, so rows are never keyed on `kpiType`). `weight` is held as a string
// so the number input can be cleared while editing; it's parsed for the sum and validation.
interface Row {
  id: string
  kpiType: string
  weight: string
}

let rowSeq = 0
function newRowId(): string {
  rowSeq += 1
  return `kpi-row-${rowSeq}`
}

function toRows(bindings: KpiBinding[]): Row[] {
  return bindings.map((b) => ({ id: newRowId(), kpiType: b.kpiType, weight: String(b.weight) }))
}

export interface KpiWeightEditorProps {
  /** Touchpoint whose bindings are edited (`PUT /api/v1/touchpoints/{id}/kpis`). */
  touchpointId: string
  /** Existing bindings to prefill, from the journey tree. Omitted/empty → unmeasured touchpoint. */
  initialBindings?: KpiBinding[]
  /**
   * Pre-resolved KPI type catalog. When omitted the editor fetches `GET /api/v1/kpi-types`
   * itself; pass it from a parent rendering many editors so the catalog is fetched only once.
   */
  kpiTypes?: KpiType[]
  /** Disables every control (e.g. the parent journey is Archived → read-only). */
  disabled?: boolean
  /** Called after a successful save with the server response (carries `npsWarning`). */
  onSaved?: (result: SaveKpiBindingsResponse) => void
}

export function KpiWeightEditor({
  touchpointId,
  initialBindings,
  kpiTypes: kpiTypesProp,
  disabled = false,
  onSaved,
}: KpiWeightEditorProps) {
  const { t, i18n } = useTranslation()
  const isArabic = i18n.language === "ar"

  const [fetchedTypes, setFetchedTypes] = useState<KpiType[]>([])
  const [loading, setLoading] = useState(!kpiTypesProp)
  const [loadError, setLoadError] = useState(false)
  const [rows, setRows] = useState<Row[]>(() => toRows(initialBindings ?? []))
  const [formError, setFormError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const kpiTypes = kpiTypesProp ?? fetchedTypes

  // Fetch the catalog only when the parent didn't supply it. When `kpiTypesProp` is provided it
  // stays the stable source and this effect early-returns (no fetch, no state writes → no loop).
  const loadTypes = useCallback(() => {
    let cancelled = false
    setLoading(true)
    setLoadError(false)
    getKpiTypes()
      .then((types) => {
        if (!cancelled) setFetchedTypes(types)
      })
      .catch(() => {
        if (!cancelled) setLoadError(true)
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [])

  useEffect(() => {
    if (kpiTypesProp) return
    return loadTypes()
  }, [kpiTypesProp, loadTypes])

  // ── Derived validation state ──────────────────────────────────────────────
  const total = rows.reduce((sum, r) => {
    const w = Number(r.weight)
    return sum + (Number.isFinite(w) ? w : 0)
  }, 0)
  const hasRows = rows.length > 0
  const sumValid = total === 100
  const everyWeightValid = rows.every((r) => {
    const w = Number(r.weight)
    return r.weight.trim() !== "" && Number.isInteger(w) && w >= 1 && w <= 100
  })
  const everyTypeChosen = rows.every((r) => r.kpiType !== "")
  // Per-row pickers exclude types used by other rows, so duplicates can't form — assert anyway.
  const noDuplicates = new Set(rows.map((r) => r.kpiType)).size === rows.length
  // Empty set = unmeasured = valid. A non-empty set must be fully valid AND sum to exactly 100.
  const canSave =
    !disabled &&
    !saving &&
    !loading &&
    !loadError &&
    (!hasRows || (everyWeightValid && everyTypeChosen && noDuplicates && sumValid))

  const npsSelected = rows.some((r) => r.kpiType === NPS_KEY)
  const canAddRow = !disabled && !saving && kpiTypes.length > rows.length

  // ── Row mutations ───────────────────────────────────────────────────────────
  function availableTypesFor(rowId: string): KpiType[] {
    const usedByOthers = new Set(rows.filter((r) => r.id !== rowId).map((r) => r.kpiType))
    return kpiTypes.filter((kt) => !usedByOthers.has(kt.typeKey))
  }

  function addRow() {
    const used = new Set(rows.map((r) => r.kpiType))
    const next = kpiTypes.find((kt) => !used.has(kt.typeKey))
    setFormError(null)
    setRows((prev) => [...prev, { id: newRowId(), kpiType: next?.typeKey ?? "", weight: "" }])
  }

  function setRowType(id: string, kpiType: string) {
    setRows((prev) => prev.map((r) => (r.id === id ? { ...r, kpiType } : r)))
  }

  function setRowWeight(id: string, weight: string) {
    // Keep digits only; the input is integer-percentage.
    const cleaned = weight.replace(/[^\d]/g, "")
    setRows((prev) => prev.map((r) => (r.id === id ? { ...r, weight: cleaned } : r)))
  }

  function removeRow(id: string) {
    setFormError(null)
    setRows((prev) => prev.filter((r) => r.id !== id))
  }

  function labelFor(kt: KpiType): string {
    return isArabic ? kt.labelAr : kt.labelEn
  }

  function messageForError(err: unknown): string {
    if (err instanceof JourneysApiError) {
      if (err.status === 403) return t("journey.archivedImmutable")
      switch (err.code) {
        case "kpi.weight_sum_invalid":
          return t("journey.kpiWeightsSum")
        case "kpi.duplicate_type":
          return t("journey.kpiDuplicateType")
        case "kpi.individual_weight_invalid":
          return t("journey.kpiWeightInvalid")
        case "kpi.unknown_type":
          return t("journey.kpiUnknownType")
      }
    }
    return t("journey.kpiSaveFailed")
  }

  async function handleSave() {
    if (!canSave) return
    setFormError(null)
    setSaving(true)
    try {
      const result = await saveKpiBindings(touchpointId, {
        kpiBindings: rows.map((r) => ({ kpiType: r.kpiType, weight: Number(r.weight) })),
      })
      // Re-sync to the server's authoritative set (covers ordering / persisted values).
      setRows(
        result.kpiBindings.map((b) => ({
          id: newRowId(),
          kpiType: b.kpiType,
          weight: String(b.weight),
        })),
      )
      onSaved?.(result)
    } catch (err) {
      setFormError(messageForError(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="space-y-4">
      {/* Header: title + Add KPI */}
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="text-base font-bold">{t("journey.kpiConfiguration")}</h3>
          <p className="text-sm text-muted-foreground">{t("journey.kpiEditorHint")}</p>
        </div>
        <Button type="button" variant="secondary" size="compact" onClick={addRow} disabled={!canAddRow}>
          <Plus className="size-4" aria-hidden />
          {t("journey.addKpi")}
        </Button>
      </div>

      {/* Catalog load failure */}
      {loadError && (
        <div
          role="alert"
          className="flex items-center justify-between gap-3 rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          <span>{t("journey.kpiTypesLoadError")}</span>
          <Button type="button" variant="secondary" size="compact" onClick={() => loadTypes()}>
            {t("journey.retry")}
          </Button>
        </div>
      )}

      {/* Loading skeleton */}
      {loading && !loadError && (
        <div className="space-y-2" aria-hidden>
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
        </div>
      )}

      {/* Save failure */}
      {formError && (
        <div
          role="alert"
          className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {formError}
        </div>
      )}

      {/* Empty (unmeasured) state */}
      {!loading && !loadError && !hasRows && (
        <div className="rounded-md border border-dashed border-border bg-muted/30 px-4 py-6 text-center">
          <p className="text-sm font-medium text-foreground">{t("journey.noKpisConfigured")}</p>
          <p className="mt-1 text-sm text-muted-foreground">{t("journey.noKpisHelp")}</p>
        </div>
      )}

      {/* Binding rows */}
      {!loading && !loadError && hasRows && (
        <div className="space-y-2">
          {rows.map((row) => {
            const options = availableTypesFor(row.id)
            const w = Number(row.weight)
            const weightInvalid =
              row.weight.trim() !== "" && (!Number.isInteger(w) || w < 1 || w > 100)
            return (
              <div key={row.id} className="flex items-end gap-2">
                <div className="flex-1 flex flex-col gap-1.5">
                  <Label htmlFor={`${row.id}-type`} className="sr-only">
                    {t("journey.kpiTypeLabel")}
                  </Label>
                  <Select
                    value={row.kpiType}
                    onValueChange={(v) => setRowType(row.id, v ?? "")}
                    disabled={disabled || saving}
                  >
                    <SelectTrigger id={`${row.id}-type`} className="w-full">
                      <SelectValue placeholder={t("journey.kpiTypePlaceholder")} />
                    </SelectTrigger>
                    <SelectContent>
                      {options.map((kt) => (
                        <SelectItem key={kt.typeKey} value={kt.typeKey}>
                          <span>{labelFor(kt)}</span>
                          <span className="ms-1.5 text-muted-foreground" dir="ltr">
                            {kt.typeKey}
                          </span>
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="w-24 space-y-1.5">
                  <Label htmlFor={`${row.id}-weight`} className="sr-only">
                    {t("journey.kpiWeight")}
                  </Label>
                  <Input
                    id={`${row.id}-weight`}
                    type="number"
                    inputMode="numeric"
                    min={1}
                    max={100}
                    step={1}
                    value={row.weight}
                    onChange={(e) => setRowWeight(row.id, e.target.value)}
                    disabled={disabled || saving}
                    aria-invalid={weightInvalid}
                    aria-label={t("journey.kpiWeight")}
                    className="text-end tabular-nums"
                  />
                </div>
                <span className="pb-2.5 text-sm text-muted-foreground" aria-hidden>
                  %
                </span>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  onClick={() => removeRow(row.id)}
                  disabled={disabled || saving}
                  aria-label={t("journey.kpiRemove")}
                  className="text-muted-foreground hover:text-destructive"
                >
                  <Trash2 className="size-4" aria-hidden />
                </Button>
              </div>
            )
          })}
        </div>
      )}

      {/* Non-blocking NPS info banner (mirrors the server `npsWarning`) */}
      {npsSelected && (
        <div
          role="note"
          className="flex items-start gap-2 rounded-md border border-nb-cyan-200 bg-nb-cyan-100 px-3 py-2 text-sm text-nb-cyan-800 dark:border-nb-cyan-800/40 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200"
        >
          <Info className="mt-0.5 size-4 shrink-0" aria-hidden />
          <span>{t("journey.npsInfoBanner")}</span>
        </div>
      )}

      {/* Live sum indicator + Save */}
      {!loading && !loadError && (
        <div className="flex items-center justify-between gap-3 border-t border-border pt-3">
          <div className="text-sm">
            {hasRows ? (
              <>
                <span
                  className={cn(
                    "font-semibold tabular-nums",
                    sumValid ? "text-d2-dark dark:text-d2-light" : "text-destructive",
                  )}
                >
                  {t("journey.kpiWeightsCurrent", { total })}
                </span>
                {!sumValid && (
                  <span className="ms-2 text-muted-foreground">{t("journey.kpiWeightsSum")}</span>
                )}
              </>
            ) : (
              <span className="text-muted-foreground">{t("journey.unmeasured")}</span>
            )}
          </div>
          <Button
            type="button"
            onClick={handleSave}
            disabled={!canSave}
            className="bg-primary text-primary-foreground hover:bg-nb-cyan-700"
          >
            {saving && <Loader2 className="size-4 animate-spin" aria-hidden />}
            {t("journey.saveChanges")}
          </Button>
        </div>
      )}
    </div>
  )
}
