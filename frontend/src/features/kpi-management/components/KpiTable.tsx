import { Link } from "react-router"
import { useTranslation } from "react-i18next"
import { Check } from "lucide-react"

import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { cn } from "@/lib/utils"
import type { KpiListItem } from "@/features/kpi-management/api"
import { KpiTypeBadge } from "./KpiTypeBadge"

/**
 * Catalogue table (US-1). shadcn `Table` with a sticky header; each row links to the KPI's
 * configuration page. The Dashboard column shows a check or an em-dash; Status is a colour + text
 * dot (never colour alone — WCAG). There is intentionally NO delete control on any row (FR-002).
 */
export function KpiTable({ items }: { items: KpiListItem[] }) {
  const { t } = useTranslation()

  return (
    <div data-testid="kpi-table" className="overflow-hidden rounded-lg border border-border bg-card shadow-sm dark:shadow-none">
      <Table>
        <TableHeader className="sticky top-0 z-10">
          <TableRow>
            <TableHead className="text-start font-semibold text-foreground">{t("kpi.colShortName")}</TableHead>
            <TableHead className="text-start font-semibold text-foreground">{t("kpi.colFullName")}</TableHead>
            <TableHead className="text-start font-semibold text-foreground">{t("kpi.colType")}</TableHead>
            <TableHead className="text-start font-semibold text-foreground">{t("kpi.colScale")}</TableHead>
            <TableHead className="text-start font-semibold text-foreground">{t("kpi.colCalcMethod")}</TableHead>
            <TableHead className="text-end font-semibold text-foreground">{t("kpi.colTarget")}</TableHead>
            <TableHead className="text-center font-semibold text-foreground">{t("kpi.colDashboard")}</TableHead>
            <TableHead className="text-start font-semibold text-foreground">{t("kpi.colStatus")}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {items.map((kpi) => (
            <TableRow
              key={kpi.id}
              data-testid="kpi-row"
              data-short-name={kpi.shortName}
              data-active={kpi.isActive}
              className="transition-colors hover:bg-muted/50"
            >
              <TableCell>
                <Link
                  to={`/kpi-management/${encodeURIComponent(kpi.shortName.toLowerCase())}`}
                  className="block text-start font-semibold text-foreground transition-colors hover:text-primary hover:underline"
                >
                  {kpi.shortName}
                </Link>
              </TableCell>
              <TableCell className="text-muted-foreground">{kpi.fullName}</TableCell>
              <TableCell>
                <KpiTypeBadge type={kpi.kpiType} />
              </TableCell>
              <TableCell className="text-start tabular-nums">{kpi.scaleLabel}</TableCell>
              <TableCell className="text-start">{kpi.calculationMethodLabel}</TableCell>
              <TableCell className="text-end tabular-nums">
                {kpi.target == null ? "—" : kpi.target}
              </TableCell>
              <TableCell className="text-center">
                {kpi.showOnDashboard ? (
                  <Check className="mx-auto size-4 text-primary" aria-label={t("kpi.onDashboard")} />
                ) : (
                  <span aria-hidden className="text-muted-foreground">
                    —
                  </span>
                )}
              </TableCell>
              <TableCell>
                <span className="inline-flex items-center gap-2">
                  <span
                    aria-hidden
                    className={cn(
                      "size-2 rounded-full",
                      kpi.isActive ? "bg-d2" : "bg-muted-foreground/50",
                    )}
                  />
                  <span className={cn("text-sm", kpi.isActive ? "text-foreground" : "text-muted-foreground")}>
                    {kpi.isActive ? t("kpi.statusActive") : t("kpi.statusInactive")}
                  </span>
                </span>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  )
}
