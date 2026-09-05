// Layered KPI binding editor (T093, FR-8.4): KPI → Perspective → Bound-journey toggle
// → Stage → Touchpoint. Each layer narrows the next: Perspective options come from the
// selected KPI (M-06 catalogue), Stage options from the survey's bound journey (M-16),
// Touchpoint options from the selected stage filtered to touchpoints measuring the
// selected KPI. Toggle OFF disables + clears Stage/Touchpoint (BR-8.2 — the server's
// KpiBindingChangePolicy also strips them on save).

import { useEffect, useState } from "react"
import { useTranslation } from "react-i18next"

import { Label } from "@/components/ui/label"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Switch } from "@/components/ui/switch"
import { getJourney, listStages } from "@/features/journeys/api"
import type { JourneyDetail, StageSummary } from "@/features/journeys/api"
import { getKpi, listKpis } from "@/features/kpi-management/api"
import type { KpiListItem, KpiPerspective } from "@/features/kpi-management/api"
import type { KpiBindingDraft } from "./builder-types"

/** Human range for an M-06 Scale enum member ("Scale0_10" -> "0-10"). */
function scaleRangeLabel(scale: string): string {
  const m = scale.match(/^Scale(\d+)_(\d+)$/)
  return m ? `${m[1]}-${m[2]}` : scale
}

export function KpiBindingEditor({
  value,
  onChange,
  /** The survey's bound journey (F3 setting); null when the survey is Seasonal. */
  boundJourneyId,
  disabled,
  idPrefix,
}: {
  value: KpiBindingDraft
  onChange: (next: KpiBindingDraft) => void
  boundJourneyId: string | null
  disabled?: boolean
  idPrefix: string
}) {
  const { t } = useTranslation()
  const [kpis, setKpis] = useState<KpiListItem[]>([])
  const [perspectives, setPerspectives] = useState<KpiPerspective[]>([])
  const [kpiScale, setKpiScale] = useState<string | null>(null)
  const [stages, setStages] = useState<StageSummary[]>([])
  const [journeyTree, setJourneyTree] = useState<JourneyDetail | null>(null)

  useEffect(() => {
    let cancelled = false
    listKpis({ activeOnly: true })
      .then((r) => !cancelled && setKpis(r.items))
      .catch(() => !cancelled && setKpis([]))
    return () => {
      cancelled = true
    }
  }, [])

  // Perspective options follow the selected KPI (layered narrowing).
  useEffect(() => {
    let cancelled = false
    if (!value.kpiKey) {
      setPerspectives([])
      setKpiScale(null)
      return
    }
    getKpi(value.kpiKey)
      .then((detail) => {
        if (cancelled) return
        setPerspectives(detail.perspectives ?? [])
        setKpiScale(detail.scale ?? null)
      })
      .catch(() => {
        if (cancelled) return
        setPerspectives([])
        setKpiScale(null)
      })
    return () => {
      cancelled = true
    }
  }, [value.kpiKey])

  // Stage + touchpoint options follow the survey's bound journey.
  useEffect(() => {
    let cancelled = false
    if (!boundJourneyId) {
      setStages([])
      setJourneyTree(null)
      return
    }
    listStages(boundJourneyId)
      .then((r) => !cancelled && setStages(r.stages))
      .catch(() => !cancelled && setStages([]))
    getJourney(boundJourneyId)
      .then((j) => !cancelled && setJourneyTree(j))
      .catch(() => !cancelled && setJourneyTree(null))
    return () => {
      cancelled = true
    }
  }, [boundJourneyId])

  const bindingActive = value.boundJourneyOn && boundJourneyId !== null
  const stageDetail = journeyTree?.stages.find((s) => s.stageId === value.stageId)
  // Touchpoints of the selected stage that measure the selected KPI (FR-8.4 filter);
  // when the KPI isn't measured anywhere in the stage the list is empty and the
  // select explains why instead of dead-ending silently.
  const touchpoints = (stageDetail?.touchpoints ?? []).filter(
    (tp) => !value.kpiKey || tp.kpiBindings.some((b) => b.kpiType === value.kpiKey)
  )

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-1.5">
        <Label htmlFor={`${idPrefix}-kpi`}>
          {t("surveysModule.kpi.kpiLabel")} <span className="text-destructive">*</span>
        </Label>
        <Select
          value={value.kpiKey ?? ""}
          onValueChange={(v) =>
            onChange({
              ...value,
              kpiKey: v || null,
              perspectiveLabel: null,
              touchpointId: null,
              touchpointDisplay: null,
            })
          }
          disabled={disabled}
        >
          <SelectTrigger id={`${idPrefix}-kpi`} className="w-full">
            <SelectValue>
              {(v) => (v ? String(v) : t("surveysModule.kpi.kpiPlaceholder"))}
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            {kpis.map((k) => (
              <SelectItem key={k.id} value={k.shortName}>
                {k.shortName} ({k.fullName})
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        {kpiScale && (
          <p className="text-xs text-muted-foreground">
            {t("surveysModule.kpi.scaleHelp", { range: scaleRangeLabel(kpiScale) })}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor={`${idPrefix}-perspective`} className="leading-relaxed">
          {t("surveysModule.kpi.perspectiveLabel")}{" "}
          <span className="font-normal text-muted-foreground">
            {t("surveysModule.settings.optionalSuffix")}
          </span>
        </Label>
        {/* Always shown once a KPI is bound: "Overall (no perspective)" is the default
            first option, followed by the KPI's named perspectives (reference parity —
            the dropdown appears even when the KPI has no named perspectives). */}
        <Select
          value={value.perspectiveLabel ?? "__overall"}
          onValueChange={(v) =>
            onChange({ ...value, perspectiveLabel: v && v !== "__overall" ? v : null })
          }
          disabled={disabled}
        >
          <SelectTrigger id={`${idPrefix}-perspective`} className="w-full">
            <SelectValue>
              {(v) =>
                v && v !== "__overall"
                  ? String(v)
                  : t("surveysModule.kpi.perspectiveOverall")
              }
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__overall">
              {t("surveysModule.kpi.perspectiveOverall")}
            </SelectItem>
            {perspectives.map((p) => (
              <SelectItem key={p.label} value={p.label}>
                {p.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <p className="text-xs text-muted-foreground">
          {t("surveysModule.kpi.perspectiveHelp")}
        </p>
      </div>

      <div className="flex items-center justify-between gap-3 rounded-md border border-border bg-muted/30 p-3">
        <div className="min-w-0">
          <Label htmlFor={`${idPrefix}-bound`} className="cursor-pointer">
            {t("surveysModule.kpi.boundJourney")}
          </Label>
          <p className="mt-0.5 text-xs leading-relaxed text-muted-foreground">
            {journeyTree?.name
              ? t("surveysModule.kpi.boundJourneyHelpBound", { journey: journeyTree.name })
              : t("surveysModule.kpi.boundJourneyHelpUnbound")}
          </p>
        </div>
        <Switch
          id={`${idPrefix}-bound`}
          checked={value.boundJourneyOn}
          onCheckedChange={(on) =>
            // BR-8.2: turning the binding off clears Stage/Touchpoint.
            onChange(
              on
                ? { ...value, boundJourneyOn: true }
                : {
                    ...value,
                    boundJourneyOn: false,
                    stageId: null,
                    touchpointId: null,
                    touchpointDisplay: null,
                  }
            )
          }
          disabled={disabled || boundJourneyId === null}
        />
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor={`${idPrefix}-stage`}>{t("surveysModule.kpi.stageLabel")}</Label>
        <Select
          value={value.stageId ?? ""}
          onValueChange={(v) =>
            onChange({ ...value, stageId: v || null, touchpointId: null, touchpointDisplay: null })
          }
          disabled={disabled || !bindingActive}
        >
          <SelectTrigger id={`${idPrefix}-stage`} className="w-full">
            <SelectValue>
              {(v) =>
                v
                  ? (stages.find((s) => s.stageId === v)?.name ?? String(v))
                  : t("surveysModule.kpi.nonePlaceholder")
              }
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            {stages.map((s) => (
              <SelectItem key={s.stageId} value={s.stageId}>
                {s.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <p className="text-xs text-muted-foreground">{t("surveysModule.kpi.stageHelp")}</p>
      </div>

      <div className="flex flex-col gap-1.5">
        {/* Stage is required before Touchpoint (kpi.touchpoint.requires_stage). */}
        <Label htmlFor={`${idPrefix}-touchpoint`} className="leading-relaxed">
          {t("surveysModule.kpi.touchpointLabel")}{" "}
          <span className="font-normal text-muted-foreground">
            {t("surveysModule.kpi.touchpointSuffix")}
          </span>
        </Label>
        <Select
          value={value.touchpointId ?? ""}
          onValueChange={(v) => {
            // Card chip shows the resolved "stage → touchpoint" names (reference).
            const tpName = touchpoints.find((tp) => tp.touchpointId === v)?.name
            onChange({
              ...value,
              touchpointId: v || null,
              touchpointDisplay:
                v && stageDetail && tpName ? `${stageDetail.name} → ${tpName}` : null,
            })
          }}
          disabled={disabled || !bindingActive || !value.stageId}
        >
          <SelectTrigger id={`${idPrefix}-touchpoint`} className="w-full">
            <SelectValue>
              {(v) =>
                v
                  ? (touchpoints.find((tp) => tp.touchpointId === v)?.name ?? String(v))
                  : t("surveysModule.kpi.nonePlaceholder")
              }
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            {touchpoints.length === 0 ? (
              <div className="px-3 py-2 text-sm text-muted-foreground">
                {t("surveysModule.kpi.noTouchpoints")}
              </div>
            ) : (
              touchpoints.map((tp) => (
                <SelectItem key={tp.touchpointId} value={tp.touchpointId}>
                  {tp.name}
                </SelectItem>
              ))
            )}
          </SelectContent>
        </Select>
      </div>
    </div>
  )
}
