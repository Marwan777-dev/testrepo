import { useState, useCallback } from "react"
import { useNavigate, useParams } from "react-router"
import {
  ArrowRight, ArrowLeft, Eye, Save, LayoutTemplate, ChevronDown, Loader2,
  Globe, BarChart2, MoreHorizontal,
} from "lucide-react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from "@/components/ui/dialog"
import { useTranslation } from "react-i18next"
import { useDirection } from "@/hooks/use-direction"
import { SurveyBuilderCanvas } from "@/components/surveys/SurveyBuilderCanvas"
import { NabadatPropertiesPanel } from "@/components/surveys/NabadatPropertiesPanel"
import { MOCK_SURVEYS } from "@/data/mock-surveys"
import type {
  SurveyStatus, SurveySettings, KpiQuestionConfig, QuestionBinding,
} from "@/types/survey"
import { DEFAULT_POST_EXPIRY_MESSAGE } from "@/types/survey"
import { cn } from "@/lib/utils"
import { toast } from "sonner"

// ── Status config ─────────────────────────────────────────────────────────────
const STATUS_CONFIG: Record<SurveyStatus, { labelAr: string; labelEn: string; className: string }> = {
  Draft:    { labelAr: "مسودة",  labelEn: "Draft",    className: "bg-muted text-muted-foreground" },
  Active:   { labelAr: "نشط",   labelEn: "Active",   className: "bg-[#C8F5DB] text-[#156632] dark:bg-[#156632]/20 dark:text-[#C8F5DB]" },
  Paused:   { labelAr: "موقوف", labelEn: "Paused",   className: "bg-[#FFF0CC] text-[#7A5000] dark:bg-[#7A5000]/20 dark:text-[#FFF0CC]" },
  Archived: { labelAr: "مؤرشف", labelEn: "Archived", className: "bg-muted/60 text-muted-foreground" },
}

// ── Save as Template dialog ───────────────────────────────────────────────────
function SaveAsTemplateDialog({
  open,
  isAr,
  onOpenChange,
  onConfirm,
}: {
  open: boolean
  isAr: boolean
  onOpenChange: (v: boolean) => void
  onConfirm: (name: string) => void
}) {
  const [name, setName] = useState("")

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{isAr ? "حفظ كقالب" : "Save as Template"}</DialogTitle>
          <DialogDescription className="text-sm text-muted-foreground leading-relaxed">
            {isAr
              ? "سيتم إنشاء قالب جديد من بنية هذا الاستبيان. روابط الرحلة ونقاط التماس لن تُحفظ في القالب وستُعاد ربطها عند الاستخدام."
              : "A new template will be created from this survey's structure. Journey and touchpoint bindings are not saved to templates — you'll re-bind them when creating a survey from this template."}
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-2 py-2">
          <label className="text-sm font-medium">
            {isAr ? "اسم القالب" : "Template Name"}
            <span className="text-destructive me-1">*</span>
          </label>
          <Input
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder={isAr ? "أدخل اسم القالب..." : "Enter template name..."}
            dir={isAr ? "rtl" : "ltr"}
          />
        </div>
        <DialogFooter className="gap-2">
          <Button variant="ghost" onClick={() => onOpenChange(false)}>
            {isAr ? "إلغاء" : "Cancel"}
          </Button>
          <Button
            className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            disabled={!name.trim()}
            onClick={() => {
              onConfirm(name.trim())
              onOpenChange(false)
              setName("")
            }}
          >
            {isAr ? "حفظ القالب" : "Save Template"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

// ── Page ──────────────────────────────────────────────────────────────────────
export default function SurveyBuilderPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { i18n } = useTranslation()
  const { isRtl } = useDirection()
  const isAr = i18n.language === "ar"
  const BackIcon = isRtl ? ArrowRight : ArrowLeft

  // Load existing survey or start fresh
  const existingSurvey = id ? MOCK_SURVEYS.find((s) => s.id === id) : null

  const [surveyName, setSurveyName] = useState(
    existingSurvey ? (isAr ? existingSurvey.nameAr : existingSurvey.nameEn) : ""
  )
  const [nameEditing, setNameEditing] = useState(!existingSurvey)
  const [status, setStatus] = useState<SurveyStatus>(existingSurvey?.status ?? "Draft")
  const [surveyJson, setSurveyJson] = useState<object>(existingSurvey?.surveyJson ?? {})
  const [selectedQuestion, setSelectedQuestion] = useState<string | null>(null)
  const [isDirty, setIsDirty] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [saveAsTemplateOpen, setSaveAsTemplateOpen] = useState(false)

  const [settings, setSettings] = useState<SurveySettings>(
    existingSurvey?.settings ?? {
      type: "Transactional",
      shuffleEnabled: false,
      shuffleMode: "Random",
      postExpiryEnabled: false,
      postExpiryMessage: DEFAULT_POST_EXPIRY_MESSAGE,
    }
  )

  const [kpiConfigs, setKpiConfigs] = useState<Record<string, KpiQuestionConfig>>({})
  const [questionBindings, setQuestionBindings] = useState<Record<string, QuestionBinding>>({})

  const handleJsonChange = useCallback((json: object) => {
    setSurveyJson(json)
    setIsDirty(true)
  }, [])

  const handleQuestionSelected = useCallback((name: string | null, isKpi: boolean) => {
    setSelectedQuestion(name)
    if (isKpi && name && !kpiConfigs[name]) {
      setKpiConfigs((prev) => ({
        ...prev,
        [name]: {
          questionName: name,
          kpiId: "",
          representationStyle: "Number",
          justificationEnabled: false,
          justificationUnsatisfiedReasons: [],
          justificationSatisfiedReasons: [],
          justificationMultiSelect: false,
          justificationHasOther: false,
        },
      }))
    }
  }, [kpiConfigs])

  function handleStatusChange(next: SurveyStatus) {
    if (next === "Active") {
      const pages = (surveyJson as any)?.pages ?? []
      const hasQuestions = pages.some((p: any) => (p.elements ?? []).length > 0)
      if (!hasQuestions) {
        toast.error(
          isAr
            ? "أضف سؤالاً واحداً على الأقل قبل تفعيل الاستبيان."
            : "Add at least one question before activating this survey."
        )
        return
      }
      const unboundKpi = Object.values(kpiConfigs).find((k) => !k.touchpointId)
      if (unboundKpi) {
        toast.error(
          isAr
            ? "يجب ربط جميع أسئلة KPI بنقطة تماس واحدة قبل التفعيل."
            : "All KPI questions must be bound to a single touchpoint before activating."
        )
        return
      }
    }
    setStatus(next)
    setIsDirty(true)
  }

  async function handleSave() {
    setIsSaving(true)
    await new Promise((r) => setTimeout(r, 800)) // Simulate API call
    setIsDirty(false)
    setIsSaving(false)
    toast.success(isAr ? "تم حفظ الاستبيان" : "Survey saved")
  }

  function handleSaveAsTemplate(name: string) {
    toast.success(
      isAr ? `تم حفظ القالب "${name}" بنجاح` : `Template "${name}" saved successfully`
    )
  }

  const statusCfg = STATUS_CONFIG[status]

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

          {nameEditing ? (
            <Input
              autoFocus
              value={surveyName}
              onChange={(e) => { setSurveyName(e.target.value); setIsDirty(true) }}
              onBlur={() => surveyName.trim() && setNameEditing(false)}
              onKeyDown={(e) => e.key === "Enter" && surveyName.trim() && setNameEditing(false)}
              placeholder={isAr ? "اسم الاستبيان" : "Survey name"}
              className="h-8 w-64 text-sm font-medium"
              dir={isAr ? "rtl" : "ltr"}
            />
          ) : (
            <button
              onClick={() => setNameEditing(true)}
              className="text-sm font-semibold text-foreground hover:text-primary transition-colors truncate max-w-60"
            >
              {surveyName || (isAr ? "استبيان بلا اسم" : "Untitled Survey")}
            </button>
          )}

          {isDirty && (
            <span className="text-xs text-muted-foreground shrink-0">
              {isAr ? "• غير محفوظ" : "• Unsaved"}
            </span>
          )}
        </div>

        {/* Center: Status */}
        <div className="flex items-center gap-2 shrink-0">
          <DropdownMenu>
            <DropdownMenuTrigger
              className={cn("flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-medium cursor-pointer", statusCfg.className)}
            >
              {isAr ? statusCfg.labelAr : statusCfg.labelEn}
              <ChevronDown className="size-3" />
            </DropdownMenuTrigger>
            <DropdownMenuContent align="center">
              {(["Draft", "Active", "Paused", "Archived"] as SurveyStatus[]).map((s) => (
                <DropdownMenuItem
                  key={s}
                  onClick={() => handleStatusChange(s)}
                  className={cn(s === status && "font-medium text-primary")}
                >
                  {isAr ? STATUS_CONFIG[s].labelAr : STATUS_CONFIG[s].labelEn}
                </DropdownMenuItem>
              ))}
            </DropdownMenuContent>
          </DropdownMenu>
        </div>

        {/* Actions */}
        <div className="flex items-center gap-2 shrink-0">
          <Button
            variant="secondary"
            size="sm"
            onClick={() => navigate(`/surveys/${id ?? "new"}/preview`)}
          >
            <Eye className="size-4 me-1.5" />
            {isAr ? "معاينة" : "Preview"}
          </Button>
          <Button
            variant="secondary"
            size="sm"
            onClick={() => navigate(`/surveys/${id ?? "new"}/translations`)}
          >
            <Globe className="size-4 me-1.5" />
            {isAr ? "ترجمة" : "Translate"}
          </Button>

          {/* More actions */}
          <DropdownMenu>
            <DropdownMenuTrigger
              className={cn(
                "inline-flex items-center justify-center size-8 rounded-md border border-border text-muted-foreground hover:bg-accent hover:text-foreground transition-colors"
              )}
              aria-label={isAr ? "المزيد من الإجراءات" : "More actions"}
            >
              <MoreHorizontal className="size-4" />
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              {id && (
                <DropdownMenuItem onClick={() => navigate(`/surveys/${id}/stats`)}>
                  <BarChart2 className="size-4 me-2" />
                  {isAr ? "الإحصائيات" : "Statistics"}
                </DropdownMenuItem>
              )}
              {id && (
                <DropdownMenuItem onClick={() => navigate(`/surveys/${id}/funnel`)}>
                  <BarChart2 className="size-4 me-2" />
                  {isAr ? "مسار الاستجابة" : "Response Funnel"}
                </DropdownMenuItem>
              )}
              <DropdownMenuItem onClick={() => setSaveAsTemplateOpen(true)}>
                <LayoutTemplate className="size-4 me-2" />
                {isAr ? "حفظ كقالب" : "Save as Template"}
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>

          <Button
            className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
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
        </div>
      </header>

      {/* ── Body ──────────────────────────────────────────────────────── */}
      <div className="flex flex-1 overflow-hidden">
        {/* Canvas (flex-1) */}
        <div className="flex-1 overflow-hidden">
          <SurveyBuilderCanvas
            initialJson={surveyJson}
            locale={isAr ? "ar" : "en"}
            onJsonChange={handleJsonChange}
            onQuestionSelected={handleQuestionSelected}
          />
        </div>

        {/* Nabadat Properties Panel */}
        <div className="w-80 xl:w-96 shrink-0 overflow-hidden">
          <NabadatPropertiesPanel
            isAr={isAr}
            settings={settings}
            onSettingsChange={(s) => { setSettings(s); setIsDirty(true) }}
            selectedQuestionName={selectedQuestion}
            kpiConfigs={kpiConfigs}
            onKpiConfigChange={(name, cfg) => {
              setKpiConfigs((prev) => ({ ...prev, [name]: cfg }))
              setIsDirty(true)
            }}
            questionBindings={questionBindings}
            onBindingChange={(name, b) => {
              setQuestionBindings((prev) => ({ ...prev, [name]: b }))
              setIsDirty(true)
            }}
          />
        </div>
      </div>

      <SaveAsTemplateDialog
        open={saveAsTemplateOpen}
        isAr={isAr}
        onOpenChange={setSaveAsTemplateOpen}
        onConfirm={handleSaveAsTemplate}
      />
    </div>
  )
}
