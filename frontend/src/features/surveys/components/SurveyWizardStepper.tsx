// Wizard stepper shared across the survey editor (reference-style segmented bar).
// Every step is freely clickable (no gating on completion): in edit mode Build
// method (done) leads Survey details → Questions → Design, navigating between the
// real per-survey routes; in create mode Questions/Design delegate to the page's
// save-then-navigate handler (a row must exist before those routes do).

import { useTranslation } from "react-i18next"
import { useNavigate } from "react-router"
import { Check } from "lucide-react"

import { cn } from "@/lib/utils"

export interface WizardStep {
  label: string
  state: "done" | "active" | "todo"
  onClick?: () => void
}

/** Reference-style segmented stepper: a bordered bar of flat segments — reached
 * steps carry the soft Mint→Cyan gradient wash, done = mint check circle, active =
 * primary number circle, upcoming = muted; any step with onClick is clickable. */
export function ChevronStepper({ steps, ariaLabel }: { steps: WizardStep[]; ariaLabel: string }) {
  return (
    <nav
      aria-label={ariaLabel}
      className="flex overflow-hidden rounded-lg border border-border bg-card motion-safe:animate-in motion-safe:fade-in-0"
    >
      {steps.map((s, i) => {
        const clickable = !!s.onClick
        const StepTag = clickable ? "button" : "div"
        return (
          <StepTag
            key={s.label}
            type={clickable ? "button" : undefined}
            onClick={s.onClick}
            aria-current={s.state === "active" ? "step" : undefined}
            className={cn(
              "flex min-w-0 flex-1 items-center gap-2.5 px-4 py-3 text-start transition-colors",
              i > 0 && "border-s border-border",
              s.state !== "todo" && "bg-gradient-to-r from-nb-mint/15 to-nb-cyan/15",
              clickable ? "cursor-pointer hover:bg-accent" : "cursor-default"
            )}
          >
            <span
              className={cn(
                "flex size-6 shrink-0 items-center justify-center rounded-full text-xs font-bold",
                s.state === "done"
                  ? "bg-nb-mint text-white"
                  : s.state === "active"
                    ? "bg-primary text-primary-foreground"
                    : "bg-muted text-muted-foreground"
              )}
            >
              {s.state === "done" ? <Check className="size-3.5" aria-hidden /> : i + 1}
            </span>
            {/* Small screens keep only the active step's label so the bar never wraps. */}
            <span
              className={cn(
                "truncate text-sm font-medium",
                s.state === "active" ? "text-foreground" : "text-muted-foreground",
                s.state !== "active" && "hidden sm:block"
              )}
            >
              {s.label}
            </span>
          </StepTag>
        )
      })}
    </nav>
  )
}

const EDIT_STEPS = [
  { key: "details", path: "settings" },
  { key: "questions", path: "builder" },
  { key: "design", path: "appearance" },
] as const

export type SurveyWizardStepKey = (typeof EDIT_STEPS)[number]["key"]

export function SurveyWizardStepper({
  surveyId,
  active,
  onCreateStep,
}: {
  /** null while creating — no survey row exists yet. */
  surveyId: string | null
  active: SurveyWizardStepKey
  /** Create mode: invoked for the not-yet-reachable steps (save then navigate). */
  onCreateStep?: (path: "builder" | "appearance") => void
}) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const ariaLabel = t("surveysModule.steps.aria")

  if (surveyId === null) {
    return (
      <ChevronStepper
        ariaLabel={ariaLabel}
        steps={[
          {
            label: t("surveysModule.steps.buildMethod"),
            state: "done",
            onClick: () => navigate("/surveys/new"),
          },
          { label: t("surveysModule.steps.details"), state: "active" },
          {
            label: t("surveysModule.steps.questions"),
            state: "todo",
            onClick: onCreateStep && (() => onCreateStep("builder")),
          },
          {
            label: t("surveysModule.steps.design"),
            state: "todo",
            onClick: onCreateStep && (() => onCreateStep("appearance")),
          },
        ]}
      />
    )
  }

  // Build method belongs to the CREATE flow only: it stays visible while this
  // survey is still inside the session's create journey (flag set on create,
  // cleared when the library mounts) — plain edit visits never show it.
  const inCreateFlow =
    typeof sessionStorage !== "undefined" &&
    sessionStorage.getItem("surveys.createFlow") === surveyId
  const activeIdx = EDIT_STEPS.findIndex((s) => s.key === active)
  const editSteps: WizardStep[] = EDIT_STEPS.map((s, i) => ({
    label: t(`surveysModule.steps.${s.key}`),
    state: i < activeIdx ? "done" : i === activeIdx ? "active" : "todo",
    onClick: i === activeIdx ? undefined : () => navigate(`/surveys/${surveyId}/${s.path}`),
  }))
  return (
    <ChevronStepper
      ariaLabel={ariaLabel}
      steps={
        inCreateFlow
          ? [
              {
                label: t("surveysModule.steps.buildMethod"),
                state: "done",
                onClick: () => navigate("/surveys/new"),
              },
              ...editSteps,
            ]
          : editSteps
      }
    />
  )
}
