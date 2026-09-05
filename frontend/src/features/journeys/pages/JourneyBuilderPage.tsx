// Journey Builder page (T038, US-1): the editing surface for a single journey. Loads the full
// journey tree (`GET /journeys/{id}`) and renders it as a vertical accordion of stage rows, each
// expanding to reveal its ordered touchpoints. Supports adding / editing / deleting stages and
// touchpoints, reordering stages with move-earlier / move-later controls, journey lifecycle
// status transitions (P-01-gated server-side), and a concurrent-edit banner. Archived journeys
// render read-only. RTL-first, design-system styled; reuses `StageAccordionItem` / `TouchpointRow`
// (T036) and the journeys API client (T035).
//
// Concurrent-edit detection lives in the `useJourneyUpdated` hook (T039): a 15 s poll of
// `GET /journeys/{id}/updated-at` compares against the journey's loaded `updatedAt` (re-baselined
// after each local mutation), fires a non-blocking `sonner` toast on divergence, and exposes a
// `changedExternally` flag that drives the dismissible banner below.

import { useCallback, useEffect, useRef, useState } from "react"
import { Link, useParams } from "react-router"
import { useTranslation } from "react-i18next"
import {
  AlertTriangle,
  Archive,
  ChevronDown,
  ChevronLeft,
  ChevronUp,
  History,
  Layers,
  Lock,
  MoreHorizontal,
  Pause,
  PencilLine,
  Play,
  Plus,
  SlidersHorizontal,
  Target,
  Trash2,
  UserPlus,
  UsersRound,
  X,
} from "lucide-react"

import { Badge } from "@/components/ui/badge"
import { Button, buttonVariants } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
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
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { cn } from "@/lib/utils"
import {
  changeJourneyStatus,
  deleteStage,
  deleteTouchpoint,
  getJourney,
  JourneysApiError,
  listPersonas,
  reorderStages,
  updateJourney,
  type JourneyDetail,
  type JourneyStatus,
  type PersonaBinding,
  type PersonaSummary,
  type StageDetail,
  type TouchpointDetail,
} from "@/features/journeys/api"
import { JourneyStatusBadge } from "@/features/journeys/components/JourneyStatusBadge"
import { JourneyFormDialog } from "@/features/journeys/components/JourneyFormDialog"
import { StageAccordionItem } from "@/features/journeys/components/StageAccordionItem"
import { TouchpointRow } from "@/features/journeys/components/TouchpointRow"
import { StageFormDialog } from "@/features/journeys/components/StageFormDialog"
import { TouchpointFormDialog } from "@/features/journeys/components/TouchpointFormDialog"
import { useJourneyUpdated } from "@/features/journeys/hooks/useJourneyUpdated"

// Localize the known journey-type archetypes; fall back to the raw key (free-form on the wire).
const TYPE_LABEL: Record<string, string> = {
  Transactional: "journey.typeTransactional",
  Lifecycle: "journey.typeLifecycle",
  IssueResolution: "journey.typeIssueResolution",
  Onboarding: "journey.typeOnboarding",
}

/** Formats an ISO-8601 timestamp for the active locale, keeping Western digits in Arabic. */
function formatUpdatedAt(iso: string, lang: string): string {
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return "—"
  const locale = lang.startsWith("ar") ? "ar-u-nu-latn" : "en"
  return new Intl.DateTimeFormat(locale, { year: "numeric", month: "short", day: "numeric" }).format(
    date,
  )
}

export default function JourneyBuilderPage() {
  const { t, i18n } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const journeyId = id ?? ""

  const [journey, setJourney] = useState<JourneyDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)
  const [statusBusy, setStatusBusy] = useState(false)

  // Concurrent-edit awareness (T039): polls `updated-at`, toasts + flags external changes.
  const { changedExternally, reset: dismissConcurrentEdit } = useJourneyUpdated({
    journeyId,
    baselineUpdatedAt: journey?.updatedAt ?? null,
  })

  // Dialog / confirmation state.
  const [stageDialog, setStageDialog] = useState<{ open: boolean; stage?: StageDetail }>({
    open: false,
  })
  const [tpDialog, setTpDialog] = useState<{
    open: boolean
    stageId: string
    touchpoint?: TouchpointDetail
  }>({ open: false, stageId: "" })
  const [editDetailsOpen, setEditDetailsOpen] = useState(false)
  const [deleteStageTarget, setDeleteStageTarget] = useState<StageDetail | null>(null)
  const [deleteTpTarget, setDeleteTpTarget] = useState<{
    stageId: string
    touchpoint: TouchpointDetail
  } | null>(null)
  const [archiveOpen, setArchiveOpen] = useState(false)

  // Persona binding (T078, US-3). The binding selector offers ONLY Active personas (FR-005), loaded
  // once via `GET /personas?status=Active`. Bindings are persisted as the full replacement set on
  // `PUT /journeys/{id}` (`personaIds`, per the journeys-API contract).
  const [activePersonas, setActivePersonas] = useState<PersonaSummary[]>([])
  const [personaSaving, setPersonaSaving] = useState(false)
  const [personaError, setPersonaError] = useState<string | null>(null)

  // Accordion expand state — a Set of expanded stage IDs. On first load the first stage opens
  // so the page lands on visible content rather than an all-collapsed wall of rows.
  const [expandedStages, setExpandedStages] = useState<Set<string>>(new Set())
  const expandInit = useRef(false)

  useEffect(() => {
    if (journey && !expandInit.current) {
      expandInit.current = true
      const first = journey.stages[0]
      if (first) setExpandedStages(new Set([first.stageId]))
    }
  }, [journey])

  const toggleStage = useCallback((stageId: string) => {
    setExpandedStages((prev) => {
      const next = new Set(prev)
      if (next.has(stageId)) next.delete(stageId)
      else next.add(stageId)
      return next
    })
  }, [])

  // Loads the journey tree. `showSkeleton` drives the initial full-page loading state; a
  // post-mutation refetch reloads silently, keeping the current content on screen.
  const load = useCallback(
    async (showSkeleton = false) => {
      if (!journeyId) return
      if (showSkeleton) setLoading(true)
      try {
        const detail = await getJourney(journeyId)
        // Updating `journey` advances the hook's baseline (journey.updatedAt), which re-arms the
        // concurrent-edit notification and clears any active banner.
        setJourney(detail)
        setLoadError(false)
      } catch {
        if (showSkeleton) setLoadError(true)
        else setActionError(t("journey.builderLoadError"))
      } finally {
        if (showSkeleton) setLoading(false)
      }
    },
    [journeyId, t],
  )

  useEffect(() => {
    void load(true)
  }, [load])

  // Load the bindable persona set (Active only — FR-005) once; the picker excludes already-bound
  // ones. A failure leaves the list empty (the picker shows its empty state) rather than blocking
  // the builder, which works fine without persona binding.
  useEffect(() => {
    let cancelled = false
    listPersonas({ status: "Active", pageSize: 200 })
      .then((page) => {
        if (!cancelled) setActivePersonas(page.items)
      })
      .catch(() => {
        if (!cancelled) setActivePersonas([])
      })
    return () => {
      cancelled = true
    }
  }, [])

  const readOnly = journey?.status === "Archived"

  // Persist a new full set of bound persona IDs via `PUT /journeys/{id}`. Optimistically patches the
  // local tree (incl. the response `updatedAt`, which re-baselines the concurrent-edit poll so a
  // self-save doesn't trip the "changed externally" banner).
  const saveBindings = useCallback(
    async (personaIds: string[], optimisticBindings: PersonaBinding[]) => {
      if (!journey) return
      setPersonaError(null)
      setPersonaSaving(true)
      try {
        const res = await updateJourney(journeyId, {
          name: journey.name,
          description: journey.description ?? null,
          journeyType: journey.journeyType,
          personaIds,
        })
        setJourney((prev) =>
          prev ? { ...prev, personaBindings: optimisticBindings, updatedAt: res.updatedAt } : prev,
        )
      } catch (err) {
        if (err instanceof JourneysApiError && err.status === 403)
          setPersonaError(t("journey.archivedImmutable"))
        else if (err instanceof JourneysApiError && err.code === "journey.invalid_persona")
          setPersonaError(t("journey.personaNotActive"))
        else setPersonaError(t("journey.personaSaveFailed"))
      } finally {
        setPersonaSaving(false)
      }
    },
    [journey, journeyId, t],
  )

  function bindPersona(persona: PersonaSummary) {
    if (!journey) return
    const ids = [...journey.personaBindings.map((b) => b.personaId), persona.personaId]
    const bindings: PersonaBinding[] = [
      ...journey.personaBindings,
      { personaId: persona.personaId, nameAr: persona.nameAr, nameEn: persona.nameEn },
    ]
    void saveBindings(ids, bindings)
  }

  function unbindPersona(personaId: string) {
    if (!journey) return
    const remaining = journey.personaBindings.filter((b) => b.personaId !== personaId)
    void saveBindings(
      remaining.map((b) => b.personaId),
      remaining,
    )
  }

  async function changeStatus(status: JourneyStatus) {
    setActionError(null)
    setStatusBusy(true)
    try {
      await changeJourneyStatus(journeyId, { status })
      await load(false)
    } catch (err) {
      if (err instanceof JourneysApiError) {
        if (err.status === 403) setActionError(t("journey.statusForbidden"))
        else if (err.code === "journey.archive_blocked_active_surveys")
          setActionError(t("journey.archiveBlocked"))
        else if (err.code === "journey.invalid_transition" || err.code === "journey.archived_terminal")
          setActionError(t("journey.statusInvalidTransition"))
        else setActionError(t("journey.statusChangeFailed"))
      } else {
        setActionError(t("journey.statusChangeFailed"))
      }
    } finally {
      setStatusBusy(false)
      setArchiveOpen(false)
    }
  }

  // Applies a new full stage ordering: updates the UI optimistically (so the rows + their
  // `N.0` labels resequence instantly), persists via `reorderStages`, then reloads to re-sync.
  // On failure the reload restores the server's order and an error banner explains why.
  const applyReorder = useCallback(
    async (stageIds: string[]) => {
      setActionError(null)
      setJourney((prev) => {
        if (!prev) return prev
        const byId = new Map(prev.stages.map((s) => [s.stageId, s]))
        const stages = stageIds
          .map((id, i) => {
            const s = byId.get(id)
            return s ? { ...s, sequenceNumber: i + 1 } : null
          })
          .filter((s): s is StageDetail => s !== null)
        return { ...prev, stages }
      })
      try {
        await reorderStages(journeyId, { stageIds })
        await load(false)
      } catch {
        setActionError(t("journey.reorderFailed"))
        await load(false)
      }
    },
    [journeyId, load, t],
  )

  function moveStage(index: number, direction: -1 | 1) {
    if (!journey) return
    const target = index + direction
    if (target < 0 || target >= journey.stages.length) return
    const stageIds = journey.stages.map((s) => s.stageId)
    ;[stageIds[index], stageIds[target]] = [stageIds[target], stageIds[index]]
    void applyReorder(stageIds)
  }

  // ── Drag-to-reorder (grip handle) ─────────────────────────────────────────────
  const [dragIndex, setDragIndex] = useState<number | null>(null)
  const [overIndex, setOverIndex] = useState<number | null>(null)
  const [overAfter, setOverAfter] = useState(false)

  const resetDrag = useCallback(() => {
    setDragIndex(null)
    setOverIndex(null)
    setOverAfter(false)
  }, [])

  function handleReorderDrop() {
    if (!journey || dragIndex === null || overIndex === null || dragIndex === overIndex) {
      resetDrag()
      return
    }
    const ids = journey.stages.map((s) => s.stageId)
    const moved = ids[dragIndex]
    const without = ids.filter((_, i) => i !== dragIndex)
    const insertAt = without.indexOf(ids[overIndex]) + (overAfter ? 1 : 0)
    without.splice(insertAt, 0, moved)
    resetDrag()
    if (without.every((id, i) => id === ids[i])) return // dropped back into place — no-op
    void applyReorder(without)
  }

  async function confirmDeleteStage(stage: StageDetail) {
    setActionError(null)
    try {
      await deleteStage(journeyId, stage.stageId)
      await load(false)
    } catch (err) {
      if (err instanceof JourneysApiError && err.code === "journey.stage_has_touchpoints")
        setActionError(t("journey.stageHasTouchpoints"))
      else if (err instanceof JourneysApiError && err.status === 403)
        setActionError(t("journey.archivedImmutable"))
      else setActionError(t("journey.deleteStageFailed"))
    } finally {
      setDeleteStageTarget(null)
    }
  }

  async function confirmDeleteTouchpoint(touchpoint: TouchpointDetail) {
    setActionError(null)
    try {
      await deleteTouchpoint(touchpoint.touchpointId)
      await load(false)
    } catch (err) {
      if (err instanceof JourneysApiError && err.status === 403)
        setActionError(t("journey.archivedImmutable"))
      else setActionError(t("journey.deleteTouchpointFailed"))
    } finally {
      setDeleteTpTarget(null)
    }
  }

  // ── Render states ───────────────────────────────────────────────────────────

  const backLink = (
    <Link
      to="/journeys"
      className="inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground"
    >
      <ChevronLeft className="size-4 rtl:rotate-180" aria-hidden />
      {t("journey.backToList")}
    </Link>
  )

  if (loading) {
    return (
      <div className="space-y-5 py-5">
        {backLink}
        <Skeleton className="h-24 w-full rounded-lg" />
        <div className="space-y-3">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-16 w-full rounded-lg" />
          ))}
        </div>
      </div>
    )
  }

  if (loadError || !journey) {
    return (
      <div className="space-y-5 py-5">
        {backLink}
        <div className="flex flex-col items-center justify-center rounded-lg border border-border bg-card py-16 text-center shadow-sm dark:shadow-none">
          <AlertTriangle className="mb-4 size-12 text-muted-foreground" aria-hidden />
          <h3 className="mb-2 text-lg font-bold">{t("journey.builderLoadErrorTitle")}</h3>
          <p className="mb-4 max-w-sm text-muted-foreground">{t("journey.builderLoadError")}</p>
          <Button variant="secondary" onClick={() => void load(true)}>
            {t("journey.retry")}
          </Button>
        </div>
      </div>
    )
  }

  const typeLabel = TYPE_LABEL[journey.journeyType]
    ? t(TYPE_LABEL[journey.journeyType])
    : journey.journeyType
  const touchpointCount = journey.stages.reduce((sum, s) => sum + s.touchpoints.length, 0)
  const lastStageIndex = journey.stages.length - 1

  // Bindable personas = Active personas not already bound (the picker filters these in; FR-005).
  const boundPersonaIds = new Set(journey.personaBindings.map((b) => b.personaId))
  const selectablePersonas = activePersonas.filter((p) => !boundPersonaIds.has(p.personaId))
  const personaLabel = (p: { nameAr: string; nameEn: string }) =>
    i18n.language.startsWith("ar") ? p.nameAr : p.nameEn

  return (
    <div className="space-y-5 py-5">
      {backLink}

      {/* Header card */}
      <Card>
        <CardContent>
          <div className="flex flex-col gap-4">
            <div className="min-w-0 space-y-1.5">
              <div className="flex flex-wrap items-center gap-2.5">
                <h1 className="min-w-0 truncate text-2xl font-heading font-bold">{journey.name}</h1>
                <JourneyStatusBadge status={journey.status} />
                <Badge
                  variant="outline"
                  className="border-nb-cyan-200 bg-nb-cyan-100 text-nb-cyan-800 dark:border-nb-cyan-900/60 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200"
                >
                  {typeLabel}
                </Badge>
              </div>
              {journey.description && (
                <p className="text-sm leading-relaxed text-muted-foreground">{journey.description}</p>
              )}
              <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted-foreground">
                <span className="whitespace-nowrap">
                  {t("journey.summaryStagesTps", {
                    stages: journey.stages.length,
                    tps: touchpointCount,
                  })}
                </span>
                <span aria-hidden>·</span>
                <span className="whitespace-nowrap">{t("journey.lastUpdated", { date: formatUpdatedAt(journey.updatedAt, i18n.language) })}</span>
              </div>
            </div>

            {/* Action toolbar — its own full-width row so the title/meta above never gets
                squeezed. Buttons wrap gracefully; the lone filled primary ("Add Stage")
                is pushed to the end so it reads as the terminal action. */}
            <div className="flex flex-wrap items-center gap-2 border-t border-border pt-4">
              {/* Edit the journey's own details (name / type / description). Secondary action —
                  hidden when Archived (read-only). The one filled primary on this header stays
                  "Add Stage" per the one-blue rule. */}
              {!readOnly && (
                <Button variant="secondary" onClick={() => setEditDetailsOpen(true)}>
                  <PencilLine className="size-4" aria-hidden />
                  {t("journey.editDetails")}
                </Button>
              )}
              {/* Secondary action — KPI & Scoring config sub-page (available read-only too). */}
              <Link
                to={`/journeys/${journeyId}/scoring`}
                className={cn(buttonVariants({ variant: "secondary" }))}
              >
                <SlidersHorizontal className="size-4" aria-hidden />
                {t("journey.openScoring")}
              </Link>
              <Link
                to={`/journeys/${journeyId}/detection`}
                className={cn(buttonVariants({ variant: "secondary" }))}
              >
                <Target className="size-4" aria-hidden />
                {t("journey.openDetection")}
              </Link>
              <Link
                to={`/journeys/${journeyId}/versions`}
                className={cn(buttonVariants({ variant: "secondary" }))}
              >
                <History className="size-4" aria-hidden />
                {t("journey.openVersions")}
              </Link>
              {readOnly ? (
                <span className="inline-flex items-center gap-1.5 text-sm text-muted-foreground">
                  <Lock className="size-4" aria-hidden />
                  {t("journey.readOnlyArchived")}
                </span>
              ) : (
                <>
                  {journey.status === "Draft" && (
                    <Button variant="secondary" disabled={statusBusy} onClick={() => void changeStatus("Active")}>
                      <Play className="size-4" aria-hidden />
                      {t("journey.statusActivate")}
                    </Button>
                  )}
                  {journey.status === "Active" && (
                    <Button
                      variant="secondary"
                      disabled={statusBusy}
                      onClick={() => void changeStatus("Inactive")}
                    >
                      <Pause className="size-4" aria-hidden />
                      {t("journey.statusDeactivate")}
                    </Button>
                  )}
                  {journey.status === "Inactive" && (
                    <Button variant="secondary" disabled={statusBusy} onClick={() => void changeStatus("Active")}>
                      <Play className="size-4" aria-hidden />
                      {t("journey.statusActivate")}
                    </Button>
                  )}
                  <Button variant="destructive" disabled={statusBusy} onClick={() => setArchiveOpen(true)}>
                    <Archive className="size-4" aria-hidden />
                    {t("journey.statusArchive")}
                  </Button>
                  <Button
                    className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
                    onClick={() => setStageDialog({ open: true })}
                  >
                    <Plus className="size-4" aria-hidden />
                    {t("journey.addStage")}
                  </Button>
                </>
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Bound personas (US-3) — the binding selector offers Active personas only (FR-005). */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base font-bold">
            <UsersRound className="size-5 text-primary" aria-hidden />
            {t("journey.personasTitle")}
          </CardTitle>
          <CardDescription>{t("journey.personasDesc")}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {personaError && (
            <div
              role="alert"
              className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
            >
              {personaError}
            </div>
          )}
          <div className="flex flex-wrap items-center gap-2">
            {journey.personaBindings.length === 0 ? (
              <p className="text-sm text-muted-foreground">{t("journey.noPersonasBound")}</p>
            ) : (
              journey.personaBindings.map((b) => (
                <span
                  key={b.personaId}
                  className="inline-flex items-center gap-1.5 rounded-full border border-border bg-card py-1 ps-3 pe-1.5 text-sm"
                >
                  <span className="truncate">{personaLabel(b)}</span>
                  {!readOnly && (
                    <button
                      type="button"
                      onClick={() => unbindPersona(b.personaId)}
                      disabled={personaSaving}
                      aria-label={t("journey.unbindPersona", { name: personaLabel(b) })}
                      className="inline-flex size-5 items-center justify-center rounded-full text-muted-foreground transition-colors hover:bg-muted hover:text-destructive disabled:opacity-50"
                    >
                      <X className="size-3.5" aria-hidden />
                    </button>
                  )}
                </span>
              ))
            )}

            {!readOnly && (
              <DropdownMenu>
                <DropdownMenuTrigger
                  className={cn(buttonVariants({ variant: "secondary", size: "compact" }))}
                  disabled={personaSaving}
                >
                  <UserPlus className="size-4" aria-hidden />
                  {t("journey.bindPersona")}
                </DropdownMenuTrigger>
                <DropdownMenuContent align="start">
                  {selectablePersonas.length === 0 ? (
                    <DropdownMenuItem disabled>
                      {activePersonas.length === 0
                        ? t("journey.noActivePersonas")
                        : t("journey.allPersonasBound")}
                    </DropdownMenuItem>
                  ) : (
                    selectablePersonas.map((p) => (
                      <DropdownMenuItem key={p.personaId} onClick={() => bindPersona(p)}>
                        <UsersRound className="size-4" aria-hidden />
                        {personaLabel(p)}
                      </DropdownMenuItem>
                    ))
                  )}
                </DropdownMenuContent>
              </DropdownMenu>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Concurrent-edit banner */}
      {changedExternally && (
        <div
          role="status"
          className="flex flex-wrap items-center gap-3 rounded-md border border-d3-dark/20 bg-d3-light px-4 py-3 dark:bg-d3-dark/25"
        >
          <AlertTriangle className="size-5 shrink-0 text-d3-dark dark:text-d3-light" aria-hidden />
          <div className="min-w-0 flex-1">
            <p className="text-sm font-semibold text-foreground">{t("journey.concurrentEditTitle")}</p>
            <p className="text-sm text-foreground/80">{t("journey.concurrentEditBody")}</p>
          </div>
          <div className="flex items-center gap-2">
            <Button size="sm" variant="secondary" onClick={() => void load(false)}>
              {t("journey.concurrentEditReload")}
            </Button>
            <Button size="sm" variant="ghost" onClick={dismissConcurrentEdit}>
              {t("journey.concurrentEditDismiss")}
            </Button>
          </div>
        </div>
      )}

      {/* Action error banner */}
      {actionError && (
        <div
          role="alert"
          className="flex items-start justify-between gap-3 rounded-md border border-destructive/30 bg-destructive/10 px-4 py-3 text-sm text-destructive"
        >
          <span>{actionError}</span>
          <button
            type="button"
            onClick={() => setActionError(null)}
            className="shrink-0 font-medium underline-offset-2 hover:underline"
          >
            {t("journey.concurrentEditDismiss")}
          </button>
        </div>
      )}

      {/* Stage accordion */}
      {journey.stages.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-lg border border-border bg-card py-16 text-center shadow-sm dark:shadow-none">
          <Layers className="mb-4 size-12 text-muted-foreground" aria-hidden />
          <h3 className="mb-2 text-lg font-bold">{t("journey.noStagesTitle")}</h3>
          <p className="mb-4 max-w-sm text-muted-foreground">{t("journey.noStagesHelp")}</p>
          {!readOnly && (
            <Button
              className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
              onClick={() => setStageDialog({ open: true })}
            >
              <Plus className="size-4 ms-2" aria-hidden />
              {t("journey.addStage")}
            </Button>
          )}
        </div>
      ) : (
        <div className="space-y-3">
          {journey.stages.map((stage, index) => (
            <StageAccordionItem
              key={stage.stageId}
              stage={stage}
              seqLabel={`${stage.sequenceNumber}.0`}
              expanded={expandedStages.has(stage.stageId)}
              onToggle={() => toggleStage(stage.stageId)}
              reorderable={!readOnly}
              dragging={dragIndex === index}
              dropPosition={
                dragIndex !== null && overIndex === index && dragIndex !== index
                  ? overAfter
                    ? "after"
                    : "before"
                  : null
              }
              onReorderStart={() => setDragIndex(index)}
              onReorderOver={(after) => {
                setOverIndex(index)
                setOverAfter(after)
              }}
              onReorderEnd={resetDrag}
              onReorderDrop={handleReorderDrop}
              actions={
                readOnly ? undefined : (
                  <>
                    <Button
                      variant="secondary"
                      size="compact"
                      aria-label={t("journey.addTouchpoint")}
                      onClick={() => setTpDialog({ open: true, stageId: stage.stageId })}
                    >
                      <Plus className="size-4" aria-hidden />
                      <span className="hidden sm:inline">{t("journey.addTouchpoint")}</span>
                    </Button>
                    <DropdownMenu>
                      <DropdownMenuTrigger
                        className={cn(buttonVariants({ variant: "ghost", size: "icon-sm" }))}
                        aria-label={t("journey.stageActions")}
                      >
                        <MoreHorizontal className="size-4" aria-hidden />
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem onClick={() => setStageDialog({ open: true, stage })}>
                          <PencilLine className="size-4" aria-hidden />
                          {t("journey.editStage")}
                        </DropdownMenuItem>
                        <DropdownMenuItem
                          disabled={index === 0}
                          onClick={() => void moveStage(index, -1)}
                        >
                          <ChevronUp className="size-4" aria-hidden />
                          {t("journey.moveStageEarlier")}
                        </DropdownMenuItem>
                        <DropdownMenuItem
                          disabled={index === lastStageIndex}
                          onClick={() => void moveStage(index, 1)}
                        >
                          <ChevronDown className="size-4" aria-hidden />
                          {t("journey.moveStageLater")}
                        </DropdownMenuItem>
                        <DropdownMenuSeparator />
                        <DropdownMenuItem
                          variant="destructive"
                          onClick={() => setDeleteStageTarget(stage)}
                        >
                          <Trash2 className="size-4" aria-hidden />
                          {t("journey.deleteStage")}
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </>
                )
              }
            >
              {stage.touchpoints.length === 0 ? (
                <p className="py-2 text-center text-sm text-muted-foreground">
                  {t("journey.noTouchpoints")}
                </p>
              ) : (
                <div className="space-y-2">
                  {stage.touchpoints.map((tp, tpIndex) => (
                    <TouchpointRow
                      key={tp.touchpointId}
                      touchpoint={tp}
                      seqLabel={`${stage.sequenceNumber}.${tpIndex + 1}`}
                      actions={
                        readOnly ? undefined : (
                          <>
                            <Button
                              variant="ghost"
                              size="icon-xs"
                              aria-label={t("journey.editTouchpoint")}
                              onClick={() =>
                                setTpDialog({ open: true, stageId: stage.stageId, touchpoint: tp })
                              }
                            >
                              <PencilLine className="size-3.5" aria-hidden />
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon-xs"
                              aria-label={t("journey.deleteTouchpoint")}
                              className="text-muted-foreground hover:text-destructive"
                              onClick={() =>
                                setDeleteTpTarget({ stageId: stage.stageId, touchpoint: tp })
                              }
                            >
                              <Trash2 className="size-3.5" aria-hidden />
                            </Button>
                          </>
                        )
                      }
                    />
                  ))}
                </div>
              )}
            </StageAccordionItem>
          ))}
        </div>
      )}

      {/* Dialogs */}
      <JourneyFormDialog
        open={editDetailsOpen}
        onOpenChange={setEditDetailsOpen}
        journey={journey}
        onSaved={() => void load(false)}
      />
      <StageFormDialog
        open={stageDialog.open}
        onOpenChange={(open) => setStageDialog((s) => ({ ...s, open }))}
        journeyId={journeyId}
        stage={stageDialog.stage}
        onSaved={() => void load(false)}
      />
      <TouchpointFormDialog
        open={tpDialog.open}
        onOpenChange={(open) => setTpDialog((s) => ({ ...s, open }))}
        stageId={tpDialog.stageId}
        touchpoint={tpDialog.touchpoint}
        onSaved={() => void load(false)}
      />

      {/* Delete stage confirmation */}
      <AlertDialog
        open={!!deleteStageTarget}
        onOpenChange={(o) => {
          if (!o) setDeleteStageTarget(null)
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("journey.deleteStageConfirmTitle")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("journey.deleteStageConfirmBody", { name: deleteStageTarget?.name ?? "" })}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("journey.publishCancel")}</AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              onClick={() => deleteStageTarget && void confirmDeleteStage(deleteStageTarget)}
            >
              {t("journey.deleteStageConfirmAction")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Delete touchpoint confirmation */}
      <AlertDialog
        open={!!deleteTpTarget}
        onOpenChange={(o) => {
          if (!o) setDeleteTpTarget(null)
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("journey.deleteTouchpointConfirmTitle")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("journey.deleteTouchpointConfirmBody", { name: deleteTpTarget?.touchpoint.name ?? "" })}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("journey.publishCancel")}</AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              onClick={() => deleteTpTarget && void confirmDeleteTouchpoint(deleteTpTarget.touchpoint)}
            >
              {t("journey.deleteTouchpointConfirmAction")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Archive confirmation */}
      <AlertDialog open={archiveOpen} onOpenChange={setArchiveOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("journey.archiveConfirmTitle")}</AlertDialogTitle>
            <AlertDialogDescription>{t("journey.archiveConfirmBody")}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={statusBusy}>{t("journey.publishCancel")}</AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              disabled={statusBusy}
              onClick={() => void changeStatus("Archived")}
            >
              {t("journey.archiveConfirmAction")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}
