// F8 Survey Builder (T090 → US3/US4, reworked for clickthrough parity). Three-column
// layout: palette (Metric / Question types / Structure) · canvas · persistent config
// panel — clicking a card selects it and edits live in the panel (no drawer). The
// canvas PERSISTS through the T152 API modules: sections/sets create rows immediately;
// renames + set edits PATCH (debounced); question edits create-then-PUT debounced;
// deletes drive the FR-2.5/FR-2.6 confirm flows; drags call the move endpoint.
// A "General" section is auto-created on an empty canvas (reference default). The
// backend ships no structure GET (TODO-M01-028) — a reload starts from an empty view.
// Paragraph elements are builder-local (no backend QuestionType — never persisted).
// DnD is @dnd-kit with a pointer sensor + DragOverlay; onDragOver relocates questions
// across sections and set pools so cross-container drops land positionally.
// US4 (T180): the routing toggle needs layout = one-question-per-page (FR-9.1,
// tooltip), an enable-confirmation modal, and locks shuffle on enable; eligible cards
// show the routing action and "Routing set" badge (set members excluded, FR-9.5).

import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import { useNavigate } from "react-router"
import { useTranslation } from "react-i18next"
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  pointerWithin,
  rectIntersection,
  useSensor,
  useSensors,
  type CollisionDetection,
  type DragEndEvent,
  type DragOverEvent,
  type DragStartEvent,
} from "@dnd-kit/core"
import { arrayMove } from "@dnd-kit/sortable"
import {
  ArrowLeft,
  ArrowRight,
  GripVertical,
  Info,
  Languages,
  Route as RouteIcon,
} from "lucide-react"

import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
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
import { Switch } from "@/components/ui/switch"
import { Textarea } from "@/components/ui/textarea"
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip"
import { useDirection } from "@/hooks/use-direction"
import { SurveysApiError, ETagConflictError, formatETag } from "../api/etag"
import {
  changeSurveyStatus,
  getSurvey,
  newIdempotencyKey,
  type SurveyStatus,
  type SurveyView,
} from "../api/surveys-api"
import { createSection, deleteSection, updateSection } from "../api/sections-api"
import {
  createQuestionsSet,
  deleteQuestionsSet,
  updateQuestionsSet,
} from "../api/questions-sets-api"
import {
  builderQuestionToInput,
  createQuestion,
  deleteQuestion,
  moveQuestion,
  updateQuestion,
} from "../api/questions-api"
import { toggleSurveyRouting } from "../api/routing-api"
import {
  countQuestions,
  newBuilderQuestion,
  newBuilderSection,
  newBuilderSet,
  type BuilderQuestion,
  type BuilderQuestionType,
  type BuilderSection,
  type BuilderSet,
} from "../components/builder-types"
import { DestructiveReturnToDraftDialog } from "../components/DestructiveReturnToDraftDialog"
import { EtagConflictDialog } from "../components/EtagConflictDialog"
import { PauseWithRulesDialog } from "../components/PauseWithRulesDialog"
import { PublishGateBanner, publishGateBlocked } from "../components/PublishGateBanner"
import { QuestionConfigPanel } from "../components/QuestionConfigPanel"
import { QuestionPalette } from "../components/QuestionPalette"
import { RoutingMapEditor, type RoutingTargetOption } from "../components/RoutingMapEditor"
import { SectionColumn, type SectionCascadeCounts } from "../components/SectionColumn"
import { SurveyStatusPill } from "../components/SurveyStatusPill"
import { SurveyWizardStepper } from "../components/SurveyWizardStepper"

/** Pointer-first collision: containers under the pointer win; fall back to rect
 * intersection so keyboard/edge drags still resolve. */
const collision: CollisionDetection = (args) => {
  const within = pointerWithin(args)
  return within.length > 0 ? within : rectIntersection(args)
}

export default function SurveyBuilderPage({ surveyId }: { surveyId: string }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { isRtl } = useDirection()
  const BackIcon = isRtl ? ArrowRight : ArrowLeft
  const NextIcon = isRtl ? ArrowLeft : ArrowRight

  const [survey, setSurvey] = useState<SurveyView | null>(null)
  const [etag, setEtagValue] = useState<string | null>(null)
  const [sections, setSections] = useState<BuilderSection[]>([])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [routingTarget, setRoutingTarget] = useState<BuilderQuestion | null>(null)
  const [loading, setLoading] = useState(true)
  const [statusBusy, setStatusBusy] = useState(false)
  const [apiError, setApiError] = useState<string | null>(null)
  const [conflictOpen, setConflictOpen] = useState(false)
  const [routingConfirmOpen, setRoutingConfirmOpen] = useState(false)
  const [setDelete, setSetDelete] = useState<{ set: BuilderSet; count: number } | null>(null)
  const [setSettingsFor, setSetSettingsFor] = useState<{
    sectionLocal: string
    setLocal: string
  } | null>(null)
  const [confirmState, setConfirmState] = useState<{
    kind: "destructive" | "pauseRules"
    to: SurveyStatus
    count: number
  } | null>(null)
  // DragOverlay ghost content while a drag is live.
  const [dragGhost, setDragGhost] = useState<string | null>(null)

  // Debounced PATCH timers (sections/sets renames + question persists).
  const patchTimers = useRef(new Map<string, number>())
  // Question creates currently in flight — the debounce re-queues instead of
  // double-POSTing while one is pending.
  const createsInFlight = useRef(new Set<string>())
  // Original container of the question being dragged (the move API needs the
  // SOURCE section id, and onDragOver already relocated the local state).
  const dragOrigin = useRef<{ sectionServerId: string | null } | null>(null)
  // Auto-create the default "General" section only once per mount.
  const seededDefault = useRef(false)
  // The config panel floors at the palette's rendered height so the two side
  // columns always read as the same height (measured — content-driven).
  const paletteRef = useRef<HTMLDivElement>(null)
  const [paletteHeight, setPaletteHeight] = useState<number | undefined>()
  useEffect(() => {
    const el = paletteRef.current
    if (!el) return
    const observer = new ResizeObserver(() => setPaletteHeight(el.offsetHeight))
    observer.observe(el)
    setPaletteHeight(el.offsetHeight)
    return () => observer.disconnect()
  }, [loading])

  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 5 } }))

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const { data, etag: nextEtag } = await getSurvey(surveyId)
      setSurvey(data)
      setEtagValue(nextEtag)
    } finally {
      setLoading(false)
    }
  }, [surveyId])

  useEffect(() => {
    void load()
  }, [load])

  const fail = useCallback((err: unknown, fallbackKey: string) => {
    if (err instanceof SurveysApiError) setApiError(err.message)
    else setApiError(fallbackKey)
  }, [])

  const questionsCount = useMemo(() => countQuestions(sections), [sections])
  const publishBlocked = publishGateBlocked(sections.length, questionsCount)

  // ── Section CRUD ───────────────────────────────────────────────────────────────

  const createSectionRow = useCallback(
    (name: string, afterLocalId?: string) => {
      const local = newBuilderSection(name)
      setSections((prev) => {
        if (!afterLocalId) return [...prev, local]
        const idx = prev.findIndex((s) => s.localId === afterLocalId)
        if (idx < 0) return [...prev, local]
        return [...prev.slice(0, idx + 1), local, ...prev.slice(idx + 1)]
      })
      createSection(surveyId, { name })
        .then(({ data }) =>
          setSections((prev) =>
            prev.map((s) =>
              s.localId === local.localId
                ? { ...s, serverId: data.id, rowVersion: data.rowVersion }
                : s
            )
          )
        )
        .catch((err) => {
          setSections((prev) => prev.filter((s) => s.localId !== local.localId))
          fail(err, "surveysModule.builder.sectionCreateFailed")
        })
      return local
    },
    [surveyId, fail]
  )

  // Default section on an empty canvas — the reference starts with "General".
  useEffect(() => {
    if (!loading && survey && sections.length === 0 && !seededDefault.current) {
      seededDefault.current = true
      createSectionRow("General")
    }
  }, [loading, survey, sections.length, createSectionRow])

  const addSection = useCallback(
    (afterLocalId?: string) => {
      // Backend requires a non-empty name (section.name.required) — sequential default.
      createSectionRow(`Section ${sections.length + 1}`, afterLocalId)
    },
    [createSectionRow, sections.length]
  )

  const renameSection = useCallback(
    (section: BuilderSection, title: string) => {
      setSections((prev) =>
        prev.map((s) => (s.localId === section.localId ? { ...s, title } : s))
      )
      if (!section.serverId) return
      const key = `section:${section.localId}`
      window.clearTimeout(patchTimers.current.get(key))
      patchTimers.current.set(
        key,
        window.setTimeout(() => {
          setSections((prev) => {
            const current = prev.find((s) => s.localId === section.localId)
            // Skip the PATCH while the title is blank (section.name.required → 400).
            if (current?.serverId && current.rowVersion != null && current.title.trim() !== "") {
              void updateSection(surveyId, current.serverId, { name: current.title }, formatETag(current.rowVersion))
                .then(({ data }) =>
                  setSections((p) =>
                    p.map((s) =>
                      s.localId === section.localId ? { ...s, rowVersion: data.rowVersion } : s
                    )
                  )
                )
                .catch((err) => fail(err, "surveysModule.builder.sectionSaveFailed"))
            }
            return prev
          })
        }, 800)
      )
    },
    [surveyId, fail]
  )

  /** FR-2.5 delete flow — returns cascade counts on 409, null when deleted. */
  const removeSection = useCallback(
    async (section: BuilderSection, confirm: boolean): Promise<SectionCascadeCounts | null> => {
      if (!section.serverId) {
        setSections((prev) => prev.filter((s) => s.localId !== section.localId))
        return null
      }
      try {
        await deleteSection(
          surveyId,
          section.serverId,
          confirm,
          section.rowVersion != null ? formatETag(section.rowVersion) : undefined
        )
        setSections((prev) => prev.filter((s) => s.localId !== section.localId))
        return null
      } catch (err) {
        if (err instanceof SurveysApiError && err.status === 409) {
          const d = (err.details ?? {}) as Record<string, unknown>
          return {
            standaloneQuestions: Number(d.standaloneQuestions ?? d.standalone_questions ?? 0),
            questionsSets: Number(d.questionsSets ?? d.questions_sets ?? 0),
            setQuestions: Number(d.setQuestions ?? d.set_questions ?? 0),
          }
        }
        fail(err, "surveysModule.builder.sectionDeleteFailed")
        return null
      }
    },
    [surveyId, fail]
  )

  // ── Questions Set CRUD (T153) ──────────────────────────────────────────────────

  const addSet = useCallback(
    (section: BuilderSection, openSettings: boolean) => {
      if (!section.serverId) return
      // Non-empty default title required by the backend (questionsset.title.required).
      const local = newBuilderSet(t("surveysModule.set.defaultTitle"))
      setSections((prev) =>
        prev.map((s) =>
          s.localId === section.localId ? { ...s, sets: [...s.sets, local] } : s
        )
      )
      if (openSettings)
        setSetSettingsFor({ sectionLocal: section.localId, setLocal: local.localId })
      createQuestionsSet(surveyId, section.serverId, {
        title: local.title,
        selectionMode: "random",
        count: 2,
      })
        .then(({ data }) =>
          setSections((prev) =>
            prev.map((s) =>
              s.localId === section.localId
                ? {
                    ...s,
                    sets: s.sets.map((st) =>
                      st.localId === local.localId
                        ? { ...st, serverId: data.id, rowVersion: data.rowVersion }
                        : st
                    ),
                  }
                : s
            )
          )
        )
        .catch((err) => {
          setSections((prev) =>
            prev.map((s) =>
              s.localId === section.localId
                ? { ...s, sets: s.sets.filter((st) => st.localId !== local.localId) }
                : s
            )
          )
          setSetSettingsFor(null)
          fail(err, "surveysModule.set.createFailed")
        })
    },
    [surveyId, t, fail]
  )

  const changeSet = useCallback(
    (section: BuilderSection, next: BuilderSet) => {
      setSections((prev) =>
        prev.map((s) =>
          s.localId === section.localId
            ? { ...s, sets: s.sets.map((st) => (st.localId === next.localId ? next : st)) }
            : s
        )
      )
      if (!next.serverId || !section.serverId) return
      const key = `set:${next.localId}`
      window.clearTimeout(patchTimers.current.get(key))
      patchTimers.current.set(
        key,
        window.setTimeout(() => {
          setSections((prev) => {
            const sec = prev.find((s) => s.localId === section.localId)
            const cur = sec?.sets.find((st) => st.localId === next.localId)
            // Skip the PATCH while the title is blank (questionsset.title.required → 400).
            if (sec?.serverId && cur?.serverId && cur.rowVersion != null && cur.title.trim() !== "") {
              void updateQuestionsSet(
                surveyId,
                sec.serverId,
                cur.serverId,
                {
                  title: cur.title,
                  description: cur.description || null,
                  selectionMode: cur.selectionMode,
                  count: cur.count,
                },
                formatETag(cur.rowVersion)
              )
                .then(({ data }) =>
                  setSections((p) =>
                    p.map((s) =>
                      s.localId === section.localId
                        ? {
                            ...s,
                            sets: s.sets.map((st) =>
                              st.localId === next.localId
                                ? { ...st, rowVersion: data.rowVersion }
                                : st
                            ),
                          }
                        : s
                    )
                  )
                )
                .catch((err) => fail(err, "surveysModule.set.saveFailed"))
            }
            return prev
          })
        }, 800)
      )
    },
    [surveyId, fail]
  )

  const removeSet = useCallback(
    async (section: BuilderSection, set: BuilderSet, confirm: boolean) => {
      const removeLocally = () =>
        setSections((prev) =>
          prev.map((s) =>
            s.localId === section.localId
              ? { ...s, sets: s.sets.filter((st) => st.localId !== set.localId) }
              : s
          )
        )
      if (!set.serverId || !section.serverId) {
        removeLocally()
        return
      }
      try {
        await deleteQuestionsSet(
          surveyId,
          section.serverId,
          set.serverId,
          confirm,
          set.rowVersion != null ? formatETag(set.rowVersion) : undefined
        )
        removeLocally()
        setSetDelete(null)
      } catch (err) {
        if (err instanceof SurveysApiError && err.status === 409) {
          const d = (err.details ?? {}) as Record<string, unknown>
          setSetDelete({ set, count: Number(d.questionsCount ?? d.questions_count ?? 0) })
          return
        }
        fail(err, "surveysModule.set.deleteFailed")
      }
    },
    [surveyId, fail]
  )

  // ── Question CRUD ──────────────────────────────────────────────────────────────

  const findQuestionHome = useCallback(
    (localId: string): { section: BuilderSection; set: BuilderSet | null } | null => {
      for (const s of sections) {
        if (s.questions.some((q) => q.localId === localId)) return { section: s, set: null }
        for (const st of s.sets)
          if (st.questions.some((q) => q.localId === localId)) return { section: s, set: st }
      }
      return null
    },
    [sections]
  )

  /** Debounced create-or-update. Re-queues while a create is in flight and after a
   * create lands (so edits made during the flight still PUT). Paragraphs never
   * persist — no backend QuestionType exists for them. */
  const queuePersistQuestion = useCallback(
    (localId: string) => {
      const key = `question:${localId}`
      window.clearTimeout(patchTimers.current.get(key))
      patchTimers.current.set(
        key,
        window.setTimeout(() => {
          setSections((prev) => {
            let home: { section: BuilderSection; set: BuilderSet | null } | null = null
            for (const s of prev) {
              if (s.questions.some((q) => q.localId === localId)) home = { section: s, set: null }
              for (const st of s.sets)
                if (st.questions.some((q) => q.localId === localId)) home = { section: s, set: st }
            }
            const q =
              home?.set?.questions.find((x) => x.localId === localId) ??
              home?.section.questions.find((x) => x.localId === localId)
            if (!home || !q || q.type === "Paragraph" || !home.section.serverId) return prev
            // question.text.required — hold the persist until the author types a
            // text (same pattern as blank section/set titles). The next edit
            // re-queues, so the row saves as soon as it becomes valid.
            if (q.text.trim() === "") return prev
            if (createsInFlight.current.has(localId)) {
              queuePersistQuestion(localId)
              return prev
            }
            const setServerId = home.set?.serverId ?? null
            const container = home.set ? home.set.questions : home.section.questions
            const order = Math.max(
              0,
              container.filter((x) => x.type !== "Paragraph").findIndex((x) => x.localId === localId)
            )
            if (q.serverId == null) {
              createsInFlight.current.add(localId)
              void createQuestion(
                surveyId,
                home.section.serverId,
                builderQuestionToInput(q, setServerId, order)
              )
                .then(({ data }) => {
                  setSections((p) =>
                    p.map((s) => ({
                      ...s,
                      questions: s.questions.map((x) =>
                        x.localId === localId
                          ? { ...x, serverId: data.id, rowVersion: data.rowVersion }
                          : x
                      ),
                      sets: s.sets.map((st) => ({
                        ...st,
                        questions: st.questions.map((x) =>
                          x.localId === localId
                            ? { ...x, serverId: data.id, rowVersion: data.rowVersion }
                            : x
                        ),
                      })),
                    }))
                  )
                  createsInFlight.current.delete(localId)
                  // Edits made while the create was in flight still need a PUT.
                  queuePersistQuestion(localId)
                })
                .catch((err) => {
                  createsInFlight.current.delete(localId)
                  fail(err, "surveysModule.builder.questionSaveFailed")
                })
            } else {
              void updateQuestion(
                surveyId,
                home.section.serverId,
                q.serverId,
                builderQuestionToInput(q, setServerId, order),
                q.rowVersion != null ? formatETag(q.rowVersion) : undefined
              )
                .then(({ data }) =>
                  setSections((p) =>
                    p.map((s) => ({
                      ...s,
                      questions: s.questions.map((x) =>
                        x.localId === localId ? { ...x, rowVersion: data.rowVersion } : x
                      ),
                      sets: s.sets.map((st) => ({
                        ...st,
                        questions: st.questions.map((x) =>
                          x.localId === localId ? { ...x, rowVersion: data.rowVersion } : x
                        ),
                      })),
                    }))
                  )
                )
                .catch((err) => fail(err, "surveysModule.builder.questionSaveFailed"))
            }
            return prev
          })
        }, 800)
      )
    },
    [surveyId, fail]
  )

  const addQuestion = useCallback(
    (type: BuilderQuestionType, sectionLocalId?: string, setLocalId?: string) => {
      const question = newBuilderQuestion(type)
      setSections((prev) => {
        // Prefer: explicit target → the selected question's section → the first section.
        const selectedHome = prev.find(
          (s) =>
            s.questions.some((q) => q.localId === selectedId) ||
            s.sets.some((st) => st.questions.some((q) => q.localId === selectedId))
        )
        const targetId = sectionLocalId ?? selectedHome?.localId ?? prev[0]?.localId
        if (!targetId) return prev
        return prev.map((s) => {
          if (s.localId !== targetId) return s
          if (setLocalId) {
            return {
              ...s,
              sets: s.sets.map((st) =>
                st.localId === setLocalId ? { ...st, questions: [...st.questions, question] } : st
              ),
            }
          }
          return { ...s, questions: [...s.questions, question] }
        })
      })
      setSelectedId(question.localId)
      if (type !== "Paragraph") queuePersistQuestion(question.localId)
    },
    [selectedId, queuePersistQuestion]
  )

  const addParagraph = useCallback((sectionLocalId: string) => {
    const paragraph = newBuilderQuestion("Paragraph")
    paragraph.text = ""
    setSections((prev) =>
      prev.map((s) =>
        s.localId === sectionLocalId ? { ...s, questions: [...s.questions, paragraph] } : s
      )
    )
    setSelectedId(paragraph.localId)
  }, [])

  const updateQuestionLocal = useCallback(
    (next: BuilderQuestion, persist = true) => {
      setSections((prev) =>
        prev.map((s) => ({
          ...s,
          questions: s.questions.map((q) => (q.localId === next.localId ? next : q)),
          sets: s.sets.map((st) => ({
            ...st,
            questions: st.questions.map((q) => (q.localId === next.localId ? next : q)),
          })),
        }))
      )
      if (persist && next.type !== "Paragraph") queuePersistQuestion(next.localId)
    },
    [queuePersistQuestion]
  )

  const removeQuestion = useCallback(
    async (localId: string) => {
      const home = findQuestionHome(localId)
      const question =
        home?.set?.questions.find((q) => q.localId === localId) ??
        home?.section.questions.find((q) => q.localId === localId)
      if (home?.section.serverId && question?.serverId) {
        try {
          await deleteQuestion(
            surveyId,
            home.section.serverId,
            question.serverId,
            question.rowVersion != null ? formatETag(question.rowVersion) : undefined
          )
        } catch (err) {
          fail(err, "surveysModule.builder.questionDeleteFailed")
          return
        }
      }
      setSections((prev) =>
        prev.map((s) => ({
          ...s,
          questions: s.questions.filter((q) => q.localId !== localId),
          sets: s.sets.map((st) => ({
            ...st,
            questions: st.questions.filter((q) => q.localId !== localId),
          })),
        }))
      )
      setSelectedId((cur) => (cur === localId ? null : cur))
    },
    [surveyId, findQuestionHome, fail]
  )

  const selectedQuestion = useMemo(() => {
    if (!selectedId) return null
    for (const s of sections) {
      const hit =
        s.questions.find((q) => q.localId === selectedId) ??
        s.sets.flatMap((st) => st.questions).find((q) => q.localId === selectedId)
      if (hit) return hit
    }
    return null
  }, [sections, selectedId])

  // ── Drag & drop ────────────────────────────────────────────────────────────────

  /** Resolve a droppable/sortable id to its container. */
  const containerOf = useCallback(
    (
      id: string,
      state: BuilderSection[]
    ): { sectionLocal: string; setLocal: string | null } | null => {
      if (id.startsWith("section:")) return { sectionLocal: id.slice(8), setLocal: null }
      if (id.startsWith("set:")) {
        const owner = state.find((s) => s.sets.some((st) => st.localId === id.slice(4)))
        return owner ? { sectionLocal: owner.localId, setLocal: id.slice(4) } : null
      }
      if (id.startsWith("question:")) {
        const qLocal = id.slice(9)
        for (const s of state) {
          if (s.questions.some((q) => q.localId === qLocal))
            return { sectionLocal: s.localId, setLocal: null }
          for (const st of s.sets)
            if (st.questions.some((q) => q.localId === qLocal))
              return { sectionLocal: s.localId, setLocal: st.localId }
        }
      }
      return null
    },
    []
  )

  const onDragStart = useCallback(
    (event: DragStartEvent) => {
      const id = String(event.active.id)
      if (id.startsWith("question:")) {
        const home = findQuestionHome(id.slice(9))
        dragOrigin.current = { sectionServerId: home?.section.serverId ?? null }
        const q =
          home?.set?.questions.find((x) => x.localId === id.slice(9)) ??
          home?.section.questions.find((x) => x.localId === id.slice(9))
        setDragGhost(q?.text || t("surveysModule.builder.untitledQuestion"))
      } else if (event.active.data.current?.paletteType) {
        setDragGhost(null)
        dragOrigin.current = null
        setDragGhost(String(event.active.data.current.paletteLabel ?? ""))
      } else {
        setDragGhost(null)
      }
    },
    [findQuestionHome, t]
  )

  /** Cross-container relocation while dragging — the standard @dnd-kit
   * multi-container pattern, so drops land positionally in the new container. */
  const onDragOver = useCallback(
    (event: DragOverEvent) => {
      const activeId = String(event.active.id)
      const overId = String(event.over?.id ?? "")
      if (!overId || !activeId.startsWith("question:")) return
      setSections((prev) => {
        const from = containerOf(activeId, prev)
        const to = containerOf(overId, prev)
        if (!from || !to) return prev
        if (from.sectionLocal === to.sectionLocal && from.setLocal === to.setLocal) return prev

        const qLocal = activeId.slice(9)
        let moved: BuilderQuestion | undefined
        const without = prev.map((s) => ({
          ...s,
          questions: s.questions.filter((q) => {
            if (q.localId === qLocal) {
              moved = q
              return false
            }
            return true
          }),
          sets: s.sets.map((st) => ({
            ...st,
            questions: st.questions.filter((q) => {
              if (q.localId === qLocal) {
                moved = q
                return false
              }
              return true
            }),
          })),
        }))
        if (!moved) return prev
        const insertAt = (list: BuilderQuestion[]): BuilderQuestion[] => {
          if (overId.startsWith("question:")) {
            const idx = list.findIndex((q) => q.localId === overId.slice(9))
            if (idx >= 0) return [...list.slice(0, idx), moved as BuilderQuestion, ...list.slice(idx)]
          }
          return [...list, moved as BuilderQuestion]
        }
        return without.map((s) => {
          if (s.localId !== to.sectionLocal) return s
          if (to.setLocal) {
            return {
              ...s,
              sets: s.sets.map((st) =>
                st.localId === to.setLocal ? { ...st, questions: insertAt(st.questions) } : st
              ),
            }
          }
          return { ...s, questions: insertAt(s.questions) }
        })
      })
    },
    [containerOf]
  )

  const onDragEnd = useCallback(
    (event: DragEndEvent) => {
      setDragGhost(null)
      const activeId = String(event.active.id)
      const overId = String(event.over?.id ?? "")
      const origin = dragOrigin.current
      dragOrigin.current = null
      if (!overId) return

      // Palette tile → section / set / question position
      const paletteType = event.active.data.current?.paletteType as BuilderQuestionType | undefined
      if (paletteType) {
        const target = containerOf(overId, sections)
        if (target) addQuestion(paletteType, target.sectionLocal, target.setLocal ?? undefined)
        return
      }
      // Palette "Questions Set" tile → section
      if (event.active.data.current?.paletteSet) {
        const target = containerOf(overId, sections)
        const section = sections.find((s) => s.localId === target?.sectionLocal)
        if (section) addSet(section, false)
        return
      }

      if (!activeId.startsWith("question:")) return
      const qLocal = activeId.slice(9)

      setSections((prev) => {
        const home = ((): { section: BuilderSection; set: BuilderSet | null } | null => {
          for (const s of prev) {
            if (s.questions.some((q) => q.localId === qLocal)) return { section: s, set: null }
            for (const st of s.sets)
              if (st.questions.some((q) => q.localId === qLocal)) return { section: s, set: st }
          }
          return null
        })()
        if (!home) return prev
        const question =
          home.set?.questions.find((q) => q.localId === qLocal) ??
          home.section.questions.find((q) => q.localId === qLocal)
        if (!question) return prev

        // Same-container final reorder (cross-container already happened in onDragOver).
        let next = prev
        const overContainer = containerOf(overId, prev)
        if (
          overId.startsWith("question:") &&
          overContainer &&
          overContainer.sectionLocal === home.section.localId &&
          (overContainer.setLocal ?? null) === (home.set?.localId ?? null)
        ) {
          const list = home.set ? home.set.questions : home.section.questions
          const oldIndex = list.findIndex((q) => q.localId === qLocal)
          const newIndex = list.findIndex((q) => q.localId === overId.slice(9))
          if (oldIndex >= 0 && newIndex >= 0 && oldIndex !== newIndex) {
            next = prev.map((s) => {
              if (s.localId !== home.section.localId) return s
              if (home.set) {
                return {
                  ...s,
                  sets: s.sets.map((st) =>
                    st.localId === home.set?.localId
                      ? { ...st, questions: arrayMove(st.questions, oldIndex, newIndex) }
                      : st
                  ),
                }
              }
              return { ...s, questions: arrayMove(s.questions, oldIndex, newIndex) }
            })
          }
        }

        // Persist the move (source section captured at drag start).
        const finalHome = ((): { section: BuilderSection; set: BuilderSet | null } | null => {
          for (const s of next) {
            if (s.questions.some((q) => q.localId === qLocal)) return { section: s, set: null }
            for (const st of s.sets)
              if (st.questions.some((q) => q.localId === qLocal)) return { section: s, set: st }
          }
          return null
        })()
        if (
          finalHome &&
          question.serverId &&
          finalHome.section.serverId &&
          origin?.sectionServerId &&
          question.type !== "Paragraph"
        ) {
          const list = finalHome.set ? finalHome.set.questions : finalHome.section.questions
          const order = Math.max(
            0,
            list.filter((q) => q.type !== "Paragraph").findIndex((q) => q.localId === qLocal)
          )
          void moveQuestion(
            surveyId,
            origin.sectionServerId,
            question.serverId,
            {
              targetSectionId: finalHome.section.serverId,
              targetSetId: finalHome.set?.serverId ?? null,
              targetOrder: order,
            },
            question.rowVersion != null ? formatETag(question.rowVersion) : undefined
          )
            .then(({ data }) =>
              updateQuestionLocal(
                {
                  ...question,
                  rowVersion: data.rowVersion,
                  // FR-9.5: moving into a set strips routing server-side.
                  hasRoutingMap: finalHome.set ? false : question.hasRoutingMap,
                },
                false
              )
            )
            .catch((err) => fail(err, "surveysModule.builder.moveFailed"))
        }
        return next
      })
    },
    [sections, surveyId, addQuestion, addSet, containerOf, updateQuestionLocal, fail]
  )

  // ── Survey-level actions ───────────────────────────────────────────────────────

  /** FR-9.1 routing toggle — enable needs the confirmation modal; locks shuffle. */
  const applyRoutingToggle = useCallback(
    async (enabled: boolean, confirm: boolean) => {
      try {
        const { data, etag: nextEtag } = await toggleSurveyRouting(
          surveyId,
          enabled,
          confirm,
          etag ?? undefined
        )
        setSurvey(data)
        setEtagValue(nextEtag)
        setRoutingConfirmOpen(false)
      } catch (err) {
        if (err instanceof ETagConflictError) {
          setConflictOpen(true)
          return
        }
        fail(err, "surveysModule.routing.toggleFailed")
      }
    },
    [surveyId, etag, fail]
  )

  const doStatusChange = useCallback(
    async (to: SurveyStatus, confirm = false) => {
      setStatusBusy(true)
      try {
        const { data, etag: nextEtag } = await changeSurveyStatus(
          surveyId,
          { to, confirm },
          etag ?? undefined,
          confirm || to === "Draft" ? newIdempotencyKey() : undefined
        )
        setSurvey(data)
        setEtagValue(nextEtag)
        setConfirmState(null)
      } catch (err) {
        if (err instanceof SurveysApiError) {
          const details = (err.details ?? {}) as Record<string, unknown>
          if (err.code === "survey.return_to_draft.destructive_confirmation_required") {
            setConfirmState({
              kind: "destructive",
              to,
              count: Number(details.responsesCount ?? details.responses_count ?? 0),
            })
            return
          }
          if (err.code === "survey.pause.requires_rules_confirmation") {
            setConfirmState({
              kind: "pauseRules",
              to,
              count: Number(details.rulesCount ?? details.rules_count ?? 0),
            })
            return
          }
          fail(err, "surveysModule.builder.statusFailed")
          return
        }
        throw err
      } finally {
        setStatusBusy(false)
      }
    },
    [surveyId, etag, fail]
  )

  // Routing targets for the editor: standalone questions AFTER the source, in order.
  const routingTargets = useMemo<RoutingTargetOption[]>(() => {
    if (!routingTarget) return []
    const flat = sections.flatMap((s) => s.questions).filter((q) => q.type !== "Paragraph")
    const index = flat.findIndex((q) => q.localId === routingTarget.localId)
    return flat
      .slice(index + 1)
      .filter((q) => q.serverId != null)
      .map((q) => ({
        questionId: q.serverId as string,
        label: q.text || t("surveysModule.builder.untitledQuestion"),
      }))
  }, [sections, routingTarget, t])

  const settingsSet = useMemo(() => {
    if (!setSettingsFor) return null
    const section = sections.find((s) => s.localId === setSettingsFor.sectionLocal)
    const set = section?.sets.find((st) => st.localId === setSettingsFor.setLocal)
    return section && set ? { section, set } : null
  }, [sections, setSettingsFor])

  if (loading || !survey) {
    return (
      <div className="space-y-5 py-5">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-96 w-full" />
      </div>
    )
  }

  const routingLayoutOk = survey.layout === "question"

  return (
    <div className="space-y-5 py-5">
      <SurveyWizardStepper surveyId={surveyId} active="questions" />

      {/* Header: back + step title/subtitle, Cancel + Continue at the end */}
      <div className="flex items-start justify-between gap-4">
        <div className="flex min-w-0 items-start gap-3">
          <Button
            variant="outline"
            size="icon"
            className="mt-0.5 size-9 shrink-0"
            onClick={() => navigate(`/surveys/${surveyId}/settings`)}
            aria-label={t("common.back")}
          >
            <BackIcon className="size-4" aria-hidden />
          </Button>
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-2xl font-heading font-bold">
                {t("surveysModule.steps.questions")}
              </h1>
              <SurveyStatusPill status={survey.status} />
            </div>
            <p className="mt-1 max-w-2xl text-sm text-muted-foreground">
              {t("surveysModule.builder.subtitle")}
            </p>
          </div>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <Button variant="outline" onClick={() => navigate("/surveys")}>
            {t("common.cancel")}
          </Button>
          <Button
            className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            onClick={() => navigate(`/surveys/${surveyId}/appearance`)}
          >
            {t("surveysModule.settings.continue")}
            <NextIcon className="size-4" aria-hidden />
          </Button>
        </div>
      </div>

      {/* Toolbar row (reference parity): Translate · divider · Activate · routing */}
      <div className="flex flex-wrap items-center gap-4 rounded-lg border border-border bg-card px-4 py-2.5">
        <Button
          variant="outline"
          size="sm"
          onClick={() => navigate(`/surveys/${surveyId}/translate`)}
        >
          <Languages className="size-4" aria-hidden />
          {t("surveysModule.builder.translate")}
        </Button>
        <div className="h-5 w-px bg-border" aria-hidden />
        {/* Activate — Draft/Paused → Active (publish gate applies), Active → Paused. */}
        <PublishGateBanner blocked={survey.status !== "Active" && publishBlocked}>
          <div className="flex items-center gap-2">
            <Switch
              id="builder-activate"
              checked={survey.status === "Active"}
              disabled={
                statusBusy ||
                (survey.status !== "Active" && publishBlocked) ||
                !["Draft", "Active", "Paused"].includes(survey.status)
              }
              onCheckedChange={(on) => void doStatusChange(on ? "Active" : "Paused")}
            />
            <Label htmlFor="builder-activate" className="cursor-pointer text-sm font-normal">
              {t("surveysModule.builder.activate")}
            </Label>
          </div>
        </PublishGateBanner>
        {/* FR-9.1: enabled only for one-question-per-page; tooltip explains why. */}
        <TooltipProvider delay={150}>
          <Tooltip>
            <TooltipTrigger render={<span className="inline-flex items-center gap-2" />}>
              <Switch
                id="builder-routing"
                checked={survey.routingOn}
                disabled={!routingLayoutOk || statusBusy}
                onCheckedChange={(on) => {
                  if (on) setRoutingConfirmOpen(true)
                  else void applyRoutingToggle(false, false)
                }}
              />
              <Label htmlFor="builder-routing" className="cursor-pointer text-sm font-normal">
                <span className="flex items-center gap-1">
                  <RouteIcon className="size-4" aria-hidden />
                  {t("surveysModule.builder.routingToggle")}
                </span>
              </Label>
            </TooltipTrigger>
            {!routingLayoutOk && (
              <TooltipContent>{t("surveysModule.routing.layoutRequired")}</TooltipContent>
            )}
          </Tooltip>
        </TooltipProvider>
      </div>

      {apiError && (
        <div
          role="alert"
          className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {apiError.startsWith("surveysModule.") ? t(apiError) : apiError}
        </div>
      )}

      {/* Three-column canvas: palette · sections · config panel */}
      <DndContext
        sensors={sensors}
        collisionDetection={collision}
        onDragStart={onDragStart}
        onDragOver={onDragOver}
        onDragEnd={onDragEnd}
      >
        <div className="flex items-stretch gap-4" style={{ zoom: 0.9 }}>
          <div
            ref={paletteRef}
            className="w-64 shrink-0 self-start rounded-lg border border-border bg-card p-3 shadow-sm dark:shadow-none lg:sticky lg:top-20"
          >
            <QuestionPalette
              onAdd={(type) => addQuestion(type)}
              onAddSet={() => {
                const first = sections[0]
                if (first) addSet(first, true)
              }}
            />
          </div>

          <div className="min-w-0 flex-1 space-y-4 rounded-lg bg-transparent p-0">
            {sections.map((section, index) => (
              <SectionColumn
                key={section.localId}
                section={section}
                index={index}
                selectedId={selectedId}
                routingOn={survey.routingOn}
                canDelete={sections.length > 1}
                onRename={(title) => renameSection(section, title)}
                onDelete={(confirm) => removeSection(section, confirm)}
                onAddParagraph={() => addParagraph(section.localId)}
                onAddSectionAfter={() => addSection(section.localId)}
                onOpenSetSettings={(set) =>
                  setSetSettingsFor({ sectionLocal: section.localId, setLocal: set.localId })
                }
                onAddQuestionToSet={(set) =>
                  addQuestion("SingleSelect", section.localId, set.localId)
                }
                onRemoveSet={(set) => void removeSet(section, set, false)}
                onSelectQuestion={(q) => setSelectedId(q.localId)}
                onEditRouting={setRoutingTarget}
                onRemoveQuestion={(localId) => void removeQuestion(localId)}
              />
            ))}
            <button
              type="button"
              onClick={() => addSection()}
              className="flex w-full items-center justify-center gap-1.5 rounded-lg border border-dashed border-border py-3 text-sm font-medium text-muted-foreground transition-colors hover:border-primary hover:text-foreground"
            >
              <span aria-hidden>+</span>
              {t("surveysModule.builder.addSection")}
            </button>
          </div>

          {/* Config panel (reference parity): the selected question edits here. */}
          {/* Floors at the palette's measured height so both side columns match;
              stays sticky and content-sized beyond that. */}
          <div
            style={{ minHeight: paletteHeight }}
            className="flex w-72 shrink-0 flex-col self-start rounded-lg border border-border bg-card p-4 shadow-sm dark:shadow-none lg:sticky lg:top-20 xl:w-80"
          >
            {selectedQuestion && (
              <h2 className="mb-4 text-xs font-semibold uppercase tracking-widest text-muted-foreground">
                {t("surveysModule.builder.questionSettings")}
              </h2>
            )}
            {/* flex-1 lets the empty state centre in the full floored height. */}
            <div className="min-h-0 flex-1">
              <QuestionConfigPanel
                question={selectedQuestion}
                boundJourneyId={survey.boundJourneyId}
                onChange={updateQuestionLocal}
                onRemove={(localId) => void removeQuestion(localId)}
              />
            </div>
          </div>
        </div>

        <DragOverlay>
          {dragGhost !== null && (
            <div className="flex items-center gap-2 rounded-lg border border-primary bg-card px-3 py-2 text-sm font-medium shadow-md">
              <GripVertical className="size-4 text-muted-foreground" aria-hidden />
              <span className="max-w-56 truncate">{dragGhost}</span>
            </div>
          )}
        </DragOverlay>
      </DndContext>

      {/* Questions Set settings dialog (reference parity) */}
      <Dialog open={settingsSet !== null} onOpenChange={(o) => !o && setSetSettingsFor(null)}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{t("surveysModule.set.settingsTitle")}</DialogTitle>
            <DialogDescription>
              {settingsSet ? `${settingsSet.section.title} › ${settingsSet.set.title}` : ""}
            </DialogDescription>
          </DialogHeader>
          {settingsSet && (
            <div className="space-y-4">
              <div className="space-y-1.5">
                <Label htmlFor="set-title">{t("surveysModule.set.title")}</Label>
                <Input
                  id="set-title"
                  value={settingsSet.set.title}
                  onChange={(e) =>
                    changeSet(settingsSet.section, { ...settingsSet.set, title: e.target.value })
                  }
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="set-desc">
                  {t("surveysModule.set.description")}{" "}
                  <span className="font-normal text-muted-foreground">
                    {t("surveysModule.set.descriptionHint")}
                  </span>
                </Label>
                <Textarea
                  id="set-desc"
                  value={settingsSet.set.description}
                  onChange={(e) =>
                    changeSet(settingsSet.section, {
                      ...settingsSet.set,
                      description: e.target.value,
                    })
                  }
                  className="min-h-16"
                />
              </div>
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div className="space-y-1.5">
                  <Label htmlFor="set-mode">{t("surveysModule.set.selectionMode")}</Label>
                  <Select
                    value={settingsSet.set.selectionMode}
                    onValueChange={(v) =>
                      v &&
                      changeSet(settingsSet.section, {
                        ...settingsSet.set,
                        selectionMode: v as BuilderSet["selectionMode"],
                      })
                    }
                  >
                    <SelectTrigger id="set-mode" className="w-full">
                      <SelectValue>
                        {(v) =>
                          t(`surveysModule.set.mode_${String(v ?? settingsSet.set.selectionMode)}`)
                        }
                      </SelectValue>
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="random">{t("surveysModule.set.mode_random")}</SelectItem>
                      <SelectItem value="low_response">
                        {t("surveysModule.set.mode_low_response")}
                      </SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="set-count">{t("surveysModule.set.countLabel")}</Label>
                  <Input
                    id="set-count"
                    type="number"
                    min={1}
                    max={Math.max(1, settingsSet.set.questions.length)}
                    value={settingsSet.set.count}
                    onChange={(e) =>
                      changeSet(settingsSet.section, {
                        ...settingsSet.set,
                        count: Math.max(1, Number(e.target.value) || 1),
                      })
                    }
                    className="tabular-nums"
                  />
                  <p className="text-xs text-muted-foreground">
                    {t("surveysModule.set.countHelp", { n: settingsSet.set.questions.length })}
                  </p>
                </div>
              </div>
              {settingsSet.set.selectionMode === "low_response" && (
                <div className="flex items-start gap-2 rounded-md border border-border bg-accent p-3 text-xs leading-relaxed text-muted-foreground">
                  <Info className="mt-0.5 size-3.5 shrink-0 text-primary" aria-hidden />
                  <span>{t("surveysModule.set.lowResponseNote")}</span>
                </div>
              )}
            </div>
          )}
          <DialogFooter>
            <Button
              className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
              onClick={() => setSetSettingsFor(null)}
            >
              {t("surveysModule.set.done")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* US4: per-question routing editor */}
      <RoutingMapEditor
        open={routingTarget !== null}
        question={routingTarget}
        surveyId={surveyId}
        targets={routingTargets}
        onClose={() => setRoutingTarget(null)}
        onSaved={(localId, hasRouting, rowVersion) => {
          const home = findQuestionHome(localId)
          const q =
            home?.section.questions.find((x) => x.localId === localId) ??
            home?.set?.questions.find((x) => x.localId === localId)
          if (q)
            updateQuestionLocal(
              {
                ...q,
                hasRoutingMap: hasRouting,
                rowVersion: rowVersion ?? q.rowVersion,
              },
              false
            )
        }}
      />

      {/* FR-9.1 enable-routing confirmation */}
      <Dialog open={routingConfirmOpen} onOpenChange={(o) => !o && setRoutingConfirmOpen(false)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t("surveysModule.routing.confirmTitle")}</DialogTitle>
            <DialogDescription>{t("surveysModule.routing.confirmBody")}</DialogDescription>
          </DialogHeader>
          <DialogFooter className="gap-2 sm:gap-2">
            <Button variant="outline" onClick={() => setRoutingConfirmOpen(false)}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void applyRoutingToggle(true, true)}>
              {t("surveysModule.routing.confirmCta")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* FR-2.6 set delete confirmation (count from the 409 payload) */}
      <Dialog open={setDelete !== null} onOpenChange={(o) => !o && setSetDelete(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t("surveysModule.set.deleteTitle")}</DialogTitle>
            <DialogDescription>
              {t("surveysModule.set.deleteBody", { count: setDelete?.count ?? 0 })}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter className="gap-2 sm:gap-2">
            <Button variant="outline" onClick={() => setSetDelete(null)}>
              {t("common.cancel")}
            </Button>
            <Button
              variant="destructive"
              onClick={() => {
                const target = setDelete
                if (!target) return
                const owner = sections.find((s) =>
                  s.sets.some((st) => st.localId === target.set.localId)
                )
                if (owner) void removeSet(owner, target.set, true)
              }}
            >
              {t("surveysModule.set.deleteConfirm")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <DestructiveReturnToDraftDialog
        open={confirmState?.kind === "destructive"}
        responsesCount={confirmState?.count ?? 0}
        busy={statusBusy}
        onConfirm={() => confirmState && void doStatusChange(confirmState.to, true)}
        onCancel={() => setConfirmState(null)}
      />
      <PauseWithRulesDialog
        open={confirmState?.kind === "pauseRules"}
        rulesCount={confirmState?.count ?? 0}
        busy={statusBusy}
        onConfirm={() => confirmState && void doStatusChange(confirmState.to, true)}
        onCancel={() => setConfirmState(null)}
      />
      <EtagConflictDialog
        open={conflictOpen}
        localValues={{ routingOn: survey.routingOn }}
        onReload={() => {
          setConflictOpen(false)
          void load()
        }}
        onDismiss={() => setConflictOpen(false)}
      />
    </div>
  )
}
