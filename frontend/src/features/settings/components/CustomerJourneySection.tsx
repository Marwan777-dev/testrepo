// Customer Journey ScoringConfig section — edits the five tenant scoring parameters: α (slider, β
// derived read-only beside it), MOT multiplier (slider), and n_floor / flag_percentile /
// rolling_window_days (integer inputs). Extracted from the former standalone
// /settings/customer-journey page so it can be composed into the unified Settings page. Save is the
// section's filled primary and is shown ONLY to P-01 (FR-062); P-07 sees a read-only form. Cancel
// discards local edits back to the loaded values. RTL-first, design-system tokens + logical properties.

import { useEffect, useRef, useState } from "react"
import { useTranslation } from "react-i18next"
import { Loader2 } from "lucide-react"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import { Card, CardContent, CardFooter } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Skeleton } from "@/components/ui/skeleton"
import { useSession } from "@/features/auth/hooks/useSession"
import { AlphaBetaSlider } from "@/features/settings/components/AlphaBetaSlider"
import { MotMultiplierSlider } from "@/features/settings/components/MotMultiplierSlider"
import { ScoringConfigInfoIcon } from "@/features/settings/components/ScoringConfigInfoIcon"
import { useScoringConfig } from "@/features/settings/hooks/useScoringConfig"

type Field = "alpha" | "mot" | "nFloor" | "flagPercentile" | "rollingWindow"

export interface CustomerJourneySectionProps {
  /** Reports whether the form has unsaved parameter edits, so the page can guard navigation. */
  onDirtyChange?: (dirty: boolean) => void
}

export function CustomerJourneySection({ onDirtyChange }: CustomerJourneySectionProps) {
  const { t } = useTranslation()
  const { session } = useSession()
  const { data, loading, error, saving, save } = useScoringConfig()

  const [alpha, setAlpha] = useState(0.5)
  const [mot, setMot] = useState(1.5)
  const [nFloor, setNFloor] = useState(100)
  const [flagPercentile, setFlagPercentile] = useState(25)
  const [rollingWindow, setRollingWindow] = useState(30)
  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({})
  const initialized = useRef(false)

  // FR-062: only the CX Program Manager (P-01) may edit; P-07 is read-only.
  const canEdit = session?.persona === "P-01"

  useEffect(() => {
    if (data && !initialized.current) {
      setAlpha(data.alpha)
      setMot(data.motMultiplier)
      setNFloor(data.nFloor)
      setFlagPercentile(data.flagPercentile)
      setRollingWindow(data.rollingWindowDays)
      initialized.current = true
    }
  }, [data])

  const dirty =
    data != null &&
    (alpha !== data.alpha ||
      mot !== data.motMultiplier ||
      nFloor !== data.nFloor ||
      flagPercentile !== data.flagPercentile ||
      rollingWindow !== data.rollingWindowDays)

  useEffect(() => {
    onDirtyChange?.(dirty)
  }, [dirty, onDirtyChange])

  function validate(): boolean {
    const next: Partial<Record<Field, string>> = {}
    if (alpha < 0 || alpha > 1) next.alpha = t("settings.customerJourney.errors.alphaRange")
    if (mot < 1 || mot > 2) next.mot = t("settings.customerJourney.errors.motRange")
    if (!Number.isInteger(nFloor) || nFloor < 1) next.nFloor = t("settings.customerJourney.errors.nFloorMin")
    if (!Number.isInteger(flagPercentile) || flagPercentile < 1 || flagPercentile > 49)
      next.flagPercentile = t("settings.customerJourney.errors.flagRange")
    if (!Number.isInteger(rollingWindow) || rollingWindow < 7)
      next.rollingWindow = t("settings.customerJourney.errors.rollingMin")
    setErrors(next)
    return Object.keys(next).length === 0
  }

  function mapServerError(code?: string) {
    switch (code) {
      case "INVALID_ALPHA_BETA_SUM":
        setErrors({ alpha: t("settings.customerJourney.errors.alphaRange") })
        break
      case "MOT_MULTIPLIER_OUT_OF_RANGE":
        setErrors({ mot: t("settings.customerJourney.errors.motRange") })
        break
      case "N_FLOOR_BELOW_MINIMUM":
        setErrors({ nFloor: t("settings.customerJourney.errors.nFloorMin") })
        break
      case "FLAG_PERCENTILE_OUT_OF_RANGE":
        setErrors({ flagPercentile: t("settings.customerJourney.errors.flagRange") })
        break
      case "ROLLING_WINDOW_BELOW_MINIMUM":
        setErrors({ rollingWindow: t("settings.customerJourney.errors.rollingMin") })
        break
      default:
        toast.error(t("settings.customerJourney.saveError"))
    }
  }

  async function handleSave() {
    if (!validate()) return
    const outcome = await save({
      alpha,
      motMultiplier: mot,
      nFloor,
      flagPercentile,
      rollingWindowDays: rollingWindow,
    })
    if (outcome.ok) {
      setErrors({})
      toast.success(t("settings.customerJourney.saved"))
    } else {
      mapServerError(outcome.code)
    }
  }

  function handleCancel() {
    if (!data) return
    setAlpha(data.alpha)
    setMot(data.motMultiplier)
    setNFloor(data.nFloor)
    setFlagPercentile(data.flagPercentile)
    setRollingWindow(data.rollingWindowDays)
    setErrors({})
  }

  return (
    <section aria-labelledby="settings-customer-journey-heading" className="space-y-4">
      <div>
        <h2 id="settings-customer-journey-heading" className="text-lg font-bold">
          {t("settings.customerJourney.title")}
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">{t("settings.customerJourney.subtitle")}</p>
      </div>

      {error ? (
        <div
          role="alert"
          className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {t("settings.customerJourney.loadError")}
        </div>
      ) : loading ? (
        <Card>
          <CardContent className="space-y-4">
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-10 w-full" />
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardContent className="space-y-6">
            {!canEdit && (
              <div
                role="status"
                data-testid="scoring-readonly-notice"
                className="rounded-md border border-border bg-muted/40 px-3 py-2 text-sm text-foreground"
              >
                {t("settings.customerJourney.readOnlyNotice")}
              </div>
            )}

            {/* Alpha / Beta */}
            <div className="space-y-2">
              <AlphaBetaSlider
                alpha={alpha}
                onAlphaChange={setAlpha}
                disabled={!canEdit}
                alphaLabel={t("settings.customerJourney.alphaCaption")}
                betaLabel={t("settings.customerJourney.betaCaption")}
                info={
                  <ScoringConfigInfoIcon
                    label={t("settings.customerJourney.info.alpha")}
                    text={t("settings.customerJourney.tooltips.alpha")}
                  />
                }
              />
              {errors.alpha && (
                <p className="text-sm text-destructive" role="alert">
                  {errors.alpha}
                </p>
              )}
            </div>

            {/* MOT multiplier */}
            <div className="space-y-2">
              <MotMultiplierSlider
                value={mot}
                onChange={setMot}
                disabled={!canEdit}
                label={t("settings.customerJourney.motCaption")}
                info={
                  <ScoringConfigInfoIcon
                    label={t("settings.customerJourney.info.mot")}
                    text={t("settings.customerJourney.tooltips.mot")}
                  />
                }
              />
              {errors.mot && (
                <p className="text-sm text-destructive" role="alert">
                  {errors.mot}
                </p>
              )}
            </div>

            {/* Integer parameters */}
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
              <IntegerField
                id="scoring-n-floor"
                label={t("settings.customerJourney.nFloorLabel")}
                infoLabel={t("settings.customerJourney.info.nFloor")}
                tooltip={t("settings.customerJourney.tooltips.nFloor")}
                value={nFloor}
                min={1}
                onChange={setNFloor}
                disabled={!canEdit}
                error={errors.nFloor}
              />
              <IntegerField
                id="scoring-flag-percentile"
                label={t("settings.customerJourney.flagPercentileLabel")}
                infoLabel={t("settings.customerJourney.info.flagPercentile")}
                tooltip={t("settings.customerJourney.tooltips.flagPercentile")}
                value={flagPercentile}
                min={1}
                max={49}
                onChange={setFlagPercentile}
                disabled={!canEdit}
                error={errors.flagPercentile}
              />
              <IntegerField
                id="scoring-rolling-window"
                label={t("settings.customerJourney.rollingWindowLabel")}
                infoLabel={t("settings.customerJourney.info.rollingWindow")}
                tooltip={t("settings.customerJourney.tooltips.rollingWindow")}
                value={rollingWindow}
                min={7}
                onChange={setRollingWindow}
                disabled={!canEdit}
                error={errors.rollingWindow}
              />
            </div>
          </CardContent>

          {canEdit && (
            <CardFooter className="justify-end gap-2">
              <Button variant="ghost" onClick={handleCancel} disabled={saving || !dirty}>
                {t("common.cancel")}
              </Button>
              <Button
                data-testid="scoring-save"
                className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
                disabled={saving || !dirty}
                onClick={() => void handleSave()}
              >
                {saving && <Loader2 className="size-4 ms-2 animate-spin" aria-hidden />}
                {t("settings.customerJourney.save")}
              </Button>
            </CardFooter>
          )}
        </Card>
      )}
    </section>
  )
}

interface IntegerFieldProps {
  id: string
  label: string
  infoLabel: string
  tooltip: string
  value: number
  min?: number
  max?: number
  onChange: (value: number) => void
  disabled?: boolean
  error?: string
}

function IntegerField({ id, label, infoLabel, tooltip, value, min, max, onChange, disabled, error }: IntegerFieldProps) {
  return (
    <div className="space-y-1.5">
      <div className="flex items-center gap-1.5">
        <Label htmlFor={id}>{label}</Label>
        <ScoringConfigInfoIcon label={infoLabel} text={tooltip} />
      </div>
      <Input
        id={id}
        type="number"
        inputMode="numeric"
        dir="ltr"
        min={min}
        max={max}
        value={Number.isNaN(value) ? "" : String(value)}
        disabled={disabled}
        aria-invalid={error ? true : undefined}
        onChange={(e) => {
          const n = e.target.valueAsNumber
          onChange(Number.isNaN(n) ? Number.NaN : Math.trunc(n))
        }}
        className="tabular-nums"
      />
      {error && (
        <p className="text-sm text-destructive" role="alert">
          {error}
        </p>
      )}
    </div>
  )
}
