// Persona Management page (T077, US-3): a data-dense table of the tenant's customer personas with a
// count-pill summary, a search + status filter, bilingual names, lifecycle status badges, a
// journey-binding-count indicator, P-01-only lifecycle transition controls, and a create-persona
// dialog. Personas move through a four-state lifecycle (Draft → Active ↔ Inactive → Archived); only
// `Active` personas are bindable to journeys (FR-005), and a persona with active bindings cannot be
// archived (409 `persona.archive_blocked_active_bindings`). RTL-first; follows the design-system
// table rules.
//
// Personas are bounded, low-cardinality definitions (a tenant has a handful), and the M-16 list API
// exposes no search query param — so the page loads the set once (up to the API's 200-row cap) and
// does search / status filtering client-side. `truncated` surfaces a note rather than silently
// capping the view if a tenant ever exceeds 200 personas.

import { useCallback, useEffect, useMemo, useState } from "react"
import { useTranslation } from "react-i18next"
import {
  Archive,
  CirclePause,
  CirclePlay,
  Inbox,
  MoreHorizontal,
  Plus,
  Route as RouteIcon,
  Search,
  UsersRound,
  type LucideIcon,
} from "lucide-react"
import { toast } from "sonner"

import { Button, buttonVariants } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Skeleton } from "@/components/ui/skeleton"
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog"
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
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { cn } from "@/lib/utils"
import { useSession } from "@/features/auth/hooks/useSession"
import {
  changePersonaStatus,
  JourneysApiError,
  listPersonas,
  type PersonaStatus,
  type PersonaSummary,
} from "@/features/journeys/api"
import { CreatePersonaDialog } from "@/features/journeys/components/CreatePersonaDialog"
import { PersonaStatusBadge } from "@/features/journeys/components/PersonaStatusBadge"

// Fetch the full set in one page (the API clamps to 200); personas are few per tenant.
const FETCH_SIZE = 200

type StatusFilter = PersonaStatus | "all"
const STATUS_FILTERS: StatusFilter[] = ["all", "Active", "Draft", "Inactive", "Archived"]

// Persona lifecycle status → i18n label (used by the status filter Select). The status *badge* is
// the shared `PersonaStatusBadge` component (Two-Palette D-scale tokens + icons live there).
const STATUS_LABEL: Record<PersonaStatus, string> = {
  Draft: "persona.statusDraft",
  Active: "persona.statusActive",
  Inactive: "persona.statusInactive",
  Archived: "persona.statusArchived",
}

// Count-pill tint per status — mirrors the status badge so the summary and the table agree.
const COUNT_PILL_STYLE: Record<StatusFilter, string> = {
  all: "bg-muted text-foreground",
  Active: "bg-d2-light text-d2-dark dark:bg-d2-dark/25 dark:text-d2-light",
  Draft: "bg-muted text-muted-foreground",
  Inactive: "bg-d3-light text-d3-dark dark:bg-d3-dark/25 dark:text-d3-light",
  Archived: "bg-d5-light text-d5-dark dark:bg-d5-dark/25 dark:text-d5-light",
}
const COUNT_LABEL: Record<StatusFilter, string> = {
  all: "persona.totalCount",
  Active: "persona.activeCount",
  Draft: "persona.draftCount",
  Inactive: "persona.inactiveCount",
  Archived: "persona.archivedCount",
}

// The non-archive transitions available from each lifecycle state. Archiving is handled separately
// (it is destructive + needs a confirm), so it is NOT listed here. Mirrors the backend state machine
// (Draft→Active, Active→Inactive, Inactive→Active).
const NEXT_TRANSITIONS: Record<PersonaStatus, { target: PersonaStatus; labelKey: string; icon: LucideIcon }[]> = {
  Draft: [{ target: "Active", labelKey: "persona.activate", icon: CirclePlay }],
  Active: [{ target: "Inactive", labelKey: "persona.deactivate", icon: CirclePause }],
  Inactive: [{ target: "Active", labelKey: "persona.activate", icon: CirclePlay }],
  Archived: [],
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

export default function PersonaManagementPage() {
  const { t, i18n } = useTranslation()
  const { session } = useSession()
  const isArabic = i18n.language.startsWith("ar")

  // Persona creation + lifecycle management is P-01 only (the spec's persona RBAC). P-02 reaches the
  // page (it may read personas) but sees a read-only list with no create button or transition menu.
  const canManage = session?.persona === "P-01"

  const [allPersonas, setAllPersonas] = useState<PersonaSummary[]>([])
  const [truncated, setTruncated] = useState(false)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(false)
  const [createOpen, setCreateOpen] = useState(false)
  const [busyId, setBusyId] = useState<string | null>(null)
  const [archiveTarget, setArchiveTarget] = useState<PersonaSummary | null>(null)

  // Client-side filters (the API supports neither search nor a combined view).
  const [search, setSearch] = useState("")
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("all")

  const load = useCallback(async () => {
    setLoading(true)
    setLoadError(false)
    try {
      const page = await listPersonas({ pageSize: FETCH_SIZE })
      setAllPersonas(page.items)
      setTruncated(Boolean(page.nextPageToken))
    } catch {
      setLoadError(true)
      setAllPersonas([])
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
      all: allPersonas.length,
      Draft: 0,
      Active: 0,
      Inactive: 0,
      Archived: 0,
    }
    for (const p of allPersonas) if (p.status in c) c[p.status] += 1
    return c
  }, [allPersonas])

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase()
    return allPersonas.filter((p) => {
      if (statusFilter !== "all" && p.status !== statusFilter) return false
      if (q && !`${p.nameAr} ${p.nameEn}`.toLowerCase().includes(q)) return false
      return true
    })
  }, [allPersonas, search, statusFilter])

  const isFiltered = statusFilter !== "all" || search.trim() !== ""

  function clearFilters() {
    setSearch("")
    setStatusFilter("all")
  }

  // Apply a lifecycle transition: optimistic-free (await, then patch the row from the response).
  // Errors surface as a non-blocking toast with a cause+fix message (CLAUDE.md error-copy rule).
  const runTransition = useCallback(
    async (persona: PersonaSummary, target: PersonaStatus) => {
      setBusyId(persona.personaId)
      try {
        const result = await changePersonaStatus(persona.personaId, { status: target })
        setAllPersonas((prev) =>
          prev.map((p) =>
            p.personaId === persona.personaId
              ? { ...p, status: result.status, updatedAt: result.updatedAt }
              : p,
          ),
        )
        toast.success(t("persona.transitionSuccess"))
      } catch (err) {
        if (err instanceof JourneysApiError && err.status === 409) {
          // Archive blocked: the persona still has active journey bindings — unbind first.
          toast.error(t("persona.archiveBlocked", { count: persona.journeyBindingCount }))
        } else if (err instanceof JourneysApiError && err.status === 403) {
          toast.error(t("persona.transitionForbidden"))
        } else {
          toast.error(t("persona.transitionFailed"))
        }
      } finally {
        setBusyId(null)
      }
    },
    [t],
  )

  async function confirmArchive() {
    const target = archiveTarget
    if (!target) return
    setArchiveTarget(null)
    await runTransition(target, "Archived")
  }

  const personaName = (p: PersonaSummary) =>
    isArabic
      ? { primary: p.nameAr, primaryDir: "rtl" as const, secondary: p.nameEn, secondaryDir: "ltr" as const, secondaryLang: "en" }
      : { primary: p.nameEn, primaryDir: "ltr" as const, secondary: p.nameAr, secondaryDir: "rtl" as const, secondaryLang: "ar" }

  // The summary pills: Total always, then each status that has at least one persona.
  const pills = STATUS_FILTERS.filter((s) => s === "all" || counts[s] > 0)

  return (
    <div className="space-y-5 py-5">
      {/* Header */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
        <div className="min-w-0">
          <div className="flex items-center gap-2.5">
            <UsersRound className="size-7 shrink-0 text-primary" aria-hidden />
            <h1 className="text-2xl font-heading font-bold">{t("persona.moduleTitle")}</h1>
          </div>
          <p className="mt-1 text-sm text-muted-foreground">{t("persona.moduleSubtitle")}</p>
        </div>
        {canManage && (
          <Button
            className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            onClick={() => setCreateOpen(true)}
          >
            <Plus className="size-4 ms-2" aria-hidden />
            {t("persona.newPersona")}
          </Button>
        )}
      </div>

      {/* Count pills (also act as quick status filters) */}
      <div className="flex flex-wrap items-center gap-2" role="group" aria-label={t("persona.filterStatus")}>
        {pills.map((value) => {
          const active = statusFilter === value
          return (
            <button
              key={value}
              type="button"
              aria-pressed={active}
              onClick={() => setStatusFilter(value)}
              className={cn(
                "inline-flex h-7 items-center rounded-full px-3 text-xs font-medium tabular-nums transition-all",
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

      {/* Filter row: search + status */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end">
        <div className="flex-1 space-y-1.5 sm:max-w-sm">
          <label
            htmlFor="persona-search"
            className="block text-xs font-medium uppercase tracking-widest text-muted-foreground"
          >
            {t("persona.searchLabel")}
          </label>
          <div className="relative">
            <Search
              className="pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
              aria-hidden
            />
            <Input
              id="persona-search"
              type="search"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t("persona.searchPlaceholder")}
              className="ps-9"
            />
          </div>
        </div>

        <div className="flex flex-col gap-1.5 sm:w-48">
          <span className="block text-xs font-medium uppercase tracking-widest text-muted-foreground">
            {t("persona.filterStatus")}
          </span>
          <Select value={statusFilter} onValueChange={(v) => setStatusFilter((v ?? "all") as StatusFilter)}>
            <SelectTrigger className="w-full">
              <SelectValue>
                {(v) => (v === "all" || v == null ? t("persona.all") : t(STATUS_LABEL[v as PersonaStatus]))}
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">{t("persona.all")}</SelectItem>
              {(["Draft", "Active", "Inactive", "Archived"] as PersonaStatus[]).map((s) => (
                <SelectItem key={s} value={s}>
                  {t(STATUS_LABEL[s])}
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
          {t("persona.loadError")}
        </div>
      )}

      {/* Table / states */}
      <div className="overflow-hidden rounded-lg border border-border bg-card shadow-sm dark:shadow-none">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="text-start font-semibold text-foreground">{t("persona.colName")}</TableHead>
              <TableHead className="text-start font-semibold text-foreground">{t("persona.colStatus")}</TableHead>
              <TableHead className="text-start font-semibold text-foreground">{t("persona.colBindings")}</TableHead>
              <TableHead className="text-start font-semibold text-foreground">{t("persona.colLastUpdated")}</TableHead>
              {canManage && (
                <TableHead className="text-end font-semibold text-foreground">{t("persona.colActions")}</TableHead>
              )}
            </TableRow>
          </TableHeader>
          <TableBody>
            {loading ? (
              Array.from({ length: 5 }).map((_, i) => (
                <TableRow key={i}>
                  <TableCell colSpan={canManage ? 5 : 4}>
                    <Skeleton className="h-5 w-full" />
                  </TableCell>
                </TableRow>
              ))
            ) : filtered.length === 0 ? (
              <TableRow>
                <TableCell colSpan={canManage ? 5 : 4}>
                  <div className="flex flex-col items-center justify-center py-16 text-center">
                    <Inbox className="mb-4 size-12 text-muted-foreground" aria-hidden />
                    <h3 className="mb-2 text-lg font-bold">
                      {isFiltered ? t("persona.emptyFilterTitle") : t("persona.noPersonas")}
                    </h3>
                    <p className="mb-4 max-w-sm text-muted-foreground">
                      {isFiltered ? t("persona.emptyFilterHelp") : t("persona.noPersonasHelp")}
                    </p>
                    {isFiltered ? (
                      <Button variant="secondary" onClick={clearFilters}>
                        {t("persona.clearFilter")}
                      </Button>
                    ) : (
                      canManage && (
                        <Button
                          className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
                          onClick={() => setCreateOpen(true)}
                        >
                          <Plus className="size-4 ms-2" aria-hidden />
                          {t("persona.newPersona")}
                        </Button>
                      )
                    )}
                  </div>
                </TableCell>
              </TableRow>
            ) : (
              filtered.map((persona) => {
                const name = personaName(persona)
                const bound = persona.journeyBindingCount > 0
                const transitions = NEXT_TRANSITIONS[persona.status] ?? []
                return (
                  <TableRow key={persona.personaId} className="transition-colors hover:bg-muted/50">
                    <TableCell>
                      <div className="flex min-w-0 flex-col items-start">
                        <bdi dir={name.primaryDir} className="max-w-full truncate text-start font-semibold text-foreground">
                          {name.primary}
                        </bdi>
                        <bdi
                          dir={name.secondaryDir}
                          lang={name.secondaryLang}
                          className="max-w-full truncate text-start text-sm text-muted-foreground"
                        >
                          {name.secondary}
                        </bdi>
                      </div>
                    </TableCell>
                    <TableCell>
                      <PersonaStatusBadge status={persona.status} />
                    </TableCell>
                    <TableCell>
                      <span
                        className={cn(
                          "inline-flex items-center gap-1.5 text-sm tabular-nums",
                          bound ? "text-d3-dark dark:text-d3-light" : "text-muted-foreground",
                        )}
                        title={bound ? t("persona.bindingsBlockArchive") : undefined}
                      >
                        <RouteIcon className="size-3.5" aria-hidden />
                        {t("persona.bindingsCount", { count: persona.journeyBindingCount })}
                      </span>
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {formatUpdatedAt(persona.updatedAt, i18n.language)}
                    </TableCell>
                    {canManage && (
                      <TableCell className="text-end">
                        {persona.status === "Archived" ? (
                          <span className="text-xs text-muted-foreground">{t("persona.terminal")}</span>
                        ) : (
                          <DropdownMenu>
                            <DropdownMenuTrigger
                              className={cn(buttonVariants({ variant: "ghost", size: "icon-sm" }))}
                              aria-label={t("persona.colActions")}
                              disabled={busyId === persona.personaId}
                            >
                              <MoreHorizontal className="size-4" aria-hidden />
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              {transitions.map((tr) => {
                                const Icon = tr.icon
                                return (
                                  <DropdownMenuItem
                                    key={tr.target}
                                    onClick={() => void runTransition(persona, tr.target)}
                                  >
                                    <Icon className="size-4" aria-hidden />
                                    {t(tr.labelKey)}
                                  </DropdownMenuItem>
                                )
                              })}
                              <DropdownMenuSeparator />
                              <DropdownMenuItem
                                variant="destructive"
                                onClick={() => setArchiveTarget(persona)}
                              >
                                <Archive className="size-4" aria-hidden />
                                {t("persona.archive")}
                              </DropdownMenuItem>
                            </DropdownMenuContent>
                          </DropdownMenu>
                        )}
                      </TableCell>
                    )}
                  </TableRow>
                )
              })
            )}
          </TableBody>
        </Table>
      </div>

      {!loading && truncated && (
        <p className="text-xs text-muted-foreground">{t("persona.listTruncated", { count: FETCH_SIZE })}</p>
      )}

      <CreatePersonaDialog open={createOpen} onOpenChange={setCreateOpen} onCreated={() => void load()} />

      {/* Archive confirmation (terminal, destructive) */}
      <AlertDialog open={!!archiveTarget} onOpenChange={(o) => !o && setArchiveTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("persona.archiveConfirmTitle")}</AlertDialogTitle>
            <AlertDialogDescription>
              {archiveTarget && archiveTarget.journeyBindingCount > 0
                ? t("persona.archiveConfirmBlocked", { count: archiveTarget.journeyBindingCount })
                : t("persona.archiveConfirmBody")}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("persona.cancel")}</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={() => void confirmArchive()}>
              {t("persona.archive")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}
