import { useState, useRef, useEffect } from "react"
import { useTranslation } from "react-i18next"
import { MessageSquare, Send, X } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { cn } from "@/lib/utils"

interface ChatMessage {
  role: "user" | "ai"
  text: string
}

const CANNED = [
  {
    keywords: ["nps", "promoter", "detractor"],
    en: "NPS is at +42. Promoters make up 58% of respondents, while detractors account for 16%. The trend has improved by 3 points over the last quarter.",
    ar: "صافي المروجين عند 42+. يشكّل المروّجون 58% من المستجيبين، بينما يمثّل المنتقدون 16%. تحسّن الاتجاه بمقدار 3 نقاط خلال الربع الأخير.",
  },
  {
    keywords: ["csat", "satisfaction"],
    en: "CSAT is at 78%. The highest-rated touchpoint is post-purchase support (89%), while onboarding scores lowest at 64%.",
    ar: "رضا العملاء عند 78%. أعلى نقطة اتصال تقييمًا هي دعم ما بعد الشراء (89%)، بينما يسجّل الإعداد الأولي أدنى مستوى عند 64%.",
  },
  {
    keywords: ["ces", "effort"],
    en: "CES at 45%. Customers report the highest effort in the returns process. Simplifying the return form could reduce effort by ~20%.",
    ar: "جهد العميل عند 45%. يُبلّغ العملاء عن أعلى جهد في عملية الإرجاع. تبسيط نموذج الإرجاع قد يقلّل الجهد بنسبة 20% تقريبًا.",
  },
  {
    keywords: ["agent", "coaching"],
    en: "Agent score is 84%. Top performer: Sara (96%). Coaching opportunity: Ahmad (71%) — tone and empathy flagged in 3 recent calls.",
    ar: "أداء الوكيل 84%. الأفضل أداءً: سارة (96%). فرصة تدريب: أحمد (71%) — تم الإشارة إلى اللهجة والتعاطف في 3 مكالمات أخيرة.",
  },
  {
    keywords: ["fcr", "resolution"],
    en: "FCR at 68%. Billing issues have the lowest first-contact resolution (52%). Routing improvements could raise this to 70%+.",
    ar: "حل من أول تواصل 68%. مشاكل الفوترة لديها أدنى نسبة حل (52%). تحسين التوجيه قد يرفعها إلى 70%+.",
  },
  {
    keywords: ["vfm", "value", "money"],
    en: "VFM at 72%. Customers on premium plans rate value higher (81%) than basic plan users (63%). Price sensitivity increased 5% this quarter.",
    ar: "القيمة مقابل المال 72%. عملاء الخطط المميزة يقيّمون القيمة أعلى (81%) من مستخدمي الخطة الأساسية (63%). ارتفعت حساسية السعر 5% هذا الربع.",
  },
  {
    keywords: ["branch", "riyadh"],
    en: "Top branch: Riyadh North with a CX score of 91/100. Lowest: Jeddah South at 67/100. Staff training is the key differentiator.",
    ar: "أفضل فرع: الرياض الشمالي بمؤشر تجربة عملاء 91/100. الأدنى: جدة الجنوبية عند 67/100. تدريب الموظفين هو العامل الفارق.",
  },
  {
    keywords: ["cxi", "index"],
    en: "CXI is at 69/100. This is a 4-point improvement from last month. Key drivers: faster response times and improved self-service options.",
    ar: "مؤشر CXI عند 69/100. هذا تحسّن بمقدار 4 نقاط عن الشهر الماضي. المحركات الرئيسية: أوقات استجابة أسرع وخيارات خدمة ذاتية محسّنة.",
  },
  {
    keywords: ["action", "ivr"],
    en: "2 tracked actions are in progress: IVR menu simplification (70% complete) and callback feature pilot (started last week).",
    ar: "إجراءان قيد التتبع: تبسيط قائمة الرد الآلي (مكتمل 70%) وتجربة ميزة معاودة الاتصال (بدأت الأسبوع الماضي).",
  },
]

interface AiChatPanelProps {
  open: boolean
  onClose: () => void
}

function AiChatPanel({ open, onClose }: AiChatPanelProps) {
  const { t, i18n } = useTranslation()
  const messagesEndRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  const [chatMessages, setChatMessages] = useState<ChatMessage[]>([
    { role: "ai", text: t("cx.chatWelcome") },
  ])
  const [chatInput, setChatInput] = useState("")

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" })
  }, [chatMessages])

  useEffect(() => {
    if (open) {
      inputRef.current?.focus()
    }
  }, [open])

  function handleSend() {
    const trimmed = chatInput.trim()
    if (!trimmed) return

    const lower = trimmed.toLowerCase()
    const match = CANNED.find((c) =>
      c.keywords.some((kw) => lower.includes(kw))
    )

    const aiText = match
      ? i18n.language === "ar"
        ? match.ar
        : match.en
      : t("cx.chatDefault")

    setChatMessages((prev) => [
      ...prev,
      { role: "user", text: trimmed },
      { role: "ai", text: aiText },
    ])
    setChatInput("")
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === "Enter") {
      e.preventDefault()
      handleSend()
    }
  }

  if (!open) return null

  return (
    <div
      className={cn(
        "fixed bottom-6 end-6 z-50",
        "w-[min(20rem,calc(100vw-2rem))] h-[min(420px,80vh)]",
        "rounded-lg shadow-xl border border-border",
        "bg-background overflow-hidden flex flex-col",
        "motion-safe:animate-in motion-safe:fade-in motion-safe:slide-in-from-bottom-4 motion-safe:duration-200"
      )}
    >
      {/* Header */}
      <div className="bg-secondary text-secondary-foreground p-3 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <MessageSquare className="size-4" />
          <span className="text-sm font-bold">{t("cx.aiAssistant")}</span>
        </div>
        <Button
          size="icon"
          variant="ghost"
          className="text-secondary-foreground/80 hover:text-secondary-foreground hover:bg-secondary/80 size-7"
          onClick={onClose}
          aria-label={t("common.close")}
        >
          <X className="size-4" />
        </Button>
      </div>

      {/* Messages Area */}
      <div className="flex-1 overflow-y-auto p-3 flex flex-col gap-3">
        {chatMessages.map((msg, idx) => (
          <div
            key={idx}
            className={cn(
              "max-w-[85%] p-3 text-sm leading-relaxed",
              msg.role === "user"
                ? "ms-auto bg-secondary text-secondary-foreground rounded-lg rounded-ee-sm"
                : "me-auto bg-muted rounded-lg rounded-es-sm",
            )}
          >
            {msg.text}
          </div>
        ))}
        <div ref={messagesEndRef} />
      </div>

      {/* Input Row */}
      <div className="border-t border-border p-3 flex gap-2">
        <Input
          ref={inputRef}
          className="rounded-full flex-1"
          placeholder={t("cx.chatPlaceholder") || ""}
          value={chatInput}
          onChange={(e) => setChatInput(e.target.value)}
          onKeyDown={handleKeyDown}
        />
        <Button
          size="icon"
          className="rounded-full bg-primary hover:bg-nb-cyan-700 text-primary-foreground shrink-0"
          onClick={handleSend}
          aria-label={t("common.submit")}
        >
          <Send className="size-4" />
        </Button>
      </div>
    </div>
  )
}

export { AiChatPanel }
