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
import type { JourneyStatus } from "@/features/journeys/api"

// Journey lifecycle status → semantic D-scale token. The lifecycle is a *state*,
// so it draws from the D-palette (never brand colors) per the Two-Palette Rule.
// Mapping mirrors the click-through design reference (clickthrough-reference,
// ba-farah) so the live app matches the prototype:
//   Active   → D2 "Good"      — live & healthy (green)
//   Draft    → D3 "Caution"   — being authored (amber)
//   Inactive → D4 "Warning"   — paused; needs attention to re-activate (orange);
//                               distinct from Draft so the two never read alike
//   Archived → neutral muted  — terminal / retired, de-emphasized (grey)
// Each state pairs its color with a distinct icon + text so color is never the sole
// signal (WCAG: "color is never the only indicator").
const STATUS_STYLE: Record<JourneyStatus, string> = {
  Draft: "bg-d3-light text-d3-dark dark:bg-d3-dark/25 dark:text-d3-light",
  Active: "bg-d2-light text-d2-dark dark:bg-d2-dark/25 dark:text-d2-light",
  Inactive: "bg-d4-light text-d4-dark dark:bg-d4-dark/25 dark:text-d4-light",
  Archived: "bg-muted text-muted-foreground",
}

const STATUS_ICON: Record<JourneyStatus, LucideIcon> = {
  Draft: PencilLine,
  Active: CircleCheck,
  Inactive: CirclePause,
  Archived: Archive,
}

const STATUS_LABEL: Record<JourneyStatus, string> = {
  Draft: "journey.statusDraft",
  Active: "journey.statusActive",
  Inactive: "journey.statusInactive",
  Archived: "journey.statusArchived",
}

/**
 * Lifecycle status pill for a journey. Defensive against an unexpected wire value
 * (the controllers serialize `JourneyStatus` to its string name, but a normalize
 * gap should never render a blank pill): an unknown status falls back to a neutral
 * pill showing the raw value.
 */
export function JourneyStatusBadge({
  status,
  className,
}: {
  status: JourneyStatus
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
