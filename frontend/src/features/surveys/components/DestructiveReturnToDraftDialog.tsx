// BR-1.6 blocking confirmation (T096): Return-to-Draft on an Active/Paused survey
// permanently deletes all N collected responses (incl. the M-07 post-expiry store) and
// invalidates in-flight respondent sessions. N comes from the 409
// `survey.return_to_draft.destructive_confirmation_required` payload
// (`details.responsesCount`); on confirm the caller re-sends with `confirm: true`.

import { useTranslation } from "react-i18next"
import { TriangleAlert } from "lucide-react"

import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"

export function DestructiveReturnToDraftDialog({
  open,
  responsesCount,
  busy,
  onConfirm,
  onCancel,
}: {
  open: boolean
  /** Exact count from the 409 payload — MUST be shown (spec § Error Handling). */
  responsesCount: number
  busy?: boolean
  onConfirm: () => void
  onCancel: () => void
}) {
  const { t } = useTranslation()
  return (
    <Dialog open={open} onOpenChange={(next) => !next && onCancel()}>
      <DialogContent className="flex max-h-[90vh] flex-col sm:max-w-md">
        <DialogHeader className="shrink-0">
          <DialogTitle className="flex items-center gap-2">
            <TriangleAlert className="size-5 shrink-0 text-destructive" aria-hidden />
            {t("surveysModule.returnToDraft.title")}
          </DialogTitle>
          <DialogDescription>{t("surveysModule.returnToDraft.question")}</DialogDescription>
        </DialogHeader>
        <div className="min-h-0 flex-1 overflow-y-auto px-1">
          <p className="text-sm leading-relaxed text-foreground" role="alert">
            {t("surveysModule.returnToDraft.warning", { count: responsesCount })}
          </p>
        </div>
        <DialogFooter className="shrink-0 gap-2 sm:gap-2">
          <Button variant="outline" onClick={onCancel} disabled={busy}>
            {t("common.cancel")}
          </Button>
          <Button variant="destructive" onClick={onConfirm} disabled={busy}>
            {t("surveysModule.returnToDraft.confirm")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
