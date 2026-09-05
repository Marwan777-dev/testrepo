// Shared scale metadata + emoji-set glyph logic for the KPI question/dashboard previews
// (consumed by UniversalArcGauge T060, KpiQuestionPreview T061, EmojiSetPreview T066). Keeping it
// here keeps the shared cx/kpi components self-contained (no dependency on the feature dto).

export type ScaleKey =
  | "Scale0_10"
  | "Scale1_3"
  | "Scale1_5"
  | "Scale1_7"
  | "Scale1_10"
  | "Scale1_100"
  | "Nps"

export interface ScaleMeta {
  /** Lowest response value. */
  min: number
  /** Highest response value. */
  max: number
  /** Number of discrete response points (boxes) on the scale. */
  points: number
}

export const SCALE_META: Record<ScaleKey, ScaleMeta> = {
  Scale0_10: { min: 0, max: 10, points: 11 },
  Scale1_3: { min: 1, max: 3, points: 3 },
  Scale1_5: { min: 1, max: 5, points: 5 },
  Scale1_7: { min: 1, max: 7, points: 7 },
  Scale1_10: { min: 1, max: 10, points: 10 },
  Scale1_100: { min: 1, max: 100, points: 100 },
  Nps: { min: -100, max: 100, points: 201 },
}

/** Ordered response values for a scale, e.g. Scale1_5 → [1,2,3,4,5]. */
export function scalePoints(scale: ScaleKey): number[] {
  const { min, points } = SCALE_META[scale]
  return Array.from({ length: points }, (_, i) => min + i)
}

// ── Emoji sets (research.md R2) ────────────────────────────────────────────────
// Each set is a canonical 11-glyph ordered sequence indexed 0..10 (worst → best). At render time
// for a K-point scale, K glyphs are picked from the sequence using linearly-spaced indices, with
// the boundary glyphs pinned to 0 and 10.

export type EmojiSetKey = "FaceClassic" | "HandThumbs"

export const EMOJI_SETS: Record<EmojiSetKey, readonly string[]> = {
  FaceClassic: ["😡", "😠", "😟", "😕", "😐", "🙂", "😊", "😄", "😁", "😍", "🤩"],
  // Thumbs/hand gradient down → up; repeated within zones to fill the 11-slot sequence.
  HandThumbs: ["👎", "👎", "👎", "🤏", "🤏", "👌", "👌", "👍", "👍", "👍", "👍"],
}

// Per-K index tables (research.md R2). Boundary glyphs pinned to 0 and 10.
const PER_K_INDICES: Record<number, number[]> = {
  3: [0, 5, 10],
  5: [0, 3, 5, 8, 10],
  7: [0, 2, 4, 5, 6, 8, 10],
  10: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
  11: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
}

/** Returns the 0..10 sequence indices to use for a K-point scale (linear-spaced fallback). */
export function emojiIndicesForK(k: number): number[] {
  if (PER_K_INDICES[k]) return PER_K_INDICES[k]
  if (k <= 1) return [10]
  return Array.from({ length: k }, (_, i) => Math.round((i / (k - 1)) * 10))
}

/** The ordered glyphs to render for a given emoji set + scale (one glyph per response point). */
export function emojiGlyphsForScale(set: EmojiSetKey, scale: ScaleKey): string[] {
  const sequence = EMOJI_SETS[set]
  const k = SCALE_META[scale].points
  // Emoji rendering is only meaningful for small scales. Scales within the 11-glyph sequence
  // (incl. Scale0_10's 11 points) get one glyph per point; wider scales (1–100, NPS) collapse to
  // 10 glyphs — matching the 10-chip cap the Number style uses for the same scales.
  const effectiveK = k > 11 ? 10 : k
  return emojiIndicesForK(effectiveK).map((i) => sequence[i])
}
