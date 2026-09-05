// T069 [US2] — KPI configuration page. Two-panel layout (form ~55% / live preview ~45%; stacks on
// tablet). Header: title ("New KPI" / "KPI Configuration" + Full Name subtitle), a back arrow with
// an unsaved-changes guard, and the Save / Cancel actions (one-blue-rule: Save is the single filled
// primary). The preview panel stacks the question preview and a dashboard gauge preview, both
// subscribed to the form state via KpiFormContext. Read-only for the P-02 Analyst (FR-009).

import { useMemo, useRef, useState, type ReactNode } from "react"
import { useNavigate, useParams } from "react-router"
import { useTranslation } from "react-i18next"
import { toast } from "sonner"
import { AlertTriangle, ArrowLeft, ArrowRight, Loader2, TrendingDown, TrendingUp } from "lucide-react"

import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import { useDirection } from "@/hooks/use-direction"
import { useSession } from "@/features/auth/hooks/useSession"
import { UniversalArcGauge } from "@/components/cx/kpi/UniversalArcGauge"
import { KpiQuestionPreview } from "@/components/cx/kpi/KpiQuestionPreview"
import type { RepresentationStyleKey } from "@/components/cx/kpi/KpiQuestionPreview"
import type { EmojiSetKey, ScaleKey } from "@/components/cx/kpi/kpi-scale-meta"
import { useKpiDetail } from "@/features/kpi-management/hooks/useKpiDetail"
import { useKpiSave } from "@/features/kpi-management/hooks/useKpiSave"
import { updateCxiWeights, type CxiWeightItem, type CxiWeightUpdateItem } from "@/features/kpi-management/api"
import { KpiApiError } from "@/features/kpi-management/kpi-api-error"
import {
  CxiWeightsTable,
  type CxiWeightsSummary,
} from "@/features/kpi-management/components/CxiWeightsTable"
import {
  KpiConfigForm,
  KpiFormProvider,
  buildSaveInput,
  isNpsState,
  useKpiForm,
  type KpiFormState,
} from "@/features/kpi-management/components/KpiConfigForm"

// Brand chart series (Two-Palette Rule: decorative weight bars use chart-* tokens, never D1–D5).
const CXI_SERIES_BG = ["bg-chart-1", "bg-chart-2", "bg-chart-3", "bg-chart-4", "bg-chart-5"]

export default function KpiConfigPage() {
  // The edit URL carries the KPI's Short Name (e.g. /kpi-management/cxi), not its GUID. The detail
  // fetch resolves the Short Name server-side; the GUID needed for the PUT comes from the loaded KPI.
  const { shortName } = useParams()
  const mode = shortName ? "edit" : "create"
  const { kpi, loading, error } = useKpiDetail(shortName)
  const { session } = useSession()
  const { t } = useTranslation()
  const readOnly = session?.persona === "P-02"

  if (loading) {
    return (
      <div className="space-y-5 py-5">
        <Skeleton className="h-9 w-64" />
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-[1.2fr_1fr]">
          <Skeleton className="h-[600px] w-full" />
          <Skeleton className="h-[600px] w-full" />
        </div>
      </div>
    )
  }

  if (error || (mode === "edit" && !kpi)) {
    return (
      <div className="space-y-4 py-5">
        <p
          role="alert"
          className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {t("kpi.detailError")}
        </p>
      </div>
    )
  }

  return (
    <KpiFormProvider detail={kpi} mode={mode} readOnly={readOnly}>
      <KpiConfigPageInner
        mode={mode}
        kpiId={kpi?.id ?? null}
        readOnly={readOnly}
        subtitle={kpi?.fullName}
        isComposite={kpi?.isComposite ?? false}
        initialCxiWeights={kpi?.cxiWeights ?? []}
      />
    </KpiFormProvider>
  )
}

function KpiConfigPageInner({
  mode,
  kpiId,
  readOnly,
  subtitle,
  isComposite,
  initialCxiWeights,
}: {
  mode: "create" | "edit"
  kpiId: string | null
  readOnly: boolean
  subtitle?: string
  isComposite: boolean
  initialCxiWeights: CxiWeightItem[]
}) {
  const { t } = useTranslation()
  const { isRtl } = useDirection()
  const navigate = useNavigate()
  const { state, isValid, patch } = useKpiForm()
  const [cxiLegend, setCxiLegend] = useState<CxiWeightsSummary["legend"]>(() =>
    initialCxiWeights
      .filter((w) => w.weight > 0)
      .map((w) => ({
        memberKpiId: w.memberKpiId,
        memberShortName: w.memberShortName,
        weight: w.weight,
        effectivePercentage: w.effectivePercentage,
      })),
  )

  // Latest editable weights payload, lifted from the table so the main Save persists it (FR-046).
  const [cxiWeights, setCxiWeights] = useState<CxiWeightUpdateItem[]>(() =>
    initialCxiWeights.filter((w) => w.weight > 0).map((w) => ({ memberKpiId: w.memberKpiId, weight: w.weight })),
  )

  const handleCxiChange = (summary: CxiWeightsSummary) => {
    setCxiLegend(summary.legend)
    setCxiWeights(summary.weights)
    patch({ cxiActiveMemberCount: summary.positiveCount })
  }
  const { save, saving } = useKpiSave()
  const [cxiSaving, setCxiSaving] = useState(false)
  const [needsConfirm, setNeedsConfirm] = useState(false)
  const [confirmLeaveOpen, setConfirmLeaveOpen] = useState(false)
  const [saveError, setSaveError] = useState<{ code: string; message: string } | null>(null)

  // Unsaved-changes guard: snapshot the initial serialised input once.
  const initialSnapshot = useRef(JSON.stringify(buildSaveInput(state)))
  const isDirty = JSON.stringify(buildSaveInput(state)) !== initialSnapshot.current

  const BackIcon = isRtl ? ArrowRight : ArrowLeft

  // Leaving with pending edits opens the confirmation dialog; a clean form navigates straight away.
  const leave = () => {
    if (isDirty) {
      setConfirmLeaveOpen(true)
      return
    }
    navigate("/kpi-management")
  }

  const runSave = async (confirmStructuralChange: boolean) => {
    setSaveError(null)
    const outcome = await save(mode, kpiId, buildSaveInput(state), confirmStructuralChange)
    if (outcome.status === "needs-structural-confirm") {
      setNeedsConfirm(true)
      return
    }
    if (outcome.status !== "saved") {
      setSaveError({ code: outcome.error.code, message: outcome.error.message })
      toast.error(outcome.error.message)
      return
    }

    // The CXI member weights save as part of the same Save action — a second full-replace call after
    // the KPI config write. On failure the page stays put and surfaces the CXI error code.
    if (isComposite && kpiId) {
      setCxiSaving(true)
      try {
        await updateCxiWeights(kpiId, cxiWeights)
      } catch (e) {
        const err =
          e instanceof KpiApiError ? e : new KpiApiError(0, { error: { code: "network_error", message: String(e) } })
        setSaveError({ code: err.code, message: err.message })
        toast.error(err.message)
        return
      } finally {
        setCxiSaving(false)
      }
    }

    toast.success(mode === "create" ? t("kpi.createdToast") : t("kpi.updatedToast"))
    navigate("/kpi-management")
  }

  return (
    <div className="space-y-5 py-5">
      {/* Header */}
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="icon" onClick={leave} aria-label={t("common.back")} data-testid="kpi-back">
            <BackIcon className="size-5" aria-hidden />
          </Button>
          <div>
            <h1 className="text-2xl font-heading font-bold">
              {mode === "create" ? t("kpi.newKpi") : t("kpi.configTitle")}
            </h1>
            {mode === "edit" && subtitle && (
              <p className="mt-1 text-sm text-muted-foreground">{subtitle}</p>
            )}
          </div>
        </div>
        {!readOnly && (
          <div className="flex items-center gap-2">
            <Button variant="outline" onClick={leave} disabled={saving || cxiSaving} data-testid="kpi-cancel">
              {t("common.cancel")}
            </Button>
            <Button
              onClick={() => runSave(false)}
              disabled={!isValid || saving || cxiSaving}
              data-testid="kpi-save"
              className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            >
              {(saving || cxiSaving) && <Loader2 className="size-4 ms-2 animate-spin" aria-hidden />}
              {saving || cxiSaving ? t("common.saving") : t("common.save")}
            </Button>
          </div>
        )}
      </div>

      {readOnly && (
        <p
          data-testid="kpi-readonly-notice"
          className="rounded-md border border-border bg-muted/40 px-3 py-2 text-sm text-muted-foreground"
        >
          {t("kpi.readOnlyNotice")}
        </p>
      )}

      {/* Inline save error (e.g. duplicate Short Name) — carries the API-05 code as a stable hook. */}
      {saveError && (
        <p
          role="alert"
          data-testid="kpi-save-error"
          data-error-code={saveError.code}
          className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {saveError.message}
        </p>
      )}

      {/* Scale-change confirmation (interim inline bar; the dialog ships in US-5) */}
      {needsConfirm && (
        <div
          role="alert"
          data-testid="kpi-scale-confirm"
          className="flex flex-col gap-3 rounded-md border border-d3/40 bg-d3-light/60 px-4 py-3 text-sm sm:flex-row sm:items-center sm:justify-between dark:bg-d3-dark/25"
        >
          <span className="flex items-center gap-2 text-d3-dark dark:text-d3-light">
            <AlertTriangle className="size-4 shrink-0" aria-hidden />
            {t("kpi.scaleChangeWarning")}
          </span>
          <div className="flex items-center gap-2">
            <Button variant="secondary" size="compact" onClick={() => setNeedsConfirm(false)} disabled={saving}>
              {t("common.cancel")}
            </Button>
            <Button
              size="compact"
              onClick={() => runSave(true)}
              disabled={saving}
              data-testid="kpi-scale-confirm-accept"
              className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            >
              {t("kpi.confirmStructuralChange")}
            </Button>
          </div>
        </div>
      )}

      {/* Two-panel layout */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-[1.2fr_1fr]">
        <Card>
          <CardContent>
            <KpiConfigForm />
          </CardContent>
        </Card>

        <div className="space-y-6 lg:sticky lg:top-20 lg:self-start">
          {/* Composite KPIs are computed from their members — no respondent question to preview. */}
          {!isComposite && (
            <Card data-testid="kpi-question-preview">
              <CardHeader>
                <CardTitle className="text-base">{t("kpi.questionPreview")}</CardTitle>
              </CardHeader>
              <CardContent>
                <KpiQuestionPreview
                  scale={state.scale as ScaleKey | null}
                  representationStyle={state.representationStyle as RepresentationStyleKey | null}
                  emojiSet={state.emojiSet as EmojiSetKey | null}
                  kpiName={state.fullName || state.shortName}
                  minDescription={
                    isRtl
                      ? state.minScaleDescription?.ar || state.minScaleDescription?.en
                      : state.minScaleDescription?.en || state.minScaleDescription?.ar
                  }
                  maxDescription={
                    isRtl
                      ? state.maxScaleDescription?.ar || state.maxScaleDescription?.en
                      : state.maxScaleDescription?.en || state.maxScaleDescription?.ar
                  }
                />
              </CardContent>
            </Card>
          )}

          <Card>
            <CardHeader>
              <CardTitle className="text-base">{t("kpi.dashboardPreview")}</CardTitle>
            </CardHeader>
            <CardContent>
              <DashboardPreview
                state={state}
                isComposite={isComposite}
                cxiLegend={cxiLegend}
                weightsSlot={
                  isComposite && kpiId ? (
                    // Composite KPIs configure member weights directly below the gauge (FR-046).
                    <CxiWeightsTable
                      cxiId={kpiId}
                      initialWeights={initialCxiWeights}
                      readOnly={readOnly}
                      onChange={handleCxiChange}
                    />
                  ) : null
                }
              />
            </CardContent>
          </Card>
        </div>
      </div>

      {/* Unsaved-changes confirmation when leaving the page */}
      <AlertDialog open={confirmLeaveOpen} onOpenChange={setConfirmLeaveOpen}>
        <AlertDialogContent data-testid="kpi-unsaved-dialog">
          <AlertDialogHeader>
            <AlertDialogTitle>{t("kpi.unsavedTitle")}</AlertDialogTitle>
            <AlertDialogDescription>{t("kpi.unsavedMessage")}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel data-testid="kpi-unsaved-stay">{t("kpi.unsavedStay")}</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                setConfirmLeaveOpen(false)
                navigate("/kpi-management")
              }}
              data-testid="kpi-unsaved-leave"
              className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            >
              {t("kpi.unsavedLeave")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}

function DashboardPreview({
  state,
  isComposite,
  cxiLegend,
  weightsSlot,
}: {
  state: KpiFormState
  isComposite: boolean
  cxiLegend: CxiWeightsSummary["legend"]
  /** Composite KPIs render their member-weights table here — directly below the gauge. */
  weightsSlot?: ReactNode
}) {
  const { t } = useTranslation()
  const nps = isNpsState(state)
  const { lowerBound, upperBound, x, y } = state.thresholds

  // The preview gauge has no live data, so its needle AND target marker both track the Target the
  // analyst is configuring. With no Target set (null or 0) they rest at the scale midpoint so the
  // dial reads as "neutral" — the marker must not snap to 0 when the Target is unset/zero.
  const hasTarget = state.target != null && state.target !== 0
  const gaugeValue = hasTarget ? state.target! : (lowerBound + upperBound) / 2

  // When the threshold fields are empty/invalid (not a strictly-ascending lower < x < y < upper pair —
  // e.g. cleared inputs), the preview falls back to default bands so the zones still render, and a
  // caption tells the analyst the gauge is using defaults until the fields are configured.
  const thresholdsConfigured =
    Number.isFinite(x) && Number.isFinite(y) && lowerBound < x && x < y && y < upperBound
  const gaugeX = thresholdsConfigured ? x : 20
  const gaugeY = thresholdsConfigured ? y : 70

  const sampleDelta = nps ? 3 : -2
  const sampleResponses = useMemo(() => (1248).toLocaleString("en-US"), [])

  const npsSegments = [
    { key: "promoters", label: t("kpi.promoters"), pct: 51, chip: "bg-d1-light text-d1-dark" },
    { key: "passives", label: t("kpi.passives"), pct: 40, chip: "bg-d3-light text-d3-dark" },
    { key: "detractors", label: t("kpi.detractors"), pct: 9, chip: "bg-d5-light text-d5-dark" },
  ]

  return (
    <div className="space-y-3">
      <div className="text-center">
        <p className="text-sm font-bold text-foreground">{state.shortName || t("kpi.shortNameField")}</p>
        {state.fullName && <p className="text-xs text-muted-foreground">{state.fullName}</p>}
      </div>

      <UniversalArcGauge
        value={gaugeValue}
        target={gaugeValue}
        x={gaugeX}
        y={gaugeY}
        lower={lowerBound}
        upper={upperBound}
        shortName={state.shortName || "KPI"}
        isNps={nps}
        className="mx-auto max-w-xs"
      />

      {!thresholdsConfigured && (
        <p
          className="text-center text-xs text-muted-foreground"
          data-testid="kpi-default-thresholds-notice"
        >
          {t("kpi.defaultThresholdsNotice")}
        </p>
      )}

      {state.target != null && (
        <p className="text-center text-xs text-muted-foreground">
          {t("kpi.targetCaption", { target: Math.round(state.target) })}
        </p>
      )}

      {nps && (
        <div className="grid grid-cols-3 gap-2">
          {npsSegments.map((seg) => (
            <div key={seg.key} className={cn("rounded-md px-2 py-1.5 text-center", seg.chip)}>
              <div className="text-sm font-bold tabular-nums">{seg.pct}%</div>
              <div className="text-[10px] font-medium">{seg.label}</div>
            </div>
          ))}
        </div>
      )}

      {/* Member-weights table sits directly below the gauge for composite KPIs. */}
      {weightsSlot}

      {/* Composite KPIs show how each member contributes (proportional Effective %). Brand chart
          colours — these are decorative series, never the semantic D-scale (Two-Palette Rule). */}
      {isComposite && (
        cxiLegend.length > 0 ? (
          <div className="space-y-2" data-testid="cxi-weight-legend">
            <div className="flex h-2 w-full overflow-hidden rounded-full bg-muted/40">
              {cxiLegend.map((member, i) => (
                <div
                  key={member.memberKpiId}
                  className={CXI_SERIES_BG[i % CXI_SERIES_BG.length]}
                  style={{ width: `${member.effectivePercentage}%` }}
                />
              ))}
            </div>
            <ul className="space-y-1">
              {cxiLegend.map((member, i) => (
                <li key={member.memberKpiId} className="flex items-center justify-between text-xs">
                  <span className="flex items-center gap-1.5">
                    <span
                      className={cn("size-2 rounded-full", CXI_SERIES_BG[i % CXI_SERIES_BG.length])}
                      aria-hidden
                    />
                    <span className="font-medium">{member.memberShortName}</span>
                  </span>
                  <span dir="ltr" className="tabular-nums text-muted-foreground">
                    {member.effectivePercentage.toFixed(1)}%
                  </span>
                </li>
              ))}
            </ul>
          </div>
        ) : (
          <p className="text-center text-xs text-muted-foreground">{t("kpi.cxiInsufficientMembers")}</p>
        )
      )}

      <div className="flex items-center justify-center gap-3 text-xs text-muted-foreground">
        <span className="inline-flex items-center gap-1">
          {sampleDelta >= 0 ? (
            <TrendingUp className="size-3.5 text-d2" aria-hidden />
          ) : (
            <TrendingDown className="size-3.5 text-d4" aria-hidden />
          )}
          <span dir="ltr" className="tabular-nums">
            {sampleDelta >= 0 ? "+" : ""}
            {sampleDelta}
          </span>
          {t("kpi.vsLastPeriod")}
        </span>
        <span aria-hidden>·</span>
        <span>
          <span dir="ltr" className="tabular-nums">
            {sampleResponses}
          </span>{" "}
          {t("kpi.responses")}
        </span>
      </div>
    </div>
  )
}
