import { useState, useRef, useEffect } from "react"
import { useNavigate } from "react-router"
import { ArrowRight, ArrowLeft, Send, Sparkles, RefreshCw, ArrowUpRight } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Textarea } from "@/components/ui/textarea"
import { Badge } from "@/components/ui/badge"
import { useTranslation } from "react-i18next"
import { useDirection } from "@/hooks/use-direction"
import { cn } from "@/lib/utils"

// ── Types ─────────────────────────────────────────────────────────────────────
type Stage = "goal" | "requirements" | "generating" | "done"

interface ChatMessage {
  id: string
  role: "ai" | "user"
  text: string
  isTyping?: boolean
}

// ── Generated survey mock (shown in preview panel after generation) ───────────
const GENERATED_QUESTIONS = [
  {
    id: "gq1",
    type: "kpi",
    labelAr: "سؤال NPS",
    labelEn: "NPS Question",
    titleAr: "كيف تقيّم احتمالية توصيتك بخدماتنا لأصدقائك وزملائك؟",
    titleEn: "How likely are you to recommend our services to your friends and colleagues?",
    subtitleAr: "على مقياس من 0 (غير محتمل تماماً) إلى 10 (محتمل للغاية)",
    subtitleEn: "On a scale from 0 (not at all likely) to 10 (extremely likely)",
  },
  {
    id: "gq2",
    type: "radio",
    labelAr: "سؤال متعدد",
    labelEn: "Multiple Choice",
    titleAr: "ما السبب الرئيسي لتقييمك؟",
    titleEn: "What is the main reason for your score?",
    optionsAr: ["جودة الخدمة ممتازة", "سرعة الاستجابة مناسبة", "سهولة الاستخدام", "سعر مناسب", "آخر"],
    optionsEn: ["Excellent service quality", "Good response speed", "Ease of use", "Fair pricing", "Other"],
  },
  {
    id: "gq3",
    type: "text",
    labelAr: "نص حر",
    labelEn: "Open Text",
    titleAr: "هل لديك أي ملاحظات أو اقتراحات إضافية؟",
    titleEn: "Do you have any additional comments or suggestions?",
    subtitleAr: "اختياري",
    subtitleEn: "Optional",
  },
]

// ── Typing indicator ──────────────────────────────────────────────────────────
function TypingIndicator() {
  return (
    <div className="flex items-center gap-1.5 px-1 py-1">
      {[0, 150, 300].map((delay, i) => (
        <div
          key={i}
          className="size-2 rounded-full bg-muted-foreground animate-bounce"
          style={{ animationDelay: `${delay}ms` }}
        />
      ))}
    </div>
  )
}

// ── Chat bubble ───────────────────────────────────────────────────────────────
function ChatBubble({ message }: { message: ChatMessage }) {
  const isUser = message.role === "user"

  return (
    <div className={cn("flex items-end gap-2", isUser ? "flex-row-reverse" : "flex-row")}>
      {/* Avatar */}
      {!isUser && (
        <div className="size-7 rounded-full bg-gradient-to-br from-nb-mint to-nb-cyan flex items-center justify-center shrink-0 mb-0.5">
          <Sparkles className="size-3.5 text-white" />
        </div>
      )}

      {/* Bubble */}
      <div
        className={cn(
          "max-w-[80%] rounded-2xl px-4 py-3 text-sm leading-relaxed",
          isUser
            ? "bg-primary text-primary-foreground rounded-ee-sm"
            : "bg-card border border-border text-foreground rounded-es-sm"
        )}
      >
        {message.isTyping ? <TypingIndicator /> : message.text}
      </div>
    </div>
  )
}

// ── Generated survey preview ──────────────────────────────────────────────────
function GeneratedPreview({ isAr }: { isAr: boolean }) {
  return (
    <div className="space-y-3">
      {GENERATED_QUESTIONS.map((q, idx) => (
        <div key={q.id} className="rounded-lg border border-border bg-card p-4">
          <div className="flex items-center gap-2 mb-2">
            <span className="text-xs font-bold text-muted-foreground tabular-nums">{idx + 1}.</span>
            <Badge
              className={cn(
                "text-xs border-transparent",
                q.type === "kpi"
                  ? "bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200"
                  : "bg-muted text-muted-foreground"
              )}
            >
              {isAr ? q.labelAr : q.labelEn}
            </Badge>
          </div>
          <p className="text-sm font-medium text-foreground leading-snug">
            {isAr ? q.titleAr : q.titleEn}
          </p>
          {(q.subtitleAr || q.subtitleEn) && (
            <p className="text-xs text-muted-foreground mt-0.5">
              {isAr ? q.subtitleAr : q.subtitleEn}
            </p>
          )}
          {q.optionsAr && (
            <div className="mt-2 space-y-1">
              {(isAr ? q.optionsAr : q.optionsEn!).map((opt) => (
                <div key={opt} className="flex items-center gap-2 text-xs text-muted-foreground">
                  <div className="size-3 rounded-full border border-border shrink-0" />
                  {opt}
                </div>
              ))}
            </div>
          )}
        </div>
      ))}
    </div>
  )
}

// ── Page ──────────────────────────────────────────────────────────────────────
export default function AiSurveyPage() {
  const navigate = useNavigate()
  const { i18n } = useTranslation()
  const { isRtl } = useDirection()
  const isAr = i18n.language === "ar"
  const BackIcon = isRtl ? ArrowRight : ArrowLeft
  const messagesEndRef = useRef<HTMLDivElement>(null)

  const [stage, setStage] = useState<Stage>("goal")
  const [input, setInput] = useState("")
  const [messages, setMessages] = useState<ChatMessage[]>([
    {
      id: "ai-0",
      role: "ai",
      text: isAr
        ? "مرحباً! أنا هنا لمساعدتك في إنشاء استبيان احترافي. ما هو الهدف الرئيسي من الاستبيان الذي تريد إنشاءه؟"
        : "Hello! I'm here to help you create a professional survey. What is the main goal of the survey you want to create?",
    },
  ])

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" })
  }, [messages])

  function addMessage(role: ChatMessage["role"], text: string, isTyping = false) {
    const id = `${role}-${Date.now()}`
    setMessages((prev) => [...prev, { id, role, text, isTyping }])
    return id
  }

  function replaceTyping(id: string, text: string) {
    setMessages((prev) =>
      prev.map((m) => (m.id === id ? { ...m, text, isTyping: false } : m))
    )
  }

  function handleSend() {
    const text = input.trim()
    if (!text) return
    setInput("")
    addMessage("user", text)

    if (stage === "goal") {
      const typingId = addMessage("ai", "", true)
      setStage("requirements")
      setTimeout(() => {
        replaceTyping(
          typingId,
          isAr
            ? "ممتاز! فهمت هدف استبيانك. هل هناك متطلبات خاصة تريد مراعاتها؟ مثلاً: عدد الأسئلة، نوع معين من الأسئلة، أو تركيز على جانب محدد. أو اضغط على \"إنشاء الاستبيان الآن\" للمتابعة مباشرةً."
            : "Excellent! I understand your survey goal. Are there any specific requirements you'd like me to consider? For example: number of questions, specific question types, or focus on a particular aspect. Or press \"Generate Survey Now\" to proceed directly."
        )
      }, 1500)
    } else if (stage === "requirements") {
      triggerGeneration()
    }
  }

  function triggerGeneration() {
    const typingId = addMessage("ai", "", true)
    setStage("generating")
    setTimeout(() => {
      replaceTyping(
        typingId,
        isAr
          ? "تمام! جاري الآن تحليل متطلباتك وإنشاء الاستبيان المناسب..."
          : "Perfect! Now analyzing your requirements and generating the appropriate survey..."
      )
    }, 800)
    setTimeout(() => {
      setStage("done")
      addMessage(
        "ai",
        isAr
          ? "أنشأت لك استبيانًا من 3 أسئلة يركّز على قياس NPS وجمع التغذية الراجعة. يمكنك الآن مراجعته في اللوحة الجانبية وتعديله بالكامل في المحرر."
          : "I've created a 3-question survey focused on measuring NPS and gathering feedback. You can now review it in the side panel and edit it fully in the builder."
      )
    }, 3200)
  }

  function handleKeyDown(e: React.KeyboardEvent) {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault()
      handleSend()
    }
  }

  const isInputDisabled = stage === "generating"
  const showGenerateNow = stage === "requirements" && !isInputDisabled

  return (
    <div className="flex flex-col h-[calc(100vh-48px)]">
      {/* Header */}
      <header className="sticky top-0 z-30 bg-background/95 backdrop-blur-sm border-b border-border px-4 h-14 flex items-center justify-between gap-4 shrink-0">
        <div className="flex items-center gap-3">
          <Button
            variant="ghost"
            size="icon"
            className="size-9"
            onClick={() => navigate("/surveys")}
            aria-label={isAr ? "العودة" : "Back"}
          >
            <BackIcon className="size-4" />
          </Button>
          <div className="flex items-center gap-2">
            <div className="size-6 rounded-full bg-gradient-to-br from-nb-mint to-nb-cyan flex items-center justify-center">
              <Sparkles className="size-3 text-white" />
            </div>
            <span className="text-sm font-semibold">
              {isAr ? "إنشاء استبيان بالذكاء الاصطناعي" : "AI Survey Builder"}
            </span>
            <Badge className="text-xs bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200 border-transparent">
              {isAr ? "تجريبي" : "Beta"}
            </Badge>
          </div>
        </div>

        {stage === "done" && (
          <Button
            className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            size="sm"
            onClick={() => navigate("/surveys/new/builder")}
          >
            <ArrowUpRight className="size-4 me-1.5" />
            {isAr ? "المتابعة إلى المحرر" : "Continue to Builder"}
          </Button>
        )}
      </header>

      {/* Body: chat + preview */}
      <div className="flex flex-1 overflow-hidden">
        {/* ── Chat panel ─────────────────────────────────────── */}
        <div className="flex flex-col w-[55%] border-e border-border">
          {/* Messages */}
          <div className="flex-1 overflow-y-auto p-6 space-y-4">
            {/* Tenant context hint */}
            <div className="rounded-md bg-nb-cyan-100/40 dark:bg-nb-cyan-900/15 border border-nb-cyan/20 p-3 text-xs text-nb-cyan-800 dark:text-nb-cyan-300 leading-relaxed">
              {isAr
                ? "تم تحميل سياق مؤسستك (القطاع: المصرفي — اللغة الافتراضية: العربية). الاستبيان المُنشأ سيتوافق مع متطلبات مؤسستك تلقائياً."
                : "Your organization context has been loaded (Sector: Banking — Default language: Arabic). The generated survey will automatically align with your organization's requirements."}
            </div>

            {messages.map((msg) => (
              <ChatBubble key={msg.id} message={msg} />
            ))}

            {stage === "generating" && (
              <div className="flex items-center gap-2 text-xs text-muted-foreground ps-9">
                <div className="size-1.5 rounded-full bg-nb-cyan animate-pulse" />
                {isAr ? "جاري التحليل والإنشاء..." : "Analyzing and generating..."}
              </div>
            )}

            <div ref={messagesEndRef} />
          </div>

          {/* Input area */}
          <div className="border-t border-border p-4 space-y-3">
            {showGenerateNow && (
              <button
                onClick={triggerGeneration}
                className="w-full flex items-center justify-center gap-2 rounded-md border border-dashed border-primary/40 py-2 text-sm font-medium text-primary hover:bg-primary/5 transition-colors"
              >
                <Sparkles className="size-4" />
                {isAr ? "إنشاء الاستبيان الآن" : "Generate Survey Now"}
              </button>
            )}
            <div className="flex items-end gap-2">
              <Textarea
                value={input}
                onChange={(e) => setInput(e.target.value)}
                onKeyDown={handleKeyDown}
                placeholder={
                  stage === "goal"
                    ? (isAr ? "صِف هدف الاستبيان..." : "Describe the survey goal...")
                    : stage === "requirements"
                    ? (isAr ? "أضف متطلبات خاصة (اختياري)..." : "Add specific requirements (optional)...")
                    : (isAr ? "جاري الإنشاء..." : "Generating...")
                }
                disabled={isInputDisabled}
                className="flex-1 min-h-[56px] max-h-32 resize-none"
                dir={isAr ? "rtl" : "ltr"}
                rows={2}
              />
              <Button
                size="icon"
                className="size-10 bg-primary hover:bg-nb-cyan-700 text-primary-foreground shrink-0"
                onClick={handleSend}
                disabled={isInputDisabled || !input.trim()}
                aria-label={isAr ? "إرسال" : "Send"}
              >
                <Send className="size-4" />
              </Button>
            </div>
            <p className="text-xs text-muted-foreground text-center">
              {isAr
                ? "الاستبيان المُنشأ بحالة مسودة — يتطلب مراجعة بشرية قبل التفعيل"
                : "Generated survey is in Draft — requires human review before activation"}
            </p>
          </div>
        </div>

        {/* ── Preview panel ──────────────────────────────────── */}
        <div className="flex-1 overflow-y-auto bg-muted/20">
          {stage === "done" ? (
            <div className="p-6 space-y-4">
              {/* Preview header */}
              <div className="flex items-center justify-between">
                <div>
                  <h2 className="text-base font-bold">
                    {isAr ? "الاستبيان المُنشأ" : "Generated Survey"}
                  </h2>
                  <p className="text-xs text-muted-foreground mt-0.5">
                    {isAr ? "3 أسئلة — مسودة" : "3 questions — Draft"}
                  </p>
                </div>
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => {
                    setStage("goal")
                    setMessages([messages[0]])
                    setInput("")
                  }}
                >
                  <RefreshCw className="size-3.5 me-1.5" />
                  {isAr ? "إعادة الإنشاء" : "Regenerate"}
                </Button>
              </div>

              <GeneratedPreview isAr={isAr} />

              <Button
                className="w-full bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
                onClick={() => navigate("/surveys/new/builder")}
              >
                <ArrowUpRight className="size-4 me-1.5" />
                {isAr ? "فتح في المحرر للتعديل" : "Open in Builder to Edit"}
              </Button>
            </div>
          ) : stage === "generating" ? (
            <div className="flex flex-col items-center justify-center h-full gap-4 text-center p-8">
              <div className="size-16 rounded-full bg-gradient-to-br from-nb-mint/20 to-nb-cyan/20 flex items-center justify-center">
                <Sparkles className="size-7 text-nb-cyan animate-pulse" />
              </div>
              <div>
                <p className="text-base font-semibold mb-1">
                  {isAr ? "جاري إنشاء الاستبيان..." : "Generating your survey..."}
                </p>
                <p className="text-sm text-muted-foreground">
                  {isAr
                    ? "يتم تحليل متطلباتك وبناء الأسئلة المناسبة"
                    : "Analyzing requirements and building appropriate questions"}
                </p>
              </div>
              <div className="flex gap-1.5">
                {[0, 200, 400].map((delay, i) => (
                  <div
                    key={i}
                    className="size-2.5 rounded-full bg-nb-cyan animate-bounce"
                    style={{ animationDelay: `${delay}ms` }}
                  />
                ))}
              </div>
            </div>
          ) : (
            <div className="flex flex-col items-center justify-center h-full gap-4 text-center p-8 text-muted-foreground">
              <div className="size-16 rounded-full bg-muted/50 flex items-center justify-center">
                <Sparkles className="size-7 text-muted-foreground/40" />
              </div>
              <div>
                <p className="text-base font-medium mb-1">
                  {isAr ? "معاينة الاستبيان" : "Survey Preview"}
                </p>
                <p className="text-sm">
                  {isAr
                    ? "ستظهر هنا بنية الاستبيان المُنشأ بعد اكتمال التوليد"
                    : "The generated survey structure will appear here after generation completes"}
                </p>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
