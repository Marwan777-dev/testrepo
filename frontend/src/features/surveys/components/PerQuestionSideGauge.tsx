// Compact per-question side gauge for the Survey Report (FR-13.3), ported from the
// clickthrough: a 180° semicircle sitting on the end of a KPI / Scale question card,
// divided from the bars by a border. KPI gauges show 3 status zones + a needle + a
// target tick + centre value/label + a "Target" caption; Scale gauges show a single
// brand-cyan fill arc + value with a stars/faces icon row below. Status colours use
// the D-scale hex (fixed across themes); all neutral chrome is theme-aware.

import { Frown, Meh, Smile, Star } from "lucide-react"
import { useTranslation } from "react-i18next"

import { cn } from "@/lib/utils"
import { D1, D2, D3, D5, perfLevel } from "@/lib/journey-data"
import type { PerQuestionCard } from "../api/report-api"

const GX = 90
const GY = 88
const GR = 66

function gpt(deg: number, r = GR): { x: number; y: number } {
  const a = (deg * Math.PI) / 180
  return { x: GX + r * Math.cos(a), y: GY + r * Math.sin(a) }
}

// Gauge sweeps 180° (left) → 360° (right) over the top.
function gArc(a0: number, a1: number, r = GR): string {
  const s = gpt(a0, r)
  const e = gpt(a1, r)
  const large = a1 - a0 > 180 ? 1 : 0
  return `M ${s.x} ${s.y} A ${r} ${r} 0 ${large} 1 ${e.x} ${e.y}`
}

const ang = (pct: number) => 180 + 1.8 * Math.max(0, Math.min(100, pct))

function zoneColor(pct: number): string {
  if (pct < 33) return D5
  if (pct < 55) return D3
  if (pct < 70) return D2
  return D1
}

function KpiSideGauge({
  value,
  display,
  label,
  target,
  targetText,
}: {
  value: number
  display: string
  label: string
  target: number | null
  targetText: string | null
}) {
  const { t } = useTranslation()
  const valColor = zoneColor(value)
  const needle = gpt(ang(value))
  const tick0 = target != null ? gpt(ang(target), GR - 12) : null
  const tick1 = target != null ? gpt(ang(target), GR + 6) : null
  const tLabel = target != null ? gpt(ang(target), GR + 16) : null

  return (
    <div className="flex shrink-0 flex-col items-center justify-center border-s border-border ps-4">
      <svg viewBox="0 0 180 104" className="w-40" role="img" aria-label={`${label} ${display}`}>
        <path d={gArc(180, 180 + 1.8 * 33)} fill="none" stroke={D5} strokeWidth={12} strokeLinecap="round" />
        <path d={gArc(180 + 1.8 * 33, 180 + 1.8 * 55)} fill="none" stroke={D3} strokeWidth={12} />
        <path d={gArc(180 + 1.8 * 55, 360)} fill="none" stroke={D1} strokeWidth={12} strokeLinecap="round" />
        {tick0 && tick1 && (
          <line x1={tick0.x} y1={tick0.y} x2={tick1.x} y2={tick1.y} className="stroke-foreground" strokeWidth={2} />
        )}
        {tLabel && (
          <text x={tLabel.x} y={tLabel.y} textAnchor="middle" fontSize={9} className="fill-muted-foreground">
            T
          </text>
        )}
        <circle cx={needle.x} cy={needle.y} r={6} fill={valColor} className="stroke-card" strokeWidth={2.5} />
        <text
          x={GX}
          y={GY - 20}
          textAnchor="middle"
          className="font-heading font-extrabold"
          fontSize={30}
          fill={valColor}
        >
          {display}
        </text>
        <text x={GX} y={GY - 4} textAnchor="middle" fontSize={11} className="fill-muted-foreground">
          {label}
        </text>
      </svg>
      {target != null && targetText && (
        <span className="-mt-1 text-xs text-muted-foreground">
          {t("surveysModule.report.target", { value: targetText })}
        </span>
      )}
    </div>
  )
}

function ScaleSideGauge({ value, children }: { value: number; children: React.ReactNode }) {
  const end = gpt(ang(value))
  return (
    <div className="flex shrink-0 flex-col items-center justify-center border-s border-border ps-4">
      <svg viewBox="0 0 180 104" className="w-40" role="img" aria-label={`${value}%`}>
        <path d={gArc(180, 360)} fill="none" className="stroke-muted" strokeWidth={12} strokeLinecap="round" />
        <path d={gArc(180, ang(value))} fill="none" className="stroke-primary" strokeWidth={12} strokeLinecap="round" />
        <circle cx={end.x} cy={end.y} r={5} className="fill-primary stroke-card" strokeWidth={2.5} />
        <text
          x={GX}
          y={GY - 8}
          textAnchor="middle"
          className="fill-primary font-heading font-extrabold"
          fontSize={30}
        >
          {value}%
        </text>
      </svg>
      <div className="-mt-1 flex flex-col items-center gap-1">{children}</div>
    </div>
  )
}

export function PerQuestionSideGauge({ card }: { card: PerQuestionCard }) {
  const { t } = useTranslation()
  if (!card.gauge) return null
  const value = Math.round(card.gauge.value)
  const target = card.gauge.target

  if (card.type === "Kpi") {
    const isNps = card.kpiId === "nps"
    const display = isNps ? (value > 0 ? `+${value}` : `${value}`) : `${value}%`
    const label = (card.kpiId ?? "").toUpperCase() || t("surveysModule.report.aggregate")
    const targetText =
      target == null ? null : isNps ? (target > 0 ? `+${target}` : `${target}`) : `${target}%`
    return (
      <KpiSideGauge value={value} display={display} label={label} target={target} targetText={targetText} />
    )
  }

  // Scale — stars / smileys views (labels view renders no gauge; handled by the card).
  const view = (card.subtype ?? "").toLowerCase()
  const stars = (value / 100) * 5
  if (view === "smileys") {
    const Icon = value >= 60 ? Smile : value >= 40 ? Meh : Frown
    const level = perfLevel(value)
    const iconColor = { d1: D1, d2: D2, d3: D3, d4: "#E05C1A", d5: D5 }[level] ?? D3
    return (
      <ScaleSideGauge value={value}>
        <Icon className="size-6" style={{ color: iconColor }} aria-hidden />
        <span className="text-xs text-muted-foreground">{t("surveysModule.report.scaleFaces")}</span>
      </ScaleSideGauge>
    )
  }
  return (
    <ScaleSideGauge value={value}>
      <div className="flex gap-0.5" aria-hidden>
        {[1, 2, 3, 4, 5].map((n) => {
          const filled = n <= Math.round(stars)
          return (
            <Star
              key={n}
              className={cn("size-4", !filled && "fill-none stroke-border")}
              style={filled ? { fill: D3, stroke: D3 } : undefined}
            />
          )
        })}
      </div>
      <span className="text-xs text-muted-foreground">
        {t("surveysModule.report.scaleStars", { value: stars.toFixed(1) })}
      </span>
    </ScaleSideGauge>
  )
}
