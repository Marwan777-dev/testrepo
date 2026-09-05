// F1 Survey Library (T086): Surveys | Templates tabs (clickthrough parity — templates
// live here, the old /templates route redirects), filter row (search bounded sm:max-w-sm
// + Type/Status/Journey selects at sm:w-48), the library table in a bordered
// overflow-hidden card with a sticky bg-muted header, per-row action icons with
// tooltips (Preview / Report / Analytics) + overflow menu (Edit / Sections / Clone /
// Save as template / status transitions / Archive), FR-1.3 no-results state, and the
// D-level SurveyStatusPill. Row click deep-links to Settings (FR-1.4). One-blue rule:
// "Add Survey" is the sole filled primary. Actions confirm via sonner toasts.

import { useCallback, useEffect, useMemo, useState } from "react"
import { Link, useNavigate, useSearchParams } from "react-router"
import { useTranslation } from "react-i18next"
import { toast } from "sonner"
import {
  Archive,
  ArchiveRestore,
  ChartNoAxesColumn,
  CircleDot,
  ClipboardList,
  Copy,
  Eye,
  FileChartColumn,
  LayoutList,
  LayoutTemplate,
  MoreHorizontal,
  PencilLine,
  Plus,
  Search,
  Send,
} from "lucide-react"

import { Badge } from "@/components/ui/badge"
import { buttonVariants } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Skeleton } from "@/components/ui/skeleton"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
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
import { Tabs, TabsContent, TabsIndicator, TabsList, TabsTrigger } from "@/components/ui/tabs"
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip"
import { cn } from "@/lib/utils"
import { formatRelativeTime } from "@/lib/relative-time"
import { useSession } from "@/features/auth/hooks/useSession"
import { listJourneys, type JourneySummary } from "@/features/journeys/api"
import { SurveysApiError } from "../api/etag"
import {
  changeSurveyStatus,
  cloneSurvey,
  getSurvey,
  listSurveys,
  newIdempotencyKey,
  publishSurvey,
  type SurveyListItem,
  type SurveyStatus,
  type SurveyType,
} from "../api/surveys-api"
import { ChangeStatusDialog } from "../components/ChangeStatusDialog"
import { DestructiveReturnToDraftDialog } from "../components/DestructiveReturnToDraftDialog"
import { PauseWithRulesDialog } from "../components/PauseWithRulesDialog"
import { SaveAsTemplateDialog } from "../components/SaveAsTemplateDialog"
import { SurveyStatusPill } from "../components/SurveyStatusPill"
import { TemplateLibraryContent } from "./TemplatePickerPage"

type TypeFilter = "All" | SurveyType
type StatusFilter = "All" | SurveyStatus

/** Self-serve transitions offered per current status (BR-1.4 matrix, P-01 only).
 * Archive/Unarchive are NOT here — they render as a separate bottom section of the
 * overflow menu (archive styled destructive, per the reference). */
const TRANSITIONS: Record<SurveyStatus, { to: SurveyStatus; labelKey: string }[]> = {
  Draft: [{ to: "Active", labelKey: "surveysModule.actions.publish" }],
  PendingReview: [],
  Active: [
    { to: "Paused", labelKey: "surveysModule.actions.pause" },
    { to: "Draft", labelKey: "surveysModule.actions.returnToDraft" },
  ],
  Paused: [
    { to: "Active", labelKey: "surveysModule.actions.reactivate" },
    { to: "Draft", labelKey: "surveysModule.actions.returnToDraft" },
  ],
  Archived: [],
}

/** Success-toast key per transition target (unarchive passes its own key). */
const TOAST_BY_TARGET: Record<SurveyStatus, string> = {
  Active: "surveysModule.toasts.activated",
  Paused: "surveysModule.toasts.paused",
  Draft: "surveysModule.toasts.returnedToDraft",
  Archived: "surveysModule.toasts.archived",
  PendingReview: "surveysModule.toasts.activated",
}

function TabCountPill({ count }: { count: number | null }) {
  if (count === null) return null
  return (
    <span className="rounded-full bg-muted-foreground/15 px-1.5 py-0.5 text-xs font-medium tabular-nums text-muted-foreground in-data-active:bg-primary/10 in-data-active:text-primary">
      {count}
    </span>
  )
}

export default function SurveyLibraryPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const { session } = useSession()
  const [searchParams, setSearchParams] = useSearchParams()
  const tab = searchParams.get("tab") === "templates" ? "templates" : "surveys"

  const [items, setItems] = useState<SurveyListItem[]>([])
  const [journeys, setJourneys] = useState<JourneySummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(false)
  const [search, setSearch] = useState("")
  const [typeFilter, setTypeFilter] = useState<TypeFilter>("All")
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("All")
  const [journeyFilter, setJourneyFilter] = useState<string>("All")
  const [busyId, setBusyId] = useState<string | null>(null)
  // Unfiltered totals for the tab count pills (surveys captured on unfiltered loads,
  // templates reported up by the eagerly-mounted templates panel).
  const [surveysCount, setSurveysCount] = useState<number | null>(null)
  const [templatesCount, setTemplatesCount] = useState<number | null>(null)
  const [templateFor, setTemplateFor] = useState<SurveyListItem | null>(null)
  // Row whose "Change status" picker is open.
  const [statusFor, setStatusFor] = useState<SurveyListItem | null>(null)
  // Bumped after "Save as template" so the kept-mounted templates tab refetches.
  const [templatesRefresh, setTemplatesRefresh] = useState(0)

  // 409 confirmation flows (BR-1.6 destructive / FR-1.10 pause-with-rules).
  const [confirmState, setConfirmState] = useState<{
    kind: "destructive" | "pauseRules"
    surveyId: string
    to: SurveyStatus
    count: number
  } | null>(null)

  // Authoring is P-01/P-03; P-02/P-06 browse read-only (server enforces too).
  const canAuthor = session?.persona === "P-01" || session?.persona === "P-03"
  const canChangeStatus = session?.persona === "P-01"

  const isFiltered =
    search !== "" || typeFilter !== "All" || statusFilter !== "All" || journeyFilter !== "All"

  const load = useCallback(async () => {
    setLoading(true)
    setError(false)
    try {
      const filtered =
        search !== "" || typeFilter !== "All" || statusFilter !== "All" || journeyFilter !== "All"
      const result = await listSurveys({
        q: search || undefined,
        type: typeFilter === "All" ? undefined : [typeFilter],
        status: statusFilter === "All" ? undefined : [statusFilter],
        journeyId: journeyFilter === "All" ? undefined : journeyFilter,
      })
      setItems(result.items)
      if (!filtered) setSurveysCount(result.totalCount)
    } catch {
      setError(true)
    } finally {
      setLoading(false)
    }
  }, [search, typeFilter, statusFilter, journeyFilter])

  // FR-1.2 real-time search — debounce keystrokes before hitting the API.
  useEffect(() => {
    const handle = window.setTimeout(() => {
      void load()
    }, 300)
    return () => window.clearTimeout(handle)
  }, [load])

  useEffect(() => {
    listJourneys({ pageSize: 200 })
      .then((r) => setJourneys(r.items))
      .catch(() => setJourneys([]))
    // Returning to the library ends any create journey — subsequent edit visits
    // show the wizard without the Build method segment.
    sessionStorage.removeItem("surveys.createFlow")
  }, [])

  const journeyName = useMemo(() => {
    const map = new Map(journeys.map((j) => [j.journeyId, j.name]))
    return (id: string | null) => (id ? (map.get(id) ?? "—") : "—")
  }, [journeys])

  const surveyName = useCallback(
    (surveyId: string) => items.find((s) => s.id === surveyId)?.nameEn ?? "",
    [items]
  )

  const doStatusChange = useCallback(
    async (surveyId: string, to: SurveyStatus, confirm = false, toastKey?: string) => {
      setBusyId(surveyId)
      try {
        // The list has no ETag — fetch the survey to get a fresh one (Q1).
        const { etag } = await getSurvey(surveyId)
        const needsKey = confirm || to === "Draft"
        await changeSurveyStatus(
          surveyId,
          { to, confirm },
          etag ?? undefined,
          needsKey ? newIdempotencyKey() : undefined
        )
        setConfirmState(null)
        toast.success(t(toastKey ?? TOAST_BY_TARGET[to], { name: surveyName(surveyId) }))
        await load()
      } catch (err) {
        if (err instanceof SurveysApiError) {
          const details = (err.details ?? {}) as Record<string, unknown>
          if (err.code === "survey.return_to_draft.destructive_confirmation_required") {
            setConfirmState({
              kind: "destructive",
              surveyId,
              to,
              count: Number(details.responsesCount ?? details.responses_count ?? 0),
            })
            return
          }
          if (err.code === "survey.pause.requires_rules_confirmation") {
            setConfirmState({
              kind: "pauseRules",
              surveyId,
              to,
              count: Number(details.rulesCount ?? details.rules_count ?? 0),
            })
            return
          }
        }
        setError(true)
      } finally {
        setBusyId(null)
      }
    },
    [load, surveyName, t]
  )

  // US2 (T123): P-01 quick-publish for PendingReview rows — the lifecycle endpoint,
  // not POST /status (PendingReview → Active goes through the approval workflow).
  const doPublishReviewed = useCallback(
    async (surveyId: string) => {
      setBusyId(surveyId)
      try {
        const { etag } = await getSurvey(surveyId)
        await publishSurvey(surveyId, undefined, etag ?? undefined, newIdempotencyKey())
        toast.success(t("surveysModule.toasts.published", { name: surveyName(surveyId) }))
        await load()
      } catch {
        setError(true)
      } finally {
        setBusyId(null)
      }
    },
    [load, surveyName, t]
  )

  const doClone = useCallback(
    async (surveyId: string) => {
      setBusyId(surveyId)
      try {
        // Reference flow: clone then open the copy's Survey details directly.
        const { data } = await cloneSurvey(surveyId, newIdempotencyKey())
        toast.success(t("surveysModule.toasts.cloned", { name: surveyName(surveyId) }))
        navigate(`/surveys/${data.id}/settings`)
      } catch {
        setError(true)
        setBusyId(null)
      }
    },
    [navigate, surveyName, t]
  )

  return (
    <div className="space-y-5 py-5">
      {/* Header */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
        <div className="min-w-0">
          <div className="flex items-center gap-2.5">
            <ClipboardList className="size-7 shrink-0 text-primary" aria-hidden />
            <h1 className="text-2xl font-heading font-bold">
              {t("surveysModule.libraryTitle")}
            </h1>
          </div>
          <p className="mt-1 text-sm text-muted-foreground">
            {t("surveysModule.librarySubtitle")}
          </p>
        </div>
        {canAuthor && (
          <Link
            to="/surveys/new"
            className={cn(buttonVariants(), "bg-primary hover:bg-nb-cyan-700 text-primary-foreground")}
          >
            <Plus className="size-4" aria-hidden />
            {t("surveysModule.addSurvey")}
          </Link>
        )}
      </div>

      <Tabs
        value={tab}
        onValueChange={(next) =>
          setSearchParams(next === "templates" ? { tab: "templates" } : {}, { replace: true })
        }
        className="gap-5"
      >
        {/* variant="line" keeps the triggers' own active bg transparent so the
            TabsIndicator's sliding pill is the single moving highlight. */}
        <TabsList
          variant="line"
          // The modifier-prefixed overrides are required: the base list class pins
          // h-8 (via group-data-horizontal) and rounded-none (via data-[variant=line]),
          // which outrank the plain utilities and squish the trigger padding.
          className="h-auto gap-1 rounded-lg border border-border bg-muted p-1 group-data-horizontal/tabs:h-auto data-[variant=line]:rounded-lg"
        >
          <TabsIndicator />
          <TabsTrigger value="surveys" className="gap-1.5 px-3.5 py-1.5 after:hidden">
            {t("surveysModule.library.tabSurveys")}
            <TabCountPill count={surveysCount} />
          </TabsTrigger>
          <TabsTrigger value="templates" className="gap-1.5 px-3.5 py-1.5 after:hidden">
            {t("surveysModule.library.tabTemplates")}
            <TabCountPill count={templatesCount} />
          </TabsTrigger>
        </TabsList>

        {/* Panels re-run the enter animation each time `hidden` toggles off, giving a
            soft fade/slide when swiping between tabs. */}
        <TabsContent
          value="surveys"
          className="space-y-5 fade-in-0 slide-in-from-bottom-2 motion-safe:animate-in"
        >
          {/* Filter row — label-less toolbar per the reference: search fills the row,
              selects show their filter name as muted placeholder text. Accessible
              names come from aria-label (visible labels intentionally dropped for
              clickthrough parity). */}
          <div className="flex flex-wrap items-center gap-3">
            <div className="relative min-w-48 flex-1">
              <Search
                className="pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
                aria-hidden
              />
              <Input
                id="survey-search"
                type="search"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder={t("surveysModule.library.searchPlaceholder")}
                aria-label={t("surveysModule.library.searchLabel")}
                className="ps-9"
              />
            </div>

            <Select value={typeFilter} onValueChange={(v) => setTypeFilter((v ?? "All") as TypeFilter)}>
              <SelectTrigger className="w-40" aria-label={t("surveysModule.library.typeFilter")}>
                <SelectValue>
                  {(v) =>
                    v === "All" || !v ? (
                      <span className="text-muted-foreground">
                        {t("surveysModule.library.typeFilter")}
                      </span>
                    ) : (
                      t(`surveysModule.type.${String(v)}`)
                    )
                  }
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">{t("surveysModule.library.allTypes")}</SelectItem>
                <SelectItem value="Transactional">{t("surveysModule.type.Transactional")}</SelectItem>
                <SelectItem value="SeasonalRelational">
                  {t("surveysModule.type.SeasonalRelational")}
                </SelectItem>
              </SelectContent>
            </Select>

            <Select
              value={statusFilter}
              onValueChange={(v) => setStatusFilter((v ?? "All") as StatusFilter)}
            >
              <SelectTrigger className="w-40" aria-label={t("surveysModule.library.statusFilter")}>
                <SelectValue>
                  {(v) =>
                    v === "All" || !v ? (
                      <span className="text-muted-foreground">
                        {t("surveysModule.library.statusFilter")}
                      </span>
                    ) : (
                      t(`surveysModule.status.${String(v)[0].toLowerCase()}${String(v).slice(1)}`)
                    )
                  }
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">{t("surveysModule.library.allStatuses")}</SelectItem>
                {(["Draft", "PendingReview", "Active", "Paused", "Archived"] as const).map((s) => (
                  <SelectItem key={s} value={s}>
                    {t(`surveysModule.status.${s[0].toLowerCase()}${s.slice(1)}`)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            <Select value={journeyFilter} onValueChange={(v) => setJourneyFilter(v ?? "All")}>
              <SelectTrigger className="w-52" aria-label={t("surveysModule.library.journeyFilter")}>
                <SelectValue>
                  {(v) =>
                    v === "All" || !v ? (
                      <span className="text-muted-foreground">
                        {t("surveysModule.library.journeyFilter")}
                      </span>
                    ) : (
                      journeyName(String(v))
                    )
                  }
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">{t("surveysModule.library.allJourneys")}</SelectItem>
                {journeys.map((j) => (
                  <SelectItem key={j.journeyId} value={j.journeyId}>
                    {j.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {error && (
            <div
              role="alert"
              className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
            >
              {t("surveysModule.library.loadError")}
            </div>
          )}

          {loading ? (
            <div className="space-y-3 rounded-lg border border-border bg-card p-4 shadow-sm dark:shadow-none">
              {Array.from({ length: 6 }).map((_, i) => (
                <Skeleton key={i} className="h-8 w-full" />
              ))}
            </div>
          ) : items.length === 0 ? (
            <div className="flex flex-col items-center justify-center rounded-lg border border-border bg-card py-16 text-center shadow-sm dark:shadow-none">
              <ClipboardList className="mb-4 size-12 text-muted-foreground" aria-hidden />
              <h3 className="mb-2 text-lg font-bold">
                {isFiltered ? t("surveysModule.library.noResults") : t("surveysModule.library.empty")}
              </h3>
              <p className="mb-4 max-w-sm text-muted-foreground">
                {isFiltered
                  ? t("surveysModule.library.noResultsHelp")
                  : t("surveysModule.library.emptyHelp")}
              </p>
              {!isFiltered && canAuthor && (
                <Link
                  to="/surveys/new"
                  className={cn(buttonVariants(), "bg-primary hover:bg-nb-cyan-700 text-primary-foreground")}
                >
                  <Plus className="size-4" aria-hidden />
                  {t("surveysModule.addSurvey")}
                </Link>
              )}
            </div>
          ) : (
            <>
              <div className="overflow-hidden rounded-lg border border-border bg-card shadow-sm dark:shadow-none">
                {/* Roomier cells than the p-2 component default — matches the
                    reference table's rhythm. */}
                <Table className="[&_td]:px-4 [&_td]:py-2.5 [&_th]:h-10 [&_th]:px-4 [&_th]:font-semibold">
                  <TableHeader className="sticky top-0 z-10">
                    {/* Explicit column widths mirror the reference's proportions —
                        with sparse dev data, auto layout would let short columns
                        collapse and drift from the clickthrough. */}
                    <TableRow>
                      <TableHead>{t("surveysModule.library.colName")}</TableHead>
                      <TableHead className="w-44">{t("surveysModule.library.colType")}</TableHead>
                      <TableHead className="w-52">{t("surveysModule.library.colJourney")}</TableHead>
                      <TableHead className="w-28 tabular-nums">
                        {t("surveysModule.library.colResponses")}
                      </TableHead>
                      <TableHead className="w-32">{t("surveysModule.library.colStatus")}</TableHead>
                      <TableHead className="w-36">{t("surveysModule.library.colUpdated")}</TableHead>
                      <TableHead className="w-36">
                        <span className="sr-only">{t("surveysModule.library.colActions")}</span>
                      </TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {items.map((row) => (
                      <TableRow
                        key={row.id}
                        className="cursor-pointer hover:bg-muted/50 transition-colors"
                        onClick={() => navigate(`/surveys/${row.id}/settings`)}
                      >
                        <TableCell className="font-medium">{row.nameEn}</TableCell>
                        <TableCell>
                          {/* Type is a category, not a status → neutral outline chip. */}
                          <Badge variant="outline" className="text-xs font-medium text-muted-foreground">
                            {t(`surveysModule.type.${row.surveyType}`)}
                          </Badge>
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {journeyName(row.boundJourneyId)}
                        </TableCell>
                        {/* Response counts aren't on the list wire yet (no ES
                            pipeline in dev) — render "—" until the backend adds it. */}
                        <TableCell className="tabular-nums font-medium text-muted-foreground">
                          —
                        </TableCell>
                        <TableCell>
                          <SurveyStatusPill status={row.status} />
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {formatRelativeTime(row.updatedAt, i18n.language)}
                        </TableCell>
                        <TableCell className="text-end" onClick={(e) => e.stopPropagation()}>
                          <div className="flex items-center justify-end gap-1">
                            {/* Link-styled actions use buttonVariants on the <Link> itself —
                                Base UI's <Button> is a native <button> and warns when its
                                render prop swaps in an <a> (repo pattern: KpiManagementPage). */}
                            <TooltipProvider>
                              <Tooltip>
                                <TooltipTrigger
                                  render={
                                    <Link
                                      to={`/surveys/${row.id}/appearance?preview=1`}
                                      aria-label={t("surveysModule.library.previewSurvey")}
                                      className={cn(buttonVariants({ variant: "ghost", size: "icon-sm" }), "text-muted-foreground hover:text-foreground")}
                                    />
                                  }
                                >
                                  <Eye className="size-4" aria-hidden />
                                </TooltipTrigger>
                                <TooltipContent>{t("surveysModule.library.previewSurvey")}</TooltipContent>
                              </Tooltip>
                              <Tooltip>
                                <TooltipTrigger
                                  render={
                                    <Link
                                      to={`/surveys/${row.id}/report`}
                                      aria-label={t("surveysModule.library.report")}
                                      className={cn(buttonVariants({ variant: "ghost", size: "icon-sm" }), "text-muted-foreground hover:text-foreground")}
                                    />
                                  }
                                >
                                  <FileChartColumn className="size-4" aria-hidden />
                                </TooltipTrigger>
                                <TooltipContent>{t("surveysModule.library.report")}</TooltipContent>
                              </Tooltip>
                              <Tooltip>
                                <TooltipTrigger
                                  render={
                                    <Link
                                      to={`/surveys/${row.id}/analytics`}
                                      aria-label={t("surveysModule.library.analytics")}
                                      className={cn(buttonVariants({ variant: "ghost", size: "icon-sm" }), "text-muted-foreground hover:text-foreground")}
                                    />
                                  }
                                >
                                  <ChartNoAxesColumn className="size-4" aria-hidden />
                                </TooltipTrigger>
                                <TooltipContent>{t("surveysModule.library.analytics")}</TooltipContent>
                              </Tooltip>
                            </TooltipProvider>
                            <DropdownMenu>
                              <DropdownMenuTrigger
                                className={cn(buttonVariants({ variant: "ghost", size: "icon-sm" }), "text-muted-foreground hover:text-foreground")}
                                aria-label={t("surveysModule.library.colActions")}
                              >
                                <MoreHorizontal className="size-4" aria-hidden />
                              </DropdownMenuTrigger>
                              <DropdownMenuContent align="end">
                                <DropdownMenuItem render={<Link to={`/surveys/${row.id}/settings`} />}>
                                  <PencilLine className="size-4" aria-hidden />
                                  {t("common.edit")}
                                </DropdownMenuItem>
                                <DropdownMenuItem render={<Link to={`/surveys/${row.id}/builder`} />}>
                                  <LayoutList className="size-4" aria-hidden />
                                  {t("surveysModule.library.sections")}
                                </DropdownMenuItem>
                                {canAuthor && (
                                  <DropdownMenuItem
                                    onClick={() => void doClone(row.id)}
                                    disabled={busyId === row.id}
                                  >
                                    <Copy className="size-4" aria-hidden />
                                    {t("surveysModule.actions.clone")}
                                  </DropdownMenuItem>
                                )}
                                {canAuthor && (
                                  <DropdownMenuItem onClick={() => setTemplateFor(row)}>
                                    <LayoutTemplate className="size-4" aria-hidden />
                                    {t("surveysModule.library.saveAsTemplate")}
                                  </DropdownMenuItem>
                                )}
                                {canChangeStatus && row.status === "PendingReview" && (
                                  <>
                                    <DropdownMenuSeparator />
                                    <DropdownMenuItem
                                      onClick={() => void doPublishReviewed(row.id)}
                                      disabled={busyId === row.id}
                                    >
                                      <Send className="size-4" aria-hidden />
                                      {t("surveysModule.actions.publish")}
                                    </DropdownMenuItem>
                                  </>
                                )}
                                {canChangeStatus && TRANSITIONS[row.status].length > 0 && (
                                  <>
                                    <DropdownMenuSeparator />
                                    <DropdownMenuItem
                                      onClick={() => setStatusFor(row)}
                                      disabled={busyId === row.id}
                                    >
                                      <CircleDot className="size-4" aria-hidden />
                                      {t("surveysModule.actions.changeStatus")}
                                    </DropdownMenuItem>
                                  </>
                                )}
                                {canChangeStatus && (
                                  <>
                                    <DropdownMenuSeparator />
                                    {row.status === "Archived" ? (
                                      <DropdownMenuItem
                                        onClick={() =>
                                          void doStatusChange(
                                            row.id,
                                            "Draft",
                                            false,
                                            "surveysModule.toasts.unarchived"
                                          )
                                        }
                                        disabled={busyId === row.id}
                                      >
                                        <ArchiveRestore className="size-4" aria-hidden />
                                        {t("surveysModule.actions.unarchive")}
                                      </DropdownMenuItem>
                                    ) : (
                                      <DropdownMenuItem
                                        variant="destructive"
                                        onClick={() => void doStatusChange(row.id, "Archived")}
                                        disabled={busyId === row.id}
                                      >
                                        <Archive className="size-4" aria-hidden />
                                        {t("surveysModule.actions.archive")}
                                      </DropdownMenuItem>
                                    )}
                                  </>
                                )}
                              </DropdownMenuContent>
                            </DropdownMenu>
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
              <p className="text-xs text-muted-foreground">
                {t("surveysModule.library.showing", {
                  shown: items.length,
                  total: surveysCount ?? items.length,
                })}
              </p>
            </>
          )}
        </TabsContent>

        {/* keepMounted: the panel must exist (hidden) from first paint so the
            templates fetch runs and the tab's count pill shows before any click. */}
        <TabsContent
          value="templates"
          keepMounted
          className="space-y-5 fade-in-0 slide-in-from-bottom-2 motion-safe:animate-in"
        >
          <TemplateLibraryContent onCountChange={setTemplatesCount} refreshToken={templatesRefresh} />
        </TabsContent>
      </Tabs>

      <ChangeStatusDialog
        survey={
          statusFor
            ? { id: statusFor.id, status: statusFor.status, name: surveyName(statusFor.id) }
            : null
        }
        validTargets={statusFor ? TRANSITIONS[statusFor.status].map((tr) => tr.to) : []}
        onClose={() => setStatusFor(null)}
        onPick={(target) => {
          const row = statusFor
          setStatusFor(null)
          if (row) void doStatusChange(row.id, target, false, TOAST_BY_TARGET[target])
        }}
      />
      <DestructiveReturnToDraftDialog
        open={confirmState?.kind === "destructive"}
        responsesCount={confirmState?.count ?? 0}
        busy={busyId !== null}
        onConfirm={() =>
          confirmState && void doStatusChange(confirmState.surveyId, confirmState.to, true)
        }
        onCancel={() => setConfirmState(null)}
      />
      <PauseWithRulesDialog
        open={confirmState?.kind === "pauseRules"}
        rulesCount={confirmState?.count ?? 0}
        busy={busyId !== null}
        onConfirm={() =>
          confirmState && void doStatusChange(confirmState.surveyId, confirmState.to, true)
        }
        onCancel={() => setConfirmState(null)}
      />
      <SaveAsTemplateDialog
        survey={templateFor}
        onClose={() => setTemplateFor(null)}
        onSaved={() => {
          setTemplateFor(null)
          toast.success(t("surveysModule.toasts.templateSaved"))
          setTemplatesRefresh((n) => n + 1)
          setSearchParams({ tab: "templates" }, { replace: true })
        }}
      />
    </div>
  )
}
