// Journey List page (T037, US-1): a data-dense table of the tenant's customer journeys with a
// count-pill summary, a search + status + type filter row, color-coded type badges, lifecycle
// status pills, a per-row actions menu, a create-journey dialog, loading skeletons, and teaching
// empty states (truly-empty vs filtered-empty). RTL-first; follows the design-system table rules.
//
// Journeys are bounded, low-cardinality *definitions* (a tenant has a handful, not thousands),
// and the M-16 list API exposes neither a search nor a type query param. So the page loads the
// set once (up to the API's 200-row page cap) and does search / status / type filtering — and the
// summary counts — client-side. If a tenant ever exceeds 200 journeys, `truncated` surfaces a
// note rather than silently capping the view.

import { useCallback, useEffect, useMemo, useState } from "react"
import { Link } from "react-router"
import { useTranslation } from "react-i18next"
import { Inbox, Map, MoreHorizontal, PencilLine, Plus, Search, SquarePen } from "lucide-react"

import { Badge } from "@/components/ui/badge"
import { Button, buttonVariants } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Skeleton } from "@/components/ui/skeleton"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { cn } from "@/lib/utils"
import { listJourneys, type JourneyStatus, type JourneySummary } from "@/features/journeys/api"
import { JourneyStatusBadge } from "@/features/journeys/components/JourneyStatusBadge"
import { JourneyFormDialog } from "@/features/journeys/components/JourneyFormDialog"

// Fetch the full set in one page (the API clamps to 200); journeys are few per tenant.
const FETCH_SIZE = 200

type StatusFilter = JourneyStatus | "all"
// Stat chips shown above the table — fixed Total / Active / Draft / Archived, always visible
// (mirrors the click-through design). Each also acts as a quick status filter (Total → all).
const STAT_CHIPS: StatusFilter[] = ["all", "Active", "Draft", "Archived"]
const STATUS_LABEL: Record<JourneyStatus, string> = {
  Draft: "journey.statusDraft",
  Active: "journey.statusActive",
  Inactive: "journey.statusInactive",
  Archived: "journey.statusArchived",
}

// `journey_type` is free-form on the wire; localize the known archetypes, fall back to the raw key.
const TYPE_LABEL: Record<string, string> = {
  Transactional: "journey.typeTransactional",
  Lifecycle: "journey.typeLifecycle",
  IssueResolution: "journey.typeIssueResolution",
  Onboarding: "journey.typeOnboarding",
}

// Type is a *category*, not a status — so its badge draws from the brand palette (cyan / mint /
// navy) per the Two-Palette Rule, never the semantic D-scale. Unknown types fall back to a
// neutral outline pill.
const TYPE_BADGE: Record<string, string> = {
  Transactional: "border-transparent bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200",
  Lifecycle: "border-transparent bg-nb-mint-100 text-nb-mint-800 dark:bg-nb-mint-900/30 dark:text-nb-mint-300",
  Onboarding: "border-transparent bg-muted text-muted-foreground",
  IssueResolution: "border-transparent bg-nb-navy-100 text-nb-navy dark:bg-nb-navy-700/50 dark:text-nb-navy-200",
}

// Count-pill tint per status — mirrors `JourneyStatusBadge` so the summary and the table agree.
// These signal lifecycle *state*, so the D-scale is the correct palette here.
const COUNT_PILL_STYLE: Record<StatusFilter, string> = {
  all: "bg-muted text-foreground",
  Active: "bg-d2-light text-d2-dark dark:bg-d2-dark/25 dark:text-d2-light",
  Draft: "bg-d3-light text-d3-dark dark:bg-d3-dark/25 dark:text-d3-light",
  Inactive: "bg-d4-light text-d4-dark dark:bg-d4-dark/25 dark:text-d4-light",
  Archived: "bg-muted text-muted-foreground",
}
const COUNT_LABEL: Record<StatusFilter, string> = {
  all: "journey.totalCount",
  Active: "journey.activeCount",
  Draft: "journey.draftCount",
  Inactive: "journey.inactiveCount",
  Archived: "journey.archivedCount",
}

/** Formats an ISO-8601 timestamp for the active locale, keeping Western digits in Arabic (CLAUDE.md). */
function formatUpdatedAt(iso: string, lang: string): string {
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return "—"
  const locale = lang.startsWith("ar") ? "ar-u-nu-latn" : "en"
  return new Intl.DateTimeFormat(locale, { year: "numeric", month: "short", day: "numeric" }).format(
    date,
  )
}

export default function JourneyListPage() {
  const { t, i18n } = useTranslation()

  const [allJourneys, setAllJourneys] = useState<JourneySummary[]>([])
  const [truncated, setTruncated] = useState(false)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(false)
  const [createOpen, setCreateOpen] = useState(false)
  // The journey whose details are being edited (drives the edit Sheet); null when closed.
  const [editTarget, setEditTarget] = useState<JourneySummary | null>(null)

  // Client-side filters (the API supports none of these directly).
  const [search, setSearch] = useState("")
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("all")
  const [typeFilter, setTypeFilter] = useState<string>("all")

  const load = useCallback(async () => {
    setLoading(true)
    setLoadError(false)
    try {
      const page = await listJourneys({ pageSize: FETCH_SIZE })
      setAllJourneys(page.items)
      setTruncated(Boolean(page.nextPageToken))
    } catch {
      setLoadError(true)
      setAllJourneys([])
      setTruncated(false)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  const counts = useMemo(() => {
    const c: Record<StatusFilter, number> = {
      all: allJourneys.length,
      Draft: 0,
      Active: 0,
      Inactive: 0,
      Archived: 0,
    }
    for (const j of allJourneys) if (j.status in c) c[j.status] += 1
    return c
  }, [allJourneys])

  const typeOptions = useMemo(
    () => Array.from(new Set(allJourneys.map((j) => j.journeyType))).sort(),
    [allJourneys],
  )

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase()
    return allJourneys.filter((j) => {
      if (statusFilter !== "all" && j.status !== statusFilter) return false
      if (typeFilter !== "all" && j.journeyType !== typeFilter) return false
      if (q && !`${j.name} ${j.description ?? ""}`.toLowerCase().includes(q)) return false
      return true
    })
  }, [allJourneys, search, statusFilter, typeFilter])

  const isFiltered = statusFilter !== "all" || typeFilter !== "all" || search.trim() !== ""

  function clearFilters() {
    setSearch("")
    setStatusFilter("all")
    setTypeFilter("all")
  }

  const typeText = (key: string) => (TYPE_LABEL[key] ? t(TYPE_LABEL[key]) : key)

  return (
    <div className="space-y-5 py-5">
      {/* Header */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
        <div className="min-w-0">
          <div className="flex items-center gap-2.5">
            <Map className="size-6 shrink-0 text-primary" aria-hidden />
            <h1 className="text-2xl font-heading font-bold">{t("journey.moduleTitle")}</h1>
          </div>
          <p className="mt-1 text-sm text-muted-foreground">{t("journey.moduleSubtitle")}</p>
        </div>
        <Button
          className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
          onClick={() => setCreateOpen(true)}
        >
          <Plus className="size-4 ms-2" aria-hidden />
          {t("journey.newJourney")}
        </Button>
      </div>

      {/* Summary stat chips (also act as quick status filters) */}
      <div className="flex flex-wrap items-center gap-3" role="group" aria-label={t("journey.filterStatus")}>
        {STAT_CHIPS.map((value) => {
          const active = statusFilter === value
          return (
            <button
              key={value}
              type="button"
              aria-pressed={active}
              onClick={() => setStatusFilter(value)}
              className={cn(
                "inline-flex items-center rounded-full px-3 py-1.5 text-sm font-medium tabular-nums transition-all",
                COUNT_PILL_STYLE[value],
                active
                  ? "ring-2 ring-ring ring-offset-1 ring-offset-background"
                  : "opacity-90 hover:opacity-100",
              )}
            >
              {t(COUNT_LABEL[value], { count: counts[value] })}
            </button>
          )
        })}
      </div>

      {/* Filter row: search + status + type */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end">
        <div className="flex-1 space-y-1.5 sm:max-w-sm">
          <label
            htmlFor="journey-search"
            className="block text-xs font-medium uppercase tracking-widest text-muted-foreground"
          >
            {t("journey.searchLabel")}
          </label>
          <div className="relative">
            <Search
              className="pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
              aria-hidden
            />
            <Input
              id="journey-search"
              type="search"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t("journey.searchPlaceholder")}
              className="ps-9"
            />
          </div>
        </div>

        <div className="flex flex-col gap-1.5 sm:w-48">
          <span className="block text-xs font-medium uppercase tracking-widest text-muted-foreground">
            {t("journey.filterStatus")}
          </span>
          <Select value={statusFilter} onValueChange={(v) => setStatusFilter((v ?? "all") as StatusFilter)}>
            <SelectTrigger className="w-full">
              <SelectValue>
                {(v) => (v === "all" ? t("journey.all") : t(STATUS_LABEL[v as JourneyStatus]))}
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">{t("journey.all")}</SelectItem>
              {(["Draft", "Active", "Inactive", "Archived"] as JourneyStatus[]).map((s) => (
                <SelectItem key={s} value={s}>
                  {t(STATUS_LABEL[s])}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="flex flex-col gap-1.5 sm:w-48">
          <span className="block text-xs font-medium uppercase tracking-widest text-muted-foreground">
            {t("journey.filterType")}
          </span>
          <Select value={typeFilter} onValueChange={(v) => setTypeFilter(v ?? "all")}>
            <SelectTrigger className="w-full">
              <SelectValue>{(v) => (v === "all" || v == null ? t("journey.all") : typeText(v as string))}</SelectValue>
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">{t("journey.all")}</SelectItem>
              {typeOptions.map((type) => (
                <SelectItem key={type} value={type}>
                  {typeText(type)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      {loadError && (
        <div
          role="alert"
          className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {t("journey.loadError")}
        </div>
      )}

      {/* Table / states */}
      <div className="overflow-hidden rounded-lg border border-border bg-card shadow-sm dark:shadow-none">
        <Table>
          <TableHeader>
            <TableRow className="bg-muted/30 hover:bg-muted/30">
              <TableHead className="text-start font-semibold text-foreground">{t("journey.colName")}</TableHead>
              <TableHead className="text-start font-semibold text-foreground">{t("journey.colType")}</TableHead>
              <TableHead className="text-start font-semibold text-foreground">{t("journey.colStatus")}</TableHead>
              <TableHead className="text-start font-semibold text-foreground">{t("journey.colVersion")}</TableHead>
              <TableHead className="text-center font-semibold text-foreground">{t("journey.colStages")}</TableHead>
              <TableHead className="text-center font-semibold text-foreground">{t("journey.colTouchpoints")}</TableHead>
              <TableHead className="text-start font-semibold text-foreground">{t("journey.colLastUpdated")}</TableHead>
              <TableHead className="text-end font-semibold text-foreground">{t("journey.colActions")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {loading ? (
              Array.from({ length: 5 }).map((_, i) => (
                <TableRow key={i}>
                  <TableCell colSpan={8}>
                    <Skeleton className="h-5 w-full" />
                  </TableCell>
                </TableRow>
              ))
            ) : filtered.length === 0 ? (
              <TableRow>
                <TableCell colSpan={8}>
                  <div className="flex flex-col items-center justify-center py-16 text-center">
                    <Inbox className="mb-4 size-12 text-muted-foreground" aria-hidden />
                    <h3 className="mb-2 text-lg font-bold">
                      {isFiltered ? t("journey.emptyFilterTitle") : t("journey.noJourneys")}
                    </h3>
                    <p className="mb-4 max-w-sm text-muted-foreground">
                      {isFiltered ? t("journey.emptyFilterHelp") : t("journey.noJourneysHelp")}
                    </p>
                    {isFiltered ? (
                      <Button variant="secondary" onClick={clearFilters}>
                        {t("journey.clearFilter")}
                      </Button>
                    ) : (
                      <Button
                        className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
                        onClick={() => setCreateOpen(true)}
                      >
                        <Plus className="size-4 ms-2" aria-hidden />
                        {t("journey.newJourney")}
                      </Button>
                    )}
                  </div>
                </TableCell>
              </TableRow>
            ) : (
              filtered.map((journey) => (
                <TableRow key={journey.journeyId} className="transition-colors hover:bg-muted/50">
                  <TableCell>
                    <Link
                      to={`/journeys/${journey.journeyId}/builder`}
                      className="block text-start font-semibold text-foreground transition-colors hover:text-primary hover:underline"
                    >
                      {journey.name}
                    </Link>
                  </TableCell>
                  <TableCell>
                    <Badge
                      variant="outline"
                      className={TYPE_BADGE[journey.journeyType] ?? undefined}
                    >
                      {typeText(journey.journeyType)}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <JourneyStatusBadge status={journey.status} />
                  </TableCell>
                  {/* Version: the list API (JourneySummary) does not yet return a published-version
                      number, so this mirrors the design's unpublished state ("—"). Wire to the real
                      field once the backend list response exposes it. */}
                  <TableCell className="text-sm tabular-nums text-muted-foreground">—</TableCell>
                  <TableCell className="text-center tabular-nums">{journey.stageCount}</TableCell>
                  <TableCell className="text-center tabular-nums">{journey.touchpointCount}</TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {formatUpdatedAt(journey.updatedAt, i18n.language)}
                  </TableCell>
                  <TableCell className="text-end">
                    <DropdownMenu>
                      <DropdownMenuTrigger
                        className={cn(buttonVariants({ variant: "ghost", size: "icon-sm" }))}
                        aria-label={t("journey.colActions")}
                      >
                        <MoreHorizontal className="size-4" aria-hidden />
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem render={<Link to={`/journeys/${journey.journeyId}/builder`} />}>
                          <SquarePen className="size-4" aria-hidden />
                          {t("journey.openBuilder")}
                        </DropdownMenuItem>
                        {journey.status !== "Archived" && (
                          <DropdownMenuItem onClick={() => setEditTarget(journey)}>
                            <PencilLine className="size-4" aria-hidden />
                            {t("journey.editDetails")}
                          </DropdownMenuItem>
                        )}
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {!loading && truncated && (
        <p className="text-xs text-muted-foreground">{t("journey.listTruncated", { count: FETCH_SIZE })}</p>
      )}

      <JourneyFormDialog open={createOpen} onOpenChange={setCreateOpen} onSaved={() => void load()} />
      <JourneyFormDialog
        open={editTarget !== null}
        onOpenChange={(o) => {
          if (!o) setEditTarget(null)
        }}
        journey={editTarget}
        onSaved={() => void load()}
      />
    </div>
  )
}
