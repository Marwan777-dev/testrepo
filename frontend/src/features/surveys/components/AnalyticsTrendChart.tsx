// FR-14.4 trend chart (T266) — the clickthrough's "Responses trend": a single
// gradient area of responses received per bucket (brand chart-1), with data-point
// dots and a legend. Supports action-event annotations via ReferenceDot + dashed
// ReferenceLine per the Trend Chart Annotations spec; the M-01 analytics payload
// carries no events yet, so `events` defaults empty and the markers appear as soon as
// a caller supplies them. The axis reverses in RTL.

import { useMemo } from "react"
import {
  Area,
  AreaChart,
  CartesianGrid,
  ReferenceDot,
  ReferenceLine,
  XAxis,
  YAxis,
} from "recharts"
import { useTranslation } from "react-i18next"

import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
  ChartTooltipContent,
} from "@/components/ui/chart"
import { useDirection } from "@/hooks/use-direction"
import type { TrendBucketData } from "../api/analytics-api"

export interface TrendEvent {
  bucketStart: string
  label: string
}

export function AnalyticsTrendChart({
  trend,
  granularity,
  events = [],
}: {
  trend: TrendBucketData[]
  granularity: string
  events?: TrendEvent[]
}) {
  const { t } = useTranslation()
  const { isRtl } = useDirection()

  const data = useMemo(
    () =>
      trend.map((b) => ({
        bucket: new Date(b.bucketStart).toLocaleDateString(undefined, {
          month: "short",
          day: granularity === "monthly" ? undefined : "numeric",
        }),
        bucketStart: b.bucketStart,
        responses: b.finished,
      })),
    [trend, granularity]
  )

  return (
    <ChartContainer
      config={{
        responses: {
          label: t("surveysModule.analytics.responsesSeries"),
          color: "var(--chart-1)",
        },
      }}
      className="h-72 w-full"
    >
      <AreaChart data={data} margin={{ top: 8, right: 12, left: 0, bottom: 0 }}>
        <defs>
          <linearGradient id="respTrendFill" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="var(--color-responses)" stopOpacity={0.28} />
            <stop offset="100%" stopColor="var(--color-responses)" stopOpacity={0.04} />
          </linearGradient>
        </defs>
        <CartesianGrid vertical={false} strokeDasharray="3 3" className="stroke-border" />
        <XAxis
          dataKey="bucket"
          tickLine={false}
          axisLine={false}
          tickMargin={8}
          reversed={isRtl}
        />
        <YAxis
          tickLine={false}
          axisLine={false}
          width={40}
          orientation={isRtl ? "right" : "left"}
        />
        <ChartTooltip content={<ChartTooltipContent />} />
        <ChartLegend content={<ChartLegendContent />} />
        <Area
          type="monotone"
          dataKey="responses"
          stroke="var(--color-responses)"
          strokeWidth={2.5}
          fill="url(#respTrendFill)"
          dot={{ r: 3, fill: "var(--color-responses)" }}
          activeDot={{ r: 5 }}
        />
        {events.map((e) => {
          const bucket = data.find((d) => d.bucketStart === e.bucketStart)
          if (!bucket) return null
          return (
            <g key={e.bucketStart + e.label}>
              <ReferenceLine
                x={bucket.bucket}
                strokeDasharray="4 4"
                className="stroke-muted-foreground"
              />
              <ReferenceDot
                x={bucket.bucket}
                y={bucket.responses}
                r={6}
                fill="var(--chart-4)"
                stroke="var(--color-card)"
                strokeWidth={2}
              />
            </g>
          )
        })}
      </AreaChart>
    </ChartContainer>
  )
}
