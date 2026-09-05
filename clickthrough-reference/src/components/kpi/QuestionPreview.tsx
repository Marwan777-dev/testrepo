// src/components/kpi/QuestionPreview.tsx
import { useTranslation } from "react-i18next"
import { Star } from "lucide-react"
import { Slider } from "@/components/ui/slider"
import type { KpiScale, RepresentationStyle, EmojiSet } from "@/types/kpi"

// Full emoji spectrum (worst → best), 10 entries each.
// pickEmojis() selects N evenly-distributed entries for any scale.
const EMOJI_SETS: Record<EmojiSet, string[]> = {
  FaceClassic:    ["😞", "😟", "😕", "😐", "😑", "🙂", "😊", "😄", "😁", "🤩"],
  FaceExpressive: ["😭", "😢", "😟", "😕", "😶", "😐", "😊", "😄", "😆", "🤩"],
  FaceBold:       ["🥺", "😔", "😒", "😑", "😐", "😌", "😊", "😁", "🥳", "🎉"],
  Thumbs:         ["👎", "👎", "👎", "✋", "✋", "✋", "✋", "👍", "👍", "👍"],
  Hearts:         ["💔", "💔", "💔", "🤍", "🤍", "🤍", "❤️", "❤️", "💖", "💖"],
  Weather:        ["⛈️", "🌧️", "🌦️", "⛅", "🌤️", "☀️", "🌟", "✨", "🌈", "🌠"],
  TrafficLights:  ["🔴", "🔴", "🔴", "🟡", "🟡", "🟡", "🟢", "🟢", "🟢", "🟢"],
  Shapes:         ["⬛", "🔷", "🔹", "⭐", "🔷", "🔸", "💎", "✨", "🌟", "💫"],
}

// Scale value count (cap 1–100 at 10 for display)
function scaleCount(scale: KpiScale): number {
  const map: Record<KpiScale, number> = {
    "0–10": 11, "1–3": 3, "1–5": 5, "1–7": 7, "1–10": 10, "1–100": 10,
  }
  return map[scale]
}

function scaleMin(scale: KpiScale): number {
  return scale.startsWith("0") ? 0 : 1
}

function pickEmojis(set: EmojiSet, count: number): string[] {
  const src = EMOJI_SETS[set]
  if (count <= 0) return []
  if (count === 1) return [src[Math.floor(src.length / 2)]]
  const result: string[] = []
  for (let i = 0; i < count; i++) {
    const idx = Math.round((i / (count - 1)) * (src.length - 1))
    result.push(src[idx])
  }
  return result
}

interface QuestionPreviewProps {
  fullName: string
  kpiId: string
  scale: KpiScale
  representationStyle: RepresentationStyle
  emojiSet: EmojiSet
  scaleMinLabel?: string
  scaleMaxLabel?: string
}

const STANDARD_QUESTIONS: Record<string, string> = {
  nps: "kpi.questionNps",
  csat: "kpi.questionCsat",
  ces: "kpi.questionCes",
  fcr: "kpi.questionFcr",
  vfm: "kpi.questionVfm",
  agent: "kpi.questionAgent",
  chs: "kpi.questionChs",
}

export default function QuestionPreview({ fullName, kpiId, scale, representationStyle, emojiSet, scaleMinLabel, scaleMaxLabel }: QuestionPreviewProps) {
  const { t } = useTranslation()

  const questionKey = STANDARD_QUESTIONS[kpiId]
  const question = questionKey
    ? t(questionKey)
    : t("kpi.questionCustom", { name: fullName || "…" })

  const count = scaleCount(scale)
  const min = scaleMin(scale)
  const max = scale === "1–100" ? 100 : min + count - 1

  return (
    <div className="rounded-lg border border-border bg-muted/20 p-4 space-y-4">
      {/* Question label */}
      <p className="text-sm font-medium leading-relaxed text-foreground">{question}</p>

      {/* Response row */}
      <div>
        {representationStyle === "Number" && (
          <div className="space-y-2">
            <div className="flex items-center gap-1.5 flex-wrap justify-center">
              {Array.from({ length: count }, (_, i) => min + i).map((n) => (
                <button
                  key={n}
                  type="button"
                  className="size-8 rounded-md border border-border bg-card text-sm font-medium tabular-nums hover:bg-primary hover:text-primary-foreground transition-colors"
                >
                  {n}
                </button>
              ))}
            </div>
            {(scaleMinLabel || scaleMaxLabel) && (
              <div className="flex justify-between text-xs text-muted-foreground">
                <span>{scaleMinLabel}</span>
                <span>{scaleMaxLabel}</span>
              </div>
            )}
          </div>
        )}

        {representationStyle === "Stars" && (
          <div className="space-y-2">
            <div className="flex items-center gap-1 justify-center">
              {Array.from({ length: Math.min(count, 10) }, (_, i) => (
                <Star
                  key={i}
                  className={`size-7 ${i < 3 ? "fill-amber-400 text-amber-400" : "text-muted-foreground"}`}
                />
              ))}
            </div>
            {(scaleMinLabel || scaleMaxLabel) && (
              <div className="flex justify-between text-xs text-muted-foreground">
                <span>{scaleMinLabel}</span>
                <span>{scaleMaxLabel}</span>
              </div>
            )}
          </div>
        )}

        {representationStyle === "Emoji" && (
          <div className="space-y-2">
            <div className="flex items-center gap-2 flex-wrap justify-center">
              {pickEmojis(emojiSet, Math.min(count, 10)).map((emoji, i) => (
                <button
                  key={i}
                  type="button"
                  className="text-2xl hover:scale-125 transition-transform"
                  aria-label={`Option ${i + 1}`}
                >
                  {emoji}
                </button>
              ))}
            </div>
            {(scaleMinLabel || scaleMaxLabel) && (
              <div className="flex justify-between text-xs text-muted-foreground">
                <span>{scaleMinLabel}</span>
                <span>{scaleMaxLabel}</span>
              </div>
            )}
          </div>
        )}

        {representationStyle === "Slider" && (
          <div className="w-full space-y-2">
            <Slider
              defaultValue={[Math.ceil((min + max) / 2)]}
              min={min}
              max={max}
              step={1}
              className="w-full"
            />
            <div className="flex justify-between text-xs text-muted-foreground">
              <span>{scaleMinLabel || min}</span>
              <span>{scaleMaxLabel || max}</span>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
