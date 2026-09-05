import { useRef, type ReactNode } from "react"
import { useTranslation } from "react-i18next"
import { ChevronDown, GripVertical } from "lucide-react"

import { Badge } from "@/components/ui/badge"
import { cn } from "@/lib/utils"
import type { StageDetail } from "@/features/journeys/api"

// Expected-emotion → label key + badge tint. The API ships `expectedEmotion` as a free
// string; it's normalized to lowercase before lookup, with an unknown value falling back to a
// neutral pill. The emotion *word* is always shown, so color enhances — never replaces — the
// label (WCAG: color is not the only indicator).
const EMOTION_META: Record<string, { labelKey: string; badge: string }> = {
  excited: { labelKey: "journey.emotionExcited", badge: "bg-d2-light text-d2-dark dark:bg-d2-dark/25 dark:text-d2-light" },
  confident: { labelKey: "journey.emotionConfident", badge: "bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200" },
  relieved: { labelKey: "journey.emotionRelieved", badge: "bg-nb-mint-100 text-nb-mint-800 dark:bg-nb-mint-900/30 dark:text-nb-mint-300" },
  neutral: { labelKey: "journey.emotionNeutral", badge: "bg-muted text-muted-foreground" },
  confused: { labelKey: "journey.emotionConfused", badge: "bg-d3-light text-d3-dark dark:bg-d3-dark/25 dark:text-d3-light" },
  anxious: { labelKey: "journey.emotionAnxious", badge: "bg-d4-light text-d4-dark dark:bg-d4-dark/25 dark:text-d4-light" },
  frustrated: { labelKey: "journey.emotionFrustrated", badge: "bg-d5-light text-d5-dark dark:bg-d5-dark/25 dark:text-d5-light" },
}

/**
 * One stage rendered as a collapsible accordion row (US-1 `StageDetail`). The header carries a
 * drag handle, the `seqLabel` number badge (e.g. `1.0`), the stage name + customer goal, the
 * expected-emotion pill, the touchpoint count, an `actions` slot (add-touchpoint + overflow menu,
 * supplied by the builder so it can wire mutation handlers), and an expand chevron. `children`
 * (the `TouchpointRow` list) sits in a body that animates open/closed via the grid-rows trick.
 *
 * Reordering: when `reorderable` is set, the grip handle initiates a native HTML5 drag (the rest
 * of the row stays click-only — `gripPressed` gates `onDragStart` so dragging the title never
 * starts a reorder). The parent owns the order + the `reorderStages` call and drives the visual
 * state (`dragging`, `dropPosition`). Keyboard users reorder via the overflow menu's move
 * actions, so drag is a pointer-only enhancement, not the sole path.
 *
 * The builder is a *config* surface, so the row stays a neutral white card: stage scores arrive
 * in M-06 and the D-scale status tint by score belongs to the analytics view, not here.
 */
export function StageAccordionItem({
  stage,
  seqLabel,
  expanded,
  onToggle,
  actions,
  children,
  className,
  reorderable = false,
  dragging = false,
  dropPosition = null,
  onReorderStart,
  onReorderOver,
  onReorderEnd,
  onReorderDrop,
}: {
  stage: StageDetail
  seqLabel: string
  expanded: boolean
  onToggle: () => void
  actions?: ReactNode
  children?: ReactNode
  className?: string
  reorderable?: boolean
  dragging?: boolean
  dropPosition?: "before" | "after" | null
  onReorderStart?: () => void
  onReorderOver?: (after: boolean) => void
  onReorderEnd?: () => void
  onReorderDrop?: () => void
}) {
  const { t } = useTranslation()
  const tpCount = stage.touchpoints.length
  const emotionKey = stage.expectedEmotion?.toLowerCase().trim() ?? ""
  const emotion = EMOTION_META[emotionKey]

  // True only while a drag began on the grip — gates onDragStart so dragging the title/chevron
  // (which are also inside the draggable section) never starts a reorder.
  const gripPressed = useRef(false)

  return (
    <section
      aria-label={t("journey.stageNumber", { number: stage.sequenceNumber })}
      draggable={reorderable}
      onDragStart={
        reorderable
          ? (e) => {
              if (!gripPressed.current) {
                e.preventDefault()
                return
              }
              e.dataTransfer.effectAllowed = "move"
              try {
                e.dataTransfer.setData("text/plain", stage.stageId)
              } catch {
                /* some browsers disallow setData outside a user gesture; safe to ignore */
              }
              onReorderStart?.()
            }
          : undefined
      }
      onDragOver={
        reorderable
          ? (e) => {
              e.preventDefault()
              const rect = e.currentTarget.getBoundingClientRect()
              onReorderOver?.(e.clientY - rect.top > rect.height / 2)
            }
          : undefined
      }
      onDrop={
        reorderable
          ? (e) => {
              e.preventDefault()
              gripPressed.current = false
              onReorderDrop?.()
            }
          : undefined
      }
      onDragEnd={
        reorderable
          ? () => {
              gripPressed.current = false
              onReorderEnd?.()
            }
          : undefined
      }
      className={cn(
        "relative rounded-lg border border-border bg-card text-card-foreground shadow-sm transition-opacity dark:shadow-none",
        dragging && "opacity-40",
        className,
      )}
    >
      {/* Insertion indicator — sits in the 12px gap above/below the hovered row */}
      {dropPosition === "before" && (
        <div className="pointer-events-none absolute inset-x-3 -top-2 z-10 h-0.5 rounded-full bg-primary" aria-hidden />
      )}
      {dropPosition === "after" && (
        <div className="pointer-events-none absolute inset-x-3 -bottom-2 z-10 h-0.5 rounded-full bg-primary" aria-hidden />
      )}

      <div className="flex items-center gap-2 p-3 sm:gap-3 sm:p-4">
        <GripVertical
          onPointerDown={reorderable ? () => (gripPressed.current = true) : undefined}
          onPointerUp={reorderable ? () => (gripPressed.current = false) : undefined}
          className={cn(
            "size-4 shrink-0 text-muted-foreground/40",
            reorderable && "cursor-grab text-muted-foreground/60 active:cursor-grabbing",
          )}
          aria-hidden
        />

        <button
          type="button"
          onClick={onToggle}
          aria-expanded={expanded}
          className="flex min-w-0 flex-1 items-center gap-3 rounded-md text-start outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          <span
            aria-hidden
            className="flex h-7 shrink-0 items-center justify-center rounded-md bg-primary/10 px-2 text-sm font-bold tabular-nums text-primary"
          >
            {seqLabel}
          </span>
          <span className="min-w-0">
            <span className="block truncate text-base font-bold">{stage.name}</span>
            {stage.customerGoal && (
              <span className="block truncate text-xs text-muted-foreground">{stage.customerGoal}</span>
            )}
          </span>
        </button>

        <div className="flex shrink-0 items-center gap-1.5 sm:gap-2">
          {emotion && (
            <Badge className={cn("hidden border-transparent font-semibold sm:inline-flex", emotion.badge)}>
              {t(emotion.labelKey)}
            </Badge>
          )}
          <span className="inline-flex items-center gap-1 text-xs text-muted-foreground tabular-nums">
            {tpCount}
            <span className="hidden sm:inline">{t(tpCount === 1 ? "journey.tpShort" : "journey.tpsShort")}</span>
          </span>
          {actions}
          <button
            type="button"
            onClick={onToggle}
            aria-label={expanded ? t("journey.collapseStage") : t("journey.expandStage")}
            className="flex size-7 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-muted hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
          >
            <ChevronDown
              className={cn(
                "size-4 transition-transform duration-200",
                expanded ? "rotate-0" : "-rotate-90 rtl:rotate-90",
              )}
              aria-hidden
            />
          </button>
        </div>
      </div>

      <div
        className={cn(
          "grid transition-[grid-template-rows,opacity] duration-300 ease-[cubic-bezier(0.16,1,0.3,1)]",
          expanded ? "grid-rows-[1fr] opacity-100" : "grid-rows-[0fr] opacity-0",
        )}
      >
        <div className="overflow-hidden">
          <div className="border-t border-border px-4 pb-4 pt-4">{children}</div>
        </div>
      </div>
    </section>
  )
}
