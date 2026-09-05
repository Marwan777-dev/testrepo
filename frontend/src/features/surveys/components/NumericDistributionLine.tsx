// Numeric/date value-distribution chart (T247, FR-13.3) — laid out like the
// clickthrough: a gradient area chart with data-point dots (markers-and-indicators
// principle) taking the full width, and an "Average" side panel on the end. Brand
// chart-1 series colour (non-semantic — Two-Palette Rule).

import { Area, AreaChart, CartesianGrid, XAxis, YAxis } from "recharts"
import { useTranslation } from "react-i18next"

import { ChartContainer, ChartTooltip, ChartTooltipContent } from "@/components/ui/chart"
import type { DistributionBucket } from "../api/report-api"

export function NumericDistributionLine({
  buckets,
  average,
  axisLabel,
  unit,
}: {
  buckets: DistributionBucket[]
  average: number | null
  axisLabel?: string | null
  unit?: string | null
}) {
  const { t } = useTranslation()
  const data = buckets.map((b) => ({ label: b.label, count: b.count }))

  return (
    <div className="flex items-stretch gap-6">
      <div className="min-w-0 flex-1">
      <ChartContainer
        config={{ count: { label: t("surveysModule.report.responses"), color: "var(--chart-1)" } }}
        className="h-56 w-full"
      >
        <AreaChart data={data} margin={{ top: 8, right: 12, left: 0, bottom: 0 }}>
          <defs>
            <linearGradient id="numDistFill" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="var(--color-count)" stopOpacity={0.28} />
              <stop offset="100%" stopColor="var(--color-count)" stopOpacity={0.04} />
            </linearGradient>
          </defs>
          <CartesianGrid vertical={false} strokeDasharray="3 3" className="stroke-border" />
          <XAxis dataKey="label" tickLine={false} axisLine={false} tickMargin={8} />
          <YAxis tickLine={false} axisLine={false} allowDecimals={false} width={32} />
          <ChartTooltip content={<ChartTooltipContent hideLabel />} />
          <Area
            type="monotone"
            dataKey="count"
            stroke="var(--color-count)"
            strokeWidth={2.5}
            fill="url(#numDistFill)"
            dot={{ r: 3, fill: "var(--color-count)" }}
            activeDot={{ r: 5 }}
          />
        </AreaChart>
      </ChartContainer>
        {axisLabel && (
          <p className="mt-1 text-center text-xs text-muted-foreground">{axisLabel}</p>
        )}
      </div>
      {average != null && (
        <div className="flex shrink-0 flex-col items-center justify-center gap-0.5 border-s border-border ps-6 pe-2 text-center">
          <p className="text-xs font-medium text-muted-foreground">
            {t("surveysModule.report.aggregate")}
          </p>
          <p className="font-heading text-4xl font-bold tabular-nums text-primary">
            {average.toFixed(1)}
          </p>
          {unit && <p className="text-xs text-muted-foreground">{unit}</p>}
        </div>
      )}
    </div>
  )
}
