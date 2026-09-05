// Locale-aware "2 days ago" formatting via Intl.RelativeTimeFormat. Arabic output
// forces Latin numerals (`-u-nu-latn`) per the design system's Western-digits rule.

export function formatRelativeTime(iso: string, locale: string): string {
  const then = new Date(iso).getTime()
  if (Number.isNaN(then)) return "—"

  const rtf = new Intl.RelativeTimeFormat(
    locale.startsWith("ar") ? "ar-u-nu-latn" : locale,
    { numeric: "auto" }
  )

  const diffMs = then - Date.now()
  const abs = Math.abs(diffMs)
  const MINUTE = 60_000
  const HOUR = 3_600_000
  const DAY = 86_400_000

  if (abs < MINUTE) return rtf.format(0, "minute")
  if (abs < HOUR) return rtf.format(Math.round(diffMs / MINUTE), "minute")
  if (abs < DAY) return rtf.format(Math.round(diffMs / HOUR), "hour")
  if (abs < 30 * DAY) return rtf.format(Math.round(diffMs / DAY), "day")
  if (abs < 365 * DAY) return rtf.format(Math.round(diffMs / (30 * DAY)), "month")
  return rtf.format(Math.round(diffMs / (365 * DAY)), "year")
}
