// src/pages/KpiConfigPage.tsx
import { useReducer, useEffect, useState } from "react"
import { useParams, useNavigate } from "react-router"
import { useTranslation } from "react-i18next"
import { ArrowRight, Info } from "lucide-react"
import { useKpis } from "@/contexts/kpi-context"
import { MOCK_BOUND_TOUCHPOINTS } from "@/data/mock-kpis"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Checkbox } from "@/components/ui/checkbox"
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select"
import {
  Card, CardContent, CardHeader, CardTitle,
} from "@/components/ui/card"
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import {
  Tooltip, TooltipContent, TooltipTrigger,
} from "@/components/ui/tooltip"
import { cn } from "@/lib/utils"
import KpiGauge from "@/components/kpi/KpiGauge"
import KpiDashboardPreview from "@/components/kpi/KpiDashboardPreview"
import CxiSpiderPreview from "@/components/kpi/CxiSpiderPreview"
import QuestionPreview from "@/components/kpi/QuestionPreview"
import PerspectiveInput from "@/components/kpi/PerspectiveInput"
import CxiWeightsTable from "@/components/kpi/CxiWeightsTable"
import type {
  KpiDefinition, KpiScale, CalculationMethod, RepresentationStyle, EmojiSet,
} from "@/types/kpi"

// ── Form State ────────────────────────────────────────────

interface FormState {
  shortName: string
  fullName: string
  perspectives: string[]
  calculationMethod: CalculationMethod
  topNValue: string
  scale: KpiScale | ""
  representationStyle: RepresentationStyle
  emojiSet: EmojiSet
  thresholdX: string
  thresholdY: string
  target: string
  isActive: boolean
  showOnDashboard: boolean
  cxiWeights: Record<string, number>
  scaleMinLabel: string
  scaleMaxLabel: string
  isDirty: boolean
}

const KPI_SCALE_LABEL_DEFAULTS: Record<string, { min: string; max: string }> = {
  csat:  { min: "Extremely Dissatisfied",       max: "Extremely Satisfied" },
  ces:   { min: "Very High Effort Required",    max: "Very Low Effort Required" },
  nps:   { min: "Definitely Would Not Recommend", max: "Definitely Would Recommend" },
  vfm:   { min: "Very Poor Value for Money",    max: "Excellent Value for Money" },
  fcr:   { min: "Issue Not Resolved",           max: "Fully Resolved on First Contact" },
  agent: { min: "Very Poor Service Experience", max: "Excellent Service Experience" },
}

const DEFAULT_STATE: FormState = {
  shortName: "", fullName: "", perspectives: [],
  calculationMethod: "WeightedAverage", topNValue: "",
  scale: "", representationStyle: "Number", emojiSet: "FaceClassic",
  thresholdX: "20", thresholdY: "70", target: "",
  isActive: true, showOnDashboard: false,
  cxiWeights: {}, scaleMinLabel: "", scaleMaxLabel: "", isDirty: false,
}

type FormAction =
  | { type: "INIT"; kpi: KpiDefinition }
  | { type: "SET"; field: keyof Omit<FormState, "perspectives" | "cxiWeights" | "isDirty">; value: string | boolean }
  | { type: "SET_PERSPECTIVES"; value: string[] }
  | { type: "SET_CXI_WEIGHT"; kpiId: string; weight: number }

function formReducer(state: FormState, action: FormAction): FormState {
  switch (action.type) {
    case "INIT": {
      const k = action.kpi
      const labelDefaults = KPI_SCALE_LABEL_DEFAULTS[k.id] ?? { min: "", max: "" }
      return {
        shortName: k.shortName,
        fullName: k.fullName,
        perspectives: k.perspectives,
        calculationMethod: k.calculationMethod,
        topNValue: k.topNValue?.toString() ?? "",
        scale: k.scale ?? "",
        representationStyle: k.representationStyle,
        emojiSet: k.emojiSet ?? "FaceClassic",
        thresholdX: k.thresholdX.toString(),
        thresholdY: k.thresholdY.toString(),
        target: k.target.toString(),
        isActive: k.isActive,
        showOnDashboard: k.showOnDashboard,
        cxiWeights: k.cxiWeights ?? {},
        scaleMinLabel: k.scaleMinLabel ?? labelDefaults.min,
        scaleMaxLabel: k.scaleMaxLabel ?? labelDefaults.max,
        isDirty: false,
      }
    }
    case "SET": {
      const next = { ...state, [action.field]: action.value, isDirty: true }
      if (action.field === "isActive" && action.value === false) {
        next.showOnDashboard = false
      }
      return next
    }
    case "SET_PERSPECTIVES":
      return { ...state, perspectives: action.value, isDirty: true }
    case "SET_CXI_WEIGHT":
      return {
        ...state,
        cxiWeights: { ...state.cxiWeights, [action.kpiId]: action.weight },
        isDirty: true,
      }
    default:
      return state
  }
}

// ── Helper ─────────────────────────────────────────────────

function scaleValues(scale: KpiScale | ""): number {
  const map: Record<KpiScale, number> = {
    "0–10": 11, "1–3": 3, "1–5": 5, "1–7": 7, "1–10": 10, "1–100": 100,
  }
  return scale ? map[scale as KpiScale] : 0
}

// ── Page Component ─────────────────────────────────────────

export default function KpiConfigPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { t } = useTranslation()
  const { kpis, saveKpi } = useKpis()

  const isCreate = !id
  const isCxi = id === "cxi"
  const isNps = id === "nps"

  const existingKpi = id ? kpis.find((k) => k.id === id) : null

  const [state, dispatch] = useReducer(formReducer, DEFAULT_STATE)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [warnings, setWarnings] = useState<Record<string, string>>({})

  // Modals
  const [showCancelModal, setShowCancelModal] = useState(false)
  const [showScaleModal, setShowScaleModal] = useState(false)
  const [showDeactivateModal, setShowDeactivateModal] = useState(false)
  const [pendingScale, setPendingScale] = useState<KpiScale | null>(null)

  // Init from existing KPI
  useEffect(() => {
    if (existingKpi) dispatch({ type: "INIT", kpi: existingKpi })
  }, [existingKpi])

  // ── Validation ─────────────────────────────────────────

  function validate(): boolean {
    const e: Record<string, string> = {}
    const w: Record<string, string> = {}
    const xNum = parseInt(state.thresholdX)
    const yNum = parseInt(state.thresholdY)
    const tNum = parseInt(state.target)
    const tMin = isNps ? -100 : 0
    const tMax = 100

    if (!state.shortName.trim()) {
      e.shortName = t("kpi.errorShortNameRequired")
    } else if (isCreate && kpis.some((k) => k.shortName.toLowerCase() === state.shortName.trim().toLowerCase())) {
      e.shortName = t("kpi.errorShortNameInUse")
    }

    if (!state.fullName.trim()) e.fullName = t("kpi.errorFullNameRequired")

    if (!isCxi) {
      if (!state.thresholdX || isNaN(xNum) || xNum <= tMin || xNum >= yNum) {
        e.thresholdX = t("kpi.errorThresholdOrder")
      }
      if (!state.thresholdY || isNaN(yNum) || yNum <= xNum || yNum >= tMax) {
        e.thresholdY = t("kpi.errorThresholdOrder")
      }
      if (!state.target) {
        e.target = t("kpi.errorTargetRequired")
      } else if (isNaN(tNum) || tNum < tMin || tNum > tMax) {
        e.target = isNps ? t("kpi.errorTargetRangeNps") : t("kpi.errorTargetRange")
      } else if (!isNaN(yNum) && tNum < yNum) {
        w.target = t("kpi.warnTargetBelowSatisfactory")
      }

      if (state.calculationMethod === "TopNBox" && state.topNValue) {
        const n = parseInt(state.topNValue)
        const sv = scaleValues(state.scale)
        const warnThreshold = Math.floor((sv - 1) / 2)
        if (!isNaN(n) && sv > 0 && n > warnThreshold) {
          w.topNValue = t("kpi.warnTopNHigh")
        }
      }
    }

    setErrors(e)
    setWarnings(w)
    return Object.keys(e).length === 0
  }

  // ── Save ───────────────────────────────────────────────

  function handleSave() {
    if (!validate()) return
    const updated: KpiDefinition = {
      id: isCreate ? state.shortName.trim().toLowerCase().replace(/\s+/g, "_") : id!,
      shortName: state.shortName.trim(),
      fullName: state.fullName.trim(),
      type: isCreate ? "Custom" : (existingKpi?.type ?? "Custom"),
      scale: isCxi ? null : (state.scale as KpiScale),
      calculationMethod: state.calculationMethod,
      topNValue: state.calculationMethod === "TopNBox" ? parseInt(state.topNValue) || undefined : undefined,
      representationStyle: state.representationStyle,
      emojiSet: state.representationStyle === "Emoji" ? state.emojiSet : undefined,
      perspectives: state.perspectives,
      thresholdX: state.thresholdX !== "" ? parseInt(state.thresholdX) : 20,
      thresholdY: state.thresholdY !== "" ? parseInt(state.thresholdY) : 70,
      target: state.target !== "" ? parseInt(state.target) : 0,
      isActive: state.isActive,
      showOnDashboard: state.showOnDashboard,
      cxiWeights: isCxi ? state.cxiWeights : undefined,
      scaleMinLabel: state.scaleMinLabel || undefined,
      scaleMaxLabel: state.scaleMaxLabel || undefined,
    }
    saveKpi(updated)
    navigate("/kpi-management")
  }

  // ── Scale change guard ─────────────────────────────────

  function handleScaleChange(newScale: KpiScale) {
    const bound = MOCK_BOUND_TOUCHPOINTS[id ?? ""]
    if (!isCreate && bound) {
      setPendingScale(newScale)
      setShowScaleModal(true)
    } else {
      dispatch({ type: "SET", field: "scale", value: newScale })
    }
  }

  // ── Deactivate guard ───────────────────────────────────

  function handleIsActiveChange(checked: boolean) {
    if (!checked) {
      const bound = MOCK_BOUND_TOUCHPOINTS[id ?? ""]
      if (!isCreate && bound) {
        setShowDeactivateModal(true)
        return
      }
    }
    dispatch({ type: "SET", field: "isActive", value: checked })
  }

  // ── Derived preview props ──────────────────────────────

  const gaugeX = state.thresholdX !== "" ? parseInt(state.thresholdX) : null
  const gaugeY = state.thresholdY !== "" ? parseInt(state.thresholdY) : null
  const gaugeTarget = state.target !== "" ? parseInt(state.target) : null

  // CXI active guard
  const cxiActiveKpisWithWeight = Object.values(state.cxiWeights).filter((w) => w >= 1).length
  const cxiCanActivate = !isCxi || cxiActiveKpisWithWeight >= 2

  // Save button enablement
  const canSave = Boolean(
    state.shortName &&
    state.fullName &&
    (isCxi || state.scale) &&
    (isCxi || state.calculationMethod) &&
    (isCxi || state.thresholdX) &&
    (isCxi || state.thresholdY),
  )

  // ── Render ─────────────────────────────────────────────

  return (
    <div className="space-y-5 py-5 px-8">
      {/* Back + Page Title */}
      <div className="flex items-center gap-3">
        <Button
          variant="ghost"
          size="icon"
          aria-label="Back"
          onClick={() => {
            if (state.isDirty) setShowCancelModal(true)
            else navigate("/kpi-management")
          }}
        >
          <ArrowRight className="size-5" />
        </Button>
        <div>
          <h1 className="text-2xl font-heading font-bold">
            {isCreate ? t("kpi.newKpi") : t("kpi.configuration")}
          </h1>
          {!isCreate && (
            <p className="text-sm text-muted-foreground mt-0.5">{state.fullName}</p>
          )}
        </div>
      </div>

      {/* Two-panel grid */}
      <div className="grid grid-cols-[55fr_45fr] gap-6 items-start">

        {/* LEFT PANEL: Form */}
        <Card>
          <CardContent className="space-y-5 pt-5">

            {/* Short Name */}
            <div className="space-y-1.5">
              <div className="flex items-center gap-1.5">
                <Label htmlFor="shortName">
                  {t("kpi.shortName")}<span className="text-destructive ms-0.5">*</span>
                </Label>
                {!isCreate && (
                  <Tooltip>
                    <TooltipTrigger
                      className="inline-flex items-center text-muted-foreground cursor-help"
                      aria-label={t("kpi.shortNameReadonlyTooltip")}
                    >
                      <Info className="size-3.5" />
                    </TooltipTrigger>
                    <TooltipContent>{t("kpi.shortNameReadonlyTooltip")}</TooltipContent>
                  </Tooltip>
                )}
              </div>
              {isCreate ? (
                <Input
                  id="shortName"
                  value={state.shortName}
                  maxLength={10}
                  onChange={(e) => dispatch({ type: "SET", field: "shortName", value: e.target.value.toUpperCase() })}
                  onBlur={() => {
                    const inUse = kpis.some((k) => k.shortName.toLowerCase() === state.shortName.trim().toLowerCase())
                    if (inUse) setErrors((prev) => ({ ...prev, shortName: t("kpi.errorShortNameInUse") }))
                    else setErrors((prev) => { const n = { ...prev }; delete n.shortName; return n })
                  }}
                  placeholder="e.g. QUAL"
                  className={cn(errors.shortName && "border-destructive")}
                />
              ) : (
                <p className="text-sm font-mono font-bold text-primary px-3 py-2 bg-muted/30 rounded-md">
                  {state.shortName}
                </p>
              )}
              {errors.shortName && <p className="text-xs text-destructive" role="alert">{errors.shortName}</p>}
            </div>

            {/* Full Name */}
            <div className="space-y-1.5">
              <Label htmlFor="fullName">
                {t("kpi.fullName")}<span className="text-destructive ms-0.5">*</span>
              </Label>
              <Input
                id="fullName"
                value={state.fullName}
                maxLength={100}
                onChange={(e) => dispatch({ type: "SET", field: "fullName", value: e.target.value })}
                className={cn(errors.fullName && "border-destructive")}
              />
              {errors.fullName && <p className="text-xs text-destructive" role="alert">{errors.fullName}</p>}
            </div>

            {/* Perspectives */}
            <div className="space-y-1.5">
              <Label>{t("kpi.perspectives")}</Label>
              <PerspectiveInput
                value={state.perspectives}
                onChange={(v) => dispatch({ type: "SET_PERSPECTIVES", value: v })}
              />
              <p className="text-xs text-muted-foreground">{t("kpi.perspectivesHint")}</p>
            </div>

            {/* CXI SPECIAL LAYOUT */}
            {isCxi ? (
              <>
                <div className="space-y-1.5">
                  <Label>{t("kpi.calcMethod")}</Label>
                  <p className="text-sm px-3 py-2 bg-muted/30 rounded-md text-muted-foreground">
                    {t("kpi.cxiMethod")}
                  </p>
                </div>
                <div className="space-y-2">
                  <Label>{t("kpi.cxiWeightsTitle")}</Label>
                  <CxiWeightsTable
                    weights={state.cxiWeights}
                    onChange={(kpiId, weight) => dispatch({ type: "SET_CXI_WEIGHT", kpiId, weight })}
                    showLegend
                  />
                </div>
              </>
            ) : (
              <>
                {/* Calculation Method */}
                <div className="space-y-1.5">
                  <Label>
                    {t("kpi.calcMethod")}<span className="text-destructive ms-0.5">*</span>
                  </Label>
                  {isNps ? (
                    <div className="space-y-1">
                      <p className="text-sm px-3 py-2 bg-muted/30 rounded-md text-muted-foreground">
                        {t("kpi.calcMethodNpsStandard")}
                      </p>
                      <p className="text-xs text-muted-foreground ps-1">{t("kpi.npsFormulaLabel")}</p>
                    </div>
                  ) : (
                    <Select
                      value={state.calculationMethod}
                      onValueChange={(v) => dispatch({ type: "SET", field: "calculationMethod", value: v as CalculationMethod })}
                    >
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="WeightedAverage">{t("kpi.calcMethodWeightedAverage")}</SelectItem>
                        <SelectItem value="TopNBox">{t("kpi.calcMethodTopNBox")}</SelectItem>
                      </SelectContent>
                    </Select>
                  )}
                </div>

                {/* TOP n value (conditional) */}
                {state.calculationMethod === "TopNBox" && !isNps && (
                  <div className="space-y-1.5">
                    <Label htmlFor="topNValue">{t("kpi.topNValue")}</Label>
                    <Input
                      id="topNValue"
                      type="number"
                      min={1}
                      value={state.topNValue}
                      onChange={(e) => dispatch({ type: "SET", field: "topNValue", value: e.target.value })}
                    />
                    <p className="text-xs text-muted-foreground">{t("kpi.topNHint")}</p>
                    {warnings.topNValue && (
                      <p className="text-xs text-d3">{warnings.topNValue}</p>
                    )}
                  </div>
                )}

                {/* Scale */}
                <div className="space-y-1.5">
                  <Label>
                    {t("kpi.scale")}<span className="text-destructive ms-0.5">*</span>
                  </Label>
                  {isNps ? (
                    <p className="text-sm px-3 py-2 bg-muted/30 rounded-md text-muted-foreground">
                      {t("kpi.scaleLocked", { scale: "0–10", kpi: "NPS" })}
                    </p>
                  ) : (
                    <Select
                      value={state.scale}
                      onValueChange={(v) => handleScaleChange(v as KpiScale)}
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="Select scale" />
                      </SelectTrigger>
                      <SelectContent>
                        {(["0–10", "1–3", "1–5", "1–7", "1–10", "1–100"] as KpiScale[]).map((s) => (
                          <SelectItem key={s} value={s}>{s}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  )}
                </div>

                {/* Scale Labels */}
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1.5">
                    <Label htmlFor="scaleMinLabel">Minimum Scale Description</Label>
                    <Input
                      id="scaleMinLabel"
                      value={state.scaleMinLabel}
                      placeholder="e.g. Strongly Disagree"
                      onChange={(e) => dispatch({ type: "SET", field: "scaleMinLabel", value: e.target.value })}
                    />
                  </div>
                  <div className="space-y-1.5">
                    <Label htmlFor="scaleMaxLabel">Maximum Scale Description</Label>
                    <Input
                      id="scaleMaxLabel"
                      value={state.scaleMaxLabel}
                      placeholder="e.g. Strongly Agree"
                      onChange={(e) => dispatch({ type: "SET", field: "scaleMaxLabel", value: e.target.value })}
                    />
                  </div>
                </div>

                {/* Representation Style */}
                <div className="space-y-1.5">
                  <Label>{t("kpi.reprStyle")}</Label>
                  <Select
                    value={state.representationStyle}
                    onValueChange={(v) => dispatch({ type: "SET", field: "representationStyle", value: v as RepresentationStyle })}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Number">{t("kpi.reprStyleNumber")}</SelectItem>
                      <SelectItem value="Stars">{t("kpi.reprStyleStars")}</SelectItem>
                      <SelectItem value="Emoji">{t("kpi.reprStyleEmoji")}</SelectItem>
                      <SelectItem value="Slider" disabled={state.scale !== "1–3"}>
                        {t("kpi.reprStyleSlider")}
                        {state.scale !== "1–3" && " (1–3 only)"}
                      </SelectItem>
                    </SelectContent>
                  </Select>
                </div>

                {/* Emoji Set (conditional) */}
                {state.representationStyle === "Emoji" && (
                  <div className="space-y-1.5">
                    <Label>{t("kpi.emojiSet")}</Label>
                    <Select
                      value={state.emojiSet}
                      onValueChange={(v) => dispatch({ type: "SET", field: "emojiSet", value: v as EmojiSet })}
                    >
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {(["FaceClassic","FaceExpressive","FaceBold","Thumbs","Hearts","Weather","TrafficLights","Shapes"] as EmojiSet[]).map((s) => (
                          <SelectItem key={s} value={s}>{t(`kpi.emojiSet${s}`)}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                )}
              </>
            )}

            {/* Threshold */}
            <div className="space-y-2">
              <Label>
                {t("kpi.threshold")}<span className="text-destructive ms-0.5">*</span>
              </Label>
              <div className="rounded-md border border-border p-3 space-y-2 text-sm">
                {/* Unsatisfactory row */}
                <div className="flex items-center gap-2">
                  <span className="w-32 text-d5 dark:text-d5-light font-medium text-xs">
                    {t("kpi.thresholdUnsatisfactory")}
                  </span>
                  <span className="text-muted-foreground">{isNps ? "−100" : "0"} ≤</span>
                  <span className="text-muted-foreground">score ≤</span>
                  <Input
                    type="number"
                    value={state.thresholdX}
                    onChange={(e) => dispatch({ type: "SET", field: "thresholdX", value: e.target.value })}
                    className={cn("w-20 text-center tabular-nums h-8", errors.thresholdX && "border-destructive")}
                  />
                </div>
                {/* Average row */}
                <div className="flex items-center gap-2">
                  <span className="w-32 text-d3 dark:text-d3-light font-medium text-xs">
                    {t("kpi.thresholdAverage")}
                  </span>
                  <Input
                    type="number"
                    value={state.thresholdX}
                    readOnly
                    className="w-20 text-center tabular-nums h-8 bg-muted/30"
                  />
                  <span className="text-muted-foreground">&lt; score ≤</span>
                  <Input
                    type="number"
                    value={state.thresholdY}
                    onChange={(e) => dispatch({ type: "SET", field: "thresholdY", value: e.target.value })}
                    className={cn("w-20 text-center tabular-nums h-8", errors.thresholdY && "border-destructive")}
                  />
                </div>
                {/* Satisfactory row */}
                <div className="flex items-center gap-2">
                  <span className="w-32 text-d2 dark:text-d2-light font-medium text-xs">
                    {t("kpi.thresholdSatisfactory")}
                  </span>
                  <Input
                    type="number"
                    value={state.thresholdY}
                    readOnly
                    className="w-20 text-center tabular-nums h-8 bg-muted/30"
                  />
                  <span className="text-muted-foreground">&lt; score ≤ {isNps ? "+100" : "100"}</span>
                </div>
              </div>
              {(errors.thresholdX || errors.thresholdY) && (
                <p className="text-xs text-destructive" role="alert">{errors.thresholdX || errors.thresholdY}</p>
              )}
            </div>

            {/* Target */}
            <div className="space-y-1.5">
              <Label htmlFor="target">{t("kpi.target")}</Label>
              <Input
                id="target"
                type="number"
                min={isNps ? -100 : 0}
                max={100}
                value={state.target}
                onChange={(e) => dispatch({ type: "SET", field: "target", value: e.target.value })}
                className={cn("tabular-nums", errors.target && "border-destructive")}
              />
              {errors.target && <p className="text-xs text-destructive" role="alert">{errors.target}</p>}
              {warnings.target && <p className="text-xs text-d3">{warnings.target}</p>}
            </div>

            {/* Active + Dashboard checkboxes */}
            <div className="space-y-3 pt-1">
              <div className="flex items-center gap-2">
                <Checkbox
                  id="isActive"
                  checked={state.isActive}
                  disabled={isCxi && !cxiCanActivate}
                  onCheckedChange={(v) => handleIsActiveChange(Boolean(v))}
                />
                <Label htmlFor="isActive" className="cursor-pointer">{t("kpi.isActive")}</Label>
                {isCxi && !cxiCanActivate && (
                  <Tooltip>
                    <TooltipTrigger
                      className="inline-flex items-center text-muted-foreground cursor-help"
                      aria-label={t("kpi.cxiMinKpisWarning")}
                    >
                      <Info className="size-3.5" />
                    </TooltipTrigger>
                    <TooltipContent>{t("kpi.cxiMinKpisWarning")}</TooltipContent>
                  </Tooltip>
                )}
              </div>
              <div className="flex items-center gap-2">
                <Checkbox
                  id="showOnDashboard"
                  checked={state.showOnDashboard}
                  disabled={!state.isActive}
                  onCheckedChange={(v) => dispatch({ type: "SET", field: "showOnDashboard", value: Boolean(v) })}
                />
                <Label htmlFor="showOnDashboard" className={cn("cursor-pointer", !state.isActive && "opacity-50")}>
                  {t("kpi.showOnDashboard")}
                </Label>
              </div>
            </div>

            {/* Form actions */}
            <div className="flex items-center justify-end gap-3 pt-2 border-t border-border">
              <Button
                variant="ghost"
                onClick={() => {
                  if (state.isDirty) setShowCancelModal(true)
                  else navigate("/kpi-management")
                }}
              >
                Cancel
              </Button>
              <Button
                onClick={handleSave}
                disabled={!canSave}
                className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
              >
                Save
              </Button>
            </div>
          </CardContent>
        </Card>

        {/* RIGHT PANEL: Live Preview */}
        <div className="space-y-4 sticky top-20">

          {/* Question Preview (hidden for CXI) */}
          {!isCxi && state.scale && (
            <Card>
              <CardHeader className="pb-3">
                <CardTitle className="text-sm">{t("kpi.previewTitle")}</CardTitle>
              </CardHeader>
              <CardContent>
                <QuestionPreview
                  fullName={state.fullName}
                  kpiId={id ?? "custom"}
                  scale={state.scale as KpiScale}
                  representationStyle={state.representationStyle}
                  emojiSet={state.emojiSet}
                  scaleMinLabel={state.scaleMinLabel}
                  scaleMaxLabel={state.scaleMaxLabel}
                />
              </CardContent>
            </Card>
          )}

          {/* KPI Dashboard Preview / Gauge */}
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-sm">
                {isCxi
                  ? t("kpi.kpiVisualization", { defaultValue: "KPI Visualization" })
                  : id && ["nps","csat","ces","chs","agent","vfm","fcr"].includes(id)
                    ? t("kpi.dashboardPreviewTitle", { defaultValue: "Dashboard Preview" })
                    : t("kpi.gaugeTitle")}
              </CardTitle>
            </CardHeader>
            <CardContent>
              {isCxi ? (
                <CxiSpiderPreview
                  weights={state.cxiWeights}
                  shortName={state.shortName || "CXI"}
                />
              ) : id && ["nps","csat","ces","chs","agent","vfm","fcr"].includes(id) ? (
                <KpiDashboardPreview
                  kpiId={id}
                  liveTitle={state.shortName || undefined}
                  liveSubtitle={state.fullName || undefined}
                  liveThresholdX={gaugeX}
                  liveThresholdY={gaugeY}
                  liveTarget={gaugeTarget}
                />
              ) : (
                <KpiGauge
                  shortName={state.shortName || (existingKpi?.shortName ?? "KPI")}
                  thresholdX={gaugeX}
                  thresholdY={gaugeY}
                  targetValue={gaugeTarget}
                  isNps={isNps}
                />
              )}
            </CardContent>
          </Card>
        </div>
      </div>

      {/* Confirmation Modals */}

      {/* Cancel with unsaved changes */}
      <AlertDialog open={showCancelModal} onOpenChange={(v) => setShowCancelModal(v)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("kpi.modalUnsavedTitle")}</AlertDialogTitle>
            <AlertDialogDescription>{t("kpi.modalUnsavedBody")}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>No</AlertDialogCancel>
            <AlertDialogAction onClick={() => navigate("/kpi-management")}>Yes, leave</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Scale change */}
      <AlertDialog open={showScaleModal} onOpenChange={(v) => setShowScaleModal(v)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("kpi.modalScaleChangeTitle")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("kpi.modalScaleChangeBody", {
                n: MOCK_BOUND_TOUCHPOINTS[id ?? ""]?.touchpoints ?? 0,
              })}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel onClick={() => setPendingScale(null)}>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                if (pendingScale) dispatch({ type: "SET", field: "scale", value: pendingScale })
                setPendingScale(null)
                setShowScaleModal(false)
              }}
            >
              Continue
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Deactivate */}
      <AlertDialog open={showDeactivateModal} onOpenChange={(v) => setShowDeactivateModal(v)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("kpi.modalDeactivateTitle")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("kpi.modalDeactivateBody", {
                n: MOCK_BOUND_TOUCHPOINTS[id ?? ""]?.touchpoints ?? 0,
                m: MOCK_BOUND_TOUCHPOINTS[id ?? ""]?.journeys ?? 0,
              })}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                dispatch({ type: "SET", field: "isActive", value: false })
                setShowDeactivateModal(false)
              }}
            >
              Deactivate
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}
