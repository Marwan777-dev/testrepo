import { useState } from 'react'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Button } from '@/components/ui/button'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { cn } from '@/lib/utils'
import type { EmotionType } from '@/types/journey'

const EMOTIONS: EmotionType[] = ['Excited', 'Neutral', 'Anxious', 'Frustrated', 'Confident', 'Confused', 'Relieved']

interface Props {
  onAdd: (data: { nameEn: string; nameAr: string; customerGoalEn?: string; expectedEmotion?: EmotionType }) => void
  onCancel: () => void
}

export default function StageAddForm({ onAdd, onCancel }: Props) {
  const [nameEn, setNameEn] = useState('')
  const [nameAr, setNameAr] = useState('')
  const [goalEn, setGoalEn] = useState('')
  const [emotion, setEmotion] = useState<EmotionType | ''>('')
  const [errors, setErrors] = useState<Record<string, string>>({})

  function handleSubmit() {
    const e: Record<string, string> = {}
    if (!nameEn.trim()) e.nameEn = 'Stage name (EN) is required'
    if (!nameAr.trim()) e.nameAr = 'Stage name (AR) is required'
    setErrors(e)
    if (Object.keys(e).length) return
    onAdd({
      nameEn: nameEn.trim(),
      nameAr: nameAr.trim(),
      customerGoalEn: goalEn.trim() || undefined,
      expectedEmotion: emotion || undefined,
    })
  }

  return (
    <div className="rounded-lg border border-primary/30 bg-accent/50 p-4 space-y-4 mt-3">
      <h4 className="text-sm font-semibold text-foreground">Add New Stage</h4>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div className="space-y-1.5">
          <Label htmlFor="sName">Name (English) <span className="text-destructive">*</span></Label>
          <Input
            id="sName"
            value={nameEn}
            onChange={e => setNameEn(e.target.value)}
            placeholder="e.g. Application Submission"
            className={cn('h-8 text-sm', errors.nameEn && 'border-destructive')}
          />
          {errors.nameEn && <p className="text-xs text-destructive" role="alert">{errors.nameEn}</p>}
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="sNameAr">Name (Arabic) <span className="text-destructive">*</span></Label>
          <Input
            id="sNameAr"
            dir="rtl"
            value={nameAr}
            onChange={e => setNameAr(e.target.value)}
            placeholder="مثال: تقديم الطلب"
            className={cn('h-8 text-sm', errors.nameAr && 'border-destructive')}
          />
          {errors.nameAr && <p className="text-xs text-destructive" role="alert">{errors.nameAr}</p>}
        </div>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div className="space-y-1.5">
          <Label htmlFor="sGoal">Customer Goal (optional)</Label>
          <Input
            id="sGoal"
            value={goalEn}
            onChange={e => setGoalEn(e.target.value)}
            placeholder="What does the customer want?"
            className="h-8 text-sm"
          />
        </div>
        <div className="space-y-1.5">
          <Label>Expected Emotion (optional)</Label>
          <Select value={emotion} onValueChange={v => setEmotion(v as EmotionType)}>
            <SelectTrigger className="h-8 text-sm">
              <SelectValue placeholder="Select emotion..." />
            </SelectTrigger>
            <SelectContent>
              {EMOTIONS.map(e => (
                <SelectItem key={e} value={e}>{e}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      <div className="flex justify-end gap-2 pt-1">
        <Button variant="outline" size="sm" onClick={onCancel}>Cancel</Button>
        <Button size="sm" onClick={handleSubmit} className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground">
          Add Stage
        </Button>
      </div>
    </div>
  )
}
