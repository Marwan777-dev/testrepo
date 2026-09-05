// F5 build-method chooser (T087): breadcrumb + back button + tinted icon-tile cards
// (clickthrough parity) — Survey builder / From a template / Build with AI. Per FR-5.5
// nothing persists here: no survey row exists until the user Continues out of Survey
// Settings, so "Survey builder" simply routes to the new-survey Settings form ("new"
// is handled as create-mode by SurveyEditorRoutes). The AI method is NOT in this spec
// (ships in a later one) — its tile stays visible but disabled so the catalogue of
// methods stays discoverable.

import { useNavigate } from "react-router"
import { useTranslation } from "react-i18next"
import { ArrowLeft, ArrowRight, BrainCircuit, LayoutTemplate, PencilRuler } from "lucide-react"

import { Button } from "@/components/ui/button"
import { useDirection } from "@/hooks/use-direction"
import { cn } from "@/lib/utils"

export default function BuildMethodPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { isRtl } = useDirection()
  const BackIcon = isRtl ? ArrowRight : ArrowLeft

  const tiles = [
    {
      key: "scratch",
      icon: PencilRuler,
      onSelect: () => navigate("/surveys/new/settings"),
      disabled: false,
      // Cyan brand tint for the icon tile (reference styling).
      tileBg: "bg-nb-cyan-100 dark:bg-nb-cyan-900/40",
      iconColor: "text-nb-cyan",
    },
    {
      key: "template",
      icon: LayoutTemplate,
      onSelect: () => navigate("/surveys?tab=templates"),
      disabled: false,
      tileBg: "bg-nb-cyan-100 dark:bg-nb-cyan-900/40",
      iconColor: "text-nb-cyan",
    },
    {
      key: "ai",
      icon: BrainCircuit,
      onSelect: () => undefined,
      disabled: true,
      // Mint accent differentiates the AI method in the reference.
      tileBg: "bg-nb-mint-100 dark:bg-nb-mint-900/40",
      iconColor: "text-nb-mint-700",
    },
  ] as const

  return (
    <div className="space-y-5 py-5">
      <p className="text-xs text-muted-foreground">{t("surveysModule.build.breadcrumb")}</p>

      <div className="flex items-start gap-3">
        <Button
          variant="outline"
          size="icon"
          className="mt-0.5 size-9 shrink-0"
          onClick={() => navigate("/surveys")}
          aria-label={t("common.back")}
        >
          <BackIcon className="size-4" aria-hidden />
        </Button>
        <div className="min-w-0">
          <h1 className="text-2xl font-heading font-bold">
            {t("surveysModule.buildMethodTitle")}
          </h1>
          <p className="mt-1 max-w-2xl text-sm text-muted-foreground">
            {t("surveysModule.buildMethodSubtitle")}
          </p>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
        {tiles.map(({ key, icon: Icon, onSelect, disabled, tileBg, iconColor }) => (
          <button
            key={key}
            type="button"
            onClick={onSelect}
            disabled={disabled}
            aria-describedby={disabled ? `build-${key}-soon` : undefined}
            // flex-col + justify-start: a native <button> vertically CENTERS its
            // content, so when the grid stretches short cards to the tallest one
            // the icon drifts down with a big top gap. Top-align explicitly.
            className={cn(
              "group flex flex-col items-start justify-start gap-3 rounded-lg border border-border bg-card p-6 text-start transition-all",
              "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2",
              disabled
                ? "cursor-not-allowed opacity-60"
                : "hover:border-primary/40 hover:shadow-md"
            )}
          >
            <div
              className={cn(
                "flex size-11 items-center justify-center rounded-md motion-safe:transition-transform motion-safe:duration-200",
                !disabled && "group-hover:scale-105",
                tileBg
              )}
            >
              <Icon className={cn("size-5", iconColor)} aria-hidden />
            </div>
            <div>
              <h3 className="text-base font-bold text-foreground">
                {t(`surveysModule.build.${key}Title`)}
              </h3>
              <p className="mt-1 text-sm leading-relaxed text-muted-foreground">
                {t(`surveysModule.build.${key}Desc`)}
              </p>
              {disabled && (
                <p
                  id={`build-${key}-soon`}
                  className="mt-2 text-xs font-medium uppercase tracking-widest text-muted-foreground"
                >
                  {t("surveysModule.build.comingSoon")}
                </p>
              )}
            </div>
          </button>
        ))}
      </div>
    </div>
  )
}
