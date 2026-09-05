// T060 [US2] — Universal KPI arc gauge. CUSTOM SVG (never Recharts) per CLAUDE.md "When to use
// Recharts vs Custom SVG": zone colouring + needle dot + target marker require hand-built SVG.
//
// Dual-ring semicircle:
//   • thick outer ring — muted track + a filled value arc in the brand cyan;
//   • thin inner ring — three D1→D3→D5 zone bands at the x / y threshold boundaries;
//   • needle dot at the current value; target marker (tick + ▼ + "T") at the Target;
//   • central numeral (large) + Short Name caption.
// Arc range/labels switch by KPI type (NPS −100..+100; others 0..100). The SVG carries
// `direction: ltr` so numerals stay LTR even on RTL pages (research.md R11). Non-colour cues
// (band ticks + target arrow + numeral) satisfy NFR-4. Memoised on its value-bearing props (R10).

import { memo } from "react"

import { D1, D3, D5 } from "@/lib/journey-data"
import { cn } from "@/lib/utils"

export interface UniversalArcGaugeProps {
  /** Current score on the scale (0..100 normalised, or −100..+100 for NPS). */
  value: number
  /** Target marker position; hidden when null. */
  target: number | null
  /** Unsatisfactory/average boundary. */
  x: number
  /** Average/satisfactory boundary. */
  y: number
  /** Scale floor (arc start). */
  lower: number
  /** Scale ceiling (arc end). */
  upper: number
  /** Short Name shown under the central numeral. */
  shortName: string
  /** NPS formats positives with a leading "+". */
  isNps?: boolean
  className?: string
}

const CX = 110
const CY = 112
const R_OUTER = 90
const OUTER_W = 13
const R_INNER = R_OUTER - 12
const INNER_W = 4

const clamp01 = (n: number) => Math.min(1, Math.max(0, n))

function polar(r: number, fraction: number): [number, number] {
  const angle = Math.PI * (1 - clamp01(fraction))
  return [CX + r * Math.cos(angle), CY - r * Math.sin(angle)]
}

function arcPath(r: number, f0: number, f1: number): string {
  const [x0, y0] = polar(r, f0)
  const [x1, y1] = polar(r, f1)
  // Each segment is ≤ 180°, so large-arc-flag = 0; sweep-flag = 1 traces over the top.
  return `M ${x0.toFixed(2)} ${y0.toFixed(2)} A ${r} ${r} 0 0 1 ${x1.toFixed(2)} ${y1.toFixed(2)}`
}

function UniversalArcGaugeImpl({
  value,
  target,
  x,
  y,
  lower,
  upper,
  shortName,
  isNps = false,
  className,
}: UniversalArcGaugeProps) {
  const span = upper - lower || 1
  const toFraction = (v: number) => clamp01((v - lower) / span)

  const fValue = toFraction(value)
  const fx = toFraction(x)
  const fy = toFraction(y)
  const fTarget = target == null ? null : toFraction(target)
  // The target marker is coloured by the band it lands in (D5 unsatisfactory → D3 average → D1
  // satisfactory), echoing the inner zone ring.
  const targetColor = target == null ? null : target < x ? D5 : target < y ? D3 : D1

  const [needleX, needleY] = polar(R_OUTER, fValue)

  const labelFractions = [0, 0.25, 0.5, 0.75, 1]
  const formatValue = (v: number) => {
    const rounded = Math.round(v)
    return isNps && rounded > 0 ? `+${rounded}` : String(rounded)
  }

  return (
    <div
      className={cn("w-full", className)}
      style={{ direction: "ltr" }}
      role="img"
      data-testid="preview-gauge"
      // Changes whenever any value-bearing prop changes (i.e. whenever the memo re-renders with new
      // data) — E2E asserts the preview reflects a field edit within the <100 ms budget (R10). Pure:
      // derived from props, no render-time mutation.
      data-render-tick={`${value}|${target}|${x}|${y}|${lower}|${upper}`}
      // The x / y band-boundary fractions — E2E asserts the inner-ring bands move when x or y change.
      data-fx={fx.toFixed(4)}
      data-fy={fy.toFixed(4)}
      aria-label={`${shortName} gauge: value ${formatValue(value)}${
        target == null ? "" : `, target ${formatValue(target)}`
      }`}
    >
      {/* viewBox is padded horizontally (−14 … 234) so the end labels — up to 4 chars for
          NPS ("−100" / "+100") — clear the arc ends instead of clipping at the edges. */}
      <svg viewBox="-14 -2 248 136" className="h-auto w-full" preserveAspectRatio="xMidYMid meet">
        {/* Outer track */}
        <path
          d={arcPath(R_OUTER, 0, 1)}
          fill="none"
          className="stroke-muted/30"
          strokeWidth={OUTER_W}
          strokeLinecap="round"
        />
        {/* Filled value arc (brand cyan) */}
        {fValue > 0 && (
          <path
            d={arcPath(R_OUTER, 0, fValue)}
            fill="none"
            stroke="var(--color-nb-cyan)"
            strokeWidth={OUTER_W}
            strokeLinecap="round"
          />
        )}

        {/* Inner three-zone ring: red → amber → green at the x / y boundaries */}
        <path d={arcPath(R_INNER, 0, fx)} fill="none" stroke={D5} strokeWidth={INNER_W} />
        <path d={arcPath(R_INNER, fx, fy)} fill="none" stroke={D3} strokeWidth={INNER_W} />
        <path d={arcPath(R_INNER, fy, 1)} fill="none" stroke={D1} strokeWidth={INNER_W} />

        {/* Needle dot at current value */}
        <circle
          cx={needleX}
          cy={needleY}
          r={6}
          fill="var(--color-nb-cyan)"
          className="stroke-card"
          strokeWidth={2.5}
        />

        {/* Target marker: a coloured ▼ sitting inside the arc at the Target position */}
        {fTarget != null && targetColor && (
          <TargetMarker fraction={fTarget} color={targetColor} />
        )}

        {/* End + mid labels — sit a touch further off the arc so they don't read as "stuck". */}
        {labelFractions.map((f) => {
          const [lx, ly] = polar(R_OUTER + 18, f)
          return (
            <text
              key={f}
              x={lx}
              y={ly}
              textAnchor="middle"
              dominantBaseline="middle"
              className="fill-muted-foreground text-[9px] tabular-nums"
            >
              {formatValue(lower + f * span)}
            </text>
          )
        })}

        {/* Centre numeral + short name */}
        <text
          x={CX}
          y={CY - 18}
          textAnchor="middle"
          className="fill-foreground text-[30px] font-bold tabular-nums"
        >
          {formatValue(value)}
        </text>
        <text
          x={CX}
          y={CY - 2}
          textAnchor="middle"
          className="fill-muted-foreground text-[11px] font-medium uppercase tracking-wider"
        >
          {shortName}
        </text>
      </svg>
    </div>
  )
}

// A ▼ triangle, painted in the target's band colour, seated just inside the inner zone ring at the
// Target's angular position and rotated so its apex points radially outward — i.e. straight at the
// arc/value it marks. As the Target moves around the dial the marker turns to keep pointing at it.
function TargetMarker({ fraction, color }: { fraction: number; color: string }) {
  const angle = Math.PI * (1 - clamp01(fraction))
  const ux = Math.cos(angle) // outward radial unit vector (screen coords: y grows downward)
  const uy = -Math.sin(angle)
  const px = -uy // unit vector perpendicular to (ux, uy), along the triangle base
  const py = ux

  const rMarker = R_OUTER - OUTER_W - 6 // sit just inside the value arc
  const cx = CX + rMarker * ux
  const cy = CY + rMarker * uy
  const apexLen = 7 // apex reaches toward the arc
  const baseLen = 5 // base sits toward the centre
  const hw = 6 // half-width of the base

  const apex = [cx + ux * apexLen, cy + uy * apexLen]
  const baseMid = [cx - ux * baseLen, cy - uy * baseLen]
  const c1 = [baseMid[0] + px * hw, baseMid[1] + py * hw]
  const c2 = [baseMid[0] - px * hw, baseMid[1] - py * hw]
  const points = [apex, c1, c2].map(([x, yy]) => `${x.toFixed(2)},${yy.toFixed(2)}`).join(" ")

  return (
    <polygon
      points={points}
      fill={color}
      className="stroke-card"
      strokeWidth={1}
      data-testid="preview-gauge-target"
    />
  )
}

export const UniversalArcGauge = memo(UniversalArcGaugeImpl)
