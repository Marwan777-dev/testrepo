// User detail page (T087), route /users/:userId. Shows the profile and a permission
// module editor. P-07 actors see CX-domain rows disabled; saving calls
// updatePermissions, after which lastPermissionSnapshotVersion bumps (shown as a
// change indicator). RTL-first.

import { useCallback, useEffect, useState } from "react"
import { useNavigate, useParams } from "react-router"
import { useTranslation } from "react-i18next"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { useSession } from "@/features/auth/hooks/useSession"
import {
  getUser,
  updatePermissions,
  UsersApiError,
  type ModuleAssignment,
  type UserDetail,
  type UserStatus,
} from "@/features/users/api"
import { UserPermissionsEditor } from "@/features/users/components/UserPermissionsEditor"

const STATUS_BADGE: Record<UserStatus, string> = {
  active: "bg-d2-light text-d2-dark dark:bg-d2-dark/25 dark:text-d2-light",
  "pending-enrollment": "bg-d3-light text-d3-dark dark:bg-d3-dark/25 dark:text-d3-light",
  locked: "bg-d5-light text-d5-dark dark:bg-d5-dark/25 dark:text-d5-light",
  inactive: "bg-muted text-muted-foreground",
}

const STATUS_LABEL: Record<UserStatus, string> = {
  active: "users.statusActive",
  inactive: "users.statusInactive",
  locked: "users.statusLocked",
  "pending-enrollment": "users.statusPending",
}

export default function UserDetailPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { userId } = useParams<{ userId: string }>()
  const { session } = useSession()
  const canManage = session?.permissionSnapshot.modules["UserManagement"]?.includes("Manage") ?? false
  const canEditCx = session?.persona === "P-01"

  const [detail, setDetail] = useState<UserDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [notFound, setNotFound] = useState(false)
  const [error, setError] = useState(false)
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)

  const load = useCallback(async () => {
    if (!userId) return
    setLoading(true)
    setError(false)
    setNotFound(false)
    try {
      setDetail(await getUser(userId))
    } catch (err) {
      if (err instanceof UsersApiError && err.status === 404) setNotFound(true)
      else setError(true)
    } finally {
      setLoading(false)
    }
  }, [userId])

  useEffect(() => {
    void load()
  }, [load])

  async function handleSave(assignments: ModuleAssignment[]) {
    if (!userId) return
    setSaving(true)
    setSaved(false)
    setError(false)
    try {
      await updatePermissions(userId, assignments)
      await load()
      setSaved(true)
    } catch {
      setError(true)
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="space-y-5 py-5">
      <div className="flex items-center justify-between gap-4">
        <Button variant="ghost" className="px-2 text-muted-foreground" onClick={() => navigate("/users")}>
          {t("users.back")}
        </Button>
        {userId && (
          <Button variant="secondary" onClick={() => navigate(`/users/${userId}/scope`)}>
            {t("dataScope.manageScopeLink")}
          </Button>
        )}
      </div>

      {loading ? (
        <div className="space-y-4">
          <Skeleton className="h-24 w-full rounded-lg" />
          <Skeleton className="h-64 w-full rounded-lg" />
        </div>
      ) : notFound ? (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <h3 className="text-lg font-bold">{t("users.notFound")}</h3>
        </div>
      ) : error && !detail ? (
        <div
          role="alert"
          className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {t("users.detailLoadError")}
        </div>
      ) : detail ? (
        <div className="space-y-5">
          {/* Profile */}
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between gap-4">
                <CardTitle className="text-xl font-heading" dir="ltr">
                  <span className="block text-start">{detail.username}</span>
                </CardTitle>
                <Badge className={STATUS_BADGE[detail.status]}>{t(STATUS_LABEL[detail.status])}</Badge>
              </div>
            </CardHeader>
            <CardContent>
              <dl className="grid grid-cols-2 gap-x-6 gap-y-3 sm:grid-cols-4">
                <div>
                  <dt className="text-xs font-medium uppercase tracking-widest text-muted-foreground">
                    {t("users.fieldPersona")}
                  </dt>
                  <dd className="mt-1 text-sm tabular-nums">{detail.persona}</dd>
                </div>
                <div>
                  <dt className="text-xs font-medium uppercase tracking-widest text-muted-foreground">
                    {t("users.fieldMfa")}
                  </dt>
                  <dd className="mt-1 text-sm">
                    {detail.isMfaEnrolled ? t("users.mfaEnrolled") : t("users.mfaNotEnrolled")}
                  </dd>
                </div>
                <div>
                  <dt className="text-xs font-medium uppercase tracking-widest text-muted-foreground">
                    {t("users.fieldOrgNode")}
                  </dt>
                  <dd className="mt-1 text-sm">{detail.organizationNodeId ?? t("users.none")}</dd>
                </div>
                <div>
                  <dt className="text-xs font-medium uppercase tracking-widest text-muted-foreground">
                    {t("users.fieldVersion")}
                  </dt>
                  <dd className="mt-1">
                    <Badge className="bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200 tabular-nums">
                      v{detail.lastPermissionSnapshotVersion}
                    </Badge>
                  </dd>
                </div>
              </dl>
            </CardContent>
          </Card>

          {saved && (
            <div
              role="status"
              className="rounded-md border border-d2-dark/30 bg-d2-light px-3 py-2 text-sm text-d2-dark dark:bg-d2-dark/25 dark:text-d2-light"
            >
              {t("users.saved")}
            </div>
          )}
          {error && (
            <div
              role="alert"
              className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
            >
              {t("users.saveFailed")}
            </div>
          )}

          {/* Permission editor — re-keyed on version so it resets after a save. */}
          <UserPermissionsEditor
            key={detail.lastPermissionSnapshotVersion}
            assignments={detail.permissionModuleAssignments}
            canManage={canManage}
            canEditCx={canEditCx}
            saving={saving}
            onSave={handleSave}
          />
        </div>
      ) : null}
    </div>
  )
}
