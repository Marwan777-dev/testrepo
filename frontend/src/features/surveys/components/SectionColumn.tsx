// F2/F8 section container (clickthrough parity): header with the mint dot, inline
// name input, "Section N" badge, counts and the ⋮ menu (Add paragraph / Add section /
// Delete section — delete refuses while it is the only section). The body renders
// full QuestionCanvasCards in a SortableContext followed by QuestionsSetCards, and
// stays a droppable (`section:{localId}`) for palette drops and cross-section moves.
// The FR-2.5 destructive-confirmation dialog lists the 409 cascade breakdown
// (standalone questions / sets / set questions) before the cascade runs.

import { useState } from "react"
import { useDroppable } from "@dnd-kit/core"
import { SortableContext, verticalListSortingStrategy } from "@dnd-kit/sortable"
import { useTranslation } from "react-i18next"
import { MoreVertical, Pilcrow, Plus, Trash2, TriangleAlert } from "lucide-react"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { buttonVariants } from "@/components/ui/button"
import { cn } from "@/lib/utils"
import type { BuilderQuestion, BuilderSection, BuilderSet } from "./builder-types"
import { QuestionCanvasCard } from "./QuestionCanvasCard"
import { QuestionsSetCard } from "./QuestionsSetCard"

/** Cascade counts from the 409 `section.delete.requires_confirmation` details. */
export interface SectionCascadeCounts {
  standaloneQuestions: number
  questionsSets: number
  setQuestions: number
}

export function SectionColumn({
  section,
  index,
  selectedId,
  routingOn,
  canDelete,
  onRename,
  onDelete,
  onAddParagraph,
  onAddSectionAfter,
  onOpenSetSettings,
  onAddQuestionToSet,
  onRemoveSet,
  onSelectQuestion,
  onEditRouting,
  onRemoveQuestion,
  disabled,
}: {
  section: BuilderSection
  /** Zero-based position — renders the "Section N" badge. */
  index: number
  selectedId: string | null
  routingOn: boolean
  /** False while this is the only section (the reference refuses the delete). */
  canDelete: boolean
  onRename: (title: string) => void
  /**
   * Deletes the section. `confirm=false` first — returns cascade counts when the
   * section is non-empty (FR-2.5), null when the delete went through.
   */
  onDelete: (confirm: boolean) => Promise<SectionCascadeCounts | null>
  onAddParagraph: () => void
  onAddSectionAfter: () => void
  onOpenSetSettings: (set: BuilderSet) => void
  onAddQuestionToSet: (set: BuilderSet) => void
  onRemoveSet: (set: BuilderSet) => void
  onSelectQuestion: (q: BuilderQuestion) => void
  onEditRouting: (q: BuilderQuestion) => void
  onRemoveQuestion: (localId: string) => void
  disabled?: boolean
}) {
  const { t } = useTranslation()
  const { setNodeRef, isOver } = useDroppable({ id: `section:${section.localId}` })
  const [cascade, setCascade] = useState<SectionCascadeCounts | null>(null)
  const [deleting, setDeleting] = useState(false)

  const requestDelete = async (confirm: boolean) => {
    setDeleting(true)
    try {
      const counts = await onDelete(confirm)
      setCascade(counts ?? null)
    } finally {
      setDeleting(false)
    }
  }

  const questionCount = section.questions.filter((q) => q.type !== "Paragraph").length

  return (
    <div
      ref={setNodeRef}
      className={cn(
        "rounded-lg border border-border bg-card shadow-sm transition-colors dark:shadow-none",
        isOver && "border-primary"
      )}
    >
      {/* Header */}
      <div className="flex items-center gap-2 border-b border-border px-4 py-2.5">
        <span className="size-2.5 shrink-0 rounded-full bg-nb-mint" aria-hidden />
        <input
          value={section.title}
          onChange={(e) => onRename(e.target.value)}
          aria-label={t("surveysModule.builder.sectionTitle")}
          placeholder={t("surveysModule.builder.sectionTitle")}
          disabled={disabled}
          className="min-w-0 flex-1 -mx-1.5 rounded-md bg-transparent px-1.5 py-0.5 text-sm font-bold outline-none transition-colors focus:bg-muted/70"
        />
        <Badge variant="outline" className="shrink-0 text-muted-foreground">
          {t("surveysModule.builder.sectionBadge", { n: index + 1 })}
        </Badge>
        <span className="hidden shrink-0 text-xs text-muted-foreground sm:block">
          {t("surveysModule.builder.sectionCounts", {
            questions: questionCount,
            sets: section.sets.length,
          })}
        </span>
        <DropdownMenu>
          <DropdownMenuTrigger
            className={cn(buttonVariants({ variant: "ghost", size: "icon-sm" }))}
            aria-label={t("surveysModule.builder.sectionMenu")}
          >
            <MoreVertical className="size-4" aria-hidden />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onClick={onAddParagraph} disabled={disabled}>
              <Pilcrow className="size-4" aria-hidden />
              {t("surveysModule.builder.addParagraph")}
            </DropdownMenuItem>
            <DropdownMenuItem onClick={onAddSectionAfter} disabled={disabled}>
              <Plus className="size-4" aria-hidden />
              {t("surveysModule.builder.addSection")}
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem
              variant="destructive"
              onClick={() => void requestDelete(false)}
              disabled={disabled || deleting || !canDelete}
            >
              <Trash2 className="size-4" aria-hidden />
              {t("surveysModule.builder.removeSection")}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>

      {/* Body */}
      <div className="min-h-16 space-y-3 p-4">
        {section.questions.length === 0 && section.sets.length === 0 ? (
          <p className="rounded-md border border-dashed border-border p-6 text-center text-sm text-muted-foreground">
            {t("surveysModule.builder.dropHint")}
          </p>
        ) : (
          <>
            <SortableContext
              items={section.questions.map((q) => `question:${q.localId}`)}
              strategy={verticalListSortingStrategy}
            >
              {section.questions.map((q) => (
                <QuestionCanvasCard
                  key={q.localId}
                  question={q}
                  selected={selectedId === q.localId}
                  insideSet={false}
                  routingOn={routingOn}
                  onSelect={() => onSelectQuestion(q)}
                  onEditRouting={() => onEditRouting(q)}
                  onRemove={() => onRemoveQuestion(q.localId)}
                  disabled={disabled}
                />
              ))}
            </SortableContext>
            {section.sets.map((set) => (
              <QuestionsSetCard
                key={set.localId}
                set={set}
                selectedId={selectedId}
                onAddQuestion={() => onAddQuestionToSet(set)}
                onOpenSettings={() => onOpenSetSettings(set)}
                onRemove={() => onRemoveSet(set)}
                onSelectQuestion={onSelectQuestion}
                onRemoveQuestion={onRemoveQuestion}
                disabled={disabled}
              />
            ))}
          </>
        )}
      </div>

      {/* FR-2.5 destructive cascade confirmation */}
      <Dialog open={cascade !== null} onOpenChange={(o) => !o && setCascade(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <TriangleAlert className="size-5 shrink-0 text-destructive" aria-hidden />
              {t("surveysModule.sectionDelete.title")}
            </DialogTitle>
            <DialogDescription>{t("surveysModule.sectionDelete.body")}</DialogDescription>
          </DialogHeader>
          {cascade && (
            <ul className="list-disc space-y-1 ps-5 text-sm text-foreground" role="alert">
              <li>
                {t("surveysModule.sectionDelete.standalone", {
                  count: cascade.standaloneQuestions,
                })}
              </li>
              <li>{t("surveysModule.sectionDelete.sets", { count: cascade.questionsSets })}</li>
              <li>
                {t("surveysModule.sectionDelete.setQuestions", { count: cascade.setQuestions })}
              </li>
            </ul>
          )}
          <DialogFooter className="gap-2 sm:gap-2">
            <Button variant="outline" onClick={() => setCascade(null)} disabled={deleting}>
              {t("common.cancel")}
            </Button>
            <Button
              variant="destructive"
              onClick={() => void requestDelete(true)}
              disabled={deleting}
            >
              {t("surveysModule.sectionDelete.confirm")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
