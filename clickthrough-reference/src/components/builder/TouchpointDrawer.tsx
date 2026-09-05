import { useState, useEffect } from 'react'
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetFooter } from '@/components/ui/sheet'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { Switch } from '@/components/ui/switch'
import { AlertTriangle, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import KPIConfig from './KPIConfig'
import type { Touchpoint, ChannelType } from '@/types/journey'

const ALL_CHANNELS: ChannelType[] = [
  'Web', 'Mobile App', 'Email', 'SMS', 'WhatsApp',
  'Phone (Inbound)', 'Phone (Outbound)', 'Branch/In-Person',
  'Chat', 'IVR', 'Social Media', 'Kiosk', 'Other',
]

const IMPORTANCE_LABELS = ['', 'Very Low', 'Low', 'Medium', 'High', 'Critical']

interface Props {
  open: boolean
  touchpoint: Touchpoint | null
  onClose: () => void
  onSave: (tp: Touchpoint) => void
}

export default function TouchpointDrawer({ open, touchpoint, onClose, onSave }: Props) {
  const [nameEn, setNameEn] = useState('')
  const [nameAr, setNameAr] = useState('')
  const [descriptionEn, setDescriptionEn] = useState('')
  const [descriptionAr, setDescriptionAr] = useState('')
  const [channels, setChannels] = useState<ChannelType[]>([])
  const [importanceCustomer, setImportanceCustomer] = useState(3)
  const [importanceBusiness, setImportanceBusiness] = useState(3)
  const [isMoT, setIsMoT] = useState(false)
  const [isMandatory, setIsMandatory] = useState(true)
  const [kpiBindings, setKpiBindings] = useState(touchpoint?.kpiBindings ?? [])
  const [npsWarningDismissed, setNpsWarningDismissed] = useState(false)
  const [motSuggestionDismissed, setMotSuggestionDismissed] = useState(false)
  const [errors, setErrors] = useState<Record<string, string>>({})

  useEffect(() => {
    if (open && touchpoint) {
      setNameEn(touchpoint.nameEn)
      setNameAr(touchpoint.nameAr)
      setDescriptionEn(touchpoint.descriptionEn ?? '')
      setDescriptionAr(touchpoint.descriptionAr ?? '')
      setChannels([...touchpoint.channels])
      setImportanceCustomer(touchpoint.importanceCustomer)
      setImportanceBusiness(touchpoint.importanceBusiness)
      setIsMoT(touchpoint.isMoT)
      setIsMandatory(touchpoint.isMandatory)
      setKpiBindings([...touchpoint.kpiBindings])
      setNpsWarningDismissed(false)
      setMotSuggestionDismissed(false)
      setErrors({})
    }
  }, [open, touchpoint])

  const showMotSuggestion = importanceCustomer >= 4 && !isMoT && !motSuggestionDismissed

  function validate() {
    const e: Record<string, string> = {}
    if (!nameEn.trim()) e.nameEn = 'Name (EN) is required'
    if (!nameAr.trim()) e.nameAr = 'Name (AR) is required'
    const total = kpiBindings.reduce((s, b) => s + b.weightPct, 0)
    if (kpiBindings.length > 0 && total !== 100) {
      e.kpi = `KPI weights must sum to 100% (currently ${total}%)`
    }
    setErrors(e)
    return Object.keys(e).length === 0
  }

  function handleSave() {
    if (!validate() || !touchpoint) return
    onSave({
      ...touchpoint,
      nameEn: nameEn.trim(),
      nameAr: nameAr.trim(),
      descriptionEn: descriptionEn.trim() || undefined,
      descriptionAr: descriptionAr.trim() || undefined,
      channels,
      importanceCustomer,
      importanceBusiness,
      isMoT,
      isMandatory,
      kpiBindings,
    })
  }

  function toggleChannel(c: ChannelType) {
    setChannels(prev => prev.includes(c) ? prev.filter(x => x !== c) : [...prev, c])
  }

  function ImportanceStars({ value, onChange }: { value: number; onChange: (v: number) => void }) {
    return (
      <div className="flex items-center gap-1">
        {[1, 2, 3, 4, 5].map(n => (
          <button
            key={n}
            type="button"
            onClick={() => onChange(n)}
            className={cn(
              'size-7 rounded-md text-xs font-bold border transition-colors',
              n <= value
                ? 'bg-primary text-primary-foreground border-primary'
                : 'bg-muted text-muted-foreground border-border hover:border-primary/50'
            )}
          >
            {n}
          </button>
        ))}
        <span className="text-xs text-muted-foreground ms-1">{IMPORTANCE_LABELS[value]}</span>
      </div>
    )
  }

  return (
    <Sheet open={open} onOpenChange={v => !v && onClose()}>
      <SheetContent side="right" className="w-full sm:max-w-lg flex flex-col gap-0 p-0 pb-[50px]">
        <SheetHeader className="px-6 pt-6 pb-4 border-b border-border">
          <SheetTitle className="text-lg font-heading font-bold">Configure Touchpoint</SheetTitle>
          {touchpoint && (
            <p className="text-sm text-muted-foreground">{touchpoint.nameEn}</p>
          )}
        </SheetHeader>

        <div className="flex-1 overflow-y-auto px-6 py-5 space-y-6">
          {/* Section 1: Basic Info */}
          <div className="space-y-4">
            <h3 className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">Basic Information</h3>

            {/* MoT suggestion */}
            {showMotSuggestion && (
              <div className="rounded-md bg-[#CCF0FB] dark:bg-nb-cyan/10 border border-nb-cyan/30 p-3 text-xs text-nb-cyan-800 dark:text-nb-cyan-300">
                <p className="font-medium mb-1">High customer importance — mark as Moment of Truth?</p>
                <div className="flex gap-2">
                  <button type="button" onClick={() => { setIsMoT(true); setMotSuggestionDismissed(true) }} className="underline font-medium hover:no-underline">Yes</button>
                  <span className="opacity-50">·</span>
                  <button type="button" onClick={() => setMotSuggestionDismissed(true)} className="underline font-medium hover:no-underline">Skip</button>
                </div>
              </div>
            )}

            <div className="space-y-1.5">
              <Label htmlFor="tpNameEn">Name (English) <span className="text-destructive">*</span></Label>
              <Input id="tpNameEn" value={nameEn} onChange={e => setNameEn(e.target.value)} className={cn(errors.nameEn && 'border-destructive')} />
              {errors.nameEn && <p className="text-sm text-destructive" role="alert">{errors.nameEn}</p>}
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="tpNameAr">Name (Arabic) <span className="text-destructive">*</span></Label>
              <Input id="tpNameAr" dir="rtl" value={nameAr} onChange={e => setNameAr(e.target.value)} className={cn(errors.nameAr && 'border-destructive')} />
              {errors.nameAr && <p className="text-sm text-destructive" role="alert">{errors.nameAr}</p>}
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="tpDescEn">Description (English)</Label>
              <Textarea id="tpDescEn" value={descriptionEn} onChange={e => setDescriptionEn(e.target.value)} rows={2} />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="tpDescAr">Description (Arabic)</Label>
              <Textarea id="tpDescAr" dir="rtl" value={descriptionAr} onChange={e => setDescriptionAr(e.target.value)} rows={2} />
            </div>

            {/* Channels */}
            <div className="space-y-2">
              <Label>Channels</Label>
              <div className="flex flex-wrap gap-1.5">
                {ALL_CHANNELS.map(c => {
                  const active = channels.includes(c)
                  return (
                    <button
                      key={c}
                      type="button"
                      onClick={() => toggleChannel(c)}
                      className={cn(
                        'inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-medium border transition-colors',
                        active
                          ? 'bg-primary text-primary-foreground border-primary'
                          : 'bg-muted text-muted-foreground border-border hover:border-primary/50'
                      )}
                    >
                      {active && <X className="size-2.5" />}
                      {c}
                    </button>
                  )
                })}
              </div>
            </div>

            {/* Importance sliders */}
            <div className="space-y-3">
              <div className="space-y-1.5">
                <Label>Importance to Customer</Label>
                <ImportanceStars value={importanceCustomer} onChange={setImportanceCustomer} />
              </div>
              <div className="space-y-1.5">
                <Label>Importance to Business</Label>
                <ImportanceStars value={importanceBusiness} onChange={setImportanceBusiness} />
              </div>
            </div>

            {/* Toggles */}
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <div>
                  <Label htmlFor="mot-toggle" className="text-sm font-medium flex items-center gap-1.5">
                    <AlertTriangle className="size-4 text-[#E8A020]" />
                    Moment of Truth
                  </Label>
                  <p className="text-xs text-muted-foreground">Critical touchpoint that defines customer perception</p>
                </div>
                <Switch
                  id="mot-toggle"
                  checked={isMoT}
                  onCheckedChange={v => { setIsMoT(v); setMotSuggestionDismissed(true) }}
                />
              </div>
              <div className="flex items-center justify-between">
                <div>
                  <Label htmlFor="mandatory-toggle" className="text-sm font-medium">Mandatory</Label>
                  <p className="text-xs text-muted-foreground">Customer must pass through this touchpoint</p>
                </div>
                <Switch id="mandatory-toggle" checked={isMandatory} onCheckedChange={setIsMandatory} />
              </div>
            </div>
          </div>

          {/* Divider */}
          <div className="border-t border-border" />

          {/* Section 2: KPI Config */}
          <KPIConfig
            bindings={kpiBindings}
            onChange={setKpiBindings}
            npsWarningDismissed={npsWarningDismissed}
            onDismissNpsWarning={() => setNpsWarningDismissed(true)}
          />

          {errors.kpi && (
            <p className="text-xs text-destructive flex items-center gap-1.5" role="alert">
              <AlertTriangle className="size-3.5" /> {errors.kpi}
            </p>
          )}
        </div>

        <SheetFooter className="px-6 pt-3 pb-1 border-t border-border flex flex-col gap-2">
          <Button onClick={handleSave} className="w-full h-10 rounded-md text-sm font-semibold bg-primary hover:bg-nb-cyan-700 text-primary-foreground">
            Save Changes
          </Button>
          <Button variant="ghost" onClick={onClose} className="w-full h-10 rounded-md text-sm font-medium bg-muted hover:bg-muted/80 text-muted-foreground">
            Cancel
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  )
}
