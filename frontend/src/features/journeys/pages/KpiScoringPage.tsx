// KPI configuration page (T056, US-2; scoring card removed in the US-2 Amendment): per-touchpoint
// KPI bindings for a single journey. One reusable `KpiWeightEditor` (T055) per touchpoint, grouped
// under its stage. The KPI-type catalog (`GET /api/v1/kpi-types`) is fetched ONCE here and passed
// down so the page renders N editors with a single catalog request.
//
// Strategic scoring is now TENANT-level (SRS §4.2.9 / §11.7, Q11 RESOLVED — per-tenant, not
// per-journey): the former per-journey scoring-model / normalization editor is removed. The five
// tenant parameters (α, MOT, n_floor, flag percentile, rolling window) are edited on the Platform
// Settings → Customer Journey page (feature 003), not here.
//
// Archived journeys render read-only. RTL-first, design-system styled (logical properties,
// D-scale tokens for measured state, brand cyan only for chrome). Reachable from the Journey Builder
// header ("KPI & Scoring") at `/journeys/:id/scoring`.

import { useCallback, useEffect, useState } from "react"
import { Link, useParams } from "react-router"
import { useTranslation } from "react-i18next"
import { AlertTriangle, ChevronLeft, CircleCheck, Layers, Lock, SlidersHorizontal } from "lucide-react"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import {
  getJourney,
  getKpiTypes,
  type JourneyDetail,
  type KpiType,
  type SaveKpiBindingsResponse,
} from "@/features/journeys/api"
import { JourneyStatusBadge } from "@/features/journeys/components/JourneyStatusBadge"
import { KpiWeightEditor } from "@/features/journeys/components/KpiWeightEditor"

export default function KpiScoringPage() {
  const { t } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const journeyId = id ?? ""

  const [journey, setJourney] = useState<JourneyDetail | null>(null)
  const [kpiTypes, setKpiTypes] = useState<KpiType[]>([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(false)

  const readOnly = journey?.status === "Archived"

  // Initial load: journey tree + KPI-type catalog.
  const load = useCallback(async () => {
    if (!journeyId) return
    setLoading(true)
    setLoadError(false)
    try {
      const [detail, types] = await Promise.all([getJourney(journeyId), getKpiTypes()])
      setJourney(detail)
      setKpiTypes(types)
    } catch {
      setLoadError(true)
    } finally {
      setLoading(false)
    }
  }, [journeyId])

  useEffect(() => {
    void load()
  }, [load])

  // Reflect a touchpoint's persisted bindings back into the tree so its measured pill stays
  // current. `KpiWeightEditor` keeps its own row state (initialized once), so updating the tree
  // here refreshes only the header badge — it never resets an in-progress editor.
  const handleTouchpointSaved = useCallback(
    (touchpointId: string, result: SaveKpiBindingsResponse) => {
      setJourney((prev) => {
        if (!prev) return prev
        return {
          ...prev,
          stages: prev.stages.map((stage) => ({
            ...stage,
            touchpoints: stage.touchpoints.map((tp) =>
              tp.touchpointId === touchpointId
                ? {
                    ...tp,
                    isMeasured: result.isMeasured,
                    kpiBindings: result.kpiBindings.map((b) => ({
                      kpiType: b.kpiType,
                      weight: b.weight,
                      isPlatformStandard: b.isPlatformStandard,
                    })),
                  }
                : tp,
            ),
          })),
        }
      })
    },
    [],
  )

  // ── Render ────────────────────────────────────────────────────────────────────

  const backLink = (
    <Link
      to={`/journeys/${journeyId}/builder`}
      className="inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground"
    >
      <ChevronLeft className="size-4 rtl:rotate-180" aria-hidden />
      {t("journey.backToBuilder")}
    </Link>
  )

  if (loading) {
    return (
      <div className="space-y-5 py-5">
        {backLink}
        <Skeleton className="h-24 w-full rounded-lg" />
        <Skeleton className="h-56 w-full rounded-lg" />
        <Skeleton className="h-40 w-full rounded-lg" />
      </div>
    )
  }

  if (loadError || !journey) {
    return (
      <div className="space-y-5 py-5">
        {backLink}
        <div className="flex flex-col items-center justify-center rounded-lg border border-border bg-card py-16 text-center shadow-sm dark:shadow-none">
          <AlertTriangle className="mb-4 size-12 text-muted-foreground" aria-hidden />
          <h3 className="mb-2 text-lg font-bold">{t("journey.builderLoadErrorTitle")}</h3>
          <p className="mb-4 max-w-sm text-muted-foreground">{t("journey.scoringLoadError")}</p>
          <Button variant="secondary" onClick={() => void load()}>
            {t("journey.retry")}
          </Button>
        </div>
      </div>
    )
  }

  const hasTouchpoints = journey.stages.some((s) => s.touchpoints.length > 0)

  return (
    <div className="space-y-5 py-5">
      {backLink}

      {/* Header card */}
      <Card>
        <CardContent>
          <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
            <div className="min-w-0 space-y-1.5">
              <div className="flex flex-wrap items-center gap-2.5">
                <SlidersHorizontal className="size-6 shrink-0 text-primary" aria-hidden />
                <h1 className="truncate text-2xl font-heading font-bold">
                  {t("journey.scoringPageTitle")}
                </h1>
                <JourneyStatusBadge status={journey.status} />
              </div>
              <p className="text-sm leading-relaxed text-muted-foreground">
                {t("journey.scoringPageSubtitle")}
              </p>
              <p className="truncate text-sm font-semibold text-foreground">{journey.name}</p>
            </div>
            {readOnly && (
              <span className="inline-flex shrink-0 items-center gap-1.5 text-sm text-muted-foreground">
                <Lock className="size-4" aria-hidden />
                {t("journey.readOnlyArchived")}
              </span>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Per-touchpoint KPI bindings */}
      <div className="space-y-1.5">
        <h2 className="text-lg font-bold">{t("journey.kpiBindingsTitle")}</h2>
        <p className="text-sm text-muted-foreground">{t("journey.kpiBindingsSubtitle")}</p>
      </div>

      {!hasTouchpoints ? (
        <div className="flex flex-col items-center justify-center rounded-lg border border-border bg-card py-16 text-center shadow-sm dark:shadow-none">
          <Layers className="mb-4 size-12 text-muted-foreground" aria-hidden />
          <h3 className="mb-2 text-lg font-bold">{t("journey.scoringNoStagesTitle")}</h3>
          <p className="mb-4 max-w-sm text-muted-foreground">{t("journey.scoringNoStagesHelp")}</p>
          <Link
            to={`/journeys/${journeyId}/builder`}
            className={cn(
              "inline-flex h-10 items-center rounded-md bg-primary px-4 text-sm font-medium text-primary-foreground transition-colors hover:bg-nb-cyan-700",
            )}
          >
            {t("journey.openBuilder")}
          </Link>
        </div>
      ) : (
        <div className="space-y-4">
          {journey.stages.map((stage) => (
            <Card key={stage.stageId}>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-base font-bold">
                  <span className="text-sm font-bold tabular-nums text-muted-foreground">
                    {stage.sequenceNumber}.0
                  </span>
                  <span className="truncate">{stage.name}</span>
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                {stage.touchpoints.length === 0 ? (
                  <p className="py-2 text-center text-sm text-muted-foreground">
                    {t("journey.scoringStageNoTouchpoints")}
                  </p>
                ) : (
                  stage.touchpoints.map((tp, tpIndex) => {
                    const kpiCount = tp.kpiBindings.length
                    const measured = tp.isMeasured && kpiCount > 0
                    return (
                      <div
                        key={tp.touchpointId}
                        className="rounded-md border border-border bg-card p-4"
                      >
                        <div className="mb-3 flex flex-wrap items-center gap-x-3 gap-y-1.5 border-b border-border pb-3">
                          <span className="shrink-0 text-xs font-bold tabular-nums text-muted-foreground">
                            {stage.sequenceNumber}.{tpIndex + 1}
                          </span>
                          <h3 className="min-w-0 flex-1 truncate text-sm font-semibold">{tp.name}</h3>
                          {measured ? (
                            <Badge className="border-transparent bg-d2-light text-[10px] text-d2-dark dark:bg-d2-dark/25 dark:text-d2-light">
                              <CircleCheck className="size-2.5" aria-hidden />
                              {kpiCount} {t(kpiCount === 1 ? "journey.kpiShort" : "journey.kpisShort")}
                            </Badge>
                          ) : (
                            <Badge className="border-transparent bg-d3-light text-[10px] text-d3-dark dark:bg-d3-dark/25 dark:text-d3-light">
                              <AlertTriangle className="size-2.5" aria-hidden />
                              {t("journey.noKpis")}
                            </Badge>
                          )}
                        </div>
                        <KpiWeightEditor
                          touchpointId={tp.touchpointId}
                          initialBindings={tp.kpiBindings}
                          kpiTypes={kpiTypes}
                          disabled={readOnly}
                          onSaved={(result) => handleTouchpointSaved(tp.touchpointId, result)}
                        />
                      </div>
                    )
                  })
                )}
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}
