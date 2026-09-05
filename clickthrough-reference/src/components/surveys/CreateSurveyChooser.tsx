import { useNavigate } from "react-router"
import { PenLine, LayoutTemplate, Sparkles, ChevronLeft, ChevronRight } from "lucide-react"
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"
import { useTranslation } from "react-i18next"
import { useDirection } from "@/hooks/use-direction"

interface CreateSurveyChooserProps {
  open: boolean
  onOpenChange: (open: boolean) => void
}

const OPTIONS = [
  {
    id: "scratch",
    icon: PenLine,
    titleAr: "من الصفر",
    titleEn: "From Scratch",
    descAr: "ابدأ باستبيان فارغ وأضف أسئلتك بنفسك",
    descEn: "Start with a blank survey and build your questions manually",
    iconBg: "bg-nb-cyan-100 dark:bg-nb-cyan-900/40",
    iconColor: "text-nb-cyan",
    route: "/surveys/new/builder",
  },
  {
    id: "template",
    icon: LayoutTemplate,
    titleAr: "من قالب",
    titleEn: "From Template",
    descAr: "اختر من مكتبة القوالب المُعدّة مسبقاً حسب القطاع",
    descEn: "Choose from our library of pre-built templates by sector",
    iconBg: "bg-nb-mint-100 dark:bg-nb-mint-900/40",
    iconColor: "text-nb-mint-700",
    route: "/surveys/templates/pick",
  },
  {
    id: "ai",
    icon: Sparkles,
    titleAr: "أنشئ بالذكاء الاصطناعي",
    titleEn: "Build with AI",
    descAr: "صف هدف استبيانك وسيقترح الذكاء الاصطناعي أسئلة مناسبة",
    descEn: "Describe your survey goal and AI will generate relevant questions",
    iconBg: "bg-nb-navy-100 dark:bg-nb-navy-700/40",
    iconColor: "text-nb-navy-300 dark:text-nb-navy-100",
    route: "/surveys/new/ai",
  },
]

export function CreateSurveyChooser({ open, onOpenChange }: CreateSurveyChooserProps) {
  const navigate = useNavigate()
  const { i18n } = useTranslation()
  const { isRtl } = useDirection()
  const isAr = i18n.language === "ar"

  const ChevronIcon = isRtl ? ChevronLeft : ChevronRight

  function handleSelect(route: string) {
    onOpenChange(false)
    navigate(route)
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle className="text-lg font-bold">
            {isAr ? "إنشاء استبيان جديد" : "Create New Survey"}
          </DialogTitle>
        </DialogHeader>

        <div className="flex flex-col gap-2 pt-2">
          {OPTIONS.map((opt) => {
            const Icon = opt.icon
            return (
              <button
                key={opt.id}
                onClick={() => handleSelect(opt.route)}
                className={cn(
                  "flex items-center gap-4 rounded-lg border border-border p-4 text-start",
                  "hover:bg-accent hover:border-primary/30 transition-colors duration-150",
                  "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2",
                  "group"
                )}
              >
                <div
                  className={cn(
                    "flex size-11 shrink-0 items-center justify-center rounded-md",
                    opt.iconBg
                  )}
                >
                  <Icon className={cn("size-5", opt.iconColor)} />
                </div>

                <div className="flex-1 min-w-0">
                  <p className="font-semibold text-foreground text-sm">
                    {isAr ? opt.titleAr : opt.titleEn}
                  </p>
                  <p className="text-xs text-muted-foreground mt-0.5 leading-relaxed">
                    {isAr ? opt.descAr : opt.descEn}
                  </p>
                </div>

                <ChevronIcon className="size-4 text-muted-foreground shrink-0 group-hover:text-primary transition-colors" />
              </button>
            )
          })}
        </div>

        <div className="pt-2">
          <Button
            variant="ghost"
            className="w-full text-muted-foreground"
            onClick={() => onOpenChange(false)}
          >
            {isAr ? "إلغاء" : "Cancel"}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  )
}
