// Distribution donut (T247) — Recharts Pie inside the shadcn ChartContainer per the
// Donut / Reasons Charts spec, laid out like the clickthrough: the donut on the start
// side with a centre total + label, and the legend beside it on the end side (dots +
// labels + bold percentages). Series colours come from the brand chart-1…5 tokens
// (never semantic D-colours — Two-Palette Rule).

import { useMemo } from "react"
import { Label, Pie, PieChart } from "recharts"

import { ChartContainer, ChartTooltip, ChartTooltipContent } from "@/components/ui/chart"
import type { DistributionBucket } from "../api/report-api"

const SERIES_VARS = [
  "var(--chart-1)",
  "var(--chart-2)",
  "var(--chart-3)",
  "var(--chart-4)",
  "var(--chart-5)",
]

export function DistributionDonut({
  buckets,
  centerLabel,
}: {
  buckets: DistributionBucket[]
  centerLabel: string
}) {
  const total = useMemo(() => buckets.reduce((sum, b) => sum + b.count, 0), [buckets])
  const data = useMemo(
    () =>
      buckets.map((b, i) => ({
        name: b.label,
        value: b.count,
        fill: SERIES_VARS[i % SERIES_VARS.length],
      })),
    [buckets]
  )

  return (
    <div className="flex items-center gap-5">
      <ChartContainer
        config={{ value: { label: centerLabel } }}
        className="aspect-square h-40 w-40 shrink-0"
      >
        <PieChart>
          <ChartTooltip content={<ChartTooltipContent hideLabel />} />
          <Pie
            data={data}
            dataKey="value"
            nameKey="name"
            innerRadius="62%"
            outerRadius="92%"
            stroke="var(--color-card)"
            strokeWidth={2}
          >
            <Label
              position="center"
              content={({ viewBox }) => {
                const vb = viewBox as { cx?: number; cy?: number } | undefined
                if (vb?.cx == null || vb?.cy == null) return null
                return (
                  <g>
                    <text
                      x={vb.cx}
                      y={vb.cy - 4}
                      textAnchor="middle"
                      className="fill-foreground font-heading text-2xl font-bold tabular-nums"
                    >
                      {total.toLocaleString("en-US")}
                    </text>
                    <text
                      x={vb.cx}
                      y={vb.cy + 15}
                      textAnchor="middle"
                      className="fill-muted-foreground text-[10px] uppercase tracking-widest"
                    >
                      {centerLabel}
                    </text>
                  </g>
                )
              }}
            />
          </Pie>
        </PieChart>
      </ChartContainer>
      <ul className="min-w-0 flex-1 space-y-2.5">
        {data.map((d, i) => (
          <li key={d.name + i} className="flex items-center gap-2 text-sm">
            <span
              className="size-2.5 shrink-0 rounded-full"
              style={{ backgroundColor: d.fill }}
              aria-hidden
            />
            <span className="min-w-0 flex-1 truncate text-muted-foreground">{d.name}</span>
            <span className="shrink-0 font-semibold tabular-nums">
              {total > 0 ? `${Math.round((d.value / total) * 100)}%` : "—"}
            </span>
          </li>
        ))}
      </ul>
    </div>
  )
}
