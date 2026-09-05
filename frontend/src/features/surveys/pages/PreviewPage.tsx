// F12 Preview (T224): full-page route rendering the server preview payload inside
// LivePreviewFrame with channel tabs (Desktop | Mobile | WhatsApp | Email — US7) and an
// EN | AR locale switch. The SAME payload re-renders in different chrome client-side.
// Section titles render as headings above each block (FR-12.4). Translated strings
// resolve through the bundle keys (survey.welcome / section.{id}.title /
// question.{id}.text — contracts/translations.md) with EN fallback.

import { useEffect, useMemo, useState } from "react"
import { useTranslation } from "react-i18next"
import { Inbox } from "lucide-react"

import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { getSurveyPreview, type PreviewChannel, type PreviewPayload } from "../api/preview-api"
import type { QuestionView } from "../api/questions-api"
import { LivePreviewFrame, type PreviewDevice } from "../components/LivePreviewFrame"
import { resolveCssColorVar } from "@/lib/resolve-css-color"

function QuestionBlock({
  question,
  tr,
  primaryColour,
}: {
  question: QuestionView
  tr: (key: string, fallback: string) => string
  primaryColour: string
}) {
  const { t } = useTranslation()
  const text = tr(`question.${question.id}.text`, question.text)
  const payload = question.payload as Record<string, unknown> | null

  let answer: React.ReactNode
  switch (question.type) {
    case "Scale":
    case "Kpi": {
      const points = Number((payload?.pointCount as number) ?? 5)
      answer = (
        <div className="flex flex-wrap gap-2">
          {Array.from({ length: Math.min(Math.max(points, 2), 11) }, (_, i) => (
            <span
              key={i}
              className="flex size-8 items-center justify-center rounded-full border text-sm tabular-nums"
              style={{ borderColor: primaryColour, color: primaryColour }}
            >
              {i + 1}
            </span>
          ))}
        </div>
      )
      break
    }
    case "SingleSelect":
    case "MultiSelect": {
      const options = (payload?.options as string[]) ?? []
      answer = (
        <ul className="space-y-1.5">
          {options.map((o, i) => (
            <li key={i} className="flex items-center gap-2 text-sm">
              <span
                className={
                  question.type === "SingleSelect"
                    ? "size-4 rounded-full border border-input"
                    : "size-4 rounded-sm border border-input"
                }
                aria-hidden
              />
              {tr(`question.${question.id}.options.${i}.label`, o)}
            </li>
          ))}
        </ul>
      )
      break
    }
    case "YesNo":
      answer = (
        <div className="flex gap-2">
          {[String(payload?.yesLabel ?? t("common.yes")), String(payload?.noLabel ?? t("common.no"))].map(
            (label) => (
              <span
                key={label}
                className="rounded-md border px-3 py-1.5 text-sm"
                style={{ borderColor: primaryColour, color: primaryColour }}
              >
                {label}
              </span>
            )
          )}
        </div>
      )
      break
    case "InputField":
      answer = (
        <div className="rounded-md border border-input bg-card px-3 py-2 text-sm text-muted-foreground">
          {t("surveysModule.previewPage.inputPlaceholder")}
        </div>
      )
      break
    case "Matrix": {
      const rows = (payload?.rows as string[]) ?? []
      answer = (
        <ul className="space-y-1 text-sm text-muted-foreground">
          {rows.map((r, i) => (
            <li key={i}>• {r}</li>
          ))}
        </ul>
      )
      break
    }
    case "Ranking": {
      const items = (payload?.items as string[]) ?? []
      answer = (
        <ol className="list-decimal space-y-1 ps-5 text-sm text-muted-foreground">
          {items.map((r, i) => (
            <li key={i}>{r}</li>
          ))}
        </ol>
      )
      break
    }
    default:
      answer = null
  }

  return (
    <div className="space-y-2">
      <p className="text-sm font-medium">
        {text || t("surveysModule.builder.untitledQuestion")}
        {question.required && <span className="text-destructive"> *</span>}
      </p>
      {answer}
    </div>
  )
}

export default function PreviewPage({ surveyId }: { surveyId: string }) {
  const { t } = useTranslation()
  const [channel, setChannel] = useState<PreviewChannel>("desktop")
  const [locale, setLocale] = useState<"en" | "ar">("en")
  const [payload, setPayload] = useState<PreviewPayload | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(false)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setError(false)
    getSurveyPreview(surveyId, channel, locale)
      .then((p) => !cancelled && setPayload(p))
      .catch(() => !cancelled && setError(true))
      .finally(() => !cancelled && setLoading(false))
    return () => {
      cancelled = true
    }
  }, [surveyId, channel, locale])

  const tr = useMemo(() => {
    const bundle = payload?.translations ?? {}
    return (key: string, fallback: string) =>
      locale === "en" ? fallback : (bundle[key] ?? fallback)
  }, [payload, locale])

  // Unset theme rows fall back to the TENANT primary (CSS var), not stock cyan,
  // so the previewer follows the tenant branding like the rest of the app.
  const primaryColour = useMemo(() => {
    const fromTheme = payload?.theme.primaryColour
    return fromTheme && fromTheme !== "#0D8BBC"
      ? fromTheme
      : resolveCssColorVar("--primary", "#0D8BBC")
  }, [payload])

  const surveyContent = payload && (
    <div className="space-y-5 p-4" dir={locale === "ar" ? "rtl" : "ltr"}>
      <div className="rounded-md p-3 text-white" style={{ backgroundColor: primaryColour }}>
        <p className="truncate text-sm font-bold">
          {tr("survey.name", payload.survey.nameEn)}
        </p>
      </div>
      {payload.survey.welcomeHtml && (
        <div
          className="text-sm leading-relaxed text-foreground"
          dangerouslySetInnerHTML={{ __html: tr("survey.welcome", payload.survey.welcomeHtml) }}
        />
      )}
      {payload.questions.length === 0 ? (
        <div className="flex flex-col items-center py-10 text-center">
          <Inbox className="mb-3 size-10 text-muted-foreground" aria-hidden />
          <p className="max-w-xs text-sm text-muted-foreground">
            {t("surveysModule.previewPage.empty")}
          </p>
        </div>
      ) : (
        payload.sections.map((section) => {
          const sectionQuestions = payload.questions.filter((q) => q.sectionId === section.id)
          if (sectionQuestions.length === 0) return null
          return (
            <section key={section.id} className="space-y-4">
              {/* FR-12.4: section titles render as headings above each block. */}
              <h3 className="border-b border-border pb-1 text-base font-bold">
                {tr(`section.${section.id}.title`, section.name) ||
                  t("surveysModule.previewPage.untitledSection")}
              </h3>
              {sectionQuestions.map((q) => (
                <QuestionBlock key={q.id} question={q} tr={tr} primaryColour={primaryColour} />
              ))}
            </section>
          )
        })
      )}
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

  return (
    <div className="space-y-5 py-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
        <div className="min-w-0">
          <h1 className="text-2xl font-heading font-bold">{t("surveysModule.previewPage.title")}</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            {payload?.survey.nameEn ?? t("surveysModule.previewPage.subtitle")}
          </p>
        </div>
        {/* Locale switch — preview in each authored language (FR-12.x) */}
        <div className="flex items-center gap-1">
          <Button
            variant={locale === "en" ? "secondary" : "ghost"}
            size="sm"
            onClick={() => setLocale("en")}
            aria-pressed={locale === "en"}
          >
            English
          </Button>
          <Button
            variant={locale === "ar" ? "secondary" : "ghost"}
            size="sm"
            onClick={() => setLocale("ar")}
            aria-pressed={locale === "ar"}
            lang="ar"
          >
            العربية
          </Button>
        </div>
      </div>

      {error && (
        <div
          role="alert"
          className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {t("surveysModule.previewPage.loadError")}
        </div>
      )}

      {loading && !payload ? (
        <div className="space-y-3">
          <Skeleton className="h-10 w-72" />
          <Skeleton className="h-96 w-full" />
        </div>
      ) : (
        <LivePreviewFrame
          device={channel as PreviewDevice}
          onDeviceChange={(next) => setChannel(next as PreviewChannel)}
          primaryColour={primaryColour}
          surveyName={payload ? tr("survey.name", payload.survey.nameEn) : ""}
          channels={["desktop", "mobile", "whatsapp", "email"]}
        >
          {surveyContent}
        </LivePreviewFrame>
      )}
    </div>
  )
}
