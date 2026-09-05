// Stage add/edit dialog (US-1, part of T038): collects a stage's name / description / customer
// goal / expected emotion / duration hint and POSTs (`addStage`) or PUTs (`updateStage`) to the
// M-16 API. `stage` undefined → add mode (appends at the end of the sequence); `stage` present →
// edit mode (prefilled). Errors surface inline (limit reached, archived-immutable, generic). On
// success it calls `onSaved()` so the builder can refetch. RTL-first, design-system styled —
// follows the `JourneyFormDialog` pattern over the journeys API (T035).

import { type FormEvent, useEffect, useState } from "react"
import { useTranslation } from "react-i18next"
import { Loader2 } from "lucide-react"

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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Textarea } from "@/components/ui/textarea"
import { addStage, updateStage, JourneysApiError, type StageDetail } from "@/features/journeys/api"

// Known stage emotions offered by the picker. The wire value is a free string (lowercase key);
// the UI labels them via the existing `journey.emotion*` strings. `expectedEmotion` is optional,
// so a sentinel leads the list (Base UI Select needs non-empty item values — `""` is unusable).
const EMOTION_NONE = "__none__"
const EMOTIONS = [
  "excited",
  "neutral",
  "anxious",
  "frustrated",
  "confident",
  "confused",
  "relieved",
] as const

const EMOTION_LABEL: Record<(typeof EMOTIONS)[number], string> = {
  excited: "journey.emotionExcited",
  neutral: "journey.emotionNeutral",
  anxious: "journey.emotionAnxious",
  frustrated: "journey.emotionFrustrated",
  confident: "journey.emotionConfident",
  confused: "journey.emotionConfused",
  relieved: "journey.emotionRelieved",
}

interface StageFormDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  journeyId: string
  /** Present → edit that stage; undefined → add a new stage at the end of the sequence. */
  stage?: StageDetail
  /** Called after a successful add/update so the builder can refetch the journey tree. */
  onSaved: () => void
}

export function StageFormDialog({
  open,
  onOpenChange,
  journeyId,
  stage,
  onSaved,
}: StageFormDialogProps) {
  const { t } = useTranslation()
  const isEdit = !!stage

  const [name, setName] = useState("")
  const [description, setDescription] = useState("")
  const [customerGoal, setCustomerGoal] = useState("")
  const [emotion, setEmotion] = useState<string>(EMOTION_NONE)
  const [durationHint, setDurationHint] = useState("")
  const [nameError, setNameError] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  // Prefill (edit) or clear (add) whenever the dialog opens or the target stage changes.
  useEffect(() => {
    if (!open) return
    setName(stage?.name ?? "")
    setDescription(stage?.description ?? "")
    setCustomerGoal(stage?.customerGoal ?? "")
    setEmotion(stage?.expectedEmotion ?? EMOTION_NONE)
    setDurationHint(stage?.durationHint ?? "")
    setNameError(null)
    setFormError(null)
  }, [open, stage])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormError(null)

    const trimmedName = name.trim()
    if (!trimmedName) {
      setNameError(t("journey.stageNameRequired"))
      return
    }
    setNameError(null)

    const payload = {
      name: trimmedName,
      description: description.trim() || null,
      customerGoal: customerGoal.trim() || null,
      expectedEmotion: emotion === EMOTION_NONE ? null : emotion,
      durationHint: durationHint.trim() || null,
    }

    setSubmitting(true)
    try {
      if (isEdit && stage) {
        await updateStage(journeyId, stage.stageId, payload)
      } else {
        await addStage(journeyId, payload)
      }
      onOpenChange(false)
      onSaved()
    } catch (err) {
      if (err instanceof JourneysApiError && err.status === 422 && err.code === "journey.stage_limit_reached") {
        setFormError(t("journey.stageLimitReached"))
      } else if (err instanceof JourneysApiError && err.status === 403) {
        setFormError(t("journey.archivedImmutable"))
      } else {
        setFormError(t("journey.stageSaveFailed"))
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="flex max-h-[90vh] flex-col sm:max-w-md">
        <DialogHeader className="shrink-0">
          <DialogTitle>{isEdit ? t("journey.editStageTitle") : t("journey.addStageTitle")}</DialogTitle>
          <DialogDescription>{t("journey.stageFormDesc")}</DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} noValidate className="flex min-h-0 flex-1 flex-col gap-4">
          {/* Body scrolls; header + footer stay pinned. */}
          <div className="min-h-0 flex-1 space-y-4 overflow-y-auto px-1">
          {formError && (
            <div
              role="alert"
              className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
            >
              {formError}
            </div>
          )}

          <div className="space-y-1.5">
            <Label htmlFor="stage-name">
              {t("journey.stageNameLabel")} <span className="text-destructive">*</span>
            </Label>
            <Input
              id="stage-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder={t("journey.stageNamePlaceholder")}
              maxLength={255}
              aria-invalid={!!nameError}
              aria-describedby={nameError ? "stage-name-error" : undefined}
              disabled={submitting}
              autoFocus
            />
            {nameError && (
              <p id="stage-name-error" role="alert" className="text-sm text-destructive">
                {nameError}
              </p>
            )}
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="stage-goal">{t("journey.customerGoal")}</Label>
            <Textarea
              id="stage-goal"
              value={customerGoal}
              onChange={(e) => setCustomerGoal(e.target.value)}
              placeholder={t("journey.customerGoalPlaceholder")}
              rows={2}
              className="leading-relaxed"
              disabled={submitting}
            />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="stage-description">{t("journey.stageDescriptionLabel")}</Label>
            <Textarea
              id="stage-description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder={t("journey.stageDescriptionPlaceholder")}
              rows={2}
              className="leading-relaxed"
              disabled={submitting}
            />
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="stage-emotion">{t("journey.stageEmotion")}</Label>
              <Select value={emotion} onValueChange={(v) => setEmotion(v ?? EMOTION_NONE)}>
                <SelectTrigger id="stage-emotion" className="w-full" disabled={submitting}>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={EMOTION_NONE}>{t("journey.emotionNotSpecified")}</SelectItem>
                  {EMOTIONS.map((value) => (
                    <SelectItem key={value} value={value}>
                      {t(EMOTION_LABEL[value])}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="stage-duration">{t("journey.durationHintLabel")}</Label>
              <Input
                id="stage-duration"
                value={durationHint}
                onChange={(e) => setDurationHint(e.target.value)}
                placeholder={t("journey.durationHintPlaceholder")}
                maxLength={120}
                disabled={submitting}
              />
            </div>
          </div>

          </div>

          <DialogFooter className="shrink-0 gap-2 sm:gap-2">
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
              disabled={submitting}
            >
              {t("journey.publishCancel")}
            </Button>
            <Button
              type="submit"
              className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
              disabled={submitting}
            >
              {submitting && <Loader2 className="size-4 animate-spin ms-2" aria-hidden />}
              {isEdit ? t("journey.saveStage") : t("journey.addStage")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
