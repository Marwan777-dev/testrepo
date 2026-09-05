// Survey status pill (T094) — D-level semantic tokens per the Two-Palette Rule:
// status is KPI-state signalling, never brand colour. Active=D1 (healthy/collecting),
// PendingReview=D2, Paused=D3 (caution), Archived=D5 muted (terminal), Draft=neutral.
// Colour is never the only indicator — the translated label always renders.

import { useTranslation } from "react-i18next"

import { Badge } from "@/components/ui/badge"
import { cn } from "@/lib/utils"
import type { SurveyStatus } from "../api/surveys-api"

const PILL_CLASSES: Record<SurveyStatus, string> = {
  Active: "bg-d1-light text-d1-dark dark:bg-d1-dark/25 dark:text-d1-light",
  PendingReview: "bg-d2-light text-d2-dark dark:bg-d2-dark/25 dark:text-d2-light",
  Paused: "bg-d3-light text-d3-dark dark:bg-d3-dark/25 dark:text-d3-light",
  Draft: "bg-muted text-muted-foreground",
  // Terminal state — D5 family but muted (no alarm urgency for an archived row).
  Archived: "bg-d5-light/60 text-d5-dark/80 dark:bg-d5-dark/20 dark:text-d5-light/70",
}

// Leading dot in the full-strength status colour — colour still never the only
// indicator (the label always renders alongside).
const DOT_CLASSES: Record<SurveyStatus, string> = {
  Active: "bg-d1",
  PendingReview: "bg-d2",
  Paused: "bg-d3",
  Draft: "bg-muted-foreground/60",
  Archived: "bg-d5/50",
}

const LABEL_KEYS: Record<SurveyStatus, string> = {
  Draft: "surveysModule.status.draft",
  PendingReview: "surveysModule.status.pendingReview",
  Active: "surveysModule.status.active",
  Paused: "surveysModule.status.paused",
  Archived: "surveysModule.status.archived",
}

export function SurveyStatusPill({
  status,
  className,
}: {
  status: SurveyStatus
  className?: string
}) {
  const { t } = useTranslation()
  return (
    <Badge className={cn("gap-1.5 border-transparent", PILL_CLASSES[status], className)}>
      <span
        className={cn("size-1.5 shrink-0 rounded-full", DOT_CLASSES[status])}
        aria-hidden
      />
      {t(LABEL_KEYS[status])}
    </Badge>
  )
}
