// Create-persona panel (T077, US-3): a right-side Sheet (slides from the reading-end side,
// RTL-aware) that collects the bilingual name + description and POSTs to `createPersona`. The
// persona is created in `Draft` status — it must be transitioned to `Active` before it can be
// bound to a journey (FR-005). On success it calls `onCreated(personaId)` so the list page can
// refresh. Mirrors `JourneyFormDialog`'s shape.

import { type FormEvent, useState } from "react"
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
import { Textarea } from "@/components/ui/textarea"
import { useDirection } from "@/hooks/use-direction"
import { createPersona, JourneysApiError } from "@/features/journeys/api"

interface CreatePersonaDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** Called with the new persona's id after a successful create (so the caller can refresh). */
  onCreated: (personaId: string) => void
}

export function CreatePersonaDialog({ open, onOpenChange, onCreated }: CreatePersonaDialogProps) {
  const { t } = useTranslation()
  const { isRtl } = useDirection()
  const [nameAr, setNameAr] = useState("")
  const [nameEn, setNameEn] = useState("")
  const [descriptionAr, setDescriptionAr] = useState("")
  const [descriptionEn, setDescriptionEn] = useState("")
  const [nameArError, setNameArError] = useState<string | null>(null)
  const [nameEnError, setNameEnError] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  // Slide from the reading-end side: right in LTR, left in RTL (matches the journey Sheet).
  const sheetSide = isRtl ? "left" : "right"

  function reset() {
    setNameAr("")
    setNameEn("")
    setDescriptionAr("")
    setDescriptionEn("")
    setNameArError(null)
    setNameEnError(null)
    setFormError(null)
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormError(null)

    const trimmedAr = nameAr.trim()
    const trimmedEn = nameEn.trim()
    let invalid = false
    if (!trimmedAr) {
      setNameArError(t("persona.createNameArRequired"))
      invalid = true
    } else {
      setNameArError(null)
    }
    if (!trimmedEn) {
      setNameEnError(t("persona.createNameEnRequired"))
      invalid = true
    } else {
      setNameEnError(null)
    }
    if (invalid) return

    setSubmitting(true)
    try {
      const created = await createPersona({
        nameAr: trimmedAr,
        nameEn: trimmedEn,
        descriptionAr: descriptionAr.trim() || null,
        descriptionEn: descriptionEn.trim() || null,
      })
      reset()
      onOpenChange(false)
      onCreated(created.personaId)
    } catch (err) {
      if (err instanceof JourneysApiError && err.status === 403) {
        setFormError(t("persona.createForbidden"))
      } else {
        setFormError(t("persona.createFailed"))
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Sheet
      open={open}
      onOpenChange={(o) => {
        if (!o) reset()
        onOpenChange(o)
      }}
    >
      <SheetContent side={sheetSide} className="w-full sm:max-w-md md:max-w-lg">
        <form onSubmit={handleSubmit} noValidate className="flex h-full flex-col">
          <SheetHeader className="border-b border-border">
            <SheetTitle className="text-lg font-heading font-bold">{t("persona.newPersona")}</SheetTitle>
            <SheetDescription>{t("persona.createDesc")}</SheetDescription>
          </SheetHeader>

          <div className="flex-1 min-h-0 space-y-5 overflow-y-auto p-4">
            {formError && (
              <div
                role="alert"
                className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
              >
                {formError}
              </div>
            )}

            {/* Name (Arabic) */}
            <div className="space-y-1.5">
              <Label htmlFor="persona-name-ar">
                {t("persona.nameAr")} <span className="text-destructive">*</span>
              </Label>
              <Input
                id="persona-name-ar"
                value={nameAr}
                onChange={(e) => setNameAr(e.target.value)}
                placeholder={t("persona.nameArPlaceholder")}
                maxLength={255}
                dir="rtl"
                lang="ar"
                aria-invalid={!!nameArError}
                aria-describedby={nameArError ? "persona-name-ar-error" : undefined}
                disabled={submitting}
                autoFocus
              />
              {nameArError && (
                <p id="persona-name-ar-error" role="alert" className="text-sm text-destructive">
                  {nameArError}
                </p>
              )}
            </div>

            {/* Name (English) */}
            <div className="space-y-1.5">
              <Label htmlFor="persona-name-en">
                {t("persona.nameEn")} <span className="text-destructive">*</span>
              </Label>
              <Input
                id="persona-name-en"
                value={nameEn}
                onChange={(e) => setNameEn(e.target.value)}
                placeholder={t("persona.nameEnPlaceholder")}
                maxLength={255}
                dir="ltr"
                lang="en"
                aria-invalid={!!nameEnError}
                aria-describedby={nameEnError ? "persona-name-en-error" : undefined}
                disabled={submitting}
              />
              {nameEnError && (
                <p id="persona-name-en-error" role="alert" className="text-sm text-destructive">
                  {nameEnError}
                </p>
              )}
            </div>

            {/* Description (Arabic) */}
            <div className="space-y-1.5">
              <Label htmlFor="persona-desc-ar">{t("persona.descAr")}</Label>
              <Textarea
                id="persona-desc-ar"
                value={descriptionAr}
                onChange={(e) => setDescriptionAr(e.target.value)}
                placeholder={t("persona.descArPlaceholder")}
                rows={3}
                dir="rtl"
                lang="ar"
                className="leading-relaxed"
                disabled={submitting}
              />
            </div>

            {/* Description (English) */}
            <div className="space-y-1.5">
              <Label htmlFor="persona-desc-en">{t("persona.descEn")}</Label>
              <Textarea
                id="persona-desc-en"
                value={descriptionEn}
                onChange={(e) => setDescriptionEn(e.target.value)}
                placeholder={t("persona.descEnPlaceholder")}
                rows={3}
                dir="ltr"
                lang="en"
                className="leading-relaxed"
                disabled={submitting}
              />
            </div>
          </div>

          <SheetFooter className="border-t border-border">
            <Button
              type="submit"
              className="w-full bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
              disabled={submitting}
            >
              {submitting && <Loader2 className="size-4 animate-spin ms-2" aria-hidden />}
              {submitting ? t("persona.creating") : t("persona.createSubmit")}
            </Button>
            <Button
              type="button"
              variant="outline"
              className="w-full"
              onClick={() => onOpenChange(false)}
              disabled={submitting}
            >
              {t("persona.cancel")}
            </Button>
          </SheetFooter>
        </form>
      </SheetContent>
    </Sheet>
  )
}
