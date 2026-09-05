// TEMPORARY route targets for the M-13 Integration Hub screens that have not been built yet (T020).
//
// Every screen gets a real route now, so navigation, the sidebar groups, deep links, and the
// permission gate are all exercisable in the browser before the screens themselves exist. Each
// component here is deleted by the story that owns its screen:
//   SCR-01/02 → T087/T085 (US3) · SCR-08 → T134 (US5) · SCR-07 → T164 (US6)
// SCR-03/04 shipped with US1 (T037/T038) and SCR-05/06 with US2 (T062/T061), so they are no
// longer in this file.

import { useTranslation } from "react-i18next"
import { ArrowLeftRight, Plug, ScrollText } from "lucide-react"

import { ScreenPlaceholder } from "@/features/integration-hub/components/ScreenPlaceholder"

export function AllIntegrationsPlaceholder() {
  const { t } = useTranslation()
  return (
    <ScreenPlaceholder
      icon={Plug}
      screenId="SCR-01"
      title={t("integrationHub.integrations.title")}
      description={t("integrationHub.integrations.description")}
      upcoming={t("integrationHub.integrations.upcoming")}
    />
  )
}

export function IntegrationWizardPlaceholder() {
  const { t } = useTranslation()
  return (
    <ScreenPlaceholder
      icon={Plug}
      screenId="SCR-02"
      title={t("integrationHub.integrationWizard.title")}
      description={t("integrationHub.integrationWizard.description")}
      upcoming={t("integrationHub.integrationWizard.upcoming")}
    />
  )
}

export function RequestLogsPlaceholder() {
  const { t } = useTranslation()
  return (
    <ScreenPlaceholder
      icon={ScrollText}
      screenId="SCR-08"
      title={t("integrationHub.requestLogs.title")}
      description={t("integrationHub.requestLogs.description")}
      upcoming={t("integrationHub.requestLogs.upcoming")}
    />
  )
}

export function ParameterMappingsPlaceholder() {
  const { t } = useTranslation()
  return (
    <ScreenPlaceholder
      icon={ArrowLeftRight}
      screenId="SCR-07"
      title={t("integrationHub.mappings.title")}
      description={t("integrationHub.mappings.description")}
      upcoming={t("integrationHub.mappings.upcoming")}
    />
  )
}

