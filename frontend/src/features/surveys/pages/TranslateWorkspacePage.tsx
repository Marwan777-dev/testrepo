// F11 Translate workspace (T217): side-by-side EN | AR editor per FR-11.2. The EN
// column is the read-only source (extracted keys); the AR column edits the bundle
// (`dir="rtl"` on each input). A coverage indicator counts missing keys. Q1: NO
// autosave — one explicit Save button per locale bundle. Logical properties only.

import { useCallback, useEffect, useMemo, useState } from "react"
import { useTranslation } from "react-i18next"
import { Languages, Loader2 } from "lucide-react"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { Skeleton } from "@/components/ui/skeleton"
import { Textarea } from "@/components/ui/textarea"
import { ETagConflictError } from "../api/etag"
import {
  getTranslationBundle,
  listTranslationLocales,
  putTranslationBundle,
  type LocaleSummary,
} from "../api/translations-api"
import { EtagConflictDialog } from "../components/EtagConflictDialog"
import { useSurveyEtag } from "../hooks/useSurveyEtag"
import { useUnsavedChangesGuard } from "../hooks/useUnsavedChangesGuard"

/** Long content (welcome/thanks/descriptions) gets a taller editor. */
function isLongKey(key: string): boolean {
  return (
    key === "survey.welcome" || key === "survey.thanks" || key.endsWith(".description")
  )
}

export default function TranslateWorkspacePage({ surveyId }: { surveyId: string }) {
  const { t } = useTranslation()
  const { captureFrom, withIfMatch } = useSurveyEtag()

  // EN = source of truth (English bundle resolves every key to the authored text).
  const [sourceKeys, setSourceKeys] = useState<Record<string, string>>({})
  const [arKeys, setArKeys] = useState<Record<string, string>>({})
  const [baseline, setBaseline] = useState<Record<string, string>>({})
  const [missing, setMissing] = useState<string[]>([])
  const [summary, setSummary] = useState<LocaleSummary | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState(false)
  const [conflictOpen, setConflictOpen] = useState(false)

  const isDirty = useMemo(
    () => JSON.stringify(arKeys) !== JSON.stringify(baseline),
    [arKeys, baseline]
  )
  useUnsavedChangesGuard(isDirty && !saving)

  const load = useCallback(async () => {
    setLoading(true)
    setError(false)
    try {
      const [en, ar, locales] = await Promise.all([
        getTranslationBundle(surveyId, "en").then((r) => r.data),
        captureFrom(() => getTranslationBundle(surveyId, "ar")),
        listTranslationLocales(surveyId),
      ])
      setSourceKeys(en.keys)
      // The AR bundle resolves missing keys to EN — blank them out so the editor
      // shows what still needs translating instead of silently echoing English.
      const edited: Record<string, string> = {}
      for (const [key, value] of Object.entries(ar.keys)) {
        edited[key] = ar.missingKeys.includes(key) ? "" : value
      }
      setArKeys(edited)
      setBaseline(edited)
      setMissing(ar.missingKeys)
      setSummary(locales.find((l) => l.locale === "ar") ?? null)
    } catch {
      setError(true)
    } finally {
      setLoading(false)
    }
  }, [surveyId, captureFrom])

  useEffect(() => {
    void load()
  }, [load])

  const save = async () => {
    setSaving(true)
    setError(false)
    try {
      // Persist only non-empty values — empty = still missing (falls back to EN).
      const keys: Record<string, string> = {}
      for (const [key, value] of Object.entries(arKeys)) {
        if (value.trim() !== "") keys[key] = value
      }
      const bundle = await withIfMatch((ifMatch) =>
        putTranslationBundle(surveyId, "ar", keys, ifMatch)
      )
      setMissing(bundle.missingKeys)
      setBaseline(arKeys)
    } catch (err) {
      if (err instanceof ETagConflictError) {
        setConflictOpen(true)
        return
      }
      setError(true)
    } finally {
      setSaving(false)
    }
  }

  const keyList = useMemo(() => Object.keys(sourceKeys), [sourceKeys])
  const translatedCount = keyList.filter((k) => (arKeys[k] ?? "").trim() !== "").length

  if (loading) {
    return (
      <div className="space-y-5 py-5">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-96 w-full" />
      </div>
    )
  }

  return (
    <div className="space-y-5 py-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
        <div className="min-w-0">
          <div className="flex items-center gap-2.5">
            <Languages className="size-7 shrink-0 text-primary" aria-hidden />
            <h1 className="text-2xl font-heading font-bold">
              {t("surveysModule.translate.title")}
            </h1>
          </div>
          <p className="mt-1 text-sm text-muted-foreground">
            {t("surveysModule.translate.subtitle")}
          </p>
        </div>
        <div className="flex items-center gap-3">
          {/* Coverage indicator (missing-keys) */}
          <Badge
            variant="outline"
            className="tabular-nums"
            aria-live="polite"
          >
            {t("surveysModule.translate.coverage", {
              translated: translatedCount,
              total: keyList.length,
            })}
          </Badge>
          <Button
            className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            onClick={() => void save()}
            disabled={saving || !isDirty}
          >
            {saving && <Loader2 className="size-4 animate-spin" aria-hidden />}
            {t("surveysModule.translate.saveBundle")}
          </Button>
        </div>
      </div>

      {error && (
        <div
          role="alert"
          className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {t("surveysModule.translate.loadError")}
        </div>
      )}

      {keyList.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-lg border border-border bg-card py-16 text-center shadow-sm dark:shadow-none">
          <Languages className="mb-4 size-12 text-muted-foreground" aria-hidden />
          <h3 className="mb-2 text-lg font-bold">{t("surveysModule.translate.empty")}</h3>
          <p className="max-w-sm text-muted-foreground">
            {t("surveysModule.translate.emptyHelp")}
          </p>
        </div>
      ) : (
        <div className="overflow-hidden rounded-lg border border-border bg-card shadow-sm dark:shadow-none">
          {/* Column headers */}
          <div className="grid grid-cols-2 gap-4 border-b border-border bg-muted px-4 py-2 text-xs font-medium uppercase tracking-widest text-muted-foreground">
            <span>{t("surveysModule.translate.sourceColumn")}</span>
            <span>{t("surveysModule.translate.targetColumn")}</span>
          </div>
          <div className="divide-y divide-border">
            {keyList.map((key) => {
              const stillMissing = (arKeys[key] ?? "").trim() === ""
              return (
                <div key={key} className="grid grid-cols-2 gap-4 p-4">
                  <div className="min-w-0 space-y-1">
                    <p className="truncate font-mono text-xs text-muted-foreground" dir="ltr">
                      {key}
                    </p>
                    <p className="text-sm leading-relaxed">{sourceKeys[key]}</p>
                  </div>
                  <div className="min-w-0 space-y-1">
                    <div className="flex items-center justify-between gap-2">
                      <Label htmlFor={`tr-${key}`} className="sr-only">
                        {t("surveysModule.translate.fieldAria", { key })}
                      </Label>
                      {stillMissing && (
                        <Badge className="border-transparent bg-d3-light text-d3-dark dark:bg-d3-dark/25 dark:text-d3-light">
                          {t("surveysModule.translate.missing")}
                        </Badge>
                      )}
                    </div>
                    <Textarea
                      id={`tr-${key}`}
                      dir="rtl"
                      lang="ar"
                      rows={isLongKey(key) ? 4 : 1}
                      className="min-h-10 leading-relaxed"
                      value={arKeys[key] ?? ""}
                      onChange={(e) => setArKeys((prev) => ({ ...prev, [key]: e.target.value }))}
                      disabled={saving}
                    />
                  </div>
                </div>
              )
            })}
          </div>
        </div>
      )}

      {summary && (
        <p className="text-xs text-muted-foreground tabular-nums">
          {t("surveysModule.translate.serverCoverage", { pct: summary.coveragePercent })}
          {" · "}
          {t("surveysModule.translate.missingCount", { count: missing.length })}
        </p>
      )}

      <EtagConflictDialog
        open={conflictOpen}
        localValues={arKeys}
        onReload={() => {
          setConflictOpen(false)
          void load()
        }}
        onDismiss={() => setConflictOpen(false)}
      />
    </div>
  )
}
