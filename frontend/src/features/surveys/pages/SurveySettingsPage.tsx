// F3 Survey Settings (T088). Create-mode when surveyId === "new" (FR-5.5 — no row
// until Continue), edit-mode otherwise (row-click deep-link target, FR-1.4). Create
// mode renders the clickthrough wizard chrome: breadcrumb, stepper (Build method ✓ →
// Survey details → Questions → Design), back + Cancel header, Continue at the bottom
// (creates the Draft then lands in the builder). Survey name required; bound-journey
// select (M-16) drives the derived survey type (BR-3.3) with the no-journey advisory;
// welcome/thanks rich text (server-sanitised, Q3) + redirect link/delay; question
// layout select with the FR-3.3 warning dialog on `question`/`count`; active-period
// {days, hours}; shuffle box + shuffle mode. Saves POST-then-PUT with the Q1 ETag
// flow — a stale write opens EtagConflictDialog. Dirty forms arm useUnsavedChangesGuard.

import { useCallback, useEffect, useMemo, useState } from "react"
import { useNavigate, useSearchParams } from "react-router"
import { useTranslation } from "react-i18next"
import {
  AlertTriangle,
  ArrowLeft,
  ArrowRight,
  BellRing,
  Loader2,
  MessageSquare,
  Minus,
  Plus,
  Send,
  Settings2,
  SlidersHorizontal,
  Undo2,
} from "lucide-react"

import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
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
import { Sheet, SheetContent, SheetFooter, SheetHeader, SheetTitle } from "@/components/ui/sheet"
import { Skeleton } from "@/components/ui/skeleton"
import { Switch } from "@/components/ui/switch"
import { Textarea } from "@/components/ui/textarea"
import { useDirection } from "@/hooks/use-direction"
import { useSession } from "@/features/auth/hooks/useSession"
import { listJourneys, type JourneySummary } from "@/features/journeys/api"
import { ETagConflictError } from "../api/etag"
import {
  createSurvey,
  getSurvey,
  newIdempotencyKey,
  publishSurvey,
  returnSurveyToDraft,
  updateSurvey,
  type LayoutMode,
  type SurveyDraftInput,
  type SurveyView,
} from "../api/surveys-api"
import { EtagConflictDialog } from "../components/EtagConflictDialog"
import { RichTextEditor } from "../components/RichTextEditor"
import { SurveyStatusPill } from "../components/SurveyStatusPill"
import { SurveyWizardStepper } from "../components/SurveyWizardStepper"
import { useSurveyEditLock } from "../hooks/useSurveyEditLock"
import { useSurveyEtag } from "../hooks/useSurveyEtag"
import { useUnsavedChangesGuard } from "../hooks/useUnsavedChangesGuard"

interface FormState {
  nameEn: string
  description: string
  boundJourneyId: string | null
  welcomeHtml: string
  thanksHtml: string
  redirectUrl: string
  redirectAfterS: string
  layout: LayoutMode
  questionsPerPage: string
  activeDays: string
  activeHours: string
  shuffle: boolean
  shuffleMode: string
  routingOn: boolean
}

const EMPTY_FORM: FormState = {
  nameEn: "",
  description: "",
  boundJourneyId: null,
  welcomeHtml: "",
  thanksHtml: "",
  redirectUrl: "",
  redirectAfterS: "5",
  layout: "section",
  questionsPerPage: "3",
  activeDays: "",
  activeHours: "",
  shuffle: false,
  shuffleMode: "random",
  routingOn: false,
}

function toForm(v: SurveyView): FormState {
  return {
    nameEn: v.nameEn,
    description: v.description ?? "",
    boundJourneyId: v.boundJourneyId,
    welcomeHtml: v.welcomeHtml ?? "",
    thanksHtml: v.thanksHtml ?? "",
    redirectUrl: v.redirectUrl ?? "",
    redirectAfterS: String(v.redirectAfterS ?? 5),
    layout: v.layout,
    questionsPerPage: v.questionsPerPage != null ? String(v.questionsPerPage) : "3",
    activeDays: v.activePeriod ? String(v.activePeriod.days) : "",
    activeHours: v.activePeriod ? String(v.activePeriod.hours) : "",
    shuffle: v.shuffle,
    shuffleMode: v.shuffleMode,
    routingOn: v.routingOn,
  }
}

function toInput(f: FormState): SurveyDraftInput {
  const days = f.activeDays === "" ? 0 : Number(f.activeDays)
  const hours = f.activeHours === "" ? 0 : Number(f.activeHours)
  return {
    nameEn: f.nameEn.trim(),
    description: f.description || null,
    boundJourneyId: f.boundJourneyId,
    welcomeHtml: f.welcomeHtml || null,
    thanksHtml: f.thanksHtml || null,
    redirectUrl: f.redirectUrl.trim() || null,
    redirectAfterS: f.redirectAfterS === "" ? 5 : Number(f.redirectAfterS),
    layout: f.layout,
    questionsPerPage: f.layout === "count" && f.questionsPerPage !== "" ? Number(f.questionsPerPage) : null,
    activePeriod: days === 0 && hours === 0 ? null : { days, hours },
    shuffle: f.shuffle,
    shuffleMode: f.shuffleMode,
    routingOn: f.routingOn,
  }
}

const LAYOUT_MODES: LayoutMode[] = ["single", "section", "question", "count"]

function SectionTitle({
  icon: Icon,
  children,
}: {
  icon: typeof Settings2
  children: React.ReactNode
}) {
  return (
    <div className="flex items-center gap-2">
      <Icon className="size-4 text-muted-foreground" aria-hidden />
      <h2 className="text-base font-bold">{children}</h2>
    </div>
  )
}


export default function SurveySettingsPage({ surveyId }: { surveyId: string }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { isRtl } = useDirection()
  const sheetSide = isRtl ? ("left" as const) : ("right" as const)
  const BackIcon = isRtl ? ArrowRight : ArrowLeft
  const NextIcon = isRtl ? ArrowLeft : ArrowRight
  const { session } = useSession()
  const [searchParams] = useSearchParams()
  // M-09 reviewer-notification deep-link: /surveys/{id}?from=review-notification
  const fromReviewNotification = searchParams.get("from") === "review-notification"
  const isCreate = surveyId === "new"

  const [survey, setSurvey] = useState<SurveyView | null>(null)
  const [form, setForm] = useState<FormState>(EMPTY_FORM)
  const [baseline, setBaseline] = useState<FormState>(EMPTY_FORM)
  const [loading, setLoading] = useState(!isCreate)
  const [saving, setSaving] = useState(false)
  const [nameError, setNameError] = useState(false)
  const [conflictOpen, setConflictOpen] = useState(false)
  const [layoutWarning, setLayoutWarning] = useState<LayoutMode | null>(null)
  const [journeys, setJourneys] = useState<JourneySummary[]>([])
  // US2 reviewer actions (T122)
  const [returnSheetOpen, setReturnSheetOpen] = useState(false)
  const [remarks, setRemarks] = useState("")
  const [remarksError, setRemarksError] = useState(false)
  const [reviewBusy, setReviewBusy] = useState(false)

  const { captureFrom, withIfMatch, setEtag } = useSurveyEtag()
  const isDirty = useMemo(
    () => JSON.stringify(form) !== JSON.stringify(baseline),
    [form, baseline]
  )
  useUnsavedChangesGuard(isDirty && !saving)

  const editLock = useSurveyEditLock(survey ? { status: survey.status } : { status: "Draft" })
  const locked = !isCreate && survey !== null && !editLock.canEdit
  // BR-15.2: the reviewer (P-01) publishes or returns a PendingReview survey.
  const isReviewer = session?.persona === "P-01"
  const showReviewActions = !isCreate && survey?.status === "PendingReview" && isReviewer

  const load = useCallback(async () => {
    if (isCreate) return
    setLoading(true)
    try {
      const view = await captureFrom(() => getSurvey(surveyId))
      setSurvey(view)
      const f = toForm(view)
      setForm(f)
      setBaseline(f)
    } finally {
      setLoading(false)
    }
  }, [isCreate, surveyId, captureFrom])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    listJourneys({ pageSize: 200 })
      .then((r) => setJourneys(r.items))
      .catch(() => setJourneys([]))
  }, [])

  // BR-3.3 — the type is derived, never user-set: journey bound ⇒ Transactional.
  const derivedType = form.boundJourneyId ? "Transactional" : "SeasonalRelational"

  const set = <K extends keyof FormState>(key: K, value: FormState[K]) =>
    setForm((f) => ({ ...f, [key]: value }))

  const applyLayout = (layout: LayoutMode) => {
    // FR-3.3: per-question / count layouts get a comprehension warning first.
    if (layout === "question" || layout === "count") setLayoutWarning(layout)
    else set("layout", layout)
  }

  const qpp = form.questionsPerPage === "" ? 3 : Number(form.questionsPerPage)

  const save = async (afterCreatePath: "builder" | "appearance" = "builder", goNext = false) => {
    if (form.nameEn.trim() === "") {
      setNameError(true)
      document.getElementById("survey-name")?.focus()
      return
    }
    setNameError(false)
    setSaving(true)
    try {
      if (isCreate) {
        const { data, etag } = await createSurvey(toInput(form), newIdempotencyKey())
        setEtag(etag)
        setBaseline(form)
        // The wizard keeps its "Build method" segment for the rest of THIS create
        // flow (cleared when the library mounts) — edit visits never show it.
        sessionStorage.setItem("surveys.createFlow", data.id)
        // Wizard flow: Continue lands on the Questions step (the builder); a
        // stepper jump to Design lands on Appearance instead.
        navigate(`/surveys/${data.id}/${afterCreatePath}`, { replace: true })
      } else {
        const view = await withIfMatch((ifMatch) => updateSurvey(surveyId, toInput(form), ifMatch))
        setSurvey(view)
        const f = toForm(view)
        setForm(f)
        setBaseline(f)
        // Continue (wizard flow) saves then advances to the Questions step.
        if (goNext) navigate(`/surveys/${surveyId}/builder`)
      }
    } catch (err) {
      if (err instanceof ETagConflictError) {
        setConflictOpen(true)
        return
      }
      throw err
    } finally {
      setSaving(false)
    }
  }

  // US2 (T122): reviewer publishes → Active; reload picks up the new status + ETag.
  const doPublish = async () => {
    setReviewBusy(true)
    try {
      await withIfMatch((ifMatch) =>
        publishSurvey(surveyId, undefined, ifMatch, newIdempotencyKey())
      )
      await load()
    } catch (err) {
      if (err instanceof ETagConflictError) {
        setConflictOpen(true)
        return
      }
      throw err
    } finally {
      setReviewBusy(false)
    }
  }

  // FR-15.3: return to Draft with required remarks (400 …remarks_required when blank).
  const doReturnToDraft = async () => {
    if (remarks.trim() === "") {
      setRemarksError(true)
      document.getElementById("review-remarks")?.focus()
      return
    }
    setRemarksError(false)
    setReviewBusy(true)
    try {
      await withIfMatch((ifMatch) => returnSurveyToDraft(surveyId, remarks.trim(), ifMatch))
      setReturnSheetOpen(false)
      setRemarks("")
      await load()
    } catch (err) {
      if (err instanceof ETagConflictError) {
        setConflictOpen(true)
        return
      }
      throw err
    } finally {
      setReviewBusy(false)
    }
  }

  if (loading) {
    return (
      <div className="space-y-5 py-5">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-96 w-full" />
      </div>
    )
  }

  const continueButton = (
    <Button
      className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
      onClick={() => {
        // A locked survey (review/active) can't save — Continue just advances.
        if (locked) navigate(`/surveys/${surveyId}/builder`)
        else void save("builder", true)
      }}
      disabled={saving}
    >
      {saving && <Loader2 className="size-4 animate-spin" aria-hidden />}
      {t("surveysModule.settings.continue")}
      <NextIcon className="size-4" aria-hidden />
    </Button>
  )

  return (
    <div className="space-y-5 py-5">
      {isCreate ? (
        <>
          <p className="text-xs text-muted-foreground">
            {t("surveysModule.settings.breadcrumb")}
          </p>
          {/* Free step navigation: jumping to Questions/Design saves the draft
              first (a row must exist), then lands on that step's page. */}
          <SurveyWizardStepper
            surveyId={null}
            active="details"
            onCreateStep={(path) => void save(path)}
          />
        </>
      ) : (
        <SurveyWizardStepper surveyId={surveyId} active="details" />
      )}

      {/* Header */}
      {isCreate ? (
        <div className="flex items-start justify-between gap-4">
          <div className="flex min-w-0 items-start gap-3">
            <Button
              variant="outline"
              size="icon"
              className="mt-0.5 size-9 shrink-0"
              onClick={() => navigate("/surveys/new")}
              aria-label={t("common.back")}
            >
              <BackIcon className="size-4" aria-hidden />
            </Button>
            <div className="min-w-0">
              <h1 className="text-2xl font-heading font-bold">
                {t("surveysModule.settings.createTitle")}
              </h1>
              <p className="mt-1 max-w-2xl text-sm text-muted-foreground">
                {t("surveysModule.settings.subtitle")}
              </p>
            </div>
          </div>
          <Button
            variant="outline"
            className="shrink-0"
            onClick={() => navigate("/surveys")}
          >
            {t("common.cancel")}
          </Button>
        </div>
      ) : (
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-2xl font-heading font-bold">{form.nameEn}</h1>
              {survey && <SurveyStatusPill status={survey.status} />}
            </div>
            <p className="mt-1 text-sm text-muted-foreground">
              {t("surveysModule.settings.subtitle")}
            </p>
          </div>
          <div className="flex items-center gap-2">
            {/* Reference layout: Cancel up top, Continue at the bottom of the form.
                During review, Publish stays THE filled primary (one-blue rule). */}
            {showReviewActions && (
              <>
                <Button variant="secondary" onClick={() => setReturnSheetOpen(true)} disabled={reviewBusy}>
                  <Undo2 className="size-4" aria-hidden />
                  {t("surveysModule.review.returnToDraft")}
                </Button>
                <Button
                  className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
                  onClick={() => void doPublish()}
                  disabled={reviewBusy}
                >
                  {reviewBusy && <Loader2 className="size-4 animate-spin" aria-hidden />}
                  <Send className="size-4" aria-hidden />
                  {t("surveysModule.review.publish")}
                </Button>
              </>
            )}
            <Button variant="outline" onClick={() => navigate("/surveys")}>
              {t("common.cancel")}
            </Button>
          </div>
        </div>
      )}

      {fromReviewNotification && survey?.status === "PendingReview" && (
        <div className="flex items-center gap-2 rounded-md border border-nb-cyan-200 bg-nb-cyan-100/50 px-3 py-2 text-sm text-nb-cyan-800 dark:border-nb-cyan-800 dark:bg-nb-cyan-900/25 dark:text-nb-cyan-200">
          <BellRing className="size-4 shrink-0" aria-hidden />
          {t("surveysModule.review.fromNotification")}
        </div>
      )}

      {locked && (
        <div
          role="alert"
          className="rounded-md border border-d3-dark/20 bg-d3-light px-3 py-2 text-sm text-d3-dark dark:bg-d3-dark/25 dark:text-d3-light"
        >
          {t(`surveysModule.editLock.${editLock.reason ?? "pending_review"}`)}
        </div>
      )}

      {/* ── Card 1: Survey settings ─────────────────────────────────────── */}
      <Card>
        <CardContent className="space-y-4 px-6">
          <SectionTitle icon={Settings2}>
            {t("surveysModule.settings.basicsTitle")}
          </SectionTitle>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-1.5 md:col-span-2">
              <Label htmlFor="survey-name">
                {t("surveysModule.settings.nameEn")} <span className="text-destructive">*</span>
              </Label>
              <Input
                id="survey-name"
                value={form.nameEn}
                maxLength={200}
                onChange={(e) => set("nameEn", e.target.value)}
                placeholder={t("surveysModule.settings.namePlaceholder")}
                disabled={locked}
                aria-invalid={nameError}
              />
              <p className="text-xs text-muted-foreground">
                {t("surveysModule.settings.nameHint")}
              </p>
              {nameError && (
                <p className="text-sm text-destructive" role="alert">
                  {t("surveysModule.settings.nameRequired")}
                </p>
              )}
            </div>

            <div className="space-y-1.5 md:col-span-2">
              <Label htmlFor="survey-desc">
                {t("surveysModule.settings.description")}{" "}
                <span className="font-normal text-muted-foreground">
                  {t("surveysModule.settings.internalSuffix")}
                </span>
              </Label>
              <Textarea
                id="survey-desc"
                value={form.description}
                onChange={(e) => set("description", e.target.value)}
                placeholder={t("surveysModule.settings.descPlaceholder")}
                disabled={locked}
                className="min-h-20"
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="survey-type">{t("surveysModule.settings.surveyType")}</Label>
              {/* Derived from the journey binding, never user-set (BR-3.3) — the
                  select renders the derived value read-only. */}
              <Select value={derivedType} disabled>
                <SelectTrigger id="survey-type" className="w-full">
                  <SelectValue>
                    {() => t(`surveysModule.type.${derivedType}`)}
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Transactional">
                    {t("surveysModule.type.Transactional")}
                  </SelectItem>
                  <SelectItem value="SeasonalRelational">
                    {t("surveysModule.type.SeasonalRelational")}
                  </SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="survey-journey">
                {t("surveysModule.settings.boundJourney")}{" "}
                <span className="font-normal text-muted-foreground">
                  {t("surveysModule.settings.optionalSuffix")}
                </span>
              </Label>
              <Select
                value={form.boundJourneyId ?? "none"}
                onValueChange={(v) => set("boundJourneyId", v === "none" ? null : v)}
                disabled={locked}
              >
                <SelectTrigger id="survey-journey" className="w-full">
                  <SelectValue>
                    {(v) =>
                      v && v !== "none"
                        ? (journeys.find((j) => j.journeyId === v)?.name ?? String(v))
                        : t("surveysModule.settings.noJourney")
                    }
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">{t("surveysModule.settings.noJourney")}</SelectItem>
                  {journeys.map((j) => (
                    <SelectItem key={j.journeyId} value={j.journeyId}>
                      {j.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          {/* Seasonal/Relational advisory — only the no-journey case can occur since
              the type is derived (a bound journey is always Transactional). */}
          {derivedType === "SeasonalRelational" && (
            <div className="flex items-start gap-2 rounded-md border border-border bg-accent p-3 text-xs text-muted-foreground">
              <AlertTriangle className="mt-0.5 size-3.5 shrink-0 text-primary" aria-hidden />
              <span className="leading-relaxed">
                {t("surveysModule.settings.advisoryPre")}{" "}
                <b className="text-foreground">{t("surveysModule.type.SeasonalRelational")}</b>{" "}
                {t("surveysModule.settings.advisoryPost")}
              </span>
            </div>
          )}
        </CardContent>
      </Card>

      {/* ── Card 2: Respondent messages ─────────────────────────────────── */}
      <Card>
        <CardContent className="space-y-4 px-6">
          <SectionTitle icon={MessageSquare}>
            {t("surveysModule.settings.messagesTitle")}
          </SectionTitle>

          <RichTextEditor
            id="survey-welcome"
            label={t("surveysModule.settings.welcome")}
            hint={t("surveysModule.settings.welcomeHint")}
            value={form.welcomeHtml}
            onChange={(html) => set("welcomeHtml", html)}
            disabled={locked}
          />
          <RichTextEditor
            id="survey-thanks"
            label={t("surveysModule.settings.thanks")}
            hint={t("surveysModule.settings.thanksHint")}
            value={form.thanksHtml}
            onChange={(html) => set("thanksHtml", html)}
            disabled={locked}
          />

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="survey-redirect">
                {t("surveysModule.settings.redirectLink")}{" "}
                <span className="font-normal text-muted-foreground">{t("common.optional")}</span>
              </Label>
              <Input
                id="survey-redirect"
                dir="ltr"
                value={form.redirectUrl}
                onChange={(e) => set("redirectUrl", e.target.value)}
                placeholder="https://bank.example/thanks"
                disabled={locked}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="survey-redirect-after">
                {t("surveysModule.settings.redirectAfter")}
              </Label>
              <div className="flex items-center gap-2">
                <Input
                  id="survey-redirect-after"
                  type="number"
                  min={0}
                  className="w-24 tabular-nums"
                  value={form.redirectAfterS}
                  onChange={(e) => set("redirectAfterS", e.target.value)}
                  disabled={locked}
                />
                <span className="text-sm text-muted-foreground">
                  {t("surveysModule.settings.seconds")}
                </span>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* ── Card 3: Collection behaviour ────────────────────────────────── */}
      <Card>
        <CardContent className="space-y-4 px-6">
          <SectionTitle icon={SlidersHorizontal}>
            {t("surveysModule.settings.behaviourTitle")}
          </SectionTitle>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="survey-layout">{t("surveysModule.settings.layout")}</Label>
              <Select
                value={form.layout}
                onValueChange={(v) => applyLayout(v as LayoutMode)}
                disabled={locked}
              >
                <SelectTrigger id="survey-layout" className="w-full">
                  <SelectValue>
                    {(v) => t(`surveysModule.layout.${String(v ?? form.layout)}`)}
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  {LAYOUT_MODES.map((m) => (
                    <SelectItem key={m} value={m}>
                      {t(`surveysModule.layout.${m}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                {t("surveysModule.settings.layoutHint")}
              </p>
              {form.layout === "count" && (
                <div className="space-y-1.5 pt-1">
                  <Label>{t("surveysModule.settings.questionsPerPage")}</Label>
                  <div className="inline-flex items-center rounded-md border border-input bg-card">
                    <button
                      type="button"
                      aria-label={t("surveysModule.settings.decrease")}
                      onClick={() => set("questionsPerPage", String(Math.max(1, qpp - 1)))}
                      disabled={locked}
                      className="inline-flex size-9 items-center justify-center rounded-s-md text-muted-foreground transition-colors hover:bg-accent hover:text-foreground"
                    >
                      <Minus className="size-4" aria-hidden />
                    </button>
                    <span className="w-10 text-center text-sm font-semibold tabular-nums">
                      {qpp}
                    </span>
                    <button
                      type="button"
                      aria-label={t("surveysModule.settings.increase")}
                      onClick={() => set("questionsPerPage", String(qpp + 1))}
                      disabled={locked}
                      className="inline-flex size-9 items-center justify-center rounded-e-md text-muted-foreground transition-colors hover:bg-accent hover:text-foreground"
                    >
                      <Plus className="size-4" aria-hidden />
                    </button>
                  </div>
                </div>
              )}
            </div>

            <div className="space-y-1.5">
              <Label>
                {t("surveysModule.settings.activePeriod")}{" "}
                <span className="font-normal text-muted-foreground">
                  {t("surveysModule.settings.optionalSuffix")}
                </span>
              </Label>
              <div className="flex items-center gap-2">
                <Input
                  type="number"
                  min={0}
                  className="tabular-nums"
                  value={form.activeDays}
                  onChange={(e) => set("activeDays", e.target.value)}
                  placeholder={t("surveysModule.settings.days")}
                  aria-label={t("surveysModule.settings.days")}
                  disabled={locked}
                />
                <Input
                  type="number"
                  min={0}
                  max={23}
                  className="tabular-nums"
                  value={form.activeHours}
                  onChange={(e) => set("activeHours", e.target.value)}
                  placeholder={t("surveysModule.settings.hours")}
                  aria-label={t("surveysModule.settings.hours")}
                  disabled={locked}
                />
              </div>
              <p className="text-xs text-muted-foreground">
                {t("surveysModule.settings.activePeriodHint")}
              </p>
            </div>
          </div>

          {/* Shuffle */}
          <div className="space-y-4 rounded-md border border-border p-4">
            <div className="flex items-start justify-between gap-4">
              <div className="min-w-0">
                <p className="text-sm font-semibold">{t("surveysModule.settings.shuffle")}</p>
                <p className="mt-0.5 text-xs text-muted-foreground">
                  {t("surveysModule.settings.shuffleHint")}
                </p>
              </div>
              <Switch
                id="survey-shuffle"
                checked={form.shuffle}
                onCheckedChange={(v) => set("shuffle", v)}
                disabled={locked}
                aria-label={t("surveysModule.settings.shuffle")}
              />
            </div>
            {form.shuffle && (
              <div className="max-w-sm space-y-1.5">
                <Label htmlFor="survey-shuffle-mode">
                  {t("surveysModule.settings.shuffleMode")}
                </Label>
                <Select
                  value={form.shuffleMode}
                  onValueChange={(v) => set("shuffleMode", v ?? "random")}
                  disabled={locked}
                >
                  <SelectTrigger id="survey-shuffle-mode" className="w-full">
                    <SelectValue>
                      {(v) => t(`surveysModule.shuffleMode.${String(v ?? form.shuffleMode)}`)}
                    </SelectValue>
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="random">{t("surveysModule.shuffleMode.random")}</SelectItem>
                    <SelectItem value="sections">
                      {t("surveysModule.shuffleMode.sections")}
                    </SelectItem>
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">
                  {t("surveysModule.settings.shuffleModeHint")}
                </p>
              </div>
            )}
          </div>

          {/* Question routing toggle — intentionally hidden for now (owner request);
              routing lives in the builder step in the reference flow. The routingOn
              field stays in FormState so existing values round-trip untouched.
          <div className="flex items-center justify-between gap-3">
            <div className="min-w-0">
              <Label htmlFor="survey-routing" className="cursor-pointer">
                {t("surveysModule.settings.routing")}
              </Label>
              <p className="mt-0.5 text-sm text-muted-foreground">
                {t("surveysModule.settings.routingHint")}
              </p>
            </div>
            <Switch
              id="survey-routing"
              checked={form.routingOn}
              onCheckedChange={(v) => set("routingOn", v)}
              disabled={locked}
            />
          </div>
          */}
        </CardContent>
      </Card>

      <div className="flex justify-end">{continueButton}</div>

      {/* FR-3.3 layout comprehension warning */}
      <Dialog open={layoutWarning !== null} onOpenChange={(o) => !o && setLayoutWarning(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t("surveysModule.settings.layoutWarningTitle")}</DialogTitle>
            <DialogDescription className="leading-relaxed">
              {t("surveysModule.settings.layoutWarningBody")}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter className="gap-2 sm:gap-2">
            <Button variant="outline" onClick={() => setLayoutWarning(null)}>
              {t("common.cancel")}
            </Button>
            <Button
              className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
              onClick={() => {
                if (layoutWarning) set("layout", layoutWarning)
                setLayoutWarning(null)
              }}
            >
              {t("surveysModule.settings.layoutWarningOk")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* FR-15.3 Return-to-draft with reviewer remarks */}
      <Sheet open={returnSheetOpen} onOpenChange={(o) => !o && setReturnSheetOpen(false)}>
        <SheetContent side={sheetSide} className="w-full sm:max-w-md">
          <SheetHeader className="shrink-0">
            <SheetTitle>{t("surveysModule.review.returnTitle")}</SheetTitle>
          </SheetHeader>
          <div className="min-h-0 flex-1 space-y-4 overflow-y-auto px-4">
            <p className="text-sm text-muted-foreground">
              {t("surveysModule.review.returnHint")}
            </p>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="review-remarks">
                {t("surveysModule.review.remarks")} <span className="text-destructive">*</span>
              </Label>
              <Textarea
                id="review-remarks"
                rows={5}
                value={remarks}
                onChange={(e) => setRemarks(e.target.value)}
                aria-invalid={remarksError}
              />
              {remarksError && (
                <p className="text-sm text-destructive" role="alert">
                  {t("surveysModule.review.remarksRequired")}
                </p>
              )}
            </div>
          </div>
          <SheetFooter className="shrink-0 gap-2">
            <Button variant="outline" onClick={() => setReturnSheetOpen(false)} disabled={reviewBusy}>
              {t("common.cancel")}
            </Button>
            <Button
              className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
              onClick={() => void doReturnToDraft()}
              disabled={reviewBusy}
            >
              {reviewBusy && <Loader2 className="size-4 animate-spin" aria-hidden />}
              {t("surveysModule.review.returnCta")}
            </Button>
          </SheetFooter>
        </SheetContent>
      </Sheet>

      <EtagConflictDialog
        open={conflictOpen}
        localValues={form as unknown as Record<string, unknown>}
        onReload={() => {
          setConflictOpen(false)
          void load()
        }}
        onDismiss={() => setConflictOpen(false)}
      />
    </div>
  )
}
