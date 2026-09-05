// Read-only viewer for a published journey version snapshot (T079, US-3). Renders the frozen
// `snapshot_payload` returned by `GET /journeys/{id}/versions/{n}` — the journey tree exactly as
// captured at publish time. A prominent banner makes the `isSnapshot` state unmistakable (this is a
// historical record, not the live journey, and nothing here is editable). Embedded inside the
// Version History page's Sheet; takes only the snapshot object so it can be reused elsewhere later.
//
// The snapshot keys the journey type as `type` and KPI bindings as `type` (the serializer's shape),
// distinct from the live journey-detail tree — see `dto/journey-version-snapshot.ts`.

import { useTranslation } from "react-i18next"
import { Layers, Lock, SlidersHorizontal, Star, Target } from "lucide-react"

import { Badge } from "@/components/ui/badge"
import { JourneyStatusBadge } from "@/features/journeys/components/JourneyStatusBadge"
import type { JourneyStatus, JourneyVersionSnapshot } from "@/features/journeys/api"

// Localized labels for the snapshot's free-form / enum-ish string fields (fall back to the raw value).
const TYPE_LABEL: Record<string, string> = {
  Transactional: "journey.typeTransactional",
  Lifecycle: "journey.typeLifecycle",
  IssueResolution: "journey.typeIssueResolution",
  Onboarding: "journey.typeOnboarding",
}
const IMPORTANCE_LABEL: Record<string, string> = {
  Low: "journey.impLow",
  Medium: "journey.impMedium",
  High: "journey.impHigh",
  Critical: "journey.impCritical",
}

export function VersionSnapshotViewer({ snapshot }: { snapshot: JourneyVersionSnapshot }) {
  const { t } = useTranslation()

  const typeText = snapshot.type
    ? TYPE_LABEL[snapshot.type]
      ? t(TYPE_LABEL[snapshot.type])
      : snapshot.type
    : null

  const scoring = snapshot.scoringConfig
  const detection = snapshot.detectionConfig
  // Tenant scoring parameters (SRS §4.2.9 / §11.7) captured at publish — technical symbols are
  // language-neutral, so no per-param i18n key is needed. β is derived (1 − α).
  const scoringParams = scoring
    ? ([
        ["α (Alpha)", scoring.alpha],
        ["β (Beta)", scoring.beta],
        ["MOT", scoring.motMultiplier],
        ["n_floor", scoring.nFloor],
        ["Flag %", scoring.flagPercentile],
        ["Window (days)", scoring.rollingWindowDays],
      ] as const).filter(([, v]) => v != null)
    : []

  return (
    <div className="space-y-5">
      {/* isSnapshot indicator — makes the read-only/historical nature unmistakable. */}
      <div className="flex items-start gap-2.5 rounded-md border border-nb-cyan-200 bg-nb-cyan-100 px-3 py-2.5 text-nb-cyan-800 dark:border-nb-cyan-900/60 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200">
        <Lock className="mt-0.5 size-4 shrink-0" aria-hidden />
        <div className="min-w-0">
          <p className="text-sm font-semibold">
            {t("journey.snapshotReadOnly")} · {t("journey.versionLabel", { number: snapshot.snapshotVersion })}
          </p>
          <p className="text-sm leading-relaxed">
            {t("journey.snapshotBanner", { version: snapshot.snapshotVersion })}
          </p>
        </div>
      </div>

      {/* Journey header */}
      <div className="space-y-2">
        <h2 className="text-lg font-heading font-bold">{snapshot.name}</h2>
        <div className="flex flex-wrap items-center gap-2">
          {snapshot.status && <JourneyStatusBadge status={snapshot.status as JourneyStatus} />}
          {typeText && (
            <Badge
              variant="outline"
              className="border-nb-cyan-200 bg-nb-cyan-100 text-nb-cyan-800 dark:border-nb-cyan-900/60 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200"
            >
              {typeText}
            </Badge>
          )}
        </div>
        {snapshot.description && (
          <p className="text-sm leading-relaxed text-muted-foreground">{snapshot.description}</p>
        )}
      </div>

      {/* Scoring configuration */}
      <section className="space-y-2 rounded-md border border-border bg-card p-4">
        <h3 className="flex items-center gap-2 text-sm font-bold">
          <SlidersHorizontal className="size-4 text-primary" aria-hidden />
          {t("journey.scoringConfigTitle")}
        </h3>
        {scoring && scoringParams.length > 0 ? (
          <dl className="grid grid-cols-1 gap-x-6 gap-y-1.5 text-sm sm:grid-cols-2">
            {scoringParams.map(([label, value]) => (
              <div key={label} className="flex items-center justify-between gap-2">
                <dt className="text-muted-foreground" dir="ltr">{label}</dt>
                <dd className="font-medium tabular-nums" dir="ltr">{value}</dd>
              </div>
            ))}
          </dl>
        ) : (
          <p className="text-sm text-muted-foreground">{t("journey.snapshotNoScoring")}</p>
        )}
      </section>

      {/* Detection thresholds */}
      {detection && (detection.painThreshold != null || detection.happyThreshold != null) && (
        <section className="space-y-2 rounded-md border border-border bg-card p-4">
          <h3 className="flex items-center gap-2 text-sm font-bold">
            <Target className="size-4 text-primary" aria-hidden />
            {t("journey.detectionTitle")}
          </h3>
          <dl className="grid grid-cols-1 gap-x-6 gap-y-1.5 text-sm sm:grid-cols-2">
            <div className="flex items-center justify-between gap-2">
              <dt className="text-muted-foreground">{t("journey.painThreshold")}</dt>
              <dd className="font-medium tabular-nums">{detection.painThreshold ?? "—"}</dd>
            </div>
            <div className="flex items-center justify-between gap-2">
              <dt className="text-muted-foreground">{t("journey.happyThreshold")}</dt>
              <dd className="font-medium tabular-nums">{detection.happyThreshold ?? "—"}</dd>
            </div>
          </dl>
        </section>
      )}

      {/* Stages → touchpoints → KPI bindings */}
      <section className="space-y-3">
        <h3 className="flex items-center gap-2 text-sm font-bold">
          <Layers className="size-4 text-primary" aria-hidden />
          {t("journey.stagesUnit")}
        </h3>
        {snapshot.stages.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("journey.snapshotNoStages")}</p>
        ) : (
          snapshot.stages.map((stage) => (
            <div key={stage.stageId} className="rounded-md border border-border bg-card p-4">
              <div className="flex items-baseline gap-2">
                <span className="text-sm font-bold tabular-nums text-muted-foreground">
                  {stage.sequenceNumber}.0
                </span>
                <h4 className="min-w-0 flex-1 truncate text-sm font-semibold">{stage.name}</h4>
              </div>
              {(stage.customerGoal || stage.expectedEmotion || stage.durationHint) && (
                <div className="mt-1.5 flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">
                  {stage.customerGoal && (
                    <span>
                      {t("journey.customerGoal")}: <span className="text-foreground">{stage.customerGoal}</span>
                    </span>
                  )}
                  {stage.expectedEmotion && (
                    <span>
                      {t("journey.stageEmotion")}: <span className="text-foreground">{stage.expectedEmotion}</span>
                    </span>
                  )}
                  {stage.durationHint && (
                    <span>
                      {t("journey.expectedDuration")}: <span className="text-foreground">{stage.durationHint}</span>
                    </span>
                  )}
                </div>
              )}

              <div className="mt-3 space-y-2">
                {stage.touchpoints.length === 0 ? (
                  <p className="text-sm text-muted-foreground">{t("journey.noTouchpoints")}</p>
                ) : (
                  stage.touchpoints.map((tp, tpIndex) => {
                    const importanceText = tp.importance
                      ? IMPORTANCE_LABEL[tp.importance]
                        ? t(IMPORTANCE_LABEL[tp.importance])
                        : tp.importance
                      : null
                    return (
                      <div key={tp.touchpointId} className="rounded-md border border-border bg-background p-3">
                        <div className="flex flex-wrap items-center gap-x-2 gap-y-1.5">
                          <span className="shrink-0 text-xs font-bold tabular-nums text-muted-foreground">
                            {stage.sequenceNumber}.{tpIndex + 1}
                          </span>
                          <span className="min-w-0 flex-1 truncate text-sm font-semibold">{tp.name}</span>
                          {tp.isMot && (
                            <Badge
                              variant="outline"
                              className="border-nb-cyan-200 text-nb-cyan-800 dark:border-nb-cyan-900/60 dark:text-nb-cyan-200"
                            >
                              <Star className="size-3" aria-hidden />
                              {t("journey.mot")}
                            </Badge>
                          )}
                          {tp.isMandatory && <Badge variant="outline">{t("journey.mandatory")}</Badge>}
                          {importanceText && (
                            <Badge variant="outline" className="text-muted-foreground">
                              {importanceText}
                            </Badge>
                          )}
                        </div>

                        {tp.channels && tp.channels.length > 0 && (
                          <p className="mt-1.5 text-xs text-muted-foreground">
                            {t("journey.channels")}: <span className="text-foreground">{tp.channels.join("، ")}</span>
                          </p>
                        )}

                        {tp.kpiBindings.length > 0 ? (
                          <div className="mt-2 flex flex-wrap gap-1.5">
                            {tp.kpiBindings.map((kpi) => (
                              <span
                                key={kpi.type}
                                className="inline-flex items-center gap-1.5 rounded-full bg-muted px-2.5 py-0.5 text-xs"
                              >
                                <span className="font-medium">{kpi.type}</span>
                                <span className="tabular-nums text-muted-foreground">{kpi.weight}%</span>
                              </span>
                            ))}
                          </div>
                        ) : (
                          <p className="mt-2 text-xs text-muted-foreground">{t("journey.noKpis")}</p>
                        )}
                      </div>
                    )
                  })
                )}
              </div>
            </div>
          ))
        )}
      </section>
    </div>
  )
}
