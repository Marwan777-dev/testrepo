// The standard access-denied state (T148, US9) — FR-GBL-02/05.
//
// Shown when a persona reaches an M-13 route it has no view grant for, most notably P-01 hitting
// `/integration-hub/logs` (BR-24: request logs are P-07-exclusive, the one screen with no
// cross-persona read-only grant). The sidebar already hides the item, so this is the direct-URL
// path: a deep link, a bookmark, or a shared link.
//
// Renders a complete, self-explanatory state rather than a blank page or a redirect — the user
// should understand *why* they can't be here and where to go instead.

import { useNavigate } from "react-router"
import { useTranslation } from "react-i18next"
import { ShieldOff } from "lucide-react"

import { Button } from "@/components/ui/button"

export interface AccessDeniedProps {
  /** Already-translated name of the screen that was refused, for the explanation line. */
  screenName: string
}

export function AccessDenied({ screenName }: AccessDeniedProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()

  return (
    <div className="space-y-5 py-5">
      <div
        className="flex flex-col items-center justify-center rounded-lg border border-border bg-card py-16 text-center shadow-sm dark:shadow-none"
        data-testid="access-denied"
        role="alert"
      >
        <ShieldOff className="mb-4 size-12 text-muted-foreground" />
        <h1 className="mb-2 text-lg font-bold">{t("integrationHub.accessDenied.title")}</h1>
        <p className="mb-4 max-w-md text-sm leading-relaxed text-muted-foreground">
          {t("integrationHub.accessDenied.body", { screen: screenName })}
        </p>
        <Button variant="outline" onClick={() => navigate("/integration-hub/integrations")}>
          {t("integrationHub.accessDenied.back")}
        </Button>
      </div>
    </div>
  )
}
