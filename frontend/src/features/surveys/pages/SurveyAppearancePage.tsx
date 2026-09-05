// F4 Design step (T089, reworked for clickthrough parity): two mode radio cards
// (Customize this survey / Use Tenant Design Guidelines) above a split pane —
// controls on the start side, LivePreviewFrame with the four channel tabs on the end
// (FR-4.2). Header carries Cancel / Preview (toggles preview-full, hiding controls) /
// Save survey. Inherited mode resolves the palette from the tenant design guidelines
// and locks controls; Customize unlocks. Theme edits update the preview instantly via
// local state (SC-003 ~100ms). Chrome uses nb-* brand tokens only (Two-Palette Rule).

import { useEffect, useMemo, useState } from "react"
import { useNavigate } from "react-router"
import { useTranslation } from "react-i18next"
import { toast } from "sonner"
import { ArrowLeft, ArrowRight, Check, Eye, Loader2 } from "lucide-react"

import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"
import {
  getSurvey,
  getSurveyTheme,
  updateSurveyTheme,
  uploadSurveyThemeLogo,
  type SurveyView,
  type ThemeMode,
} from "../api/surveys-api"
import type { AppearanceState } from "../components/AppearanceControls"
import {
  LivePreviewFrame,
  type PreviewDevice,
  type PreviewQuestionLite,
} from "../components/LivePreviewFrame"
import { getSurveyPreview } from "../api/preview-api"
import { useDirection } from "@/hooks/use-direction"
import {
  DEFAULT_THEME,
  SurveyDesignControls,
  type SurveyThemeDraft,
} from "../components/SurveyDesignControls"
import { SurveyWizardStepper } from "../components/SurveyWizardStepper"
import { useUnsavedChangesGuard } from "../hooks/useUnsavedChangesGuard"
import { resolveCssColorVar } from "@/lib/resolve-css-color"

/** The platform's stock primary — an API theme row holding this value is "unset". */
const STOCK_PRIMARY = "#0D8BBC"

const DEFAULT_STATE: AppearanceState = {
  mode: "Inherited",
  backgroundType: "Solid",
  primaryColour: "#0D8BBC",
  logoFileName: null,
}

/** Reference-style mode radio cards — Customize first, Tenant Guidelines second. */
function ModeCards({
  mode,
  onChange,
  disabled,
}: {
  mode: ThemeMode
  onChange: (mode: ThemeMode) => void
  disabled?: boolean
}) {
  const { t } = useTranslation()
  const cards: { value: ThemeMode; titleKey: string; descKey: string }[] = [
    {
      value: "Customized",
      titleKey: "surveysModule.appearance.customizeTitle",
      descKey: "surveysModule.appearance.customizeDesc",
    },
    {
      value: "Inherited",
      titleKey: "surveysModule.appearance.inheritedTitle",
      descKey: "surveysModule.appearance.inheritedDesc",
    },
  ]
  return (
    <div role="radiogroup" className="grid grid-cols-1 gap-3 sm:grid-cols-2">
      {cards.map((card) => {
        const active = mode === card.value
        return (
          <button
            key={card.value}
            type="button"
            role="radio"
            aria-checked={active}
            onClick={() => onChange(card.value)}
            disabled={disabled}
            className={cn(
              "flex items-start gap-3 rounded-lg border p-4 text-start transition-colors",
              active ? "border-primary bg-primary/5" : "border-border hover:bg-accent"
            )}
          >
            <span
              className={cn(
                "mt-0.5 flex size-4 shrink-0 items-center justify-center rounded-full border-2",
                active ? "border-primary" : "border-muted-foreground/40"
              )}
              aria-hidden
            >
              {active && <span className="size-2 rounded-full bg-primary" />}
            </span>
            <span className="min-w-0">
              <span className="block text-sm font-bold">{t(card.titleKey)}</span>
              <span className="mt-0.5 block text-xs text-muted-foreground">
                {t(card.descKey)}
              </span>
            </span>
          </button>
        )
      })}
    </div>
  )
}

export default function SurveyAppearancePage({ surveyId }: { surveyId: string }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { isRtl } = useDirection()
  const BackIcon = isRtl ? ArrowRight : ArrowLeft
  const [survey, setSurvey] = useState<SurveyView | null>(null)
  const [state, setState] = useState<AppearanceState>(DEFAULT_STATE)
  const [baseline, setBaseline] = useState<AppearanceState>(DEFAULT_STATE)
  // Full design-controls draft — the backend persists the {mode, backgroundType,
  // primaryColour, logo} subset; the remaining keys drive the live preview.
  const [theme, setTheme] = useState<SurveyThemeDraft>(DEFAULT_THEME)
  const [inheritedColour, setInheritedColour] = useState<string>(DEFAULT_STATE.primaryColour)
  const [device, setDevice] = useState<PreviewDevice>("desktop")
  const [pendingLogo, setPendingLogo] = useState<File | null>(null)
  // The survey's real questions, rendered live in the themed preview.
  const [previewQuestions, setPreviewQuestions] = useState<PreviewQuestionLite[] | undefined>()
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState(false)
  // Preview mode: hide the controls column, widen the preview (reference parity).
  // The library's eye icon deep-links here with ?preview=1 (reference behavior).
  const [previewFull, setPreviewFull] = useState(
    () => new URLSearchParams(window.location.search).get("preview") === "1"
  )

  const isDirty = JSON.stringify(state) !== JSON.stringify(baseline) || pendingLogo !== null
  useUnsavedChangesGuard(isDirty && !saving)

  // The tenant theme lands as CSS variables (--primary) — resolve it once so the
  // preview's default/inherited colors follow the tenant instead of stock cyan.
  const tenantPrimary = useMemo(() => resolveCssColorVar("--primary", STOCK_PRIMARY), [])

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    Promise.all([getSurvey(surveyId), getSurveyTheme(surveyId)])
      .then(([{ data: view }, themeRow]) => {
        if (cancelled) return
        setSurvey(view)
        // Inherited = the organization's branding — always the TENANT primary.
        setInheritedColour(tenantPrimary)
        // A stock-cyan theme row means "never customized" — seed from the tenant.
        const seedColour =
          themeRow.primaryColour === STOCK_PRIMARY ? tenantPrimary : themeRow.primaryColour
        const next: AppearanceState = {
          mode: view.themeMode,
          backgroundType: "Solid",
          primaryColour: seedColour,
          logoFileName: null,
        }
        setState(next)
        setBaseline(next)
        setTheme((cur) => ({
          ...cur,
          primary: seedColour,
          buttonColor: seedColour,
          btnBorder: seedColour,
        }))
      })
      .finally(() => !cancelled && setLoading(false))
    // Real questions for the live preview — best-effort (sample renders if absent).
    getSurveyPreview(surveyId, "desktop", "en")
      .then((p) => {
        if (cancelled) return
        setPreviewQuestions(
          p.questions.map((q) => ({
            text: q.text || t("surveysModule.builder.untitledQuestion"),
            type: q.type,
            required: q.required,
            options: ((q.payload as Record<string, unknown> | null)?.options as string[]) ?? [],
            points: Number((q.payload as Record<string, unknown> | null)?.pointCount ?? 5),
          }))
        )
      })
      .catch(() => !cancelled && setPreviewQuestions(undefined))
    return () => {
      cancelled = true
    }
  }, [surveyId, t])

  // Inherited mode always previews the tenant-guideline colour, not stale edits.
  const effectiveColour = state.mode === "Inherited" ? inheritedColour : state.primaryColour
  const locked = state.mode === "Inherited"
  // Locked mode previews the tenant guideline theme, not the draft edits.
  const effectiveTheme: SurveyThemeDraft = locked
    ? { ...DEFAULT_THEME, primary: inheritedColour, buttonColor: inheritedColour, btnBorder: inheritedColour }
    : theme

  /** Theme edits — mirror the persistable keys into the save subset. */
  const onThemeChange = <K extends keyof SurveyThemeDraft>(key: K, value: SurveyThemeDraft[K]) => {
    setTheme((cur) => ({ ...cur, [key]: value }))
    if (key === "primary")
      setState((s) => ({ ...s, primaryColour: String(value) }))
    if (key === "bgType") {
      const map = { solid: "Solid", gradient: "Gradient", image: "Image", pattern: "Pattern" } as const
      setState((s) => ({ ...s, backgroundType: map[value as keyof typeof map] }))
    }
  }

  const save = async () => {
    setSaving(true)
    setSaveError(false)
    try {
      await updateSurveyTheme(surveyId, {
        mode: state.mode,
        backgroundType: state.backgroundType,
        primaryColour: state.mode === "Customized" ? state.primaryColour : null,
      })
      if (pendingLogo) {
        await uploadSurveyThemeLogo(surveyId, pendingLogo)
        setPendingLogo(null)
      }
      setBaseline(state)
      // Save survey finishes the wizard — back to the library.
      toast.success(t("surveysModule.appearance.savedToast"))
      navigate("/surveys")
    } catch {
      setSaveError(true)
    } finally {
      setSaving(false)
    }
  }

  if (loading) {
    return (
      <div className="space-y-5 py-5">
        <Skeleton className="h-8 w-64" />
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
          <Skeleton className="h-96 w-full" />
          <Skeleton className="h-96 w-full lg:col-span-2" />
        </div>
      </div>
    )
  }

  return (
    <div className="space-y-5 py-5">
      <SurveyWizardStepper surveyId={surveyId} active="design" />

      {/* Header: back + title/subtitle, Cancel / Preview / Save survey at the end */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
        <div className="flex min-w-0 items-start gap-3">
          <Button
            variant="outline"
            size="icon"
            className="mt-0.5 size-9 shrink-0"
            onClick={() => navigate(`/surveys/${surveyId}/builder`)}
            aria-label={t("common.back")}
          >
            <BackIcon className="size-4" aria-hidden />
          </Button>
          <div className="min-w-0">
            <h1 className="text-2xl font-heading font-bold">
              {t("surveysModule.appearance.title")}
            </h1>
            <p className="mt-1 max-w-2xl text-sm text-muted-foreground">
              {t("surveysModule.appearance.designSubtitle")}
            </p>
            {survey?.nameEn && (
              <p className="mt-1 truncate text-xs font-medium text-muted-foreground">
                {survey.nameEn}
              </p>
            )}
          </div>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <Button variant="outline" onClick={() => navigate("/surveys")}>
            {t("common.cancel")}
          </Button>
          <Button variant="outline" onClick={() => setPreviewFull((p) => !p)}>
            <Eye className="size-4" aria-hidden />
            {previewFull
              ? t("surveysModule.appearance.backToDesign")
              : t("common.preview")}
          </Button>
          <Button
            className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            onClick={() => void save()}
            disabled={saving}
          >
            {saving ? (
              <Loader2 className="size-4 animate-spin" aria-hidden />
            ) : (
              <Check className="size-4" aria-hidden />
            )}
            {t("surveysModule.builder.saveSurvey")}
          </Button>
        </div>
      </div>

      {saveError && (
        <div
          role="alert"
          className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {t("surveysModule.appearance.saveError")}
        </div>
      )}

      {/* Mode radio cards */}
      <ModeCards
        mode={state.mode}
        onChange={(mode) => setState((s) => ({ ...s, mode }))}
        disabled={saving}
      />

      {/* FR-4.2 split pane: controls scroll, preview stays pinned. Preview mode
          hides the controls column entirely and lets the preview go full width. */}
      <div
        className={cn(
          "grid grid-cols-1 gap-6",
          !previewFull && "lg:grid-cols-[minmax(0,360px)_1fr]"
        )}
      >
        {!previewFull && (
          <div className="self-start">
            <SurveyDesignControls
              theme={theme}
              onChange={onThemeChange}
              locked={locked || saving}
              onLogoFile={(file) => {
                setPendingLogo(file)
                setState((s) => ({ ...s, logoFileName: file?.name ?? null }))
              }}
            />
          </div>
        )}

        <div>
          <LivePreviewFrame
            device={device}
            onDeviceChange={setDevice}
            primaryColour={effectiveColour}
            surveyName={survey?.nameEn ?? ""}
            welcomeHtml={survey?.welcomeHtml}
            channels={["mobile", "desktop", "whatsapp", "email"]}
            theme={effectiveTheme}
            questions={previewQuestions}
          />
        </div>
      </div>
    </div>
  )
}
