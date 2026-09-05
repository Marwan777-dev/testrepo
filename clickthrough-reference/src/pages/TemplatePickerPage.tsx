import { useState } from "react"
import { useNavigate } from "react-router"
import { ArrowRight, ArrowLeft, Search, LayoutTemplate, Lock } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Badge } from "@/components/ui/badge"
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select"
import { useTranslation } from "react-i18next"
import { useDirection } from "@/hooks/use-direction"
import { MOCK_TEMPLATES } from "@/data/mock-surveys"
import { INDUSTRY_OPTIONS } from "@/types/survey"
import type { TemplateClass } from "@/types/survey"
import { cn } from "@/lib/utils"

const SECTOR_LABELS: Record<string, { ar: string; en: string }> = {
  Banking:            { ar: "البنوك", en: "Banking" },
  Telecommunications: { ar: "الاتصالات", en: "Telecom" },
  Government:         { ar: "الحكومة", en: "Government" },
  Automotive:         { ar: "السيارات", en: "Automotive" },
  Entertainment:      { ar: "الترفيه", en: "Entertainment" },
  Services:           { ar: "الخدمات", en: "Services" },
}

export default function TemplatePickerPage() {
  const navigate = useNavigate()
  const { i18n } = useTranslation()
  const { isRtl } = useDirection()
  const isAr = i18n.language === "ar"
  const BackIcon = isRtl ? ArrowRight : ArrowLeft

  const [search, setSearch] = useState("")
  // Default to tenant industry (Banking per mock M-11 setting)
  const [sector, setSector] = useState("Banking")
  const [templateClass, setTemplateClass] = useState<"all" | TemplateClass>("all")

  const filtered = MOCK_TEMPLATES.filter((t) => {
    const name = isAr ? t.nameAr : t.nameEn
    if (search && !name.toLowerCase().includes(search.toLowerCase())) return false
    if (sector !== "all" && !t.sectors.includes(sector)) return false
    if (templateClass !== "all" && t.templateClass !== templateClass) return false
    return true
  })

  return (
    <div className="space-y-6 py-5 px-8">
      {/* Header */}
      <div className="flex items-start gap-4">
        <Button
          variant="ghost"
          size="icon"
          className="size-9 mt-0.5 shrink-0"
          onClick={() => navigate("/surveys")}
          aria-label={isAr ? "العودة" : "Back"}
        >
          <BackIcon className="size-4" />
        </Button>
        <div>
          <h1 className="text-2xl font-heading font-bold">
            {isAr ? "اختر قالباً" : "Choose a Template"}
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            {isAr
              ? "ابدأ من قالب جاهز — يمكنك تعديله بالكامل بعد اختياره"
              : "Start from a ready-made template — fully editable after selection"}
          </p>
        </div>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-3">
        <div className="relative flex-1 min-w-52">
          <Search className="absolute start-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground pointer-events-none" />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={isAr ? "ابحث عن قالب..." : "Search templates..."}
            className="ps-9"
          />
        </div>
        <Select value={sector} onValueChange={(v) => setSector(v ?? "all")}>
          <SelectTrigger className="w-44">
            <SelectValue placeholder={isAr ? "القطاع" : "Sector"} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{isAr ? "جميع القطاعات" : "All Sectors"}</SelectItem>
            {INDUSTRY_OPTIONS.map((o) => (
              <SelectItem key={o.value} value={o.value}>
                {isAr ? o.labelAr : o.labelEn}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select
          value={templateClass}
          onValueChange={(v) => setTemplateClass((v ?? "all") as "all" | TemplateClass)}
        >
          <SelectTrigger className="w-44">
            <SelectValue placeholder={isAr ? "الفئة" : "Class"} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{isAr ? "جميع الفئات" : "All Classes"}</SelectItem>
            <SelectItem value="Platform">{isAr ? "قوالب المنصة" : "Platform"}</SelectItem>
            <SelectItem value="Tenant">{isAr ? "قوالب مخصصة" : "Tenant"}</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* Results count */}
      <p className="text-sm text-muted-foreground">
        {isAr
          ? `${filtered.length} قالب متاح`
          : `${filtered.length} template${filtered.length !== 1 ? "s" : ""} available`}
      </p>

      {/* Template grid */}
      {filtered.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-20 text-center">
          <LayoutTemplate className="size-12 text-muted-foreground mb-4" />
          <h3 className="text-lg font-bold mb-2">
            {isAr ? "لا توجد قوالب" : "No templates found"}
          </h3>
          <p className="text-sm text-muted-foreground max-w-sm">
            {isAr
              ? "جرّب تعديل معايير البحث أو اختر قطاعاً مختلفاً"
              : "Try adjusting your filters or select a different sector"}
          </p>
          <Button
            variant="secondary"
            className="mt-4"
            onClick={() => { setSector("all"); setSearch("") }}
          >
            {isAr ? "إظهار جميع القوالب" : "Show all templates"}
          </Button>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {filtered.map((t) => {
            const name = isAr ? t.nameAr : t.nameEn
            const isPlatform = t.templateClass === "Platform"

            return (
              <button
                key={t.id}
                onClick={() => navigate(`/surveys/new/from-template/${t.id}`)}
                className="group text-start rounded-lg border border-border bg-card p-5 hover:border-primary/60 hover:shadow-md hover:shadow-primary/5 transition-all"
              >
                {/* Top row: icon + class badge */}
                <div className="flex items-start justify-between gap-2 mb-4">
                  <div className="p-2.5 rounded-md bg-nb-cyan-100/60 dark:bg-nb-cyan-900/20">
                    <LayoutTemplate className="size-5 text-nb-cyan" />
                  </div>
                  <div className="flex items-center gap-1.5">
                    {isPlatform && (
                      <Lock className="size-3 text-muted-foreground" />
                    )}
                    <Badge
                      className={cn(
                        "text-xs border-transparent",
                        isPlatform
                          ? "bg-nb-navy-100 text-nb-navy-800 dark:bg-nb-navy-700/40 dark:text-nb-navy-100"
                          : "bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200"
                      )}
                    >
                      {isPlatform ? (isAr ? "منصة" : "Platform") : (isAr ? "مخصص" : "Tenant")}
                    </Badge>
                  </div>
                </div>

                {/* Template name */}
                <h3 className="text-base font-semibold text-foreground group-hover:text-primary transition-colors mb-1.5 leading-snug">
                  {name}
                </h3>

                {/* Meta */}
                <div className="flex items-center gap-3 text-xs text-muted-foreground mb-3">
                  <span>
                    {t.questionCount} {isAr ? "سؤال" : "questions"}
                  </span>
                  <span className="text-border">•</span>
                  <span>
                    {isAr
                      ? `يستخدمه ${t.usedBySurveys} استبيان`
                      : `Used by ${t.usedBySurveys} survey${t.usedBySurveys !== 1 ? "s" : ""}`}
                  </span>
                </div>

                {/* Sector tags */}
                <div className="flex flex-wrap gap-1.5">
                  {t.sectors.map((s) => (
                    <span
                      key={s}
                      className={cn(
                        "text-xs px-2 py-0.5 rounded-full",
                        s === sector && sector !== "all"
                          ? "bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200"
                          : "bg-muted text-muted-foreground"
                      )}
                    >
                      {isAr ? SECTOR_LABELS[s]?.ar ?? s : SECTOR_LABELS[s]?.en ?? s}
                    </span>
                  ))}
                </div>

                {/* Hover CTA */}
                <div className="mt-4 pt-3 border-t border-border/50 opacity-0 group-hover:opacity-100 transition-opacity">
                  <span className="text-xs font-semibold text-primary">
                    {isAr ? "ابدأ من هذا القالب ←" : "Start from this template →"}
                  </span>
                </div>
              </button>
            )
          })}
        </div>
      )}
    </div>
  )
}
