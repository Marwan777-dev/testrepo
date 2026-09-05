import { useState, useMemo, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Alert, AlertDescription } from '@/components/ui/alert'
import {
  ArrowLeft,
  Plus,
  Send,
  X,
  AlertTriangle,
  Info,
  Map,
  BarChart2,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { mockJourneys } from '@/data/mockJourneys'
import StageList from '@/components/builder/StageList'
import StageDrawer from '@/components/builder/StageDrawer'
import PublishModal from '@/components/builder/PublishModal'
import type { Journey, Stage, EmotionType, JourneyStatus, JourneyType } from '@/types/journey'

let stageIdCounter = 9000
let tpIdCounter = 9000

const statusConfig: Record<JourneyStatus, { label: string; className: string }> = {
  Active: { label: 'Active', className: 'bg-[#C8F5DB] text-[#156632] dark:bg-[#156632]/20 dark:text-[#C8F5DB]' },
  Draft: { label: 'Draft', className: 'bg-[#FFF0CC] text-[#7A5000] dark:bg-[#7A5000]/20 dark:text-[#FFF0CC]' },
  Archived: { label: 'Archived', className: 'bg-muted text-muted-foreground' },
}

const typeConfig: Record<JourneyType, string> = {
  Transactional: 'bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan/10 dark:text-nb-cyan-300',
  Lifecycle: 'bg-nb-mint-100 text-nb-mint-800 dark:bg-nb-mint/10 dark:text-nb-mint-300',
  'Issue-Resolution': 'bg-[#FFE4D0] text-[#7A2800] dark:bg-[#7A2800]/20 dark:text-[#FFE4D0]',
  Onboarding: 'bg-nb-navy-100 text-nb-navy dark:bg-nb-navy/20 dark:text-nb-navy-200',
}

function bumpVersion(version: string, hasMajor: boolean): string {
  if (!version) return '1.0'
  const [maj, min] = version.split('.').map(Number)
  if (hasMajor) return `${maj + 1}.0`
  return `${maj}.${(min ?? 0) + 1}`
}

type ChangeType = 'major' | 'minor'

interface ChangeEntry {
  type: ChangeType
  description: string
}

export default function JourneyBuilderPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()

  const [allJourneys, setAllJourneys] = useState<Journey[]>(mockJourneys)
  const journey = allJourneys.find(j => j.id === id)

  const [stages, setStages] = useState<Stage[]>(journey?.stages ?? [])
  const [isDirty, setIsDirty] = useState(false)
  const [changes, setChanges] = useState<ChangeEntry[]>([])

  const [stageDrawerOpen, setStageDrawerOpen] = useState(false)
  const [publishOpen, setPublishOpen] = useState(false)

  // Banners
  const [reorderBannerVisible, setReorderBannerVisible] = useState(false)
  const [stageLimitBanner, setStageLimitBanner] = useState(false)
  const [tpJourneyLimitBanner, setTpJourneyLimitBanner] = useState(false)

  const totalTouchpoints = useMemo(() => stages.reduce((s, st) => s + st.touchpoints.length, 0), [stages])
  const majorChanges = changes.filter(c => c.type === 'major').length
  const minorChanges = changes.filter(c => c.type === 'minor').length

  function markDirty(type: ChangeType = 'major', description = '') {
    setIsDirty(true)
    setChanges(prev => [...prev, { type, description }])
  }

  const handleReorder = useCallback((reordered: Stage[]) => {
    setStages(reordered)
    setReorderBannerVisible(true)
    markDirty('major', 'Stage reorder')
  }, [])

  const handleUpdateStage = useCallback((updated: Stage) => {
    setStages(prev => prev.map(s => s.id === updated.id ? updated : s))
    // Name/goal/emotion changes are minor; structure is handled by sub-components calling onDirty
  }, [])

  const handleDuplicateStage = useCallback((stage: Stage) => {
    if (stages.length >= 20) { setStageLimitBanner(true); return }
    const copy: Stage = {
      ...stage,
      id: `stage-dup-${stageIdCounter++}`,
      nameEn: `${stage.nameEn} (Copy)`,
      nameAr: `${stage.nameAr} (نسخة)`,
      sequenceOrder: stages.length + 1,
      isExpanded: false,
      touchpoints: stage.touchpoints.map(t => ({
        ...t,
        id: `tp-dup-${tpIdCounter++}`,
        kpiBindings: t.kpiBindings.map(k => ({ ...k, id: `kb-dup-${tpIdCounter++}` })),
      })),
    }
    setStages(prev => [...prev, copy])
    markDirty('major', 'Stage duplicated')
  }, [stages])

  const handleDeleteStage = useCallback((id: string) => {
    setStages(prev => prev.filter(s => s.id !== id).map((s, i) => ({ ...s, sequenceOrder: i + 1 })))
    markDirty('major', 'Stage deleted')
  }, [])

  function handleAddStage(data: {
    nameEn: string
    nameAr: string
    customerGoalEn?: string
    customerGoalAr?: string
    expectedEmotion?: EmotionType
    sequenceFlag: 'Sequential' | 'Parallel'
  }) {
    if (stages.length >= 20) { setStageLimitBanner(true); return }
    const newStage: Stage = {
      id: `stage-new-${stageIdCounter++}`,
      nameEn: data.nameEn,
      nameAr: data.nameAr,
      customerGoalEn: data.customerGoalEn,
      customerGoalAr: data.customerGoalAr,
      expectedEmotion: data.expectedEmotion,
      sequenceFlag: data.sequenceFlag,
      sequenceOrder: stages.length + 1,
      isExpanded: false,
      touchpoints: [],
    }
    setStages(prev => [...prev, newStage])
    setStageDrawerOpen(false)
    markDirty('major', 'Stage added')
  }

  function handlePublish() {
    if (!journey) return
    const nextVersion = bumpVersion(journey.version, majorChanges > 0)
    setAllJourneys(prev => prev.map(j => j.id === id ? {
      ...j,
      stages,
      version: nextVersion,
      status: j.status === 'Draft' ? 'Active' : j.status,
      updatedAt: new Date().toISOString(),
    } : j))
    setIsDirty(false)
    setChanges([])
    setPublishOpen(false)
  }

  if (!journey) {
    return (
      <div className="px-8 flex flex-col items-center justify-center py-24 text-center space-y-3">
        <Map className="size-12 text-muted-foreground" />
        <h2 className="text-xl font-heading font-bold">Journey not found</h2>
        <p className="text-muted-foreground text-sm">This journey may have been deleted or the ID is incorrect.</p>
        <Button onClick={() => navigate('/journeys')}>
          <ArrowLeft className="size-4 me-2" /> Back to Journeys
        </Button>
      </div>
    )
  }

  const currentJourney = allJourneys.find(j => j.id === id) ?? journey
  const statusStyle = statusConfig[currentJourney.status]

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

      {/* Journey Header */}
      <div className="rounded-lg border border-border bg-card p-4 shadow-sm">
        <div className="flex flex-col sm:flex-row sm:items-center gap-4">
          <div className="flex-1 min-w-0">
            <div className="flex flex-wrap items-center gap-2 mb-1">
              <h1 className="text-xl font-heading font-bold text-foreground truncate">
                {currentJourney.nameEn}
              </h1>
              <Badge className={cn('text-xs border-0', statusStyle.className)}>
                {statusStyle.label}
              </Badge>
              {currentJourney.version && (
                <Badge variant="outline" className="text-xs font-mono">v{currentJourney.version}</Badge>
              )}
              <Badge className={cn('text-xs border-0', typeConfig[currentJourney.journeyType])}>
                {currentJourney.journeyType}
              </Badge>
            </div>
            <div className="flex items-center gap-3 mt-2 text-xs text-muted-foreground">
              <span>{stages.length} stages</span>
              <span>{totalTouchpoints} touchpoints</span>
            </div>
          </div>
          <div className="flex items-center gap-2 shrink-0">
            <Button
              variant="secondary"
              onClick={() => navigate(`/journeys/${currentJourney.id}/stats`)}
              className="gap-1.5"
            >
              <BarChart2 className="size-4" /> Analytics
            </Button>
            <Button
              variant="secondary"
              onClick={() => setStageDrawerOpen(true)}
              className="gap-1.5"
            >
              <Plus className="size-4" /> Add Stage
            </Button>
            <Button
              disabled={!isDirty}
              onClick={() => setPublishOpen(true)}
              className={cn(
                'gap-1.5',
                isDirty
                  ? 'bg-primary hover:bg-nb-cyan-700 text-primary-foreground'
                  : 'bg-muted text-muted-foreground cursor-not-allowed'
              )}
            >
              <Send className="size-4" /> Publish
            </Button>
          </div>
        </div>
      </div>

      {/* Limit banners */}
      {stageLimitBanner && (
        <Alert className="border-[#E8A020]/30 bg-[#FFF0CC] dark:bg-[#7A5000]/20">
          <AlertTriangle className="size-4 text-[#E8A020]" />
          <AlertDescription className="text-[#7A5000] dark:text-[#FFF0CC] flex items-center justify-between">
            <span>You've reached the maximum of 20 stages. Consider splitting this into multiple journeys.</span>
            <button type="button" onClick={() => setStageLimitBanner(false)} aria-label="Dismiss" className="ms-3 shrink-0"><X className="size-4" /></button>
          </AlertDescription>
        </Alert>
      )}

      {tpJourneyLimitBanner && (
        <Alert className="border-[#E8A020]/30 bg-[#FFF0CC] dark:bg-[#7A5000]/20">
          <AlertTriangle className="size-4 text-[#E8A020]" />
          <AlertDescription className="text-[#7A5000] dark:text-[#FFF0CC] flex items-center justify-between">
            <span>You've reached the total touchpoint limit (300) for this journey.</span>
            <button type="button" onClick={() => setTpJourneyLimitBanner(false)} aria-label="Dismiss" className="ms-3 shrink-0"><X className="size-4" /></button>
          </AlertDescription>
        </Alert>
      )}

      {/* Reorder banner */}
      {reorderBannerVisible && (
        <Alert className="border-[#E8A020]/30 bg-[#FFF0CC] dark:bg-[#7A5000]/20">
          <Info className="size-4 text-[#E8A020]" />
          <AlertDescription className="text-[#7A5000] dark:text-[#FFF0CC] flex items-center justify-between">
            <span>Reordering stages will trigger a <strong>Major version bump</strong> on next publish.</span>
            <button type="button" onClick={() => setReorderBannerVisible(false)} aria-label="Dismiss" className="ms-3 shrink-0"><X className="size-4" /></button>
          </AlertDescription>
        </Alert>
      )}

      {/* Unsaved changes indicator */}
      {isDirty && (
        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          <span className="size-2 rounded-full bg-[#E8A020] inline-block" />
          Unsaved changes — {majorChanges > 0 && `${majorChanges} major`}{majorChanges > 0 && minorChanges > 0 && ', '}{minorChanges > 0 && `${minorChanges} minor`}
        </div>
      )}

      {/* Stage list */}
      {stages.length === 0 ? (
        <div className="rounded-lg border border-dashed border-border p-16 text-center space-y-3">
          <Map className="size-12 text-muted-foreground mx-auto" />
          <div>
            <h3 className="text-base font-semibold text-foreground">No stages yet</h3>
            <p className="text-sm text-muted-foreground mt-1">Add your first stage to start building this journey map.</p>
          </div>
          <Button
            onClick={() => setStageDrawerOpen(true)}
            className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground gap-1.5"
          >
            <Plus className="size-4" /> Add First Stage
          </Button>
        </div>
      ) : (
        <StageList
          stages={stages}
          onReorder={handleReorder}
          onUpdateStage={handleUpdateStage}
          onDuplicateStage={handleDuplicateStage}
          onDeleteStage={handleDeleteStage}
          onDirty={() => markDirty('major')}
        />
      )}

      {/* Stage Drawer */}
      <StageDrawer
        open={stageDrawerOpen}
        onClose={() => setStageDrawerOpen(false)}
        onSave={handleAddStage}
      />

      {/* Publish Modal */}
      <PublishModal
        open={publishOpen}
        currentVersion={currentJourney.version}
        changes={{ major: majorChanges, minor: minorChanges }}
        onPublish={handlePublish}
        onClose={() => setPublishOpen(false)}
      />
    </div>
  )
}
