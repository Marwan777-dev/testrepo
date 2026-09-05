// T123 [US5] — blocking confirmation dialog shown before deactivating a KPI that is still bound to
// M-16 touchpoints (FR-026). Presentational only: the parent (KpiConfigForm) pre-fetches the
// binding-usage counts, opens this dialog when they are > 0, and on Confirm issues the
// `PATCH /kpis/{id}/activation` with `confirm=true`. On Cancel the Active toggle is left unchanged.
// Reusable for any binding-usage confirmation (e.g. the FR-017 scale change) via the optional
// title / description / confirmLabel overrides.

import { useTranslation } from "react-i18next"
import { Loader2 } from "lucide-react"

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog"

export interface BindingUsageConfirmDialogProps {
  open: boolean
  /** Active touchpoints bound to the KPI (FR-026 confirmation copy). */
  touchpointCount: number
  /** Distinct non-archived journeys those touchpoints span. */
  journeyCount: number
  /** Disables the actions and shows a spinner while the activation request is in flight. */
  pending?: boolean
  onConfirm: () => void
  onCancel: () => void
  /** Override the default deactivation copy when reused for another binding-usage confirmation. */
  title?: string
  description?: string
  confirmLabel?: string
}

export function BindingUsageConfirmDialog({
  open,
  touchpointCount,
  journeyCount,
  pending = false,
  onConfirm,
  onCancel,
  title,
  description,
  confirmLabel,
}: BindingUsageConfirmDialogProps) {
  const { t } = useTranslation()

  return (
    <AlertDialog
      open={open}
      onOpenChange={(next) => {
        // Closing by Escape / scrim is a cancel — never an implicit confirm.
        if (!next && !pending) onCancel()
      }}
    >
      <AlertDialogContent data-testid="kpi-deactivate-dialog">
        <AlertDialogHeader>
          <AlertDialogTitle>{title ?? t("kpi.deactivateTitle")}</AlertDialogTitle>
          <AlertDialogDescription>
            {description ??
              t("kpi.deactivateMessage", { touchpoints: touchpointCount, journeys: journeyCount })}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel
            data-testid="kpi-deactivate-cancel"
            disabled={pending}
            onClick={(e) => {
              e.preventDefault()
              onCancel()
            }}
          >
            {t("common.cancel")}
          </AlertDialogCancel>
          <AlertDialogAction
            data-testid="kpi-deactivate-confirm"
            disabled={pending}
            onClick={(e) => {
              e.preventDefault()
              onConfirm()
            }}
            className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
          >
            {pending && <Loader2 className="size-4 ms-2 animate-spin" aria-hidden />}
            {confirmLabel ?? t("kpi.deactivateConfirm")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
