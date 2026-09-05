import { useTranslation } from "react-i18next"

/**
 * Page-header caption rendering "[X] Active KPIs" beneath the KPI Management title. The count comes
 * from `useKpiList().activeCount` (active rows across the whole catalogue), so it stays stable as
 * the user filters and decrements immediately when a KPI is deactivated this session (FR-008 /
 * SC-011). Western digits in Arabic per the design system.
 */
export function ActiveKpiCountSubtitle({ count }: { count: number }) {
  const { t } = useTranslation()
  return (
    <p data-testid="kpi-active-count" className="mt-1 text-sm text-muted-foreground">
      {t("kpi.activeCount", { count })}
    </p>
  )
}
