import { useState } from "react"
import { useNavigate } from "react-router"
import {
  Plus, Search, Pencil, Eye, MoreHorizontal, Inbox, FileText,
  Lock, LayoutTemplate, Trash2, Copy, Archive, BarChart2,
} from "lucide-react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select"
import { Badge } from "@/components/ui/badge"
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs"
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from "@/components/ui/table"
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem,
  DropdownMenuSeparator, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import {
  Tooltip, TooltipContent, TooltipProvider, TooltipTrigger,
} from "@/components/ui/tooltip"
import { useTranslation } from "react-i18next"
import { CreateSurveyChooser } from "@/components/surveys/CreateSurveyChooser"
import { MOCK_SURVEYS, MOCK_TEMPLATES } from "@/data/mock-surveys"
import type { SurveyTemplate, SurveyStatus, SurveyType, TemplateClass } from "@/types/survey"
import { cn } from "@/lib/utils"

// ── Status badge ──────────────────────────────────────────────────────────────
const STATUS_CONFIG: Record<SurveyStatus, { labelAr: string; labelEn: string; className: string }> = {
  Draft:    { labelAr: "مسودة",    labelEn: "Draft",    className: "bg-muted text-muted-foreground border-transparent" },
  Active:   { labelAr: "نشط",     labelEn: "Active",   className: "bg-[#C8F5DB] text-[#156632] dark:bg-[#156632]/20 dark:text-[#C8F5DB] border-transparent" },
  Paused:   { labelAr: "موقوف",   labelEn: "Paused",   className: "bg-[#FFF0CC] text-[#7A5000] dark:bg-[#7A5000]/20 dark:text-[#FFF0CC] border-transparent" },
  Archived: { labelAr: "مؤرشف",   labelEn: "Archived", className: "bg-muted/60 text-muted-foreground border-transparent" },
}

function StatusBadge({ status, isAr }: { status: SurveyStatus; isAr: boolean }) {
  const cfg = STATUS_CONFIG[status]
  return (
    <Badge className={cn("text-xs font-medium", cfg.className)}>
      {isAr ? cfg.labelAr : cfg.labelEn}
    </Badge>
  )
}

// ── Type badge ────────────────────────────────────────────────────────────────
function TypeBadge({ type, isAr }: { type: SurveyType; isAr: boolean }) {
  return (
    <Badge
      className={cn(
        "text-xs font-medium border-transparent",
        type === "Transactional"
          ? "bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200"
          : "bg-nb-mint-100 text-nb-mint-800 dark:bg-nb-mint-900/40 dark:text-nb-mint-200"
      )}
    >
      {isAr
        ? type === "Transactional" ? "تشغيلي" : "دوري / علائقي"
        : type === "Transactional" ? "Transactional" : "Relational"}
    </Badge>
  )
}

// ── Template class badge ───────────────────────────────────────────────────────
function ClassBadge({ cls, isAr }: { cls: TemplateClass; isAr: boolean }) {
  return (
    <Badge
      className={cn(
        "text-xs font-medium border-transparent",
        cls === "Platform"
          ? "bg-nb-navy-100 text-nb-navy-800 dark:bg-nb-navy-700/40 dark:text-nb-navy-100"
          : "bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200"
      )}
    >
      {cls === "Platform"
        ? (isAr ? "منصة" : "Platform")
        : (isAr ? "مخصص" : "Tenant")}
    </Badge>
  )
}

const SECTOR_LABELS: Record<string, { ar: string; en: string }> = {
  Banking:         { ar: "البنوك", en: "Banking" },
  Telecommunications: { ar: "الاتصالات", en: "Telecom" },
  Government:      { ar: "الحكومة", en: "Government" },
  Automotive:      { ar: "السيارات", en: "Automotive" },
  Entertainment:   { ar: "الترفيه", en: "Entertainment" },
  Services:        { ar: "الخدمات", en: "Services" },
}

// ── Surveys tab ───────────────────────────────────────────────────────────────
function SurveysTab() {
  const navigate = useNavigate()
  const { i18n } = useTranslation()
  const isAr = i18n.language === "ar"

  const [search, setSearch] = useState("")
  const [typeFilter, setTypeFilter] = useState("all")
  const [statusFilter, setStatusFilter] = useState("all")
  const [journeyFilter, setJourneyFilter] = useState("all")

  const journeyOptions = Array.from(
    new Set(MOCK_SURVEYS.filter((s) => s.journeyId).map((s) => s.journeyId!))
  ).map((id) => {
    const s = MOCK_SURVEYS.find((x) => x.journeyId === id)!
    return { id, labelAr: s.journeyNameAr!, labelEn: s.journeyNameEn! }
  })

  const filtered = MOCK_SURVEYS.filter((s) => {
    const name = isAr ? s.nameAr : s.nameEn
    if (search && !name.toLowerCase().includes(search.toLowerCase())) return false
    if (typeFilter !== "all" && s.type !== typeFilter) return false
    if (statusFilter !== "all" && s.status !== statusFilter) return false
    if (journeyFilter !== "all" && s.journeyId !== journeyFilter) return false
    return true
  })

  if (MOCK_SURVEYS.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-20 text-center">
        <Inbox className="size-12 text-muted-foreground mb-4" />
        <h3 className="text-lg font-bold mb-2">
          {isAr ? "لا توجد استبيانات بعد" : "No surveys yet"}
        </h3>
        <p className="text-sm text-muted-foreground mb-4 max-w-sm">
          {isAr
            ? "ابدأ بإنشاء أول استبيان لجمع آراء عملائك"
            : "Create your first survey to start collecting customer feedback"}
        </p>
      </div>
    )
  }

  return (
    <div className="space-y-4">
      {/* Filter bar */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="relative flex-1 min-w-48">
          <Search className="absolute start-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground pointer-events-none" />
          <Input
            className="ps-9"
            placeholder={isAr ? "ابحث عن استبيان..." : "Search surveys..."}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <Select value={typeFilter} onValueChange={(v) => setTypeFilter(v ?? "all")}>
          <SelectTrigger className="w-44">
            <SelectValue placeholder={isAr ? "النوع" : "Type"} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{isAr ? "كل الأنواع" : "All Types"}</SelectItem>
            <SelectItem value="Transactional">{isAr ? "تشغيلي" : "Transactional"}</SelectItem>
            <SelectItem value="Relational">{isAr ? "دوري / علائقي" : "Relational"}</SelectItem>
          </SelectContent>
        </Select>
        <Select value={statusFilter} onValueChange={(v) => setStatusFilter(v ?? "all")}>
          <SelectTrigger className="w-40">
            <SelectValue placeholder={isAr ? "الحالة" : "Status"} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{isAr ? "كل الحالات" : "All Statuses"}</SelectItem>
            <SelectItem value="Draft">{isAr ? "مسودة" : "Draft"}</SelectItem>
            <SelectItem value="Active">{isAr ? "نشط" : "Active"}</SelectItem>
            <SelectItem value="Paused">{isAr ? "موقوف" : "Paused"}</SelectItem>
            <SelectItem value="Archived">{isAr ? "مؤرشف" : "Archived"}</SelectItem>
          </SelectContent>
        </Select>
        <Select value={journeyFilter} onValueChange={(v) => setJourneyFilter(v ?? "all")}>
          <SelectTrigger className="w-52">
            <SelectValue placeholder={isAr ? "الرحلة" : "Journey"} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{isAr ? "كل الرحلات" : "All Journeys"}</SelectItem>
            {journeyOptions.map((j) => (
              <SelectItem key={j.id} value={j.id}>
                {isAr ? j.labelAr : j.labelEn}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Table */}
      <div className="rounded-lg border border-border overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow className="bg-muted/40 hover:bg-muted/40">
              <TableHead className="text-start font-semibold">
                {isAr ? "الاستبيان" : "Survey"}
              </TableHead>
              <TableHead className="text-start font-semibold">
                {isAr ? "النوع" : "Type"}
              </TableHead>
              <TableHead className="text-start font-semibold">
                {isAr ? "الرحلة" : "Journey"}
              </TableHead>
              <TableHead className="text-start font-semibold">
                {isAr ? "الحالة" : "Status"}
              </TableHead>
              <TableHead className="text-start font-semibold">
                {isAr ? "الإصدار" : "Version"}
              </TableHead>
              <TableHead className="text-start font-semibold">
                {isAr ? "آخر تحديث" : "Updated"}
              </TableHead>
              <TableHead className="text-end font-semibold tabular-nums">
                {isAr ? "الردود" : "Responses"}
              </TableHead>
              <TableHead className="w-24" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {filtered.length === 0 ? (
              <TableRow>
                <TableCell colSpan={8} className="py-16 text-center text-muted-foreground">
                  {isAr ? "لا توجد نتائج مطابقة" : "No matching surveys"}
                </TableCell>
              </TableRow>
            ) : (
              filtered.map((survey) => (
                <TableRow
                  key={survey.id}
                  className="hover:bg-muted/50 cursor-pointer transition-colors"
                  onClick={() => navigate(`/surveys/${survey.id}/edit`)}
                >
                  <TableCell>
                    <div>
                      <p className="font-medium text-foreground text-sm">
                        {isAr ? survey.nameAr : survey.nameEn}
                      </p>
                      <p className="text-xs text-muted-foreground mt-0.5">
                        {isAr ? survey.nameEn : survey.nameAr}
                      </p>
                    </div>
                  </TableCell>
                  <TableCell>
                    <TypeBadge type={survey.type} isAr={isAr} />
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {survey.journeyId
                      ? (isAr ? survey.journeyNameAr : survey.journeyNameEn)
                      : "—"}
                  </TableCell>
                  <TableCell>
                    <StatusBadge status={survey.status} isAr={isAr} />
                  </TableCell>
                  <TableCell className="font-mono text-sm text-muted-foreground">
                    {survey.version}
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {survey.updatedAt}
                  </TableCell>
                  <TableCell className="text-end tabular-nums font-medium text-sm">
                    {survey.responseCount.toLocaleString("en-US")}
                  </TableCell>
                  <TableCell onClick={(e) => e.stopPropagation()}>
                    <div className="flex items-center justify-end gap-1">
                      <TooltipProvider>
                        <Tooltip>
                          <TooltipTrigger
                            className="inline-flex size-8 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-foreground transition-colors"
                            onClick={(e) => { e.stopPropagation(); navigate(`/surveys/${survey.id}/edit`) }}
                            aria-label={isAr ? "تعديل" : "Edit"}
                          >
                            <Pencil className="size-3.5" />
                          </TooltipTrigger>
                          <TooltipContent>{isAr ? "تعديل" : "Edit"}</TooltipContent>
                        </Tooltip>
                        <Tooltip>
                          <TooltipTrigger
                            className="inline-flex size-8 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-foreground transition-colors"
                            onClick={(e) => { e.stopPropagation(); navigate(`/surveys/${survey.id}/preview`) }}
                            aria-label={isAr ? "معاينة" : "Preview"}
                          >
                            <Eye className="size-3.5" />
                          </TooltipTrigger>
                          <TooltipContent>{isAr ? "معاينة" : "Preview"}</TooltipContent>
                        </Tooltip>
                        <Tooltip>
                          <TooltipTrigger
                            className="inline-flex size-8 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-foreground transition-colors"
                            onClick={(e) => { e.stopPropagation(); navigate(`/surveys/${survey.id}/stats`) }}
                            aria-label={isAr ? "الإحصائيات" : "Statistics"}
                          >
                            <BarChart2 className="size-3.5" />
                          </TooltipTrigger>
                          <TooltipContent>{isAr ? "الإحصائيات" : "Statistics"}</TooltipContent>
                        </Tooltip>
                      </TooltipProvider>
                      <DropdownMenu>
                        <DropdownMenuTrigger
                          className="inline-flex size-8 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-foreground transition-colors"
                          aria-label={isAr ? "المزيد" : "More"}
                        >
                          <MoreHorizontal className="size-3.5" />
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          <DropdownMenuItem onClick={(e) => { e.stopPropagation(); navigate(`/surveys/${survey.id}/stats`) }}>
                            <BarChart2 className="size-4 me-2" />
                            {isAr ? "الإحصائيات" : "Statistics"}
                          </DropdownMenuItem>
                          <DropdownMenuItem onClick={(e) => { e.stopPropagation(); navigate(`/surveys/${survey.id}/funnel`) }}>
                            <BarChart2 className="size-4 me-2" />
                            {isAr ? "مسار الاستجابة" : "Response Funnel"}
                          </DropdownMenuItem>
                          <DropdownMenuSeparator />
                          <DropdownMenuItem>
                            <Copy className="size-4 me-2" />
                            {isAr ? "تكرار" : "Duplicate"}
                          </DropdownMenuItem>
                          <DropdownMenuItem>
                            <Archive className="size-4 me-2" />
                            {isAr ? "أرشفة" : "Archive"}
                          </DropdownMenuItem>
                          <DropdownMenuSeparator />
                          <DropdownMenuItem className="text-destructive">
                            <Trash2 className="size-4 me-2" />
                            {isAr ? "حذف" : "Delete"}
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </div>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      <p className="text-xs text-muted-foreground">
        {isAr
          ? `${filtered.length} استبيان من أصل ${MOCK_SURVEYS.length}`
          : `Showing ${filtered.length} of ${MOCK_SURVEYS.length} surveys`}
      </p>
    </div>
  )
}

// ── Templates tab ─────────────────────────────────────────────────────────────
function TemplatesTab() {
  const navigate = useNavigate()
  const { i18n } = useTranslation()
  const isAr = i18n.language === "ar"

  const [search, setSearch] = useState("")
  const [classFilter, setClassFilter] = useState("all")
  const [sectorFilter, setSectorFilter] = useState("all")

  const filtered = MOCK_TEMPLATES.filter((t) => {
    const name = isAr ? t.nameAr : t.nameEn
    if (search && !name.toLowerCase().includes(search.toLowerCase())) return false
    if (classFilter !== "all" && t.templateClass !== classFilter) return false
    if (sectorFilter !== "all" && !t.sectors.includes(sectorFilter)) return false
    return true
  })

  return (
    <div className="space-y-4">
      {/* Filter bar */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="relative flex-1 min-w-48">
          <Search className="absolute start-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground pointer-events-none" />
          <Input
            className="ps-9"
            placeholder={isAr ? "ابحث عن قالب..." : "Search templates..."}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <Select value={classFilter} onValueChange={(v) => setClassFilter(v ?? "all")}>
          <SelectTrigger className="w-44">
            <SelectValue placeholder={isAr ? "الفئة" : "Class"} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{isAr ? "كل الفئات" : "All Classes"}</SelectItem>
            <SelectItem value="Platform">{isAr ? "قوالب المنصة" : "Platform"}</SelectItem>
            <SelectItem value="Tenant">{isAr ? "قوالب مخصصة" : "Tenant"}</SelectItem>
          </SelectContent>
        </Select>
        <Select value={sectorFilter} onValueChange={(v) => setSectorFilter(v ?? "all")}>
          <SelectTrigger className="w-44">
            <SelectValue placeholder={isAr ? "القطاع" : "Sector"} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{isAr ? "كل القطاعات" : "All Sectors"}</SelectItem>
            {Object.entries(SECTOR_LABELS).map(([key, label]) => (
              <SelectItem key={key} value={key}>
                {isAr ? label.ar : label.en}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Card grid */}
      {filtered.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-20 text-center">
          <FileText className="size-12 text-muted-foreground mb-4" />
          <h3 className="text-lg font-bold mb-2">
            {isAr ? "لا توجد قوالب مطابقة" : "No matching templates"}
          </h3>
          <p className="text-sm text-muted-foreground">
            {isAr ? "جرّب تغيير الفلاتر أو القطاع" : "Try adjusting the filters or sector"}
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {filtered.map((tpl) => (
            <TemplateCard key={tpl.id} template={tpl} isAr={isAr} navigate={navigate} />
          ))}
        </div>
      )}

      <p className="text-xs text-muted-foreground">
        {isAr
          ? `${filtered.length} قالب من أصل ${MOCK_TEMPLATES.length}`
          : `Showing ${filtered.length} of ${MOCK_TEMPLATES.length} templates`}
      </p>
    </div>
  )
}

function TemplateCard({
  template,
  isAr,
  navigate,
}: {
  template: SurveyTemplate
  isAr: boolean
  navigate: ReturnType<typeof useNavigate>
}) {
  const isPlatform = template.templateClass === "Platform"

  return (
    <div className="flex flex-col rounded-lg border border-border bg-card p-5 gap-4 hover:shadow-md transition-shadow duration-150">
      {/* Header */}
      <div className="flex items-start justify-between gap-3">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-1">
            <ClassBadge cls={template.templateClass} isAr={isAr} />
            {isPlatform && (
              <Lock className="size-3.5 text-muted-foreground shrink-0" aria-label={isAr ? "للقراءة فقط" : "Read-only"} />
            )}
          </div>
          <h3 className="font-semibold text-sm text-foreground leading-snug">
            {isAr ? template.nameAr : template.nameEn}
          </h3>
          <p className="text-xs text-muted-foreground mt-0.5">
            {isAr ? template.nameEn : template.nameAr}
          </p>
        </div>
      </div>

      {/* Sector tags */}
      <div className="flex flex-wrap gap-1.5">
        {template.sectors.map((s) => (
          <Badge key={s} variant="outline" className="text-xs">
            {isAr ? SECTOR_LABELS[s]?.ar ?? s : SECTOR_LABELS[s]?.en ?? s}
          </Badge>
        ))}
      </div>

      {/* Meta */}
      <div className="flex items-center justify-between text-xs text-muted-foreground mt-auto">
        <span>
          {isAr
            ? `${template.questionCount} أسئلة`
            : `${template.questionCount} questions`}
        </span>
        <span>
          {isAr
            ? `مستخدم في ${template.usedBySurveys} استبيان`
            : `Used by ${template.usedBySurveys} surveys`}
        </span>
      </div>

      {/* Actions */}
      <div className="flex items-center gap-2 pt-1 border-t border-border">
        <Button
          className="flex-1 bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
          size="sm"
          onClick={() => navigate(`/surveys/new/from-template/${template.id}`)}
        >
          {isAr ? "استخدم كاستبيان" : "Use as Survey"}
        </Button>
        {!isPlatform ? (
          <Button
            variant="secondary"
            size="icon"
            className="size-9"
            onClick={() => navigate(`/surveys/templates/${template.id}/edit`)}
            aria-label={isAr ? "تعديل القالب" : "Edit template"}
          >
            <Pencil className="size-3.5" />
          </Button>
        ) : (
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger
                className="inline-flex size-9 items-center justify-center rounded-md opacity-40 cursor-not-allowed bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200"
                aria-label={isAr ? "للقراءة فقط" : "Read-only"}
              >
                <Pencil className="size-3.5" />
              </TooltipTrigger>
              <TooltipContent>
                {isAr ? "قوالب المنصة للقراءة فقط" : "Platform templates are read-only"}
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
        )}
      </div>
    </div>
  )
}

// ── Page ──────────────────────────────────────────────────────────────────────
export default function SurveysLibraryPage() {
  const { i18n } = useTranslation()
  const isAr = i18n.language === "ar"
  const navigate = useNavigate()
  const [chooserOpen, setChooserOpen] = useState(false)

  return (
    <div className="space-y-5 py-5 px-8">
      {/* Page header */}
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-heading font-bold">
            {isAr ? "مكتبة الاستبيانات" : "Survey Library"}
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            {isAr
              ? "إدارة استبياناتك وقوالبك في مكان واحد"
              : "Manage your surveys and templates in one place"}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="secondary"
            onClick={() => navigate("/surveys/templates/new")}
          >
            <LayoutTemplate className="size-4 me-2" />
            {isAr ? "+ إضافة قالب" : "+ Add Template"}
          </Button>
          <Button
            className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            onClick={() => setChooserOpen(true)}
          >
            <Plus className="size-4 me-2" />
            {isAr ? "إضافة استبيان" : "Add Survey"}
          </Button>
        </div>
      </div>

      {/* Tabs */}
      <Tabs defaultValue="surveys" className="space-y-4">
        <TabsList className="bg-muted/60">
          <TabsTrigger value="surveys">
            {isAr ? "الاستبيانات" : "Surveys"}
            <span className="ms-2 rounded-full bg-primary/10 text-primary text-xs px-1.5 py-0.5 font-medium tabular-nums">
              {MOCK_SURVEYS.length}
            </span>
          </TabsTrigger>
          <TabsTrigger value="templates">
            {isAr ? "القوالب" : "Templates"}
            <span className="ms-2 rounded-full bg-primary/10 text-primary text-xs px-1.5 py-0.5 font-medium tabular-nums">
              {MOCK_TEMPLATES.length}
            </span>
          </TabsTrigger>
        </TabsList>

        <TabsContent value="surveys">
          <SurveysTab />
        </TabsContent>
        <TabsContent value="templates">
          <TemplatesTab />
        </TabsContent>
      </Tabs>

      <CreateSurveyChooser open={chooserOpen} onOpenChange={setChooserOpen} />
    </div>
  )
}
