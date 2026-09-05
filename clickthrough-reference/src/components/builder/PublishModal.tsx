import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { ArrowUpCircle, GitMerge } from 'lucide-react'

interface ChangeSet {
  major: number
  minor: number
}

interface Props {
  open: boolean
  currentVersion: string
  changes: ChangeSet
  onPublish: () => void
  onClose: () => void
}

function bumpVersion(version: string, changes: ChangeSet): string {
  if (!version) return '1.0'
  const [maj, min] = version.split('.').map(Number)
  if (changes.major > 0) return `${maj + 1}.0`
  if (changes.minor > 0) return `${maj}.${(min ?? 0) + 1}`
  return version
}

export default function PublishModal({ open, currentVersion, changes, onPublish, onClose }: Props) {
  const nextVersion = bumpVersion(currentVersion, changes)
  const hasMajor = changes.major > 0
  const hasMinor = changes.minor > 0

  return (
    <Dialog open={open} onOpenChange={v => !v && onClose()}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2 font-heading">
            <ArrowUpCircle className="size-5 text-primary" />
            Publish Journey
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-1">
          {/* Version bump */}
          <div className="rounded-lg bg-muted/50 p-4 space-y-2">
            <p className="text-sm text-muted-foreground">Version will be updated:</p>
            <div className="flex items-center gap-3">
              <span className="text-lg font-mono font-bold text-muted-foreground line-through">
                v{currentVersion || '—'}
              </span>
              <GitMerge className="size-4 text-muted-foreground" />
              <span className="text-2xl font-mono font-bold text-primary">v{nextVersion}</span>
            </div>
          </div>

          {/* Change summary */}
          <div className="space-y-2">
            <p className="text-sm font-medium text-foreground">Change summary</p>
            <div className="flex flex-wrap gap-2">
              {hasMajor && (
                <Badge className="bg-[#FFE4D0] text-[#7A2800] dark:bg-[#7A2800]/20 dark:text-[#FFE4D0] border-0">
                  {changes.major} structural change{changes.major > 1 ? 's' : ''} · Major
                </Badge>
              )}
              {hasMinor && (
                <Badge className="bg-[#FFF0CC] text-[#7A5000] dark:bg-[#7A5000]/20 dark:text-[#FFF0CC] border-0">
                  {changes.minor} cosmetic change{changes.minor > 1 ? 's' : ''} · Minor
                </Badge>
              )}
              {!hasMajor && !hasMinor && (
                <Badge variant="secondary">No tracked changes</Badge>
              )}
            </div>
          </div>

          {/* What counts as what */}
          <div className="rounded-md border border-border p-3 space-y-1.5 text-xs text-muted-foreground">
            <p className="font-medium text-foreground text-xs">Change types</p>
            <p><span className="font-medium text-[#E05C1A]">Major:</span> Stage/touchpoint additions, removals, reordering, KPI changes</p>
            <p><span className="font-medium text-[#E8A020]">Minor:</span> Renames, descriptions, goals, emotions, importance scores, MoT toggle</p>
          </div>
        </div>

        <DialogFooter className="gap-2">
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button onClick={onPublish} className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground">
            Publish v{nextVersion}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
