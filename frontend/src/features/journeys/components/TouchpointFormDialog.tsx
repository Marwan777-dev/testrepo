// Touchpoint add/edit dialog (US-1, part of T038): collects a touchpoint's name / description /
// channels / importance / MoT / mandatory flags and POSTs (`addTouchpoint`) or PUTs
// (`updateTouchpoint`) to the M-16 API. `touchpoint` undefined → add mode (under `stageId`);
// `touchpoint` present → edit mode (prefilled). Channels are picked as toggle chips from the
// tenant channel library. Errors surface inline (limit reached, archived-immutable, generic).
// On success it calls `onSaved()` so the builder can refetch. RTL-first, design-system styled.
//
// Note: `GET /journeys/{id}` does not return a touchpoint's `description`, so the field starts
// blank in edit mode (the user can re-enter it); name/channels/importance/flags prefill fully.

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
import { Switch } from "@/components/ui/switch"
import { Textarea } from "@/components/ui/textarea"
import { DEFAULT_CHANNELS } from "@/lib/journey-data"
import {
  addTouchpoint,
  updateTouchpoint,
  JourneysApiError,
  type TouchpointDetail,
  type TouchpointImportance,
} from "@/features/journeys/api"

const IMPORTANCES: TouchpointImportance[] = ["Low", "Medium", "High", "Critical"]
const IMPORTANCE_LABEL: Record<TouchpointImportance, string> = {
  Low: "journey.impLow",
  Medium: "journey.impMedium",
  High: "journey.impHigh",
  Critical: "journey.impCritical",
}

interface TouchpointFormDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** Parent stage — touchpoints are added under it (`POST /stages/{stageId}/touchpoints`). */
  stageId: string
  /** Present → edit that touchpoint; undefined → add a new touchpoint to the stage. */
  touchpoint?: TouchpointDetail
  /** Called after a successful add/update so the builder can refetch the journey tree. */
  onSaved: () => void
}

export function TouchpointFormDialog({
  open,
  onOpenChange,
  stageId,
  touchpoint,
  onSaved,
}: TouchpointFormDialogProps) {
  const { t, i18n } = useTranslation()
  const isArabic = i18n.language === "ar"
  const isEdit = !!touchpoint

  const [name, setName] = useState("")
  const [description, setDescription] = useState("")
  const [channels, setChannels] = useState<string[]>([])
  const [importance, setImportance] = useState<TouchpointImportance>("Medium")
  const [isMoT, setIsMoT] = useState(false)
  const [isMandatory, setIsMandatory] = useState(false)
  const [nameError, setNameError] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  // Prefill (edit) or clear (add) whenever the dialog opens or the target touchpoint changes.
  useEffect(() => {
    if (!open) return
    setName(touchpoint?.name ?? "")
    setDescription("")
    setChannels(touchpoint?.channels ?? [])
    setImportance(touchpoint?.importance ?? "Medium")
    setIsMoT(touchpoint?.isMoT ?? false)
    setIsMandatory(touchpoint?.isMandatory ?? false)
    setNameError(null)
    setFormError(null)
  }, [open, touchpoint])

  function toggleChannel(key: string) {
    setChannels((prev) => (prev.includes(key) ? prev.filter((c) => c !== key) : [...prev, key]))
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormError(null)

    const trimmedName = name.trim()
    if (!trimmedName) {
      setNameError(t("journey.tpNameRequired"))
      return
    }
    setNameError(null)

    const payload = {
      name: trimmedName,
      description: description.trim() || null,
      channels,
      importance,
      isMoT,
      isMandatory,
    }

    setSubmitting(true)
    try {
      if (isEdit && touchpoint) {
        await updateTouchpoint(touchpoint.touchpointId, payload)
      } else {
        await addTouchpoint(stageId, payload)
      }
      onOpenChange(false)
      onSaved()
    } catch (err) {
      if (
        err instanceof JourneysApiError &&
        err.status === 422 &&
        err.code === "journey.touchpoint_limit_reached"
      ) {
        setFormError(t("journey.touchpointLimitReached"))
      } else if (err instanceof JourneysApiError && err.status === 403) {
        setFormError(t("journey.archivedImmutable"))
      } else {
        setFormError(t("journey.tpSaveFailed"))
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="flex max-h-[90vh] flex-col sm:max-w-lg">
        <DialogHeader className="shrink-0">
          <DialogTitle>
            {isEdit ? t("journey.editTouchpointTitle") : t("journey.addTouchpointTitle")}
          </DialogTitle>
          <DialogDescription>{t("journey.touchpointFormDesc")}</DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} noValidate className="flex min-h-0 flex-1 flex-col gap-4">
          {/* Body scrolls; header + footer stay pinned. min-h-0 lets this flex child shrink. */}
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
            <Label htmlFor="tp-name">
              {t("journey.tpNameLabel")} <span className="text-destructive">*</span>
            </Label>
            <Input
              id="tp-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder={t("journey.tpNamePlaceholder")}
              maxLength={255}
              aria-invalid={!!nameError}
              aria-describedby={nameError ? "tp-name-error" : undefined}
              disabled={submitting}
              autoFocus
            />
            {nameError && (
              <p id="tp-name-error" role="alert" className="text-sm text-destructive">
                {nameError}
              </p>
            )}
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="tp-description">{t("journey.tpDescriptionLabel")}</Label>
            <Textarea
              id="tp-description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder={t("journey.tpDescriptionPlaceholder")}
              rows={2}
              className="leading-relaxed"
              disabled={submitting}
            />
          </div>

          <div className="space-y-1.5">
            <Label id="tp-channels-label">{t("journey.channels")}</Label>
            <p className="text-sm text-muted-foreground">{t("journey.channelsHelp")}</p>
            <div
              className="flex flex-wrap gap-2 pt-1"
              role="group"
              aria-labelledby="tp-channels-label"
            >
              {DEFAULT_CHANNELS.map((channel) => {
                const selected = channels.includes(channel.key)
                return (
                  <Button
                    key={channel.key}
                    type="button"
                    size="sm"
                    variant={selected ? "secondary" : "outline"}
                    aria-pressed={selected}
                    onClick={() => toggleChannel(channel.key)}
                    disabled={submitting}
                    className="rounded-full"
                  >
                    {isArabic ? channel.label.ar : channel.label.en}
                  </Button>
                )
              })}
            </div>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="tp-importance">{t("journey.importanceLabel")}</Label>
            <Select
              value={importance}
              onValueChange={(v) => setImportance(v as TouchpointImportance)}
            >
              <SelectTrigger id="tp-importance" className="w-full" disabled={submitting}>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {IMPORTANCES.map((value) => (
                  <SelectItem key={value} value={value}>
                    {t(IMPORTANCE_LABEL[value])}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-3 rounded-md border border-border bg-card p-3">
            <div className="flex items-start justify-between gap-3">
              <div className="space-y-0.5">
                <Label htmlFor="tp-mot" className="font-medium">
                  {t("journey.motToggle")}
                </Label>
                <p className="text-sm text-muted-foreground">{t("journey.motToggleHelp")}</p>
              </div>
              <Switch
                id="tp-mot"
                checked={isMoT}
                onCheckedChange={setIsMoT}
                disabled={submitting}
                aria-label={t("journey.motToggle")}
              />
            </div>
            <div className="flex items-start justify-between gap-3">
              <div className="space-y-0.5">
                <Label htmlFor="tp-mandatory" className="font-medium">
                  {t("journey.mandatoryToggle")}
                </Label>
                <p className="text-sm text-muted-foreground">{t("journey.mandatoryHelp")}</p>
              </div>
              <Switch
                id="tp-mandatory"
                checked={isMandatory}
                onCheckedChange={setIsMandatory}
                disabled={submitting}
                aria-label={t("journey.mandatoryToggle")}
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
              {isEdit ? t("journey.saveTouchpoint") : t("journey.addTouchpoint")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
