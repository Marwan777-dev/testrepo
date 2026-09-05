// F7 Template editor (T200, clickthrough parity): breadcrumb + back, "Edit Template"
// header with Delete template / Save settings / Edit questions, and a MAIN SETTINGS
// card — bilingual name row, description, comma-separated tags, and the snapshot
// info note. Name/description/tags editable for Customized templates; everything
// disabled with a read-only notice for BuiltIn (FR-7.1). Class and Primary sector
// are intentionally NOT authoring inputs (FR-7.3 — curated platform metadata).
// "Edit questions" follows the snapshot model: it instantiates a working-copy survey
// and opens the builder on it.

import { useEffect, useState } from "react"
import { Navigate, useNavigate, useParams } from "react-router"
import { Trans, useTranslation } from "react-i18next"
import {
  ArrowLeft,
  ArrowRight,
  Check,
  Info,
  Loader2,
  Lock,
  PencilLine,
  Settings2,
  Trash2,
} from "lucide-react"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Skeleton } from "@/components/ui/skeleton"
import { Textarea } from "@/components/ui/textarea"
import { useDirection } from "@/hooks/use-direction"
import { useSurveyEtag } from "../hooks/useSurveyEtag"
import { newIdempotencyKey } from "../api/surveys-api"
import {
  deleteTemplate,
  getTemplate,
  instantiateTemplate,
  updateTemplate,
  type TemplateView,
} from "../api/templates-api"

export default function TemplateEditorPage() {
  const { id } = useParams<{ id: string }>()
  if (!id) return <Navigate to="/surveys?tab=templates" replace />
  return <TemplateEditorBody templateId={id} />
}

function TemplateEditorBody({ templateId }: { templateId: string }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { isRtl } = useDirection()
  const BackIcon = isRtl ? ArrowRight : ArrowLeft
  const { captureFrom, withIfMatch, etag } = useSurveyEtag()

  const [template, setTemplate] = useState<TemplateView | null>(null)
  const [nameEn, setNameEn] = useState("")
  const [nameAr, setNameAr] = useState("")
  const [description, setDescription] = useState("")
  // Reference input model: one comma-separated tags field.
  const [tagsText, setTagsText] = useState("")
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [deleteError, setDeleteError] = useState(false)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    captureFrom(() => getTemplate(templateId))
      .then((view) => {
        if (cancelled) return
        setTemplate(view)
        setNameEn(view.nameEn)
        setNameAr(view.nameAr ?? "")
        setDescription(view.description ?? "")
        setTagsText(view.tags.join(", "))
      })
      .catch(() => !cancelled && setError(true))
      .finally(() => !cancelled && setLoading(false))
    return () => {
      cancelled = true
    }
  }, [templateId, captureFrom])

  const builtIn = template?.class === "BuiltIn"
  const locked = builtIn || saving
  const parsedTags = tagsText
    .split(",")
    .map((tag) => tag.trim())
    .filter(Boolean)

  const save = async () => {
    setSaving(true)
    setError(false)
    try {
      const view = await withIfMatch((ifMatch) =>
        updateTemplate(
          templateId,
          { nameEn, nameAr: nameAr || null, description: description || null, tags: parsedTags },
          ifMatch
        )
      )
      setTemplate(view)
      setTagsText(view.tags.join(", "))
    } catch {
      setError(true)
    } finally {
      setSaving(false)
    }
  }

  const editQuestions = async () => {
    setSaving(true)
    try {
      const { surveyId } = await instantiateTemplate(templateId, undefined, newIdempotencyKey())
      navigate(`/surveys/${surveyId}/builder`)
    } catch {
      setError(true)
      setSaving(false)
    }
  }

  const doDelete = async () => {
    setSaving(true)
    setDeleteError(false)
    try {
      await deleteTemplate(templateId, etag ?? undefined)
      navigate("/surveys?tab=templates")
    } catch {
      setDeleteError(true)
      setSaving(false)
    }
  }

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
      <p className="text-xs text-muted-foreground">{t("surveysModule.templates.breadcrumb")}</p>

      {/* Header: back + title/subtitle, Delete / Save settings / Edit questions */}
      <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between lg:gap-4">
        <div className="flex min-w-0 items-start gap-3">
          <Button
            variant="outline"
            size="icon"
            className="mt-0.5 size-9 shrink-0"
            onClick={() => navigate("/surveys?tab=templates")}
            aria-label={t("common.back")}
          >
            <BackIcon className="size-4" aria-hidden />
          </Button>
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-2xl font-heading font-bold">
                {t("surveysModule.templates.editTitle")}
              </h1>
              {builtIn && (
                <Badge variant="outline" className="gap-1">
                  <Lock className="size-3" aria-hidden />
                  {t("surveysModule.templates.builtIn")}
                </Badge>
              )}
            </div>
            <p className="mt-1 max-w-2xl text-sm text-muted-foreground">
              {t("surveysModule.templates.editSubtitle")}
            </p>
          </div>
        </div>
        <div className="flex shrink-0 flex-wrap items-center gap-2">
          {!builtIn && (
            <Button
              variant="ghost"
              className="bg-destructive/10 text-destructive hover:bg-destructive/20 hover:text-destructive"
              onClick={() => setDeleteOpen(true)}
              disabled={saving}
            >
              <Trash2 className="size-4" aria-hidden />
              {t("surveysModule.templates.deleteTemplate")}
            </Button>
          )}
          <Button variant="outline" onClick={() => void save()} disabled={locked}>
            {saving ? (
              <Loader2 className="size-4 animate-spin" aria-hidden />
            ) : (
              <Check className="size-4" aria-hidden />
            )}
            {t("surveysModule.templates.saveSettings")}
          </Button>
          <Button
            className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            onClick={() => void editQuestions()}
            disabled={saving}
          >
            <PencilLine className="size-4" aria-hidden />
            {t("surveysModule.templates.editQuestions")}
          </Button>
        </div>
      </div>

      {/* FR-7.1: built-in templates are read-only for tenants */}
      {builtIn && (
        <div
          role="alert"
          className="rounded-md border border-d3-dark/20 bg-d3-light px-3 py-2 text-sm text-d3-dark dark:bg-d3-dark/25 dark:text-d3-light"
        >
          {t("surveysModule.templates.builtInNotice")}
        </div>
      )}

      {error && (
        <div
          role="alert"
          className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {t("surveysModule.templates.saveError")}
        </div>
      )}

      <Card className="max-w-4xl">
        <CardContent className="space-y-3.5 px-6">
          <div className="flex items-center gap-2">
            <Settings2 className="size-4 text-muted-foreground" aria-hidden />
            <h2 className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">
              {t("surveysModule.templates.mainSettings")}
            </h2>
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="template-name-en" className="leading-relaxed">
                {t("surveysModule.templates.nameEn")}{" "}
                <span className="font-normal text-muted-foreground">
                  {t("surveysModule.templates.nameEnSuffix")}
                </span>
              </Label>
              <Input
                id="template-name-en"
                value={nameEn}
                onChange={(e) => setNameEn(e.target.value)}
                disabled={locked}
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="template-name-ar" className="leading-relaxed">
                {t("surveysModule.templates.nameAr")}{" "}
                <span className="font-normal text-muted-foreground">
                  {t("surveysModule.templates.nameArSuffix")}
                </span>
              </Label>
              <Input
                id="template-name-ar"
                dir="rtl"
                lang="ar"
                value={nameAr}
                onChange={(e) => setNameAr(e.target.value)}
                disabled={locked}
              />
            </div>
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="template-desc" className="leading-relaxed">
              {t("surveysModule.templates.description")}{" "}
              <span className="font-normal text-muted-foreground">
                {t("surveysModule.templates.descSuffix")}
              </span>
            </Label>
            <Textarea
              id="template-desc"
              rows={3}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder={t("surveysModule.templates.descPlaceholder")}
              disabled={locked}
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="template-tags" className="leading-relaxed">
              {t("surveysModule.templates.tags")}{" "}
              <span className="font-normal text-muted-foreground">
                {t("surveysModule.templates.tagsSuffix")}
              </span>
            </Label>
            <Input
              id="template-tags"
              value={tagsText}
              onChange={(e) => setTagsText(e.target.value)}
              disabled={locked}
            />
          </div>

          {/* FR-7.3: Class + Primary sector are curated metadata — display-only. */}
          {template && template.sectors.length > 0 && (
            <div className="flex flex-col gap-1.5">
              <span className="text-sm font-medium">{t("surveysModule.templates.sectors")}</span>
              <div className="flex flex-wrap gap-1.5">
                {template.sectors.map((s) => (
                  <Badge
                    key={s}
                    className="border-transparent bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200"
                  >
                    {s}
                  </Badge>
                ))}
              </div>
            </div>
          )}

          {/* Snapshot info note (reference parity) */}
          <div className="flex items-start gap-2 rounded-md border border-nb-cyan-200 bg-nb-cyan-100/50 p-3 text-xs leading-relaxed text-nb-cyan-800 dark:border-nb-cyan-800 dark:bg-nb-cyan-900/25 dark:text-nb-cyan-200">
            <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            <span>
              <Trans
                i18nKey="surveysModule.templates.templateNote"
                components={{ b: <b className="font-semibold" /> }}
              />
            </span>
          </div>
        </CardContent>
      </Card>

      {/* Delete confirmation */}
      <Dialog open={deleteOpen} onOpenChange={(o) => !o && setDeleteOpen(false)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t("surveysModule.templates.deleteConfirmTitle")}</DialogTitle>
            <DialogDescription>
              {t("surveysModule.templates.deleteConfirmBody")}
            </DialogDescription>
          </DialogHeader>
          {deleteError && (
            <p className="text-sm text-destructive" role="alert">
              {t("surveysModule.templates.deleteFailed")}
            </p>
          )}
          <DialogFooter className="gap-2 sm:gap-2">
            <Button variant="outline" onClick={() => setDeleteOpen(false)} disabled={saving}>
              {t("common.cancel")}
            </Button>
            <Button variant="destructive" onClick={() => void doDelete()} disabled={saving}>
              {t("surveysModule.templates.deleteConfirmCta")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
