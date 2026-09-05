/**
 * Minimal, dependency-free OKLCH color math for the tenant theming engine.
 *
 * Why OKLCH: perceptual lightness (L) lets us generate tint/shade ramps that
 * look evenly stepped — the same property that makes the hand-tuned Nabadat
 * `nb-*` ramps feel balanced. Plain HSL lightening looks muddy by comparison.
 *
 * All functions operate on `#rrggbb` hex strings and clamp to the sRGB gamut.
 * Matrices are Björn Ottosson's reference OKLab transform.
 */

export interface Oklch {
  /** Perceptual lightness, 0..1 */
  l: number
  /** Chroma, 0..~0.4 */
  c: number
  /** Hue in degrees, 0..360 */
  h: number
}

/* ---------- hex <-> sRGB ---------- */

function parseHex(hex: string): [number, number, number] {
  let h = hex.trim().replace(/^#/, "")
  if (h.length === 3) h = h.split("").map((ch) => ch + ch).join("")
  if (h.length !== 6 || /[^0-9a-fA-F]/.test(h)) {
    throw new Error(`Invalid hex color: "${hex}"`)
  }
  return [
    parseInt(h.slice(0, 2), 16) / 255,
    parseInt(h.slice(2, 4), 16) / 255,
    parseInt(h.slice(4, 6), 16) / 255,
  ]
}

function toHex(rgb: [number, number, number]): string {
  const part = (v: number) =>
    Math.max(0, Math.min(255, Math.round(v * 255)))
      .toString(16)
      .padStart(2, "0")
  return `#${part(rgb[0])}${part(rgb[1])}${part(rgb[2])}`
}

/** `rgba(r, g, b, a)` string from a hex color — for translucent border/input tokens. */
export function hexToRgbaString(hex: string, alpha: number): string {
  const [r, g, b] = parseHex(hex)
  return `rgba(${Math.round(r * 255)}, ${Math.round(g * 255)}, ${Math.round(b * 255)}, ${alpha})`
}

/* ---------- gamma ---------- */

const srgbToLinear = (c: number) =>
  c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4)

const linearToSrgb = (c: number) =>
  c <= 0.0031308 ? 12.92 * c : 1.055 * Math.pow(c, 1 / 2.4) - 0.055

/* ---------- sRGB <-> OKLab <-> OKLCH ---------- */

export function hexToOklch(hex: string): Oklch {
  const [r8, g8, b8] = parseHex(hex)
  const r = srgbToLinear(r8)
  const g = srgbToLinear(g8)
  const b = srgbToLinear(b8)

  const l_ = Math.cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b)
  const m_ = Math.cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b)
  const s_ = Math.cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b)

  const L = 0.2104542553 * l_ + 0.793617785 * m_ - 0.0040720468 * s_
  const a = 1.9779984951 * l_ - 2.428592205 * m_ + 0.4505937099 * s_
  const bb = 0.0259040371 * l_ + 0.7827717662 * m_ - 0.808675766 * s_

  const c = Math.sqrt(a * a + bb * bb)
  let h = (Math.atan2(bb, a) * 180) / Math.PI
  if (h < 0) h += 360
  return { l: L, c, h }
}

export function oklchToHex({ l, c, h }: Oklch): string {
  const hr = (h * Math.PI) / 180
  const a = c * Math.cos(hr)
  const b = c * Math.sin(hr)

  const l_ = l + 0.3963377774 * a + 0.2158037573 * b
  const m_ = l - 0.1055613458 * a - 0.0638541728 * b
  const s_ = l - 0.0894841775 * a - 1.291485548 * b

  const lc = l_ * l_ * l_
  const mc = m_ * m_ * m_
  const sc = s_ * s_ * s_

  const r = 4.0767416621 * lc - 3.3077115913 * mc + 0.2309699292 * sc
  const g = -1.2684380046 * lc + 2.6097574011 * mc - 0.3413193965 * sc
  const bch = -0.0041960863 * lc - 0.7034186147 * mc + 1.707614701 * sc

  return toHex([linearToSrgb(r), linearToSrgb(g), linearToSrgb(bch)])
}

/* ---------- helpers ---------- */

/** Return a copy of `hex` re-lit to perceptual lightness `l` (0..1), with chroma scaled. */
export function withLightness(hex: string, l: number, chromaScale = 1): string {
  const o = hexToOklch(hex)
  return oklchToHex({ l, c: o.c * chromaScale, h: o.h })
}

/** WCAG relative luminance of a hex color (0..1). */
function relativeLuminance(hex: string): number {
  const [r, g, b] = parseHex(hex).map(srgbToLinear) as [number, number, number]
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

/**
 * Pick a readable foreground (`light` vs `dark`) for a given background.
 * Defaults match the Nabadat tokens: white text, navy text.
 */
export function readableForeground(
  bg: string,
  light = "#ffffff",
  dark = "#1E2235",
): string {
  return relativeLuminance(bg) > 0.45 ? dark : light
}

/**
 * One ramp step: a target perceptual lightness plus a chroma multiplier
 * (tints desaturate toward white, deep shades pull chroma back slightly).
 */
export interface RampStop {
  l: number
  chromaScale: number
}

export type RampKey = "100" | "200" | "300" | "DEFAULT" | "700" | "800" | "900"

/** Build a `{100,200,300,DEFAULT,700,800,900}` ramp from a seed, per the supplied stops. */
export function buildRamp(
  seed: string,
  stops: Record<Exclude<RampKey, "DEFAULT">, RampStop>,
): Record<RampKey, string> {
  return {
    "100": withLightness(seed, stops["100"].l, stops["100"].chromaScale),
    "200": withLightness(seed, stops["200"].l, stops["200"].chromaScale),
    "300": withLightness(seed, stops["300"].l, stops["300"].chromaScale),
    DEFAULT: seed,
    "700": withLightness(seed, stops["700"].l, stops["700"].chromaScale),
    "800": withLightness(seed, stops["800"].l, stops["800"].chromaScale),
    "900": withLightness(seed, stops["900"].l, stops["900"].chromaScale),
  }
}
