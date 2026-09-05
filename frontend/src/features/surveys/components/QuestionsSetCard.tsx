// Questions Set block (F10, clickthrough parity): cyan-tinted container with a
// Layers header, "shows k of n · mode" status line, and header actions — add
// question, open the set-settings dialog, delete. The pool is a droppable +
// SortableContext of full QuestionCanvasCards, so members drag in, out and reorder
// like standalone questions (routing is never offered inside a set — FR-9.5).

import { useDroppable } from "@dnd-kit/core"
import { SortableContext, verticalListSortingStrategy } from "@dnd-kit/sortable"
import { useTranslation } from "react-i18next"
import { Layers, Plus, Settings2, Trash2 } from "lucide-react"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"
import type { BuilderQuestion, BuilderSet } from "./builder-types"
import { QuestionCanvasCard } from "./QuestionCanvasCard"

export function QuestionsSetCard({
  set,
  selectedId,
  onAddQuestion,
  onOpenSettings,
  onRemove,
  onSelectQuestion,
  onRemoveQuestion,
  disabled,
}: {
  set: BuilderSet
  selectedId: string | null
  onAddQuestion: () => void
  onOpenSettings: () => void
  onRemove: () => void
  onSelectQuestion: (q: BuilderQuestion) => void
  onRemoveQuestion: (localId: string) => void
  disabled?: boolean
}) {
  const { t } = useTranslation()
  const { setNodeRef, isOver } = useDroppable({ id: `set:${set.localId}` })
  const memberCount = set.questions.length
  const effectiveCount = Math.min(Math.max(1, set.count), Math.max(1, memberCount || set.count))

  return (
    <div className="rounded-lg border border-nb-cyan-200 bg-nb-cyan-100/30 p-3 transition-colors dark:border-nb-cyan-900/60 dark:bg-nb-cyan-900/10">
      {/* Header */}
      <div className="flex items-center gap-2">
        <Layers className="size-4 shrink-0 text-nb-cyan-700 dark:text-nb-cyan-300" aria-hidden />
        <b className="min-w-0 flex-1 truncate text-sm">{set.title}</b>
        <Badge className="shrink-0 border-transparent bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200">
          {t("surveysModule.set.badge")}
        </Badge>
        <span className="hidden shrink-0 text-xs text-muted-foreground sm:block">
          {t("surveysModule.set.showsKofN", { k: effectiveCount, n: memberCount })}
          {" · "}
          {t(`surveysModule.set.mode_${set.selectionMode}`)}
        </span>
        <Button
          variant="ghost"
          size="icon-sm"
          aria-label={t("surveysModule.set.addQuestion")}
          onClick={onAddQuestion}
          disabled={disabled}
        >
          <Plus className="size-4" aria-hidden />
        </Button>
        <Button
          variant="ghost"
          size="icon-sm"
          aria-label={t("surveysModule.set.settingsAria")}
          onClick={onOpenSettings}
          disabled={disabled}
        >
          <Settings2 className="size-4" aria-hidden />
        </Button>
        <Button
          variant="ghost"
          size="icon-sm"
          className="hover:bg-destructive/10 hover:text-destructive"
          aria-label={t("surveysModule.set.remove")}
          onClick={onRemove}
          disabled={disabled}
        >
          <Trash2 className="size-4" aria-hidden />
        </Button>
      </div>
      {set.description && (
        <p className="ps-6 pt-0.5 text-xs text-muted-foreground">{set.description}</p>
      )}

      {/* Pool */}
      <div
        ref={setNodeRef}
        className={cn(
          "mt-2 space-y-2 rounded-md p-1 transition-all",
          isOver && "bg-primary/5 ring-2 ring-primary/40"
        )}
      >
        <SortableContext
          items={set.questions.map((q) => `question:${q.localId}`)}
          strategy={verticalListSortingStrategy}
        >
          {set.questions.map((q) => (
            <QuestionCanvasCard
              key={q.localId}
              question={q}
              selected={selectedId === q.localId}
              insideSet
              routingOn={false}
              onSelect={() => onSelectQuestion(q)}
              onEditRouting={() => undefined}
              onRemove={() => onRemoveQuestion(q.localId)}
              disabled={disabled}
            />
          ))}
        </SortableContext>
        {memberCount === 0 && (
          <p className="rounded-md border border-dashed border-nb-cyan-300 p-4 text-center text-sm text-muted-foreground dark:border-nb-cyan-900/60">
            {t("surveysModule.set.dropHint")}
          </p>
        )}
      </div>
    </div>
  )
}
