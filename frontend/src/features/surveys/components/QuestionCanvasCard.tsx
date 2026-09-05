// Canvas question card (clickthrough parity): grip handle, question text with the
// required star, description, badge row (type / KPI chip / required / comments /
// reason / "Routing set"), a live type-accurate QuestionPreview, the optional
// comment-box mock and reason box, plus routing + delete actions. Clicking the card
// selects it — settings edit in the right config panel. Sortable via @dnd-kit
// (id `question:{localId}`), used for standalone questions AND set members so
// questions can drag between sections, into and out of sets.

import { useSortable } from "@dnd-kit/sortable"
import { CSS } from "@dnd-kit/utilities"
import { useTranslation } from "react-i18next"
import {
  Gauge,
  GitBranch,
  GripVertical,
  Lightbulb,
  Pilcrow,
  Trash2,
} from "lucide-react"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { cn } from "@/lib/utils"
import { isRoutingEligible, type BuilderQuestion } from "./builder-types"
import { QuestionPreview } from "./QuestionPreview"

const TYPE_LABEL_KEYS: Record<BuilderQuestion["type"], string> = {
  Scale: "surveysModule.palette.scale",
  InputField: "surveysModule.palette.inputField",
  SingleSelect: "surveysModule.palette.singleSelect",
  MultiSelect: "surveysModule.palette.multiSelect",
  YesNo: "surveysModule.palette.yesNo",
  Matrix: "surveysModule.palette.matrix",
  Ranking: "surveysModule.palette.ranking",
  Kpi: "surveysModule.palette.kpi",
  Paragraph: "surveysModule.palette.paragraph",
}

export function QuestionCanvasCard({
  question: q,
  selected,
  insideSet,
  routingOn,
  onSelect,
  onEditRouting,
  onRemove,
  disabled,
}: {
  question: BuilderQuestion
  selected: boolean
  insideSet: boolean
  routingOn: boolean
  onSelect: () => void
  onEditRouting: () => void
  onRemove: () => void
  disabled?: boolean
}) {
  const { t } = useTranslation()
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: `question:${q.localId}`,
  })
  const routable = routingOn && !insideSet && isRoutingEligible(q.type, q.subType, insideSet)

  // ── Paragraph element — compact muted card ──────────────────────────────────
  if (q.type === "Paragraph") {
    return (
      <div
        ref={setNodeRef}
        style={{ transform: CSS.Transform.toString(transform), transition }}
        onClick={onSelect}
        className={cn(
          "flex cursor-pointer items-start gap-2 rounded-lg border bg-muted/30 p-4 transition-colors",
          selected ? "border-primary ring-2 ring-primary/20" : "border-border hover:border-primary/40",
          isDragging && "opacity-40"
        )}
      >
        <button
          type="button"
          className="mt-0.5 cursor-grab touch-none text-muted-foreground/50 hover:text-foreground"
          aria-label={t("surveysModule.builder.dragQuestion")}
          {...listeners}
          {...attributes}
        >
          <GripVertical className="size-4" aria-hidden />
        </button>
        <Pilcrow className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden />
        <p className="min-w-0 flex-1 whitespace-pre-wrap text-sm text-muted-foreground">
          {q.text || t("surveysModule.config.paragraphTitle")}
        </p>
        <Button
          variant="ghost"
          size="icon-sm"
          className="hover:bg-destructive/10 hover:text-destructive"
          aria-label={t("surveysModule.config.deleteParagraph")}
          onClick={(e) => {
            e.stopPropagation()
            onRemove()
          }}
          disabled={disabled}
        >
          <Trash2 className="size-4" aria-hidden />
        </Button>
      </div>
    )
  }

  return (
    <div
      ref={setNodeRef}
      style={{ transform: CSS.Transform.toString(transform), transition }}
      onClick={onSelect}
      className={cn(
        "flex cursor-pointer items-start gap-2 rounded-lg border bg-card p-4 transition-all",
        selected
          ? "border-primary ring-2 ring-primary/20"
          : "border-border hover:border-primary/40 hover:shadow-sm",
        isDragging && "opacity-40"
      )}
    >
      <button
        type="button"
        className="mt-0.5 cursor-grab touch-none text-muted-foreground/50 hover:text-foreground"
        aria-label={t("surveysModule.builder.dragQuestion")}
        {...listeners}
        {...attributes}
      >
        <GripVertical className="size-4" aria-hidden />
      </button>

      <div className="min-w-0 flex-1 space-y-2">
        <p className="text-sm font-semibold">
          {q.text || t("surveysModule.builder.untitledQuestion")}
          {q.required && <span className="text-destructive"> *</span>}
        </p>
        {q.description && (
          <p className="text-xs text-muted-foreground">{q.description}</p>
        )}

        {/* Badge row */}
        <div className="flex flex-wrap items-center gap-1.5">
          {q.type === "Kpi" ? (
            <>
              <Badge className="gap-1 border-transparent bg-nb-mint-100 text-nb-mint-800 dark:bg-nb-mint-900/40 dark:text-nb-mint-200">
                <Gauge className="size-3" aria-hidden />
                {t("surveysModule.palette.kpi")}
                {q.kpi.kpiKey ? ` · ${q.kpi.kpiKey}` : ""}
              </Badge>
              {q.kpi.touchpointDisplay && (
                <Badge variant="outline">{q.kpi.touchpointDisplay}</Badge>
              )}
            </>
          ) : (
            <Badge variant="outline" className="text-muted-foreground">
              {t(TYPE_LABEL_KEYS[q.type])}
            </Badge>
          )}
          {q.required && <Badge variant="outline">{t("surveysModule.question.required")}</Badge>}
          {q.showComments && (
            <Badge variant="outline">{t("surveysModule.question.commentsBadge")}</Badge>
          )}
          {q.type === "Kpi" && q.reason.on && (
            <Badge variant="outline">{t("surveysModule.config.reasonBadge")}</Badge>
          )}
          {q.hasRoutingMap && (
            <Badge className="gap-1 border-transparent bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200">
              <GitBranch className="size-3" aria-hidden />
              {t("surveysModule.question.routingBadge")}
            </Badge>
          )}
        </div>

        {/* Live preview */}
        <QuestionPreview question={q} />

        {q.showComments && (
          <Input
            disabled
            placeholder={t("surveysModule.config.commentMockPlaceholder")}
            className="h-9"
          />
        )}
        {q.type === "Kpi" && q.reason.on && (
          <div className="flex items-center gap-2 rounded-md bg-muted/50 px-3 py-2 text-xs text-muted-foreground">
            <Lightbulb className="size-3.5 shrink-0" aria-hidden />
            {q.reason.prompt || t("surveysModule.config.reasonFallback")}
          </div>
        )}
      </div>

      {/* Action column */}
      <div className="flex flex-col gap-1" onClick={(e) => e.stopPropagation()}>
        {routable && (
          <Button
            variant="ghost"
            size="icon-sm"
            aria-label={t("surveysModule.routing.editAria")}
            className={cn(q.hasRoutingMap && "text-nb-cyan-700 dark:text-nb-cyan-300")}
            onClick={onEditRouting}
            disabled={disabled}
          >
            <GitBranch className="size-4" aria-hidden />
          </Button>
        )}
        <Button
          variant="ghost"
          size="icon-sm"
          className="hover:bg-destructive/10 hover:text-destructive"
          aria-label={t("surveysModule.question.remove")}
          onClick={onRemove}
          disabled={disabled}
        >
          <Trash2 className="size-4" aria-hidden />
        </Button>
      </div>
    </div>
  )
}
