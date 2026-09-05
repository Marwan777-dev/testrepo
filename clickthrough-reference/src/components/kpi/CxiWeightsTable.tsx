// src/components/kpi/CxiWeightsTable.tsx
import { useTranslation } from "react-i18next"
import { Input } from "@/components/ui/input"
import { useKpis } from "@/contexts/kpi-context"

// High-contrast cool-tone palette for CXI weight distribution.
// Ordered to maximise adjacent-segment contrast in the bar:
// alternates light/dark and jumps across the hue wheel.
// Never red, green, or yellow — those are reserved for D1–D5 KPI states.
const SEGMENT_COLORS = [
  "#22D3EE", // Bright cyan       — light, vivid
  "#3730A3", // Deep indigo       — very dark, strong contrast with cyan
  "#DB2777", // Hot pink          — vivid, strong contrast with indigo
  "#0369A1", // Deep teal-blue    — dark, strong contrast with pink
  "#8B5CF6", // Medium violet     — bright purple
  "#1D4ED8", // Royal blue        — vivid, contrasts with violet
  "#A21CAF", // Dark magenta      — deep fuchsia, contrasts with blue
]

interface CxiWeightsTableProps {
  weights: Record<string, number>     // kpiId → weight
  onChange: (kpiId: string, weight: number) => void
  showLegend?: boolean                // show proportional bar below table
}

export default function CxiWeightsTable({ weights, onChange, showLegend }: CxiWeightsTableProps) {
  const { t } = useTranslation()
  const { kpis } = useKpis()

  // Active KPIs excluding CXI itself
  const activeKpis = kpis.filter((k) => k.isActive && k.id !== "cxi")

  const totalWeight = activeKpis.reduce((sum, k) => sum + (weights[k.id] ?? 0), 0)

  function effectivePct(kpiId: string): string {
    const w = weights[kpiId] ?? 0
    if (totalWeight === 0 || w === 0) return "0%"
    return `${Math.round((w / totalWeight) * 100)}%`
  }

  return (
    <div className="space-y-3">
      {/* Table header */}
      <div className="grid grid-cols-[1fr_80px_72px] gap-2 text-xs font-medium text-muted-foreground uppercase tracking-widest px-1">
        <span>{t("kpi.cxiWeightsKpi")}</span>
        <span className="text-center">{t("kpi.cxiWeightsWeight")}</span>
        <span className="text-end">{t("kpi.cxiWeightsEffective")}</span>
      </div>

      {/* KPI rows */}
      <div className="space-y-2">
        {activeKpis.map((kpi) => {
          const w = weights[kpi.id] ?? 0
          return (
            <div key={kpi.id} className="grid grid-cols-[1fr_80px_72px] gap-2 items-center">
              <div className="text-sm truncate">
                <span className="font-medium">{kpi.shortName}</span>
                <span className="text-muted-foreground text-xs ms-1">— {kpi.fullName}</span>
              </div>
              <Input
                type="number"
                min={0}
                value={w === 0 ? "" : w}
                onChange={(e) => {
                  const val = parseInt(e.target.value) || 0
                  onChange(kpi.id, Math.max(0, val))
                }}
                className="h-8 text-sm text-center tabular-nums"
                placeholder="0"
              />
              <span className="text-sm tabular-nums text-end font-medium">
                {effectivePct(kpi.id)}
              </span>
            </div>
          )
        })}
      </div>

      {/* Summary row */}
      {activeKpis.length > 0 && (
        <div className="rounded-md bg-muted/40 px-3 py-2 text-xs text-muted-foreground">
          {t("kpi.cxiWeightsTotal", { total: totalWeight })}
        </div>
      )}

      {/* Proportional bar legend */}
      {showLegend && totalWeight > 0 && (
        <div className="space-y-1.5">
          <div className="flex h-3 rounded-full overflow-hidden">
            {activeKpis
              .filter((k) => (weights[k.id] ?? 0) > 0)
              .map((k, i) => {
                const pct = ((weights[k.id] ?? 0) / totalWeight) * 100
                return (
                  <div
                    key={k.id}
                    style={{ width: `${pct}%`, backgroundColor: SEGMENT_COLORS[i % SEGMENT_COLORS.length] }}
                    title={`${k.shortName}: ${Math.round(pct)}%`}
                  />
                )
              })}
          </div>
          <div className="flex flex-wrap gap-x-3 gap-y-1">
            {activeKpis
              .filter((k) => (weights[k.id] ?? 0) > 0)
              .map((k, i) => (
                <span key={k.id} className="flex items-center gap-1 text-xs text-muted-foreground">
                  <span
                    className="size-2 rounded-full inline-block shrink-0"
                    style={{ backgroundColor: SEGMENT_COLORS[i % SEGMENT_COLORS.length] }}
                  />
                  {k.shortName} {effectivePct(k.id)}
                </span>
              ))}
          </div>
        </div>
      )}
    </div>
  )
}
