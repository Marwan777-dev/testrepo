// TEMPORARY scaffolding for the M-13 Integration Hub (T020).
//
// Each of the seven screens gets a real route now so navigation, the sidebar group, deep links,
// and the permission gate are all exercisable in the browser before the screens themselves exist.
// Every page here is replaced by its real implementation in the story that owns it:
//   SCR-03/04 → T038/T037 (US1) · SCR-05/06 → T062/T061 (US2) · SCR-01/02 → T087/T085 (US3)
//   SCR-08    → T134 (US5)      · SCR-07    → T164 (US6)
//
// Renders the design-system empty state (CLAUDE.md "Empty States"): an icon, the screen's real
// title and description, and a line naming what will live here — an empty state must teach the
// interface, never render blank.

import type { LucideIcon } from "lucide-react"
import { useTranslation } from "react-i18next"

import { Badge } from "@/components/ui/badge"

export interface ScreenPlaceholderProps {
  icon: LucideIcon
  /** Already-translated screen title (the page `h1`). */
  title: string
  /** Already-translated one-line description under the title. */
  description: string
  /** Already-translated line naming what this screen will contain. */
  upcoming: string
  /** Screen id from the SRS, e.g. "SCR-03" — rendered as a quiet reference chip. */
  screenId: string
}

export function ScreenPlaceholder({
  icon: Icon,
  title,
  description,
  upcoming,
  screenId,
}: ScreenPlaceholderProps) {
  const { t } = useTranslation()

  return (
    <div className="space-y-5 py-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
        <div className="min-w-0">
          <h1 className="font-heading text-2xl font-bold">{title}</h1>
          <p className="mt-1 text-sm text-muted-foreground">{description}</p>
        </div>
        <Badge variant="outline" className="w-fit font-mono">
          {screenId}
        </Badge>
      </div>

      <div className="flex flex-col items-center justify-center rounded-lg border border-border bg-card py-16 text-center shadow-sm dark:shadow-none">
        <Icon className="mb-4 size-12 text-muted-foreground" />
        <h2 className="mb-2 text-lg font-bold">{t("integrationHub.placeholder.title")}</h2>
        <p className="mb-4 max-w-sm text-sm leading-relaxed text-muted-foreground">{upcoming}</p>
        <p className="max-w-sm text-sm text-muted-foreground">
          {t("integrationHub.placeholder.hint")}
        </p>
      </div>
    </div>
  )
}
