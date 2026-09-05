// src/pages/SettingsCustomerJourneyPage.tsx
import { useState, useEffect } from "react"
import { useNavigate } from "react-router"
import { useTranslation } from "react-i18next"
import { ArrowLeft, ArrowRight, Info } from "lucide-react"
import { useSettings } from "@/contexts/settings-context"
import { useDirection } from "@/hooks/use-direction"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Slider } from "@/components/ui/slider"
import { Card, CardContent } from "@/components/ui/card"
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import {
  Tooltip, TooltipContent, TooltipProvider, TooltipTrigger,
} from "@/components/ui/tooltip"
import { cn } from "@/lib/utils"
import type { ScoringConfig } from "@/types/settings"

export default function SettingsCustomerJourneyPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { scoringConfig, saveScoring } = useSettings()
  const { isRtl } = useDirection()

  const BackArrow = isRtl ? ArrowLeft : ArrowRight

  const [alpha, setAlpha] = useState(scoringConfig.alpha)
  const [motMultiplier, setMotMultiplier] = useState(scoringConfig.motMultiplier)
  const [nFloor, setNFloor] = useState(scoringConfig.nFloor)
  const [flagPercentile, setFlagPercentile] = useState(scoringConfig.flagPercentile)
  const [rollingWindowDays, setRollingWindowDays] = useState(scoringConfig.rollingWindowDays)
  const [isDirty, setIsDirty] = useState(false)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [showLeaveModal, setShowLeaveModal] = useState(false)

  // β is always derived — never stored
  const beta = parseFloat((1 - alpha).toFixed(3))

  useEffect(() => {
    setAlpha(scoringConfig.alpha)
    setMotMultiplier(scoringConfig.motMultiplier)
    setNFloor(scoringConfig.nFloor)
    setFlagPercentile(scoringConfig.flagPercentile)
    setRollingWindowDays(scoringConfig.rollingWindowDays)
    setIsDirty(false)
  }, [scoringConfig])

  function markDirty() { setIsDirty(true) }

  function validate() {
    const e: Record<string, string> = {}
    if (alpha < 0 || alpha > 1) e.alpha = t("settings.errAlpha")
    if (motMultiplier < 1.0 || motMultiplier > 2.0) e.motMultiplier = t("settings.errMot")
    if (!Number.isInteger(nFloor) || nFloor < 1) e.nFloor = t("settings.errNFloor")
    if (!Number.isInteger(flagPercentile) || flagPercentile < 1 || flagPercentile > 49) {
      e.flagPercentile = t("settings.errFlagPct")
    }
    if (!Number.isInteger(rollingWindowDays) || rollingWindowDays < 7) {
      e.rollingWindowDays = t("settings.errRollingDays")
    }
    setErrors(e)
    return Object.keys(e).length === 0
  }

  function handleSave() {
    if (!validate()) return
    const updated: ScoringConfig = {
      alpha, motMultiplier, nFloor, flagPercentile, rollingWindowDays,
    }
    saveScoring(updated)
    setIsDirty(false)
    navigate("/settings")
  }

  function handleBack() {
    if (isDirty) { setShowLeaveModal(true) } else { navigate("/settings") }
  }

  function extractSliderValue(v: number | readonly number[]): number {
    return Array.isArray(v) ? (v as readonly number[])[0] : (v as number)
  }

  return (
    <TooltipProvider>
    <div className="space-y-5 py-5 px-8">
      {/* Back + Title */}
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="icon" aria-label={t("common.back")} onClick={handleBack}>
          <BackArrow className="size-5" />
        </Button>
        <div>
          <h1 className="text-2xl font-heading font-bold">{t("settings.cjTitle")}</h1>
          <p className="text-sm text-muted-foreground mt-0.5">{t("settings.cjDesc")}</p>
        </div>
      </div>

      <Card className="max-w-2xl">
        <CardContent className="space-y-8">

          {/* ── Alpha (α) ──────────────────────────────── */}
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-1.5">
                <Label className="text-sm font-semibold">{t("settings.cjAlpha")}</Label>
                <Tooltip>
                  <TooltipTrigger
                    className="inline-flex items-center text-muted-foreground cursor-help"
                    aria-label={t("settings.cjAlpha")}
                  >
                    <Info className="size-3.5" />
                  </TooltipTrigger>
                  <TooltipContent side="right" className="max-w-sm p-4 text-xs leading-relaxed font-sans">
                    <p>{t("settings.tooltipAlpha")}</p>
                  </TooltipContent>
                </Tooltip>
              </div>
              <span className="text-sm font-mono font-bold tabular-nums text-primary">
                {alpha.toFixed(3)}
              </span>
            </div>
            <Slider
              value={[alpha]}
              min={0}
              max={1}
              step={0.001}
              onValueChange={(v) => {
                setAlpha(parseFloat(extractSliderValue(v).toFixed(3)))
                markDirty()
              }}
            />
            {/* β read-only display */}
            <div className="flex items-center gap-3 pt-1">
              <div className="flex items-center gap-2 px-3 py-1.5 rounded-md bg-muted/40 border border-border">
                <span className="text-xs text-muted-foreground">{t("settings.cjBeta")}</span>
                <span className="text-sm font-mono font-bold tabular-nums">
                  {beta.toFixed(3)}
                </span>
              </div>
              <span className="text-xs text-muted-foreground">{t("settings.cjBetaDerived")}</span>
            </div>
            {errors.alpha && (
              <p className="text-xs text-destructive" role="alert">{errors.alpha}</p>
            )}
          </div>

          {/* ── MOT Multiplier ─────────────────────────── */}
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-1.5">
                <Label className="text-sm font-semibold">{t("settings.cjMot")}</Label>
                <Tooltip>
                  <TooltipTrigger
                    className="inline-flex items-center text-muted-foreground cursor-help"
                    aria-label={t("settings.cjMot")}
                  >
                    <Info className="size-3.5" />
                  </TooltipTrigger>
                  <TooltipContent side="right" className="max-w-sm p-4 text-xs leading-relaxed font-sans">
                    <p>{t("settings.tooltipMot")}</p>
                  </TooltipContent>
                </Tooltip>
              </div>
              <Input
                type="number"
                min={1.0}
                max={2.0}
                step={0.1}
                value={motMultiplier}
                onChange={(e) => {
                  const v = parseFloat(e.target.value)
                  if (!isNaN(v)) { setMotMultiplier(parseFloat(v.toFixed(1))); markDirty() }
                }}
                className={cn(
                  "w-20 text-center tabular-nums h-8",
                  errors.motMultiplier && "border-destructive",
                )}
              />
            </div>
            <Slider
              value={[motMultiplier]}
              min={1}
              max={2}
              step={0.1}
              onValueChange={(v) => {
                setMotMultiplier(parseFloat(extractSliderValue(v).toFixed(1)))
                markDirty()
              }}
            />
            {errors.motMultiplier && (
              <p className="text-xs text-destructive" role="alert">{errors.motMultiplier}</p>
            )}
          </div>

          {/* ── Responses Count Floor ──────────────────── */}
          <div className="space-y-1.5">
            <div className="flex items-center gap-1.5">
              <Label htmlFor="nFloor" className="text-sm font-semibold">
                {t("settings.cjNFloor")}
              </Label>
              <Tooltip>
                <TooltipTrigger
                  className="inline-flex items-center text-muted-foreground cursor-help"
                  aria-label={t("settings.cjNFloor")}
                >
                  <Info className="size-3.5" />
                </TooltipTrigger>
                <TooltipContent side="right" className="max-w-sm p-4 text-xs leading-relaxed font-sans">
                  <p>{t("settings.tooltipNFloor")}</p>
                </TooltipContent>
              </Tooltip>
            </div>
            <Input
              id="nFloor"
              type="number"
              min={1}
              value={nFloor}
              onChange={(e) => {
                const v = parseInt(e.target.value, 10)
                if (!isNaN(v)) { setNFloor(v); markDirty() }
              }}
              className={cn("tabular-nums", errors.nFloor && "border-destructive")}
            />
            {errors.nFloor && (
              <p className="text-xs text-destructive" role="alert">{errors.nFloor}</p>
            )}
          </div>

          {/* ── Flag Percentile ────────────────────────── */}
          <div className="space-y-1.5">
            <div className="flex items-center gap-1.5">
              <Label htmlFor="flagPct" className="text-sm font-semibold">
                {t("settings.cjFlagPct")}
              </Label>
              <Tooltip>
                <TooltipTrigger
                  className="inline-flex items-center text-muted-foreground cursor-help"
                  aria-label={t("settings.cjFlagPct")}
                >
                  <Info className="size-3.5" />
                </TooltipTrigger>
                <TooltipContent side="right" className="max-w-sm p-4 text-xs leading-relaxed font-sans">
                  <p>{t("settings.tooltipFlagPct")}</p>
                </TooltipContent>
              </Tooltip>
            </div>
            <Input
              id="flagPct"
              type="number"
              min={1}
              max={49}
              value={flagPercentile}
              onChange={(e) => {
                const v = parseInt(e.target.value, 10)
                if (!isNaN(v)) { setFlagPercentile(v); markDirty() }
              }}
              className={cn("tabular-nums", errors.flagPercentile && "border-destructive")}
            />
            {errors.flagPercentile && (
              <p className="text-xs text-destructive" role="alert">{errors.flagPercentile}</p>
            )}
          </div>

          {/* ── Rolling Window Days ────────────────────── */}
          <div className="space-y-1.5">
            <div className="flex items-center gap-1.5">
              <Label htmlFor="rollingDays" className="text-sm font-semibold">
                {t("settings.cjRollingDays")}
              </Label>
              <Tooltip>
                <TooltipTrigger
                  className="inline-flex items-center text-muted-foreground cursor-help"
                  aria-label={t("settings.cjRollingDays")}
                >
                  <Info className="size-3.5" />
                </TooltipTrigger>
                <TooltipContent side="right" className="max-w-sm p-4 text-xs leading-relaxed font-sans">
                  <p>{t("settings.tooltipRollingDays")}</p>
                </TooltipContent>
              </Tooltip>
            </div>
            <Input
              id="rollingDays"
              type="number"
              min={7}
              value={rollingWindowDays}
              onChange={(e) => {
                const v = parseInt(e.target.value, 10)
                if (!isNaN(v)) { setRollingWindowDays(v); markDirty() }
              }}
              className={cn("tabular-nums", errors.rollingWindowDays && "border-destructive")}
            />
            {errors.rollingWindowDays && (
              <p className="text-xs text-destructive" role="alert">{errors.rollingWindowDays}</p>
            )}
          </div>

          {/* Actions */}
          <div className="flex items-center justify-end gap-3 pt-2 border-t border-border">
            <Button variant="ghost" onClick={handleBack}>{t("common.cancel")}</Button>
            <Button
              disabled={!isDirty}
              onClick={handleSave}
              className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            >
              {t("common.save")}
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Unsaved-changes guard */}
      <AlertDialog open={showLeaveModal} onOpenChange={setShowLeaveModal}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("settings.unsavedTitle")}</AlertDialogTitle>
            <AlertDialogDescription>{t("settings.unsavedBody")}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("common.no")}</AlertDialogCancel>
            <AlertDialogAction onClick={() => navigate("/settings")}>
              {t("common.yes")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
    </TooltipProvider>
  )
}
