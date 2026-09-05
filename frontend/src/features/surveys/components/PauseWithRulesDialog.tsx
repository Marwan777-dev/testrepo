// FR-1.10 blocking confirmation (T097): pausing an Active survey that active M-02
// distribution rules still target. The exact rule count comes from the 409
// `survey.pause.requires_rules_confirmation` payload (`details.rulesCount`); on
// confirm the caller re-sends the Pause with `confirm: true`.

import { useTranslation } from "react-i18next"
import { CirclePause } from "lucide-react"

import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"

export function PauseWithRulesDialog({
  open,
  rulesCount,
  busy,
  onConfirm,
  onCancel,
}: {
  open: boolean
  /** Exact count from the 409 payload — shown verbatim per FR-1.10. */
  rulesCount: number
  busy?: boolean
  onConfirm: () => void
  onCancel: () => void
}) {
  const { t } = useTranslation()
  return (
    <Dialog open={open} onOpenChange={(next) => !next && onCancel()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <CirclePause className="size-5 shrink-0 text-d3-dark dark:text-d3-light" aria-hidden />
            {t("surveysModule.pauseRules.title")}
          </DialogTitle>
          <DialogDescription>
            {t("surveysModule.pauseRules.body", { count: rulesCount })}
          </DialogDescription>
        </DialogHeader>
        <DialogFooter className="gap-2 sm:gap-2">
          <Button variant="outline" onClick={onCancel} disabled={busy}>
            {t("common.cancel")}
          </Button>
          <Button onClick={onConfirm} disabled={busy}>
            {t("surveysModule.pauseRules.confirm")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
