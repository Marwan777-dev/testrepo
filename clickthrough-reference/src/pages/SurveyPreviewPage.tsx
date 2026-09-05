import { useState } from "react"
import { useNavigate, useParams } from "react-router"
import {
  ArrowRight, ArrowLeft, Pencil, Monitor, Smartphone, Mail, MessageCircle,
  AlertTriangle, Star,
} from "lucide-react"
import { Button } from "@/components/ui/button"
import { useTranslation } from "react-i18next"
import { useDirection } from "@/hooks/use-direction"
import { MOCK_SURVEYS } from "@/data/mock-surveys"
import { cn } from "@/lib/utils"

// ── Demo questions (from mock survey data) ───────────────────────────────────
const DEMO_QUESTIONS = [
  {
    id: "q1",
    type: "kpi",
    titleAr: "كيف تقيّم تجربتك العامة مع خدمتنا؟",
    titleEn: "How would you rate your overall experience with our service?",
    kpi: "CSAT",
    scale: [1, 2, 3, 4, 5] as number[],
    whatsappWarning: { ar: "تُعرض مقياس التقييم كخيارات مرقمة على واتساب", en: "Rating scale renders as numbered options on WhatsApp" },
  },
  {
    id: "q2",
    type: "rating",
    titleAr: "ما مدى احتمال توصيتك بخدماتنا لأصدقائك وزملائك؟",
    titleEn: "How likely are you to recommend our services to friends and colleagues?",
    scale: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10] as number[],
    whatsappWarning: { ar: "يُعرض مقياس 0-10 كقائمة أرقام على واتساب", en: "0–10 scale renders as a numbered list on WhatsApp" },
  },
  {
    id: "q3",
    type: "text",
    titleAr: "ما الذي يمكننا تحسينه في تجربتك؟",
    titleEn: "What could we improve about your experience?",
    emailWarning: { ar: "حقول النص الحر تُفتح في المتصفح خارج البريد الإلكتروني", en: "Free-text fields open in browser outside the email" },
  },
]

type Channel = "mobile" | "desktop" | "whatsapp" | "email"

// ── Mobile Web preview ────────────────────────────────────────────────────────
function MobilePreview({ isAr, surveyName }: { isAr: boolean; surveyName: string }) {
  const [selected, setSelected] = useState<Record<string, number>>({})

  return (
    <div className="flex justify-center">
      <div
        className="relative rounded-[44px] overflow-hidden shadow-2xl"
        style={{ width: 375, border: "10px solid #1E2235" }}
      >
        {/* Status bar */}
        <div className="bg-nb-navy h-7 flex items-center justify-between px-6">
          <span className="text-white text-xs font-medium">9:41</span>
          <div className="flex items-center gap-1">
            <div className="flex gap-px items-end h-3">
              {[3,5,7,9].map((h, i) => (
                <div key={i} className="w-1 bg-white rounded-sm" style={{ height: h }} />
              ))}
            </div>
            <div className="w-5 h-3 rounded-sm border border-white flex items-center ps-0.5">
              <div className="w-3 h-2 bg-white rounded-xs" />
            </div>
          </div>
        </div>

        {/* Browser chrome */}
        <div className="bg-background border-b border-border flex items-center gap-2 px-3 h-8">
          <div className="flex-1 bg-muted rounded text-xs text-muted-foreground px-2 py-0.5 truncate">
            nabadat.cx/s/{surveyName.slice(0, 6)}
          </div>
        </div>

        {/* Survey content */}
        <div className="bg-background overflow-y-auto" style={{ maxHeight: 560 }}>
          {/* Brand header */}
          <div className="bg-gradient-to-r from-nb-mint to-nb-cyan px-4 py-5">
            <div className="flex items-center gap-2 mb-2">
              <div className="size-6 rounded-full bg-white/20 flex items-center justify-center">
                <span className="text-white text-xs font-bold">N</span>
              </div>
              <span className="text-white text-xs font-semibold">Nabadat</span>
            </div>
            <p className="text-white text-sm font-heading font-bold leading-snug" dir={isAr ? "rtl" : "ltr"}>
              {surveyName}
            </p>
            <p className="text-white/80 text-xs mt-1">
              {isAr ? "يستغرق دقيقتين فقط" : "Takes just 2 minutes"}
            </p>
          </div>

          <div className="px-4 py-4 space-y-5">
            {DEMO_QUESTIONS.map((q, idx) => (
              <div key={q.id} className="space-y-2">
                <p className="text-xs font-medium text-muted-foreground">
                  {idx + 1} / {DEMO_QUESTIONS.length}
                </p>
                <p className="text-sm font-semibold text-foreground leading-snug" dir={isAr ? "rtl" : "ltr"}>
                  {isAr ? q.titleAr : q.titleEn}
                </p>

                {q.type === "kpi" && (q.scale?.length ?? 0) <= 5 && (
                  <div className="flex gap-2">
                    {(q.scale ?? []).map((n) => (
                      <button
                        key={n}
                        onClick={() => setSelected((p) => ({ ...p, [q.id]: n }))}
                        className={cn(
                          "flex-1 aspect-square flex items-center justify-center rounded-lg border-2 text-sm font-bold transition-colors",
                          selected[q.id] === n
                            ? "border-primary bg-primary text-primary-foreground"
                            : "border-border text-foreground hover:border-primary/50"
                        )}
                      >
                        {n}
                      </button>
                    ))}
                  </div>
                )}

                {q.type === "rating" && (
                  <div className="flex flex-wrap gap-1.5">
                    {(q.scale ?? []).map((n) => (
                      <button
                        key={n}
                        onClick={() => setSelected((p) => ({ ...p, [q.id]: n }))}
                        className={cn(
                          "size-8 flex items-center justify-center rounded-md border text-xs font-medium transition-colors",
                          selected[q.id] === n
                            ? "border-primary bg-primary text-primary-foreground"
                            : "border-border text-foreground hover:border-primary/50"
                        )}
                      >
                        {n}
                      </button>
                    ))}
                  </div>
                )}

                {q.type === "text" && (
                  <div className="rounded-lg border border-border bg-muted/30 px-3 py-2 min-h-[60px]">
                    <p className="text-xs text-muted-foreground">
                      {isAr ? "اكتب هنا..." : "Type here..."}
                    </p>
                  </div>
                )}
              </div>
            ))}

            <Button className="w-full bg-primary hover:bg-nb-cyan-700 text-primary-foreground text-sm">
              {isAr ? "إرسال" : "Submit"}
            </Button>
            <p className="text-xs text-center text-muted-foreground pb-2">
              {isAr ? "مدعوم بـ Nabadat CX" : "Powered by Nabadat CX"}
            </p>
          </div>
        </div>
      </div>
    </div>
  )
}

// ── Desktop Web preview ───────────────────────────────────────────────────────
function DesktopPreview({ isAr, surveyName }: { isAr: boolean; surveyName: string }) {
  const [selected, setSelected] = useState<Record<string, number>>({})

  return (
    <div className="rounded-lg border border-border overflow-hidden shadow-sm">
      {/* Browser chrome */}
      <div className="bg-muted/80 border-b border-border flex items-center gap-3 px-4 h-10">
        <div className="flex gap-1.5">
          {["#FF5F57", "#FEBC2E", "#28C840"].map((c) => (
            <div key={c} className="size-3 rounded-full" style={{ backgroundColor: c }} />
          ))}
        </div>
        <div className="flex-1 max-w-sm bg-background rounded px-3 h-6 flex items-center">
          <span className="text-xs text-muted-foreground truncate">nabadat.cx/survey/srv-001</span>
        </div>
      </div>

      {/* Page content */}
      <div className="bg-background min-h-[520px] py-12 px-8">
        <div className="max-w-2xl mx-auto space-y-8">
          {/* Header */}
          <div className="text-center space-y-2">
            <div className="flex items-center justify-center gap-2 mb-3">
              <div className="h-1 w-12 rounded-full bg-gradient-to-r from-nb-mint to-nb-cyan" />
              <span className="text-xs font-medium text-muted-foreground uppercase tracking-widest">Nabadat CX</span>
              <div className="h-1 w-12 rounded-full bg-gradient-to-r from-nb-cyan to-nb-mint" />
            </div>
            <h1 className="text-2xl font-heading font-bold" dir={isAr ? "rtl" : "ltr"}>{surveyName}</h1>
            <p className="text-sm text-muted-foreground">
              {isAr ? "يستغرق دقيقتين فقط — شكراً لمشاركتك" : "Takes just 2 minutes — thank you for participating"}
            </p>
          </div>

          {/* Questions */}
          <div className="space-y-6">
            {DEMO_QUESTIONS.map((q, idx) => (
              <div key={q.id} className="rounded-lg border border-border bg-card p-6 space-y-4">
                <div className="flex items-start gap-3">
                  <span className="size-6 rounded-full bg-primary/10 text-primary text-xs font-bold flex items-center justify-center shrink-0 mt-0.5">
                    {idx + 1}
                  </span>
                  <p className="text-base font-semibold text-foreground leading-snug" dir={isAr ? "rtl" : "ltr"}>
                    {isAr ? q.titleAr : q.titleEn}
                  </p>
                </div>

                {q.type === "kpi" && (q.scale?.length ?? 0) <= 5 && (
                  <div className="flex gap-2 ps-9">
                    {[1,2,3,4,5].map((n) => (
                      <button
                        key={n}
                        onClick={() => setSelected((p) => ({ ...p, [q.id]: n }))}
                        className={cn(
                          "size-10 flex items-center justify-center rounded-md border-2 text-sm font-semibold transition-all",
                          selected[q.id] === n
                            ? "border-primary bg-primary text-primary-foreground scale-105"
                            : "border-border text-foreground hover:border-primary/60"
                        )}
                      >
                        <Star className={cn("size-5", selected[q.id] >= n ? "fill-[#E8A020] stroke-[#E8A020]" : "stroke-border")} />
                      </button>
                    ))}
                  </div>
                )}

                {q.type === "rating" && (
                  <div className="ps-9">
                    <div className="flex gap-1.5 flex-wrap">
                      {(q.scale ?? []).map((n) => (
                        <button
                          key={n}
                          onClick={() => setSelected((p) => ({ ...p, [q.id]: n }))}
                          className={cn(
                            "size-10 flex items-center justify-center rounded-md border text-sm font-medium transition-all",
                            selected[q.id] === n
                              ? "border-primary bg-primary text-primary-foreground"
                              : "border-border text-foreground hover:border-primary/50"
                          )}
                        >
                          {n}
                        </button>
                      ))}
                    </div>
                    <div className="flex justify-between mt-1.5 text-xs text-muted-foreground">
                      <span>{isAr ? "غير محتمل أبداً" : "Not at all likely"}</span>
                      <span>{isAr ? "محتمل للغاية" : "Extremely likely"}</span>
                    </div>
                  </div>
                )}

                {q.type === "text" && (
                  <div className="ps-9">
                    <div className="rounded-md border border-border bg-background px-4 py-3 min-h-[80px]">
                      <p className="text-sm text-muted-foreground">
                        {isAr ? "اكتب إجابتك هنا..." : "Write your answer here..."}
                      </p>
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>

          <div className="text-center">
            <Button className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground px-8">
              {isAr ? "إرسال الإجابات" : "Submit Answers"}
            </Button>
          </div>
        </div>
      </div>
    </div>
  )
}

// ── WhatsApp preview ──────────────────────────────────────────────────────────
function WhatsAppPreview({ isAr, surveyName }: { isAr: boolean; surveyName: string }) {
  return (
    <div className="flex justify-center">
      <div className="w-[380px] rounded-2xl overflow-hidden shadow-2xl border border-border">
        {/* WhatsApp header */}
        <div className="flex items-center gap-3 px-4 py-3" style={{ backgroundColor: "#075E54" }}>
          <div className="size-9 rounded-full bg-white flex items-center justify-center shrink-0">
            <span className="text-sm font-bold" style={{ color: "#075E54" }}>N</span>
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-white text-sm font-semibold">Nabadat CX</p>
            <p className="text-xs" style={{ color: "#a8d8b9" }}>
              {isAr ? "متصل" : "online"}
            </p>
          </div>
          <MessageCircle className="size-5 text-white/80" />
        </div>

        {/* Chat */}
        <div className="min-h-[480px] p-4 space-y-3" style={{ backgroundColor: "#ECE5DD" }}>
          {/* Intro message */}
          <div className="max-w-[85%] rounded-2xl rounded-ss-sm bg-white px-4 py-2.5 shadow-sm">
            <p className="text-sm leading-relaxed" dir={isAr ? "rtl" : "ltr"}>
              {isAr
                ? `مرحباً! نريد معرفة رأيك في ${surveyName}. هل يمكنك الإجابة على ٣ أسئلة سريعة؟`
                : `Hello! We'd love your feedback on ${surveyName}. Can you answer 3 quick questions?`}
            </p>
            <p className="text-[10px] text-right mt-1" style={{ color: "#8696a0" }}>10:23 ✓✓</p>
          </div>

          {DEMO_QUESTIONS.map((q, idx) => (
            <div key={q.id} className="space-y-1.5">
              {/* Question bubble */}
              <div className="max-w-[85%] rounded-2xl rounded-ss-sm bg-white px-4 py-2.5 shadow-sm">
                <p className="text-xs text-muted-foreground mb-1">
                  {isAr ? `السؤال ${idx + 1}` : `Question ${idx + 1}`}
                </p>
                <p className="text-sm leading-relaxed" dir={isAr ? "rtl" : "ltr"}>
                  {isAr ? q.titleAr : q.titleEn}
                </p>
                <p className="text-[10px] text-right mt-1" style={{ color: "#8696a0" }}>10:2{3 + idx}</p>
              </div>

              {/* WhatsApp-style options */}
              {(q.type === "kpi" || q.type === "rating") && (
                <div className="flex flex-wrap gap-1.5">
                  {(q.type === "kpi" ? [1,2,3,4,5] : [0,1,2,3,4,5,6,7,8,9,10]).map((n) => (
                    <button
                      key={n}
                      className="px-3 py-1 rounded-full text-xs font-medium border"
                      style={{ backgroundColor: "#DCF8C6", borderColor: "#25D366", color: "#075E54" }}
                    >
                      {n}
                    </button>
                  ))}
                </div>
              )}

              {q.type === "text" && (
                <div className="max-w-[85%] rounded-2xl rounded-ss-sm bg-white px-4 py-2 shadow-sm">
                  <p className="text-xs italic text-muted-foreground">
                    {isAr ? "اكتب ردك..." : "Type your reply..."}
                  </p>
                </div>
              )}

              {/* Warning */}
              {"whatsappWarning" in q && q.whatsappWarning && (
                <div className="flex items-start gap-2 bg-[#FFF0CC] border border-[#E8A020]/30 rounded-lg px-3 py-2 max-w-[90%]">
                  <AlertTriangle className="size-3.5 text-[#E8A020] shrink-0 mt-0.5" />
                  <p className="text-xs text-[#7A5000]">
                    {isAr ? q.whatsappWarning.ar : q.whatsappWarning.en}
                  </p>
                </div>
              )}
            </div>
          ))}
        </div>

        {/* Input bar */}
        <div className="flex items-center gap-2 px-3 py-2 border-t" style={{ backgroundColor: "#F0F2F5" }}>
          <div className="flex-1 bg-white rounded-full px-4 py-2 text-xs text-muted-foreground">
            {isAr ? "اكتب رسالة..." : "Type a message..."}
          </div>
          <div className="size-8 rounded-full flex items-center justify-center" style={{ backgroundColor: "#00A884" }}>
            <Send className="size-3.5 text-white" />
          </div>
        </div>
      </div>
    </div>
  )
}

function Send({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} fill="currentColor">
      <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z" />
    </svg>
  )
}

// ── Email preview ─────────────────────────────────────────────────────────────
function EmailPreview({ isAr, surveyName }: { isAr: boolean; surveyName: string }) {
  const firstQ = DEMO_QUESTIONS[0]

  return (
    <div className="rounded-lg border border-border overflow-hidden shadow-sm">
      {/* Email chrome */}
      <div className="bg-muted/80 border-b border-border p-4 space-y-2.5 text-sm">
        {[
          { label: isAr ? "من:" : "From:", value: "Nabadat CX <noreply@nabadat.cx>" },
          { label: isAr ? "إلى:" : "To:", value: "customer@example.com" },
          { label: isAr ? "الموضوع:" : "Subject:", value: surveyName, bold: true },
        ].map(({ label, value, bold }) => (
          <div key={label} className="flex items-center gap-3">
            <span className="text-muted-foreground w-20 shrink-0 text-xs">{label}</span>
            <span className={cn("text-foreground text-xs", bold && "font-semibold")}>{value}</span>
          </div>
        ))}
      </div>

      {/* Email body */}
      <div className="p-6" style={{ backgroundColor: "#f5f5f5" }}>
        <div className="max-w-lg mx-auto rounded-lg overflow-hidden shadow-sm">
          {/* Email header */}
          <div className="bg-gradient-to-r from-nb-mint to-nb-cyan p-8 text-center">
            <p className="text-white/80 text-xs uppercase tracking-widest mb-2">Nabadat CX</p>
            <h2 className="font-heading font-bold text-xl text-white mb-2" dir={isAr ? "rtl" : "ltr"}>
              {isAr ? "شاركنا رأيك" : "Share Your Feedback"}
            </h2>
            <p className="text-white/80 text-sm">
              {isAr ? "دقيقتان فقط تكفيان" : "Only 2 minutes of your time"}
            </p>
          </div>

          {/* First question embedded */}
          <div className="bg-white p-6 border-x border-b-0">
            <div className="flex items-center gap-1.5 mb-4">
              <div className="size-1.5 rounded-full bg-nb-cyan" />
              <p className="text-xs text-muted-foreground uppercase tracking-widest">
                {isAr ? "السؤال الأول" : "First Question"}
              </p>
            </div>
            <p className="text-base font-semibold text-foreground mb-4" dir={isAr ? "rtl" : "ltr"}>
              {isAr ? firstQ.titleAr : firstQ.titleEn}
            </p>

            {/* Embedded rating buttons */}
            <div className="flex justify-center gap-2">
              {[1,2,3,4,5].map((n) => (
                <div
                  key={n}
                  className="size-10 rounded-full border-2 flex items-center justify-center text-sm font-bold cursor-pointer hover:bg-primary hover:text-primary-foreground hover:border-primary transition-colors border-nb-cyan text-nb-cyan"
                >
                  {n}
                </div>
              ))}
            </div>
          </div>

          {/* Email warning for remaining questions */}
          <div className="bg-white px-6 pt-0 pb-4 border-x">
            <div className="flex items-start gap-2 mt-4 bg-nb-cyan-100/50 dark:bg-nb-cyan-900/20 rounded-md p-3">
              <AlertTriangle className="size-3.5 text-nb-cyan shrink-0 mt-0.5" />
              <p className="text-xs text-nb-cyan-800 dark:text-nb-cyan-300">
                {isAr
                  ? "النقر على أي تقييم أعلاه سيفتح بقية الاستبيان (سؤالان إضافيان) في متصفحك."
                  : "Tapping any rating above will open the remaining questions (2 more) in your browser."}
              </p>
            </div>
          </div>

          {/* Email footer */}
          <div className="bg-white border-x border-b border-border rounded-b-lg px-6 py-4 text-center space-y-2">
            <Button className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground text-sm px-6">
              {isAr ? "إكمال الاستبيان في المتصفح" : "Complete Survey in Browser"}
            </Button>
            <p className="text-xs text-muted-foreground">
              {isAr
                ? "إلغاء الاشتراك من رسائل الاستبيان"
                : "Unsubscribe from survey emails"}
            </p>
          </div>
        </div>
      </div>
    </div>
  )
}

// ── Page ──────────────────────────────────────────────────────────────────────
export default function SurveyPreviewPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { i18n } = useTranslation()
  const { isRtl } = useDirection()
  const isAr = i18n.language === "ar"
  const BackIcon = isRtl ? ArrowRight : ArrowLeft

  const survey = id ? MOCK_SURVEYS.find((s) => s.id === id) : null
  const surveyName = survey ? (isAr ? survey.nameAr : survey.nameEn) : (isAr ? "معاينة الاستبيان" : "Survey Preview")

  const [channel, setChannel] = useState<Channel>("mobile")
  const [locale, setLocale] = useState<"ar" | "en">("ar")

  const channels: { key: Channel; labelAr: string; labelEn: string; icon: React.ReactNode }[] = [
    { key: "mobile",   labelAr: "ويب موبايل",    labelEn: "Mobile Web",   icon: <Smartphone className="size-4" /> },
    { key: "desktop",  labelAr: "ويب ديسكتوب",   labelEn: "Desktop Web",  icon: <Monitor className="size-4" /> },
    { key: "whatsapp", labelAr: "واتساب",          labelEn: "WhatsApp",     icon: <MessageCircle className="size-4" /> },
    { key: "email",    labelAr: "البريد الإلكتروني", labelEn: "Email",     icon: <Mail className="size-4" /> },
  ]

  const previewIsAr = locale === "ar"

  return (
    <div className="flex flex-col h-[calc(100vh-48px)]">
      {/* Header */}
      <header className="sticky top-0 z-30 bg-background/95 backdrop-blur-sm border-b border-border px-4 h-14 flex items-center justify-between gap-4 shrink-0">
        <div className="flex items-center gap-3 min-w-0">
          <Button
            variant="ghost"
            size="icon"
            className="size-9 shrink-0"
            onClick={() => navigate(id ? `/surveys/${id}/edit` : "/surveys")}
            aria-label={isAr ? "العودة إلى المحرر" : "Back to builder"}
          >
            <BackIcon className="size-4" />
          </Button>
          <div className="min-w-0">
            <p className="text-xs text-muted-foreground">{isAr ? "معاينة متعددة القنوات" : "Multi-Channel Preview"}</p>
            <p className="text-sm font-semibold truncate">{surveyName}</p>
          </div>
        </div>

        <div className="flex items-center gap-2 shrink-0">
          {/* Locale switcher */}
          <div className="flex rounded-md border border-border overflow-hidden">
            {(["ar", "en"] as const).map((l) => (
              <button
                key={l}
                onClick={() => setLocale(l)}
                className={cn(
                  "px-3 py-1.5 text-xs font-medium transition-colors",
                  locale === l ? "bg-primary text-primary-foreground" : "text-muted-foreground hover:bg-muted"
                )}
              >
                {l === "ar" ? "العربية" : "English"}
              </button>
            ))}
          </div>

          <Button
            variant="secondary"
            size="sm"
            onClick={() => navigate(id ? `/surveys/${id}/edit` : "/surveys/new/builder")}
          >
            <Pencil className="size-4 me-1.5" />
            {isAr ? "تعديل الاستبيان" : "Edit Survey"}
          </Button>
        </div>
      </header>

      {/* Channel tabs */}
      <div className="border-b border-border bg-background px-4">
        <div className="flex gap-1">
          {channels.map((ch) => (
            <button
              key={ch.key}
              onClick={() => setChannel(ch.key)}
              className={cn(
                "flex items-center gap-2 px-4 py-2.5 text-sm font-medium border-b-2 transition-colors",
                channel === ch.key
                  ? "border-primary text-primary"
                  : "border-transparent text-muted-foreground hover:text-foreground"
              )}
            >
              {ch.icon}
              {isAr ? ch.labelAr : ch.labelEn}
            </button>
          ))}
        </div>
      </div>

      {/* Preview area */}
      <div className="flex-1 overflow-y-auto bg-muted/20 p-8">
        {channel === "mobile"   && <MobilePreview   isAr={previewIsAr} surveyName={surveyName} />}
        {channel === "desktop"  && <DesktopPreview  isAr={previewIsAr} surveyName={surveyName} />}
        {channel === "whatsapp" && <WhatsAppPreview isAr={previewIsAr} surveyName={surveyName} />}
        {channel === "email"    && <EmailPreview    isAr={previewIsAr} surveyName={surveyName} />}
      </div>
    </div>
  )
}
