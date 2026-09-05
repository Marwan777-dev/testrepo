// src/pages/SettingsPage.tsx
import { useState, useRef, useEffect } from "react"
import { useTranslation } from "react-i18next"
import { useSettings } from "@/contexts/settings-context"
import {
  Building2, SlidersHorizontal, Search, Upload, Info,
} from "lucide-react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Slider } from "@/components/ui/slider"
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select"
import { Card, CardContent } from "@/components/ui/card"
import { Separator } from "@/components/ui/separator"
import {
  Tooltip, TooltipContent, TooltipProvider, TooltipTrigger,
} from "@/components/ui/tooltip"
import { cn } from "@/lib/utils"
import type { Industry, ScoringConfig } from "@/types/settings"

// ── Nav definition ──────────────────────────────────────────

const NAV_SECTIONS = [
  { id: "organization",    label: "settings.orgTitle",  Icon: Building2 },
  { id: "customer-journey", label: "settings.cjTitle",  Icon: SlidersHorizontal },
] as const

// ── Org constants ───────────────────────────────────────────

const INDUSTRIES: Industry[] = [
  "Banking", "Telecommunications", "Government",
  "Automotive", "Entertainment", "Services",
]

const INDUSTRY_LABEL_KEY: Record<Industry, string> = {
  Banking:            "settings.orgIndustryBanking",
  Telecommunications: "settings.orgIndustryTelecom",
  Government:         "settings.orgIndustryGovt",
  Automotive:         "settings.orgIndustryAuto",
  Entertainment:      "settings.orgIndustryEntertain",
  Services:           "settings.orgIndustryServices",
}

// ── Page ────────────────────────────────────────────────────

export default function SettingsPage() {
  const { t } = useTranslation()
  const { orgConfig, saveOrg, scoringConfig, saveScoring } = useSettings()

  // Nav
  const [activeSection, setActiveSection] = useState<string>("organization")
  const [search, setSearch] = useState("")

  // Scroll refs
  const rightPanelRef = useRef<HTMLDivElement>(null)
  const orgRef        = useRef<HTMLDivElement>(null)
  const cjRef         = useRef<HTMLDivElement>(null)

  const sectionRefMap: Record<string, React.RefObject<HTMLDivElement | null>> = {
    "organization":     orgRef,
    "customer-journey": cjRef,
  }

  // ── Org state ──
  const fileRef = useRef<HTMLInputElement>(null)
  const [orgName,        setOrgName]        = useState(orgConfig.name)
  const [orgLogoUrl,     setOrgLogoUrl]     = useState<string | null>(orgConfig.logoUrl)
  const [orgLogoWarning, setOrgLogoWarning] = useState(false)
  const [orgIndustry,    setOrgIndustry]    = useState<Industry>(orgConfig.industry)
  const [orgIsDirty,     setOrgIsDirty]     = useState(false)
  const [orgErrors,      setOrgErrors]      = useState<Record<string, string>>({})

  useEffect(() => {
    setOrgName(orgConfig.name)
    setOrgLogoUrl(orgConfig.logoUrl)
    setOrgIndustry(orgConfig.industry)
    setOrgIsDirty(false)
  }, [orgConfig])

  // ── CJ state ──
  const [alpha,             setAlpha]             = useState(scoringConfig.alpha)
  const [motMultiplier,     setMotMultiplier]     = useState(scoringConfig.motMultiplier)
  const [nFloor,            setNFloor]            = useState(scoringConfig.nFloor)
  const [flagPercentile,    setFlagPercentile]    = useState(scoringConfig.flagPercentile)
  const [rollingWindowDays, setRollingWindowDays] = useState(scoringConfig.rollingWindowDays)
  const [cjIsDirty,         setCjIsDirty]         = useState(false)
  const [cjErrors,          setCjErrors]          = useState<Record<string, string>>({})

  const beta = parseFloat((1 - alpha).toFixed(3))

  useEffect(() => {
    setAlpha(scoringConfig.alpha)
    setMotMultiplier(scoringConfig.motMultiplier)
    setNFloor(scoringConfig.nFloor)
    setFlagPercentile(scoringConfig.flagPercentile)
    setRollingWindowDays(scoringConfig.rollingWindowDays)
    setCjIsDirty(false)
  }, [scoringConfig])

  // ── Scroll & active tracking ────────────────────────────

  function scrollToSection(id: string) {
    setActiveSection(id)
    const ref       = sectionRefMap[id]
    const container = rightPanelRef.current
    if (ref?.current && container) {
      const containerRect = container.getBoundingClientRect()
      const sectionRect   = ref.current.getBoundingClientRect()
      const offset        = sectionRect.top - containerRect.top + container.scrollTop - 56
      container.scrollTo({ top: offset, behavior: "smooth" })
    }
  }

  useEffect(() => {
    const container = rightPanelRef.current
    if (!container) return

    function onScroll() {
      const scrollTop = container!.scrollTop + 80
      const sections: { id: string; ref: React.RefObject<HTMLDivElement | null> }[] = [
        { id: "organization",     ref: orgRef },
        { id: "customer-journey", ref: cjRef },
      ]
      let current = sections[0].id
      for (const s of sections) {
        if (s.ref.current && s.ref.current.offsetTop <= scrollTop) current = s.id
      }
      setActiveSection(current)
    }

    container.addEventListener("scroll", onScroll, { passive: true })
    return () => container.removeEventListener("scroll", onScroll)
  }, [])

  // ── Org handlers ────────────────────────────────────────

  function handleLogoChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setOrgLogoWarning(file.size > 2 * 1024 * 1024)
    setOrgLogoUrl(URL.createObjectURL(file))
    setOrgIsDirty(true)
  }

  function handleOrgSave() {
    const e: Record<string, string> = {}
    if (!orgName.trim()) e.name = t("settings.errNameRequired")
    setOrgErrors(e)
    if (Object.keys(e).length) return
    saveOrg({ name: orgName.trim(), logoUrl: orgLogoUrl, industry: orgIndustry })
    setOrgIsDirty(false)
  }

  function handleOrgCancel() {
    setOrgName(orgConfig.name)
    setOrgLogoUrl(orgConfig.logoUrl)
    setOrgIndustry(orgConfig.industry)
    setOrgIsDirty(false)
    setOrgErrors({})
  }

  // ── CJ handlers ─────────────────────────────────────────

  function sliderVal(v: number | readonly number[]): number {
    return Array.isArray(v) ? (v as readonly number[])[0] : (v as number)
  }

  function handleCjSave() {
    const e: Record<string, string> = {}
    if (alpha < 0 || alpha > 1) e.alpha = t("settings.errAlpha")
    if (motMultiplier < 1.0 || motMultiplier > 2.0) e.motMultiplier = t("settings.errMot")
    if (!Number.isInteger(nFloor) || nFloor < 1) e.nFloor = t("settings.errNFloor")
    if (!Number.isInteger(flagPercentile) || flagPercentile < 1 || flagPercentile > 49) {
      e.flagPercentile = t("settings.errFlagPct")
    }
    if (!Number.isInteger(rollingWindowDays) || rollingWindowDays < 7) {
      e.rollingWindowDays = t("settings.errRollingDays")
    }
    setCjErrors(e)
    if (Object.keys(e).length) return
    const updated: ScoringConfig = { alpha, motMultiplier, nFloor, flagPercentile, rollingWindowDays }
    saveScoring(updated)
    setCjIsDirty(false)
  }

  function handleCjCancel() {
    setAlpha(scoringConfig.alpha)
    setMotMultiplier(scoringConfig.motMultiplier)
    setNFloor(scoringConfig.nFloor)
    setFlagPercentile(scoringConfig.flagPercentile)
    setRollingWindowDays(scoringConfig.rollingWindowDays)
    setCjIsDirty(false)
    setCjErrors({})
  }

  // ── Render ───────────────────────────────────────────────

  return (
    <TooltipProvider>
      <div className="py-5 px-8 space-y-5 h-full flex flex-col">

        {/* Page header */}
        <div>
          <h1 className="text-2xl font-heading font-bold">{t("settings.title")}</h1>
          <p className="text-sm text-muted-foreground mt-1">{t("settings.subtitle")}</p>
        </div>

        {/* Two-panel layout — fills remaining viewport height */}
        <div className="flex flex-1 min-h-0 border border-border rounded-lg overflow-hidden">

          {/* ── Left nav ─────────────────────────────────── */}
          <div className="w-52 shrink-0 border-e border-border bg-muted/20 flex flex-col">
            <nav className="p-3 space-y-0.5">
              {NAV_SECTIONS.map(({ id, label, Icon }) => (
                <button
                  key={id}
                  type="button"
                  onClick={() => scrollToSection(id)}
                  className={cn(
                    "w-full flex items-center gap-2.5 px-3 py-2 rounded-md text-sm transition-colors text-start",
                    activeSection === id
                      ? "bg-primary/10 text-primary font-semibold"
                      : "text-foreground hover:bg-muted",
                  )}
                >
                  <Icon className="size-4 shrink-0" />
                  {t(label)}
                </button>
              ))}
            </nav>
          </div>

          {/* ── Right scrollable content ──────────────────── */}
          <div ref={rightPanelRef} className="flex-1 min-w-0 overflow-y-auto">

            {/* Sticky search bar */}
            <div className="sticky top-0 z-10 bg-background/95 backdrop-blur-sm border-b border-border px-6 py-3 flex items-center gap-2">
              <Search className="size-4 text-muted-foreground shrink-0" />
              <Input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder={t("settings.search", { defaultValue: "Search settings…" })}
                className="h-8 text-sm max-w-xs"
              />
            </div>

            <div className="p-6 space-y-10">

              {/* ════════════════════════════════════════════
                  Organization
              ════════════════════════════════════════════ */}
              <section ref={orgRef} id="organization">
                <div className="mb-5">
                  <h2 className="text-base font-bold">{t("settings.orgTitle")}</h2>
                  <p className="text-sm text-muted-foreground">{t("settings.orgDesc")}</p>
                </div>

                <Card className="max-w-2xl">
                  <CardContent className="space-y-5 pt-5">

                    {/* Name */}
                    <div className="space-y-1.5">
                      <Label htmlFor="orgName">
                        {t("settings.orgName")}<span className="text-destructive ms-0.5">*</span>
                      </Label>
                      <Input
                        id="orgName"
                        value={orgName}
                        maxLength={150}
                        placeholder={t("settings.orgNamePlaceholder")}
                        onChange={(e) => { setOrgName(e.target.value); setOrgIsDirty(true) }}
                        className={cn(orgErrors.name && "border-destructive")}
                      />
                      {orgErrors.name && (
                        <p className="text-xs text-destructive" role="alert">{orgErrors.name}</p>
                      )}
                    </div>

                    {/* Logo */}
                    <div className="space-y-1.5">
                      <Label>{t("settings.orgLogo")}</Label>
                      <div className="flex items-center gap-3">
                        {orgLogoUrl ? (
                          <img
                            src={orgLogoUrl}
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
                            {orgLogoUrl ? t("settings.orgLogoReplace") : t("settings.orgLogoUpload")}
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
                      {orgLogoWarning && (
                        <p className="text-xs text-d3 dark:text-d3-light">{t("settings.logoSizeWarning")}</p>
                      )}
                    </div>

                    {/* Industry */}
                    <div className="space-y-1.5">
                      <Label>{t("settings.orgIndustry")}</Label>
                      <Select
                        value={orgIndustry}
                        onValueChange={(v) => { setOrgIndustry(v as Industry); setOrgIsDirty(true) }}
                      >
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {INDUSTRIES.map((ind) => (
                            <SelectItem key={ind} value={ind}>{t(INDUSTRY_LABEL_KEY[ind])}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>

                    {/* Actions */}
                    <div className="flex items-center justify-end gap-3 pt-2 border-t border-border">
                      <Button variant="ghost" disabled={!orgIsDirty} onClick={handleOrgCancel}>
                        {t("common.cancel")}
                      </Button>
                      <Button
                        disabled={!orgIsDirty}
                        onClick={handleOrgSave}
                        className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
                      >
                        {t("common.save")}
                      </Button>
                    </div>
                  </CardContent>
                </Card>
              </section>

              <Separator />

              {/* ════════════════════════════════════════════
                  Customer Journey
              ════════════════════════════════════════════ */}
              <section ref={cjRef} id="customer-journey">
                <div className="mb-5">
                  <h2 className="text-base font-bold">{t("settings.cjTitle")}</h2>
                  <p className="text-sm text-muted-foreground">{t("settings.cjDesc")}</p>
                </div>

                <Card className="max-w-2xl">
                  <CardContent className="space-y-8 pt-5">

                    {/* Alpha */}
                    <div className="space-y-3">
                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-1.5">
                          <Label className="text-sm font-semibold">{t("settings.cjAlpha")}</Label>
                          <Tooltip>
                            <TooltipTrigger
                              className="inline-flex items-center text-muted-foreground cursor-help"
                              aria-label={t("settings.cjAlpha")}
                            >
                              <Info className="size-3.5" />
                            </TooltipTrigger>
                            <TooltipContent side="right" className="max-w-sm p-4 text-xs leading-relaxed font-sans">
                              <p>{t("settings.tooltipAlpha")}</p>
                            </TooltipContent>
                          </Tooltip>
                        </div>
                        <span className="text-sm font-mono font-bold tabular-nums text-primary">
                          {alpha.toFixed(3)}
                        </span>
                      </div>
                      <Slider
                        value={[alpha]}
                        min={0} max={1} step={0.001}
                        onValueChange={(v) => { setAlpha(parseFloat(sliderVal(v).toFixed(3))); setCjIsDirty(true) }}
                      />
                      <div className="flex items-center gap-3 pt-1">
                        <div className="flex items-center gap-2 px-3 py-1.5 rounded-md bg-muted/40 border border-border">
                          <span className="text-xs text-muted-foreground">{t("settings.cjBeta")}</span>
                          <span className="text-sm font-mono font-bold tabular-nums">{beta.toFixed(3)}</span>
                        </div>
                        <span className="text-xs text-muted-foreground">{t("settings.cjBetaDerived")}</span>
                      </div>
                      {cjErrors.alpha && <p className="text-xs text-destructive" role="alert">{cjErrors.alpha}</p>}
                    </div>

                    {/* MOT Multiplier */}
                    <div className="space-y-3">
                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-1.5">
                          <Label className="text-sm font-semibold">{t("settings.cjMot")}</Label>
                          <Tooltip>
                            <TooltipTrigger
                              className="inline-flex items-center text-muted-foreground cursor-help"
                              aria-label={t("settings.cjMot")}
                            >
                              <Info className="size-3.5" />
                            </TooltipTrigger>
                            <TooltipContent side="right" className="max-w-sm p-4 text-xs leading-relaxed font-sans">
                              <p>{t("settings.tooltipMot")}</p>
                            </TooltipContent>
                          </Tooltip>
                        </div>
                        <Input
                          type="number" min={1.0} max={2.0} step={0.1}
                          value={motMultiplier}
                          onChange={(e) => {
                            const v = parseFloat(e.target.value)
                            if (!isNaN(v)) { setMotMultiplier(parseFloat(v.toFixed(1))); setCjIsDirty(true) }
                          }}
                          className={cn("w-20 text-center tabular-nums h-8", cjErrors.motMultiplier && "border-destructive")}
                        />
                      </div>
                      <Slider
                        value={[motMultiplier]}
                        min={1} max={2} step={0.1}
                        onValueChange={(v) => { setMotMultiplier(parseFloat(sliderVal(v).toFixed(1))); setCjIsDirty(true) }}
                      />
                      {cjErrors.motMultiplier && <p className="text-xs text-destructive" role="alert">{cjErrors.motMultiplier}</p>}
                    </div>

                    {/* N Floor */}
                    <div className="space-y-1.5">
                      <div className="flex items-center gap-1.5">
                        <Label htmlFor="nFloor" className="text-sm font-semibold">{t("settings.cjNFloor")}</Label>
                        <Tooltip>
                          <TooltipTrigger
                            className="inline-flex items-center text-muted-foreground cursor-help"
                            aria-label={t("settings.cjNFloor")}
                          >
                            <Info className="size-3.5" />
                          </TooltipTrigger>
                          <TooltipContent side="right" className="max-w-sm p-4 text-xs leading-relaxed font-sans">
                            <p>{t("settings.tooltipNFloor")}</p>
                          </TooltipContent>
                        </Tooltip>
                      </div>
                      <Input
                        id="nFloor" type="number" min={1} value={nFloor}
                        onChange={(e) => { const v = parseInt(e.target.value, 10); if (!isNaN(v)) { setNFloor(v); setCjIsDirty(true) } }}
                        className={cn("tabular-nums", cjErrors.nFloor && "border-destructive")}
                      />
                      {cjErrors.nFloor && <p className="text-xs text-destructive" role="alert">{cjErrors.nFloor}</p>}
                    </div>

                    {/* Flag Percentile */}
                    <div className="space-y-1.5">
                      <div className="flex items-center gap-1.5">
                        <Label htmlFor="flagPct" className="text-sm font-semibold">{t("settings.cjFlagPct")}</Label>
                        <Tooltip>
                          <TooltipTrigger
                            className="inline-flex items-center text-muted-foreground cursor-help"
                            aria-label={t("settings.cjFlagPct")}
                          >
                            <Info className="size-3.5" />
                          </TooltipTrigger>
                          <TooltipContent side="right" className="max-w-sm p-4 text-xs leading-relaxed font-sans">
                            <p>{t("settings.tooltipFlagPct")}</p>
                          </TooltipContent>
                        </Tooltip>
                      </div>
                      <Input
                        id="flagPct" type="number" min={1} max={49} value={flagPercentile}
                        onChange={(e) => { const v = parseInt(e.target.value, 10); if (!isNaN(v)) { setFlagPercentile(v); setCjIsDirty(true) } }}
                        className={cn("tabular-nums", cjErrors.flagPercentile && "border-destructive")}
                      />
                      {cjErrors.flagPercentile && <p className="text-xs text-destructive" role="alert">{cjErrors.flagPercentile}</p>}
                    </div>

                    {/* Rolling Window Days */}
                    <div className="space-y-1.5">
                      <div className="flex items-center gap-1.5">
                        <Label htmlFor="rollingDays" className="text-sm font-semibold">{t("settings.cjRollingDays")}</Label>
                        <Tooltip>
                          <TooltipTrigger
                            className="inline-flex items-center text-muted-foreground cursor-help"
                            aria-label={t("settings.cjRollingDays")}
                          >
                            <Info className="size-3.5" />
                          </TooltipTrigger>
                          <TooltipContent side="right" className="max-w-sm p-4 text-xs leading-relaxed font-sans">
                            <p>{t("settings.tooltipRollingDays")}</p>
                          </TooltipContent>
                        </Tooltip>
                      </div>
                      <Input
                        id="rollingDays" type="number" min={7} value={rollingWindowDays}
                        onChange={(e) => { const v = parseInt(e.target.value, 10); if (!isNaN(v)) { setRollingWindowDays(v); setCjIsDirty(true) } }}
                        className={cn("tabular-nums", cjErrors.rollingWindowDays && "border-destructive")}
                      />
                      {cjErrors.rollingWindowDays && <p className="text-xs text-destructive" role="alert">{cjErrors.rollingWindowDays}</p>}
                    </div>

                    {/* CJ Actions */}
                    <div className="flex items-center justify-end gap-3 pt-2 border-t border-border">
                      <Button variant="ghost" disabled={!cjIsDirty} onClick={handleCjCancel}>
                        {t("common.cancel")}
                      </Button>
                      <Button
                        disabled={!cjIsDirty}
                        onClick={handleCjSave}
                        className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
                      >
                        {t("common.save")}
                      </Button>
                    </div>
                  </CardContent>
                </Card>
              </section>

              {/* Bottom breathing room */}
              <div className="h-10" />
            </div>
          </div>
        </div>
      </div>
    </TooltipProvider>
  )
}
