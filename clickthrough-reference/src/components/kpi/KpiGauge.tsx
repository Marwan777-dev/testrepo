// src/components/kpi/KpiGauge.tsx
import { useTranslation } from "react-i18next"

interface KpiGaugeProps {
  shortName: string
  thresholdX: number | null
  thresholdY: number | null
  targetValue: number | null
  isNps?: boolean
}

// SVG layout constants
const CX = 100
const CY = 90
const R = 68      // arc radius
const SW = 14     // stroke width

// D-scale semantic colors
const RED   = "#C01B2A"
const AMBER = "#E8A020"
const GREEN = "#1A7A3C"
const MUTED = "#C4CADD"

function toRad(deg: number) { return (deg * Math.PI) / 180 }

function pt(angleDeg: number, r = R) {
  const rad = toRad(angleDeg)
  return { x: +(CX + r * Math.cos(rad)).toFixed(2), y: +(CY + r * Math.sin(rad)).toFixed(2) }
}

// Map a value within [min, max] to an SVG angle on the gauge (clockwise from 135°)
function vToA(val: number, min: number, max: number) {
  return 135 + ((val - min) / (max - min)) * 270
}

// Clockwise SVG arc path from angleDeg a1 to a2 at radius r
function arcPath(a1: number, a2: number, r = R) {
  const p1 = pt(a1, r)
  const p2 = pt(a2, r)
  const span = ((a2 - a1) % 360 + 360) % 360
  const large = span > 180 ? 1 : 0
  return `M ${p1.x} ${p1.y} A ${r} ${r} 0 ${large} 1 ${p2.x} ${p2.y}`
}

export default function KpiGauge({ shortName, thresholdX, thresholdY, targetValue, isNps = false }: KpiGaugeProps) {
  const { t } = useTranslation()

  const min = isNps ? -100 : 0
  const max = 100

  // Use defaults when thresholds are missing
  const useDefaults = thresholdX === null || thresholdY === null
  const x = thresholdX ?? 20
  const y = thresholdY ?? 70

  // Clamp target for display
  const rawTarget = targetValue ?? Math.round((min + max) / 2)
  const target = Math.min(max, Math.max(min, rawTarget))

  // Angle positions for zone boundaries
  const startA = 135
  const endA   = 45   // = 135 + 270 (wraps)
  const xA = vToA(x, min, max)
  const yA = vToA(y, min, max)
  const targetA = vToA(target, min, max)

  // Scale labels: [value, angle]
  const scaleLabels: [string, number][] = isNps
    ? [["-100", 135], ["-50", 202.5], ["0", 270], ["+50", 337.5], ["+100", 45]]
    : [["0", 135], ["25", 202.5], ["50", 270], ["75", 337.5], ["100", 45]]

  // Target marker dot position
  const dotPt = pt(targetA, R)

  // Band that target falls in (for dot color)
  const targetColor = target <= x ? RED : target <= y ? AMBER : GREEN

  return (
    <svg
      viewBox="0 0 200 115"
      className="w-full"
      role="img"
      aria-label={`${shortName} gauge — target ${target}`}
    >
      {/* Background arc (full 270°) */}
      <path
        d={arcPath(startA, endA)}
        fill="none"
        stroke={MUTED}
        strokeWidth={SW}
        strokeLinecap="round"
      />

      {/* Red zone: min → x */}
      <path
        d={arcPath(startA, xA)}
        fill="none"
        stroke={RED}
        strokeWidth={SW}
        strokeLinecap="round"
        opacity={0.85}
      />

      {/* Amber zone: x → y */}
      <path
        d={arcPath(xA, yA)}
        fill="none"
        stroke={AMBER}
        strokeWidth={SW}
        opacity={0.85}
      />

      {/* Green zone: y → max */}
      <path
        d={arcPath(yA, endA)}
        fill="none"
        stroke={GREEN}
        strokeWidth={SW}
        strokeLinecap="round"
        opacity={0.85}
      />

      {/* Target marker dot */}
      <circle
        cx={dotPt.x}
        cy={dotPt.y}
        r={6}
        fill={targetColor}
        stroke="white"
        strokeWidth={2.5}
      />

      {/* Target marker label (▼) */}
      {(() => {
        const labelPt = pt(targetA, R - 22)
        return (
          <text
            x={labelPt.x}
            y={labelPt.y + 4}
            textAnchor="middle"
            fontSize={10}
            fill={targetColor}
            fontWeight="bold"
          >
            ▼
          </text>
        )
      })()}

      {/* Score value */}
      <text
        x={CX}
        y={CY - 4}
        textAnchor="middle"
        fontSize={26}
        fontWeight="700"
        fill="currentColor"
        className="fill-foreground"
      >
        {isNps && target > 0 ? `+${target}` : target}
      </text>

      {/* KPI short name label */}
      <text
        x={CX}
        y={CY + 15}
        textAnchor="middle"
        fontSize={11}
        fill="currentColor"
        className="fill-muted-foreground"
        fontWeight="500"
      >
        {shortName || "KPI"}
      </text>

      {/* Scale labels at arc endpoints and quarters */}
      {scaleLabels.map(([label, angle]) => {
        const labelPt = pt(angle, R + 18)
        return (
          <text
            key={label}
            x={labelPt.x}
            y={labelPt.y + 4}
            textAnchor="middle"
            fontSize={8}
            fill="currentColor"
            className="fill-muted-foreground"
          >
            {label}
          </text>
        )
      })}

      {/* Default threshold fallback label */}
      {useDefaults && (
        <text
          x={CX}
          y={108}
          textAnchor="middle"
          fontSize={7}
          className="fill-muted-foreground"
          opacity={0.7}
        >
          {t("kpi.gaugeDefaultThresholdLabel")}
        </text>
      )}
    </svg>
  )
}
