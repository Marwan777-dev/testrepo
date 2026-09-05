import { useState } from "react"
import { useNavigate, useParams } from "react-router"
import {
  ArrowRight, ArrowLeft, Plus, Check, Pencil, Globe, Lock,
  AlertTriangle, Sparkles, CheckCheck, X, Send,
} from "lucide-react"
import { Button } from "@/components/ui/button"
import { Textarea } from "@/components/ui/textarea"
import { Badge } from "@/components/ui/badge"
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from "@/components/ui/dialog"
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select"
import {
  Tooltip, TooltipContent, TooltipProvider, TooltipTrigger,
} from "@/components/ui/tooltip"
import { useTranslation } from "react-i18next"
import { useDirection } from "@/hooks/use-direction"
import { MOCK_SURVEYS } from "@/data/mock-surveys"
import type { TranslationState } from "@/types/survey"
import { cn } from "@/lib/utils"
import { toast } from "sonner"

// ── Field definitions ─────────────────────────────────────────────────────────
interface TranslationField {
  id: string
  labelAr: string
  labelEn: string
  sourceText: string
  category: "survey" | "question" | "option" | "setting"
  multiline?: boolean
}

const FIELDS: TranslationField[] = [
  { id: "survey_title", labelAr: "عنوان الاستبيان",     labelEn: "Survey Title",         sourceText: "استبيان رضا العملاء — الربع الثاني",                                         category: "survey" },
  { id: "q1_title",     labelAr: "عنوان السؤال 1",      labelEn: "Q1 Title",              sourceText: "كيف تقيّم تجربتك العامة مع خدمتنا؟",                                        category: "question", multiline: true },
  { id: "q1_desc",      labelAr: "وصف السؤال 1",        labelEn: "Q1 Description",        sourceText: "على مقياس من 1 إلى 5",                                                       category: "question" },
  { id: "q2_title",     labelAr: "عنوان السؤال 2",      labelEn: "Q2 Title",              sourceText: "ما مدى احتمال توصيتك بخدماتنا لأصدقائك وزملائك؟",                          category: "question", multiline: true },
  { id: "q2_opt1",      labelAr: "الخيار 1 (س2)",       labelEn: "Q2 Option 1",           sourceText: "جودة الخدمة ممتازة",                                                         category: "option" },
  { id: "q2_opt2",      labelAr: "الخيار 2 (س2)",       labelEn: "Q2 Option 2",           sourceText: "سرعة الاستجابة مناسبة",                                                      category: "option" },
  { id: "q3_title",     labelAr: "عنوان السؤال 3",      labelEn: "Q3 Title",              sourceText: "هل لديك أي ملاحظات أو اقتراحات إضافية؟",                                   category: "question", multiline: true },
  { id: "post_expiry",  labelAr: "رسالة ما بعد الانتهاء", labelEn: "Post-Expiry Message", sourceText: "على الرغم من انتهاء فترة الاستبيان الأصلية، نرحب بملاحظاتك حول تجربتك الأخيرة.", category: "setting", multiline: true },
]

// ── Language definitions ──────────────────────────────────────────────────────
const AVAILABLE_LANGUAGES = [
  { code: "en", nameAr: "الإنجليزية", nameEn: "English",  dir: "ltr" as const },
  { code: "fr", nameAr: "الفرنسية",   nameEn: "French",   dir: "ltr" as const },
  { code: "de", nameAr: "الألمانية",   nameEn: "German",   dir: "ltr" as const },
  { code: "tr", nameAr: "التركية",     nameEn: "Turkish",  dir: "ltr" as const },
  { code: "ur", nameAr: "الأردية",     nameEn: "Urdu",     dir: "rtl" as const },
]

function getLangDir(code: string): "ltr" | "rtl" {
  return AVAILABLE_LANGUAGES.find((l) => l.code === code)?.dir ?? "ltr"
}

// ── Realistic AI draft translations per language ──────────────────────────────
const AI_DRAFT_TEXTS: Record<string, Record<string, string>> = {
  en: {
    survey_title: "Customer Satisfaction Survey — Q2",
    q1_title:     "How would you rate your overall experience with our service?",
    q1_desc:      "On a scale from 1 to 5",
    q2_title:     "How likely are you to recommend our services to your friends and colleagues?",
    q2_opt1:      "Excellent service quality",
    q2_opt2:      "Adequate response speed",
    q3_title:     "Do you have any additional comments or suggestions?",
    post_expiry:  "Although the original survey period has ended, we still welcome your feedback on your recent experience.",
  },
  fr: {
    survey_title: "Enquête de satisfaction client — T2",
    q1_title:     "Comment évalueriez-vous votre expérience globale avec notre service ?",
    q1_desc:      "Sur une échelle de 1 à 5",
    q2_title:     "Quelle est la probabilité que vous recommandiez nos services à vos amis et collègues ?",
    q2_opt1:      "Excellente qualité de service",
    q2_opt2:      "Rapidité de réponse satisfaisante",
    q3_title:     "Avez-vous des commentaires ou suggestions supplémentaires ?",
    post_expiry:  "Bien que la période d'enquête originale soit terminée, nous apprécions toujours vos retours sur votre expérience récente.",
  },
  de: {
    survey_title: "Kundenzufriedenheitsumfrage — Q2",
    q1_title:     "Wie würden Sie Ihre Gesamterfahrung mit unserem Service bewerten?",
    q1_desc:      "Auf einer Skala von 1 bis 5",
    q2_title:     "Wie wahrscheinlich ist es, dass Sie unsere Dienste Ihren Freunden und Kollegen empfehlen würden?",
    q2_opt1:      "Hervorragende Servicequalität",
    q2_opt2:      "Angemessene Reaktionsgeschwindigkeit",
    q3_title:     "Haben Sie weitere Kommentare oder Vorschläge?",
    post_expiry:  "Obwohl der ursprüngliche Umfragezeitraum abgelaufen ist, freuen wir uns weiterhin über Ihr Feedback zu Ihrer jüngsten Erfahrung.",
  },
  tr: {
    survey_title: "Müşteri Memnuniyeti Anketi — Ç2",
    q1_title:     "Hizmetimizle genel deneyiminizi nasıl değerlendirirsiniz?",
    q1_desc:      "1'den 5'e kadar bir ölçekte",
    q2_title:     "Hizmetlerimizi arkadaşlarınıza ve meslektaşlarınıza tavsiye etme olasılığınız nedir?",
    q2_opt1:      "Mükemmel hizmet kalitesi",
    q2_opt2:      "Yeterli yanıt hızı",
    q3_title:     "Ek yorumlarınız veya önerileriniz var mı?",
    post_expiry:  "Orijinal anket süresi sona ermiş olsa da, son deneyiminiz hakkındaki görüşlerinizi almaktan memnuniyet duyarız.",
  },
  ur: {
    survey_title: "گاہک اطمینان سروے — سہ ماہی 2",
    q1_title:     "آپ ہماری سروس کے ساتھ اپنے مجموعی تجربے کو کیسے ریٹ کریں گے؟",
    q1_desc:      "1 سے 5 کے پیمانے پر",
    q2_title:     "آپ اپنے دوستوں اور ساتھیوں کو ہماری خدمات کی سفارش کرنے کا کتنا امکان ہے؟",
    q2_opt1:      "بہترین سروس کا معیار",
    q2_opt2:      "مناسب جوابی رفتار",
    q3_title:     "کیا آپ کے کوئی اضافی تبصرے یا تجاویز ہیں؟",
    post_expiry:  "اگرچہ اصل سروے کی مدت ختم ہو چکی ہے، ہم اپنے حالیہ تجربے کے بارے میں آپ کی رائے کا خیرمقدم کرتے ہیں۔",
  },
}

// ── Initial state ─────────────────────────────────────────────────────────────
type TranslationRecord = Record<string, { text: string; state: TranslationState }>

// English: mixed states to demonstrate all scenarios
const INITIAL_EN: TranslationRecord = {
  survey_title: { text: AI_DRAFT_TEXTS.en.survey_title, state: "approved" },
  q1_title:     { text: AI_DRAFT_TEXTS.en.q1_title,     state: "ai_draft" },
  q1_desc:      { text: AI_DRAFT_TEXTS.en.q1_desc,      state: "ai_draft" },
  q2_title:     { text: AI_DRAFT_TEXTS.en.q2_title,     state: "human_edited" },
  q2_opt1:      { text: AI_DRAFT_TEXTS.en.q2_opt1,      state: "approved" },
  // Stale: source was edited ("سرعة الاستجابة" → "سرعة الاستجابة مناسبة") on Jun 20
  q2_opt2:      { text: AI_DRAFT_TEXTS.en.q2_opt2,      state: "stale" },
  q3_title:     { text: AI_DRAFT_TEXTS.en.q3_title,     state: "ai_draft" },
  post_expiry:  { text: "",                              state: "untranslated" },
}

// French: all untranslated
const INITIAL_FR: TranslationRecord = Object.fromEntries(
  FIELDS.map((f) => [f.id, { text: "", state: "untranslated" as TranslationState }])
)

// ── State chip config ─────────────────────────────────────────────────────────
const STATE_CONFIG: Record<TranslationState, { labelAr: string; labelEn: string; className: string }> = {
  untranslated: { labelAr: "غير مترجم",          labelEn: "Untranslated",   className: "bg-muted text-muted-foreground border-transparent" },
  ai_draft:     { labelAr: "مسودة AI",            labelEn: "AI Draft",       className: "bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan-900/30 dark:text-nb-cyan-200 border-transparent" },
  human_edited: { labelAr: "بانتظار المراجعة",    labelEn: "Needs Review",   className: "bg-[#FFF0CC] text-[#7A5000] dark:bg-[#7A5000]/20 dark:text-[#FFF0CC] border-transparent" },
  approved:     { labelAr: "معتمد",               labelEn: "Approved",       className: "bg-[#C8F5DB] text-[#156632] dark:bg-[#156632]/20 dark:text-[#C8F5DB] border-transparent" },
  stale:        { labelAr: "متقادم",              labelEn: "Stale",          className: "bg-[#FFE4D0] text-[#7A2800] dark:bg-[#7A2800]/20 dark:text-[#FFE4D0] border-transparent" },
}

const CATEGORY_LABELS: Record<TranslationField["category"], { ar: string; en: string }> = {
  survey:   { ar: "استبيان",  en: "Survey"   },
  question: { ar: "سؤال",    en: "Question" },
  option:   { ar: "خيار",    en: "Option"   },
  setting:  { ar: "إعداد",   en: "Setting"  },
}

type FilterType = "all" | "needs_review" | "approved" | "stale" | "untranslated"

// ── Helpers ───────────────────────────────────────────────────────────────────
function progressColor(approved: number, total: number): string {
  if (total === 0) return "#7A8196"
  const r = approved / total
  if (r >= 1) return "#2EB85C"
  if (r >= 0.5) return "#E8A020"
  return "#C01B2A"
}

function canPublish(t: TranslationRecord): boolean {
  return FIELDS.every((f) => t[f.id]?.state === "approved")
}

function publishBlockers(t: TranslationRecord, isAr: boolean): string {
  const untranslated = FIELDS.filter((f) => t[f.id]?.state === "untranslated" || !t[f.id]).length
  const stale        = FIELDS.filter((f) => t[f.id]?.state === "stale").length
  const needsReview  = FIELDS.filter((f) => t[f.id]?.state === "ai_draft" || t[f.id]?.state === "human_edited").length
  const parts: string[] = []
  if (untranslated) parts.push(isAr ? `${untranslated} غير مترجم` : `${untranslated} untranslated`)
  if (stale)        parts.push(isAr ? `${stale} متقادم` : `${stale} stale`)
  if (needsReview)  parts.push(isAr ? `${needsReview} بانتظار المراجعة` : `${needsReview} needs review`)
  return parts.join("، ")
}

// ── Page ──────────────────────────────────────────────────────────────────────
export default function SurveyTranslationsPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { i18n } = useTranslation()
  const { isRtl } = useDirection()
  const isAr = i18n.language === "ar"
  const BackIcon = isRtl ? ArrowRight : ArrowLeft

  const survey = id ? MOCK_SURVEYS.find((s) => s.id === id) : null
  const surveyName = survey ? (isAr ? survey.nameAr : survey.nameEn) : (isAr ? "استبيان" : "Survey")

  const [addedLangs, setAddedLangs] = useState<string[]>(["en", "fr"])
  const [draftingLangs, setDraftingLangs] = useState<Set<string>>(new Set())
  const [selectedLang, setSelectedLang] = useState("en")
  const [translations, setTranslations] = useState<Record<string, TranslationRecord>>({
    en: INITIAL_EN,
    fr: INITIAL_FR,
  })
  const [filter, setFilter] = useState<FilterType>("all")
  const [editingField, setEditingField] = useState<string | null>(null)
  const [editDraft, setEditDraft] = useState("")
  const [addLangOpen, setAddLangOpen] = useState(false)
  const [langToAdd, setLangToAdd] = useState("")
  const [isRequestingDraft, setIsRequestingDraft] = useState(false)
  const [staleDismissed, setStaleDismissed] = useState(false)

  const currentTranslations = translations[selectedLang] ?? {}
  const selectedLangMeta = AVAILABLE_LANGUAGES.find((l) => l.code === selectedLang)
  const translationDir = getLangDir(selectedLang)

  function getApprovedCount(langCode: string) {
    const t = translations[langCode] ?? {}
    return FIELDS.filter((f) => t[f.id]?.state === "approved").length
  }

  function countByState(langCode: string, states: TranslationState[]) {
    const t = translations[langCode] ?? {}
    return FIELDS.filter((f) => states.includes(t[f.id]?.state ?? "untranslated")).length
  }

  function matchesFilter(fieldId: string): boolean {
    const state = currentTranslations[fieldId]?.state ?? "untranslated"
    switch (filter) {
      case "needs_review": return state === "ai_draft" || state === "human_edited" || state === "stale"
      case "approved":     return state === "approved"
      case "stale":        return state === "stale"
      case "untranslated": return state === "untranslated"
      default:             return true
    }
  }

  function handleApprove(fieldId: string) {
    setTranslations((prev) => ({
      ...prev,
      [selectedLang]: {
        ...prev[selectedLang],
        [fieldId]: { ...prev[selectedLang][fieldId], state: "approved" },
      },
    }))
  }

  function handleApproveAll() {
    setTranslations((prev) => {
      const updated = { ...prev[selectedLang] }
      FIELDS.forEach((f) => {
        if (updated[f.id]?.state === "ai_draft" || updated[f.id]?.state === "human_edited") {
          updated[f.id] = { ...updated[f.id], state: "approved" }
        }
      })
      return { ...prev, [selectedLang]: updated }
    })
    toast.success(isAr ? "تم اعتماد جميع المسودات" : "All drafts approved")
  }

  function startEditing(fieldId: string) {
    setEditingField(fieldId)
    setEditDraft(currentTranslations[fieldId]?.text ?? "")
  }

  function commitEdit(fieldId: string) {
    const text = editDraft.trim()
    setTranslations((prev) => {
      const current = prev[selectedLang]?.[fieldId]
      const prevState = current?.state ?? "untranslated"
      const newState: TranslationState =
        prevState === "approved" ? "human_edited" :
        prevState === "untranslated" && text ? "human_edited" :
        prevState === "untranslated" ? "untranslated" :
        prevState
      return {
        ...prev,
        [selectedLang]: {
          ...prev[selectedLang],
          [fieldId]: { text, state: newState },
        },
      }
    })
    setEditingField(null)
    setEditDraft("")
  }

  function cancelEdit() {
    setEditingField(null)
    setEditDraft("")
  }

  async function handleRequestDraft() {
    setIsRequestingDraft(true)
    await new Promise((r) => setTimeout(r, 1800))
    const draftMap = AI_DRAFT_TEXTS[selectedLang] ?? {}
    setTranslations((prev) => {
      const updated = { ...prev[selectedLang] }
      FIELDS.forEach((f) => {
        if (updated[f.id]?.state === "untranslated") {
          updated[f.id] = { text: draftMap[f.id] ?? "", state: "ai_draft" }
        }
      })
      return { ...prev, [selectedLang]: updated }
    })
    setIsRequestingDraft(false)
    toast.success(isAr ? "تمت ترجمة الحقول بالذكاء الاصطناعي — يرجى مراجعتها قبل الاعتماد" : "AI draft translation complete — please review before approving")
  }

  async function handleAddLanguage() {
    if (!langToAdd || addedLangs.includes(langToAdd)) return
    const code = langToAdd
    setAddedLangs((prev) => [...prev, code])
    setTranslations((prev) => ({
      ...prev,
      [code]: Object.fromEntries(FIELDS.map((f) => [f.id, { text: "", state: "untranslated" as TranslationState }])),
    }))
    setSelectedLang(code)
    setLangToAdd("")
    setAddLangOpen(false)
    // Auto-request AI draft for new language
    setDraftingLangs((prev) => new Set(prev).add(code))
    await new Promise((r) => setTimeout(r, 2000))
    const draftMap = AI_DRAFT_TEXTS[code] ?? {}
    setTranslations((prev) => ({
      ...prev,
      [code]: Object.fromEntries(
        FIELDS.map((f) => [f.id, { text: draftMap[f.id] ?? "", state: (draftMap[f.id] ? "ai_draft" : "untranslated") as TranslationState }])
      ),
    }))
    setDraftingLangs((prev) => { const s = new Set(prev); s.delete(code); return s })
    toast.success(isAr ? `تمت ترجمة ${AVAILABLE_LANGUAGES.find(l => l.code === code)?.[isAr ? "nameAr" : "nameEn"]} بالذكاء الاصطناعي` : `AI draft ready for ${AVAILABLE_LANGUAGES.find(l => l.code === code)?.nameEn}`)
  }

  function handlePublish() {
    if (!canPublish(currentTranslations)) {
      toast.error(
        isAr
          ? `لا يمكن النشر — ${publishBlockers(currentTranslations, true)}`
          : `Cannot publish — ${publishBlockers(currentTranslations, false)}`
      )
      return
    }
    toast.success(
      isAr
        ? `تم نشر نسخة ${selectedLangMeta?.nameAr} بنجاح`
        : `${selectedLangMeta?.nameEn} version published successfully`
    )
  }

  const visibleFields = FIELDS.filter((f) => matchesFilter(f.id))

  const filterCounts: Record<FilterType, number> = {
    all:          FIELDS.length,
    needs_review: FIELDS.filter((f) => { const s = currentTranslations[f.id]?.state ?? "untranslated"; return s === "ai_draft" || s === "human_edited" || s === "stale" }).length,
    approved:     FIELDS.filter((f) => currentTranslations[f.id]?.state === "approved").length,
    stale:        FIELDS.filter((f) => currentTranslations[f.id]?.state === "stale").length,
    untranslated: FIELDS.filter((f) => !currentTranslations[f.id] || currentTranslations[f.id]?.state === "untranslated").length,
  }

  const aiDraftCount = FIELDS.filter((f) => currentTranslations[f.id]?.state === "ai_draft").length
  const showStaleNotice = filterCounts.stale > 0 && !staleDismissed

  return (
    <div className="flex flex-col h-[calc(100vh-48px)]">
      {/* ── Header ────────────────────────────────────────────── */}
      <header className="sticky top-0 z-30 bg-background/95 backdrop-blur-sm border-b border-border px-4 h-14 flex items-center gap-4 shrink-0">
        <Button
          variant="ghost"
          size="icon"
          className="size-9 shrink-0"
          onClick={() => navigate(id ? `/surveys/${id}/edit` : "/surveys")}
          aria-label={isAr ? "العودة إلى المحرر" : "Back to builder"}
        >
          <BackIcon className="size-4" />
        </Button>
        <div className="flex-1 min-w-0">
          <p className="text-xs text-muted-foreground">{isAr ? "ترجمة الاستبيان" : "Survey Translation"}</p>
          <p className="text-sm font-semibold text-foreground truncate">{surveyName}</p>
        </div>
        <div className="flex items-center gap-3 shrink-0">
          <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
            <Globe className="size-3.5" />
            <span>{addedLangs.length + 1} {isAr ? "لغات" : "languages"}</span>
          </div>
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger
                className={cn(
                  "inline-flex items-center gap-1.5 rounded-md px-3 py-1.5 text-xs font-semibold transition-colors",
                  canPublish(currentTranslations)
                    ? "bg-[#C8F5DB] text-[#156632] hover:bg-[#B0ECC7] dark:bg-[#156632]/20 dark:text-[#C8F5DB]"
                    : "bg-muted text-muted-foreground cursor-not-allowed"
                )}
                onClick={handlePublish}
                aria-label={isAr ? "نشر هذه اللغة" : "Publish this language"}
              >
                <Send className="size-3" />
                {isAr ? `نشر ${selectedLangMeta?.nameAr ?? ""}` : `Publish ${selectedLangMeta?.nameEn ?? ""}`}
              </TooltipTrigger>
              {!canPublish(currentTranslations) && (
                <TooltipContent className="max-w-xs text-xs">
                  {isAr
                    ? `لا يمكن النشر: ${publishBlockers(currentTranslations, true)}`
                    : `Cannot publish: ${publishBlockers(currentTranslations, false)}`}
                </TooltipContent>
              )}
            </Tooltip>
          </TooltipProvider>
        </div>
      </header>

      {/* ── Body ──────────────────────────────────────────────── */}
      <div className="flex flex-1 overflow-hidden">

        {/* ── Language sidebar ──────────────────────────────── */}
        <div className="w-64 shrink-0 border-e border-border flex flex-col bg-card">
          <div className="px-4 py-3 border-b border-border">
            <p className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">
              {isAr ? "اللغات" : "Languages"}
            </p>
          </div>

          <div className="flex-1 overflow-y-auto p-2 space-y-1">
            {/* Source language — locked */}
            <div className="flex items-center gap-2.5 rounded-md px-3 py-2.5 bg-muted/40">
              <Lock className="size-3.5 text-muted-foreground shrink-0" />
              <div className="flex-1 min-w-0">
                <p className="text-xs font-semibold text-foreground">
                  {isAr ? "العربية" : "Arabic"}
                </p>
                <div className="flex items-center gap-1.5 mt-0.5">
                  <div className="flex-1 h-1 rounded-full bg-[#2EB85C]" />
                  <span className="text-xs text-muted-foreground">{isAr ? "مصدر" : "Source"}</span>
                </div>
              </div>
            </div>

            {/* Target languages */}
            {addedLangs.map((code) => {
              const lang = AVAILABLE_LANGUAGES.find((l) => l.code === code)
              const approved = getApprovedCount(code)
              const total = FIELDS.length
              const pct = total > 0 ? (approved / total) * 100 : 0
              const isSelected = selectedLang === code
              const isDrafting = draftingLangs.has(code)
              const staleCount = countByState(code, ["stale"])
              const untranslatedCount = countByState(code, ["untranslated"])

              return (
                <button
                  key={code}
                  onClick={() => { setSelectedLang(code); setStaleDismissed(false) }}
                  className={cn(
                    "w-full flex items-center gap-2.5 rounded-md px-3 py-2.5 text-start transition-colors",
                    isSelected ? "bg-primary/10 ring-1 ring-primary/20" : "hover:bg-accent"
                  )}
                >
                  {/* Progress dot */}
                  {isDrafting ? (
                    <div className="size-3.5 rounded-full border-2 border-nb-cyan border-t-transparent animate-spin shrink-0" />
                  ) : (
                    <div
                      className="size-2.5 rounded-full shrink-0 mt-0.5"
                      style={{ backgroundColor: progressColor(approved, total) }}
                    />
                  )}

                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-1.5">
                      <p className={cn("text-xs font-semibold", isSelected ? "text-primary" : "text-foreground")}>
                        {isAr ? lang?.nameAr : lang?.nameEn}
                      </p>
                      {staleCount > 0 && (
                        <span className="size-4 flex items-center justify-center rounded-full bg-[#FFE4D0] text-[10px] font-bold text-[#7A2800]">
                          {staleCount}
                        </span>
                      )}
                    </div>

                    {isDrafting ? (
                      <p className="text-xs text-nb-cyan mt-0.5">{isAr ? "جاري الترجمة..." : "Translating..."}</p>
                    ) : (
                      <>
                        {/* Mini progress bar */}
                        <div className="flex items-center gap-1.5 mt-1">
                          <div className="flex-1 h-1 rounded-full bg-muted/60 overflow-hidden">
                            <div
                              className="h-full rounded-full transition-all duration-500"
                              style={{ width: `${pct}%`, backgroundColor: progressColor(approved, total) }}
                            />
                          </div>
                          <span className="text-xs text-muted-foreground tabular-nums">
                            {approved}/{total}
                          </span>
                        </div>
                        {untranslatedCount > 0 && (
                          <p className="text-[10px] text-muted-foreground mt-0.5">
                            {untranslatedCount} {isAr ? "غير مترجم" : "untranslated"}
                          </p>
                        )}
                      </>
                    )}
                  </div>
                </button>
              )
            })}
          </div>

          <div className="p-3 border-t border-border">
            <Button
              variant="secondary"
              size="sm"
              className="w-full"
              onClick={() => setAddLangOpen(true)}
            >
              <Plus className="size-4 me-1.5" />
              {isAr ? "إضافة لغة" : "Add Language"}
            </Button>
          </div>
        </div>

        {/* ── Main translation area ──────────────────────────── */}
        <div className="flex-1 flex flex-col overflow-hidden">

          {/* Toolbar */}
          <div className="border-b border-border px-4 py-2.5 flex items-center justify-between gap-4 shrink-0 flex-wrap gap-y-2">
            {/* Filter tabs */}
            <div className="flex items-center gap-1 flex-wrap">
              {(["all", "needs_review", "approved", "stale", "untranslated"] as FilterType[]).map((f) => {
                const labels: Record<FilterType, { ar: string; en: string }> = {
                  all:           { ar: "الكل",               en: "All" },
                  needs_review:  { ar: "بانتظار المراجعة",   en: "Needs Review" },
                  approved:      { ar: "معتمد",              en: "Approved" },
                  stale:         { ar: "متقادم",             en: "Stale" },
                  untranslated:  { ar: "غير مترجم",          en: "Untranslated" },
                }
                return (
                  <button
                    key={f}
                    onClick={() => setFilter(f)}
                    className={cn(
                      "flex items-center gap-1.5 px-2.5 py-1.5 rounded-md text-xs font-medium transition-colors whitespace-nowrap",
                      filter === f ? "bg-primary/10 text-primary" : "text-muted-foreground hover:text-foreground hover:bg-accent"
                    )}
                  >
                    {isAr ? labels[f].ar : labels[f].en}
                    {filterCounts[f] > 0 && (
                      <span className={cn(
                        "text-xs rounded-full px-1.5 py-0.5 min-w-[18px] text-center tabular-nums",
                        filter === f ? "bg-primary/20 text-primary" : "bg-muted text-muted-foreground"
                      )}>
                        {filterCounts[f]}
                      </span>
                    )}
                  </button>
                )
              })}
            </div>

            {/* Action buttons */}
            <div className="flex items-center gap-2">
              {aiDraftCount > 0 && (
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={handleApproveAll}
                >
                  <CheckCheck className="size-3.5 me-1.5" />
                  {isAr ? `اعتماد جميع المسودات (${aiDraftCount})` : `Approve all drafts (${aiDraftCount})`}
                </Button>
              )}
              <Button
                variant="secondary"
                size="sm"
                onClick={handleRequestDraft}
                disabled={isRequestingDraft || filterCounts.untranslated === 0}
              >
                {isRequestingDraft ? (
                  <div className="size-3.5 me-1.5 rounded-full border-2 border-nb-cyan border-t-transparent animate-spin" />
                ) : (
                  <Sparkles className="size-3.5 me-1.5" />
                )}
                {isAr ? "ترجمة AI للحقول الفارغة" : "AI Draft Empty Fields"}
              </Button>
            </div>
          </div>

          {/* Stale notice (FR-8.5) */}
          {showStaleNotice && (
            <div className="mx-4 mt-3 rounded-lg border border-[#E05C1A]/30 bg-[#FFE4D0]/60 dark:bg-[#7A2800]/20 px-4 py-3 flex items-start gap-3 shrink-0">
              <AlertTriangle className="size-4 text-[#E05C1A] shrink-0 mt-0.5" />
              <div className="flex-1 min-w-0">
                <p className="text-xs font-semibold text-[#7A2800] dark:text-[#FFE4D0]">
                  {filterCounts.stale === 1
                    ? (isAr ? "حقل واحد متقادم" : "1 stale field")
                    : (isAr ? `${filterCounts.stale} حقول متقادمة` : `${filterCounts.stale} stale fields`)}
                </p>
                <p className="text-xs text-[#7A2800]/70 dark:text-[#FFE4D0]/70 leading-relaxed mt-0.5">
                  {isAr
                    ? "تم تعديل النص المصدر بعد اعتماد هذه الترجمات — راجعها وأعد اعتمادها."
                    : "Source text was edited after these translations were approved — review and re-approve them."}
                </p>
              </div>
              <div className="flex items-center gap-1.5 shrink-0">
                <Button
                  size="sm"
                  variant="ghost"
                  className="h-7 text-xs text-[#7A2800] hover:bg-[#FFE4D0] px-2"
                  onClick={() => { setFilter("stale"); setStaleDismissed(false) }}
                >
                  {isAr ? "عرض" : "View"}
                </Button>
                <Button
                  size="icon"
                  variant="ghost"
                  className="size-7 text-[#7A2800] hover:bg-[#FFE4D0]"
                  onClick={() => setStaleDismissed(true)}
                  aria-label={isAr ? "تجاهل" : "Dismiss"}
                >
                  <X className="size-3" />
                </Button>
              </div>
            </div>
          )}

          {/* Translation table */}
          <div className="flex-1 overflow-y-auto mt-3">
            {visibleFields.length === 0 ? (
              <div className="flex flex-col items-center justify-center py-20 text-center">
                <Check className="size-10 text-[#2EB85C] mb-3" />
                <p className="text-base font-semibold mb-1">
                  {filter === "approved"
                    ? (isAr ? "لا توجد حقول معتمدة بعد" : "No approved fields yet")
                    : (isAr ? "لا توجد حقول تطابق الفلتر" : "No fields match this filter")}
                </p>
                <p className="text-sm text-muted-foreground">
                  {isAr ? "جرّب فلتراً مختلفاً" : "Try a different filter"}
                </p>
              </div>
            ) : (
              <table className="w-full text-sm">
                <thead className="sticky top-0 bg-muted/80 backdrop-blur-sm border-b border-border z-10">
                  <tr>
                    <th className="text-start text-xs font-semibold text-muted-foreground px-4 py-2.5 w-44">
                      {isAr ? "الحقل" : "Field"}
                    </th>
                    <th className="text-start text-xs font-semibold text-muted-foreground px-4 py-2.5 w-[36%]">
                      {isAr ? "النص المصدر (عربي)" : "Source (Arabic)"}
                    </th>
                    <th className="text-start text-xs font-semibold text-muted-foreground px-4 py-2.5">
                      {isAr ? `الترجمة (${selectedLangMeta?.nameAr ?? selectedLang})` : `Translation (${selectedLangMeta?.nameEn ?? selectedLang})`}
                    </th>
                    <th className="text-start text-xs font-semibold text-muted-foreground px-4 py-2.5 w-36">
                      {isAr ? "الحالة" : "Status"}
                    </th>
                    <th className="px-4 py-2.5 w-28" />
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {visibleFields.map((field) => {
                    const t = currentTranslations[field.id] ?? { text: "", state: "untranslated" as TranslationState }
                    const cfg = STATE_CONFIG[t.state]
                    const isEditing = editingField === field.id
                    const isStale = t.state === "stale"

                    return (
                      <tr
                        key={field.id}
                        className={cn(
                          "hover:bg-muted/20 transition-colors align-top",
                          isStale && "bg-[#FFE4D0]/15 dark:bg-[#7A2800]/8"
                        )}
                      >
                        {/* Field name */}
                        <td className="px-4 py-3">
                          <div className="flex items-start gap-1.5">
                            {isStale && <AlertTriangle className="size-3.5 text-[#E05C1A] mt-0.5 shrink-0" />}
                            <div>
                              <p className="text-xs font-semibold text-foreground leading-snug">
                                {isAr ? field.labelAr : field.labelEn}
                              </p>
                              <p className="text-xs text-muted-foreground mt-0.5">
                                {isAr ? CATEGORY_LABELS[field.category].ar : CATEGORY_LABELS[field.category].en}
                              </p>
                            </div>
                          </div>
                        </td>

                        {/* Source text */}
                        <td className="px-4 py-3">
                          <p
                            className="text-xs text-muted-foreground leading-relaxed"
                            dir="rtl"
                            lang="ar"
                          >
                            {field.sourceText}
                          </p>
                        </td>

                        {/* Translation — editable */}
                        <td className="px-4 py-3">
                          {isEditing ? (
                            <div className="space-y-2">
                              <Textarea
                                autoFocus
                                value={editDraft}
                                onChange={(e) => setEditDraft(e.target.value)}
                                onKeyDown={(e) => {
                                  if (e.key === "Enter" && !e.shiftKey && !field.multiline) {
                                    e.preventDefault()
                                    commitEdit(field.id)
                                  }
                                  if (e.key === "Escape") cancelEdit()
                                }}
                                rows={field.multiline ? 3 : 1}
                                className="text-xs resize-none"
                                dir={translationDir}
                                lang={selectedLang}
                              />
                              <div className="flex gap-1.5">
                                <Button
                                  size="sm"
                                  className="h-6 text-xs bg-primary hover:bg-nb-cyan-700 text-primary-foreground px-2"
                                  onClick={() => commitEdit(field.id)}
                                >
                                  {isAr ? "حفظ" : "Save"}
                                </Button>
                                <Button
                                  size="sm"
                                  variant="ghost"
                                  className="h-6 text-xs px-2"
                                  onClick={cancelEdit}
                                >
                                  {isAr ? "إلغاء" : "Cancel"}
                                </Button>
                              </div>
                            </div>
                          ) : (
                            <button
                              onClick={() => startEditing(field.id)}
                              className="text-start w-full group"
                            >
                              {t.text ? (
                                <p
                                  className="text-xs text-foreground leading-relaxed group-hover:text-primary transition-colors"
                                  dir={translationDir}
                                  lang={selectedLang}
                                >
                                  {t.text}
                                </p>
                              ) : (
                                <p className="text-xs text-muted-foreground italic group-hover:text-primary transition-colors">
                                  {isAr ? "انقر للإضافة..." : "Click to add..."}
                                </p>
                              )}
                            </button>
                          )}
                        </td>

                        {/* State badge */}
                        <td className="px-4 py-3">
                          <Badge className={cn("text-xs whitespace-nowrap", cfg.className)}>
                            {isAr ? cfg.labelAr : cfg.labelEn}
                          </Badge>
                          {isStale && (
                            <p className="text-[10px] text-[#E05C1A] mt-1">
                              {isAr ? "المصدر تغيّر" : "Source changed"}
                            </p>
                          )}
                        </td>

                        {/* Actions */}
                        <td className="px-4 py-3">
                          <div className="flex items-center gap-1">
                            {t.state !== "approved" && t.text && !isEditing && (
                              <Button
                                size="sm"
                                variant="ghost"
                                className="h-7 text-xs text-[#2EB85C] hover:bg-[#C8F5DB] hover:text-[#156632] dark:hover:bg-[#156632]/20 px-2 whitespace-nowrap"
                                onClick={() => handleApprove(field.id)}
                              >
                                <Check className="size-3 me-1" />
                                {isAr ? "اعتماد" : "Approve"}
                              </Button>
                            )}
                            {t.state === "approved" && !isEditing && (
                              <Button
                                size="icon"
                                variant="ghost"
                                className="size-7 text-muted-foreground"
                                onClick={() => startEditing(field.id)}
                                aria-label={isAr ? "تعديل" : "Edit"}
                              >
                                <Pencil className="size-3" />
                              </Button>
                            )}
                          </div>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            )}
          </div>
        </div>
      </div>

      {/* ── Add Language Dialog ────────────────────────────────── */}
      <Dialog open={addLangOpen} onOpenChange={setAddLangOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>{isAr ? "إضافة لغة هدف" : "Add Target Language"}</DialogTitle>
            <DialogDescription className="text-sm text-muted-foreground leading-relaxed">
              {isAr
                ? "بعد الإضافة سيتم طلب مسودة ترجمة من الذكاء الاصطناعي تلقائياً. راجعها واعتمدها قبل النشر — لا تصل إلى العملاء إلا بعد الاعتماد."
                : "After adding, an AI draft will be generated automatically. Review and approve before publishing — translations never reach customers without approval."}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2 py-2">
            <label className="text-sm font-medium">
              {isAr ? "اللغة" : "Language"}
              <span className="text-destructive me-1">*</span>
            </label>
            <Select value={langToAdd} onValueChange={(v) => setLangToAdd(v ?? "")}>
              <SelectTrigger>
                <SelectValue placeholder={isAr ? "اختر لغة..." : "Select a language..."} />
              </SelectTrigger>
              <SelectContent>
                {AVAILABLE_LANGUAGES.filter((l) => !addedLangs.includes(l.code) && l.code !== "ar").map((l) => (
                  <SelectItem key={l.code} value={l.code}>
                    {isAr ? l.nameAr : l.nameEn}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <DialogFooter className="gap-2">
            <Button variant="ghost" onClick={() => setAddLangOpen(false)}>
              {isAr ? "إلغاء" : "Cancel"}
            </Button>
            <Button
              className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
              disabled={!langToAdd}
              onClick={handleAddLanguage}
            >
              <Sparkles className="size-4 me-1.5" />
              {isAr ? "إضافة وترجمة AI" : "Add & AI Translate"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
