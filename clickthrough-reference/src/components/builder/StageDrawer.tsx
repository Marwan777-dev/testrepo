import { useState, useEffect } from 'react'
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetFooter } from '@/components/ui/sheet'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { cn } from '@/lib/utils'
import type { EmotionType } from '@/types/journey'

const EMOTIONS: EmotionType[] = ['Excited', 'Neutral', 'Anxious', 'Frustrated', 'Confident', 'Confused', 'Relieved']

const emotionEmoji: Record<EmotionType, string> = {
  Excited: '😊',
  Neutral: '😐',
  Anxious: '😟',
  Frustrated: '😤',
  Confident: '😎',
  Confused: '😕',
  Relieved: '😌',
}

interface StageFormData {
  nameEn: string
  nameAr: string
  customerGoalEn?: string
  customerGoalAr?: string
  expectedEmotion?: EmotionType
  sequenceFlag: 'Sequential' | 'Parallel'
}

interface Props {
  open: boolean
  onClose: () => void
  onSave: (data: StageFormData) => void
}

export default function StageDrawer({ open, onClose, onSave }: Props) {
  const [nameEn, setNameEn] = useState('')
  const [nameAr, setNameAr] = useState('')
  const [goalEn, setGoalEn] = useState('')
  const [goalAr, setGoalAr] = useState('')
  const [emotion, setEmotion] = useState<EmotionType | ''>('')
  const [sequenceFlag, setSequenceFlag] = useState<'Sequential' | 'Parallel'>('Sequential')
  const [errors, setErrors] = useState<Record<string, string>>({})

  useEffect(() => {
    if (open) {
      setNameEn('')
      setNameAr('')
      setGoalEn('')
      setGoalAr('')
      setEmotion('')
      setSequenceFlag('Sequential')
      setErrors({})
    }
  }, [open])

  function validate() {
    const e: Record<string, string> = {}
    if (!nameEn.trim()) e.nameEn = 'Stage name (English) is required'
    if (!nameAr.trim()) e.nameAr = 'Stage name (Arabic) is required'
    setErrors(e)
    return Object.keys(e).length === 0
  }

  function handleSave() {
    if (!validate()) return
    onSave({
      nameEn: nameEn.trim(),
      nameAr: nameAr.trim(),
      customerGoalEn: goalEn.trim() || undefined,
      customerGoalAr: goalAr.trim() || undefined,
      expectedEmotion: emotion || undefined,
      sequenceFlag,
    })
  }

  return (
    <Sheet open={open} onOpenChange={v => !v && onClose()}>
      <SheetContent side="right" className="w-full sm:max-w-lg flex flex-col gap-0 p-0 pb-[50px]">
        <SheetHeader className="px-6 pt-6 pb-4 border-b border-border">
          <SheetTitle className="text-lg font-heading font-bold">Add New Stage</SheetTitle>
          <p className="text-sm text-muted-foreground">Define the stage details for this customer journey.</p>
        </SheetHeader>

        <div className="flex-1 overflow-y-auto px-6 py-5 space-y-6">

          {/* Names */}
          <div className="space-y-4">
            <h3 className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">Stage Name</h3>

            <div className="space-y-1.5">
              <Label htmlFor="stNameEn">
                Name (English) <span className="text-destructive">*</span>
              </Label>
              <Input
                id="stNameEn"
                value={nameEn}
                onChange={e => setNameEn(e.target.value)}
                placeholder="e.g. Application Submission"
                className={cn(errors.nameEn && 'border-destructive')}
              />
              {errors.nameEn && <p className="text-sm text-destructive" role="alert">{errors.nameEn}</p>}
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="stNameAr">
                Name (Arabic) <span className="text-destructive">*</span>
              </Label>
              <Input
                id="stNameAr"
                dir="rtl"
                value={nameAr}
                onChange={e => setNameAr(e.target.value)}
                placeholder="مثال: تقديم الطلب"
                className={cn(errors.nameAr && 'border-destructive')}
              />
              {errors.nameAr && <p className="text-sm text-destructive" role="alert">{errors.nameAr}</p>}
            </div>
          </div>

          <div className="border-t border-border" />

          {/* Customer Goal */}
          <div className="space-y-4">
            <h3 className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">Customer Goal</h3>

            <div className="space-y-1.5">
              <Label htmlFor="stGoalEn">Goal (English)</Label>
              <Textarea
                id="stGoalEn"
                value={goalEn}
                onChange={e => setGoalEn(e.target.value)}
                placeholder="What does the customer want to achieve at this stage?"
                rows={2}
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="stGoalAr">Goal (Arabic)</Label>
              <Textarea
                id="stGoalAr"
                dir="rtl"
                value={goalAr}
                onChange={e => setGoalAr(e.target.value)}
                placeholder="ماذا يريد العميل أن يحقق في هذه المرحلة؟"
                rows={2}
              />
            </div>
          </div>

          <div className="border-t border-border" />

          {/* Behaviour */}
          <div className="space-y-4">
            <h3 className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">Behaviour</h3>

            <div className="space-y-1.5">
              <Label>Expected Customer Emotion</Label>
              <Select value={emotion} onValueChange={v => setEmotion(v as EmotionType)}>
                <SelectTrigger>
                  <SelectValue placeholder="How will the customer feel?" />
                </SelectTrigger>
                <SelectContent>
                  {EMOTIONS.map(em => (
                    <SelectItem key={em} value={em}>
                      <span className="me-2">{emotionEmoji[em]}</span>{em}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-1.5">
              <Label>Sequence Type</Label>
              <Select value={sequenceFlag} onValueChange={v => setSequenceFlag(v as 'Sequential' | 'Parallel')}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Sequential">Sequential — touchpoints happen one after another</SelectItem>
                  <SelectItem value="Parallel">Parallel — touchpoints happen simultaneously</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>

        </div>

        <SheetFooter className="px-6 pt-3 pb-1 border-t border-border flex flex-col gap-2">
          <Button
            onClick={handleSave}
            className="w-full h-10 rounded-full text-sm font-semibold bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
          >
            Add Stage
          </Button>
          <Button
            variant="ghost"
            onClick={onClose}
            className="w-full h-10 rounded-full text-sm font-medium bg-muted hover:bg-muted/80 text-muted-foreground"
          >
            Cancel
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  )
}
