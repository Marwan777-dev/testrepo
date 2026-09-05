// F4 design controls (clickthrough parity): the full reference control set —
// Branding / Colors / Buttons / Header / Footer / Background as single-open
// accordion groups, plus Typography / Surfaces / Status colors / Layout behind the
// "Show advanced settings" toggle. The theme draft is client state; the backend
// persists only {mode, backgroundType, primaryColour, logo} today, so the remaining
// keys style the live preview without round-tripping (matching the reference's
// local-theme model). Inherited mode disables everything (FR-4.x).

import { useState } from "react"
import { useTranslation } from "react-i18next"
import { ChevronDown, Upload, X } from "lucide-react"

import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { cn } from "@/lib/utils"

export type ThemeRadius = "sharp" | "small" | "medium" | "large"

export interface SurveyThemeDraft {
  primary: string
  textColor: string
  buttonColor: string
  buttonText: string
  btnBorder: string
  btnRadius: ThemeRadius
  showLogo: boolean
  showTitle: boolean
  headerAlign: "start" | "center"
  footerText: string
  /** Object URL of the uploaded logo (preview only — the file uploads separately). */
  logo: string | null
  bgType: "solid" | "gradient" | "image" | "pattern"
  gradFrom: string
  gradTo: string
  gradAngle: string
  bgImage: string
  bgOpacity: number
  headingFont: string
  bodyFont: string
  bodySize: string
  headingSize: string
  lineHeight: string
  background: string
  card: string
  border: string
  success: string
  warning: string
  error: string
  radius: ThemeRadius
  progress: "bar" | "steps" | "none"
}

export const DEFAULT_THEME: SurveyThemeDraft = {
  primary: "#0D8BBC",
  textColor: "#1E2235",
  buttonColor: "#0D8BBC",
  buttonText: "#FFFFFF",
  btnBorder: "#0D8BBC",
  btnRadius: "medium",
  showLogo: true,
  showTitle: true,
  headerAlign: "start",
  footerText: "",
  logo: null,
  bgType: "solid",
  gradFrom: "#0D8BBC",
  gradTo: "#13DB9B",
  gradAngle: "135",
  bgImage: "",
  bgOpacity: 100,
  headingFont: "Sora",
  bodyFont: "Poppins",
  bodySize: "14",
  headingSize: "15",
  lineHeight: "1.5",
  background: "#F4F7FA",
  card: "#FFFFFF",
  border: "#C9D4DC",
  success: "#1FAE78",
  warning: "#E0A106",
  error: "#E5484D",
  radius: "medium",
  progress: "bar",
}

export const RADIUS_PX: Record<ThemeRadius, number> = { sharp: 0, small: 6, medium: 12, large: 20 }
const FONTS = ["Sora", "Poppins", "IBM Plex Sans Arabic", "System"]

type ThemeKey = keyof SurveyThemeDraft

/** One tidy row: label on the start side, swatch + hex on the end — fits the narrow
 * controls column without truncating (the 2-col grid squeezed both inputs). */
function ColorField({
  label,
  value,
  onChange,
  disabled,
}: {
  label: string
  value: string
  onChange: (v: string) => void
  disabled?: boolean
}) {
  return (
    <div className="flex items-center justify-between gap-2">
      <Label className="min-w-0 flex-1 text-xs">{label}</Label>
      <input
        type="color"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        disabled={disabled}
        aria-label={label}
        className="h-8 w-9 shrink-0 cursor-pointer rounded-md border border-input bg-card p-0.5 disabled:cursor-not-allowed disabled:opacity-50"
      />
      <Input
        value={value}
        onChange={(e) => onChange(e.target.value)}
        disabled={disabled}
        aria-label={`${label} (hex)`}
        dir="ltr"
        className="h-8 w-24 shrink-0 font-mono text-xs"
      />
    </div>
  )
}

function SelectField({
  label,
  value,
  onChange,
  options,
  disabled,
}: {
  label: string
  value: string
  onChange: (v: string) => void
  options: [string, string][]
  disabled?: boolean
}) {
  return (
    <div className="space-y-1.5">
      <Label className="text-xs">{label}</Label>
      <Select value={value} onValueChange={(v) => v && onChange(v)} disabled={disabled}>
        <SelectTrigger className="w-full" aria-label={label}>
          <SelectValue>
            {(v) => options.find(([key]) => key === (v ?? value))?.[1] ?? ""}
          </SelectValue>
        </SelectTrigger>
        <SelectContent>
          {options.map(([key, text]) => (
            <SelectItem key={key} value={key}>
              {text}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  )
}

function CheckField({
  label,
  checked,
  onChange,
  disabled,
}: {
  label: string
  checked: boolean
  onChange: (v: boolean) => void
  disabled?: boolean
}) {
  return (
    <label className="flex cursor-pointer items-center gap-2 text-sm">
      <Checkbox checked={checked} onCheckedChange={(v) => onChange(v === true)} disabled={disabled} />
      {label}
    </label>
  )
}

/** Single-open accordion group. */
function Group({
  id,
  title,
  open,
  onToggle,
  disabled,
  children,
}: {
  id: string
  title: string
  open: boolean
  onToggle: (id: string) => void
  disabled?: boolean
  children: React.ReactNode
}) {
  return (
    <div className="rounded-md border border-border">
      <button
        type="button"
        onClick={() => onToggle(id)}
        aria-expanded={open}
        disabled={disabled}
        className="flex w-full items-center justify-between px-3 py-2.5 text-sm font-semibold transition-colors hover:bg-accent disabled:pointer-events-none"
      >
        {title}
        <ChevronDown
          className={cn("size-4 text-muted-foreground transition-transform", open && "rotate-180")}
          aria-hidden
        />
      </button>
      <div
        className={cn(
          "grid transition-[grid-template-rows,opacity] duration-300 ease-[cubic-bezier(0.16,1,0.3,1)]",
          open ? "grid-rows-[1fr] opacity-100" : "grid-rows-[0fr] opacity-0"
        )}
      >
        <div className="overflow-hidden">
          <div className="space-y-3 border-t border-border px-3 pb-4 pt-4">{children}</div>
        </div>
      </div>
    </div>
  )
}

export function SurveyDesignControls({
  theme,
  onChange,
  locked,
  onLogoFile,
}: {
  theme: SurveyThemeDraft
  onChange: <K extends ThemeKey>(key: K, value: SurveyThemeDraft[K]) => void
  /** Inherited mode — controls disabled until Customize is selected. */
  locked: boolean
  /** The logo persists via the theme logo endpoint — the raw file goes up here. */
  onLogoFile: (file: File | null) => void
}) {
  const { t } = useTranslation()
  const [open, setOpen] = useState("branding")
  const [advOpen, setAdvOpen] = useState(false)
  const toggle = (id: string) => setOpen((cur) => (cur === id ? "" : id))
  const d = locked

  const radiusOptions: [string, string][] = [
    ["sharp", t("surveysModule.design.radiusSharp")],
    ["small", t("surveysModule.design.radiusSmall")],
    ["medium", t("surveysModule.design.radiusMedium")],
    ["large", t("surveysModule.design.radiusLarge")],
  ]
  const fontOptions: [string, string][] = FONTS.map((f) => [f, f])

  return (
    <div className={cn("space-y-2", locked && "opacity-60")}>
      {/* ── Branding ── */}
      <Group disabled={locked} id="branding" title={t("surveysModule.design.branding")} open={!locked && open === "branding"} onToggle={toggle}>
        <div className="space-y-1.5">
          <Label className="text-xs">{t("surveysModule.design.logo")}</Label>
          <div className="flex items-center gap-2">
            {theme.logo ? (
              <img src={theme.logo} alt="" className="h-9 max-w-24 rounded-md border border-border object-contain p-1" />
            ) : (
              <span className="flex h-9 items-center rounded-md border border-dashed border-border px-3 text-xs text-muted-foreground">
                {t("surveysModule.design.logoNone")}
              </span>
            )}
            <Button
              variant="secondary"
              size="compact"
              disabled={d}
              onClick={() => document.getElementById("design-logo-file")?.click()}
            >
              <Upload className="size-4" aria-hidden />
              {t("surveysModule.appearance.uploadLogo")}
            </Button>
            {theme.logo && (
              <Button
                variant="ghost"
                size="icon-sm"
                aria-label={t("surveysModule.design.logoClear")}
                disabled={d}
                onClick={() => {
                  onChange("logo", null)
                  onLogoFile(null)
                }}
              >
                <X className="size-4" aria-hidden />
              </Button>
            )}
          </div>
          <input
            id="design-logo-file"
            type="file"
            accept="image/png,image/jpeg,image/svg+xml"
            className="sr-only"
            aria-label={t("surveysModule.appearance.uploadLogo")}
            disabled={d}
            onChange={(e) => {
              const file = e.target.files?.[0]
              if (file) {
                onChange("logo", URL.createObjectURL(file))
                onLogoFile(file)
              }
            }}
          />
          <p className="text-xs text-muted-foreground">{t("surveysModule.design.logoHint")}</p>
        </div>
      </Group>

      {/* ── Colors ── */}
      <Group disabled={locked} id="colors" title={t("surveysModule.design.colors")} open={!locked && open === "colors"} onToggle={toggle}>
        <div className="space-y-3">
          <ColorField label={t("surveysModule.design.primary")} value={theme.primary} onChange={(v) => onChange("primary", v)} disabled={d} />
          <ColorField label={t("surveysModule.design.textColor")} value={theme.textColor} onChange={(v) => onChange("textColor", v)} disabled={d} />
        </div>
      </Group>

      {/* ── Buttons ── */}
      <Group disabled={locked} id="buttons" title={t("surveysModule.design.buttons")} open={!locked && open === "buttons"} onToggle={toggle}>
        <div className="space-y-3">
          <SelectField label={t("surveysModule.design.btnRadius")} value={theme.btnRadius} onChange={(v) => onChange("btnRadius", v as ThemeRadius)} options={radiusOptions} disabled={d} />
          <ColorField label={t("surveysModule.design.btnBorder")} value={theme.btnBorder} onChange={(v) => onChange("btnBorder", v)} disabled={d} />
          <ColorField label={t("surveysModule.design.buttonColor")} value={theme.buttonColor} onChange={(v) => onChange("buttonColor", v)} disabled={d} />
          <ColorField label={t("surveysModule.design.buttonText")} value={theme.buttonText} onChange={(v) => onChange("buttonText", v)} disabled={d} />
        </div>
      </Group>

      {/* ── Header ── */}
      <Group disabled={locked} id="header" title={t("surveysModule.design.header")} open={!locked && open === "header"} onToggle={toggle}>
        <CheckField label={t("surveysModule.design.showLogo")} checked={theme.showLogo} onChange={(v) => onChange("showLogo", v)} disabled={d} />
        <CheckField label={t("surveysModule.design.showTitle")} checked={theme.showTitle} onChange={(v) => onChange("showTitle", v)} disabled={d} />
        <SelectField
          label={t("surveysModule.design.headerAlign")}
          value={theme.headerAlign}
          onChange={(v) => onChange("headerAlign", v as "start" | "center")}
          options={[
            ["start", t("surveysModule.design.alignStart")],
            ["center", t("surveysModule.design.alignCenter")],
          ]}
          disabled={d}
        />
      </Group>

      {/* ── Footer ── */}
      <Group disabled={locked} id="footer" title={t("surveysModule.design.footer")} open={!locked && open === "footer"} onToggle={toggle}>
        <div className="space-y-1.5">
          <Label className="text-xs">{t("surveysModule.design.footerText")}</Label>
          <Input value={theme.footerText} onChange={(e) => onChange("footerText", e.target.value)} disabled={d} />
          <p className="text-xs text-muted-foreground">{t("surveysModule.design.footerHint")}</p>
        </div>
      </Group>

      {/* ── Background ── */}
      <Group disabled={locked} id="background" title={t("surveysModule.design.backgroundGroup")} open={!locked && open === "background"} onToggle={toggle}>
        <SelectField
          label={t("surveysModule.design.bgType")}
          value={theme.bgType}
          onChange={(v) => onChange("bgType", v as SurveyThemeDraft["bgType"])}
          options={[
            ["solid", t("surveysModule.appearance.bgSolid")],
            ["gradient", t("surveysModule.appearance.bgGradient")],
            ["image", t("surveysModule.appearance.bgImage")],
            ["pattern", t("surveysModule.appearance.bgPattern")],
          ]}
          disabled={d}
        />
        {theme.bgType === "solid" && (
          <p className="text-xs text-muted-foreground">{t("surveysModule.design.bgSolidNote")}</p>
        )}
        {theme.bgType === "gradient" && (
          <div className="space-y-3">
            <ColorField label={t("surveysModule.design.gradFrom")} value={theme.gradFrom} onChange={(v) => onChange("gradFrom", v)} disabled={d} />
            <ColorField label={t("surveysModule.design.gradTo")} value={theme.gradTo} onChange={(v) => onChange("gradTo", v)} disabled={d} />
            <div>
              <SelectField
                label={t("surveysModule.design.gradAngle")}
                value={theme.gradAngle}
                onChange={(v) => onChange("gradAngle", v)}
                options={[
                  ["90", t("surveysModule.design.angleTopBottom")],
                  ["135", t("surveysModule.design.angleDiagonal")],
                  ["180", t("surveysModule.design.angleBottomTop")],
                  ["45", t("surveysModule.design.angleDiagonalUp")],
                ]}
                disabled={d}
              />
            </div>
          </div>
        )}
        {(theme.bgType === "image" || theme.bgType === "pattern") && (
          <div className="space-y-1.5">
            <Label className="text-xs">
              {theme.bgType === "pattern"
                ? t("surveysModule.design.patternImage")
                : t("surveysModule.design.bgImage")}
            </Label>
            <div className="flex items-center gap-2">
              <Button
                variant="secondary"
                size="compact"
                disabled={d}
                onClick={() => document.getElementById("design-bg-file")?.click()}
              >
                <Upload className="size-4" aria-hidden />
                {t("surveysModule.appearance.uploadLogo")}
              </Button>
              {theme.bgImage && (
                <Button
                  variant="ghost"
                  size="icon-sm"
                  aria-label={t("surveysModule.design.logoClear")}
                  disabled={d}
                  onClick={() => onChange("bgImage", "")}
                >
                  <X className="size-4" aria-hidden />
                </Button>
              )}
            </div>
            <input
              id="design-bg-file"
              type="file"
              accept="image/png,image/jpeg"
              className="sr-only"
              aria-label={t("surveysModule.design.bgImage")}
              disabled={d}
              onChange={(e) => {
                const file = e.target.files?.[0]
                if (file) onChange("bgImage", URL.createObjectURL(file))
              }}
            />
          </div>
        )}
        <div className="space-y-1.5">
          <Label className="text-xs">
            {t("surveysModule.design.bgOpacity")}
            <span className="ms-1 text-muted-foreground" dir="ltr">
              {theme.bgOpacity}%
            </span>
          </Label>
          <input
            type="range"
            min={0}
            max={100}
            value={theme.bgOpacity}
            onChange={(e) => onChange("bgOpacity", Number(e.target.value))}
            disabled={d}
            aria-label={t("surveysModule.design.bgOpacity")}
            className="w-full accent-[var(--color-nb-cyan)]"
          />
        </div>
      </Group>

      {/* ── Advanced toggle ── */}
      <button
        type="button"
        onClick={() => setAdvOpen((a) => !a)}
        aria-expanded={advOpen}
        disabled={locked}
        className="flex w-full items-center justify-between rounded-md border border-border px-3 py-2.5 text-sm font-semibold transition-colors hover:bg-accent disabled:pointer-events-none"
      >
        {advOpen
          ? t("surveysModule.design.hideAdvanced")
          : t("surveysModule.design.showAdvanced")}
        <ChevronDown
          className={cn("size-4 text-muted-foreground transition-transform", advOpen && "rotate-180")}
          aria-hidden
        />
      </button>

      {advOpen && !locked && (
        <>
          <Group disabled={locked} id="typography" title={t("surveysModule.design.typography")} open={!locked && open === "typography"} onToggle={toggle}>
            <div className="space-y-3">
              <SelectField label={t("surveysModule.design.headingFont")} value={theme.headingFont} onChange={(v) => onChange("headingFont", v)} options={fontOptions} disabled={d} />
              <SelectField label={t("surveysModule.design.bodyFont")} value={theme.bodyFont} onChange={(v) => onChange("bodyFont", v)} options={fontOptions} disabled={d} />
              <SelectField
                label={t("surveysModule.design.bodySize")}
                value={theme.bodySize}
                onChange={(v) => onChange("bodySize", v)}
                options={[
                  ["13", t("surveysModule.design.sizeSmall")],
                  ["14", t("surveysModule.design.sizeDefault")],
                  ["16", t("surveysModule.design.sizeLarge")],
                ]}
                disabled={d}
              />
              <SelectField
                label={t("surveysModule.design.headingSize")}
                value={theme.headingSize}
                onChange={(v) => onChange("headingSize", v)}
                options={[
                  ["15", t("surveysModule.design.sizeDefault")],
                  ["17", t("surveysModule.design.sizeLarge")],
                  ["20", t("surveysModule.design.sizeXl")],
                ]}
                disabled={d}
              />
              <SelectField
                label={t("surveysModule.design.lineHeight")}
                value={theme.lineHeight}
                onChange={(v) => onChange("lineHeight", v)}
                options={[
                  ["1.3", t("surveysModule.design.lineTight")],
                  ["1.5", t("surveysModule.design.lineNormal")],
                  ["1.7", t("surveysModule.design.lineRelaxed")],
                ]}
                disabled={d}
              />
            </div>
          </Group>

          <Group disabled={locked} id="surfaces" title={t("surveysModule.design.surfaces")} open={!locked && open === "surfaces"} onToggle={toggle}>
            <div className="space-y-3">
              <ColorField label={t("surveysModule.design.backgroundColor")} value={theme.background} onChange={(v) => onChange("background", v)} disabled={d} />
              <ColorField label={t("surveysModule.design.cardBackground")} value={theme.card} onChange={(v) => onChange("card", v)} disabled={d} />
              <ColorField label={t("surveysModule.design.borderColor")} value={theme.border} onChange={(v) => onChange("border", v)} disabled={d} />
            </div>
          </Group>

          <Group disabled={locked} id="status" title={t("surveysModule.design.statusColors")} open={!locked && open === "status"} onToggle={toggle}>
            <div className="space-y-3">
              <ColorField label={t("surveysModule.design.success")} value={theme.success} onChange={(v) => onChange("success", v)} disabled={d} />
              <ColorField label={t("surveysModule.design.warning")} value={theme.warning} onChange={(v) => onChange("warning", v)} disabled={d} />
              <ColorField label={t("surveysModule.design.error")} value={theme.error} onChange={(v) => onChange("error", v)} disabled={d} />
            </div>
          </Group>

          <Group disabled={locked} id="layout" title={t("surveysModule.design.layoutGroup")} open={!locked && open === "layout"} onToggle={toggle}>
            <div className="space-y-3">
              <SelectField label={t("surveysModule.design.cardRadius")} value={theme.radius} onChange={(v) => onChange("radius", v as ThemeRadius)} options={radiusOptions} disabled={d} />
              <SelectField
                label={t("surveysModule.design.progressBar")}
                value={theme.progress}
                onChange={(v) => onChange("progress", v as SurveyThemeDraft["progress"])}
                options={[
                  ["bar", t("surveysModule.design.progressBarOpt")],
                  ["steps", t("surveysModule.design.progressSteps")],
                  ["none", t("surveysModule.design.progressNone")],
                ]}
                disabled={d}
              />
            </div>
          </Group>
        </>
      )}
    </div>
  )
}
