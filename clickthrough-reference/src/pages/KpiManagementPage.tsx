// src/pages/KpiManagementPage.tsx
import { useState, useMemo } from "react"
import { useNavigate } from "react-router"
import { useTranslation } from "react-i18next"
import { Plus, Search, CheckSquare, Gauge } from "lucide-react"
import { useKpis } from "@/contexts/kpi-context"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Checkbox } from "@/components/ui/checkbox"
import { Badge } from "@/components/ui/badge"
import { Label } from "@/components/ui/label"
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select"
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from "@/components/ui/table"
import { cn } from "@/lib/utils"
import type { KpiDefinition } from "@/types/kpi"

// Fixed order for standard KPIs (per spec)
const STANDARD_ORDER = ["nps", "csat", "ces", "cxi", "fcr", "vfm", "agent", "chs"]

function sortKpis(kpis: KpiDefinition[]): KpiDefinition[] {
  const standard = STANDARD_ORDER
    .map((id) => kpis.find((k) => k.id === id))
    .filter(Boolean) as KpiDefinition[]
  const custom = kpis
    .filter((k) => k.type === "Custom")
    .sort((a, b) => (b.createdAt ?? "").localeCompare(a.createdAt ?? ""))
  return [...standard, ...custom]
}

function calcMethodLabel(method: string, t: (k: string) => string): string {
  const map: Record<string, string> = {
    WeightedAverage: t("kpi.calcMethodWeightedAverage"),
    TopNBox: t("kpi.calcMethodTopNBox"),
    NpsStandard: t("kpi.calcMethodNpsStandard"),
    WeightedComposite: t("kpi.calcMethodWeightedComposite"),
  }
  return map[method] ?? method
}

export default function KpiManagementPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { kpis, activeCount } = useKpis()

  const [activeOnly, setActiveOnly] = useState(true)
  const [typeFilter, setTypeFilter] = useState<"all" | "Standard" | "Custom">("all")
  const [search, setSearch] = useState("")

  const filtered = useMemo(() => {
    let list = sortKpis(kpis)
    if (activeOnly) list = list.filter((k) => k.isActive)
    if (typeFilter !== "all") list = list.filter((k) => k.type === typeFilter)
    if (search.trim()) {
      const q = search.trim().toLowerCase()
      list = list.filter(
        (k) =>
          k.shortName.toLowerCase().includes(q) ||
          k.fullName.toLowerCase().includes(q),
      )
    }
    return list
  }, [kpis, activeOnly, typeFilter, search])

  return (
    <div className="space-y-5 py-5 px-8">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-heading font-bold">{t("kpi.management")}</h1>
          <p className="text-sm text-muted-foreground mt-1">
            {t("kpi.activeCount", { count: activeCount })}
          </p>
        </div>
        <Button
          className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground gap-2"
          onClick={() => navigate("/kpi-management/new")}
        >
          <Plus className="size-4" />
          {t("kpi.addKpi")}
        </Button>
      </div>

      {/* Filter Bar */}
      <div className="flex items-end gap-3 flex-wrap">
        <div className="flex flex-col gap-1.5">
          <Label className="text-xs font-medium text-muted-foreground uppercase tracking-widest">
            {t("kpi.colType")}
          </Label>
          <Select value={typeFilter} onValueChange={(v) => setTypeFilter(v as typeof typeFilter)}>
            <SelectTrigger className="w-40">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">{t("kpi.typeAll")}</SelectItem>
              <SelectItem value="Standard">{t("kpi.typeStandard")}</SelectItem>
              <SelectItem value="Custom">{t("kpi.typeCustom")}</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label className="text-xs font-medium text-muted-foreground uppercase tracking-widest">
            {t("kpi.colStatus")}
          </Label>
          <div className="flex items-center gap-2 h-10 px-3 rounded-md border border-input bg-card">
            <Checkbox
              id="activeOnly"
              checked={activeOnly}
              onCheckedChange={(v) => setActiveOnly(Boolean(v))}
            />
            <Label htmlFor="activeOnly" className="text-sm cursor-pointer">
              {t("kpi.activeOnly")}
            </Label>
          </div>
        </div>

        <div className="flex flex-col gap-1.5 flex-1 min-w-48 max-w-72">
          <Label className="text-xs font-medium text-muted-foreground uppercase tracking-widest">
            {t("common.search", { defaultValue: "Search" })}
          </Label>
          <div className="relative">
            <Search className="absolute start-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground pointer-events-none" />
            <Input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t("kpi.searchPlaceholder")}
              className="ps-9"
            />
          </div>
        </div>
      </div>

      {/* Table */}
      <div className="rounded-lg border border-border overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="w-28">{t("kpi.colShortName")}</TableHead>
              <TableHead>{t("kpi.colFullName")}</TableHead>
              <TableHead className="w-24">{t("kpi.colType")}</TableHead>
              <TableHead className="w-20">{t("kpi.colScale")}</TableHead>
              <TableHead className="w-40">{t("kpi.colMethod")}</TableHead>
              <TableHead className="w-20 text-center">{t("kpi.colTarget")}</TableHead>
              <TableHead className="w-24 text-center">{t("kpi.colDashboard")}</TableHead>
              <TableHead className="w-28">{t("kpi.colStatus")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {filtered.length === 0 ? (
              <TableRow>
                <TableCell colSpan={8} className="text-center py-12 text-muted-foreground">
                  <Gauge className="size-8 mx-auto mb-2 opacity-30" />
                  <p>{t("common.noData", { defaultValue: "No KPIs found." })}</p>
                </TableCell>
              </TableRow>
            ) : (
              filtered.map((kpi) => (
                <TableRow
                  key={kpi.id}
                  onClick={() => navigate(`/kpi-management/${kpi.id}`)}
                  className={cn(
                    "cursor-pointer hover:bg-muted/50 transition-colors",
                    !kpi.isActive && "opacity-50",
                  )}
                >
                  <TableCell className="font-mono font-bold text-sm text-primary">
                    {kpi.shortName}
                  </TableCell>
                  <TableCell className="text-sm">{kpi.fullName}</TableCell>
                  <TableCell>
                    <Badge
                      variant="outline"
                      className={kpi.type === "Standard"
                        ? "text-xs bg-nb-cyan-100 text-nb-cyan-800 border-nb-cyan-200 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200 dark:border-nb-cyan-800"
                        : "text-xs"}
                    >
                      {kpi.type === "Standard" ? t("kpi.typeStandard") : t("kpi.typeCustom")}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-sm tabular-nums text-muted-foreground">
                    {kpi.scale ?? "—"}
                  </TableCell>
                  <TableCell className="text-xs text-muted-foreground">
                    {calcMethodLabel(kpi.calculationMethod, t)}
                  </TableCell>
                  <TableCell className="text-center tabular-nums font-medium">
                    {kpi.target}
                  </TableCell>
                  <TableCell className="text-center">
                    {kpi.showOnDashboard ? (
                      <CheckSquare className="size-4 text-primary mx-auto" />
                    ) : (
                      <span className="text-muted-foreground text-xs">—</span>
                    )}
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center gap-1.5">
                      <span
                        className={cn(
                          "size-2 rounded-full shrink-0",
                          kpi.isActive ? "bg-d2" : "bg-muted-foreground/40",
                        )}
                      />
                      <span className="text-xs">
                        {kpi.isActive ? t("kpi.statusActive") : t("kpi.statusInactive")}
                      </span>
                    </div>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>
    </div>
  )
}
