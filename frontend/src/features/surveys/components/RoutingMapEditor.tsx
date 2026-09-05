// F9 per-question routing editor (T179): opens as a Sheet from the question card.
// One row per answer key with a "Go to" Select; the default target is "next question"
// — rendered but NOT persisted (the saved map is sparse, research.md §6). "End survey"
// persists the "__end" sentinel. Saves through routing-api with the question's ETag.

import { useCallback, useEffect, useState } from "react"
import { useTranslation } from "react-i18next"
import { Loader2 } from "lucide-react"

import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Sheet, SheetContent, SheetFooter, SheetHeader, SheetTitle } from "@/components/ui/sheet"
import { Skeleton } from "@/components/ui/skeleton"
import { useDirection } from "@/hooks/use-direction"
import {
  getQuestionRouting,
  ROUTING_END_SENTINEL,
  saveQuestionRouting,
} from "../api/routing-api"
import { formatETag } from "../api/etag"
import { routingAnswerKeys, type BuilderQuestion } from "./builder-types"

/** Sentinel for the unsaved default ("next question") — never sent to the server. */
const DEFAULT_TARGET = "__default"

export interface RoutingTargetOption {
  questionId: string
  label: string
}

export function RoutingMapEditor({
  open,
  question,
  surveyId,
  /** Candidate target questions (standalone, after this one in order — page-derived). */
  targets,
  onClose,
  onSaved,
}: {
  open: boolean
  question: BuilderQuestion | null
  surveyId: string
  targets: RoutingTargetOption[]
  onClose: () => void
  /** Reports the refreshed hasRouting flag + new row version back to the canvas. */
  onSaved: (questionLocalId: string, hasRouting: boolean, rowVersion: number | null) => void
}) {
  const { t } = useTranslation()
  const { isRtl } = useDirection()
  const sheetSide = isRtl ? ("left" as const) : ("right" as const)

  const [map, setMap] = useState<Record<string, string>>({})
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState(false)

  const serverId = question?.serverId ?? null

  useEffect(() => {
    if (!open || !question) return
    setError(false)
    if (!serverId) {
      // Unsaved question — no server map yet; edit from a blank sparse map.
      setMap({})
      return
    }
    let cancelled = false
    setLoading(true)
    getQuestionRouting(surveyId, serverId)
      .then((view) => !cancelled && setMap(view.map))
      .catch(() => !cancelled && setError(true))
      .finally(() => !cancelled && setLoading(false))
    return () => {
      cancelled = true
    }
  }, [open, question, serverId, surveyId])

  const save = useCallback(async () => {
    if (!question || !serverId) return
    setSaving(true)
    setError(false)
    try {
      const ifMatch = question.rowVersion != null ? formatETag(question.rowVersion) : undefined
      const { data, etag } = await saveQuestionRouting(surveyId, serverId, map, ifMatch)
      const nextVersion = etag ? Number(etag.replace(/\D/g, "")) : null
      onSaved(question.localId, data.hasRouting, Number.isNaN(nextVersion) ? null : nextVersion)
      onClose()
    } catch {
      setError(true)
    } finally {
      setSaving(false)
    }
  }, [question, serverId, surveyId, map, onSaved, onClose])

  if (!question) return null
  const answerKeys = routingAnswerKeys(question)

  return (
    <Sheet open={open} onOpenChange={(o) => !o && onClose()}>
      <SheetContent side={sheetSide} className="w-full sm:max-w-md">
        <SheetHeader className="shrink-0">
          <SheetTitle>{t("surveysModule.routing.title")}</SheetTitle>
        </SheetHeader>

        <div className="min-h-0 flex-1 space-y-4 overflow-y-auto px-4">
          <p className="text-sm text-muted-foreground">
            {t("surveysModule.routing.hint", {
              question: question.text || t("surveysModule.builder.untitledQuestion"),
            })}
          </p>

          {error && (
            <div
              role="alert"
              className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
            >
              {t("surveysModule.routing.error")}
            </div>
          )}

          {!serverId && (
            <div
              role="alert"
              className="rounded-md border border-d3-dark/20 bg-d3-light px-3 py-2 text-sm text-d3-dark dark:bg-d3-dark/25 dark:text-d3-light"
            >
              {t("surveysModule.routing.saveFirst")}
            </div>
          )}

          {loading ? (
            <div className="space-y-2">
              {Array.from({ length: 4 }).map((_, i) => (
                <Skeleton key={i} className="h-10 w-full" />
              ))}
            </div>
          ) : (
            answerKeys.map((key) => {
              const selected = map[key] ?? DEFAULT_TARGET
              return (
                <div key={key} className="flex items-center gap-3">
                  <span className="w-24 shrink-0 truncate text-sm font-medium" title={key}>
                    {key}
                  </span>
                  <Label htmlFor={`route-${key}`} className="sr-only">
                    {t("surveysModule.routing.goTo", { answer: key })}
                  </Label>
                  <Select
                    value={selected}
                    onValueChange={(v) => {
                      setMap((m) => {
                        const next = { ...m }
                        // The default is never persisted — deleting the entry restores it.
                        if (!v || v === DEFAULT_TARGET) delete next[key]
                        else next[key] = v
                        return next
                      })
                    }}
                    disabled={saving || !serverId}
                  >
                    <SelectTrigger id={`route-${key}`} className="w-full">
                      <SelectValue>
                        {(v) => {
                          const value = String(v ?? DEFAULT_TARGET)
                          if (value === DEFAULT_TARGET)
                            return t("surveysModule.routing.nextQuestion")
                          if (value === ROUTING_END_SENTINEL)
                            return t("surveysModule.routing.endSurvey")
                          return (
                            targets.find((tgt) => tgt.questionId === value)?.label ?? value
                          )
                        }}
                      </SelectValue>
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={DEFAULT_TARGET}>
                        {t("surveysModule.routing.nextQuestion")}
                      </SelectItem>
                      {targets.map((tgt) => (
                        <SelectItem key={tgt.questionId} value={tgt.questionId}>
                          {tgt.label}
                        </SelectItem>
                      ))}
                      <SelectItem value={ROUTING_END_SENTINEL}>
                        {t("surveysModule.routing.endSurvey")}
                      </SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              )
            })
          )}
        </div>

        <SheetFooter className="shrink-0 gap-2">
          <Button variant="outline" onClick={onClose} disabled={saving}>
            {t("common.cancel")}
          </Button>
          <Button
            className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            onClick={() => void save()}
            disabled={saving || !serverId}
          >
            {saving && <Loader2 className="size-4 animate-spin" aria-hidden />}
            {t("common.save")}
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  )
}
