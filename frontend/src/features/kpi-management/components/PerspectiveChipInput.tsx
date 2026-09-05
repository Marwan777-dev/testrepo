// T065 [US2] — perspective chip input. Type a label and press Enter or comma to add a chip; × to
// remove. Max 10 chips, ≤ 60 chars each. display_order follows chip position (full-replace on save,
// FR-028). RTL-aware (logical properties).

import { useState, type KeyboardEvent } from "react"
import { useTranslation } from "react-i18next"
import { X } from "lucide-react"

import { Label } from "@/components/ui/label"
import type { KpiPerspective } from "@/features/kpi-management/api"

const MAX_CHIPS = 10
const MAX_LEN = 60

export interface PerspectiveChipInputProps {
  value: KpiPerspective[]
  onChange: (next: KpiPerspective[]) => void
  readOnly?: boolean
}

export function PerspectiveChipInput({ value, onChange, readOnly }: PerspectiveChipInputProps) {
  const { t } = useTranslation()
  const [draft, setDraft] = useState("")

  const reindex = (labels: KpiPerspective[]): KpiPerspective[] =>
    labels.map((p, i) => ({ ...p, displayOrder: i }))

  const addChip = () => {
    const label = draft.trim().slice(0, MAX_LEN)
    if (label === "" || value.length >= MAX_CHIPS) {
      setDraft("")
      return
    }
    if (value.some((p) => p.label.toLowerCase() === label.toLowerCase())) {
      setDraft("")
      return
    }
    onChange(reindex([...value, { label, displayOrder: value.length }]))
    setDraft("")
  }

  const removeChip = (index: number) => {
    onChange(reindex(value.filter((_, i) => i !== index)))
  }

  const onKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter" || e.key === ",") {
      e.preventDefault()
      addChip()
    } else if (e.key === "Backspace" && draft === "" && value.length > 0) {
      removeChip(value.length - 1)
    }
  }

  return (
    <div className="space-y-1.5">
      <Label htmlFor="kpi-perspective-input">{t("kpi.perspectives")}</Label>
      <div className="flex flex-wrap items-center gap-2 rounded-md border border-input bg-card p-2">
        {value.map((perspective, index) => (
          <span
            key={`${perspective.label}-${index}`}
            className="inline-flex items-center gap-1 rounded-sm bg-nb-cyan-100 px-2 py-0.5 text-sm text-nb-cyan-800 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200"
          >
            {perspective.label}
            {!readOnly && (
              <button
                type="button"
                onClick={() => removeChip(index)}
                className="rounded-full p-0.5 hover:bg-nb-cyan-200/60 dark:hover:bg-nb-cyan-800/60"
                aria-label={t("kpi.removePerspective", { label: perspective.label })}
              >
                <X className="size-3" aria-hidden />
              </button>
            )}
          </span>
        ))}
        {!readOnly && value.length < MAX_CHIPS && (
          <input
            id="kpi-perspective-input"
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={onKeyDown}
            onBlur={addChip}
            maxLength={MAX_LEN}
            className="min-w-32 flex-1 bg-transparent text-sm outline-none placeholder:text-muted-foreground"
            placeholder={t("kpi.perspectivePlaceholder")}
          />
        )}
      </div>
      <p className="text-xs text-muted-foreground">{t("kpi.perspectivesHelp")}</p>
    </div>
  )
}
