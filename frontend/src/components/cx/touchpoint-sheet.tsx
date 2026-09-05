import { useEffect, useState } from "react"
import { useTranslation } from "react-i18next"
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Label } from "@/components/ui/label"
import { Switch } from "@/components/ui/switch"
import { Badge } from "@/components/ui/badge"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { useDirection } from "@/hooks/use-direction"
import { Plus, Trash2, AlertTriangle, Sparkles } from "lucide-react"
import { cn } from "@/lib/utils"
import {
  DEFAULT_CHANNELS,
  KPI_LABELS,
  type ChannelKey,
  type KpiBinding,
  type KpiType,
  type Touchpoint,
} from "@/lib/journey-data"

interface TouchpointSheetProps {
  open: boolean
  onClose: () => void
  touchpoint: Touchpoint | null // null = creating new
  onSave: (tp: Touchpoint) => void
}

const EMPTY_TP: Touchpoint = {
  id: "",
  name: { en: "", ar: "" },
  description: { en: "", ar: "" },
  channels: [],
  importanceCustomer: 3,
  importanceBusiness: 3,
  isMot: false,
  isMandatory: false,
  tpWeight: 1,
  kpis: [],
}

const IMP_LABELS = ["impLow", "impLow", "impMedium", "impMedium", "impHigh"]

function ImportanceSelector({
  value,
  onChange,
  ariaLabel,
}: {
  value: number
  onChange: (v: 1 | 2 | 3 | 4 | 5) => void
  ariaLabel: string
}) {
  const { t } = useTranslation()
  return (
    <div className="flex items-center gap-1.5" role="radiogroup" aria-label={ariaLabel}>
      {[1, 2, 3, 4, 5].map((n) => (
        <button
          key={n}
          type="button"
          role="radio"
          aria-checked={value === n}
          onClick={() => onChange(n as never)}
          className={cn(
            "size-9 rounded-full text-xs font-bold tabular-nums",
            "motion-safe:transition-all motion-safe:duration-150",
            "focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none",
            value >= n
              ? "bg-primary text-primary-foreground shadow-sm"
              : "bg-muted text-muted-foreground hover:bg-muted/80",
          )}
        >
          {n}
        </button>
      ))}
      <span className="ms-2 text-xs text-muted-foreground">
        {t(`journey.${IMP_LABELS[value - 1]}`)}
      </span>
    </div>
  )
}

function ChannelChip({
  active,
  label,
  onClick,
}: {
  active: boolean
  label: string
  onClick: () => void
}) {
  return (
    <button
      type="button"
      aria-pressed={active}
      onClick={onClick}
      className={cn(
        "rounded-full px-3 py-1.5 text-xs font-medium border",
        "motion-safe:transition-all motion-safe:duration-150",
        "focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none",
        active
          ? "bg-nb-cyan text-white border-nb-cyan shadow-sm"
          : "bg-background border-border text-foreground hover:bg-muted",
      )}
    >
      {label}
    </button>
  )
}

export function TouchpointSheet({
  open,
  onClose,
  touchpoint,
  onSave,
}: TouchpointSheetProps) {
  const { t, i18n } = useTranslation()
  const { isRtl } = useDirection()
  const isArabic = i18n.language === "ar"

  const [form, setForm] = useState<Touchpoint>(EMPTY_TP)

  // Reset form when touchpoint changes
  useEffect(() => {
    setForm(touchpoint ?? { ...EMPTY_TP, id: `t-${Date.now()}` })
  }, [touchpoint, open])

  const kpiTotal = form.kpis.reduce((acc, k) => acc + k.weight, 0)
  const kpiValid = form.kpis.length === 0 || kpiTotal === 100

  const showMotSuggestion =
    !form.isMot && form.importanceCustomer >= 4 && form.kpis.length > 0

  const updateField = <K extends keyof Touchpoint>(k: K, v: Touchpoint[K]) => {
    setForm((f) => ({ ...f, [k]: v }))
  }

  const toggleChannel = (key: ChannelKey) => {
    setForm((f) => ({
      ...f,
      channels: f.channels.includes(key)
        ? f.channels.filter((c) => c !== key)
        : [...f.channels, key],
    }))
  }

  const addKpi = () => {
    if (form.kpis.length >= 5) return
    const available = (Object.keys(KPI_LABELS) as KpiType[]).find(
      (k) => !form.kpis.some((b) => b.type === k),
    )
    if (!available) return
    const remaining = Math.max(0, 100 - kpiTotal)
    setForm((f) => ({
      ...f,
      kpis: [...f.kpis, { type: available, weight: remaining }],
    }))
  }

  const updateKpi = (index: number, patch: Partial<KpiBinding>) => {
    setForm((f) => ({
      ...f,
      kpis: f.kpis.map((b, i) => (i === index ? { ...b, ...patch } : b)),
    }))
  }

  const removeKpi = (index: number) => {
    setForm((f) => ({ ...f, kpis: f.kpis.filter((_, i) => i !== index) }))
  }

  const handleSave = () => {
    if (!form.name.ar.trim() || !form.name.en.trim()) return
    if (!kpiValid) return
    onSave(form)
    onClose()
  }

  const isNew = touchpoint === null

  // Sheet slides from "right" in LTR (end), "left" in RTL (end) — so the
  // panel always appears on the reading-end side of the screen.
  const sheetSide = isRtl ? "left" : "right"

  return (
    <Sheet open={open} onOpenChange={(o) => !o && onClose()}>
      <SheetContent
        side={sheetSide}
        className="w-full sm:max-w-md md:max-w-lg"
      >
        <SheetHeader className="border-b border-border">
          <SheetTitle className="text-lg font-heading font-bold">
            {t("journey.configureTouchpoint")}
          </SheetTitle>
          <SheetDescription>
            {isNew
              ? t("journey.newTouchpoint")
              : isArabic
                ? form.name.ar
                : form.name.en}
          </SheetDescription>
        </SheetHeader>

        <div className="flex-1 min-h-0 overflow-y-auto px-4 py-2 space-y-6">
          {/* ── Name fields ─────────────────── */}
          <div className="space-y-3">
            <div>
              <Label htmlFor="tp-name-ar">
                {t("journey.nameAr")}{" "}
                <span className="text-destructive">*</span>
              </Label>
              <Input
                id="tp-name-ar"
                dir="rtl"
                value={form.name.ar}
                onChange={(e) =>
                  updateField("name", { ...form.name, ar: e.target.value })
                }
                className="mt-1.5"
              />
            </div>
            <div>
              <Label htmlFor="tp-name-en">
                {t("journey.nameEn")}{" "}
                <span className="text-destructive">*</span>
              </Label>
              <Input
                id="tp-name-en"
                dir="ltr"
                value={form.name.en}
                onChange={(e) =>
                  updateField("name", { ...form.name, en: e.target.value })
                }
                className="mt-1.5"
              />
            </div>
          </div>

          {/* ── Descriptions ─────────────────── */}
          <div className="space-y-3">
            <div>
              <Label htmlFor="tp-desc-ar">{t("journey.descAr")}</Label>
              <Textarea
                id="tp-desc-ar"
                dir="rtl"
                rows={2}
                value={form.description.ar}
                onChange={(e) =>
                  updateField("description", {
                    ...form.description,
                    ar: e.target.value,
                  })
                }
                className="mt-1.5 leading-relaxed"
              />
            </div>
            <div>
              <Label htmlFor="tp-desc-en">{t("journey.descEn")}</Label>
              <Textarea
                id="tp-desc-en"
                dir="ltr"
                rows={2}
                value={form.description.en}
                onChange={(e) =>
                  updateField("description", {
                    ...form.description,
                    en: e.target.value,
                  })
                }
                className="mt-1.5"
              />
            </div>
          </div>

          {/* ── Channels ─────────────────────── */}
          <div>
            <Label className="mb-1">{t("journey.channels")}</Label>
            <p className="text-xs text-muted-foreground mb-2.5">
              {t("journey.channelsHelp")}
            </p>
            <div className="flex flex-wrap gap-1.5">
              {DEFAULT_CHANNELS.map((c) => (
                <ChannelChip
                  key={c.key}
                  active={form.channels.includes(c.key)}
                  label={isArabic ? c.label.ar : c.label.en}
                  onClick={() => toggleChannel(c.key)}
                />
              ))}
            </div>
          </div>

          {/* ── Importance ───────────────────── */}
          <div className="space-y-4 pt-2 border-t border-border">
            <div className="pt-4">
              <Label className="mb-2 block">
                {t("journey.importanceCustomer")}
              </Label>
              <ImportanceSelector
                value={form.importanceCustomer}
                onChange={(v) => updateField("importanceCustomer", v)}
                ariaLabel={t("journey.importanceCustomer")}
              />
            </div>
            <div>
              <Label className="mb-2 block">
                {t("journey.importanceBusiness")}
              </Label>
              <ImportanceSelector
                value={form.importanceBusiness}
                onChange={(v) => updateField("importanceBusiness", v)}
                ariaLabel={t("journey.importanceBusiness")}
              />
            </div>
          </div>

          {/* ── MoT auto-suggest banner (FR-2.10) ─ */}
          {showMotSuggestion && (
            <div className="flex items-start gap-2.5 rounded-md border border-nb-cyan/30 bg-nb-cyan-100/50 p-3 dark:bg-nb-cyan-900/20">
              <Sparkles className="size-4 text-nb-cyan-700 dark:text-nb-cyan-300 mt-0.5 shrink-0" />
              <div className="flex-1">
                <p className="text-xs font-medium">{t("journey.motSuggest")}</p>
                <div className="flex gap-2 mt-2">
                  <Button
                    size="sm"
                    variant="default"
                    className="h-7 text-xs bg-primary text-primary-foreground"
                    onClick={() => updateField("isMot", true)}
                  >
                    {t("journey.motSuggestAccept")}
                  </Button>
                  <Button size="sm" variant="ghost" className="h-7 text-xs">
                    {t("journey.motSuggestDecline")}
                  </Button>
                </div>
              </div>
            </div>
          )}

          {/* ── Toggles ──────────────────────── */}
          <div className="space-y-3 pt-2 border-t border-border">
            <div className="flex items-start justify-between pt-4 gap-3">
              <div className="flex-1">
                <Label htmlFor="tp-mot" className="text-sm font-bold">
                  {t("journey.motToggle")}
                </Label>
                <p className="text-xs text-muted-foreground mt-0.5">
                  {t("journey.motToggleHelp")}
                </p>
              </div>
              <Switch
                id="tp-mot"
                checked={form.isMot}
                onCheckedChange={(v) => updateField("isMot", v)}
              />
            </div>
            <div className="flex items-start justify-between gap-3">
              <div className="flex-1">
                <Label htmlFor="tp-mandatory" className="text-sm font-bold">
                  {t("journey.mandatoryToggle")}
                </Label>
                <p className="text-xs text-muted-foreground mt-0.5">
                  {t("journey.mandatoryHelp")}
                </p>
              </div>
              <Switch
                id="tp-mandatory"
                checked={form.isMandatory}
                onCheckedChange={(v) => updateField("isMandatory", v)}
              />
            </div>
          </div>

          {/* ── KPI Configuration ────────────── */}
          <div className="space-y-3 pt-2 border-t border-border">
            <div className="flex items-center justify-between pt-4">
              <div>
                <h4 className="text-sm font-bold">
                  {t("journey.kpiConfiguration")}
                </h4>
                <p className="text-xs text-muted-foreground">
                  {t("journey.kpiWeightsCurrent", { total: kpiTotal })}
                </p>
              </div>
              <Button
                size="sm"
                variant="secondary"
                onClick={addKpi}
                disabled={form.kpis.length >= 5}
              >
                <Plus className="size-3.5 me-1" />
                {t("journey.addKpi")}
              </Button>
            </div>

            {form.kpis.length === 0 ? (
              <div className="rounded-md border border-dashed border-border bg-muted/30 p-4 text-center">
                <AlertTriangle className="size-5 text-d3 mx-auto mb-1.5" />
                <p className="text-xs font-medium">
                  {t("journey.noKpisConfigured")}
                </p>
                <p className="text-[10px] text-muted-foreground mt-0.5">
                  {t("journey.noKpisHelp")}
                </p>
              </div>
            ) : (
              <div className="space-y-2">
                {form.kpis.map((b, i) => {
                  const lbl = KPI_LABELS[b.type]
                  return (
                    <div
                      key={i}
                      className="flex items-center gap-2 rounded-md border border-border p-2.5"
                    >
                      <div className="flex-1 min-w-0">
                        <Select
                          value={b.type}
                          onValueChange={(v) =>
                            updateKpi(i, { type: v as KpiType })
                          }
                        >
                          <SelectTrigger className="h-8 text-xs">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            {(Object.keys(KPI_LABELS) as KpiType[]).map(
                              (kt) => (
                                <SelectItem
                                  key={kt}
                                  value={kt}
                                  disabled={
                                    kt !== b.type &&
                                    form.kpis.some((x) => x.type === kt)
                                  }
                                >
                                  {isArabic
                                    ? KPI_LABELS[kt].ar
                                    : KPI_LABELS[kt].en}
                                </SelectItem>
                              ),
                            )}
                          </SelectContent>
                        </Select>
                        <span className="sr-only">
                          {isArabic ? lbl.ar : lbl.en}
                        </span>
                      </div>
                      <div className="flex items-center gap-1.5 w-32 shrink-0">
                        <Input
                          type="number"
                          min={0}
                          max={100}
                          value={b.weight}
                          onChange={(e) =>
                            updateKpi(i, {
                              weight: Math.max(
                                0,
                                Math.min(100, Number(e.target.value) || 0),
                              ),
                            })
                          }
                          className="h-8 text-xs tabular-nums"
                          aria-label={t("journey.kpiWeight")}
                        />
                        <span className="text-xs text-muted-foreground">
                          %
                        </span>
                      </div>
                      <Button
                        variant="ghost"
                        size="icon-sm"
                        onClick={() => removeKpi(i)}
                        aria-label={t("journey.kpiRemove")}
                      >
                        <Trash2 className="size-3.5 text-muted-foreground" />
                      </Button>
                    </div>
                  )
                })}

                {/* Weight summary */}
                <div
                  className={cn(
                    "flex items-center justify-between rounded-md px-3 py-2 text-xs font-medium",
                    kpiValid
                      ? "bg-d2-light text-d2-dark dark:bg-d2-dark/20 dark:text-d2-light"
                      : "bg-d4-light text-d4-dark dark:bg-d4-dark/20 dark:text-d4-light",
                  )}
                  role={kpiValid ? "status" : "alert"}
                >
                  <span>{t("journey.kpiWeightsSum")}</span>
                  <span className="tabular-nums font-bold">{kpiTotal}%</span>
                </div>
              </div>
            )}
          </div>
        </div>

        <SheetFooter className="border-t border-border">
          <Button
            className="w-full bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            disabled={
              !form.name.ar.trim() || !form.name.en.trim() || !kpiValid
            }
            onClick={handleSave}
          >
            {t("journey.saveChanges")}
          </Button>
          <Button variant="outline" className="w-full" onClick={onClose}>
            {t("common.cancel")}
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  )
}

// Display badge used by the Builder list — kept in this file so it imports
// from the same set of types.
export function TouchpointBadges({ tp }: { tp: Touchpoint }) {
  const { t, i18n } = useTranslation()
  const isArabic = i18n.language === "ar"
  const primaryChannel = tp.channels[0]
  const channelLabel = primaryChannel
    ? DEFAULT_CHANNELS.find((c) => c.key === primaryChannel)?.label
    : null

  return (
    <div className="flex items-center gap-2 flex-wrap">
      {channelLabel && (
        <Badge variant="outline" className="text-[10px]">
          {isArabic ? channelLabel.ar : channelLabel.en}
          {tp.channels.length > 1 && (
            <span className="ms-1 tabular-nums opacity-60">
              +{tp.channels.length - 1}
            </span>
          )}
        </Badge>
      )}
      <div
        className="flex items-center gap-0.5"
        aria-label={t("journey.importanceCustomer")}
      >
        {[1, 2, 3, 4, 5].map((n) => (
          <span
            key={n}
            className={cn(
              "size-2 rounded-full",
              n <= tp.importanceCustomer ? "bg-nb-cyan" : "bg-muted",
            )}
          />
        ))}
      </div>
      {tp.isMot && (
        <Badge className="text-[10px] bg-nb-cyan text-white border-nb-cyan">
          {t("journey.mot")}
        </Badge>
      )}
      {tp.kpis.length > 0 ? (
        <Badge
          variant="outline"
          className="text-[10px] bg-nb-cyan-100 text-nb-cyan-800 border-nb-cyan/20 dark:bg-nb-cyan-900/20 dark:text-nb-cyan-300"
        >
          {tp.kpis.length}{" "}
          {tp.kpis.length === 1
            ? t("journey.kpiShort")
            : t("journey.kpisShort")}
        </Badge>
      ) : (
        <Badge
          variant="outline"
          className="text-[10px] bg-d3-light text-d3-dark border-d3/20 dark:bg-d3-dark/20 dark:text-d3-light"
        >
          <AlertTriangle className="size-2.5 me-1" />
          {t("journey.noKpis")}
        </Badge>
      )}
    </div>
  )
}
