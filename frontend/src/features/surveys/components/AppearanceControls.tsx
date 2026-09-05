// F4 appearance controls list (T095). Inherited mode resolves every token from the
// tenant design guidelines and LOCKS the controls; Customize unlocks them (FR-4.x).
// Chrome uses nb-* brand tokens only (never d{n}-* — Two-Palette Rule); the preview
// colour itself is tenant data, applied via inline style in LivePreviewFrame.

import { useTranslation } from "react-i18next"
import { Lock, Upload } from "lucide-react"

import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Switch } from "@/components/ui/switch"
import type { BackgroundType, ThemeMode } from "../api/surveys-api"

export interface AppearanceState {
  mode: ThemeMode
  backgroundType: BackgroundType
  primaryColour: string
  logoFileName: string | null
}

const BACKGROUND_TYPES: BackgroundType[] = ["Solid", "Gradient", "Image", "Pattern"]

export function AppearanceControls({
  value,
  onChange,
  onLogoSelect,
  disabled,
  hideModeToggle,
}: {
  value: AppearanceState
  onChange: (next: AppearanceState) => void
  onLogoSelect: (file: File) => void
  disabled?: boolean
  /** The design step renders the mode as radio cards above the pane — skip the inline switch. */
  hideModeToggle?: boolean
}) {
  const { t } = useTranslation()
  const locked = value.mode === "Inherited"

  return (
    <div className="space-y-5">
      {/* Mode: Inherited (locked to tenant guidelines) vs Customize */}
      {!hideModeToggle && (
        <div className="flex items-center justify-between gap-3 rounded-md border border-border bg-muted/30 p-3">
          <div className="min-w-0">
            <Label htmlFor="appearance-mode" className="cursor-pointer">
              {t("surveysModule.appearance.customize")}
            </Label>
            <p className="mt-0.5 text-sm text-muted-foreground">
              {locked
                ? t("surveysModule.appearance.inheritedHint")
                : t("surveysModule.appearance.customizeHint")}
            </p>
          </div>
          <Switch
            id="appearance-mode"
            checked={value.mode === "Customized"}
            onCheckedChange={(on) => onChange({ ...value, mode: on ? "Customized" : "Inherited" })}
            disabled={disabled}
          />
        </div>
      )}

      {locked && (
        <p className="flex items-center gap-2 text-sm text-muted-foreground">
          <Lock className="size-4 shrink-0" aria-hidden />
          {t("surveysModule.appearance.lockedNote")}
        </p>
      )}

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="appearance-primary">{t("surveysModule.appearance.primaryColour")}</Label>
        <div className="flex items-center gap-2">
          <input
            id="appearance-primary"
            type="color"
            value={value.primaryColour}
            onChange={(e) => onChange({ ...value, primaryColour: e.target.value })}
            disabled={disabled || locked}
            className="h-10 w-14 cursor-pointer rounded-md border border-input bg-card p-1 disabled:cursor-not-allowed disabled:opacity-50"
            aria-label={t("surveysModule.appearance.primaryColour")}
          />
          <Input
            value={value.primaryColour}
            onChange={(e) => onChange({ ...value, primaryColour: e.target.value })}
            disabled={disabled || locked}
            aria-label={t("surveysModule.appearance.primaryColourHex")}
            dir="ltr"
            className="font-mono sm:w-36"
          />
        </div>
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="appearance-bg">{t("surveysModule.appearance.backgroundType")}</Label>
        <Select
          value={value.backgroundType}
          onValueChange={(v) => onChange({ ...value, backgroundType: v as BackgroundType })}
          disabled={disabled || locked}
        >
          <SelectTrigger id="appearance-bg" className="w-full sm:w-48">
            <SelectValue>
              {(v) => t(`surveysModule.appearance.bg${String(v ?? value.backgroundType)}`)}
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            {BACKGROUND_TYPES.map((b) => (
              <SelectItem key={b} value={b}>
                {t(`surveysModule.appearance.bg${b}`)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="appearance-logo">{t("surveysModule.appearance.logo")}</Label>
        <div className="flex items-center gap-2">
          <Button
            variant="secondary"
            disabled={disabled || locked}
            onClick={() => document.getElementById("appearance-logo")?.click()}
          >
            <Upload className="size-4" aria-hidden />
            {t("surveysModule.appearance.uploadLogo")}
          </Button>
          {value.logoFileName && (
            <span className="truncate text-sm text-muted-foreground">{value.logoFileName}</span>
          )}
        </div>
        <input
          id="appearance-logo"
          type="file"
          accept="image/png,image/jpeg,image/svg+xml"
          className="sr-only"
          aria-label={t("surveysModule.appearance.uploadLogo")}
          disabled={disabled || locked}
          onChange={(e) => {
            const file = e.target.files?.[0]
            if (file) onLogoSelect(file)
          }}
        />
      </div>
    </div>
  )
}
