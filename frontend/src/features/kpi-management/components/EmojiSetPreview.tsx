// T066 [US2] — Emoji Set picker + live preview. Lists the two v1 sets (FaceClassic, HandThumbs)
// and previews the K glyphs the current scale would render, per research.md R2's per-K slot rule.

import { Label } from "@/components/ui/label"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  EMOJI_SETS,
  type EmojiSetKey,
  type ScaleKey,
  emojiGlyphsForScale,
} from "@/components/cx/kpi/kpi-scale-meta"

const SETS: { key: EmojiSetKey; label: string }[] = [
  { key: "FaceClassic", label: "Faces (classic)" },
  { key: "HandThumbs", label: "Thumbs" },
]

export interface EmojiSetPreviewProps {
  value: EmojiSetKey | null
  onChange: (set: EmojiSetKey) => void
  scale: ScaleKey | null
  label: string
}

export function EmojiSetPreview({ value, onChange, scale, label }: EmojiSetPreviewProps) {
  const selected = value ?? "FaceClassic"
  const glyphs = scale ? emojiGlyphsForScale(selected, scale) : EMOJI_SETS[selected].slice(0, 5)

  return (
    <div className="space-y-1.5">
      <Label htmlFor="kpi-emoji-set">{label}</Label>
      <Select value={selected} onValueChange={(v) => onChange((v ?? "FaceClassic") as EmojiSetKey)}>
        <SelectTrigger id="kpi-emoji-set" className="w-full">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {SETS.map((s) => (
            <SelectItem key={s.key} value={s.key}>
              <span className="flex items-center gap-2">
                <span aria-hidden>{EMOJI_SETS[s.key][0]}</span>
                {s.label}
              </span>
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      <div className="flex flex-wrap gap-2 rounded-md border border-border bg-card p-3 text-2xl leading-none">
        {glyphs.map((g, i) => (
          <span key={i} aria-hidden>
            {g}
          </span>
        ))}
      </div>
    </div>
  )
}
