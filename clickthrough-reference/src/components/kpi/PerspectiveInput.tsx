// src/components/kpi/PerspectiveInput.tsx
import { useState, useRef, KeyboardEvent } from "react"
import { X } from "lucide-react"
import { Badge } from "@/components/ui/badge"
import { Input } from "@/components/ui/input"
import { useTranslation } from "react-i18next"
import { cn } from "@/lib/utils"

interface PerspectiveInputProps {
  value: string[]
  onChange: (value: string[]) => void
  disabled?: boolean
}

const MAX_COUNT = 10
const MAX_LENGTH = 60

export default function PerspectiveInput({ value, onChange, disabled }: PerspectiveInputProps) {
  const { t } = useTranslation()
  const [inputValue, setInputValue] = useState("")
  const inputRef = useRef<HTMLInputElement>(null)

  function addPerspective(raw: string) {
    const trimmed = raw.trim().slice(0, MAX_LENGTH)
    if (!trimmed || value.includes(trimmed) || value.length >= MAX_COUNT) return
    onChange([...value, trimmed])
    setInputValue("")
  }

  function removePerspective(item: string) {
    onChange(value.filter((p) => p !== item))
  }

  function handleKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === "Enter" || e.key === ",") {
      e.preventDefault()
      addPerspective(inputValue)
    } else if (e.key === "Backspace" && !inputValue && value.length > 0) {
      removePerspective(value[value.length - 1])
    }
  }

  function handleBlur() {
    if (inputValue.trim()) addPerspective(inputValue)
  }

  const atLimit = value.length >= MAX_COUNT

  return (
    <div
      className={cn(
        "min-h-10 rounded-md border border-input bg-card px-3 py-2 flex flex-wrap gap-1.5 cursor-text",
        disabled && "opacity-50 cursor-not-allowed",
      )}
      onClick={() => inputRef.current?.focus()}
    >
      {value.map((p) => (
        <Badge key={p} variant="outline" className="gap-1 pe-1 text-xs">
          {p}
          {!disabled && (
            <button
              type="button"
              aria-label={`Remove ${p}`}
              onClick={(e) => { e.stopPropagation(); removePerspective(p) }}
              className="hover:text-destructive transition-colors"
            >
              <X className="size-3" />
            </button>
          )}
        </Badge>
      ))}

      {!disabled && !atLimit && (
        <Input
          ref={inputRef}
          value={inputValue}
          onChange={(e) => setInputValue(e.target.value.slice(0, MAX_LENGTH))}
          onKeyDown={handleKeyDown}
          onBlur={handleBlur}
          placeholder={value.length === 0 ? t("kpi.addPerspective") : ""}
          className="h-auto border-0 bg-transparent p-0 text-sm shadow-none focus-visible:ring-0 min-w-24 flex-1"
        />
      )}
    </div>
  )
}
