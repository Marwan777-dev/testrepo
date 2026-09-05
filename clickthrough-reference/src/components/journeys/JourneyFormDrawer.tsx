import { useState, useEffect } from 'react'
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetFooter } from '@/components/ui/sheet'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Badge } from '@/components/ui/badge'
import { X } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { Journey, JourneyType } from '@/types/journey'

const JOURNEY_TYPES: JourneyType[] = ['Transactional', 'Lifecycle', 'Issue-Resolution', 'Onboarding']
const PERSONAS = ['Retail Customer', 'Digital User', 'Business Owner', 'SME', 'Premium Customer', 'Government Client']

interface Props {
  open: boolean
  journey?: Journey | null
  onClose: () => void
  onSave: (data: Partial<Journey>) => void
}

export default function JourneyFormDrawer({ open, journey, onClose, onSave }: Props) {
  const isEdit = !!journey
  const [nameEn, setNameEn] = useState('')
  const [nameAr, setNameAr] = useState('')
  const [descriptionEn, setDescriptionEn] = useState('')
  const [descriptionAr, setDescriptionAr] = useState('')
  const [journeyType, setJourneyType] = useState<JourneyType | ''>('')
  const [expectedDurationDays, setExpectedDurationDays] = useState('')
  const [personas, setPersonas] = useState<string[]>([])
  const [errors, setErrors] = useState<Record<string, string>>({})

  useEffect(() => {
    if (open) {
      setNameEn(journey?.nameEn ?? '')
      setNameAr(journey?.nameAr ?? '')
      setDescriptionEn(journey?.descriptionEn ?? '')
      setDescriptionAr(journey?.descriptionAr ?? '')
      setJourneyType(journey?.journeyType ?? '')
      setExpectedDurationDays(journey?.expectedDurationDays?.toString() ?? '')
      setPersonas(journey?.personas ?? [])
      setErrors({})
    }
  }, [open, journey])

  function validate() {
    const e: Record<string, string> = {}
    if (!nameEn.trim()) e.nameEn = 'Journey name (EN) is required'
    if (!nameAr.trim()) e.nameAr = 'Journey name (AR) is required'
    if (!journeyType) e.journeyType = 'Journey type is required'
    setErrors(e)
    return Object.keys(e).length === 0
  }

  function handleSave() {
    if (!validate()) return
    onSave({
      nameEn: nameEn.trim(),
      nameAr: nameAr.trim(),
      descriptionEn: descriptionEn.trim() || undefined,
      descriptionAr: descriptionAr.trim() || undefined,
      journeyType: journeyType as JourneyType,
      expectedDurationDays: expectedDurationDays ? parseInt(expectedDurationDays) : undefined,
      personas,
    })
  }

  function togglePersona(p: string) {
    setPersonas(prev => prev.includes(p) ? prev.filter(x => x !== p) : [...prev, p])
  }

  return (
    <Sheet open={open} onOpenChange={v => !v && onClose()}>
      <SheetContent side="right" className="w-full sm:max-w-lg flex flex-col gap-0 p-0 pb-[50px]">
        <SheetHeader className="px-6 pt-6 pb-4 border-b border-border">
          <SheetTitle className="text-lg font-heading font-bold">
            {isEdit ? 'Edit Journey' : 'New Journey'}
          </SheetTitle>
        </SheetHeader>

        <div className="flex-1 overflow-y-auto px-6 py-5 space-y-5">
          {/* Name EN */}
          <div className="space-y-1.5">
            <Label htmlFor="nameEn">
              Journey Name (English) <span className="text-destructive">*</span>
            </Label>
            <Input
              id="nameEn"
              value={nameEn}
              onChange={e => setNameEn(e.target.value)}
              placeholder="e.g. Apply for Personal Loan"
              className={cn(errors.nameEn && 'border-destructive')}
            />
            {errors.nameEn && <p className="text-sm text-destructive" role="alert">{errors.nameEn}</p>}
          </div>

          {/* Name AR */}
          <div className="space-y-1.5">
            <Label htmlFor="nameAr">
              Journey Name (Arabic) <span className="text-destructive">*</span>
            </Label>
            <Input
              id="nameAr"
              dir="rtl"
              value={nameAr}
              onChange={e => setNameAr(e.target.value)}
              placeholder="مثال: التقديم على قرض شخصي"
              className={cn(errors.nameAr && 'border-destructive')}
            />
            {errors.nameAr && <p className="text-sm text-destructive" role="alert">{errors.nameAr}</p>}
          </div>

          {/* Description EN */}
          <div className="space-y-1.5">
            <Label htmlFor="desc">Description (English)</Label>
            <Textarea
              id="desc"
              value={descriptionEn}
              onChange={e => setDescriptionEn(e.target.value)}
              placeholder="Briefly describe this journey..."
              rows={3}
            />
          </div>

          {/* Description AR */}
          <div className="space-y-1.5">
            <Label htmlFor="descAr">Description <span lang="ar">(بالعربية)</span></Label>
            <Textarea
              id="descAr"
              dir="rtl"
              value={descriptionAr}
              onChange={e => setDescriptionAr(e.target.value)}
              placeholder="اكتب وصفاً مختصراً لهذه الرحلة..."
              rows={3}
            />
          </div>

          {/* Journey Type */}
          <div className="space-y-1.5">
            <Label>
              Journey Type <span className="text-destructive">*</span>
            </Label>
            <Select value={journeyType} onValueChange={v => setJourneyType(v as JourneyType)}>
              <SelectTrigger className={cn(errors.journeyType && 'border-destructive')}>
                <SelectValue placeholder="Select type..." />
              </SelectTrigger>
              <SelectContent>
                {JOURNEY_TYPES.map(t => (
                  <SelectItem key={t} value={t}>{t}</SelectItem>
                ))}
              </SelectContent>
            </Select>
            {errors.journeyType && <p className="text-sm text-destructive" role="alert">{errors.journeyType}</p>}
          </div>

          {/* Expected Duration */}
          <div className="space-y-1.5">
            <Label htmlFor="duration">Expected Duration (days)</Label>
            <Input
              id="duration"
              type="number"
              min="1"
              value={expectedDurationDays}
              onChange={e => setExpectedDurationDays(e.target.value)}
              placeholder="e.g. 5"
            />
          </div>

          {/* Personas */}
          <div className="space-y-2">
            <Label>Personas</Label>
            <div className="flex flex-wrap gap-2">
              {PERSONAS.map(p => {
                const active = personas.includes(p)
                return (
                  <button
                    key={p}
                    type="button"
                    onClick={() => togglePersona(p)}
                    className={cn(
                      'inline-flex items-center gap-1 px-3 py-1 rounded-full text-xs font-medium border transition-colors',
                      active
                        ? 'bg-primary text-primary-foreground border-primary'
                        : 'bg-muted text-muted-foreground border-border hover:border-primary/50'
                    )}
                  >
                    {active && <X className="size-3" />}
                    {p}
                  </button>
                )
              })}
            </div>
            {personas.length > 0 && (
              <div className="flex flex-wrap gap-1 mt-1">
                {personas.map(p => (
                  <Badge key={p} variant="secondary" className="text-xs">{p}</Badge>
                ))}
              </div>
            )}
          </div>
        </div>

        <SheetFooter className="px-6 pt-3 pb-1 border-t border-border flex flex-col gap-2">
          <Button onClick={handleSave} className="w-full h-10 rounded-md text-sm font-semibold bg-primary hover:bg-nb-cyan-700 text-primary-foreground">
            {isEdit ? 'Save Changes' : 'Create Journey'}
          </Button>
          <Button variant="ghost" onClick={onClose} className="w-full h-10 rounded-md text-sm font-medium bg-muted hover:bg-muted/80 text-muted-foreground">
            Cancel
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  )
}
