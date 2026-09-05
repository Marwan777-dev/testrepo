// KPI Management catalogue page (US-1). Page header (title + active-count subtitle + the single
// primary "+ Add KPI" action, hidden for the P-02 Analyst per FR-009), a Type / Active-only /
// Search filter row (40px controls), and the catalogue table with loading skeletons and teaching
// empty states (truly-empty vs filtered-empty). RTL-first; follows the design-system table rules.

import { Link } from "react-router"
import { useTranslation } from "react-i18next"
import { Gauge, Inbox, Plus, Search } from "lucide-react"

import { Button, buttonVariants } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Skeleton } from "@/components/ui/skeleton"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { cn } from "@/lib/utils"
import { useSession } from "@/features/auth/hooks/useSession"
import type { KpiTypeFilter } from "@/features/kpi-management/api"
import { useKpiList } from "@/features/kpi-management/hooks/useKpiList"
import { ActiveKpiCountSubtitle } from "@/features/kpi-management/components/ActiveKpiCountSubtitle"
import { KpiTable } from "@/features/kpi-management/components/KpiTable"

const TYPE_OPTIONS: KpiTypeFilter[] = ["All", "Standard", "Custom"]
const TYPE_LABEL: Record<KpiTypeFilter, string> = {
  All: "kpi.all",
  Standard: "kpi.typeStandard",
  Custom: "kpi.typeCustom",
}

export default function KpiManagementPage() {
  const { t } = useTranslation()
  const { session } = useSession()
  const {
    items,
    activeCount,
    truncated,
    loading,
    error,
    type,
    setType,
    activeOnly,
    setActiveOnly,
    search,
    setSearch,
    isFiltered,
    clearFilters,
  } = useKpiList()

  // FR-009: the P-02 Analyst sees the catalogue read-only — no create action. P-01/P-06 may author.
  const canEdit = session?.persona !== "P-02"

  return (
    <div className="space-y-5 py-5">
      {/* Header */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
        <div className="min-w-0">
          <div className="flex items-center gap-2.5">
            <Gauge className="size-7 shrink-0 text-primary" aria-hidden />
            <h1 className="text-2xl font-heading font-bold">{t("kpi.moduleTitle")}</h1>
          </div>
          <ActiveKpiCountSubtitle count={activeCount} />
        </div>
        {canEdit && (
          <Link
            to="/kpi-management/new"
            data-testid="kpi-add-button"
            className={cn(buttonVariants(), "bg-primary hover:bg-nb-cyan-700 text-primary-foreground")}
          >
            <Plus className="size-4 ms-2" aria-hidden />
            {t("kpi.addKpi")}
          </Link>
        )}
      </div>

      {/* Filter row: type + active-only + search (40px controls) */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end">
        <div className="flex-1 space-y-1.5 sm:max-w-sm">
          <label
            htmlFor="kpi-search"
            className="block text-xs font-medium uppercase tracking-widest text-muted-foreground"
          >
            {t("kpi.searchLabel")}
          </label>
          <div className="relative">
            <Search
              className="pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
              aria-hidden
            />
            <Input
              id="kpi-search"
              type="search"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t("kpi.searchPlaceholder")}
              className="ps-9"
            />
          </div>
        </div>

        <div className="flex flex-col gap-1.5 sm:w-48">
          <span className="block text-xs font-medium uppercase tracking-widest text-muted-foreground">
            {t("kpi.filterType")}
          </span>
          <Select value={type} onValueChange={(v) => setType((v ?? "All") as KpiTypeFilter)}>
            <SelectTrigger data-testid="kpi-type-filter" className="w-full">
              <SelectValue>{(v) => t(TYPE_LABEL[(v as KpiTypeFilter) ?? "All"])}</SelectValue>
            </SelectTrigger>
            <SelectContent>
              {TYPE_OPTIONS.map((option) => (
                <SelectItem key={option} value={option} data-testid={`kpi-type-option-${option}`}>
                  {t(TYPE_LABEL[option])}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="flex h-10 items-center gap-2 sm:pb-0">
          <Checkbox
            id="kpi-active-only"
            checked={activeOnly}
            onCheckedChange={(checked) => setActiveOnly(checked === true)}
          />
          <Label htmlFor="kpi-active-only" className="cursor-pointer text-sm font-normal">
            {t("kpi.activeOnly")}
          </Label>
        </div>
      </div>

      {error && (
        <div
          role="alert"
          className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {t("kpi.loadError")}
        </div>
      )}

      {/* Table / states */}
      {loading ? (
        <div className="space-y-3 rounded-lg border border-border bg-card p-4 shadow-sm dark:shadow-none">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-8 w-full" />
          ))}
        </div>
      ) : items.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-lg border border-border bg-card py-16 text-center shadow-sm dark:shadow-none">
          <Inbox className="mb-4 size-12 text-muted-foreground" aria-hidden />
          <h3 className="mb-2 text-lg font-bold">
            {isFiltered ? t("kpi.emptyFilterTitle") : t("kpi.noKpis")}
          </h3>
          <p className="mb-4 max-w-sm text-muted-foreground">
            {isFiltered ? t("kpi.emptyFilterHelp") : t("kpi.noKpisHelp")}
          </p>
          {isFiltered ? (
            <Button variant="secondary" onClick={clearFilters}>
              {t("kpi.clearFilter")}
            </Button>
          ) : (
            canEdit && (
              <Link
                to="/kpi-management/new"
                className={cn(buttonVariants(), "bg-primary hover:bg-nb-cyan-700 text-primary-foreground")}
              >
                <Plus className="size-4 ms-2" aria-hidden />
                {t("kpi.addKpi")}
              </Link>
            )
          )}
        </div>
      ) : (
        <KpiTable items={items} />
      )}

      {!loading && truncated && (
        <p className="text-xs text-muted-foreground">{t("kpi.listTruncated", { count: 200 })}</p>
      )}
    </div>
  )
}
