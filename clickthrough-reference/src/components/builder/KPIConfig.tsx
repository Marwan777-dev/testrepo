import { Button } from '@/components/ui/button'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Input } from '@/components/ui/input'
import { AlertTriangle, Plus, Trash2, Shuffle } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { KPIBinding, KPIType } from '@/types/journey'

const KPI_TYPES: KPIType[] = ['NPS', 'CSAT', 'CES', 'FCR', 'Sentiment']

interface Props {
  bindings: KPIBinding[]
  onChange: (bindings: KPIBinding[]) => void
  npsWarningDismissed: boolean
  onDismissNpsWarning: () => void
}

let kpiIdCounter = 1000

export default function KPIConfig({ bindings, onChange, npsWarningDismissed, onDismissNpsWarning }: Props) {
  const total = bindings.reduce((s, b) => s + b.weightPct, 0)
  const hasNps = bindings.some(b => b.kpiType === 'NPS')
  const showNpsWarning = hasNps && !npsWarningDismissed
  const isOverLimit = bindings.length >= 5
  const weightError = bindings.length > 0 && total !== 100

  function addKPI() {
    if (isOverLimit) return
    const newBinding: KPIBinding = {
      id: `kpi-new-${kpiIdCounter++}`,
      kpiType: 'CSAT',
      weightPct: bindings.length === 0 ? 100 : 0,
    }
    onChange([...bindings, newBinding])
  }

  function removeKPI(id: string) {
    onChange(bindings.filter(b => b.id !== id))
  }

  function updateType(id: string, kpiType: KPIType) {
    onChange(bindings.map(b => b.id === id ? { ...b, kpiType } : b))
  }

  function updateWeight(id: string, val: string) {
    const num = Math.min(100, Math.max(0, parseInt(val) || 0))
    onChange(bindings.map(b => b.id === id ? { ...b, weightPct: num } : b))
  }

  function autoBalance() {
    if (bindings.length === 0) return
    const base = Math.floor(100 / bindings.length)
    const remainder = 100 - base * bindings.length
    onChange(bindings.map((b, i) => ({ ...b, weightPct: i === 0 ? base + remainder : base })))
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <h4 className="text-sm font-semibold text-foreground">KPI Configuration</h4>
        <div className="flex items-center gap-2">
          {bindings.length > 0 && (
            <Button variant="ghost" size="sm" onClick={autoBalance} className="text-xs gap-1 h-7">
              <Shuffle className="size-3" /> Auto-balance
            </Button>
          )}
          {!isOverLimit && (
            <Button variant="secondary" size="sm" onClick={addKPI} className="text-xs gap-1 h-7">
              <Plus className="size-3" /> Add KPI
            </Button>
          )}
        </div>
      </div>

      {/* NPS advisory */}
      {showNpsWarning && (
        <div className="rounded-md bg-[#FFF0CC] dark:bg-[#7A5000]/20 border border-[#E8A020]/30 p-3 text-xs text-[#7A5000] dark:text-[#FFF0CC]">
          <p className="font-medium mb-1 flex items-center gap-1.5">
            <AlertTriangle className="size-3.5 shrink-0" />
            NPS measures overall loyalty, not single-touchpoint satisfaction.
          </p>
          <p className="mb-2">Consider using CSAT or CES for this touchpoint.</p>
          <div className="flex gap-2">
            <button
              type="button"
              onClick={onDismissNpsWarning}
              className="text-[#7A5000] dark:text-[#FFF0CC] underline font-medium hover:no-underline"
            >
              Use NPS anyway
            </button>
            <span className="text-[#7A5000]/50">·</span>
            <button
              type="button"
              onClick={() => {
                onChange(bindings.filter(b => b.kpiType !== 'NPS'))
                onDismissNpsWarning()
              }}
              className="text-[#7A5000] dark:text-[#FFF0CC] underline font-medium hover:no-underline"
            >
              Choose another KPI
            </button>
          </div>
        </div>
      )}

      {/* Empty state */}
      {bindings.length === 0 && (
        <div className="rounded-md border border-dashed border-border p-4 text-center">
          <AlertTriangle className="size-5 text-muted-foreground mx-auto mb-1.5" />
          <p className="text-sm text-muted-foreground">No KPIs configured</p>
          <p className="text-xs text-muted-foreground mt-0.5">Add at least one KPI to measure this touchpoint.</p>
        </div>
      )}

      {/* KPI rows */}
      {bindings.length > 0 && (
        <div className="space-y-2">
          {bindings.map(b => (
            <div key={b.id} className="flex items-center gap-2">
              <Select value={b.kpiType} onValueChange={v => updateType(b.id, v as KPIType)}>
                <SelectTrigger className="flex-1 h-8 text-xs">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {KPI_TYPES.map(k => (
                    <SelectItem key={k} value={k} className="text-xs">{k}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <div className="relative w-20">
                <Input
                  type="number"
                  min={0}
                  max={100}
                  value={b.weightPct}
                  onChange={e => updateWeight(b.id, e.target.value)}
                  className="h-8 text-xs pe-5 tabular-nums"
                />
                <span className="absolute end-2 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">%</span>
              </div>
              <Button
                variant="ghost"
                size="icon"
                aria-label="Remove KPI"
                onClick={() => removeKPI(b.id)}
                className="size-8 text-muted-foreground hover:text-destructive shrink-0"
              >
                <Trash2 className="size-3.5" />
              </Button>
            </div>
          ))}

          {/* Weight sum indicator */}
          <div className={cn(
            'flex items-center justify-between rounded-md px-3 py-1.5 text-xs font-medium',
            weightError
              ? 'bg-[#FFD6DA] text-[#6B0010] dark:bg-[#6B0010]/20 dark:text-[#FFD6DA]'
              : 'bg-[#C8F5DB] text-[#156632] dark:bg-[#156632]/20 dark:text-[#C8F5DB]'
          )}>
            <span>Total weight</span>
            <span className="tabular-nums font-bold">{total}%</span>
          </div>
          {weightError && (
            <p className="text-xs text-destructive" role="alert">
              Weights must add up to 100%. Currently at {total}%.
            </p>
          )}
        </div>
      )}
    </div>
  )
}
