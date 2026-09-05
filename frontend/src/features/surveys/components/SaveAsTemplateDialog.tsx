// "Save as template" from a library row — snapshots the survey as a Customized
// template via POST /templates (F7). The snapshot keeps settings/questions/KPI links
// but strips journey & touchpoint bindings (the info note states this per the spec).

import { useEffect, useState } from "react"
import { useTranslation } from "react-i18next"
import { FileText, Loader2 } from "lucide-react"

import { Button } from "@/components/ui/button"
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
import { Textarea } from "@/components/ui/textarea"
import { newIdempotencyKey, type SurveyListItem } from "../api/surveys-api"
import { createTemplate } from "../api/templates-api"

export function SaveAsTemplateDialog({
  survey,
  onClose,
  onSaved,
}: {
  survey: SurveyListItem | null
  onClose: () => void
  onSaved: () => void
}) {
  const { t } = useTranslation()
  const [name, setName] = useState("")
  const [description, setDescription] = useState("")
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(false)

  // Prefill the template name from the source survey each time the dialog opens.
  useEffect(() => {
    if (survey) {
      setName(survey.nameEn)
      setDescription("")
      setError(false)
    }
  }, [survey])

  const save = async () => {
    if (!survey || !name.trim()) return
    setBusy(true)
    setError(false)
    try {
      await createTemplate(
        {
          sourceSurveyId: survey.id,
          nameEn: name.trim(),
          description: description.trim() || null,
        },
        newIdempotencyKey()
      )
      onSaved()
    } catch {
      setError(true)
    } finally {
      setBusy(false)
    }
  }

  return (
    <Dialog open={survey !== null} onOpenChange={(next) => !next && !busy && onClose()}>
      <DialogContent className="flex max-h-[90vh] flex-col sm:max-w-lg">
        <DialogHeader className="shrink-0">
          <DialogTitle>{t("surveysModule.saveTemplate.title")}</DialogTitle>
          <DialogDescription>{survey?.nameEn}</DialogDescription>
        </DialogHeader>
        <div className="min-h-0 flex-1 space-y-4 overflow-y-auto px-1">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="template-name">
              {t("surveysModule.saveTemplate.nameLabel")}{" "}
              <span className="text-destructive">*</span>
            </Label>
            <Input
              id="template-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
            />
            {name.trim() === "" && (
              <p className="text-sm text-destructive" role="alert">
                {t("surveysModule.saveTemplate.nameRequired")}
              </p>
            )}
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="template-description">
              {t("surveysModule.saveTemplate.descLabel")}{" "}
              <span className="font-normal text-muted-foreground">
                {t("common.optional")}
              </span>
            </Label>
            <Textarea
              id="template-description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder={t("surveysModule.saveTemplate.descPlaceholder")}
              className="min-h-20"
            />
          </div>
          <div className="flex items-start gap-2 rounded-md border border-border bg-muted/50 p-3 text-xs text-muted-foreground">
            <FileText className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            <span className="leading-relaxed">{t("surveysModule.saveTemplate.note")}</span>
          </div>
          {error && (
            <p className="text-sm text-destructive" role="alert">
              {t("surveysModule.saveTemplate.error")}
            </p>
          )}
        </div>
        <DialogFooter className="shrink-0 gap-2 sm:gap-2">
          <Button variant="outline" onClick={onClose} disabled={busy}>
            {t("common.cancel")}
          </Button>
          <Button
            className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            onClick={() => void save()}
            disabled={busy || name.trim() === ""}
          >
            {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
            {t("surveysModule.saveTemplate.save")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
