// src/pages/SettingsOrganizationPage.tsx
import { useState, useEffect, useRef } from "react"
import { useNavigate } from "react-router"
import { useTranslation } from "react-i18next"
import { ArrowLeft, ArrowRight, Upload } from "lucide-react"
import { useSettings } from "@/contexts/settings-context"
import { useDirection } from "@/hooks/use-direction"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select"
import {
  Card, CardContent, CardHeader, CardTitle,
} from "@/components/ui/card"
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import { cn } from "@/lib/utils"
import type { Industry } from "@/types/settings"

const INDUSTRIES: Industry[] = [
  "Banking", "Telecommunications", "Government",
  "Automotive", "Entertainment", "Services",
]

const INDUSTRY_LABEL_KEY: Record<Industry, string> = {
  Banking:          "settings.orgIndustryBanking",
  Telecommunications: "settings.orgIndustryTelecom",
  Government:       "settings.orgIndustryGovt",
  Automotive:       "settings.orgIndustryAuto",
  Entertainment:    "settings.orgIndustryEntertain",
  Services:         "settings.orgIndustryServices",
}

export default function SettingsOrganizationPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { orgConfig, saveOrg } = useSettings()
  const { isRtl } = useDirection()
  const fileRef = useRef<HTMLInputElement>(null)

  const BackArrow = isRtl ? ArrowLeft : ArrowRight

  const [name, setName] = useState(orgConfig.name)
  const [logoUrl, setLogoUrl] = useState<string | null>(orgConfig.logoUrl)
  const [logoWarning, setLogoWarning] = useState(false)
  const [industry, setIndustry] = useState<Industry>(orgConfig.industry)
  const [isDirty, setIsDirty] = useState(false)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [showLeaveModal, setShowLeaveModal] = useState(false)

  useEffect(() => {
    setName(orgConfig.name)
    setLogoUrl(orgConfig.logoUrl)
    setIndustry(orgConfig.industry)
    setIsDirty(false)
  }, [orgConfig])

  function markDirty() { setIsDirty(true) }

  function handleLogoChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setLogoWarning(file.size > 2 * 1024 * 1024)
    setLogoUrl(URL.createObjectURL(file))
    markDirty()
  }

  function validate() {
    const e: Record<string, string> = {}
    if (!name.trim()) e.name = t("settings.errNameRequired")
    setErrors(e)
    return Object.keys(e).length === 0
  }

  function handleSave() {
    if (!validate()) return
    saveOrg({ name: name.trim(), logoUrl, industry })
    setIsDirty(false)
    navigate("/settings")
  }

  function handleBack() {
    if (isDirty) { setShowLeaveModal(true) } else { navigate("/settings") }
  }

  return (
    <div className="space-y-5 py-5 px-8">
      {/* Back + Title */}
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="icon" aria-label={t("common.back")} onClick={handleBack}>
          <BackArrow className="size-5" />
        </Button>
        <div>
          <h1 className="text-2xl font-heading font-bold">{t("settings.orgTitle")}</h1>
          <p className="text-sm text-muted-foreground mt-0.5">{t("settings.orgDesc")}</p>
        </div>
      </div>

      {/* Two-column grid */}
      <div className="grid grid-cols-[55fr_45fr] gap-6 items-start">

        {/* Form card */}
        <Card>
          <CardContent className="space-y-5">

            {/* Name */}
            <div className="space-y-1.5">
              <Label htmlFor="orgName">
                {t("settings.orgName")}<span className="text-destructive ms-0.5">*</span>
              </Label>
              <Input
                id="orgName"
                value={name}
                maxLength={150}
                placeholder={t("settings.orgNamePlaceholder")}
                onChange={(e) => { setName(e.target.value); markDirty() }}
                className={cn(errors.name && "border-destructive")}
              />
              {errors.name && (
                <p className="text-xs text-destructive" role="alert">{errors.name}</p>
              )}
            </div>

            {/* Logo */}
            <div className="space-y-1.5">
              <Label>{t("settings.orgLogo")}</Label>
              <div className="flex items-center gap-3">
                {logoUrl ? (
                  <img
                    src={logoUrl}
                    alt="Tenant logo"
                    className="h-12 w-12 rounded-md object-contain border border-border bg-muted/30"
                  />
                ) : (
                  <div className="flex h-12 w-12 items-center justify-center rounded-md border border-dashed border-border bg-muted/30 text-muted-foreground">
                    <Upload className="size-4" />
                  </div>
                )}
                <div className="space-y-1">
                  <Button
                    type="button"
                    variant="secondary"
                    size="sm"
                    onClick={() => fileRef.current?.click()}
                  >
                    {logoUrl ? t("settings.orgLogoReplace") : t("settings.orgLogoUpload")}
                  </Button>
                  <p className="text-xs text-muted-foreground">{t("settings.orgLogoHint")}</p>
                </div>
              </div>
              <input
                ref={fileRef}
                type="file"
                accept=".png,.jpg,.jpeg,.svg"
                className="hidden"
                onChange={handleLogoChange}
              />
              {logoWarning && (
                <p className="text-xs text-d3 dark:text-d3-light">{t("settings.logoSizeWarning")}</p>
              )}
            </div>

            {/* Industry */}
            <div className="space-y-1.5">
              <Label>{t("settings.orgIndustry")}</Label>
              <Select
                value={industry}
                onValueChange={(v) => { setIndustry(v as Industry); markDirty() }}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {INDUSTRIES.map((ind) => (
                    <SelectItem key={ind} value={ind}>
                      {t(INDUSTRY_LABEL_KEY[ind])}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {/* Actions */}
            <div className="flex items-center justify-end gap-3 pt-2 border-t border-border">
              <Button variant="ghost" onClick={handleBack}>{t("common.cancel")}</Button>
              <Button
                disabled={!isDirty}
                onClick={handleSave}
                className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
              >
                {t("common.save")}
              </Button>
            </div>
          </CardContent>
        </Card>

        {/* Live preview */}
        <div className="sticky top-20">
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-sm">{t("common.preview")}</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col items-center gap-4 py-6">
              {logoUrl ? (
                <img
                  src={logoUrl}
                  alt="Tenant logo preview"
                  className="h-20 w-20 rounded-lg object-contain border border-border bg-muted/20"
                />
              ) : (
                <div className="flex h-20 w-20 items-center justify-center rounded-lg border-2 border-dashed border-border text-muted-foreground bg-muted/20">
                  <Upload className="size-6" />
                </div>
              )}
              <div className="text-center">
                <p className="font-heading font-bold text-lg">
                  {name.trim() || t("settings.orgNamePlaceholder")}
                </p>
                <p className="text-xs text-muted-foreground mt-0.5">
                  {t(INDUSTRY_LABEL_KEY[industry])}
                </p>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>

      {/* Unsaved-changes guard */}
      <AlertDialog open={showLeaveModal} onOpenChange={setShowLeaveModal}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("settings.unsavedTitle")}</AlertDialogTitle>
            <AlertDialogDescription>{t("settings.unsavedBody")}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("common.no")}</AlertDialogCancel>
            <AlertDialogAction onClick={() => navigate("/settings")}>
              {t("common.yes")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}
