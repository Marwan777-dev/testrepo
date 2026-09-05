// Q1 stale-ETag conflict handler (T099): another editor saved first (normal under Q8
// team-owned Drafts). Offers "Reload latest" (refetch + rebind the form) and "Copy my
// changes" (local form values → clipboard so nothing is lost before reloading).

import { useState } from "react"
import { useTranslation } from "react-i18next"
import { Check, Copy, RefreshCw } from "lucide-react"

import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"

export function EtagConflictDialog({
  open,
  /** The local (unsaved) form values, serialised for the clipboard copy action. */
  localValues,
  busy,
  onReload,
  onDismiss,
}: {
  open: boolean
  localValues: Record<string, unknown>
  busy?: boolean
  onReload: () => void
  onDismiss: () => void
}) {
  const { t } = useTranslation()
  const [copied, setCopied] = useState(false)

  const copyChanges = async () => {
    try {
      await navigator.clipboard.writeText(JSON.stringify(localValues, null, 2))
      setCopied(true)
    } catch {
      // Clipboard may be unavailable (permissions); the button simply stays un-ticked.
    }
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) {
          setCopied(false)
          onDismiss()
        }
      }}
    >
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t("surveysModule.conflict.title")}</DialogTitle>
          <DialogDescription>{t("surveysModule.conflict.body")}</DialogDescription>
        </DialogHeader>
        <DialogFooter className="gap-2 sm:gap-2">
          <Button variant="outline" onClick={copyChanges} disabled={busy}>
            {copied ? (
              <Check className="size-4" aria-hidden />
            ) : (
              <Copy className="size-4" aria-hidden />
            )}
            {copied ? t("surveysModule.conflict.copied") : t("surveysModule.conflict.copy")}
          </Button>
          <Button
            onClick={() => {
              setCopied(false)
              onReload()
            }}
            disabled={busy}
          >
            <RefreshCw className="size-4" aria-hidden />
            {t("surveysModule.conflict.reload")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
