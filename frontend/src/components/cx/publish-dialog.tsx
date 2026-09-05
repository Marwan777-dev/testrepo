import { useTranslation } from "react-i18next"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { GitBranch, AlertTriangle, ChevronRight } from "lucide-react"
import { cn } from "@/lib/utils"

// Each pending change carries enough metadata to render a human-readable
// list AND to drive the Major/Minor classification per FR-4.3 / SRS §3.4.1.
export interface PendingChange {
  type:
    | "add_stage"
    | "remove_stage"
    | "reorder_stages"
    | "add_tp"
    | "remove_tp"
    | "add_kpi"
    | "remove_kpi"
    | "change_kpi_weights"
    | "change_channels"
    | "rename"
    | "edit_description"
    | "toggle_mot"
    | "change_importance"
    | "change_weight"
  // Major or Minor — classification is intrinsic to the change type per the
  // SRS table; we surface it on the diff so reviewers see why a publish is
  // bumping major vs minor.
  classification: "major" | "minor"
  // Human-readable label resolved at construction time so we don't try to
  // template here.
  description: string
}

interface PublishDialogProps {
  open: boolean
  onClose: () => void
  currentVersion: string // "—" if never published
  pendingChanges: PendingChange[]
  onConfirm: () => void
}

function nextVersion(current: string, hasMajor: boolean): string {
  if (current === "—") return "1.0"
  const [maj, min] = current.split(".").map((s) => parseInt(s, 10))
  if (hasMajor) return `${maj + 1}.0`
  return `${maj}.${min + 1}`
}

export function PublishDialog({
  open,
  onClose,
  currentVersion,
  pendingChanges,
  onConfirm,
}: PublishDialogProps) {
  const { t } = useTranslation()

  const majorChanges = pendingChanges.filter((c) => c.classification === "major")
  const minorChanges = pendingChanges.filter((c) => c.classification === "minor")
  const hasMajor = majorChanges.length > 0
  const resulting = nextVersion(currentVersion, hasMajor)
  const empty = pendingChanges.length === 0

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <GitBranch className="size-5 text-nb-cyan" />
            {t("journey.publishReview")}
          </DialogTitle>
          <DialogDescription>{t("journey.publishReviewDesc")}</DialogDescription>
        </DialogHeader>

        {/* ── Version arrow visual ───────────── */}
        <div className="flex items-center justify-center gap-3 rounded-md bg-muted/50 px-4 py-4">
          <div className="text-center">
            <p className="text-[10px] uppercase tracking-widest text-muted-foreground font-medium">
              {t("journey.colVersion")}
            </p>
            <p className="text-xl font-heading font-bold tabular-nums text-muted-foreground mt-0.5">
              {currentVersion === "—" ? "—" : `v${currentVersion}`}
            </p>
          </div>
          <ChevronRight className="size-5 text-muted-foreground rtl:rotate-180" />
          <div className="text-center">
            <p className="text-[10px] uppercase tracking-widest text-nb-cyan-700 dark:text-nb-cyan-300 font-medium">
              {t("journey.publishResultingVersion")}
            </p>
            <p className="text-xl font-heading font-bold tabular-nums text-nb-cyan mt-0.5">
              v{resulting}
            </p>
          </div>
        </div>

        {/* ── Change counts ──────────────────── */}
        {!empty && (
          <div className="flex flex-wrap gap-2">
            {hasMajor && (
              <Badge
                variant="outline"
                className="text-xs py-1 px-2.5 bg-d4-light text-d4-dark border-d4/20 dark:bg-d4-dark/20 dark:text-d4-light"
              >
                <span className="font-bold tabular-nums me-1">
                  {majorChanges.length}
                </span>
                {majorChanges.length === 1
                  ? t("journey.publishMajorCount", { count: majorChanges.length })
                      .replace(`${majorChanges.length} `, "")
                  : t("journey.publishMajorCountPlural", {
                      count: majorChanges.length,
                    }).replace(`${majorChanges.length} `, "")}
              </Badge>
            )}
            {minorChanges.length > 0 && (
              <Badge
                variant="outline"
                className="text-xs py-1 px-2.5 bg-d2-light text-d2-dark border-d2/20 dark:bg-d2-dark/20 dark:text-d2-light"
              >
                <span className="font-bold tabular-nums me-1">
                  {minorChanges.length}
                </span>
                {minorChanges.length === 1
                  ? t("journey.publishMinorCount", { count: minorChanges.length })
                      .replace(`${minorChanges.length} `, "")
                  : t("journey.publishMinorCountPlural", {
                      count: minorChanges.length,
                    }).replace(`${minorChanges.length} `, "")}
              </Badge>
            )}
          </div>
        )}

        {/* ── Change diff list ───────────────── */}
        {empty ? (
          <div className="flex items-center gap-2 rounded-md border border-dashed border-border bg-muted/30 px-3 py-4 text-center">
            <AlertTriangle className="size-4 text-d3 shrink-0" />
            <p className="text-xs text-muted-foreground flex-1 text-start">
              {t("journey.noChanges")}
            </p>
          </div>
        ) : (
          <div className="space-y-1 max-h-64 overflow-y-auto rounded-md border border-border">
            <p className="text-[10px] uppercase tracking-widest text-muted-foreground font-medium px-3 pt-3 pb-1">
              {t("journey.reasonsForChange")}
            </p>
            {pendingChanges.map((c, i) => (
              <div
                key={i}
                className="flex items-center gap-2.5 px-3 py-1.5 text-xs"
              >
                <span
                  className={cn(
                    "size-1.5 rounded-full shrink-0",
                    c.classification === "major" ? "bg-d4" : "bg-d2",
                  )}
                />
                <span className="flex-1 text-foreground">{c.description}</span>
                <Badge
                  variant="outline"
                  className={cn(
                    "text-[9px] uppercase tracking-wide py-0.5 px-1.5 font-bold",
                    c.classification === "major"
                      ? "bg-d4-light text-d4-dark border-d4/20 dark:bg-d4-dark/20 dark:text-d4-light"
                      : "bg-d2-light text-d2-dark border-d2/20 dark:bg-d2-dark/20 dark:text-d2-light",
                  )}
                >
                  {c.classification === "major" ? "Major" : "Minor"}
                </Badge>
              </div>
            ))}
          </div>
        )}

        <DialogFooter className="gap-2 sm:gap-2">
          <Button variant="outline" onClick={onClose}>
            {t("journey.publishCancel")}
          </Button>
          <Button
            className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            disabled={empty}
            onClick={() => {
              onConfirm()
              onClose()
            }}
          >
            {t("journey.publishConfirm", { version: resulting })}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
