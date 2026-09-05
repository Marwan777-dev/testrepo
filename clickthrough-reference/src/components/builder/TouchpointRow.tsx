import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Pencil, MoreHorizontal, Copy, Trash2, AlertTriangle, Star } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { Touchpoint } from '@/types/journey'

const CHANNEL_SHORT: Record<string, string> = {
  'Web': 'Web',
  'Mobile App': 'Mobile',
  'Email': 'Email',
  'SMS': 'SMS',
  'WhatsApp': 'WA',
  'Phone (Inbound)': 'Phone↓',
  'Phone (Outbound)': 'Phone↑',
  'Branch/In-Person': 'Branch',
  'Chat': 'Chat',
  'IVR': 'IVR',
  'Social Media': 'Social',
  'Kiosk': 'Kiosk',
  'Other': 'Other',
}

interface Props {
  touchpoint: Touchpoint
  stageIndex: number
  tpIndex: number
  onEdit: (tp: Touchpoint) => void
  onDuplicate: (tp: Touchpoint) => void
  onDelete: (id: string) => void
}

export default function TouchpointRow({ touchpoint, stageIndex, tpIndex, onEdit, onDuplicate, onDelete }: Props) {
  const index = `${stageIndex}.${tpIndex}`
  const hasKpis = touchpoint.kpiBindings.length > 0
  const weightSum = touchpoint.kpiBindings.reduce((s, b) => s + b.weightPct, 0)
  const kpiError = hasKpis && weightSum !== 100

  return (
    <div className="flex items-center gap-3 px-4 py-2.5 rounded-md hover:bg-muted/40 transition-colors group/tp">
      {/* Index */}
      <span className="text-xs font-mono tabular-nums text-muted-foreground w-8 shrink-0">{index}</span>

      {/* Name */}
      <button
        type="button"
        onClick={() => onEdit(touchpoint)}
        className="flex-1 text-start text-sm font-medium text-foreground hover:text-primary transition-colors truncate"
      >
        {touchpoint.nameEn}
      </button>

      {/* Channel chips */}
      <div className="hidden sm:flex items-center gap-1 shrink-0">
        {touchpoint.channels.slice(0, 3).map(c => (
          <Badge key={c} variant="outline" className="text-[10px] py-0 px-1.5 h-5 font-normal">
            {CHANNEL_SHORT[c] ?? c}
          </Badge>
        ))}
        {touchpoint.channels.length > 3 && (
          <Badge variant="outline" className="text-[10px] py-0 px-1.5 h-5 font-normal">
            +{touchpoint.channels.length - 3}
          </Badge>
        )}
      </div>

      {/* Importance */}
      <div className="hidden md:flex items-center gap-0.5 shrink-0" title={`Customer importance: ${touchpoint.importanceCustomer}`}>
        {[1,2,3,4,5].map(n => (
          <Star key={n} className={cn('size-2.5', n <= touchpoint.importanceCustomer ? 'fill-nb-cyan text-nb-cyan' : 'fill-muted text-muted')} />
        ))}
      </div>

      {/* MoT indicator */}
      {touchpoint.isMoT && (
        <span className="inline-flex items-center gap-1 shrink-0 text-[#7A5000] dark:text-[#FFF0CC] bg-[#FFF0CC] dark:bg-[#7A5000]/20 px-1.5 py-0.5 rounded text-[10px] font-medium">
          <AlertTriangle className="size-3 text-[#E8A020]" />
          MoT
        </span>
      )}

      {/* KPI indicator */}
      <div className="shrink-0">
        {kpiError ? (
          <span className="flex items-center gap-1 text-xs text-[#E05C1A]">
            <AlertTriangle className="size-3" /> KPI weights
          </span>
        ) : hasKpis ? (
          <Badge variant="secondary" className="text-[10px] py-0 h-5">
            {touchpoint.kpiBindings.length} KPI{touchpoint.kpiBindings.length > 1 ? 's' : ''}
          </Badge>
        ) : (
          <span className="flex items-center gap-1 text-xs text-muted-foreground">
            <AlertTriangle className="size-3" /> No KPIs
          </span>
        )}
      </div>

      {/* Actions */}
      <div className="flex items-center gap-1 opacity-0 group-hover/tp:opacity-100 transition-opacity shrink-0">
        <Button
          variant="ghost"
          size="icon"
          aria-label="Edit touchpoint"
          onClick={() => onEdit(touchpoint)}
          className="size-7"
        >
          <Pencil className="size-3.5" />
        </Button>
        <DropdownMenu>
          <DropdownMenuTrigger
            aria-label="More actions"
            className="inline-flex items-center justify-center rounded-md size-7 hover:bg-accent text-muted-foreground hover:text-foreground transition-colors"
          >
            <MoreHorizontal className="size-3.5" />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onClick={() => onDuplicate(touchpoint)}>
              <Copy className="size-4 me-2" /> Duplicate
            </DropdownMenuItem>
            <DropdownMenuItem
              onClick={() => onDelete(touchpoint.id)}
              className="text-destructive focus:text-destructive"
            >
              <Trash2 className="size-4 me-2" /> Delete
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </div>
  )
}
