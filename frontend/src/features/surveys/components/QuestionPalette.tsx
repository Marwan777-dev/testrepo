// F8 question palette (T091, FR-8.1): 7 draggable answer-type tiles in the specified
// order, KPI under a separate "Metric" heading, and the "Structure" group with the
// Questions Set tile. Tiles are @dnd-kit draggables (id `palette:<type>` /
// `palette:set`); they are also buttons so keyboard/tap users can add without
// dragging (no hover-only interactions).

import { useDraggable } from "@dnd-kit/core"
import { useTranslation } from "react-i18next"
import {
  ArrowUpDown,
  CircleDot,
  Gauge,
  Grid3x3,
  Layers,
  ListChecks,
  Ruler,
  TextCursorInput,
  ToggleLeft,
  type LucideIcon,
} from "lucide-react"

import { cn } from "@/lib/utils"
import type { BuilderQuestionType } from "./builder-types"

// FR-8.1 order — do not resort.
const ANSWER_TYPES: { type: BuilderQuestionType; icon: LucideIcon; labelKey: string }[] = [
  { type: "Scale", icon: Ruler, labelKey: "surveysModule.palette.scale" },
  { type: "InputField", icon: TextCursorInput, labelKey: "surveysModule.palette.inputField" },
  { type: "SingleSelect", icon: CircleDot, labelKey: "surveysModule.palette.singleSelect" },
  { type: "MultiSelect", icon: ListChecks, labelKey: "surveysModule.palette.multiSelect" },
  { type: "YesNo", icon: ToggleLeft, labelKey: "surveysModule.palette.yesNo" },
  { type: "Matrix", icon: Grid3x3, labelKey: "surveysModule.palette.matrix" },
  { type: "Ranking", icon: ArrowUpDown, labelKey: "surveysModule.palette.ranking" },
]

function PaletteTile({
  dragId,
  dragData,
  icon: Icon,
  label,
  chipClass,
  onClick,
}: {
  dragId: string
  dragData: Record<string, unknown>
  icon: LucideIcon
  label: string
  /** Icon chip tint — mint for the KPI metric, cyan for structure, muted otherwise. */
  chipClass: string
  onClick: () => void
}) {
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: dragId,
    data: dragData,
  })
  return (
    <button
      ref={setNodeRef}
      type="button"
      onClick={onClick}
      className={cn(
        "flex w-full cursor-grab items-center gap-2.5 rounded-md border border-border bg-card p-2 text-start text-sm",
        "transition-colors hover:bg-accent focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2",
        isDragging && "opacity-50"
      )}
      {...listeners}
      {...attributes}
    >
      <span className={cn("flex size-7 shrink-0 items-center justify-center rounded-md", chipClass)}>
        <Icon className="size-4" aria-hidden />
      </span>
      <span className="truncate">{label}</span>
    </button>
  )
}

function GroupHeading({ children }: { children: React.ReactNode }) {
  return (
    <h3 className="mb-2 text-xs font-medium uppercase tracking-widest text-muted-foreground">
      {children}
    </h3>
  )
}

export function QuestionPalette({
  onAdd,
  onAddSet,
}: {
  onAdd: (type: BuilderQuestionType) => void
  onAddSet: () => void
}) {
  const { t } = useTranslation()
  return (
    <div className="space-y-4">
      <div>
        <GroupHeading>{t("surveysModule.palette.metric")}</GroupHeading>
        <PaletteTile
          dragId="palette:Kpi"
          dragData={{ paletteType: "Kpi" }}
          icon={Gauge}
          label={t("surveysModule.palette.kpi")}
          chipClass="bg-nb-mint-100 text-nb-mint-700 dark:bg-nb-mint-900/40 dark:text-nb-mint-300"
          onClick={() => onAdd("Kpi")}
        />
      </div>
      <div>
        <GroupHeading>{t("surveysModule.palette.answerTypes")}</GroupHeading>
        <div className="space-y-2">
          {ANSWER_TYPES.map(({ type, icon, labelKey }) => (
            <PaletteTile
              key={type}
              dragId={`palette:${type}`}
              dragData={{ paletteType: type }}
              icon={icon}
              label={t(labelKey)}
              chipClass="bg-muted text-muted-foreground"
              onClick={() => onAdd(type)}
            />
          ))}
        </div>
      </div>
      <div>
        <GroupHeading>{t("surveysModule.palette.structure")}</GroupHeading>
        <PaletteTile
          dragId="palette:set"
          dragData={{ paletteSet: true }}
          icon={Layers}
          label={t("surveysModule.palette.questionsSet")}
          chipClass="bg-nb-cyan-100 text-nb-cyan-700 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-300"
          onClick={onAddSet}
        />
      </div>
    </div>
  )
}
