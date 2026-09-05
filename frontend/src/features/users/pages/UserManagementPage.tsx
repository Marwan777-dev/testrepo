// User Management page (T086): data-dense table of tenant users with invite,
// status-aware row actions (gated by UserManagement.Manage), search + status
// filter, cursor pagination, loading skeletons, and an empty state. RTL-first.

import { useCallback, useEffect, useState } from "react"
import { Link } from "react-router"
import { useTranslation } from "react-i18next"
import { Inbox, KeyRound, Loader2, LockOpen, Plus, UserCheck, UserX } from "lucide-react"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { Input } from "@/components/ui/input"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Skeleton } from "@/components/ui/skeleton"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { useSession } from "@/features/auth/hooks/useSession"
import {
  deactivateUser,
  listUsers,
  reactivateUser,
  resetMfa,
  unlockUser,
  type UserStatus,
  type UserSummary,
} from "@/features/users/api"
import { InviteUserDialog } from "@/features/users/components/InviteUserDialog"

const PAGE_SIZE = 20
const STATUSES: UserStatus[] = ["active", "inactive", "locked", "pending-enrollment"]

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

export default function UserManagementPage() {
  const { t } = useTranslation()
  const { session } = useSession()
  const canManage = session?.permissionSnapshot.modules["UserManagement"]?.includes("Manage") ?? false

  const [users, setUsers] = useState<UserSummary[]>([])
  const [nextPageToken, setNextPageToken] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(false)
  const [busyId, setBusyId] = useState<string | null>(null)

  // Filters (search is debounced into `query`).
  const [search, setSearch] = useState("")
  const [query, setQuery] = useState("")
  const [statusFilter, setStatusFilter] = useState<UserStatus | "all">("all")

  // Cursor pagination: `pageToken` is the current page; `history` lets us go back.
  const [pageToken, setPageToken] = useState<string | undefined>(undefined)
  const [history, setHistory] = useState<(string | undefined)[]>([])

  const [inviteOpen, setInviteOpen] = useState(false)
  const [confirmUser, setConfirmUser] = useState<UserSummary | null>(null)

  useEffect(() => {
    const handle = setTimeout(() => setQuery(search.trim()), 300)
    return () => clearTimeout(handle)
  }, [search])

  // Reset to the first page whenever a filter changes.
  useEffect(() => {
    setPageToken(undefined)
    setHistory([])
  }, [query, statusFilter])

  const load = useCallback(async () => {
    setLoading(true)
    setLoadError(false)
    try {
      const page = await listUsers({
        pageSize: PAGE_SIZE,
        pageToken,
        status: statusFilter === "all" ? undefined : statusFilter,
        q: query || undefined,
      })
      setUsers(page.items)
      setNextPageToken(page.nextPageToken)
    } catch {
      setLoadError(true)
      setUsers([])
      setNextPageToken(null)
    } finally {
      setLoading(false)
    }
  }, [pageToken, statusFilter, query])

  useEffect(() => {
    void load()
  }, [load])

  async function runAction(action: (id: string) => Promise<void>, user: UserSummary) {
    setBusyId(user.userId)
    try {
      await action(user.userId)
      await load()
    } catch {
      setLoadError(true)
    } finally {
      setBusyId(null)
    }
  }

  function goNext() {
    if (!nextPageToken) return
    setHistory((h) => [...h, pageToken])
    setPageToken(nextPageToken)
  }

  function goPrev() {
    setHistory((h) => {
      if (h.length === 0) return h
      const copy = [...h]
      const prev = copy.pop()
      setPageToken(prev)
      return copy
    })
  }

  return (
    <div className="space-y-5 py-5">
      {/* Header */}
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-heading font-bold">{t("users.title")}</h1>
          <p className="text-sm text-muted-foreground mt-1">{t("users.subtitle")}</p>
        </div>
        {canManage && (
          <Button
            className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            onClick={() => setInviteOpen(true)}
          >
            <Plus className="size-4 ms-2" />
            {t("users.invite")}
          </Button>
        )}
      </div>

      {/* Filters */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
        <Input
          type="search"
          className="sm:max-w-xs"
          placeholder={t("users.searchPlaceholder")}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          aria-label={t("users.searchPlaceholder")}
        />
        <Select value={statusFilter} onValueChange={(v) => setStatusFilter(v as UserStatus | "all")}>
          <SelectTrigger className="sm:w-48" aria-label={t("users.filterStatus")}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t("users.allStatuses")}</SelectItem>
            {STATUSES.map((s) => (
              <SelectItem key={s} value={s}>
                {t(STATUS_LABEL[s])}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {loadError && (
        <div
          role="alert"
          className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {t("users.loadError")}
        </div>
      )}

      {/* Table / states */}
      <div className="overflow-hidden rounded-lg border border-border bg-card shadow-sm dark:shadow-none">
        <Table>
          <TableHeader className="sticky top-0 z-10">
            <TableRow>
              <TableHead className="text-start">{t("users.colUsername")}</TableHead>
              <TableHead className="text-start">{t("users.colPersona")}</TableHead>
              <TableHead className="text-start">{t("users.colStatus")}</TableHead>
              <TableHead className="text-start">{t("users.colMfa")}</TableHead>
              {canManage && <TableHead className="text-end">{t("users.colActions")}</TableHead>}
            </TableRow>
          </TableHeader>
          <TableBody>
            {loading ? (
              Array.from({ length: 5 }).map((_, i) => (
                <TableRow key={i}>
                  <TableCell colSpan={canManage ? 5 : 4}>
                    <Skeleton className="h-5 w-full" />
                  </TableCell>
                </TableRow>
              ))
            ) : users.length === 0 ? (
              <TableRow>
                <TableCell colSpan={canManage ? 5 : 4}>
                  <div className="flex flex-col items-center justify-center py-16 text-center">
                    <Inbox className="size-12 text-muted-foreground mb-4" />
                    <h3 className="text-lg font-bold mb-2">{t("users.emptyTitle")}</h3>
                    <p className="text-muted-foreground mb-4 max-w-sm">{t("users.emptyDesc")}</p>
                    {canManage && (
                      <Button
                        className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
                        onClick={() => setInviteOpen(true)}
                      >
                        <Plus className="size-4 ms-2" />
                        {t("users.invite")}
                      </Button>
                    )}
                  </div>
                </TableCell>
              </TableRow>
            ) : (
              users.map((user) => (
                <TableRow key={user.userId} className="hover:bg-muted/50 transition-colors">
                  <TableCell className="font-medium" dir="ltr">
                    <Link
                      to={`/users/${user.userId}`}
                      className="block text-start text-primary hover:underline"
                    >
                      {user.username}
                    </Link>
                  </TableCell>
                  <TableCell className="tabular-nums">{user.persona}</TableCell>
                  <TableCell>
                    <Badge className={STATUS_BADGE[user.status]}>{t(STATUS_LABEL[user.status])}</Badge>
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {user.isMfaEnrolled ? t("users.mfaEnrolled") : t("users.mfaNotEnrolled")}
                  </TableCell>
                  {canManage && (
                    <TableCell className="text-end">
                      <div className="flex items-center justify-end gap-1">
                        {user.status === "locked" && (
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label={t("users.actionUnlock")}
                            title={t("users.actionUnlock")}
                            disabled={busyId === user.userId}
                            onClick={() => runAction(unlockUser, user)}
                          >
                            <LockOpen className="size-4" />
                          </Button>
                        )}
                        {user.status === "inactive" ? (
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label={t("users.actionReactivate")}
                            title={t("users.actionReactivate")}
                            disabled={busyId === user.userId}
                            onClick={() => runAction(reactivateUser, user)}
                          >
                            <UserCheck className="size-4" />
                          </Button>
                        ) : (
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label={t("users.actionDeactivate")}
                            title={t("users.actionDeactivate")}
                            disabled={busyId === user.userId}
                            onClick={() => setConfirmUser(user)}
                          >
                            <UserX className="size-4" />
                          </Button>
                        )}
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label={t("users.actionResetMfa")}
                          title={t("users.actionResetMfa")}
                          disabled={busyId === user.userId}
                          onClick={() => runAction(resetMfa, user)}
                        >
                          {busyId === user.userId ? (
                            <Loader2 className="size-4 animate-spin" />
                          ) : (
                            <KeyRound className="size-4" />
                          )}
                        </Button>
                      </div>
                    </TableCell>
                  )}
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {/* Pagination */}
      {!loading && users.length > 0 && (
        <div className="flex items-center justify-end gap-2">
          <Button variant="secondary" onClick={goPrev} disabled={history.length === 0}>
            {t("users.prev")}
          </Button>
          <Button variant="secondary" onClick={goNext} disabled={!nextPageToken}>
            {t("users.next")}
          </Button>
        </div>
      )}

      <InviteUserDialog open={inviteOpen} onOpenChange={setInviteOpen} onCreated={() => void load()} />

      {/* Deactivate confirmation */}
      <Dialog open={confirmUser !== null} onOpenChange={(o) => !o && setConfirmUser(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t("users.confirmDeactivateTitle")}</DialogTitle>
            <DialogDescription>
              {t("users.confirmDeactivateDesc", { username: confirmUser?.username ?? "" })}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter className="gap-2 sm:gap-2">
            <Button variant="secondary" onClick={() => setConfirmUser(null)}>
              {t("users.cancel")}
            </Button>
            <Button
              variant="destructive"
              onClick={() => {
                const target = confirmUser
                setConfirmUser(null)
                if (target) void runAction(deactivateUser, target)
              }}
            >
              {t("users.actionDeactivate")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
