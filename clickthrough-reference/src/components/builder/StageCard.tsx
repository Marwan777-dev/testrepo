import { useState, useRef } from 'react'
import { useSortable } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetFooter } from '@/components/ui/sheet'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  GripVertical,
  ChevronDown,
  ChevronRight,
  Plus,
  MoreHorizontal,
  Pencil,
  Copy,
  Trash2,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import TouchpointRow from './TouchpointRow'
import TouchpointDrawer from './TouchpointDrawer'
import type { Stage, Touchpoint, EmotionType } from '@/types/journey'

const EMOTIONS: EmotionType[] = ['Excited', 'Neutral', 'Anxious', 'Frustrated', 'Confident', 'Confused', 'Relieved']

const emotionColors: Record<EmotionType, string> = {
  Excited: 'bg-[#C8F5DB] text-[#156632] dark:bg-[#156632]/20 dark:text-[#C8F5DB]',
  Neutral: 'bg-muted text-muted-foreground',
  Anxious: 'bg-[#FFE4D0] text-[#7A2800] dark:bg-[#7A2800]/20 dark:text-[#FFE4D0]',
  Frustrated: 'bg-[#FFD6DA] text-[#6B0010] dark:bg-[#6B0010]/20 dark:text-[#FFD6DA]',
  Confident: 'bg-[#CCF0FB] text-nb-cyan-800 dark:bg-nb-cyan/10 dark:text-nb-cyan-300',
  Confused: 'bg-[#FFF0CC] text-[#7A5000] dark:bg-[#7A5000]/20 dark:text-[#FFF0CC]',
  Relieved: 'bg-nb-mint-100 text-nb-mint-800 dark:bg-nb-mint/10 dark:text-nb-mint-300',
}

let tpIdCounter = 5000

interface Props {
  stage: Stage
  stageIndex: number
  onUpdate: (updated: Stage) => void
  onDuplicate: (stage: Stage) => void
  onDelete: (id: string) => void
  onDirty: () => void
}

export default function StageCard({ stage, stageIndex, onUpdate, onDuplicate, onDelete, onDirty }: Props) {
  const [isExpanded, setIsExpanded] = useState(stage.isExpanded)
  const [editingName, setEditingName] = useState(false)
  const [nameValue, setNameValue] = useState(stage.nameEn)
  const [metaOpen, setMetaOpen] = useState(false)
  const [tpDrawerOpen, setTpDrawerOpen] = useState(false)
  const [activeTp, setActiveTp] = useState<Touchpoint | null>(null)
  const nameRef = useRef<HTMLInputElement>(null)

  // Stage metadata edit state
  const [metaNameEn, setMetaNameEn] = useState(stage.nameEn)
  const [metaNameAr, setMetaNameAr] = useState(stage.nameAr)
  const [metaGoalEn, setMetaGoalEn] = useState(stage.customerGoalEn ?? '')
  const [metaGoalAr, setMetaGoalAr] = useState(stage.customerGoalAr ?? '')
  const [metaEmotion, setMetaEmotion] = useState<EmotionType | ''>(stage.expectedEmotion ?? '')
  const [metaSeq, setMetaSeq] = useState<'Sequential' | 'Parallel'>(stage.sequenceFlag)

  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: stage.id })

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.5 : 1,
  }

  function commitName() {
    setEditingName(false)
    if (nameValue.trim() && nameValue !== stage.nameEn) {
      onUpdate({ ...stage, nameEn: nameValue.trim() })
      onDirty()
    }
  }

  function openMetaDrawer() {
    setMetaNameEn(stage.nameEn)
    setMetaNameAr(stage.nameAr)
    setMetaGoalEn(stage.customerGoalEn ?? '')
    setMetaGoalAr(stage.customerGoalAr ?? '')
    setMetaEmotion(stage.expectedEmotion ?? '')
    setMetaSeq(stage.sequenceFlag)
    setMetaOpen(true)
  }

  function saveMeta() {
    onUpdate({
      ...stage,
      nameEn: metaNameEn.trim() || stage.nameEn,
      nameAr: metaNameAr.trim() || stage.nameAr,
      customerGoalEn: metaGoalEn.trim() || undefined,
      customerGoalAr: metaGoalAr.trim() || undefined,
      expectedEmotion: metaEmotion || undefined,
      sequenceFlag: metaSeq,
    })
    setMetaOpen(false)
    onDirty()
  }

  function addTouchpoint() {
    if (stage.touchpoints.length >= 30) return
    const newTp: Touchpoint = {
      id: `tp-new-${tpIdCounter++}`,
      nameEn: 'New Touchpoint',
      nameAr: 'نقطة تماس جديدة',
      channels: [],
      importanceCustomer: 3,
      importanceBusiness: 3,
      isMoT: false,
      isMandatory: false,
      kpiBindings: [],
      sequenceOrder: stage.touchpoints.length + 1,
    }
    setActiveTp(newTp)
    setTpDrawerOpen(true)
    if (!isExpanded) setIsExpanded(true)
  }

  function editTouchpoint(tp: Touchpoint) {
    setActiveTp(tp)
    setTpDrawerOpen(true)
  }

  function saveTouchpoint(updated: Touchpoint) {
    const exists = stage.touchpoints.find(t => t.id === updated.id)
    const newTouchpoints = exists
      ? stage.touchpoints.map(t => t.id === updated.id ? updated : t)
      : [...stage.touchpoints, { ...updated, sequenceOrder: stage.touchpoints.length + 1 }]
    onUpdate({ ...stage, touchpoints: newTouchpoints })
    setTpDrawerOpen(false)
    setActiveTp(null)
    onDirty()
  }

  function duplicateTouchpoint(tp: Touchpoint) {
    const copy: Touchpoint = {
      ...tp,
      id: `tp-dup-${tpIdCounter++}`,
      nameEn: `${tp.nameEn} (Copy)`,
      sequenceOrder: stage.touchpoints.length + 1,
      kpiBindings: tp.kpiBindings.map(b => ({ ...b, id: `kb-${tpIdCounter++}` })),
    }
    onUpdate({ ...stage, touchpoints: [...stage.touchpoints, copy] })
    onDirty()
  }

  function deleteTouchpoint(id: string) {
    onUpdate({
      ...stage,
      touchpoints: stage.touchpoints
        .filter(t => t.id !== id)
        .map((t, i) => ({ ...t, sequenceOrder: i + 1 })),
    })
    onDirty()
  }

  const totalTouchpoints = stage.touchpoints.length

  return (
    <div ref={setNodeRef} style={style} className={cn('rounded-lg border border-border bg-card shadow-sm', isDragging && 'shadow-lg')}>
      {/* Stage Header */}
      <div className="flex items-center gap-2 px-3 py-2.5">
        {/* Drag handle */}
        <button
          {...attributes}
          {...listeners}
          aria-label="Drag to reorder stage"
          className="cursor-grab active:cursor-grabbing text-muted-foreground hover:text-foreground p-0.5 shrink-0"
        >
          <GripVertical className="size-4" />
        </button>

        {/* Index badge */}
        <span className="text-xs font-mono tabular-nums text-muted-foreground bg-muted rounded px-1.5 py-0.5 shrink-0">
          {stageIndex}.0
        </span>

        {/* Stage name — double click to rename */}
        {editingName ? (
          <input
            ref={nameRef}
            value={nameValue}
            onChange={e => setNameValue(e.target.value)}
            onBlur={commitName}
            onKeyDown={e => { if (e.key === 'Enter') commitName(); if (e.key === 'Escape') { setEditingName(false); setNameValue(stage.nameEn) } }}
            autoFocus
            className="flex-1 bg-transparent border-b border-primary text-sm font-semibold outline-none"
          />
        ) : (
          <button
            type="button"
            onDoubleClick={() => { setEditingName(true); setNameValue(stage.nameEn) }}
            title="Double-click to rename"
            className="flex-1 text-start text-sm font-semibold text-foreground truncate hover:text-primary transition-colors"
          >
            {stage.nameEn}
          </button>
        )}

        {/* Emotion badge */}
        {stage.expectedEmotion && (
          <Badge className={cn('text-[10px] shrink-0 border-0', emotionColors[stage.expectedEmotion])}>
            {stage.expectedEmotion}
          </Badge>
        )}

        {/* Touchpoint count */}
        <span className="text-xs text-muted-foreground tabular-nums shrink-0">{totalTouchpoints} TP</span>

        {/* Add touchpoint */}
        <Button
          variant="secondary"
          size="compact"
          onClick={addTouchpoint}
          className="text-xs shrink-0 hidden sm:flex"
        >
          <Plus className="size-3" /> Add Touchpoint
        </Button>

        {/* Kebab */}
        <DropdownMenu>
          <DropdownMenuTrigger
            aria-label="Stage actions"
            className="inline-flex items-center justify-center rounded-md size-7 shrink-0 hover:bg-accent text-muted-foreground hover:text-foreground transition-colors"
          >
            <MoreHorizontal className="size-4" />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onClick={openMetaDrawer}>
              <Pencil className="size-4 me-2" /> Edit Metadata
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => onDuplicate(stage)}>
              <Copy className="size-4 me-2" /> Duplicate Stage
            </DropdownMenuItem>
            <DropdownMenuItem onClick={addTouchpoint} className="sm:hidden">
              <Plus className="size-4 me-2" /> Add Touchpoint
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={() => onDelete(stage.id)} className="text-destructive focus:text-destructive">
              <Trash2 className="size-4 me-2" /> Delete Stage
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>

        {/* Expand/collapse */}
        <Button
          variant="ghost"
          size="icon"
          aria-label={isExpanded ? 'Collapse stage' : 'Expand stage'}
          onClick={() => setIsExpanded(v => !v)}
          className="size-7 shrink-0"
        >
          {isExpanded ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}
        </Button>
      </div>

      {/* Touchpoints — animated expand */}
      <div className={cn(
        'grid transition-[grid-template-rows,opacity] duration-300 ease-[cubic-bezier(0.16,1,0.3,1)]',
        isExpanded ? 'grid-rows-[1fr] opacity-100' : 'grid-rows-[0fr] opacity-0'
      )}>
        <div className="overflow-hidden">
          {stage.touchpoints.length > 0 ? (
            <div className="border-t border-border/50 py-1">
              {stage.touchpoints.map((tp, i) => (
                <TouchpointRow
                  key={tp.id}
                  touchpoint={tp}
                  stageIndex={stageIndex}
                  tpIndex={i + 1}
                  onEdit={editTouchpoint}
                  onDuplicate={duplicateTouchpoint}
                  onDelete={deleteTouchpoint}
                />
              ))}
            </div>
          ) : (
            <div className="border-t border-border/50 px-4 py-4 text-center text-sm text-muted-foreground">
              No touchpoints yet.{' '}
              <button
                type="button"
                onClick={addTouchpoint}
                className="text-primary underline hover:no-underline"
              >
                Add the first one
              </button>
            </div>
          )}
        </div>
      </div>

      {/* Stage Metadata Drawer */}
      <Sheet open={metaOpen} onOpenChange={v => !v && setMetaOpen(false)}>
        <SheetContent side="right" className="w-full sm:max-w-md flex flex-col gap-0 p-0 pb-[50px]">
          <SheetHeader className="px-6 pt-6 pb-4 border-b border-border">
            <SheetTitle className="font-heading font-bold">Edit Stage Metadata</SheetTitle>
          </SheetHeader>
          <div className="flex-1 overflow-y-auto px-6 py-5 space-y-4">
            <div className="space-y-1.5">
              <Label>Name (English) <span className="text-destructive">*</span></Label>
              <Input value={metaNameEn} onChange={e => setMetaNameEn(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label>Name (Arabic) <span className="text-destructive">*</span></Label>
              <Input dir="rtl" value={metaNameAr} onChange={e => setMetaNameAr(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label>Customer Goal (English)</Label>
              <Textarea value={metaGoalEn} onChange={e => setMetaGoalEn(e.target.value)} rows={2} placeholder="What does the customer want to achieve?" />
            </div>
            <div className="space-y-1.5">
              <Label>Customer Goal (Arabic)</Label>
              <Textarea dir="rtl" value={metaGoalAr} onChange={e => setMetaGoalAr(e.target.value)} rows={2} placeholder="ماذا يريد العميل أن يحقق؟" />
            </div>
            <div className="space-y-1.5">
              <Label>Expected Emotion</Label>
              <Select value={metaEmotion} onValueChange={v => setMetaEmotion(v as EmotionType)}>
                <SelectTrigger>
                  <SelectValue placeholder="Select emotion..." />
                </SelectTrigger>
                <SelectContent>
                  {EMOTIONS.map(e => <SelectItem key={e} value={e}>{e}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label>Sequence</Label>
              <Select value={metaSeq} onValueChange={v => setMetaSeq(v as 'Sequential' | 'Parallel')}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Sequential">Sequential</SelectItem>
                  <SelectItem value="Parallel">Parallel</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
          <SheetFooter className="px-6 pt-3 pb-1 border-t border-border flex flex-col gap-2">
            <Button onClick={saveMeta} className="w-full h-10 rounded-md text-sm font-semibold bg-primary hover:bg-nb-cyan-700 text-primary-foreground">Save</Button>
            <Button variant="ghost" onClick={() => setMetaOpen(false)} className="w-full h-10 rounded-md text-sm font-medium bg-muted hover:bg-muted/80 text-muted-foreground">Cancel</Button>
          </SheetFooter>
        </SheetContent>
      </Sheet>

      {/* Touchpoint Config Drawer */}
      <TouchpointDrawer
        open={tpDrawerOpen}
        touchpoint={activeTp}
        onClose={() => { setTpDrawerOpen(false); setActiveTp(null) }}
        onSave={saveTouchpoint}
      />
    </div>
  )
}
