// Detection threshold editor (US-4, T093): edits one journey's pain/happy detection configuration
// (`GET`/`PUT /api/v1/journeys/{id}/detection`). Three parts in one Save:
//   1. Journey-level thresholds — a `painThreshold` (score ≤ this = pain point) and a
//      `happyThreshold` (score ≥ this = happy moment), both integers in [0, 100] with inline
//      validation that pain MUST be strictly below happy (FR-007).
//   2. Per-stage overrides — each stage can switch on an override; a blank field there inherits the
//      journey-level value (`null` on the wire), the most specific value wins. Effective pain<happy
//      is validated against the inherited journey values so an impossible override can't be saved.
//   3. Unmeasured-touchpoint callout — touchpoints with no KPI bindings are excluded from detection
//      (FR-010); the editor surfaces which ones so the author knows they won't be evaluated.
//
// Self-contained: it fetches its own detection config (404 `journey.no_detection_config` → defaults)
// and saves the whole set in one `PUT`. The journey tree (`stages`) is passed in by the parent page
// (T094) — it drives the override rows and the unmeasured callout. RTL-first, design-system styled
// (logical properties, D-scale tokens for the validity / unmeasured signals, brand cyan for chrome).

import { useCallback, useEffect, useMemo, useState } from "react"
import { useTranslation } from "react-i18next"
import { AlertTriangle, CircleCheck, Loader2, Target } from "lucide-react"

import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Skeleton } from "@/components/ui/skeleton"
import { Switch } from "@/components/ui/switch"
import { cn } from "@/lib/utils"
import {
  getDetection,
  JourneysApiError,
  saveDetection,
  type SaveDetectionResponse,
  type StageDetail,
} from "@/features/journeys/api"

/** Per-stage override draft state. `enabled` off → no override sent; a blank field → inherit (null). */
interface OverrideDraft {
  enabled: boolean
  pain: string
  happy: string
}

export interface DetectionThresholdEditorProps {
  /** Journey whose detection config is edited (`PUT /api/v1/journeys/{id}/detection`). */
  journeyId: string
  /** The journey's stages (with touchpoints) — drives the override rows and the unmeasured callout. */
  stages: StageDetail[]
  /** Disables every control (e.g. the parent journey is Archived → read-only). */
  disabled?: boolean
  /** Called after a successful save with the server response (carries the persisted counts). */
  onSaved?: (result: SaveDetectionResponse) => void
}

/** Whole 0–100 integer — the valid range for any detection threshold. */
function inRange(n: number): boolean {
  return Number.isInteger(n) && n >= 0 && n <= 100
}

/** Keeps digits only; thresholds are whole 0–100 values. */
function cleanThreshold(value: string): string {
  return value.replace(/[^\d]/g, "")
}

export function DetectionThresholdEditor({
  journeyId,
  stages,
  disabled = false,
  onSaved,
}: DetectionThresholdEditorProps) {
  const { t } = useTranslation()

  const [pain, setPain] = useState("")
  const [happy, setHappy] = useState("")
  const [overrides, setOverrides] = useState<Record<string, OverrideDraft>>({})
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(false)
  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)
  const [savedAt, setSavedAt] = useState<string | null>(null)

  // Initial load: the detection config GET 404s with `journey.no_detection_config` when none was
  // saved — that is the "use defaults" path (blank thresholds), not an error.
  const load = useCallback(() => {
    if (!journeyId) return
    let cancelled = false
    setLoading(true)
    setLoadError(false)
    setFormError(null)

    const blankDrafts = (): Record<string, OverrideDraft> =>
      Object.fromEntries(stages.map((s) => [s.stageId, { enabled: false, pain: "", happy: "" }]))

    getDetection(journeyId)
      .then((cfg) => {
        if (cancelled) return
        setPain(String(cfg.painThreshold))
        setHappy(String(cfg.happyThreshold))
        const byStage = new Map(cfg.stageOverrides.map((o) => [o.stageId, o]))
        setOverrides(
          Object.fromEntries(
            stages.map((s) => {
              const o = byStage.get(s.stageId)
              return [
                s.stageId,
                o
                  ? {
                      enabled: true,
                      pain: o.painThreshold == null ? "" : String(o.painThreshold),
                      happy: o.happyThreshold == null ? "" : String(o.happyThreshold),
                    }
                  : { enabled: false, pain: "", happy: "" },
              ]
            }),
          ),
        )
        setSavedAt(cfg.updatedAt)
      })
      .catch((err) => {
        if (cancelled) return
        if (err instanceof JourneysApiError && err.status === 404) {
          // No detection config yet — start blank, the author sets the thresholds.
          setPain("")
          setHappy("")
          setOverrides(blankDrafts())
          setSavedAt(null)
        } else {
          setLoadError(true)
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => {
      cancelled = true
    }
    // `stages` is stable for the lifetime of a page load (the parent fetches the tree once).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [journeyId])

  useEffect(() => load(), [load])

  // ── Journey-level validation ──────────────────────────────────────────────────
  const painProvided = pain.trim() !== ""
  const happyProvided = happy.trim() !== ""
  const painNum = Number(pain)
  const happyNum = Number(happy)
  const painRangeOk = painProvided && inRange(painNum)
  const happyRangeOk = happyProvided && inRange(happyNum)
  const journeyOutOfRange = (painProvided && !painRangeOk) || (happyProvided && !happyRangeOk)
  const journeyPainBelowHappy = painRangeOk && happyRangeOk && painNum < happyNum
  const journeyValid = painRangeOk && happyRangeOk && journeyPainBelowHappy

  const journeyError = journeyOutOfRange
    ? t("journey.detectionOutOfRange")
    : painRangeOk && happyRangeOk && !journeyPainBelowHappy
      ? t("journey.detectionThresholdInvalid")
      : null

  // ── Per-stage override validation (effective pain<happy against inherited journey values) ──
  const enabledStageIds = useMemo(
    () => stages.filter((s) => overrides[s.stageId]?.enabled).map((s) => s.stageId),
    [stages, overrides],
  )

  function overrideError(stageId: string): string | null {
    const o = overrides[stageId]
    if (!o?.enabled) return null
    const oPainProvided = o.pain.trim() !== ""
    const oHappyProvided = o.happy.trim() !== ""
    const oPain = Number(o.pain)
    const oHappy = Number(o.happy)
    if ((oPainProvided && !inRange(oPain)) || (oHappyProvided && !inRange(oHappy)))
      return t("journey.detectionOutOfRange")
    // Effective value = the override's own value, or the inherited journey-level value.
    const effPain = oPainProvided ? oPain : painRangeOk ? painNum : NaN
    const effHappy = oHappyProvided ? oHappy : happyRangeOk ? happyNum : NaN
    if (Number.isFinite(effPain) && Number.isFinite(effHappy) && effPain >= effHappy)
      return t("journey.detectionThresholdInvalid")
    return null
  }

  const overridesValid = enabledStageIds.every((id) => overrideError(id) === null)
  const canSave = !disabled && !saving && !loading && !loadError && journeyValid && overridesValid

  // ── Unmeasured touchpoints (no KPI bindings → excluded from detection, FR-010) ──
  const unmeasured = useMemo(
    () =>
      stages.flatMap((s) =>
        s.touchpoints.filter((tp) => !tp.isMeasured).map((tp) => ({ stage: s, touchpoint: tp })),
      ),
    [stages],
  )

  // ── Mutations ───────────────────────────────────────────────────────────────────
  function setOverride(stageId: string, patch: Partial<OverrideDraft>) {
    setFormError(null)
    setOverrides((prev) => ({
      ...prev,
      [stageId]: { ...(prev[stageId] ?? { enabled: false, pain: "", happy: "" }), ...patch },
    }))
  }

  function messageForError(err: unknown): string {
    if (err instanceof JourneysApiError) {
      if (err.status === 403) return t("journey.archivedImmutable")
      switch (err.code) {
        case "detection.threshold_invalid":
          return t("journey.detectionThresholdInvalid")
        case "detection.out_of_range":
          return t("journey.detectionOutOfRange")
        case "detection.unknown_stage":
          return t("journey.detectionUnknownStage")
        case "detection.unknown_touchpoint":
          return t("journey.detectionUnknownTouchpoint")
      }
    }
    return t("journey.detectionSaveFailed")
  }

  async function handleSave() {
    if (!canSave) return
    setFormError(null)
    setSaving(true)
    try {
      const result = await saveDetection(journeyId, {
        painThreshold: painNum,
        happyThreshold: happyNum,
        stageOverrides: enabledStageIds.map((stageId) => {
          const o = overrides[stageId]
          return {
            stageId,
            painThreshold: o.pain.trim() === "" ? null : Number(o.pain),
            happyThreshold: o.happy.trim() === "" ? null : Number(o.happy),
          }
        }),
        touchpointOverrides: [],
      })
      setSavedAt(result.updatedAt)
      onSaved?.(result)
    } catch (err) {
      setFormError(messageForError(err))
    } finally {
      setSaving(false)
    }
  }

  // ── Render ────────────────────────────────────────────────────────────────────
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base font-bold">
          <Target className="size-5 shrink-0 text-primary" aria-hidden />
          {t("journey.detectionEditorTitle")}
        </CardTitle>
        <CardDescription>{t("journey.detectionEditorHint")}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-6">
        {/* Catalog / config load failure */}
        {loadError && (
          <div
            role="alert"
            className="flex items-center justify-between gap-3 rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
          >
            <span>{t("journey.detectionLoadError")}</span>
            <Button type="button" variant="secondary" size="compact" onClick={() => load()}>
              {t("journey.retry")}
            </Button>
          </div>
        )}

        {/* Loading skeleton */}
        {loading && !loadError && (
          <div className="space-y-4" aria-hidden>
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-24 w-full" />
          </div>
        )}

        {!loading && !loadError && (
          <>
            {/* Journey-level thresholds */}
            <section className="space-y-3">
              <h3 className="text-sm font-bold">{t("journey.detectionJourneyLevel")}</h3>
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div className="space-y-1.5">
                  <Label htmlFor="detection-pain">{t("journey.painThreshold")}</Label>
                  <Input
                    id="detection-pain"
                    type="number"
                    inputMode="numeric"
                    min={0}
                    max={100}
                    step={1}
                    value={pain}
                    onChange={(e) => {
                      setFormError(null)
                      setPain(cleanThreshold(e.target.value))
                    }}
                    disabled={disabled || saving}
                    aria-invalid={painProvided && !painRangeOk}
                    className="text-end tabular-nums"
                  />
                  <p className="text-sm text-muted-foreground">{t("journey.painThresholdHint")}</p>
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="detection-happy">{t("journey.happyThreshold")}</Label>
                  <Input
                    id="detection-happy"
                    type="number"
                    inputMode="numeric"
                    min={0}
                    max={100}
                    step={1}
                    value={happy}
                    onChange={(e) => {
                      setFormError(null)
                      setHappy(cleanThreshold(e.target.value))
                    }}
                    disabled={disabled || saving}
                    aria-invalid={happyProvided && !happyRangeOk}
                    className="text-end tabular-nums"
                  />
                  <p className="text-sm text-muted-foreground">{t("journey.happyThresholdHint")}</p>
                </div>
              </div>
              {journeyError && (
                <p className="text-sm text-destructive" role="alert">
                  {journeyError}
                </p>
              )}
            </section>

            {/* Per-stage overrides */}
            <section className="space-y-3 border-t border-border pt-6">
              <div>
                <h3 className="text-sm font-bold">{t("journey.detectionStageOverrides")}</h3>
                <p className="text-sm text-muted-foreground">
                  {t("journey.detectionStageOverridesHint")}
                </p>
              </div>

              {stages.length === 0 ? (
                <p className="rounded-md border border-dashed border-border bg-muted/30 px-4 py-6 text-center text-sm text-muted-foreground">
                  {t("journey.detectionNoStages")}
                </p>
              ) : (
                <div className="space-y-2">
                  {stages.map((stage) => {
                    const draft = overrides[stage.stageId] ?? {
                      enabled: false,
                      pain: "",
                      happy: "",
                    }
                    const err = overrideError(stage.stageId)
                    const inheritPain = painProvided ? pain : t("journey.detectionInherit")
                    const inheritHappy = happyProvided ? happy : t("journey.detectionInherit")
                    return (
                      <div
                        key={stage.stageId}
                        className="rounded-md border border-border bg-card p-3"
                      >
                        <div className="flex items-center justify-between gap-3">
                          <div className="flex min-w-0 items-center gap-2">
                            <span className="shrink-0 text-xs font-bold tabular-nums text-muted-foreground">
                              {stage.sequenceNumber}.0
                            </span>
                            <span className="truncate text-sm font-semibold">{stage.name}</span>
                          </div>
                          <div className="flex shrink-0 items-center gap-2">
                            <Label
                              htmlFor={`override-${stage.stageId}`}
                              className="text-xs font-medium text-muted-foreground"
                            >
                              {t("journey.detectionOverride")}
                            </Label>
                            <Switch
                              id={`override-${stage.stageId}`}
                              checked={draft.enabled}
                              onCheckedChange={(v) => setOverride(stage.stageId, { enabled: v })}
                              disabled={disabled || saving}
                              aria-label={t("journey.detectionOverrideAria", { stage: stage.name })}
                            />
                          </div>
                        </div>

                        {/* Animated expand/collapse of the per-stage threshold fields. */}
                        <div
                          className={cn(
                            "grid transition-[grid-template-rows,opacity] duration-300 ease-[cubic-bezier(0.16,1,0.3,1)]",
                            draft.enabled ? "grid-rows-[1fr] opacity-100" : "grid-rows-[0fr] opacity-0",
                          )}
                        >
                          <div className="overflow-hidden">
                            <div className="mt-3 grid grid-cols-1 gap-3 border-t border-border pt-3 sm:grid-cols-2">
                              <div className="space-y-1.5">
                                <Label
                                  htmlFor={`override-${stage.stageId}-pain`}
                                  className="text-xs text-muted-foreground"
                                >
                                  {t("journey.painThreshold")}
                                </Label>
                                <Input
                                  id={`override-${stage.stageId}-pain`}
                                  type="number"
                                  inputMode="numeric"
                                  min={0}
                                  max={100}
                                  step={1}
                                  value={draft.pain}
                                  onChange={(e) =>
                                    setOverride(stage.stageId, { pain: cleanThreshold(e.target.value) })
                                  }
                                  disabled={disabled || saving || !draft.enabled}
                                  placeholder={inheritPain}
                                  aria-label={t("journey.painThreshold")}
                                  className="text-end tabular-nums"
                                />
                              </div>
                              <div className="space-y-1.5">
                                <Label
                                  htmlFor={`override-${stage.stageId}-happy`}
                                  className="text-xs text-muted-foreground"
                                >
                                  {t("journey.happyThreshold")}
                                </Label>
                                <Input
                                  id={`override-${stage.stageId}-happy`}
                                  type="number"
                                  inputMode="numeric"
                                  min={0}
                                  max={100}
                                  step={1}
                                  value={draft.happy}
                                  onChange={(e) =>
                                    setOverride(stage.stageId, { happy: cleanThreshold(e.target.value) })
                                  }
                                  disabled={disabled || saving || !draft.enabled}
                                  placeholder={inheritHappy}
                                  aria-label={t("journey.happyThreshold")}
                                  className="text-end tabular-nums"
                                />
                              </div>
                            </div>
                            {err && (
                              <p className="mt-2 text-sm text-destructive" role="alert">
                                {err}
                              </p>
                            )}
                            <p className="mt-2 text-xs text-muted-foreground">
                              {t("journey.detectionInheritHint")}
                            </p>
                          </div>
                        </div>
                      </div>
                    )
                  })}
                </div>
              )}
            </section>

            {/* Unmeasured touchpoints — excluded from detection (FR-010) */}
            {unmeasured.length > 0 && (
              <div
                role="note"
                className="rounded-md border border-d3-dark/20 bg-d3-light px-4 py-3 dark:border-d3-light/20 dark:bg-d3-dark/25"
              >
                <div className="flex items-start gap-2">
                  <AlertTriangle
                    className="mt-0.5 size-4 shrink-0 text-d3-dark dark:text-d3-light"
                    aria-hidden
                  />
                  <div className="min-w-0 space-y-1.5">
                    <p className="text-sm font-semibold text-foreground">
                      {t("journey.detectionUnmeasuredTitle", { n: unmeasured.length })}
                    </p>
                    <p className="text-sm text-foreground/80">
                      {t("journey.detectionUnmeasuredBody")}
                    </p>
                    <ul className="flex flex-wrap gap-1.5 pt-0.5">
                      {unmeasured.map(({ stage, touchpoint }) => (
                        <li
                          key={touchpoint.touchpointId}
                          className="inline-flex items-center gap-1 rounded-sm bg-card px-2 py-0.5 text-xs font-medium text-foreground"
                        >
                          <span className="tabular-nums text-muted-foreground">
                            {stage.sequenceNumber}.0
                          </span>
                          <span className="truncate">{touchpoint.name}</span>
                        </li>
                      ))}
                    </ul>
                  </div>
                </div>
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

            {/* Footer: saved status + single Save (persists thresholds + overrides together) */}
            <div className="flex items-center justify-between gap-3 border-t border-border pt-4">
              <span className="text-sm text-muted-foreground">
                {savedAt ? (
                  <span className="inline-flex items-center gap-1.5 text-d2-dark dark:text-d2-light">
                    <CircleCheck className="size-4" aria-hidden />
                    {t("journey.detectionSaved")}
                  </span>
                ) : (
                  t("journey.detectionNotSaved")
                )}
              </span>
              <Button
                type="button"
                onClick={() => void handleSave()}
                disabled={!canSave}
                className="bg-primary text-primary-foreground hover:bg-nb-cyan-700"
              >
                {saving && <Loader2 className="size-4 animate-spin" aria-hidden />}
                {t("journey.detectionSaveButton")}
              </Button>
            </div>
          </>
        )}
      </CardContent>
    </Card>
  )
}
