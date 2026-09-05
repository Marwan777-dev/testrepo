// Journey create / edit panel (US-1): a right-side Sheet (slides from the reading-end side,
// RTL-aware) that collects name / journeyType / description. `journey` undefined → create mode
// (POSTs `createJourney`); `journey` present → edit mode (prefilled, PUTs `updateJourney`). A 409
// `journey.name_conflict` surfaces inline on the name field; a 403 `journey.archived_immutable`
// (edit of an Archived journey) and other failures show a generic banner. On success it calls
// `onSaved(journeyId)` so the caller (list / builder) can refresh.
//
// Wire note: the M-16 journey contract stores a single name + description + journeyType — no
// bilingual fields. Edit mode omits `personaIds` from the PUT body, which the backend treats as
// "leave persona bindings unchanged" (JourneyService: `request.PersonaIds is null` → no-op), so
// editing metadata never disturbs the journey's bound personas. This replaces the former
// `CreateJourneyDialog`; the `#journey-name` selector is preserved for the E2E lane.

import { type FormEvent, useEffect, useMemo, useState } from "react"
import { useTranslation } from "react-i18next"
import { Loader2 } from "lucide-react"

import { Button } from "@/components/ui/button"
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet"
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
import { useDirection } from "@/hooks/use-direction"
import { createJourney, updateJourney, JourneysApiError } from "@/features/journeys/api"

// `journey_type` is a free-form tenant value on the wire (data-model.md), but the UI offers the
// four canonical archetypes with localized labels. Values are the stable PascalCase wire form.
const JOURNEY_TYPES = ["Transactional", "Lifecycle", "IssueResolution", "Onboarding"] as const

const TYPE_LABEL: Record<string, string> = {
  Transactional: "journey.typeTransactional",
  Lifecycle: "journey.typeLifecycle",
  IssueResolution: "journey.typeIssueResolution",
  Onboarding: "journey.typeOnboarding",
}

/** The minimal journey shape the edit form prefills from (list summary or full detail both fit). */
export interface JourneyFormTarget {
  journeyId: string
  name: string
  description?: string | null
  journeyType: string
}

interface JourneyFormDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** Present → edit that journey's metadata; undefined/null → create a new journey. */
  journey?: JourneyFormTarget | null
  /** Called with the journey's id after a successful create/update (so the caller can refresh). */
  onSaved: (journeyId: string) => void
}

export function JourneyFormDialog({ open, onOpenChange, journey, onSaved }: JourneyFormDialogProps) {
  const { t } = useTranslation()
  const { isRtl } = useDirection()
  const isEdit = !!journey

  const [name, setName] = useState("")
  const [journeyType, setJourneyType] = useState<string>("")
  const [description, setDescription] = useState("")
  const [nameError, setNameError] = useState<string | null>(null)
  const [typeError, setTypeError] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  // Slide from the reading-end side: right in LTR, left in RTL (matches the touchpoint Sheet).
  const sheetSide = isRtl ? "left" : "right"

  // Prefill (edit) or clear (create) whenever the panel opens or the target journey changes.
  useEffect(() => {
    if (!open) return
    setName(journey?.name ?? "")
    setJourneyType(journey?.journeyType ?? "")
    setDescription(journey?.description ?? "")
    setNameError(null)
    setTypeError(null)
    setFormError(null)
  }, [open, journey])

  // The type picker offers the four archetypes; if the journey under edit carries a free-form type
  // outside that set, surface it as an extra leading option so the Select still reflects its value.
  const typeOptions = useMemo(() => {
    const base: string[] = [...JOURNEY_TYPES]
    if (journey?.journeyType && !base.includes(journey.journeyType)) base.unshift(journey.journeyType)
    return base
  }, [journey])

  const typeLabel = (type: string) => (TYPE_LABEL[type] ? t(TYPE_LABEL[type]) : type)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormError(null)

    const trimmedName = name.trim()
    let invalid = false
    if (!trimmedName) {
      setNameError(t("journey.createNameRequired"))
      invalid = true
    } else {
      setNameError(null)
    }
    if (!journeyType) {
      setTypeError(t("journey.createTypeRequired"))
      invalid = true
    } else {
      setTypeError(null)
    }
    if (invalid) return

    const trimmedDesc = description.trim()
    setSubmitting(true)
    try {
      if (isEdit && journey) {
        // Omit `personaIds` so the backend leaves persona bindings untouched (see file header).
        await updateJourney(journey.journeyId, {
          name: trimmedName,
          description: trimmedDesc || null,
          journeyType,
        })
        onOpenChange(false)
        onSaved(journey.journeyId)
      } else {
        const created = await createJourney({
          name: trimmedName,
          description: trimmedDesc || null,
          journeyType,
        })
        onOpenChange(false)
        onSaved(created.journeyId)
      }
    } catch (err) {
      if (err instanceof JourneysApiError && err.isNameConflict) {
        setNameError(t("journey.createNameConflict"))
      } else if (err instanceof JourneysApiError && err.status === 403) {
        setFormError(t("journey.archivedImmutable"))
      } else {
        setFormError(isEdit ? t("journey.editFailed") : t("journey.createFailed"))
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side={sheetSide} className="w-full gap-0 p-0 sm:max-w-lg">
        <form onSubmit={handleSubmit} noValidate className="flex h-full flex-col">
          <SheetHeader className="border-b border-border px-6 pt-6 pb-4">
            <SheetTitle className="text-lg font-heading font-bold">
              {isEdit ? t("journey.editTitle") : t("journey.newJourney")}
            </SheetTitle>
            <SheetDescription>{isEdit ? t("journey.editDesc") : t("journey.createDesc")}</SheetDescription>
          </SheetHeader>

          <div className="flex-1 min-h-0 space-y-5 overflow-y-auto px-6 py-5">
            {formError && (
              <div
                role="alert"
                className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
              >
                {formError}
              </div>
            )}

            {/* Journey name */}
            <div className="space-y-1.5">
              <Label htmlFor="journey-name">
                {t("journey.formName")} <span className="text-destructive">*</span>
              </Label>
              <Input
                id="journey-name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder={t("journey.formNamePlaceholder")}
                maxLength={255}
                aria-invalid={!!nameError}
                aria-describedby={nameError ? "journey-name-error" : undefined}
                disabled={submitting}
                autoFocus
              />
              {nameError && (
                <p id="journey-name-error" role="alert" className="text-sm text-destructive">
                  {nameError}
                </p>
              )}
            </div>

            {/* Journey type */}
            <div className="space-y-1.5">
              <Label htmlFor="journey-type">
                {t("journey.formType")} <span className="text-destructive">*</span>
              </Label>
              <Select
                value={journeyType}
                onValueChange={(v) => {
                  setJourneyType(v ?? "")
                  setTypeError(null)
                }}
              >
                <SelectTrigger
                  id="journey-type"
                  className="w-full"
                  disabled={submitting}
                  aria-invalid={!!typeError}
                  aria-describedby={typeError ? "journey-type-error" : undefined}
                >
                  <SelectValue>
                    {(v) =>
                      v ? (
                        typeLabel(v as string)
                      ) : (
                        <span className="text-muted-foreground">{t("journey.formTypePlaceholder")}</span>
                      )
                    }
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  {typeOptions.map((type) => (
                    <SelectItem key={type} value={type}>
                      {typeLabel(type)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {typeError && (
                <p id="journey-type-error" role="alert" className="text-sm text-destructive">
                  {typeError}
                </p>
              )}
            </div>

            {/* Description */}
            <div className="space-y-1.5">
              <Label htmlFor="journey-description">{t("journey.formDescription")}</Label>
              <Textarea
                id="journey-description"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder={t("journey.formDescriptionPlaceholder")}
                rows={3}
                className="leading-relaxed"
                disabled={submitting}
              />
            </div>
          </div>

          <SheetFooter className="border-t border-border px-6 pt-3 pb-1">
            <Button
              type="submit"
              className="w-full font-semibold bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
              disabled={submitting}
            >
              {submitting && <Loader2 className="size-4 animate-spin ms-2" aria-hidden />}
              {submitting
                ? isEdit
                  ? t("journey.saving")
                  : t("journey.creating")
                : isEdit
                  ? t("journey.editSubmit")
                  : t("journey.createSubmit")}
            </Button>
            <Button
              type="button"
              variant="outline"
              className="w-full"
              onClick={() => onOpenChange(false)}
              disabled={submitting}
            >
              {t("journey.publishCancel")}
            </Button>
          </SheetFooter>
        </form>
      </SheetContent>
    </Sheet>
  )
}
