import { useMemo, useState } from 'react'
import { useParams, useNavigate } from 'react-router'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  AreaChart,
  Area,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  Cell,
} from 'recharts'
import {
  ArrowLeft,
  TrendingUp,
  TrendingDown,
  Users,
  Zap,
  BarChart2,
  Map,
  AlertTriangle,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { mockJourneys } from '@/data/mockJourneys'
import type { Journey, JourneyStatus, JourneyType } from '@/types/journey'
import SwimLanes from '@/components/journeys/SwimLanes'

// ─── Semantic D1–D5 ──────────────────────────────────────
const D1 = '#1A7A3C'
const D2 = '#2EB85C'
const D3 = '#E8A020'
const D4 = '#E05C1A'
const D5 = '#C01B2A'

function perfColor(v: number) {
  if (v >= 85) return D1
  if (v >= 75) return D2
  if (v >= 60) return D3
  if (v >= 45) return D4
  return D5
}

function perfBg(v: number) {
  if (v >= 85) return 'bg-[#D4F4E2] text-[#0D4A24] dark:bg-[#0D4A24]/30 dark:text-[#D4F4E2]'
  if (v >= 75) return 'bg-[#C8F5DB] text-[#156632] dark:bg-[#156632]/20 dark:text-[#C8F5DB]'
  if (v >= 60) return 'bg-[#FFF0CC] text-[#7A5000] dark:bg-[#7A5000]/20 dark:text-[#FFF0CC]'
  if (v >= 45) return 'bg-[#FFE4D0] text-[#7A2800] dark:bg-[#7A2800]/20 dark:text-[#FFE4D0]'
  return 'bg-[#FFD6DA] text-[#6B0010] dark:bg-[#6B0010]/20 dark:text-[#FFD6DA]'
}

const statusConfig: Record<JourneyStatus, string> = {
  Active: 'bg-[#C8F5DB] text-[#156632] dark:bg-[#156632]/20 dark:text-[#C8F5DB]',
  Draft: 'bg-[#FFF0CC] text-[#7A5000] dark:bg-[#7A5000]/20 dark:text-[#FFF0CC]',
  Archived: 'bg-muted text-muted-foreground',
}

const typeConfig: Record<JourneyType, string> = {
  Transactional: 'bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan/10 dark:text-nb-cyan-300',
  Lifecycle: 'bg-nb-mint-100 text-nb-mint-800 dark:bg-nb-mint/10 dark:text-nb-mint-300',
  'Issue-Resolution': 'bg-[#FFE4D0] text-[#7A2800] dark:bg-[#7A2800]/20 dark:text-[#FFE4D0]',
  Onboarding: 'bg-nb-navy-100 text-nb-navy dark:bg-nb-navy/20 dark:text-nb-navy-200',
}

// ─── Deterministic mock score generator ─────────────────
function seedScore(seed: string, min: number, max: number): number {
  let h = 0
  for (let i = 0; i < seed.length; i++) h = (Math.imul(31, h) + seed.charCodeAt(i)) | 0
  return min + (Math.abs(h) % (max - min + 1))
}

function buildMockData(journey: Journey) {
  const totalTouchpoints = journey.stages.reduce((s, st) => s + st.touchpoints.length, 0)

  // Overall KPIs
  const overallCsat = seedScore(journey.id + 'csat', 68, 94)
  const overallCes  = seedScore(journey.id + 'ces',  62, 91)
  const overallNps  = seedScore(journey.id + 'nps',  24, 68)
  const responseRate = seedScore(journey.id + 'resp', 54, 88)
  const responses   = seedScore(journey.id + 'resp2', 400, 3200)

  // Stage scores
  const stageScores = journey.stages.map(s => ({
    name: s.nameEn.length > 20 ? s.nameEn.slice(0, 18) + '…' : s.nameEn,
    score: seedScore(s.id + 'score', 55, 95),
    responses: seedScore(s.id + 'r', 80, 600),
  }))

  // Touchpoint scores (top ones only)
  const touchpointScores = journey.stages.flatMap((s, si) =>
    s.touchpoints.map((tp, ti) => ({
      name: tp.nameEn,
      stage: s.nameEn,
      score: seedScore(tp.id + 'sc', 50, 98),
      isMoT: tp.isMoT,
      kpiCount: tp.kpiBindings.length,
      stageIdx: si + 1,
      tpIdx: ti + 1,
    }))
  ).sort((a, b) => b.score - a.score)

  // 12-week trend
  const trend = Array.from({ length: 12 }, (_, i) => {
    const base = overallCsat
    const delta = seedScore(journey.id + i + 'trend', -8, 8)
    return {
      week: `W${i + 1}`,
      csat: Math.min(100, Math.max(30, base + delta - 6 + i * 0.5)),
      nps: Math.min(100, Math.max(10, overallNps + seedScore(journey.id + i + 'nps', -6, 6))),
    }
  })

  return { overallCsat, overallCes, overallNps, responseRate, responses, totalTouchpoints, stageScores, touchpointScores, trend }
}

// ─── Subcomponents ───────────────────────────────────────
function KpiCard({ label, value, unit = '', trend, color, sub }: {
  label: string; value: number; unit?: string; trend: number; color: string; sub?: string
}) {
  const positive = trend >= 0
  return (
    <Card className="shadow-sm dark:shadow-none">
      <CardContent className="p-5">
        <p className="text-xs font-medium uppercase tracking-widest text-muted-foreground mb-2">{label}</p>
        <div className="flex items-end gap-2">
          <span className="text-3xl font-heading font-bold tabular-nums" style={{ color }}>
            {value}{unit}
          </span>
          <span className={cn('text-xs font-medium mb-1 flex items-center gap-0.5', positive ? 'text-[#2EB85C]' : 'text-[#E05C1A]')}>
            {positive ? <TrendingUp className="size-3" /> : <TrendingDown className="size-3" />}
            {positive ? '+' : ''}{trend}
          </span>
        </div>
        {sub && <p className="text-xs text-muted-foreground mt-1">{sub}</p>}
      </CardContent>
    </Card>
  )
}

export default function JourneyStatsPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const journey = mockJourneys.find(j => j.id === id)

  const data = useMemo(() => journey ? buildMockData(journey) : null, [journey])
  const [visibleKpis, setVisibleKpis] = useState<Record<string, boolean>>({ csat: true, nps: true })

  function toggleKpi(key: string) {
    setVisibleKpis(prev => {
      const next = { ...prev, [key]: !prev[key] }
      const anyVisible = Object.values(next).some(Boolean)
      return anyVisible ? next : prev
    })
  }

  if (!journey || !data) {
    return (
      <div className="px-8 flex flex-col items-center justify-center py-24 text-center space-y-3">
        <Map className="size-12 text-muted-foreground" />
        <h2 className="text-xl font-heading font-bold">Journey not found</h2>
        <Button onClick={() => navigate('/journeys')}>
          <ArrowLeft className="size-4 me-2" /> Back to Journeys
        </Button>
      </div>
    )
  }

  const { overallCsat, overallCes, overallNps, responseRate, responses, totalTouchpoints, stageScores, touchpointScores, trend } = data
  const moTCount = journey.stages.flatMap(s => s.touchpoints).filter(t => t.isMoT).length

  return (
    <div className="px-8 space-y-5 py-5">

      {/* Breadcrumb */}
      <button
        type="button"
        onClick={() => navigate('/journeys')}
        className="flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground transition-colors"
      >
        <ArrowLeft className="size-4" /> Customer Journeys
      </button>

      {/* Page header */}
      <div className="flex flex-col sm:flex-row sm:items-center gap-4">
        <div className="flex-1">
          <div className="flex flex-wrap items-center gap-2 mb-1">
            <BarChart2 className="size-5 text-primary" />
            <h1 className="text-2xl font-heading font-bold text-foreground">{journey.nameEn}</h1>
            <Badge className={cn('text-xs border-0', statusConfig[journey.status])}>{journey.status}</Badge>
            {journey.version && <Badge variant="outline" className="text-xs font-mono">v{journey.version}</Badge>}
            <Badge className={cn('text-xs border-0', typeConfig[journey.journeyType])}>{journey.journeyType}</Badge>
          </div>
          <p className="text-sm text-muted-foreground">Journey performance analytics</p>
        </div>
        <Button
          variant="secondary"
          onClick={() => navigate(`/journeys/${journey.id}`)}
          className="gap-1.5 shrink-0"
        >
          <Map className="size-4" /> Open Builder
        </Button>
      </div>

      {/* KPI cards */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <KpiCard label="Overall CSAT" value={overallCsat} unit="%" trend={+4} color={perfColor(overallCsat)} sub={`${responses.toLocaleString()} responses`} />
        <KpiCard label="Avg CES" value={overallCes} unit="%" trend={+2} color={perfColor(overallCes)} sub="Higher is better" />
        <KpiCard label="NPS Score" value={overallNps} unit="" trend={+6} color={perfColor(overallNps)} sub="Net Promoter Score" />
        <KpiCard label="Response Rate" value={responseRate} unit="%" trend={+1} color={perfColor(responseRate)} sub={`${journey.stages.length} stages · ${totalTouchpoints} TPs`} />
      </div>

      {/* Summary chips */}
      <div className="flex flex-wrap gap-3">
        {[
          { icon: Map, label: `${journey.stages.length} Stages`, className: 'bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan/10 dark:text-nb-cyan-300' },
          { icon: Zap, label: `${totalTouchpoints} Touchpoints`, className: 'bg-nb-navy-100 text-nb-navy dark:bg-nb-navy/20 dark:text-nb-navy-200' },
          { icon: AlertTriangle, label: `${moTCount} Moments of Truth`, className: 'bg-[#FFF0CC] text-[#7A5000] dark:bg-[#7A5000]/20 dark:text-[#FFF0CC]' },
          { icon: Users, label: `${responses.toLocaleString()} Responses`, className: 'bg-nb-mint-100 text-nb-mint-800 dark:bg-nb-mint/10 dark:text-nb-mint-300' },
        ].map(({ icon: Icon, label, className }) => (
          <Badge key={label} className={cn('gap-1.5 px-3 py-1.5 text-xs font-medium border-0', className)}>
            <Icon className="size-3" />{label}
          </Badge>
        ))}
      </div>

      {/* Journey swim lanes */}
      <div>
        <h2 className="text-base font-bold text-foreground mb-3">Journey Map</h2>
        <SwimLanes stages={journey.stages} />
      </div>

      {/* Charts row */}
      <div className="grid grid-cols-1 lg:grid-cols-5 gap-6">

        {/* Trend chart — 3 cols */}
        <Card className="lg:col-span-3 shadow-sm dark:shadow-none">
          <CardHeader className="pb-2">
            <CardTitle className="text-base font-bold">Performance Trend</CardTitle>
            <CardDescription>CSAT & NPS over the last 12 weeks</CardDescription>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={220}>
              <AreaChart data={trend} margin={{ top: 4, right: 4, left: -20, bottom: 0 }}>
                <defs>
                  <linearGradient id="csatGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#0D8BBC" stopOpacity={0.25} />
                    <stop offset="95%" stopColor="#0D8BBC" stopOpacity={0.02} />
                  </linearGradient>
                  <linearGradient id="npsGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#13DB9B" stopOpacity={0.2} />
                    <stop offset="95%" stopColor="#13DB9B" stopOpacity={0.02} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" strokeOpacity={0.5} />
                <XAxis dataKey="week" tick={{ fontSize: 11 }} stroke="var(--muted-foreground)" />
                <YAxis domain={[0, 100]} tick={{ fontSize: 11 }} stroke="var(--muted-foreground)" />
                <Tooltip
                  contentStyle={{ borderRadius: 8, border: '1px solid var(--border)', background: 'var(--card)', fontSize: 12 }}
                  formatter={(v, name) => [`${Number(v).toFixed(1)}${name === 'csat' ? '%' : ''}`, String(name).toUpperCase()]}
                />
                {visibleKpis.csat && (
                  <Area type="monotone" dataKey="csat" stroke="#0D8BBC" strokeWidth={2.5} fill="url(#csatGrad)" dot={{ r: 3, fill: '#0D8BBC' }} name="csat" />
                )}
                {visibleKpis.nps && (
                  <Area type="monotone" dataKey="nps" stroke="#13DB9B" strokeWidth={2} strokeDasharray="6 3" fill="url(#npsGrad)" dot={{ r: 3, fill: '#13DB9B' }} name="nps" />
                )}
              </AreaChart>
            </ResponsiveContainer>
            <div className="flex items-center gap-3 mt-3">
              {[
                { key: 'csat', label: 'CSAT %', color: '#0D8BBC' },
                { key: 'nps', label: 'NPS', color: '#13DB9B' },
              ].map(({ key, label, color }) => {
                const active = visibleKpis[key]
                return (
                  <button
                    key={key}
                    type="button"
                    onClick={() => toggleKpi(key)}
                    className={cn(
                      'flex items-center gap-1.5 text-xs rounded-full px-2.5 py-1 border transition-all',
                      active
                        ? 'border-border text-foreground'
                        : 'border-transparent text-muted-foreground/40 line-through'
                    )}
                  >
                    <span className="size-2.5 rounded-full inline-block shrink-0 transition-opacity" style={{ background: color, opacity: active ? 1 : 0.3 }} />
                    {label}
                  </button>
                )
              })}
            </div>
          </CardContent>
        </Card>

        {/* Stage bar chart — 2 cols */}
        <Card className="lg:col-span-2 shadow-sm dark:shadow-none">
          <CardHeader className="pb-2">
            <CardTitle className="text-base font-bold">Score by Stage</CardTitle>
            <CardDescription>Average satisfaction per stage</CardDescription>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={stageScores} layout="vertical" margin={{ top: 0, right: 24, left: 0, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" horizontal={false} stroke="var(--border)" strokeOpacity={0.5} />
                <XAxis type="number" domain={[0, 100]} tick={{ fontSize: 10 }} stroke="var(--muted-foreground)" />
                <YAxis type="category" dataKey="name" tick={{ fontSize: 10 }} width={90} stroke="var(--muted-foreground)" />
                <Tooltip
                  contentStyle={{ borderRadius: 8, border: '1px solid var(--border)', background: 'var(--card)', fontSize: 12 }}
                  formatter={(v) => [`${v}%`, 'Score']}
                />
                <Bar dataKey="score" radius={[0, 4, 4, 0]}>
                  {stageScores.map((s, i) => (
                    <Cell key={i} fill={perfColor(s.score)} />
                  ))}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>
      </div>

      {/* Touchpoint performance table */}
      <Card className="shadow-sm dark:shadow-none">
        <CardHeader className="pb-2">
          <CardTitle className="text-base font-bold">Touchpoint Performance</CardTitle>
          <CardDescription>All touchpoints ranked by satisfaction score</CardDescription>
        </CardHeader>
        <CardContent className="p-0">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border bg-muted/30">
                  <th className="text-start px-4 py-2.5 text-xs font-semibold text-muted-foreground w-12">#</th>
                  <th className="text-start px-4 py-2.5 text-xs font-semibold text-muted-foreground">Touchpoint</th>
                  <th className="text-start px-4 py-2.5 text-xs font-semibold text-muted-foreground">Stage</th>
                  <th className="text-center px-4 py-2.5 text-xs font-semibold text-muted-foreground">KPIs</th>
                  <th className="text-center px-4 py-2.5 text-xs font-semibold text-muted-foreground">MoT</th>
                  <th className="text-end px-4 py-2.5 text-xs font-semibold text-muted-foreground w-40">Score</th>
                </tr>
              </thead>
              <tbody>
                {touchpointScores.map((tp, i) => (
                  <tr key={i} className="border-b border-border/50 hover:bg-muted/30 transition-colors">
                    <td className="px-4 py-2.5 text-xs text-muted-foreground tabular-nums">{tp.stageIdx}.{tp.tpIdx}</td>
                    <td className="px-4 py-2.5 font-medium text-foreground">{tp.name}</td>
                    <td className="px-4 py-2.5 text-muted-foreground text-xs">{tp.stage}</td>
                    <td className="px-4 py-2.5 text-center">
                      {tp.kpiCount > 0
                        ? <Badge variant="secondary" className="text-[10px] h-5">{tp.kpiCount} KPI{tp.kpiCount > 1 ? 's' : ''}</Badge>
                        : <span className="flex items-center justify-center gap-1 text-xs text-muted-foreground"><AlertTriangle className="size-3" /> None</span>}
                    </td>
                    <td className="px-4 py-2.5 text-center">
                      {tp.isMoT && <AlertTriangle className="size-4 text-[#E8A020] mx-auto" aria-label="Moment of Truth" />}
                    </td>
                    <td className="px-4 py-2.5">
                      <div className="flex items-center justify-end gap-2">
                        {/* Mini bar */}
                        <div className="w-20 h-1.5 rounded-full bg-muted overflow-hidden">
                          <div
                            className="h-full rounded-full transition-all"
                            style={{ width: `${tp.score}%`, background: perfColor(tp.score) }}
                          />
                        </div>
                        <span className={cn('text-xs font-bold tabular-nums px-1.5 py-0.5 rounded', perfBg(tp.score))}>
                          {tp.score}%
                        </span>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>

    </div>
  )
}
