import { useState, useCallback } from "react"
import { useNavigate, useParams } from "react-router"
import {
  ArrowRight, ArrowLeft, ClipboardList, Save, Lock, Loader2, X, Plus,
} from "lucide-react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Badge } from "@/components/ui/badge"
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from "@/components/ui/dialog"
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select"
import { Label } from "@/components/ui/label"
import { useTranslation } from "react-i18next"
import { useDirection } from "@/hooks/use-direction"
import { SurveyBuilderCanvas } from "@/components/surveys/SurveyBuilderCanvas"
import { MOCK_TEMPLATES } from "@/data/mock-surveys"
import { INDUSTRY_OPTIONS } from "@/types/survey"
import { toast } from "sonner"

const SECTOR_LABELS: Record<string, { ar: string; en: string }> = {
  Banking:            { ar: "البنوك", en: "Banking" },
  Telecommunications: { ar: "الاتصالات", en: "Telecom" },
  Government:         { ar: "الحكومة", en: "Government" },
  Automotive:         { ar: "السيارات", en: "Automotive" },
  Entertainment:      { ar: "الترفيه", en: "Entertainment" },
  Services:           { ar: "الخدمات", en: "Services" },
}

// ── Use as Survey dialog ──────────────────────────────────────────────────────
function UseAsSurveyDialog({
  open,
  isAr,
  templateName,
  onOpenChange,
  onConfirm,
}: {
  open: boolean
  isAr: boolean
  templateName: string
  onOpenChange: (v: boolean) => void
  onConfirm: () => void
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{isAr ? "استخدام كاستبيان" : "Use as Survey"}</DialogTitle>
          <DialogDescription className="text-sm text-muted-foreground leading-relaxed">
            {isAr
              ? `سيتم إنشاء استبيان جديد كنسخة من "${templateName}". ستحتاج إلى ربط الرحلة ونقاط التماس في محرر الاستبيان.`
              : `A new survey will be created as a copy of "${templateName}". You'll need to bind journeys and touchpoints in the survey editor.`}
          </DialogDescription>
        </DialogHeader>
        <DialogFooter className="gap-2">
          <Button variant="ghost" onClick={() => onOpenChange(false)}>
            {isAr ? "إلغاء" : "Cancel"}
          </Button>
          <Button
            className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            onClick={() => { onOpenChange(false); onConfirm() }}
          >
            {isAr ? "إنشاء الاستبيان" : "Create Survey"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

// ── Template metadata sidebar ─────────────────────────────────────────────────
function TemplateMetaPanel({
  isAr,
  name,
  sectors,
  isPlatform,
  onNameChange,
  onSectorsChange,
}: {
  isAr: boolean
  name: string
  sectors: string[]
  isPlatform: boolean
  onNameChange: (v: string) => void
  onSectorsChange: (v: string[]) => void
}) {
  const [sectorToAdd, setSectorToAdd] = useState("")

  function addSector(s: string) {
    if (s && !sectors.includes(s)) {
      onSectorsChange([...sectors, s])
    }
    setSectorToAdd("")
  }

  return (
    <div className="flex flex-col h-full bg-card border-s border-border">
      {/* Header */}
      <div className="px-4 py-3 border-b border-border">
        <p className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">
          {isAr ? "معلومات القالب" : "Template Info"}
        </p>
      </div>

      <div className="flex-1 overflow-y-auto p-4 space-y-6">
        {/* Template name */}
        <div className="space-y-2">
          <Label className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">
            {isAr ? "اسم القالب" : "Template Name"}
          </Label>
          {isPlatform ? (
            <div className="flex items-center gap-2 rounded-md bg-muted/60 px-3 py-2">
              <Lock className="size-3.5 text-muted-foreground shrink-0" />
              <span className="text-sm text-muted-foreground">{name}</span>
            </div>
          ) : (
            <Input
              value={name}
              onChange={(e) => onNameChange(e.target.value)}
              placeholder={isAr ? "اسم القالب..." : "Template name..."}
              dir={isAr ? "rtl" : "ltr"}
            />
          )}
        </div>

        {/* Template class */}
        <div className="space-y-2">
          <Label className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">
            {isAr ? "فئة القالب" : "Template Class"}
          </Label>
          <div className="flex items-center gap-2 rounded-md bg-muted/60 px-3 py-2">
            {isPlatform ? (
              <>
                <Lock className="size-3.5 text-muted-foreground shrink-0" />
                <span className="text-sm text-muted-foreground">
                  {isAr ? "قالب منصة (للقراءة فقط)" : "Platform Template (read-only)"}
                </span>
              </>
            ) : (
              <span className="text-sm text-foreground">
                {isAr ? "قالب مخصص (للمؤسسة)" : "Tenant Template"}
              </span>
            )}
          </div>
          {isPlatform && (
            <p className="text-xs text-muted-foreground">
              {isAr
                ? "لا يمكن تعديل قوالب المنصة. استخدمها لإنشاء استبيان قابل للتعديل."
                : "Platform templates cannot be edited. Use them to create an editable survey."}
            </p>
          )}
        </div>

        {/* Sectors */}
        <div className="space-y-2">
          <Label className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">
            {isAr ? "القطاعات" : "Sectors"}
          </Label>

          <div className="flex flex-wrap gap-1.5">
            {sectors.map((s) => (
              <div
                key={s}
                className="flex items-center gap-1 rounded-full border border-border bg-card px-2.5 py-1"
              >
                <span className="text-xs">{isAr ? SECTOR_LABELS[s]?.ar ?? s : SECTOR_LABELS[s]?.en ?? s}</span>
                {!isPlatform && (
                  <button
                    onClick={() => onSectorsChange(sectors.filter((x) => x !== s))}
                    className="text-muted-foreground hover:text-destructive"
                    aria-label={isAr ? "حذف" : "Remove"}
                  >
                    <X className="size-3" />
                  </button>
                )}
              </div>
            ))}
            {sectors.length === 0 && (
              <p className="text-xs text-muted-foreground">
                {isAr ? "لم يتم تحديد قطاعات" : "No sectors selected"}
              </p>
            )}
          </div>

          {!isPlatform && (
            <div className="flex items-center gap-2">
              <Select value={sectorToAdd} onValueChange={(v) => setSectorToAdd(v ?? "")}>
                <SelectTrigger className="flex-1">
                  <SelectValue placeholder={isAr ? "أضف قطاعاً..." : "Add sector..."} />
                </SelectTrigger>
                <SelectContent>
                  {INDUSTRY_OPTIONS.filter((o) => !sectors.includes(o.value)).map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {isAr ? o.labelAr : o.labelEn}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Button
                size="icon"
                variant="secondary"
                className="size-10 shrink-0"
                onClick={() => addSector(sectorToAdd)}
                disabled={!sectorToAdd}
                aria-label={isAr ? "إضافة" : "Add"}
              >
                <Plus className="size-4" />
              </Button>
            </div>
          )}
        </div>

        {/* Info: no journey binding */}
        <div className="rounded-md bg-nb-cyan-100/50 dark:bg-nb-cyan-900/20 border border-nb-cyan/20 p-3 space-y-1">
          <p className="text-xs font-medium text-nb-cyan-800 dark:text-nb-cyan-200">
            {isAr ? "القوالب لا تحتوي على روابط رحلات" : "Templates have no journey bindings"}
          </p>
          <p className="text-xs text-nb-cyan-700 dark:text-nb-cyan-300 leading-relaxed">
            {isAr
              ? "روابط الرحلات ونقاط التماس تُحدد عند إنشاء الاستبيان من هذا القالب."
              : "Journey and touchpoint bindings are set when a survey is created from this template."}
          </p>
        </div>
      </div>
    </div>
  )
}

// ── Page ──────────────────────────────────────────────────────────────────────
export default function TemplateBuilderPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { i18n } = useTranslation()
  const { isRtl } = useDirection()
  const isAr = i18n.language === "ar"
  const BackIcon = isRtl ? ArrowRight : ArrowLeft

  const existingTemplate = id ? MOCK_TEMPLATES.find((t) => t.id === id) : null
  const isPlatform = existingTemplate?.templateClass === "Platform"

  const [templateName, setTemplateName] = useState(
    existingTemplate ? (isAr ? existingTemplate.nameAr : existingTemplate.nameEn) : ""
  )
  const [sectors, setSectors] = useState<string[]>(existingTemplate?.sectors ?? [])
  const [surveyJson, setSurveyJson] = useState<object>(existingTemplate?.surveyJson ?? {})
  const [isDirty, setIsDirty] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [useAsSurveyOpen, setUseAsSurveyOpen] = useState(false)

  const handleJsonChange = useCallback((json: object) => {
    if (!isPlatform) { setSurveyJson(json); setIsDirty(true) }
  }, [isPlatform])

  async function handleSave() {
    if (isPlatform) return
    setIsSaving(true)
    await new Promise((r) => setTimeout(r, 800))
    setIsDirty(false)
    setIsSaving(false)
    toast.success(isAr ? "تم حفظ القالب" : "Template saved")
  }

  function handleUseAsSurvey() {
    toast.success(
      isAr
        ? "تم إنشاء استبيان جديد من هذا القالب"
        : "New survey created from this template"
    )
    navigate("/surveys/new/builder")
  }

  return (
    <div className="flex flex-col h-[calc(100vh-48px)]">
      {/* ── Header ────────────────────────────────────────────────────── */}
      <header className="sticky top-0 z-30 bg-background/95 backdrop-blur-sm border-b border-border px-4 h-14 flex items-center justify-between gap-4 shrink-0">
        {/* Back + Name */}
        <div className="flex items-center gap-3 min-w-0">
          <Button
            variant="ghost"
            size="icon"
            className="size-9 shrink-0"
            onClick={() => navigate("/surveys")}
            aria-label={isAr ? "العودة إلى المكتبة" : "Back to library"}
          >
            <BackIcon className="size-4" />
          </Button>

          <div className="flex items-center gap-2 min-w-0">
            <span className="text-sm font-semibold text-foreground truncate max-w-60">
              {templateName || (isAr ? "قالب بلا اسم" : "Untitled Template")}
            </span>
            {isPlatform ? (
              <Badge className="text-xs bg-nb-navy-100 text-nb-navy-800 dark:bg-nb-navy-700/40 dark:text-nb-navy-100 border-transparent shrink-0">
                <Lock className="size-2.5 me-1" />
                {isAr ? "منصة" : "Platform"}
              </Badge>
            ) : (
              <Badge className="text-xs bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200 border-transparent shrink-0">
                {isAr ? "مخصص" : "Tenant"}
              </Badge>
            )}
            {isDirty && (
              <span className="text-xs text-muted-foreground shrink-0">
                {isAr ? "• غير محفوظ" : "• Unsaved"}
              </span>
            )}
          </div>
        </div>

        {/* Actions */}
        <div className="flex items-center gap-2 shrink-0">
          <Button
            className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            size="sm"
            onClick={() => setUseAsSurveyOpen(true)}
          >
            <ClipboardList className="size-4 me-1.5" />
            {isAr ? "استخدم كاستبيان" : "Use as Survey"}
          </Button>
          {!isPlatform && (
            <Button
              variant="secondary"
              size="sm"
              onClick={handleSave}
              disabled={isSaving || !isDirty}
            >
              {isSaving ? (
                <Loader2 className="size-4 me-1.5 animate-spin" />
              ) : (
                <Save className="size-4 me-1.5" />
              )}
              {isAr ? "حفظ" : "Save"}
            </Button>
          )}
        </div>
      </header>

      {/* ── Body ──────────────────────────────────────────────────────── */}
      <div className="flex flex-1 overflow-hidden">
        {/* Canvas */}
        <div className="flex-1 overflow-hidden">
          <SurveyBuilderCanvas
            initialJson={surveyJson}
            isTemplate
            locale={isAr ? "ar" : "en"}
            onJsonChange={handleJsonChange}
          />
        </div>

        {/* Template meta panel */}
        <div className="w-80 xl:w-96 shrink-0 overflow-hidden">
          <TemplateMetaPanel
            isAr={isAr}
            name={templateName}
            sectors={sectors}
            isPlatform={isPlatform ?? false}
            onNameChange={(v) => { setTemplateName(v); setIsDirty(true) }}
            onSectorsChange={(v) => { setSectors(v); setIsDirty(true) }}
          />
        </div>
      </div>

      <UseAsSurveyDialog
        open={useAsSurveyOpen}
        isAr={isAr}
        templateName={templateName}
        onOpenChange={setUseAsSurveyOpen}
        onConfirm={handleUseAsSurvey}
      />
    </div>
  )
}
