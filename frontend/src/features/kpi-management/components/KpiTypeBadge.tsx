import { useTranslation } from "react-i18next"

import { Badge } from "@/components/ui/badge"
import type { KpiType } from "@/features/kpi-management/api"

/**
 * KPI type tag. Standard = a filled brand-cyan pill (`bg-nb-cyan / text-white`); Custom = the
 * outline variant (white fill per CLAUDE.md). Cyan (brand) is used rather than mint so the
 * pill never reads as the D2 "Good" semantic state on a KPI screen (Two-Palette Rule).
 */
export function KpiTypeBadge({ type }: { type: KpiType }) {
  const { t } = useTranslation()

  if (type === "Standard") {
    return (
      <Badge
        data-testid="kpi-type-badge"
        data-type="Standard"
        className="border-transparent bg-nb-cyan text-white dark:bg-nb-cyan-700 dark:text-white"
      >
        {t("kpi.typeStandard")}
      </Badge>
    )
  }

  return (
    <Badge data-testid="kpi-type-badge" data-type="Custom" variant="outline">
      {t("kpi.typeCustom")}
    </Badge>
  )
}
