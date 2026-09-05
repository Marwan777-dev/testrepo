// Permission route guard (used inside AuthGuard). Renders the nested routes only
// when the session holds the required permission module; otherwise it shows an
// access-restricted state (per spec: direct URLs return access-denied, not a
// silent redirect). A module is "held" when the snapshot grants any mode for it.

import { Outlet } from "react-router"
import { useTranslation } from "react-i18next"
import { ShieldAlert } from "lucide-react"

import { useSession } from "@/features/auth/hooks/useSession"

export function RequirePermission({ module: moduleId }: { module: string }) {
  const { t } = useTranslation()
  const { session } = useSession()
  const allowed = (session?.permissionSnapshot.modules[moduleId]?.length ?? 0) > 0

  if (allowed) return <Outlet />

  return (
    <div className="space-y-5 py-5">
      <div className="flex flex-col items-center justify-center py-16 text-center">
        <ShieldAlert className="size-12 text-muted-foreground mb-4" />
        <h1 className="text-lg font-bold mb-2">{t("common.accessRestrictedTitle")}</h1>
        <p className="text-muted-foreground max-w-sm">{t("common.accessRestrictedDesc")}</p>
      </div>
    </div>
  )
}
