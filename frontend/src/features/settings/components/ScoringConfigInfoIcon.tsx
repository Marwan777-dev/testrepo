// T107 [US4] — a small "?" affordance beside each scoring parameter. The tooltip carries the
// explanatory copy (range/default + guidance, bilingual via the passed `text`). Keyboard-focusable
// (it is a real <button>) and Esc-dismissible — both provided by the base-ui Tooltip (FR-059 / NFR-S1).

import { CircleHelp } from "lucide-react"

import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip"

export interface ScoringConfigInfoIconProps {
  /** Accessible name for the trigger (e.g. "About Alpha"). */
  label: string
  /** The explanatory tooltip body (already localised). */
  text: string
}

export function ScoringConfigInfoIcon({ label, text }: ScoringConfigInfoIconProps) {
  return (
    <TooltipProvider delay={150}>
      <Tooltip>
        <TooltipTrigger
          type="button"
          aria-label={label}
          className="inline-flex size-5 shrink-0 items-center justify-center rounded-full text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          <CircleHelp className="size-4" aria-hidden />
        </TooltipTrigger>
        <TooltipContent className="max-w-sm text-start leading-relaxed">{text}</TooltipContent>
      </Tooltip>
    </TooltipProvider>
  )
}
