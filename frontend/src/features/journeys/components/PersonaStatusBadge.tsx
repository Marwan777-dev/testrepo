import { useTranslation } from "react-i18next"
import {
  Archive,
  CircleCheck,
  CirclePause,
  PencilLine,
  type LucideIcon,
} from "lucide-react"

import { Badge } from "@/components/ui/badge"
import { cn } from "@/lib/utils"
import type { PersonaStatus } from "@/features/journeys/api"

// Persona lifecycle status → semantic D-scale token. The lifecycle is a *state*, so it draws from
// the D-palette (never brand colors) per the Two-Palette Rule — identical mapping to
// `JourneyStatusBadge` so personas and journeys signal status consistently:
//   Draft    → neutral muted  — being authored (keeps the common new-persona state off the scale)
//   Active   → D2 "Good"      — live & bindable to journeys (FR-005)
//   Inactive → D3 "Caution"   — paused; no longer offered in the binding selector
//   Archived → D5 "Critical"  — terminal / retired (rare, matches the D5-rarity rule)
// Each state pairs its color with a distinct icon + text so color is never the sole signal (WCAG).
const STATUS_STYLE: Record<PersonaStatus, string> = {
  Draft: "bg-muted text-muted-foreground",
  Active: "bg-d2-light text-d2-dark dark:bg-d2-dark/25 dark:text-d2-light",
  Inactive: "bg-d3-light text-d3-dark dark:bg-d3-dark/25 dark:text-d3-light",
  Archived: "bg-d5-light text-d5-dark dark:bg-d5-dark/25 dark:text-d5-light",
}

const STATUS_ICON: Record<PersonaStatus, LucideIcon> = {
  Draft: PencilLine,
  Active: CircleCheck,
  Inactive: CirclePause,
  Archived: Archive,
}

const STATUS_LABEL: Record<PersonaStatus, string> = {
  Draft: "persona.statusDraft",
  Active: "persona.statusActive",
  Inactive: "persona.statusInactive",
  Archived: "persona.statusArchived",
}

/**
 * Lifecycle status pill for a persona. Defensive against an unexpected wire value (the controller
 * serializes `PersonaStatus` to its string name, but a normalize gap should never render a blank
 * pill): an unknown status falls back to a neutral pill showing the raw value.
 */
export function PersonaStatusBadge({
  status,
  className,
}: {
  status: PersonaStatus
  className?: string
}) {
  const { t } = useTranslation()
  const Icon = STATUS_ICON[status] ?? PencilLine
  const labelKey = STATUS_LABEL[status]
  return (
    <Badge
      className={cn(
        STATUS_STYLE[status] ?? "bg-muted text-muted-foreground",
        className,
      )}
    >
      <Icon className="size-3" aria-hidden />
      {labelKey ? t(labelKey) : status}
    </Badge>
  )
}
