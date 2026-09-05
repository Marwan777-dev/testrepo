// T068 [US2] — the KPI configuration form. A single useReducer-backed state object holds every
// field (research.md R10's < 100 ms budget: one object, one dispatch, no per-field useState storm).
// The state is published through KpiFormContext so the preview panel (KpiConfigPage) re-renders off
// the same source without prop-drilling. Cross-field invariants (Slider⇒scale 1–3, n⇒TopNBox,
// Show-on-Dashboard⇒Active, NPS threshold range) are enforced in one `normalize` pass after every
// patch. Standard-KPI field locks (Short Name always; Scale + Calc Method for NPS) are honoured.

import { createContext, useContext, useMemo, useReducer, useState, type ReactNode } from "react"
import { useTranslation } from "react-i18next"
import { toast } from "sonner"

import { getKpiBindingUsage } from "@/features/kpi-management/api"
import { BindingUsageConfirmDialog } from "./BindingUsageConfirmDialog"
import { Checkbox } from "@/components/ui/checkbox"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { SCALE_META, type ScaleKey } from "@/components/cx/kpi/kpi-scale-meta"
import type {
  BilingualText,
  CalculationMethodKind,
  EmojiSet,
  KpiDetail,
  KpiPerspective,
  KpiSaveInput,
  RepresentationStyle,
  Scale,
} from "@/features/kpi-management/api"
import { ScaleEndpointDescriptionFields } from "./ScaleEndpointDescriptionFields"
import { ThresholdBandEditor } from "./ThresholdBandEditor"
import { PerspectiveChipInput } from "./PerspectiveChipInput"
import { EmojiSetPreview } from "./EmojiSetPreview"

export interface KpiFormState {
  shortName: string
  fullName: string
  isStandard: boolean
  isComposite: boolean
  calculationMethod: CalculationMethodKind
  topNValue: number | null
  scale: Scale | null
  minScaleDescription: BilingualText | null
  maxScaleDescription: BilingualText | null
  representationStyle: RepresentationStyle | null
  emojiSet: EmojiSet | null
  thresholds: { lowerBound: number; x: number; y: number; upperBound: number }
  target: number | null
  isActive: boolean
  showOnDashboard: boolean
  perspectives: KpiPerspective[]
  /** Composite-only: members carrying a positive weight. Gates the Active checkbox (FR-043). */
  cxiActiveMemberCount: number
}

const CUSTOM_SCALES: Scale[] = ["Scale0_10", "Scale1_3", "Scale1_5", "Scale1_7", "Scale1_10", "Scale1_100"]
const CUSTOM_CALC_METHODS: CalculationMethodKind[] = ["WeightedAverage", "TopNBox"]

export function isNpsState(s: KpiFormState): boolean {
  return s.calculationMethod === "NPSStandard"
}

function targetRange(s: KpiFormState): { min: number; max: number } {
  return isNpsState(s) ? { min: -100, max: 100 } : { min: 0, max: 100 }
}

/** Enforces every cross-field invariant in one pass after a patch. */
function normalize(s: KpiFormState): KpiFormState {
  const next = { ...s }

  // NPS threshold range is −100..100; everything else 0..100.
  const nps = isNpsState(next)
  next.thresholds = {
    ...next.thresholds,
    lowerBound: nps ? -100 : 0,
    upperBound: 100,
  }

  // n only applies to TOP-n Box.
  if (next.calculationMethod !== "TopNBox") next.topNValue = null
  else if (next.topNValue == null) next.topNValue = 1

  // Slider is valid only on the 1–3 scale.
  if (next.representationStyle === "Slider" && next.scale !== "Scale1_3") {
    next.representationStyle = "Number"
  }

  // Emoji set only meaningful for the Emoji representation.
  if (next.representationStyle !== "Emoji") next.emojiSet = null
  else if (next.emojiSet == null) next.emojiSet = "FaceClassic"

  // FR-043: a composite KPI with fewer than two weighted members cannot be Active — force it off so
  // the checkbox reflects reality (the editor also disables it while blocked).
  if (next.isComposite && next.cxiActiveMemberCount < 2) next.isActive = false

  // Show-on-Dashboard requires Active.
  if (!next.isActive) next.showOnDashboard = false

  return next
}

type Action = { type: "patch"; patch: Partial<KpiFormState> } | { type: "reset"; state: KpiFormState }

function reducer(state: KpiFormState, action: Action): KpiFormState {
  switch (action.type) {
    case "patch":
      return normalize({ ...state, ...action.patch })
    case "reset":
      return normalize(action.state)
  }
}

export function initialFormState(detail: KpiDetail | null): KpiFormState {
  if (!detail) {
    return normalize({
      shortName: "",
      fullName: "",
      isStandard: false,
      isComposite: false,
      calculationMethod: "WeightedAverage",
      topNValue: null,
      scale: "Scale1_5",
      minScaleDescription: null,
      maxScaleDescription: null,
      representationStyle: "Number",
      emojiSet: null,
      thresholds: { lowerBound: 0, x: 20, y: 70, upperBound: 100 },
      target: 80,
      isActive: true,
      showOnDashboard: false,
      perspectives: [],
      cxiActiveMemberCount: 0,
    })
  }
  return normalize({
    shortName: detail.shortName,
    fullName: detail.fullName,
    isStandard: detail.kpiType === "Standard",
    isComposite: detail.isComposite,
    calculationMethod: detail.calculationMethod,
    topNValue: detail.topNValue,
    scale: detail.scale,
    minScaleDescription: detail.minScaleDescription,
    maxScaleDescription: detail.maxScaleDescription,
    representationStyle: detail.representationStyle,
    emojiSet: detail.emojiSet,
    thresholds: detail.thresholds,
    target: detail.target,
    isActive: detail.isActive,
    showOnDashboard: detail.showOnDashboard,
    perspectives: detail.perspectives,
    cxiActiveMemberCount: detail.cxiWeights?.filter((w) => w.weight > 0).length ?? 0,
  })
}

export function buildSaveInput(s: KpiFormState): KpiSaveInput {
  return {
    shortName: s.shortName.trim(),
    fullName: s.fullName.trim(),
    perspectives: s.perspectives,
    calculationMethod: s.calculationMethod,
    topNValue: s.topNValue,
    scale: s.scale,
    minScaleDescription: s.minScaleDescription,
    maxScaleDescription: s.maxScaleDescription,
    representationStyle: s.representationStyle,
    emojiSet: s.emojiSet,
    thresholds: s.thresholds,
    target: s.target,
    isActive: s.isActive,
    showOnDashboard: s.showOnDashboard,
  }
}

/** TOP-n Box advisory + blocking rules (FR-014 / FR-015) — the TS twin of TopNBoxWarningRule. */
export function topNState(scale: Scale | null, n: number | null): { warn: boolean; blocking: boolean } {
  if (scale == null || n == null) return { warn: false, blocking: false }
  const meta = SCALE_META[scale as ScaleKey]
  if (!meta) return { warn: false, blocking: false }
  const span = meta.max - meta.min
  return { warn: n > span / 2, blocking: n >= meta.points }
}

export function formValidity(s: KpiFormState): boolean {
  if (s.shortName.trim() === "" || s.shortName.trim().length > 20) return false
  if (s.fullName.trim() === "" || s.fullName.trim().length > 100) return false
  if (!s.isComposite && s.scale == null) return false
  const { lowerBound, x, y, upperBound } = s.thresholds
  if (!(lowerBound < x && x < y && y < upperBound)) return false
  if (s.isActive && s.target == null) return false
  if (s.target != null) {
    const { min, max } = targetRange(s)
    if (s.target < min || s.target > max) return false
  }
  if (s.calculationMethod === "TopNBox") {
    if (s.topNValue == null) return false
    if (topNState(s.scale, s.topNValue).blocking) return false
  }
  return true
}

// ── Context ─────────────────────────────────────────────────────────────────────

interface KpiFormContextValue {
  state: KpiFormState
  patch: (patch: Partial<KpiFormState>) => void
  mode: "create" | "edit"
  /** The persisted KPI's id (edit mode only) — needed for the FR-026 activation endpoint. */
  kpiId: string | null
  readOnly: boolean
  isValid: boolean
}

const KpiFormContext = createContext<KpiFormContextValue | null>(null)

export function useKpiForm(): KpiFormContextValue {
  const ctx = useContext(KpiFormContext)
  if (!ctx) throw new Error("useKpiForm must be used within KpiFormProvider")
  return ctx
}

export function KpiFormProvider({
  detail,
  mode,
  readOnly = false,
  children,
}: {
  detail: KpiDetail | null
  mode: "create" | "edit"
  readOnly?: boolean
  children: ReactNode
}) {
  const [state, dispatch] = useReducer(reducer, detail, initialFormState)
  const kpiId = detail?.id ?? null
  const value = useMemo<KpiFormContextValue>(
    () => ({
      state,
      patch: (patch) => dispatch({ type: "patch", patch }),
      mode,
      kpiId,
      readOnly,
      isValid: formValidity(state),
    }),
    [state, mode, kpiId, readOnly],
  )
  return <KpiFormContext.Provider value={value}>{children}</KpiFormContext.Provider>
}

// ── Form ────────────────────────────────────────────────────────────────────────

const REQUIRED = <span className="text-destructive"> *</span>

export function KpiConfigForm() {
  const { t } = useTranslation()
  const { state, patch, mode, kpiId, readOnly } = useKpiForm()

  // FR-026 binding-aware Active toggle (edit mode only). Toggling Active is NEVER persisted on its
  // own — it only updates form state, and the main Save button writes it (PUT /kpis/{id} carries
  // isActive). Deactivating a KPI still bound to M-16 touchpoints/journeys first opens a warning
  // dialog so the analyst sees the impact before flipping the checkbox; confirming there only mutates
  // form state, never the database. In create mode the KPI does not exist yet, so the toggle simply
  // updates form state too.
  const [activationBusy, setActivationBusy] = useState(false)
  const [deactivateDialog, setDeactivateDialog] = useState<{
    open: boolean
    touchpoints: number
    journeys: number
  }>({ open: false, touchpoints: 0, journeys: 0 })

  // Flips the form state to inactive (Show-on-Dashboard cascades off in the reducer's normalize
  // pass) and closes the warning dialog. No DB write — the main Save persists the new value.
  const applyDeactivation = () => {
    patch({ isActive: false })
    setDeactivateDialog((d) => ({ ...d, open: false }))
  }

  const handleActiveToggle = async (next: boolean) => {
    // Activation, create mode, or a not-yet-persisted KPI: just update form state.
    if (next || mode !== "edit" || !kpiId) {
      patch({ isActive: next })
      return
    }
    // Deactivation in edit mode — probe M-16 binding usage so we can warn before flipping the state.
    setActivationBusy(true)
    let touchpoints = 0
    let journeys = 0
    try {
      const usage = await getKpiBindingUsage(kpiId)
      touchpoints = usage.touchpointCount
      journeys = usage.journeyCount
    } catch {
      toast.error(t("kpi.deactivateError"))
      setActivationBusy(false)
      return
    }
    setActivationBusy(false)
    if (touchpoints > 0 || journeys > 0) {
      setDeactivateDialog({ open: true, touchpoints, journeys })
    } else {
      applyDeactivation()
    }
  }

  const shortNameLocked = readOnly || mode === "edit"
  const npsLocked = readOnly || (state.isStandard && isNpsState(state))
  const calcLocked = readOnly || state.isStandard
  const { min: targetMin, max: targetMax } = targetRange(state)
  // Target must sit inside the scale's allowed range (blocking); a target below the Satisfactory
  // band (< y) is allowed but flagged as a non-blocking advisory.
  const targetOutOfRange = state.target != null && (state.target < targetMin || state.target > targetMax)
  const targetBelowSatisfactory =
    state.target != null && !targetOutOfRange && state.target < state.thresholds.y
  const topN = topNState(state.scale, state.topNValue)
  // FR-043: a composite KPI's Active checkbox stays disabled until at least two members carry a weight.
  const cxiActivationBlocked = state.isComposite && state.cxiActiveMemberCount < 2

  const calcLabel: Record<CalculationMethodKind, string> = {
    WeightedAverage: t("kpi.calcWeightedAverage"),
    TopNBox: t("kpi.calcTopNBox"),
    NPSStandard: t("kpi.calcNpsStandard"),
    WeightedComposite: t("kpi.calcWeightedComposite"),
  }
  const scaleLabel: Record<Scale, string> = {
    Scale0_10: "0–10",
    Scale1_3: "1–3",
    Scale1_5: "1–5",
    Scale1_7: "1–7",
    Scale1_10: "1–10",
    Scale1_100: "1–100",
    Nps: "−100…+100",
  }
  const repLabel: Record<RepresentationStyle, string> = {
    Number: t("kpi.repNumber"),
    Stars: t("kpi.repStars"),
    Emoji: t("kpi.repEmoji"),
    Slider: t("kpi.repSlider"),
  }

  return (
    <div className="space-y-6">
      {/* Identity */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div className="space-y-1.5">
          <Label htmlFor="kpi-short-name">
            {t("kpi.shortNameField")}
            {REQUIRED}
          </Label>
          <Input
            id="kpi-short-name"
            value={state.shortName}
            maxLength={20}
            disabled={shortNameLocked}
            title={mode === "edit" ? t("kpi.shortNameLocked") : undefined}
            onChange={(e) => patch({ shortName: e.target.value })}
          />
          {mode === "edit" && (
            <p className="text-xs text-muted-foreground">{t("kpi.shortNameLocked")}</p>
          )}
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="kpi-full-name">
            {t("kpi.fullNameField")}
            {REQUIRED}
          </Label>
          <Input
            id="kpi-full-name"
            value={state.fullName}
            maxLength={100}
            disabled={readOnly}
            onChange={(e) => patch({ fullName: e.target.value })}
          />
        </div>
      </div>

      {/* Composite KPIs (CXI): calculation method is fixed to Weighted Composite (read-only). */}
      {state.isComposite && (
        <div className="space-y-1.5 sm:max-w-xs">
          <Label>{t("kpi.calcMethod")}</Label>
          <Input
            value={t("kpi.calcWeightedComposite")}
            disabled
            readOnly
            data-testid="kpi-calc-composite"
          />
          <p className="text-xs text-muted-foreground">{t("kpi.cxiCompositeMethodHint")}</p>
        </div>
      )}

      {/* Calculation method + scale */}
      {!state.isComposite && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div className="space-y-1.5">
            <Label>{t("kpi.calcMethod")}</Label>
            <Select
              value={state.calculationMethod}
              onValueChange={(v) => patch({ calculationMethod: (v ?? "WeightedAverage") as CalculationMethodKind })}
              disabled={calcLocked}
            >
              <SelectTrigger className="w-full" data-testid="kpi-calc-method" data-locked={calcLocked}>
                <SelectValue>{(v) => calcLabel[(v as CalculationMethodKind) ?? "WeightedAverage"]}</SelectValue>
              </SelectTrigger>
              <SelectContent>
                {(calcLocked ? [state.calculationMethod] : CUSTOM_CALC_METHODS).map((m) => (
                  <SelectItem key={m} value={m} data-testid={`kpi-calc-option-${m}`}>
                    {calcLabel[m]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {state.calculationMethod === "NPSStandard" && (
              <p className="text-xs text-muted-foreground" data-testid="kpi-calc-nps-formula">
                {t("kpi.calcNpsFormula")}
              </p>
            )}
          </div>

          <div className="space-y-1.5">
            <Label>
              {t("kpi.scaleField")}
              {REQUIRED}
            </Label>
            {npsLocked ? (
              <Input value={t("kpi.scaleLockedNps")} disabled readOnly data-testid="kpi-scale-locked" />
            ) : (
              <Select
                value={state.scale ?? undefined}
                onValueChange={(v) => patch({ scale: (v ?? null) as Scale })}
                disabled={readOnly}
              >
                <SelectTrigger className="w-full" data-testid="kpi-scale">
                  <SelectValue>{(v) => (v ? scaleLabel[v as Scale] : "")}</SelectValue>
                </SelectTrigger>
                <SelectContent>
                  {CUSTOM_SCALES.map((sc) => (
                    <SelectItem key={sc} value={sc} data-testid={`kpi-scale-option-${sc}`}>
                      {scaleLabel[sc]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          </div>
        </div>
      )}

      {/* TOP-n Box n value (conditional) */}
      {state.calculationMethod === "TopNBox" && (
        <div className="space-y-1.5 sm:max-w-xs">
          <Label htmlFor="kpi-top-n">
            {t("kpi.topNValue")}
            {REQUIRED}
          </Label>
          <Input
            id="kpi-top-n"
            type="number"
            inputMode="numeric"
            min={1}
            value={state.topNValue ?? ""}
            disabled={readOnly}
            aria-invalid={topN.blocking}
            onChange={(e) => patch({ topNValue: e.target.value === "" ? null : Math.round(Number(e.target.value)) })}
          />
          {topN.blocking ? (
            <p className="text-sm text-destructive" role="alert" data-testid="kpi-topn-error">
              {t("kpi.topNBlocking", { points: state.scale ? SCALE_META[state.scale as ScaleKey].points : 0 })}
            </p>
          ) : (
            topN.warn && (
              <p className="text-sm text-d3-dark dark:text-d3-light" data-testid="kpi-topn-warning">
                {t("kpi.topNWarn", { n: state.topNValue })}
              </p>
            )
          )}
        </div>
      )}

      {/* Representation + emoji set */}
      {!state.isComposite && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div className="space-y-1.5">
            <Label>{t("kpi.representationStyle")}</Label>
            <Select
              value={state.representationStyle ?? "Number"}
              onValueChange={(v) => patch({ representationStyle: (v ?? "Number") as RepresentationStyle })}
              disabled={readOnly}
            >
              <SelectTrigger className="w-full" data-testid="kpi-representation" data-value={state.representationStyle ?? "Number"}>
                <SelectValue>{(v) => repLabel[(v as RepresentationStyle) ?? "Number"]}</SelectValue>
              </SelectTrigger>
              <SelectContent>
                {(["Number", "Stars", "Emoji", "Slider"] as RepresentationStyle[]).map((r) => (
                  <SelectItem
                    key={r}
                    value={r}
                    data-testid={`kpi-representation-option-${r}`}
                    disabled={r === "Slider" && state.scale !== "Scale1_3"}
                  >
                    {repLabel[r]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {state.scale !== "Scale1_3" && (
              <p className="text-xs text-muted-foreground">{t("kpi.sliderHint")}</p>
            )}
          </div>
          {state.representationStyle === "Emoji" && (
            <EmojiSetPreview
              value={state.emojiSet}
              scale={state.scale as ScaleKey | null}
              label={t("kpi.emojiSet")}
              onChange={(set) => patch({ emojiSet: set })}
            />
          )}
        </div>
      )}

      {/* Scale endpoint descriptions */}
      {!state.isComposite && (
        <ScaleEndpointDescriptionFields
          min={state.minScaleDescription}
          max={state.maxScaleDescription}
          readOnly={readOnly}
          onChange={({ min, max }) => patch({ minScaleDescription: min, maxScaleDescription: max })}
        />
      )}

      {/* Thresholds */}
      <div className="space-y-1.5">
        <Label>
          {t("kpi.thresholdConfig")}
          {REQUIRED}
        </Label>
        <ThresholdBandEditor
          lower={state.thresholds.lowerBound}
          upper={state.thresholds.upperBound}
          x={state.thresholds.x}
          y={state.thresholds.y}
          readOnly={readOnly}
          onChange={({ x, y }) => patch({ thresholds: { ...state.thresholds, x, y } })}
        />
      </div>

      {/* Target */}
      <div className="space-y-1.5">
        <Label htmlFor="kpi-target">
          {t("kpi.target")}
          {state.isActive && REQUIRED}
        </Label>
        <Input
          id="kpi-target"
          type="number"
          inputMode="numeric"
          min={targetMin}
          max={targetMax}
          value={state.target ?? ""}
          disabled={readOnly}
          aria-invalid={targetOutOfRange}
          onChange={(e) => patch({ target: e.target.value === "" ? null : Number(e.target.value) })}
        />
        {targetOutOfRange ? (
          <p className="text-sm text-destructive" role="alert" data-testid="kpi-target-error">
            {t("kpi.targetOutOfRange", { min: targetMin, max: targetMax })}
          </p>
        ) : targetBelowSatisfactory ? (
          <p className="text-sm text-d3-dark dark:text-d3-light" data-testid="kpi-target-warning">
            {t("kpi.targetBelowSatisfactory")}
          </p>
        ) : (
          <p className="text-xs text-muted-foreground">{t("kpi.targetRange", { min: targetMin, max: targetMax })}</p>
        )}
      </div>

      {/* Perspectives */}
      {!state.isComposite && (
        <PerspectiveChipInput
          value={state.perspectives}
          readOnly={readOnly}
          onChange={(perspectives) => patch({ perspectives })}
        />
      )}

      {/* Active + dashboard */}
      <div className="space-y-3">
        <div
          className="flex items-center gap-2"
          title={cxiActivationBlocked ? t("kpi.cxiInsufficientMembers") : undefined}
        >
          <Checkbox
            id="kpi-active"
            checked={state.isActive}
            disabled={readOnly || cxiActivationBlocked || activationBusy}
            data-testid="kpi-active"
            onCheckedChange={(checked) => void handleActiveToggle(checked === true)}
          />
          <Label
            htmlFor="kpi-active"
            className="cursor-pointer text-sm font-normal data-[disabled]:text-muted-foreground"
          >
            {t("kpi.activeField")}
          </Label>
        </div>
        {cxiActivationBlocked && (
          <p className="text-xs text-d3-dark dark:text-d3-light" data-testid="kpi-active-blocked">
            {t("kpi.cxiInsufficientMembers")}
          </p>
        )}
        <div className="flex items-center gap-2">
          <Checkbox
            id="kpi-show-dashboard"
            checked={state.showOnDashboard}
            disabled={readOnly || !state.isActive}
            data-testid="kpi-show-dashboard"
            onCheckedChange={(checked) => patch({ showOnDashboard: checked === true })}
          />
          <Label
            htmlFor="kpi-show-dashboard"
            className="cursor-pointer text-sm font-normal data-[disabled]:text-muted-foreground"
          >
            {t("kpi.showOnDashboard")}
          </Label>
        </div>
        {!state.isActive && (
          <p className="text-xs text-muted-foreground">{t("kpi.showOnDashboardHint")}</p>
        )}
      </div>

      {/* FR-026 deactivation confirmation (bound KPI). */}
      <BindingUsageConfirmDialog
        open={deactivateDialog.open}
        touchpointCount={deactivateDialog.touchpoints}
        journeyCount={deactivateDialog.journeys}
        pending={activationBusy}
        onConfirm={applyDeactivation}
        onCancel={() => setDeactivateDialog((d) => ({ ...d, open: false }))}
      />
    </div>
  )
}
