import { useState, useMemo } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import {
  Table,
  TableBody,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog'
import { Plus, Search, Map, AlertTriangle } from 'lucide-react'
import { mockJourneys } from '@/data/mockJourneys'
import JourneyRow from '@/components/journeys/JourneyRow'
import JourneyFormDrawer from '@/components/journeys/JourneyFormDrawer'
import type { Journey, JourneyStatus, JourneyType } from '@/types/journey'

let idCounter = 100

function generateId() {
  return `j-${idCounter++}`
}

export default function JourneysPage() {
  const [journeys, setJourneys] = useState<Journey[]>(mockJourneys)
  const [search, setSearch] = useState('')
  const [filterStatus, setFilterStatus] = useState<JourneyStatus | 'All'>('All')
  const [filterType, setFilterType] = useState<JourneyType | 'All'>('All')
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [editingJourney, setEditingJourney] = useState<Journey | null>(null)

  const [archiveTarget, setArchiveTarget] = useState<Journey | null>(null)

  const filtered = useMemo(() => {
    return journeys.filter(j => {
      if (search && !j.nameEn.toLowerCase().includes(search.toLowerCase()) && !j.nameAr.includes(search)) return false
      if (filterStatus !== 'All' && j.status !== filterStatus) return false
      if (filterType !== 'All' && j.journeyType !== filterType) return false
      return true
    })
  }, [journeys, search, filterStatus, filterType])

  function openCreate() {
    setEditingJourney(null)
    setDrawerOpen(true)
  }

  function openEdit(j: Journey) {
    setEditingJourney(j)
    setDrawerOpen(true)
  }

  function handleSave(data: Partial<Journey>) {
    if (editingJourney) {
      setJourneys(prev => prev.map(j => j.id === editingJourney.id ? { ...j, ...data, updatedAt: new Date().toISOString() } : j))
    } else {
      const newJourney: Journey = {
        id: generateId(),
        nameEn: data.nameEn!,
        nameAr: data.nameAr!,
        descriptionEn: data.descriptionEn,
        journeyType: data.journeyType!,
        status: 'Draft',
        version: '',
        updatedAt: new Date().toISOString(),
        stages: [],
        personas: data.personas ?? [],
        expectedDurationDays: data.expectedDurationDays,
      }
      setJourneys(prev => [newJourney, ...prev])
    }
    setDrawerOpen(false)
    setEditingJourney(null)
  }

  function handleClone(j: Journey) {
    const cloned: Journey = {
      ...j,
      id: generateId(),
      nameEn: `${j.nameEn} (Copy)`,
      nameAr: `${j.nameAr} (نسخة)`,
      status: 'Draft',
      version: '',
      updatedAt: new Date().toISOString(),
      stages: j.stages.map(s => ({
        ...s,
        id: `s-clone-${idCounter++}`,
        touchpoints: s.touchpoints.map(t => ({
          ...t,
          id: `tp-clone-${idCounter++}`,
          kpiBindings: t.kpiBindings.map(k => ({ ...k, id: `kb-clone-${idCounter++}` })),
        })),
      })),
    }
    setJourneys(prev => [cloned, ...prev])
  }

  function confirmArchive(j: Journey) {
    setArchiveTarget(j)
  }

  function handleArchive() {
    if (!archiveTarget) return
    setJourneys(prev => prev.map(j => j.id === archiveTarget.id ? { ...j, status: 'Archived', updatedAt: new Date().toISOString() } : j))
    setArchiveTarget(null)
  }

  function handleExport(j: Journey) {
    const blob = new Blob([JSON.stringify(j, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `${j.nameEn.replace(/\s+/g, '-').toLowerCase()}.json`
    a.click()
    URL.revokeObjectURL(url)
  }

  const statusCounts = useMemo(() => ({
    total: journeys.length,
    active: journeys.filter(j => j.status === 'Active').length,
    draft: journeys.filter(j => j.status === 'Draft').length,
    archived: journeys.filter(j => j.status === 'Archived').length,
  }), [journeys])

  return (
    <div className="px-8 space-y-5 py-5">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-heading font-bold text-foreground flex items-center gap-2">
            <Map className="size-6 text-primary" />
            Customer Journeys
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Design and manage end-to-end customer journey maps
          </p>
        </div>
        <Button onClick={openCreate} className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground gap-2">
          <Plus className="size-4" />
          New Journey
        </Button>
      </div>

      {/* Stat chips */}
      <div className="flex flex-wrap gap-3">
        {[
          { label: 'Total', value: statusCounts.total, className: 'bg-muted text-foreground' },
          { label: 'Active', value: statusCounts.active, className: 'bg-[#C8F5DB] text-[#156632] dark:bg-[#156632]/20 dark:text-[#C8F5DB]' },
          { label: 'Draft', value: statusCounts.draft, className: 'bg-[#FFF0CC] text-[#7A5000] dark:bg-[#7A5000]/20 dark:text-[#FFF0CC]' },
          { label: 'Archived', value: statusCounts.archived, className: 'bg-muted text-muted-foreground' },
        ].map(c => (
          <Badge key={c.label} className={`text-sm px-3 py-1.5 font-medium border-0 ${c.className}`}>
            {c.value} {c.label}
          </Badge>
        ))}
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-3 items-end">
        <div className="flex flex-col gap-1 flex-1 min-w-[200px] max-w-sm">
          <span className="text-xs font-medium text-muted-foreground uppercase tracking-wide">Search</span>
          <div className="relative">
            <Search className="size-4 absolute start-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={search}
              onChange={e => setSearch(e.target.value)}
              placeholder="Search journeys..."
              className="ps-9"
            />
          </div>
        </div>

        <div className="flex flex-col gap-1">
          <span className="text-xs font-medium text-muted-foreground uppercase tracking-wide">Status</span>
          <Select value={filterStatus} onValueChange={v => setFilterStatus(v as JourneyStatus | 'All')}>
            <SelectTrigger className="w-36">
              <SelectValue placeholder="Status" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="All">All Statuses</SelectItem>
              <SelectItem value="Active">Active</SelectItem>
              <SelectItem value="Draft">Draft</SelectItem>
              <SelectItem value="Archived">Archived</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="flex flex-col gap-1">
          <span className="text-xs font-medium text-muted-foreground uppercase tracking-wide">Type</span>
          <Select value={filterType} onValueChange={v => setFilterType(v as JourneyType | 'All')}>
            <SelectTrigger className="w-44">
              <SelectValue placeholder="Type" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="All">All Types</SelectItem>
              <SelectItem value="Transactional">Transactional</SelectItem>
              <SelectItem value="Lifecycle">Lifecycle</SelectItem>
              <SelectItem value="Issue-Resolution">Issue-Resolution</SelectItem>
              <SelectItem value="Onboarding">Onboarding</SelectItem>
            </SelectContent>
          </Select>
        </div>

      </div>

      {/* Table */}
      <div className="rounded-lg border border-border bg-card shadow-sm overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow className="bg-muted/30 hover:bg-muted/30">
              <TableHead className="font-semibold">Journey Name</TableHead>
              <TableHead className="font-semibold">Type</TableHead>
              <TableHead className="font-semibold">Status</TableHead>
              <TableHead className="font-semibold">Version</TableHead>
              <TableHead className="font-semibold text-center">Stages</TableHead>
              <TableHead className="font-semibold text-center">Touchpoints</TableHead>
              <TableHead className="font-semibold">Last Updated</TableHead>
              <TableHead className="font-semibold text-end pe-4">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {filtered.length === 0 ? (
              <TableRow>
                <td colSpan={8} className="py-16 text-center">
                  <div className="flex flex-col items-center gap-2 text-muted-foreground">
                    <Map className="size-10 mb-1" />
                    <p className="text-base font-medium text-foreground">No journeys found</p>
                    <p className="text-sm">
                      {search || filterStatus !== 'All' || filterType !== 'All'
                        ? 'Try adjusting your filters'
                        : 'Create your first customer journey to get started'}
                    </p>
                    {!search && filterStatus === 'All' && filterType === 'All' && (
                      <Button onClick={openCreate} className="mt-2 bg-primary hover:bg-nb-cyan-700 text-primary-foreground gap-1.5">
                        <Plus className="size-4" /> New Journey
                      </Button>
                    )}
                  </div>
                </td>
              </TableRow>
            ) : (
              filtered.map(j => (
                <JourneyRow
                  key={j.id}
                  journey={j}
                  onEdit={openEdit}
                  onClone={handleClone}
                  onArchive={confirmArchive}
                  onExport={handleExport}
                />
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {/* Create/Edit Drawer */}
      <JourneyFormDrawer
        open={drawerOpen}
        journey={editingJourney}
        onClose={() => { setDrawerOpen(false); setEditingJourney(null) }}
        onSave={handleSave}
      />

      {/* Archive Confirmation */}
      <Dialog open={!!archiveTarget} onOpenChange={v => !v && setArchiveTarget(null)}>
        <DialogContent className="max-w-sm">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <AlertTriangle className="size-5 text-[#E8A020]" />
              Archive Journey
            </DialogTitle>
          </DialogHeader>
          <p className="text-sm text-muted-foreground py-2">
            Archive <span className="font-semibold text-foreground">"{archiveTarget?.nameEn}"</span>?
            It will no longer appear in active views, but the data will be preserved.
          </p>
          <DialogFooter className="gap-2">
            <Button variant="outline" onClick={() => setArchiveTarget(null)}>Cancel</Button>
            <Button variant="destructive" onClick={handleArchive}>Archive</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
