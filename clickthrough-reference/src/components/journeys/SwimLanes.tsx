import { useState } from 'react'
import { TrendingUp, TrendingDown, Minus, Target, Layers, Zap, AlertTriangle } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { Stage, EmotionType, Touchpoint } from '@/types/journey'

// ─── D-scale performance level ────────────────────────────────────────────────
function perfLevel(v: number): 'd1' | 'd2' | 'd3' | 'd4' | 'd5' {
  if (v >= 85) return 'd1'
  if (v >= 75) return 'd2'
  if (v >= 60) return 'd3'
  if (v >= 45) return 'd4'
  return 'd5'
}

// Tailwind arbitrary-class strings kept static so purge can find them
const D_BG: Record<string, string> = {
  d1: 'bg-[#D4F4E2] dark:bg-[#0D4A24]/25',
  d2: 'bg-[#C8F5DB] dark:bg-[#156632]/20',
  d3: 'bg-[#FFF0CC] dark:bg-[#7A5000]/20',
  d4: 'bg-[#FFE4D0] dark:bg-[#7A2800]/20',
  d5: 'bg-[#FFD6DA] dark:bg-[#6B0010]/20',
}

const D_SCORE_TEXT: Record<string, string> = {
  d1: 'text-[#0D4A24] dark:text-[#D4F4E2]',
  d2: 'text-[#156632] dark:text-[#C8F5DB]',
  d3: 'text-[#7A5000] dark:text-[#FFF0CC]',
  d4: 'text-[#7A2800] dark:text-[#FFE4D0]',
  d5: 'text-[#6B0010] dark:text-[#FFD6DA]',
}

// ─── Performance color (D1–D5) ───────────────────────────────────────────────
const D1 = '#1A7A3C', D2 = '#2EB85C', D3 = '#E8A020', D4 = '#E05C1A', D5 = '#C01B2A'
function perfColor(v: number) {
  if (v >= 85) return D1; if (v >= 75) return D2; if (v >= 60) return D3; if (v >= 45) return D4; return D5
}

// ─── Emotion config (yPct: 0 = top/great, 100 = bottom/poor) ─────────────────
const EMOTION_CONFIG: Record<EmotionType, { yPct: number; emoji: string }> = {
  Excited:    { yPct: 13, emoji: '😄' },
  Confident:  { yPct: 26, emoji: '😊' },
  Relieved:   { yPct: 40, emoji: '😌' },
  Neutral:    { yPct: 53, emoji: '😐' },
  Confused:   { yPct: 65, emoji: '😕' },
  Anxious:    { yPct: 76, emoji: '😰' },
  Frustrated: { yPct: 86, emoji: '😤' },
}

const EXPERIENCE_H = 220

// ─── Deterministic mock data per stage ──────────────────────────────────────
function seedScore(seed: string, min: number, max: number): number {
  let h = 0
  for (let i = 0; i < seed.length; i++) h = (Math.imul(31, h) + seed.charCodeAt(i)) | 0
  return min + (Math.abs(h) % (max - min + 1))
}

function stageData(stage: Stage) {
  const score = seedScore(stage.id + 'sc', 54, 96)
  const rawDelta = seedScore(stage.id + 'dt', 0, 22) - 11
  const delta = rawDelta === 0 ? 1 : rawDelta
  const kpiCount = stage.touchpoints.reduce((s, tp) => s + tp.kpiBindings.length, 0)
  return { score, delta, kpiCount }
}

// ─── Smooth SVG path ─────────────────────────────────────────────────────────
function smoothPath(pts: { x: number; y: number }[]): string {
  if (pts.length === 0) return ''
  if (pts.length === 1) return `M ${pts[0].x} ${pts[0].y}`
  let d = `M ${pts[0].x} ${pts[0].y}`
  for (let i = 1; i < pts.length; i++) {
    const cpx = (pts[i - 1].x + pts[i].x) / 2
    d += ` C ${cpx} ${pts[i - 1].y} ${cpx} ${pts[i].y} ${pts[i].x} ${pts[i].y}`
  }
  return d
}

// ─── Touchpoint tooltip ───────────────────────────────────────────────────────
interface TpTooltipState { tp: Touchpoint; stage: Stage; x: number; y: number }

function TouchpointTooltip({ data }: { data: TpTooltipState }) {
  const { tp } = data
  const tooltipW = 252
  const left = Math.min(data.x + 10, window.innerWidth - tooltipW - 12)
  const top  = Math.max(data.y - 4, 8)

  return (
    <div className="fixed z-50 pointer-events-none" style={{ left, top }}>
      <div className="bg-card border border-border rounded-xl shadow-xl p-4 space-y-3 text-xs" style={{ width: tooltipW }}>
        <div>
          <p className="font-semibold text-sm text-foreground leading-snug">{tp.nameEn}</p>
          {tp.nameAr && <p className="text-muted-foreground mt-0.5" dir="rtl">{tp.nameAr}</p>}
        </div>

        {tp.descriptionEn && (
          <p className="text-muted-foreground leading-relaxed">{tp.descriptionEn}</p>
        )}

        {tp.channels.length > 0 && (
          <div className="flex flex-wrap gap-1">
            {tp.channels.map(ch => (
              <span key={ch} className="bg-muted text-muted-foreground px-1.5 py-0.5 rounded-sm text-[10px]">{ch}</span>
            ))}
          </div>
        )}

        <div className="flex gap-5 text-muted-foreground">
          {[['Customer', tp.importanceCustomer], ['Business', tp.importanceBusiness]].map(([label, val]) => (
            <div key={label as string}>
              <p className="text-[10px] uppercase tracking-wide mb-1">{label}</p>
              <span className="text-[#E8A020] text-sm">
                {'★'.repeat(val as number)}<span className="opacity-25">{'★'.repeat(5 - (val as number))}</span>
              </span>
            </div>
          ))}
        </div>

        <div className="flex flex-wrap gap-1.5">
          {tp.isMoT && (
            <span className="inline-flex items-center gap-1 bg-[#FFF0CC] text-[#7A5000] dark:bg-[#7A5000]/20 dark:text-[#FFF0CC] px-2 py-0.5 rounded-full text-[10px] font-medium">
              <AlertTriangle className="size-3 text-[#E8A020]" />
              Moment of Truth
            </span>
          )}
          {tp.isMandatory && (
            <span className="bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan/10 dark:text-nb-cyan-300 px-2 py-0.5 rounded-full text-[10px] font-medium">
              Mandatory
            </span>
          )}
          {tp.kpiBindings.length > 0 && (
            <span className="bg-muted text-muted-foreground px-2 py-0.5 rounded-full text-[10px]">
              {tp.kpiBindings.map(k => k.kpiType).join(' · ')}
            </span>
          )}
          {tp.kpiBindings.length === 0 && (
            <span className="bg-muted text-muted-foreground px-2 py-0.5 rounded-full text-[10px]">No KPIs</span>
          )}
        </div>
      </div>
    </div>
  )
}

// ─── Main component ───────────────────────────────────────────────────────────
export default function SwimLanes({ stages }: { stages: Stage[] }) {
  const [tooltip, setTooltip] = useState<TpTooltipState | null>(null)

  if (stages.length === 0) return null

  const n = stages.length
  const svgW = n * 100

  const computed = stages.map(s => stageData(s))

  const pathPts = stages.map((s, i) => ({
    x: (i + 0.5) * 100,
    y: s.expectedEmotion ? EMOTION_CONFIG[s.expectedEmotion].yPct : 50,
  }))

  return (
    <>
      <div className="overflow-x-auto rounded-lg border border-border bg-card shadow-sm">
        <div style={{ minWidth: Math.max(n * 210, 420) }} className="relative">

          {/* ── Stage headers ─────────────────────────────────────────────── */}
          <div className="flex divide-x divide-border border-b border-border">
            {stages.map((stage, i) => {
              const { score, delta } = computed[i]
              const level = perfLevel(score)
              const isUp = delta > 0
              const isFlat = delta === 0

              return (
                <div key={stage.id} className={cn('flex-1 px-3 py-3', D_BG[level])}>
                  {/* Stage name + index */}
                  <div className="flex items-start justify-between gap-1 mb-2">
                    <div className="min-w-0">
                      <span className="text-[10px] font-mono text-muted-foreground">S{i + 1}</span>
                      <p className="text-xs font-bold text-foreground leading-snug truncate">{stage.nameEn}</p>
                    </div>
                    {/* Score chip — D-scale deep text on D-scale light bg */}
                    <span className={cn('text-xs font-bold tabular-nums px-1.5 py-0.5 rounded-md shrink-0', D_SCORE_TEXT[level])}>
                      {score}%
                    </span>
                  </div>

                  {/* Improvement / deterioration rate */}
                  <div className={cn(
                    'inline-flex items-center gap-1 text-[11px] font-semibold px-2 py-0.5 rounded-full',
                    isUp
                      ? 'bg-[#C8F5DB] text-[#156632] dark:bg-[#156632]/20 dark:text-[#C8F5DB]'
                      : isFlat
                      ? 'bg-muted text-muted-foreground'
                      : 'bg-[#FFD6DA] text-[#6B0010] dark:bg-[#6B0010]/20 dark:text-[#FFD6DA]'
                  )}>
                    {isUp
                      ? <TrendingUp className="size-3" />
                      : isFlat
                      ? <Minus className="size-3" />
                      : <TrendingDown className="size-3" />}
                    {isUp ? '+' : ''}{delta}% vs last period
                  </div>
                </div>
              )
            })}
          </div>

          {/* ── Experience zone (emotion curve) ───────────────────────────── */}
          <div className="flex divide-x divide-border relative" style={{ height: EXPERIENCE_H }}>
            {/* Lane background columns — D-scale tinted */}
            {stages.map((stage, i) => {
              const { score } = computed[i]
              const level = perfLevel(score)
              return <div key={stage.id} className={cn('flex-1', D_BG[level])} />
            })}

            {/* Mint→Cyan connecting curve drawn behind emotion bubbles */}
            <svg
              className="absolute inset-0 w-full h-full"
              viewBox={`0 0 ${svgW} 100`}
              preserveAspectRatio="none"
              aria-hidden="true"
            >
              <defs>
                <linearGradient id="sw-mint-cyan" x1="0" y1="0" x2="1" y2="0">
                  <stop offset="0%" stopColor="#13DB9B" stopOpacity="0.5" />
                  <stop offset="100%" stopColor="#0D8BBC" stopOpacity="0.5" />
                </linearGradient>
              </defs>
              <path
                d={smoothPath(pathPts)}
                fill="none"
                stroke="url(#sw-mint-cyan)"
                strokeWidth="0.7"
                vectorEffect="non-scaling-stroke"
              />
            </svg>

            {/* Emotion bubbles — white circle with perfColor(score) ring */}
            {stages.map((stage, i) => {
              if (!stage.expectedEmotion) return null
              const emo = EMOTION_CONFIG[stage.expectedEmotion]
              const { score } = computed[i]
              return (
                <div
                  key={stage.id}
                  className="absolute pointer-events-none select-none flex flex-col items-center"
                  style={{
                    left: `${((i + 0.5) / n) * 100}%`,
                    top: `${emo.yPct}%`,
                    transform: 'translate(-50%, -50%)',
                  }}
                >
                  <div
                    className="size-12 rounded-full bg-card flex items-center justify-center text-xl shadow-sm"
                    style={{ border: `2.5px solid ${perfColor(score)}` }}
                  >
                    <span role="img" aria-label={stage.expectedEmotion}>{emo.emoji}</span>
                  </div>
                  <p className="text-[10px] font-semibold text-foreground mt-1 whitespace-nowrap">
                    {stage.expectedEmotion}
                  </p>
                </div>
              )
            })}
          </div>

          {/* ── Stage metadata strip ───────────────────────────────────────── */}
          <div className="flex divide-x divide-border border-t border-border/60">
            {stages.map((stage, i) => {
              const { score, kpiCount } = computed[i]
              const level = perfLevel(score)
              return (
                <div key={stage.id} className={cn('flex-1 px-3 py-2.5 space-y-1.5', D_BG[level])}>
                  {stage.customerGoalEn ? (
                    <p className="text-[10px] text-muted-foreground italic leading-snug line-clamp-2">
                      "{stage.customerGoalEn}"
                    </p>
                  ) : (
                    <p className="text-[10px] text-muted-foreground/50 italic">No goal defined</p>
                  )}

                  <div className="flex flex-wrap items-center gap-1.5">
                    <span className="inline-flex items-center gap-1 text-[10px] bg-card/70 text-muted-foreground px-1.5 py-0.5 rounded">
                      <Layers className="size-2.5" />{stage.sequenceFlag}
                    </span>
                    <span className="inline-flex items-center gap-1 text-[10px] bg-card/70 text-muted-foreground px-1.5 py-0.5 rounded">
                      <Target className="size-2.5" />{stage.touchpoints.length} TP{stage.touchpoints.length !== 1 ? 's' : ''}
                    </span>
                    {kpiCount > 0 && (
                      <span className="inline-flex items-center gap-1 text-[10px] bg-card/70 text-muted-foreground px-1.5 py-0.5 rounded">
                        <Zap className="size-2.5" />{kpiCount} KPI{kpiCount !== 1 ? 's' : ''}
                      </span>
                    )}
                  </div>
                </div>
              )
            })}
          </div>

          {/* ── Touchpoint zone ────────────────────────────────────────────── */}
          <div className="flex divide-x divide-border border-t border-border/60">
            {stages.map((stage, i) => {
              const { score } = computed[i]
              const level = perfLevel(score)
              return (
                <div
                  key={stage.id}
                  className={cn('flex-1 p-2.5 space-y-1.5', D_BG[level])}
                  style={{ minHeight: 80 }}
                >
                  {stage.touchpoints.length === 0 ? (
                    <p className="text-[10px] text-muted-foreground text-center py-4 italic">No touchpoints</p>
                  ) : (
                    stage.touchpoints.map(tp => (
                      <button
                        key={tp.id}
                        type="button"
                        className={cn(
                          'w-full text-start leading-tight px-2.5 py-1.5 rounded-md',
                          'bg-card/80 hover:bg-card border border-border/40 hover:border-border/80',
                          'text-foreground transition-colors flex items-center gap-2 min-w-0'
                        )}
                        onMouseEnter={e => {
                          const rect = e.currentTarget.getBoundingClientRect()
                          setTooltip({ tp, stage, x: rect.right, y: rect.top })
                        }}
                        onMouseLeave={() => setTooltip(null)}
                      >
                        {tp.isMoT && (
                          <AlertTriangle className="size-3.5 text-[#E8A020] shrink-0" aria-label="Moment of Truth" />
                        )}
                        <span className="truncate text-[11px] font-medium">{tp.nameEn}</span>
                      </button>
                    ))
                  )}
                </div>
              )
            })}
          </div>

        </div>
      </div>

      {tooltip && <TouchpointTooltip data={tooltip} />}
    </>
  )
}
