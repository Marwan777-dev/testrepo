// Custom SVG KPI gauge (T247) — a 240° dual-ring gauge matching the clickthrough's
// KpiDashboardPreview: a thick outer value arc coloured by perfColor over a muted
// track, a thin inner 3-zone ring (red 0–33% / amber 33–55% / green 55–100%), a
// needle dot with a card-surface contrast ring, a target tick + "T" marker, and a
// large centre value (KPI-coloured) with its label below. Neutral chrome (tracks,
// pivot, "T") uses theme-aware Tailwind classes; only STATUS colours (perfColor /
// D-scale zone hex) are raw values, fixed across themes. Only used by the Report page.

import { useTranslation } from "react-i18next"

import { D2, D3, D5, perfColor } from "@/lib/journey-data"

const SIZE = 210
const CX = SIZE / 2 // 105
const CY = SIZE / 2 + 14 // 119
const START = -210
const SWEEP = 240
const R_OUTER = SIZE * 0.42 // 88.2
const OUTER_W = SIZE * 0.06 // 12.6
const ZONE_W = 3.4
const R_ZONE = R_OUTER - OUTER_W / 2 - ZONE_W / 2 // ≈ 80.1
// Inner-ring zone splits — bad / average / great context.
const Z1 = 0.33
const Z2 = 0.55

const toRad = (deg: number) => (deg * Math.PI) / 180

function pt(r: number, deg: number): { x: number; y: number } {
  return { x: CX + r * Math.cos(toRad(deg)), y: CY + r * Math.sin(toRad(deg)) }
}

function arc(r: number, a1: number, a2: number): string {
  const s = pt(r, a1)
  const e = pt(r, a2)
  const large = a2 - a1 > 180 ? 1 : 0
  return `M ${s.x} ${s.y} A ${r} ${r} 0 ${large} 1 ${e.x} ${e.y}`
}

export function KpiGaugeSvg({
  value,
  target,
  min = 0,
  max = 100,
  label,
  /** perfColor kpiId ("nps" | "ces" | undefined) so 52 NPS ≠ 52 CES. */
  kpiId,
  /** Optional ▲/▼ pp delta shown under the gauge. */
  deltaPp,
}: {
  value: number
  target?: number | null
  min?: number
  max?: number
  label: string
  kpiId?: string
  deltaPp?: number | null
}) {
  const { t } = useTranslation()
  const clamp = (v: number) => Math.min(Math.max(v, min), max)
  const frac = (clamp(value) - min) / (max - min)
  // Zone banding + colour work on the 0–100 normalised position for NPS-style scales.
  const colour = perfColor(kpiId === "nps" ? value : frac * 100, kpiId)
  const valueAngle = START + frac * SWEEP
  const needle = pt(R_OUTER - 4, valueAngle)
  const zA1 = START + Z1 * SWEEP
  const zA2 = START + Z2 * SWEEP

  // Negative-min scales (NPS) show a signed integer; percentage scales show "%".
  const displayVal =
    min < 0
      ? value > 0
        ? `+${Math.round(value)}`
        : `${Math.round(value)}`
      : `${Math.round(value)}%`

  return (
    <div className="flex flex-col items-center">
      <svg
        viewBox="0 10 210 166"
        className="w-full max-w-[240px]"
        role="img"
        aria-label={t("surveysModule.report.gaugeAria", { label, value })}
      >
        {/* Inner 3-zone ring (thin): track + red / amber / green context */}
        <path
          d={arc(R_ZONE, START, START + SWEEP)}
          fill="none"
          strokeWidth={ZONE_W}
          strokeLinecap="round"
          className="stroke-muted/30"
        />
        <path d={arc(R_ZONE, START, zA1)} fill="none" stroke={D5} strokeWidth={ZONE_W} strokeLinecap="round" />
        <path d={arc(R_ZONE, zA1, zA2)} fill="none" stroke={D3} strokeWidth={ZONE_W} />
        <path d={arc(R_ZONE, zA2, START + SWEEP)} fill="none" stroke={D2} strokeWidth={ZONE_W} strokeLinecap="round" />

        {/* Outer ring (thick): muted track + value arc in the STATUS colour */}
        <path
          d={arc(R_OUTER, START, START + SWEEP)}
          fill="none"
          strokeWidth={OUTER_W}
          strokeLinecap="round"
          className="stroke-muted/30"
        />
        {frac > 0.005 && (
          <path
            d={arc(R_OUTER, START, valueAngle)}
            fill="none"
            stroke={colour}
            strokeWidth={OUTER_W}
            strokeLinecap="round"
          />
        )}

        {/* Target marker: tick + small "T" label */}
        {target != null &&
          (() => {
            const tf = (clamp(target) - min) / (max - min)
            const ta = START + tf * SWEEP
            const inner = pt(R_OUTER - OUTER_W * 0.7, ta)
            const outer = pt(R_OUTER + OUTER_W * 0.5, ta)
            const labelPos = pt(R_OUTER + OUTER_W * 0.5 + 9, ta)
            return (
              <g>
                <line
                  x1={inner.x}
                  y1={inner.y}
                  x2={outer.x}
                  y2={outer.y}
                  strokeWidth={2.5}
                  strokeLinecap="round"
                  className="stroke-foreground"
                />
                <text
                  x={labelPos.x}
                  y={labelPos.y + 3}
                  textAnchor="middle"
                  className="fill-muted-foreground text-[9px] font-bold"
                >
                  T
                </text>
              </g>
            )
          })()}

        {/* Needle dot at current value + centre pivot (theme-aware contrast ring) */}
        <circle cx={needle.x} cy={needle.y} r={6} fill={colour} strokeWidth={2.5} className="stroke-card" />
        <circle cx={CX} cy={CY} r={3.5} className="fill-muted-foreground" />

        {/* Centre value + label */}
        <text
          x={CX}
          y={CY - 6}
          textAnchor="middle"
          fill={colour}
          className="font-heading text-[34px] font-extrabold tabular-nums"
        >
          {displayVal}
        </text>
        <text
          x={CX}
          y={CY + 15}
          textAnchor="middle"
          className="fill-muted-foreground text-[10px] font-semibold uppercase tracking-widest"
        >
          {label}
        </text>
      </svg>

      {deltaPp != null && (
        <p
          className="mt-1 text-sm font-medium tabular-nums"
          style={{ color: deltaPp >= 0 ? D2 : D5 }}
        >
          {deltaPp >= 0 ? "▲" : "▼"} {Math.abs(deltaPp).toFixed(1)} {t("surveysModule.report.pp")}
        </p>
      )}
    </div>
  )
}
