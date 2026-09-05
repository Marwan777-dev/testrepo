// T140 [US6] — Logo upload field. File picker accepting PNG/JPG/SVG; previews the current logo via
// `<img src={logoUrl}>` (rendering the served bytes as an image is defence-in-depth on top of the
// server-side SVG sanitiser — the browser never executes script in an <img>). A file over the 2 MB
// soft limit raises a non-blocking warning but still uploads (FR-050 / spec scenario 3). The
// success / "sanitised" / error toasts are owned by the page, driven by the upload outcome.

import { useRef } from "react"
import { useTranslation } from "react-i18next"
import { ImageOff, Loader2, Upload } from "lucide-react"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"

const ACCEPTED = "image/png,image/jpeg,image/svg+xml"
const RECOMMENDED_MAX_BYTES = 2 * 1024 * 1024

interface LogoUploadFieldProps {
  logoUrl: string | null
  uploading: boolean
  disabled?: boolean
  onSelect: (file: File) => void
}

export function LogoUploadField({ logoUrl, uploading, disabled, onSelect }: LogoUploadFieldProps) {
  const { t } = useTranslation()
  const inputRef = useRef<HTMLInputElement>(null)

  const handleChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    // Reset so picking the same file again re-fires onChange.
    event.target.value = ""
    if (!file) return

    if (file.size > RECOMMENDED_MAX_BYTES) {
      toast.warning(t("settings.organization.logoSizeWarning"))
    }
    onSelect(file)
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-4">
        <div className="flex size-20 shrink-0 items-center justify-center overflow-hidden rounded-md border border-border bg-card">
          {logoUrl ? (
            <img src={logoUrl} alt={t("settings.organization.logoAlt")} className="max-h-full max-w-full object-contain" />
          ) : (
            <ImageOff className="size-7 text-muted-foreground" aria-hidden />
          )}
        </div>

        <div className="space-y-1.5">
          <Button
            type="button"
            variant="secondary"
            disabled={disabled || uploading}
            onClick={() => inputRef.current?.click()}
          >
            {uploading ? (
              <Loader2 className="size-4 ms-2 animate-spin" aria-hidden />
            ) : (
              <Upload className="size-4 ms-2" aria-hidden />
            )}
            {logoUrl ? t("settings.organization.logoReplace") : t("settings.organization.logoUpload")}
          </Button>
          <p className="text-sm text-muted-foreground">{t("settings.organization.logoHint")}</p>
        </div>
      </div>

      <input
        ref={inputRef}
        type="file"
        accept={ACCEPTED}
        className="sr-only"
        aria-label={t("settings.organization.logoUpload")}
        data-testid="organization-logo-input"
        onChange={handleChange}
      />
    </div>
  )
}
