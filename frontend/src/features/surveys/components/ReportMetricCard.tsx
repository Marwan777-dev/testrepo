// Report metric card (T247) — one stat tile matching the clickthrough: a small-caps
// label with a muted icon on the end, a large tabular value, and an optional hint
// line. Uses the base Card (rounded-lg + ring + shadow-sm/dark:none) for elevation;
// CardContent takes horizontal padding only (the card supplies its own vertical).

import type { LucideIcon } from "lucide-react"

import { Card, CardContent } from "@/components/ui/card"

export function ReportMetricCard({
  icon: Icon,
  label,
  value,
  hint,
}: {
  icon: LucideIcon
  label: string
  value: string
  hint?: string
}) {
  return (
    <Card>
      <CardContent className="px-4">
        <div className="flex items-start justify-between gap-2">
          <p className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">
            {label}
          </p>
          <Icon className="size-4 shrink-0 text-muted-foreground" aria-hidden />
        </div>
        <p className="mt-2 font-heading text-3xl font-bold tabular-nums text-foreground">{value}</p>
        {hint && <p className="mt-1 text-sm text-muted-foreground">{hint}</p>}
      </CardContent>
    </Card>
  )
}
