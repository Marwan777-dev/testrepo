import {
  DndContext,
  closestCenter,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  DragEndEvent,
} from '@dnd-kit/core'
import {
  SortableContext,
  sortableKeyboardCoordinates,
  verticalListSortingStrategy,
  arrayMove,
} from '@dnd-kit/sortable'
import { restrictToVerticalAxis } from '@dnd-kit/modifiers'
import StageCard from './StageCard'
import type { Stage } from '@/types/journey'

interface Props {
  stages: Stage[]
  onReorder: (stages: Stage[]) => void
  onUpdateStage: (updated: Stage) => void
  onDuplicateStage: (stage: Stage) => void
  onDeleteStage: (id: string) => void
  onDirty: () => void
}

export default function StageList({
  stages,
  onReorder,
  onUpdateStage,
  onDuplicateStage,
  onDeleteStage,
  onDirty,
}: Props) {
  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates })
  )

  function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event
    if (!over || active.id === over.id) return

    const oldIndex = stages.findIndex(s => s.id === active.id)
    const newIndex = stages.findIndex(s => s.id === over.id)
    const reordered = arrayMove(stages, oldIndex, newIndex).map((s, i) => ({
      ...s,
      sequenceOrder: i + 1,
    }))
    onReorder(reordered)
    onDirty()
  }

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCenter}
      modifiers={[restrictToVerticalAxis]}
      onDragEnd={handleDragEnd}
    >
      <SortableContext items={stages.map(s => s.id)} strategy={verticalListSortingStrategy}>
        <div className="space-y-3">
          {stages.map((stage, idx) => (
            <StageCard
              key={stage.id}
              stage={stage}
              stageIndex={idx + 1}
              onUpdate={onUpdateStage}
              onDuplicate={onDuplicateStage}
              onDelete={onDeleteStage}
              onDirty={onDirty}
            />
          ))}
        </div>
      </SortableContext>
    </DndContext>
  )
}
