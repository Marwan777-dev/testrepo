import { useState } from "react"
import {
  Settings, HelpCircle, Languages, GitBranch, Plus, X, GripVertical,
  AlertTriangle, Info, Gauge,
} from "lucide-react"
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs"
import { Label } from "@/components/ui/label"
import { Switch } from "@/components/ui/switch"
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Badge } from "@/components/ui/badge"
import { cn } from "@/lib/utils"
import type { SurveySettings, KpiQuestionConfig, QuestionBinding } from "@/types/survey"
import { DEFAULT_POST_EXPIRY_MESSAGE } from "@/types/survey"
import {
  MOCK_JOURNEYS_FOR_BINDING,
  MOCK_STAGES_BY_JOURNEY,
  MOCK_TOUCHPOINTS_BY_STAGE,
  MOCK_ACTIVE_KPIS,
} from "@/data/mock-surveys"

interface NabadatPropertiesPanelProps {
  isAr: boolean
  settings: SurveySettings
  onSettingsChange: (s: SurveySettings) => void
  selectedQuestionName: string | null
  kpiConfigs: Record<string, KpiQuestionConfig>
  onKpiConfigChange: (name: string, cfg: KpiQuestionConfig) => void
  questionBindings: Record<string, QuestionBinding>
  onBindingChange: (name: string, b: QuestionBinding) => void
}

// ── Reusable section wrapper ──────────────────────────────────────────────────
function PanelSection({
  title,
  children,
}: {
  title: string
  children: React.ReactNode
}) {
  return (
    <div className="space-y-3">
      <p className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">
        {title}
      </p>
      {children}
    </div>
  )
}

// ── Survey settings tab ───────────────────────────────────────────────────────
function SurveySettingsTab({
  isAr,
  settings,
  onChange,
}: {
  isAr: boolean
  settings: SurveySettings
  onChange: (s: SurveySettings) => void
}) {
  const selectedJourneyStages = settings.journeyId
    ? (MOCK_STAGES_BY_JOURNEY[settings.journeyId] ?? [])
    : []

  const showTypeAdvisory =
    settings.type === "Transactional" &&
    settings.journeyId === "j-001" // Digital Onboarding is Lifecycle — unusual pairing

  return (
    <div className="space-y-6 p-4">
      {/* Survey type */}
      <PanelSection title={isAr ? "نوع الاستبيان" : "Survey Type"}>
        <div className="grid grid-cols-2 gap-2">
          {(["Transactional", "Relational"] as const).map((t) => (
            <button
              key={t}
              onClick={() => onChange({ ...settings, type: t })}
              className={cn(
                "rounded-md border px-3 py-2 text-xs font-medium text-start transition-colors",
                settings.type === t
                  ? "border-primary bg-primary/10 text-primary"
                  : "border-border text-muted-foreground hover:bg-accent"
              )}
            >
              {t === "Transactional"
                ? (isAr ? "تشغيلي" : "Transactional")
                : (isAr ? "دوري / علائقي" : "Relational")}
            </button>
          ))}
        </div>
        {showTypeAdvisory && (
          <div className="flex items-start gap-2 rounded-md bg-[#FFF0CC] dark:bg-[#7A5000]/20 border border-[#E8A020]/30 p-3 text-xs text-[#7A5000] dark:text-[#FFF0CC]">
            <AlertTriangle className="size-3.5 mt-0.5 shrink-0" />
            <span>
              {isAr
                ? "نوع الاستبيان غير معتاد لنوع الرحلة المختار. تابع إذا كان ذلك مقصوداً."
                : "This survey type is unusual for the selected journey type. Continue if intentional."}
            </span>
          </div>
        )}
      </PanelSection>

      {/* Journey binding */}
      <PanelSection title={isAr ? "ربط الرحلة" : "Journey Binding"}>
        <Select
          value={settings.journeyId ?? "none"}
          onValueChange={(v) => {
            const val = v ?? "none"
            onChange({ ...settings, journeyId: val === "none" ? undefined : val, stageIds: [] })
          }}
        >
          <SelectTrigger>
            <SelectValue placeholder={isAr ? "اختر رحلة..." : "Select journey..."} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="none">{isAr ? "— غير مرتبط —" : "— Unbound —"}</SelectItem>
            {MOCK_JOURNEYS_FOR_BINDING.map((j) => (
              <SelectItem key={j.id} value={j.id}>
                {isAr ? j.nameAr : j.nameEn}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        {selectedJourneyStages.length > 0 && (
          <div className="space-y-2">
            <Label className="text-xs text-muted-foreground">
              {isAr ? "المراحل (اختياري)" : "Stages (optional)"}
            </Label>
            <div className="flex flex-col gap-1">
              {selectedJourneyStages.map((stage) => {
                const selected = settings.stageIds?.includes(stage.id) ?? false
                return (
                  <button
                    key={stage.id}
                    onClick={() => {
                      const current = settings.stageIds ?? []
                      const next = selected
                        ? current.filter((id) => id !== stage.id)
                        : [...current, stage.id]
                      onChange({ ...settings, stageIds: next })
                    }}
                    className={cn(
                      "flex items-center gap-2 rounded-md border px-3 py-2 text-xs text-start transition-colors",
                      selected
                        ? "border-primary bg-primary/10 text-primary"
                        : "border-border text-muted-foreground hover:bg-accent"
                    )}
                  >
                    <span className={cn("size-2 rounded-full shrink-0", selected ? "bg-primary" : "bg-muted-foreground/30")} />
                    {isAr ? stage.nameAr : stage.nameEn}
                  </button>
                )
              })}
            </div>
          </div>
        )}
      </PanelSection>

      {/* Shuffle */}
      <PanelSection title={isAr ? "خلط الأسئلة" : "Question Shuffling"}>
        <div className="flex items-center justify-between">
          <Label htmlFor="shuffle-toggle" className="text-sm">
            {isAr ? "تفعيل الخلط" : "Enable Shuffling"}
          </Label>
          <Switch
            id="shuffle-toggle"
            checked={settings.shuffleEnabled}
            onCheckedChange={(v) => onChange({ ...settings, shuffleEnabled: v })}
          />
        </div>
        {settings.shuffleEnabled && (
          <div className="space-y-2">
            <Select
              value={settings.shuffleMode}
              onValueChange={(v) =>
                v && onChange({ ...settings, shuffleMode: v as SurveySettings["shuffleMode"] })
              }
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Random">
                  {isAr ? "خلط عشوائي" : "Random Shuffling"}
                </SelectItem>
                <SelectItem value="LowResponse">
                  {isAr ? "إعطاء الأولوية للأسئلة قليلة الردود" : "Prioritize Low-Response Questions"}
                </SelectItem>
                <SelectItem value="RoundRobin">
                  {isAr ? "خلط دوري (Round Robin)" : "Round Robin Shuffling"}
                </SelectItem>
              </SelectContent>
            </Select>
            {settings.shuffleMode !== "Random" && (
              <div className="flex items-start gap-2 rounded-md bg-muted/60 p-3 text-xs text-muted-foreground">
                <Info className="size-3.5 mt-0.5 shrink-0" />
                <span>
                  {isAr
                    ? "يتطلب بيانات الردود من M-04 ويصبح فعّالاً بعد بدء جمع الردود."
                    : "Requires response data from M-04 and takes effect once collection begins."}
                </span>
              </div>
            )}
          </div>
        )}
      </PanelSection>

      {/* Post-expiry */}
      <PanelSection title={isAr ? "جمع الردود بعد الانتهاء" : "Post-Expiry Feedback"}>
        <div className="flex items-center justify-between">
          <Label htmlFor="postexpiry-toggle" className="text-sm">
            {isAr ? "السماح بالردود بعد الانتهاء" : "Allow Post-Expiry Responses"}
          </Label>
          <Switch
            id="postexpiry-toggle"
            checked={settings.postExpiryEnabled}
            onCheckedChange={(v) =>
              onChange({
                ...settings,
                postExpiryEnabled: v,
                postExpiryMessage: v ? DEFAULT_POST_EXPIRY_MESSAGE : "",
              })
            }
          />
        </div>
        {settings.postExpiryEnabled && (
          <div
            className={cn(
              "grid transition-[grid-template-rows,opacity] duration-300 ease-[cubic-bezier(0.16,1,0.3,1)]",
              "grid-rows-[1fr] opacity-100"
            )}
          >
            <div className="overflow-hidden">
              <div className="pt-2 space-y-2">
                <Label className="text-xs text-muted-foreground">
                  {isAr ? "رسالة ما بعد الانتهاء" : "Post-Expiry Message"}
                </Label>
                <Textarea
                  value={settings.postExpiryMessage}
                  onChange={(e) => onChange({ ...settings, postExpiryMessage: e.target.value })}
                  rows={3}
                  className="text-sm resize-none"
                  dir={isAr ? "rtl" : "ltr"}
                />
              </div>
            </div>
          </div>
        )}
      </PanelSection>
    </div>
  )
}

// ── KPI question config ───────────────────────────────────────────────────────
function KpiQuestionTab({
  isAr,
  config,
  onChange,
  surveyStageIds,
}: {
  isAr: boolean
  config: KpiQuestionConfig
  onChange: (c: KpiQuestionConfig) => void
  surveyStageIds?: string[]
}) {
  const [newUnsatisfied, setNewUnsatisfied] = useState("")
  const [newSatisfied, setNewSatisfied] = useState("")

  // Collect touchpoints from selected stages
  const availableTouchpoints = (surveyStageIds ?? []).flatMap(
    (sid) => MOCK_TOUCHPOINTS_BY_STAGE[sid] ?? []
  )

  const selectedKpi = MOCK_ACTIVE_KPIS.find((k) => k.id === config.kpiId)

  const REPRESENTATION_OPTIONS = [
    { value: "Number", labelAr: "رقمي", labelEn: "Number" },
    { value: "Stars", labelAr: "نجوم", labelEn: "Stars" },
    { value: "Faces", labelAr: "وجوه تعبيرية", labelEn: "Emoji Faces" },
    { value: "Thumbs", labelAr: "إبهام", labelEn: "Thumbs" },
    { value: "Slider", labelAr: "شريط تمرير", labelEn: "Slider" },
  ]

  function addReason(branch: "unsatisfied" | "satisfied", text: string) {
    if (!text.trim()) return
    const key = branch === "unsatisfied" ? "justificationUnsatisfiedReasons" : "justificationSatisfiedReasons"
    onChange({ ...config, [key]: [...config[key], text.trim()] })
    if (branch === "unsatisfied") setNewUnsatisfied("")
    else setNewSatisfied("")
  }

  function removeReason(branch: "unsatisfied" | "satisfied", idx: number) {
    const key = branch === "unsatisfied" ? "justificationUnsatisfiedReasons" : "justificationSatisfiedReasons"
    onChange({ ...config, [key]: config[key].filter((_, i) => i !== idx) })
  }

  return (
    <div className="space-y-6 p-4">
      {/* KPI selection */}
      <PanelSection title={isAr ? "مؤشر الأداء (KPI)" : "KPI"}>
        <Select
          value={config.kpiId}
          onValueChange={(v) => v && onChange({ ...config, kpiId: v })}
        >
          <SelectTrigger>
            <SelectValue placeholder={isAr ? "اختر مؤشراً..." : "Select KPI..."} />
          </SelectTrigger>
          <SelectContent>
            {MOCK_ACTIVE_KPIS.map((kpi) => (
              <SelectItem key={kpi.id} value={kpi.id}>
                <span className="font-medium">{kpi.shortName}</span>
                <span className="text-muted-foreground ms-2 text-xs">({kpi.scale})</span>
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        {selectedKpi && (
          <p className="text-xs text-muted-foreground">{selectedKpi.fullName}</p>
        )}
      </PanelSection>

      {/* Representation */}
      <PanelSection title={isAr ? "أسلوب العرض" : "Representation Style"}>
        <div className="grid grid-cols-2 gap-1.5">
          {REPRESENTATION_OPTIONS.map((opt) => (
            <button
              key={opt.value}
              onClick={() => onChange({ ...config, representationStyle: opt.value as KpiQuestionConfig["representationStyle"] })}
              className={cn(
                "rounded-md border px-2 py-1.5 text-xs font-medium text-start transition-colors",
                config.representationStyle === opt.value
                  ? "border-primary bg-primary/10 text-primary"
                  : "border-border text-muted-foreground hover:bg-accent"
              )}
            >
              {isAr ? opt.labelAr : opt.labelEn}
            </button>
          ))}
        </div>
        <p className="text-xs text-muted-foreground">
          {isAr
            ? "أسلوب العرض للعرض فقط — القيمة المُرسَلة تتطابق دائماً مع مقياس مؤشر الأداء."
            : "Display only — the emitted value always matches the KPI's scale."}
        </p>
      </PanelSection>

      {/* Touchpoint binding (required for KPI questions) */}
      <PanelSection title={isAr ? "ربط نقطة التماس (مطلوب)" : "Touchpoint Binding (Required)"}>
        {availableTouchpoints.length === 0 ? (
          <p className="text-xs text-muted-foreground rounded-md bg-muted/60 p-3">
            {isAr
              ? "حدد مرحلة في إعدادات الاستبيان أولاً لإتاحة نقاط التماس."
              : "Select a stage in Survey Settings first to see available touchpoints."}
          </p>
        ) : (
          <Select
            value={config.touchpointId ?? "none"}
            onValueChange={(v) => {
              const val = v ?? "none"
              const tp = availableTouchpoints.find((t) => t.id === val)
              onChange({
                ...config,
                touchpointId: val === "none" ? undefined : val,
                touchpointName: tp ? (isAr ? tp.nameAr : tp.nameEn) : undefined,
              })
            }}
          >
            <SelectTrigger className={cn(!config.touchpointId && "border-destructive")}>
              <SelectValue placeholder={isAr ? "اختر نقطة تماس..." : "Select touchpoint..."} />
            </SelectTrigger>
            <SelectContent>
              {availableTouchpoints.map((tp) => (
                <SelectItem key={tp.id} value={tp.id}>
                  {isAr ? tp.nameAr : tp.nameEn}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}
        {!config.touchpointId && (
          <p className="text-xs text-destructive">
            {isAr
              ? "سؤال KPI يتطلب ربطاً بنقطة تماس واحدة."
              : "A KPI question must be bound to a single touchpoint."}
          </p>
        )}
      </PanelSection>

      {/* Score justification */}
      <PanelSection title={isAr ? "أسئلة التبرير" : "Score Justification"}>
        <div className="flex items-center justify-between">
          <Label htmlFor="justification-toggle" className="text-sm">
            {isAr ? "تفعيل أسئلة التبرير" : "Enable Justification Questions"}
          </Label>
          <Switch
            id="justification-toggle"
            checked={config.justificationEnabled}
            onCheckedChange={(v) => onChange({ ...config, justificationEnabled: v })}
          />
        </div>

        {config.justificationEnabled && (
          <div className="space-y-4 pt-2">
            {/* Multi-select toggle */}
            <div className="flex items-center justify-between">
              <Label className="text-xs text-muted-foreground">
                {isAr ? "تحديد متعدد" : "Multi-Select Reasons"}
              </Label>
              <Switch
                checked={config.justificationMultiSelect}
                onCheckedChange={(v) => onChange({ ...config, justificationMultiSelect: v })}
              />
            </div>

            {/* Unsatisfied reasons */}
            <ReasonsList
              isAr={isAr}
              branch="unsatisfied"
              label={isAr ? "أسباب الفرع غير الراضي" : "Unsatisfied Branch Reasons"}
              reasons={config.justificationUnsatisfiedReasons}
              newValue={newUnsatisfied}
              onNewValueChange={setNewUnsatisfied}
              onAdd={(text) => addReason("unsatisfied", text)}
              onRemove={(idx) => removeReason("unsatisfied", idx)}
              colorClass="border-[#C01B2A]/30 bg-[#FFD6DA]/30 dark:bg-[#6B0010]/20"
            />

            {/* Satisfied reasons */}
            <ReasonsList
              isAr={isAr}
              branch="satisfied"
              label={isAr ? "أسباب الفرع الراضي" : "Satisfied Branch Reasons"}
              reasons={config.justificationSatisfiedReasons}
              newValue={newSatisfied}
              onNewValueChange={setNewSatisfied}
              onAdd={(text) => addReason("satisfied", text)}
              onRemove={(idx) => removeReason("satisfied", idx)}
              colorClass="border-[#2EB85C]/30 bg-[#C8F5DB]/30 dark:bg-[#156632]/20"
            />

            {/* Include "Other" */}
            <div className="flex items-center gap-2">
              <input
                id="other-option"
                type="checkbox"
                checked={config.justificationHasOther}
                onChange={(e) => onChange({ ...config, justificationHasOther: e.target.checked })}
                className="rounded border-border"
              />
              <Label htmlFor="other-option" className="text-sm cursor-pointer">
                {isAr ? 'تضمين خيار "أخرى (نص حر)"' : 'Include "Other (free text)" option'}
              </Label>
            </div>
          </div>
        )}
      </PanelSection>
    </div>
  )
}

function ReasonsList({
  isAr,
  label,
  reasons,
  newValue,
  onNewValueChange,
  onAdd,
  onRemove,
  colorClass,
}: {
  isAr: boolean
  branch: "unsatisfied" | "satisfied"
  label: string
  reasons: string[]
  newValue: string
  onNewValueChange: (v: string) => void
  onAdd: (text: string) => void
  onRemove: (idx: number) => void
  colorClass: string
}) {
  return (
    <div className={cn("rounded-md border p-3 space-y-2", colorClass)}>
      <p className="text-xs font-medium text-foreground">{label}</p>
      {reasons.map((r, idx) => (
        <div key={idx} className="flex items-center gap-2 text-xs">
          <GripVertical className="size-3 text-muted-foreground shrink-0" />
          <span className="flex-1 text-foreground">{r}</span>
          <button onClick={() => onRemove(idx)} aria-label={isAr ? "حذف" : "Remove"}>
            <X className="size-3 text-muted-foreground hover:text-destructive" />
          </button>
        </div>
      ))}
      <div className="flex items-center gap-2">
        <Input
          value={newValue}
          onChange={(e) => onNewValueChange(e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && onAdd(newValue)}
          placeholder={isAr ? "أضف سبباً..." : "Add reason..."}
          className="h-7 text-xs"
        />
        <Button
          size="icon"
          variant="ghost"
          className="size-7 shrink-0"
          onClick={() => onAdd(newValue)}
          aria-label={isAr ? "إضافة" : "Add"}
        >
          <Plus className="size-3" />
        </Button>
      </div>
    </div>
  )
}

// ── General question settings tab ─────────────────────────────────────────────
function GeneralQuestionTab({
  isAr,
  binding,
  onChange,
  surveyJourneyId,
}: {
  isAr: boolean
  binding: QuestionBinding
  onChange: (b: QuestionBinding) => void
  surveyJourneyId?: string
}) {
  const availableStages = surveyJourneyId
    ? (MOCK_STAGES_BY_JOURNEY[surveyJourneyId] ?? [])
    : []

  const availableTouchpoints = binding.stageId
    ? (MOCK_TOUCHPOINTS_BY_STAGE[binding.stageId] ?? [])
    : []

  return (
    <div className="space-y-6 p-4">
      <PanelSection title={isAr ? "مستوى الربط" : "Binding Level"}>
        <div className="grid grid-cols-3 gap-1.5">
          {(["journey", "stage", "touchpoint"] as const).map((lvl) => (
            <button
              key={lvl}
              onClick={() => onChange({ ...binding, bindingLevel: lvl })}
              className={cn(
                "rounded-md border px-2 py-1.5 text-xs font-medium text-center transition-colors",
                binding.bindingLevel === lvl
                  ? "border-primary bg-primary/10 text-primary"
                  : "border-border text-muted-foreground hover:bg-accent"
              )}
            >
              {isAr
                ? lvl === "journey" ? "رحلة" : lvl === "stage" ? "مرحلة" : "نقطة تماس"
                : lvl === "journey" ? "Journey" : lvl === "stage" ? "Stage" : "Touchpoint"}
            </button>
          ))}
        </div>

        {(binding.bindingLevel === "stage" || binding.bindingLevel === "touchpoint") && (
          <Select
            value={binding.stageId ?? "none"}
            onValueChange={(v) => { const val = v ?? "none"; onChange({ ...binding, stageId: val === "none" ? undefined : val, touchpointId: undefined }) }}
          >
            <SelectTrigger>
              <SelectValue placeholder={isAr ? "اختر مرحلة..." : "Select stage..."} />
            </SelectTrigger>
            <SelectContent>
              {availableStages.map((s) => (
                <SelectItem key={s.id} value={s.id}>
                  {isAr ? s.nameAr : s.nameEn}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}

        {binding.bindingLevel === "touchpoint" && binding.stageId && (
          <Select
            value={binding.touchpointId ?? "none"}
            onValueChange={(v) => { const val = v ?? "none"; onChange({ ...binding, touchpointId: val === "none" ? undefined : val }) }}
          >
            <SelectTrigger>
              <SelectValue placeholder={isAr ? "اختر نقطة تماس..." : "Select touchpoint..."} />
            </SelectTrigger>
            <SelectContent>
              {availableTouchpoints.map((tp) => (
                <SelectItem key={tp.id} value={tp.id}>
                  {isAr ? tp.nameAr : tp.nameEn}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}
      </PanelSection>
    </div>
  )
}

// ── Empty state (no question selected) ───────────────────────────────────────
function NoQuestionSelected({ isAr }: { isAr: boolean }) {
  return (
    <div className="flex flex-col items-center justify-center py-12 text-center px-6">
      <HelpCircle className="size-10 text-muted-foreground mb-3" />
      <p className="text-sm font-medium text-foreground mb-1">
        {isAr ? "لم يتم تحديد سؤال" : "No Question Selected"}
      </p>
      <p className="text-xs text-muted-foreground leading-relaxed">
        {isAr
          ? "حدد سؤالاً في المحرر لتكوين خصائصه هنا"
          : "Select a question in the editor to configure its properties here"}
      </p>
    </div>
  )
}

// ── Main panel ────────────────────────────────────────────────────────────────
export function NabadatPropertiesPanel({
  isAr,
  settings,
  onSettingsChange,
  selectedQuestionName,
  kpiConfigs,
  onKpiConfigChange,
  questionBindings,
  onBindingChange,
}: NabadatPropertiesPanelProps) {
  const isKpiQuestion =
    selectedQuestionName !== null &&
    kpiConfigs[selectedQuestionName] !== undefined

  const currentKpiConfig: KpiQuestionConfig = selectedQuestionName
    ? (kpiConfigs[selectedQuestionName] ?? {
        questionName: selectedQuestionName,
        kpiId: "",
        representationStyle: "Number",
        justificationEnabled: false,
        justificationUnsatisfiedReasons: [],
        justificationSatisfiedReasons: [],
        justificationMultiSelect: false,
        justificationHasOther: false,
      })
    : {
        questionName: "",
        kpiId: "",
        representationStyle: "Number",
        justificationEnabled: false,
        justificationUnsatisfiedReasons: [],
        justificationSatisfiedReasons: [],
        justificationMultiSelect: false,
        justificationHasOther: false,
      }

  const currentBinding: QuestionBinding = selectedQuestionName
    ? (questionBindings[selectedQuestionName] ?? {
        questionName: selectedQuestionName,
        bindingLevel: "journey",
      })
    : { questionName: "", bindingLevel: "journey" }

  return (
    <div className="flex flex-col h-full bg-card border-s border-border">
      <Tabs defaultValue="settings" className="flex flex-col h-full">
        <TabsList className="rounded-none border-b border-border bg-transparent px-4 py-0 h-12 justify-start gap-0 shrink-0">
          <TabsTrigger
            value="settings"
            className="rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:shadow-none px-3 h-full text-xs gap-1.5"
          >
            <Settings className="size-3.5" />
            {isAr ? "الإعدادات" : "Settings"}
          </TabsTrigger>
          <TabsTrigger
            value="question"
            className="rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:shadow-none px-3 h-full text-xs gap-1.5"
          >
            {isKpiQuestion ? <Gauge className="size-3.5 text-nb-cyan" /> : <HelpCircle className="size-3.5" />}
            {isAr ? "السؤال" : "Question"}
            {selectedQuestionName && (
              <span className="size-1.5 rounded-full bg-primary" />
            )}
          </TabsTrigger>
          <TabsTrigger
            value="translation"
            className="rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:shadow-none px-3 h-full text-xs gap-1.5"
          >
            <Languages className="size-3.5" />
            {isAr ? "الترجمة" : "Translation"}
          </TabsTrigger>
          <TabsTrigger
            value="versions"
            className="rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:shadow-none px-3 h-full text-xs gap-1.5"
          >
            <GitBranch className="size-3.5" />
            {isAr ? "الإصدارات" : "Versions"}
          </TabsTrigger>
        </TabsList>

        <div className="flex-1 overflow-y-auto">
          <TabsContent value="settings" className="m-0 h-full">
            <SurveySettingsTab isAr={isAr} settings={settings} onChange={onSettingsChange} />
          </TabsContent>

          <TabsContent value="question" className="m-0 h-full">
            {!selectedQuestionName ? (
              <NoQuestionSelected isAr={isAr} />
            ) : isKpiQuestion ? (
              <KpiQuestionTab
                isAr={isAr}
                config={currentKpiConfig}
                onChange={(cfg) => onKpiConfigChange(selectedQuestionName, cfg)}
                surveyStageIds={settings.stageIds}
              />
            ) : (
              <GeneralQuestionTab
                isAr={isAr}
                binding={currentBinding}
                onChange={(b) => onBindingChange(selectedQuestionName, b)}
                surveyJourneyId={settings.journeyId}
              />
            )}
          </TabsContent>

          <TabsContent value="translation" className="m-0">
            <div className="flex flex-col items-center justify-center py-16 text-center px-6">
              <Languages className="size-10 text-muted-foreground mb-3" />
              <p className="text-sm font-medium text-foreground mb-1">
                {isAr ? "مساحة الترجمة" : "Translation Workspace"}
              </p>
              <p className="text-xs text-muted-foreground leading-relaxed mb-4">
                {isAr
                  ? "إدارة ترجمات الاستبيان وسير عمل الموافقة متاحة قريباً"
                  : "Survey translation management and approval workflow — coming soon"}
              </p>
              <Badge variant="outline" className="text-xs">
                {isAr ? "المرحلة الثانية" : "Phase 2"}
              </Badge>
            </div>
          </TabsContent>

          <TabsContent value="versions" className="m-0">
            <div className="flex flex-col items-center justify-center py-16 text-center px-6">
              <GitBranch className="size-10 text-muted-foreground mb-3" />
              <p className="text-sm font-medium text-foreground mb-1">
                {isAr ? "سجل الإصدارات" : "Version History"}
              </p>
              <p className="text-xs text-muted-foreground leading-relaxed mb-4">
                {isAr
                  ? "نموذج الإصدارات قيد التطوير. حقل الإصدار محجوز في البنية الحالية."
                  : "The versioning model is under development. The version field is reserved in the current schema."}
              </p>
              <div className="rounded-md bg-muted/60 border border-border px-4 py-2 font-mono text-sm text-muted-foreground">
                v1.0
              </div>
              <Badge variant="outline" className="text-xs mt-3">
                {isAr ? "مؤجل" : "Deferred"}
              </Badge>
            </div>
          </TabsContent>
        </div>
      </Tabs>
    </div>
  )
}
