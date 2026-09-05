// F1 "Change status" picker (clickthrough parity) — a small dialog that lists the
// self-serve lifecycle states (Draft / Active / Paused) with the current one marked
// "Current" and each valid transition offering "Set". Picking a target delegates to
// the page's doStatusChange, so the existing pause-with-rules (FR-1.10) and
// return-to-draft (BR-1.6) confirmations still fire underneath. Archive / Unarchive
// stay as their own menu action, per the reference help text.

import { useTranslation } from "react-i18next"

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { cn } from "@/lib/utils"
import type { SurveyStatus } from "../api/surveys-api"
import { SurveyStatusPill } from "./SurveyStatusPill"

const OPTIONS: SurveyStatus[] = ["Draft", "Active", "Paused"]

export function ChangeStatusDialog({
  survey,
  validTargets,
  onPick,
  onClose,
}: {
  survey: { id: string; status: SurveyStatus; name: string } | null
  /** Statuses reachable from the current one (BR-1.4); others render disabled. */
  validTargets: SurveyStatus[]
  onPick: (target: SurveyStatus) => void
  onClose: () => void
}) {
  const { t } = useTranslation()

  return (
    <Dialog open={!!survey} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t("surveysModule.actions.changeStatus")}</DialogTitle>
          <DialogDescription>{survey?.name}</DialogDescription>
        </DialogHeader>

        <div className="space-y-2">
          {OPTIONS.map((st) => {
            const current = survey?.status === st
            const selectable = !current && validTargets.includes(st)
            return (
              <button
                key={st}
                type="button"
                disabled={!selectable}
                onClick={() => selectable && onPick(st)}
                className={cn(
                  "flex w-full items-center justify-between rounded-md border px-4 py-3 text-sm transition-colors",
                  current
                    ? "border-primary bg-primary/5"
                    : selectable
                      ? "border-border hover:bg-accent"
                      : "cursor-not-allowed border-border opacity-50",
                )}
              >
                <SurveyStatusPill status={st} />
                <span
                  className={cn(
                    "text-xs font-medium",
                    current ? "text-primary" : "text-muted-foreground",
                  )}
                >
                  {current
                    ? t("surveysModule.changeStatus.current")
                    : selectable
                      ? t("surveysModule.changeStatus.set")
                      : ""}
                </span>
              </button>
            )
          })}
        </div>

        <p className="text-xs leading-relaxed text-muted-foreground">
          {t("surveysModule.changeStatus.help")}
        </p>
      </DialogContent>
    </Dialog>
  )
}
