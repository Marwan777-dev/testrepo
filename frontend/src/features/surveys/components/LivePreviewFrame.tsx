// F4/F12 live preview frame (T095, extended by T225): Desktop / Mobile browser chrome
// plus WhatsApp / Email chrome variants (US7). The inner survey content is identical
// across channels — only the outer frame changes. Re-renders instantly on theme
// changes (SC-003 — plain React state, no fetch).
//
// THEMING EXEMPTION (CLAUDE.md § Theming self-review — "third-party brand mockups"):
// the WhatsApp and Email shells below simulate EXTERNAL apps, so their chrome colours
// (WhatsApp teal header/wallpaper tint, bubble white) are intentionally-fixed inline
// styles and MUST NOT re-theme with the tenant. The survey theme colour itself is
// tenant DATA (also inline by design). Neutral Nabadat chrome stays on tokens.

import { type ReactNode } from "react"
import { useTranslation } from "react-i18next"
import { Mail, MessageCircle, Monitor, Smartphone } from "lucide-react"

import { cn } from "@/lib/utils"
import { RADIUS_PX, type SurveyThemeDraft } from "./SurveyDesignControls"

export type PreviewDevice = "desktop" | "mobile" | "whatsapp" | "email"

/** Minimal question shape for the themed design-step preview. */
export interface PreviewQuestionLite {
  text: string
  type: string
  required: boolean
  options: string[]
  points: number
}

const FONT_STACK: Record<string, string> = {
  Sora: "Sora, sans-serif",
  Poppins: "Poppins, sans-serif",
  "IBM Plex Sans Arabic": "'IBM Plex Sans Arabic', sans-serif",
  System: "system-ui, sans-serif",
}

const CHANNEL_META: Record<PreviewDevice, { icon: typeof Monitor; labelKey: string }> = {
  desktop: { icon: Monitor, labelKey: "surveysModule.appearance.desktop" },
  mobile: { icon: Smartphone, labelKey: "surveysModule.appearance.mobile" },
  whatsapp: { icon: MessageCircle, labelKey: "surveysModule.previewPage.whatsapp" },
  email: { icon: Mail, labelKey: "surveysModule.previewPage.email" },
}

/** Slug for the mock URL bar — mirrors the reference's survey.nabadat.app/s/{slug}. */
function slugOf(name: string): string {
  const slug = name
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
  return slug || "survey"
}

export function LivePreviewFrame({
  device,
  onDeviceChange,
  primaryColour,
  surveyName,
  welcomeHtml,
  channels = ["desktop", "mobile"],
  theme,
  questions,
  children,
}: {
  device: PreviewDevice
  onDeviceChange: (next: PreviewDevice) => void
  /** Resolved theme colour (Inherited → tenant guideline, Customized → theme row). */
  primaryColour: string
  surveyName: string
  welcomeHtml?: string | null
  /** Which channel tabs to offer (F4 uses desktop/mobile; F12 preview all four). */
  channels?: PreviewDevice[]
  /** Full design-step theme — when set, the sample block styles from it (F4). */
  theme?: SurveyThemeDraft
  /** The survey's real questions (design step) — rendered themed; sample otherwise. */
  questions?: PreviewQuestionLite[]
  /** Full survey content (F12 preview). When absent, the F4 sample block renders. */
  children?: ReactNode
}) {
  const { t } = useTranslation()

  // ── Themed sample (design step) — every theme key visibly drives the preview.
  // Fixed 400px scroll body (reference parity) so long content scrolls in place. ──
  const themedContent = theme ? (
    <div
      className="h-[400px] overflow-y-auto p-4"
      style={{
        background:
          theme.bgType === "gradient"
            ? `linear-gradient(${theme.gradAngle}deg, ${theme.gradFrom}, ${theme.gradTo})`
            : theme.bgType !== "solid" && theme.bgImage
              ? `url(${theme.bgImage}) ${theme.bgType === "pattern" ? "repeat" : "center / cover"}`
              : theme.background,
        opacity: theme.bgOpacity / 100,
      }}
    >
      <div
        className="space-y-4 p-4"
        style={{
          backgroundColor: theme.card,
          border: `1px solid ${theme.border}`,
          borderRadius: RADIUS_PX[theme.radius],
          color: theme.textColor,
          fontFamily: FONT_STACK[theme.bodyFont] ?? FONT_STACK.Poppins,
          fontSize: `${theme.bodySize}px`,
          lineHeight: theme.lineHeight,
        }}
      >
        {(theme.showLogo || theme.showTitle) && (
          <div
            className={cn(
              "flex items-center gap-2",
              theme.headerAlign === "center" && "justify-center"
            )}
          >
            {theme.showLogo &&
              (theme.logo ? (
                <img src={theme.logo} alt="" className="h-7 object-contain" />
              ) : (
                <span
                  className="flex size-7 items-center justify-center rounded-md text-xs font-bold"
                  style={{ backgroundColor: theme.primary, color: theme.buttonText }}
                  aria-hidden
                >
                  {(surveyName || "S")[0]}
                </span>
              ))}
            {theme.showTitle && (
              <span
                className="truncate font-bold"
                style={{
                  fontFamily: FONT_STACK[theme.headingFont] ?? FONT_STACK.Sora,
                  fontSize: `${theme.headingSize}px`,
                }}
              >
                {surveyName || t("surveysModule.appearance.sampleTitle")}
              </span>
            )}
          </div>
        )}
        {theme.progress === "bar" && (
          <div className="h-1.5 overflow-hidden rounded-full" style={{ backgroundColor: theme.border }}>
            <div className="h-full w-1/3 rounded-full" style={{ backgroundColor: theme.primary }} />
          </div>
        )}
        {theme.progress === "steps" && (
          <div className="flex gap-1.5">
            {[0, 1, 2].map((i) => (
              <span
                key={i}
                className="h-1.5 flex-1 rounded-full"
                style={{ backgroundColor: i === 0 ? theme.primary : theme.border }}
              />
            ))}
          </div>
        )}
        {welcomeHtml ? (
          <div dangerouslySetInnerHTML={{ __html: welcomeHtml }} />
        ) : (
          <p style={{ opacity: 0.8 }}>{t("surveysModule.appearance.sampleWelcome")}</p>
        )}
        {/* Real survey questions when provided; the F4 sample question otherwise. */}
        {questions && questions.length === 0 ? (
          <p style={{ opacity: 0.6 }}>{t("surveysModule.previewPage.empty")}</p>
        ) : (
          (questions ?? [
            {
              text: t("surveysModule.appearance.sampleQuestion"),
              type: "Scale",
              required: false,
              options: [],
              points: 5,
            },
          ]).map((q, qi) => (
            <div
              key={qi}
              className="space-y-2 p-3"
              style={{
                border: `1px solid ${theme.border}`,
                borderRadius: RADIUS_PX[theme.radius],
              }}
            >
              <p className="font-semibold">
                {q.text}
                {q.required && <span style={{ color: theme.error }}> *</span>}
              </p>
              {q.type === "SingleSelect" || q.type === "MultiSelect" ? (
                <div className="space-y-1.5">
                  {(q.options.length > 0 ? q.options : ["Option 1", "Option 2"]).map((o, oi) => (
                    <div
                      key={oi}
                      className="flex items-center gap-2 px-3 py-1.5 text-sm"
                      style={{
                        border: `1px solid ${theme.border}`,
                        borderRadius: RADIUS_PX[theme.btnRadius],
                      }}
                    >
                      <span
                        className="size-3.5 shrink-0"
                        style={{
                          border: `1.5px solid ${oi === 0 ? theme.primary : theme.border}`,
                          backgroundColor: oi === 0 ? theme.primary : "transparent",
                          borderRadius: q.type === "SingleSelect" ? "50%" : 3,
                        }}
                      />
                      {o}
                    </div>
                  ))}
                </div>
              ) : q.type === "YesNo" ? (
                <div className="flex gap-2">
                  {[t("common.yes"), t("common.no")].map((label, li) => (
                    <span
                      key={label}
                      className="px-4 py-1.5 text-sm"
                      style={{
                        border: `1.5px solid ${li === 0 ? theme.primary : theme.border}`,
                        backgroundColor: li === 0 ? theme.primary : "transparent",
                        color: li === 0 ? theme.buttonText : theme.textColor,
                        borderRadius: RADIUS_PX[theme.btnRadius],
                      }}
                    >
                      {label}
                    </span>
                  ))}
                </div>
              ) : q.type === "InputField" ? (
                <div
                  className="h-9 px-3 py-2 text-sm"
                  style={{
                    border: `1px solid ${theme.border}`,
                    borderRadius: RADIUS_PX[theme.btnRadius],
                    opacity: 0.6,
                  }}
                />
              ) : (
                <div className="flex flex-wrap gap-1.5">
                  {Array.from({ length: Math.min(Math.max(q.points, 2), 11) }).map((_, i) => {
                    const sel = i + 1 === Math.ceil(Math.max(q.points, 2) * 0.8)
                    return (
                      <span
                        key={i}
                        className="flex size-8 items-center justify-center text-sm tabular-nums"
                        style={{
                          border: `1.5px solid ${sel ? theme.primary : theme.border}`,
                          backgroundColor: sel ? theme.primary : "transparent",
                          color: sel ? theme.buttonText : theme.textColor,
                          borderRadius: RADIUS_PX[theme.btnRadius],
                        }}
                      >
                        {i + 1}
                      </span>
                    )
                  })}
                </div>
              )}
            </div>
          ))
        )}
        <button
          type="button"
          tabIndex={-1}
          className="pointer-events-none w-full py-2.5 text-sm font-semibold"
          style={{
            backgroundColor: theme.buttonColor,
            color: theme.buttonText,
            border: `1px solid ${theme.btnBorder}`,
            borderRadius: RADIUS_PX[theme.btnRadius],
          }}
        >
          {t("common.submit")}
        </button>
        {theme.footerText && (
          <p className="text-center text-xs" style={{ opacity: 0.6 }}>
            {theme.footerText}
          </p>
        )}
      </div>
    </div>
  ) : null

  const content = children ?? themedContent ?? (
    <div className="space-y-4 p-4">
      {/* Simulated respondent header — driven by the theme colour (data, not chrome). */}
      <div className="rounded-md p-3 text-white" style={{ backgroundColor: primaryColour }}>
        <p className="truncate text-sm font-bold">
          {surveyName || t("surveysModule.appearance.sampleTitle")}
        </p>
      </div>
      {welcomeHtml ? (
        // Sanitised server-side on save (Q3 allowlist); preview renders the same HTML.
        <div
          className="text-sm leading-relaxed text-foreground"
          dangerouslySetInnerHTML={{ __html: welcomeHtml }}
        />
      ) : (
        <p className="text-sm text-muted-foreground">
          {t("surveysModule.appearance.sampleWelcome")}
        </p>
      )}
      <div className="space-y-2">
        <p className="text-sm font-medium">{t("surveysModule.appearance.sampleQuestion")}</p>
        <div className="flex gap-2">
          {[1, 2, 3, 4, 5].map((n) => (
            <span
              key={n}
              className="flex size-8 items-center justify-center rounded-full border text-sm tabular-nums"
              style={{ borderColor: primaryColour, color: primaryColour }}
            >
              {n}
            </span>
          ))}
        </div>
      </div>
      <button
        type="button"
        tabIndex={-1}
        className="pointer-events-none rounded-md px-4 py-2 text-sm font-medium text-white"
        style={{ backgroundColor: primaryColour }}
      >
        {t("common.submit")}
      </button>
    </div>
  )

  const browserChrome = (
    <>
      <div className="flex items-center gap-2 border-b border-border bg-muted px-3 py-2">
        <span className="flex gap-1.5" aria-hidden>
          <span className="size-2.5 rounded-full bg-nb-stone-lt" />
          <span className="size-2.5 rounded-full bg-nb-stone-lt" />
          <span className="size-2.5 rounded-full bg-nb-stone-lt" />
        </span>
        {/* Mock URL bar (reference parity) */}
        <span
          dir="ltr"
          className="min-w-0 flex-1 truncate rounded-full border border-border bg-card px-3 py-1 text-xs text-muted-foreground"
        >
          survey.nabadat.app/s/{slugOf(surveyName)}
        </span>
      </div>
      {content}
    </>
  )

  // Third-party mockup — fixed WhatsApp colours by design (see exemption note above).
  const whatsappChrome = (
    <div style={{ backgroundColor: "#E5DDD5" }}>
      <div className="flex items-center gap-2 px-3 py-2" style={{ backgroundColor: "#075E54" }}>
        <span
          className="flex size-8 items-center justify-center rounded-full text-sm font-bold"
          style={{ backgroundColor: "#128C7E", color: "#FFFFFF" }}
          aria-hidden
        >
          {(surveyName || "S")[0]}
        </span>
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold" style={{ color: "#FFFFFF" }}>
            {surveyName || t("surveysModule.appearance.sampleTitle")}
          </p>
          <p className="text-xs" style={{ color: "#B7DFD9" }}>
            {t("surveysModule.previewPage.whatsappOnline")}
          </p>
        </div>
      </div>
      <div className="p-3">
        <div
          className="overflow-hidden rounded-lg shadow-sm"
          style={{ backgroundColor: "#FFFFFF" }}
        >
          {content}
        </div>
      </div>
    </div>
  )

  // Third-party mockup — generic mail-client shell; neutral tokens are fine here
  // (it's not a specific brand), but the layout mimics an email reader.
  const emailChrome = (
    <>
      <div className="space-y-1 border-b border-border bg-muted px-4 py-3 text-sm">
        <p className="font-semibold text-foreground">
          {t("surveysModule.previewPage.emailSubject", {
            name: surveyName || t("surveysModule.appearance.sampleTitle"),
          })}
        </p>
        <p className="text-xs text-muted-foreground" dir="ltr">
          {t("surveysModule.previewPage.emailFrom")}
        </p>
      </div>
      {content}
    </>
  )

  return (
    <div className="space-y-3">
      {/* Channel switcher — underlined tab row (reference parity) */}
      <div className="flex items-center gap-1 border-b border-border">
        {channels.map((c) => {
          const Icon = CHANNEL_META[c].icon
          return (
            <button
              key={c}
              type="button"
              onClick={() => onDeviceChange(c)}
              aria-pressed={device === c}
              className={cn(
                "-mb-px inline-flex items-center gap-1.5 border-b-2 px-3 py-2 text-sm font-medium transition-colors",
                device === c
                  ? "border-primary text-foreground"
                  : "border-transparent text-muted-foreground hover:text-foreground"
              )}
            >
              <Icon className="size-4" aria-hidden />
              {t(CHANNEL_META[c].labelKey)}
            </button>
          )
        })}
      </div>

      {/* Stage + frame — the muted stage backdrop centres each device mockup.
          The mobile phone bezel is a DEVICE mockup (CLAUDE.md third-party/device
          exemption): fixed navy bezel, radius above the 16px cap by design. */}
      <div className="flex justify-center rounded-lg bg-muted/40 p-6 dark:bg-muted/20">
        <div
          className={cn(
            "overflow-hidden bg-card",
            device === "mobile"
              ? "w-[320px] rounded-[34px] border-[10px] border-nb-navy shadow-md"
              : "rounded-lg border border-border shadow-sm dark:shadow-none",
            device === "whatsapp" ? "w-full max-w-xs" : device === "mobile" ? "" : "w-full"
          )}
          role="img"
          aria-label={t("surveysModule.appearance.previewAria")}
        >
          {device === "whatsapp"
            ? whatsappChrome
            : device === "email"
              ? emailChrome
              : device === "mobile"
                ? (content)
                : browserChrome}
        </div>
      </div>
    </div>
  )
}
