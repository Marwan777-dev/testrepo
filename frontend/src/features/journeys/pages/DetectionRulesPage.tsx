// Detection Rules page (T094, US-4): the pain/happy detection configuration surface for a single
// journey. It loads the journey tree (`GET /journeys/{id}`) — which supplies the stages, their
// touchpoints, and each touchpoint's measured state — and composes the self-contained
// `DetectionThresholdEditor` (T093), which fetches and saves its own detection config
// (`GET`/`PUT /api/v1/journeys/{id}/detection`).
//
// The page owns only the journey load + the header chrome (title, status badge, read-only lock);
// the editor owns the thresholds, per-stage overrides, unmeasured callout, validation, and save.
// Archived journeys render read-only (the editor's controls are disabled). RTL-first,
// design-system styled (logical properties, brand cyan for chrome only). Reachable from the
// Journey Builder header ("Detection Rules") at `/journeys/:id/detection`.

import { useCallback, useEffect, useState } from "react"
import { Link, useParams } from "react-router"
import { useTranslation } from "react-i18next"
import { AlertTriangle, ChevronLeft, Lock, Target } from "lucide-react"

import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { getJourney, type JourneyDetail } from "@/features/journeys/api"
import { JourneyStatusBadge } from "@/features/journeys/components/JourneyStatusBadge"
import { DetectionThresholdEditor } from "@/features/journeys/components/DetectionThresholdEditor"

export default function DetectionRulesPage() {
  const { t } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const journeyId = id ?? ""

  const [journey, setJourney] = useState<JourneyDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(false)

  const readOnly = journey?.status === "Archived"

  // Initial load: the journey tree. The detection config itself is fetched by the editor (which
  // treats a 404 `journey.no_detection_config` as the "use defaults" path, not an error).
  const load = useCallback(async () => {
    if (!journeyId) return
    setLoading(true)
    setLoadError(false)
    try {
      const detail = await getJourney(journeyId)
      setJourney(detail)
    } catch {
      setLoadError(true)
    } finally {
      setLoading(false)
    }
  }, [journeyId])

  useEffect(() => {
    void load()
  }, [load])

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
        <Skeleton className="h-80 w-full rounded-lg" />
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
          <p className="mb-4 max-w-sm text-muted-foreground">{t("journey.detectionLoadError")}</p>
          <Button variant="secondary" onClick={() => void load()}>
            {t("journey.retry")}
          </Button>
        </div>
      </div>
    )
  }

  return (
    <div className="space-y-5 py-5">
      {backLink}

      {/* Header card */}
      <Card>
        <CardContent>
          <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
            <div className="min-w-0 space-y-1.5">
              <div className="flex flex-wrap items-center gap-2.5">
                <Target className="size-6 shrink-0 text-primary" aria-hidden />
                <h1 className="truncate text-2xl font-heading font-bold">
                  {t("journey.detectionPageTitle")}
                </h1>
                <JourneyStatusBadge status={journey.status} />
              </div>
              <p className="text-sm leading-relaxed text-muted-foreground">
                {t("journey.detectionPageSubtitle")}
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

      {/* Detection thresholds + per-stage overrides + unmeasured callout (T093). */}
      <DetectionThresholdEditor journeyId={journeyId} stages={journey.stages} disabled={readOnly} />
    </div>
  )
}
