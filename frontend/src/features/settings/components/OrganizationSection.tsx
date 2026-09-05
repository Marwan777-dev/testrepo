// Organization settings section — the Name / Industry / Logo form, extracted from the former
// standalone /settings/organization page so it can be composed into the unified Settings page.
// Self-contained: owns its form state and the useOrganizationSettings data layer. Save is the
// section's filled primary; Cancel discards local edits back to the loaded values. Write controls
// are hidden/disabled for personas without the TenantConfiguration `Manage` mode (FR-052) — the data
// layer enforces it server-side regardless. RTL-first, design-system tokens + logical properties.

import { useEffect, useRef, useState } from "react"
import { useTranslation } from "react-i18next"
import { Loader2 } from "lucide-react"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import { Card, CardContent, CardFooter } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Skeleton } from "@/components/ui/skeleton"
import { useSession } from "@/features/auth/hooks/useSession"
import { IndustryDropdown } from "@/features/settings/components/IndustryDropdown"
import { LogoUploadField } from "@/features/settings/components/LogoUploadField"
import { useOrganizationSettings } from "@/features/settings/hooks/useOrganizationSettings"

export interface OrganizationSectionProps {
  /** Reports whether the form has unsaved Name/Industry edits, so the page can guard navigation. */
  onDirtyChange?: (dirty: boolean) => void
}

export function OrganizationSection({ onDirtyChange }: OrganizationSectionProps) {
  const { t } = useTranslation()
  const { session } = useSession()
  const { data, loading, error, saving, uploading, saveSettings, uploadLogo } =
    useOrganizationSettings()

  const [name, setName] = useState("")
  const [industry, setIndustry] = useState("")
  const [nameError, setNameError] = useState<string | null>(null)
  const initialized = useRef(false)

  // Seed the form once from the loaded settings; later reloads (after a logo upload) must not clobber
  // unsaved Name/Industry edits.
  useEffect(() => {
    if (data && !initialized.current) {
      setName(data.name)
      setIndustry(data.industry)
      initialized.current = true
    }
  }, [data])

  // FR-052: only a persona with the TenantConfiguration `Manage` mode may edit (P-01 / P-07).
  const canEdit = (session?.permissionSnapshot.modules["TenantConfiguration"] ?? []).includes("Manage")

  const dirty = data != null && (name !== data.name || industry !== data.industry)

  useEffect(() => {
    onDirtyChange?.(dirty)
  }, [dirty, onDirtyChange])

  const handleSave = async () => {
    const trimmed = name.trim()
    if (!trimmed) {
      setNameError(t("settings.organization.nameRequired"))
      return
    }
    setNameError(null)

    const outcome = await saveSettings(trimmed, industry)
    if (outcome.ok) {
      toast.success(t("settings.organization.saved"))
      return
    }
    switch (outcome.code) {
      case "ORGANIZATION_NAME_REQUIRED":
        setNameError(t("settings.organization.nameRequired"))
        break
      case "ORGANIZATION_NAME_TOO_LONG":
        setNameError(t("settings.organization.nameTooLong"))
        break
      case "ORGANIZATION_INDUSTRY_UNKNOWN":
        toast.error(t("settings.organization.industryUnknown"))
        break
      default:
        toast.error(t("settings.organization.saveError"))
    }
  }

  const handleCancel = () => {
    if (!data) return
    setName(data.name)
    setIndustry(data.industry)
    setNameError(null)
  }

  const handleLogoSelect = async (file: File) => {
    const outcome = await uploadLogo(file)
    if (outcome.ok) {
      if (outcome.result.wasSanitised) {
        toast.info(t("settings.organization.logoSanitised"))
      } else {
        toast.success(t("settings.organization.logoUploaded"))
      }
      return
    }
    switch (outcome.code) {
      case "LOGO_CONTENT_TYPE_UNSUPPORTED":
        toast.error(t("settings.organization.logoTypeError"))
        break
      case "LOGO_SVG_UNSAFE_CONTENT":
        toast.error(t("settings.organization.logoUnsafe"))
        break
      case "LOGO_TOO_LARGE":
        toast.error(t("settings.organization.logoTooLarge"))
        break
      default:
        toast.error(t("settings.organization.logoUploadError"))
    }
  }

  return (
    <section aria-labelledby="settings-organization-heading" className="space-y-4">
      <div>
        <h2 id="settings-organization-heading" className="text-lg font-bold">
          {t("settings.organization.title")}
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">{t("settings.organization.subtitle")}</p>
      </div>

      {error ? (
        <div
          role="alert"
          className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {t("settings.organization.loadError")}
        </div>
      ) : loading ? (
        <Card>
          <CardContent className="space-y-4">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-20 w-full" />
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardContent className="space-y-4">
            {!canEdit && (
              <div
                role="status"
                data-testid="organization-readonly-notice"
                className="rounded-md border border-border bg-muted/40 px-3 py-2 text-sm text-foreground"
              >
                {t("settings.organization.readOnlyNotice")}
              </div>
            )}

            {/* Name */}
            <div className="space-y-1.5">
              <Label htmlFor="organization-name">
                {t("settings.organization.nameLabel")} <span className="text-destructive">*</span>
              </Label>
              <Input
                id="organization-name"
                data-testid="organization-name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                maxLength={150}
                disabled={!canEdit}
                aria-invalid={nameError ? true : undefined}
              />
              {nameError && (
                <p className="text-sm text-destructive" role="alert">
                  {nameError}
                </p>
              )}
            </div>

            {/* Logo */}
            <div className="space-y-1.5">
              <Label>{t("settings.organization.logoLabel")}</Label>
              <LogoUploadField
                logoUrl={data?.logo?.url ?? null}
                uploading={uploading}
                disabled={!canEdit}
                onSelect={handleLogoSelect}
              />
            </div>

            {/* Industry */}
            <div className="space-y-1.5">
              <Label htmlFor="organization-industry">{t("settings.organization.industryLabel")}</Label>
              <IndustryDropdown
                id="organization-industry"
                value={industry}
                options={data?.industryOptions ?? []}
                onChange={setIndustry}
                disabled={!canEdit}
              />
            </div>
          </CardContent>

          {canEdit && (
            <CardFooter className="justify-end gap-2">
              <Button variant="ghost" onClick={handleCancel} disabled={saving || !dirty}>
                {t("common.cancel")}
              </Button>
              <Button
                data-testid="organization-save"
                className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
                disabled={saving}
                onClick={() => void handleSave()}
              >
                {saving && <Loader2 className="size-4 ms-2 animate-spin" aria-hidden />}
                {t("settings.organization.save")}
              </Button>
            </CardFooter>
          )}
        </Card>
      )}
    </section>
  )
}
