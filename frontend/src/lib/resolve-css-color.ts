// Resolve a theme CSS variable (e.g. --primary) to a hex string usable in
// JS-driven surfaces: <input type="color">, canvas, inline preview styles. Tenant
// themes emit OKLCH values, which those surfaces can't consume directly — a canvas
// context parses any CSS color and serialises opaque colors back as #rrggbb.

export function resolveCssColorVar(varName: string, fallback: string): string {
  if (typeof document === "undefined") return fallback
  const raw = getComputedStyle(document.documentElement).getPropertyValue(varName).trim()
  if (!raw) return fallback
  const ctx = document.createElement("canvas").getContext("2d")
  if (!ctx) return fallback
  ctx.fillStyle = fallback
  ctx.fillStyle = raw
  const value = ctx.fillStyle
  // Opaque colors serialise as #rrggbb; anything else (rgba/invalid) keeps fallback.
  return /^#[0-9a-f]{6}$/i.test(value) ? value : fallback
}
