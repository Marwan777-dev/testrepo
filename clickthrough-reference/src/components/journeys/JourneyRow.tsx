import { useNavigate } from 'react-router'
import { Badge } from '@/components/ui/badge'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Button } from '@/components/ui/button'
import { TableCell, TableRow } from '@/components/ui/table'
import { MoreHorizontal, Pencil, Copy, Download, Archive, BarChart2 } from 'lucide-react'
import type { Journey, JourneyStatus, JourneyType } from '@/types/journey'
import { cn } from '@/lib/utils'
import { format } from 'date-fns'

const statusConfig: Record<JourneyStatus, { label: string; className: string }> = {
  Active: { label: 'Active', className: 'bg-[#C8F5DB] text-[#156632] dark:bg-[#156632]/20 dark:text-[#C8F5DB]' },
  Draft: { label: 'Draft', className: 'bg-[#FFF0CC] text-[#7A5000] dark:bg-[#7A5000]/20 dark:text-[#FFF0CC]' },
  Archived: { label: 'Archived', className: 'bg-muted text-muted-foreground' },
}

const typeConfig: Record<JourneyType, { label: string; className: string }> = {
  Transactional: { label: 'Transactional', className: 'bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan/10 dark:text-nb-cyan-300' },
  Lifecycle: { label: 'Lifecycle', className: 'bg-nb-mint-100 text-nb-mint-800 dark:bg-nb-mint/10 dark:text-nb-mint-300' },
  'Issue-Resolution': { label: 'Issue-Resolution', className: 'bg-[#FFE4D0] text-[#7A2800] dark:bg-[#7A2800]/20 dark:text-[#FFE4D0]' },
  Onboarding: { label: 'Onboarding', className: 'bg-nb-navy-100 text-nb-navy dark:bg-nb-navy/20 dark:text-nb-navy-200' },
}

interface Props {
  journey: Journey
  onEdit: (j: Journey) => void
  onClone: (j: Journey) => void
  onArchive: (j: Journey) => void
  onExport: (j: Journey) => void
}

export default function JourneyRow({ journey, onEdit, onClone, onArchive, onExport }: Props) {
  const navigate = useNavigate()
  const totalTouchpoints = journey.stages.reduce((sum, s) => sum + s.touchpoints.length, 0)
  const status = statusConfig[journey.status]
  const type = typeConfig[journey.journeyType]

  return (
    <TableRow className="hover:bg-muted/50 transition-colors cursor-pointer group">
      <TableCell
        className="font-medium text-foreground hover:text-primary transition-colors"
        onClick={() => navigate(`/journeys/${journey.id}`)}
      >
        <div className="flex flex-col gap-0.5">
          <span className="text-sm font-semibold">{journey.nameEn}</span>
        </div>
      </TableCell>

      <TableCell>
        <Badge className={cn('text-xs font-medium border-0', type.className)}>
          {type.label}
        </Badge>
      </TableCell>

      <TableCell>
        <Badge className={cn('text-xs font-medium border-0', status.className)}>
          {status.label}
        </Badge>
      </TableCell>

      <TableCell className="text-sm tabular-nums text-muted-foreground">
        {journey.version ? `v${journey.version}` : '—'}
      </TableCell>

      <TableCell className="text-sm tabular-nums text-center">{journey.stages.length}</TableCell>
      <TableCell className="text-sm tabular-nums text-center">{totalTouchpoints}</TableCell>

      <TableCell className="text-sm text-muted-foreground tabular-nums">
        {format(new Date(journey.updatedAt), 'MMM d, yyyy')}
      </TableCell>

      <TableCell onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-end gap-1 pe-1">
          <Button
            variant="ghost"
            size="icon"
            aria-label="View statistics"
            onClick={() => navigate(`/journeys/${journey.id}/stats`)}
            className="size-8 text-muted-foreground hover:text-primary"
          >
            <BarChart2 className="size-4" />
          </Button>
        <DropdownMenu>
          <DropdownMenuTrigger
            aria-label="Journey actions"
            className="inline-flex items-center justify-center rounded-md size-8 hover:bg-accent text-muted-foreground hover:text-foreground"
          >
            <MoreHorizontal className="size-4" />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onClick={() => onEdit(journey)}>
              <Pencil className="size-4 me-2" /> Edit
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => onClone(journey)}>
              <Copy className="size-4 me-2" /> Clone
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => onExport(journey)}>
              <Download className="size-4 me-2" /> Export JSON
            </DropdownMenuItem>
            {journey.status !== 'Archived' && (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem
                  onClick={() => onArchive(journey)}
                  className="text-destructive focus:text-destructive"
                >
                  <Archive className="size-4 me-2" /> Archive
                </DropdownMenuItem>
              </>
            )}
          </DropdownMenuContent>
        </DropdownMenu>
        </div>
      </TableCell>
    </TableRow>
  )
}
