// Audit Log page (T119), route /audit-log. A read-only, data-dense table of the
// tenant's M-10 audit events: event type (categorical brand-tinted badge), actor
// username, affected entity, and timestamp. Filter bar: event-type select + a from/to
// date range. Cursor pagination, loading skeletons, and an empty state. No edit/delete
// controls — the audit trail is append-only. RTL-first: logical properties throughout.
//
// Event-type badges are CATEGORICAL, so they use the brand palette (cyan/navy), never
// the D1–D5 semantic scale (Two-Palette Rule: semantic colors signal KPI status only).

import { useCallback, useEffect, useState } from "react"
import { useTranslation } from "react-i18next"
import { ScrollText } from "lucide-react"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Skeleton } from "@/components/ui/skeleton"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { listAuditEvents, type AuditLogEntry } from "@/features/audit-log/api"

const PAGE_SIZE = 25

// The M-10 event types the audit trail surfaces (data-model.md). Offered as filter
// options; the table itself renders whatever type the backend returns.
const EVENT_TYPES = [
  "user.created",
  "user.updated",
  "user.deactivated",
  "user.reactivated",
  "authentication.account.unlocked",
  "mfa.reset",
  "password.reset.requested",
  "permission.modified",
  "scope.assigned",
  "custom_rule.created",
  "custom_rule.updated",
  "custom_rule.deleted",
  "persona_baseline.updated",
  "session.created",
  "session.revoked",
] as const

/** Categorical brand-tint for an event type, by family (never the semantic D-scale). */
function eventBadgeClass(eventType: string): string {
  const family = eventType.split(".")[0]
  switch (family) {
    case "authentication":
    case "session":
    case "mfa":
    case "password":
      return "bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200"
    case "permission":
    case "scope":
    case "custom_rule":
    case "persona_baseline":
    case "role":
      return "bg-nb-navy-100 text-nb-navy-700 dark:bg-nb-navy-800/60 dark:text-nb-navy-200"
    default:
      return "bg-muted text-muted-foreground"
  }
}

export default function AuditLogPage() {
  const { t, i18n } = useTranslation()
  const timeFormatter = new Intl.DateTimeFormat(i18n.language === "ar" ? "ar" : "en", {
    dateStyle: "medium",
    timeStyle: "short",
  })

  const [events, setEvents] = useState<AuditLogEntry[]>([])
  const [nextPageToken, setNextPageToken] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(false)

  // Filters.
  const [eventType, setEventType] = useState<string>("all")
  const [from, setFrom] = useState("")
  const [to, setTo] = useState("")

  // Cursor pagination: `pageToken` is the current page; `history` lets us go back.
  const [pageToken, setPageToken] = useState<string | undefined>(undefined)
  const [history, setHistory] = useState<(string | undefined)[]>([])

  // Reset to the first page whenever a filter changes.
  useEffect(() => {
    setPageToken(undefined)
    setHistory([])
  }, [eventType, from, to])

  const load = useCallback(async () => {
    setLoading(true)
    setLoadError(false)
    try {
      const page = await listAuditEvents({
        pageSize: PAGE_SIZE,
        pageToken,
        eventType: eventType === "all" ? undefined : eventType,
        // Make the date range inclusive of whole days.
        from: from ? `${from}T00:00:00Z` : undefined,
        to: to ? `${to}T23:59:59Z` : undefined,
      })
      setEvents(page.items)
      setNextPageToken(page.nextPageToken)
    } catch {
      setLoadError(true)
      setEvents([])
      setNextPageToken(null)
    } finally {
      setLoading(false)
    }
  }, [pageToken, eventType, from, to])

  useEffect(() => {
    void load()
  }, [load])

  function goNext() {
    if (!nextPageToken) return
    setHistory((h) => [...h, pageToken])
    setPageToken(nextPageToken)
  }

  function goPrev() {
    setHistory((h) => {
      if (h.length === 0) return h
      const copy = [...h]
      const prev = copy.pop()
      setPageToken(prev)
      return copy
    })
  }

  return (
    <div className="space-y-5 py-5">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-heading font-bold">{t("auditLog.title")}</h1>
        <p className="text-sm text-muted-foreground mt-1">{t("auditLog.subtitle")}</p>
      </div>

      {/* Filters */}
      <div className="flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-end">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="filter-event-type">{t("auditLog.filterEventType")}</Label>
          <Select value={eventType} onValueChange={(v) => setEventType(v ?? "all")}>
            <SelectTrigger id="filter-event-type" className="sm:w-64" aria-label={t("auditLog.filterEventType")}>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">{t("auditLog.allEventTypes")}</SelectItem>
              {EVENT_TYPES.map((type) => (
                <SelectItem key={type} value={type}>
                  <span dir="ltr" className="font-mono text-sm">{type}</span>
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="filter-from">{t("auditLog.filterFrom")}</Label>
          <Input
            id="filter-from"
            type="date"
            dir="ltr"
            className="sm:w-44"
            value={from}
            max={to || undefined}
            onChange={(e) => setFrom(e.target.value)}
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="filter-to">{t("auditLog.filterTo")}</Label>
          <Input
            id="filter-to"
            type="date"
            dir="ltr"
            className="sm:w-44"
            value={to}
            min={from || undefined}
            onChange={(e) => setTo(e.target.value)}
          />
        </div>
      </div>

      {loadError && (
        <div
          role="alert"
          className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {t("auditLog.loadError")}
        </div>
      )}

      {/* Table / states */}
      <div className="overflow-hidden rounded-lg border border-border bg-card shadow-sm dark:shadow-none">
        <Table>
          <TableHeader className="sticky top-0 z-10">
            <TableRow>
              <TableHead className="text-start">{t("auditLog.colEvent")}</TableHead>
              <TableHead className="text-start">{t("auditLog.colActor")}</TableHead>
              <TableHead className="text-start">{t("auditLog.colEntity")}</TableHead>
              <TableHead className="text-start">{t("auditLog.colTime")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {loading ? (
              Array.from({ length: 6 }).map((_, i) => (
                <TableRow key={i}>
                  <TableCell colSpan={4}>
                    <Skeleton className="h-5 w-full" />
                  </TableCell>
                </TableRow>
              ))
            ) : events.length === 0 ? (
              <TableRow>
                <TableCell colSpan={4}>
                  <div className="flex flex-col items-center justify-center py-16 text-center">
                    <ScrollText className="size-12 text-muted-foreground mb-4" />
                    <h3 className="text-lg font-bold mb-2">{t("auditLog.emptyTitle")}</h3>
                    <p className="text-muted-foreground max-w-sm">{t("auditLog.emptyDesc")}</p>
                  </div>
                </TableCell>
              </TableRow>
            ) : (
              events.map((event) => (
                <TableRow key={event.eventId} className="hover:bg-muted/50 transition-colors">
                  <TableCell>
                    <Badge className={`font-mono ${eventBadgeClass(event.eventType)}`}>
                      <span dir="ltr">{event.eventType}</span>
                    </Badge>
                  </TableCell>
                  <TableCell className="text-sm" dir="ltr">
                    <span className="text-start">{event.actorUsername ?? t("auditLog.none")}</span>
                  </TableCell>
                  <TableCell className="text-sm">
                    <span className="font-medium">{event.entityType ?? t("auditLog.none")}</span>
                    {event.entityId && (
                      <span dir="ltr" className="block max-w-[220px] truncate font-mono text-xs text-muted-foreground">
                        {event.entityId}
                      </span>
                    )}
                  </TableCell>
                  <TableCell className="text-start text-sm tabular-nums">
                    <span dir="ltr">{timeFormatter.format(new Date(event.occurredAtUtc))}</span>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {/* Pagination */}
      {!loading && events.length > 0 && (
        <div className="flex items-center justify-end gap-2">
          <Button variant="secondary" onClick={goPrev} disabled={history.length === 0}>
            {t("auditLog.prev")}
          </Button>
          <Button variant="secondary" onClick={goNext} disabled={!nextPageToken}>
            {t("auditLog.next")}
          </Button>
        </div>
      )}
    </div>
  )
}
