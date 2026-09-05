// Permission module assignment editor (T088). Renders the DOC-02 module catalogue
// as rows of mode checkboxes (View / Manage / Full). CX-domain rows are disabled for
// non-P-01 actors, and the whole grid is read-only without UserManagement.Manage.
// Emits the full module set on save (modules with no modes are revoked).

import { useEffect, useMemo, useState } from "react"
import { useTranslation } from "react-i18next"
import { Loader2 } from "lucide-react"

import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Checkbox } from "@/components/ui/checkbox"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import type { ModuleAssignment } from "@/features/users/api"
import { PERMISSION_MODES, PERMISSION_MODULES } from "@/lib/permission-modules"

interface UserPermissionsEditorProps {
  assignments: ModuleAssignment[]
  /** Actor holds UserManagement.Manage — without it the grid is read-only. */
  canManage: boolean
  /** Actor may assign CX-domain modules (P-01 only). */
  canEditCx: boolean
  saving: boolean
  onSave: (assignments: ModuleAssignment[]) => void
}

type Draft = Record<string, string[]>

function toDraft(assignments: ModuleAssignment[]): Draft {
  const draft: Draft = {}
  for (const a of assignments) draft[a.moduleId] = [...a.allowedModes]
  return draft
}

export function UserPermissionsEditor({
  assignments,
  canManage,
  canEditCx,
  saving,
  onSave,
}: UserPermissionsEditorProps) {
  const { t } = useTranslation()
  const initial = useMemo(() => toDraft(assignments), [assignments])
  const [draft, setDraft] = useState<Draft>(initial)
  const [dirty, setDirty] = useState(false)

  // Re-sync when the upstream assignments change (e.g. after a save + refetch).
  useEffect(() => {
    setDraft(toDraft(assignments))
    setDirty(false)
  }, [assignments])

  function rowDisabled(cx: boolean): boolean {
    return !canManage || (cx && !canEditCx)
  }

  function toggle(moduleId: string, mode: string) {
    setDraft((d) => {
      const current = new Set(d[moduleId] ?? [])
      if (current.has(mode)) current.delete(mode)
      else current.add(mode)
      return { ...d, [moduleId]: [...current] }
    })
    setDirty(true)
  }

  function handleSave() {
    const next: ModuleAssignment[] = PERMISSION_MODULES
      .map(({ id }) => ({ moduleId: id, allowedModes: draft[id] ?? [] }))
      .filter((a) => a.allowedModes.length > 0)
    onSave(next)
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base font-bold">{t("users.permissionsTitle")}</CardTitle>
        <CardDescription>{t("users.permissionsDesc")}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {!canEditCx && (
          <p className="text-sm text-muted-foreground">{t("users.cxLocked")}</p>
        )}

        <div className="overflow-hidden rounded-md border border-border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="text-start">{t("users.colModule")}</TableHead>
                {PERMISSION_MODES.map((mode) => (
                  <TableHead key={mode} className="text-center">
                    {t(`users.mode.${mode}`)}
                  </TableHead>
                ))}
              </TableRow>
            </TableHeader>
            <TableBody>
              {PERMISSION_MODULES.map(({ id, cx }) => {
                const disabled = rowDisabled(cx)
                const modes = draft[id] ?? []
                return (
                  <TableRow key={id} className={disabled ? "opacity-60" : undefined}>
                    <TableCell className="font-medium">{t(`users.module.${id}`)}</TableCell>
                    {PERMISSION_MODES.map((mode) => (
                      <TableCell key={mode} className="text-center">
                        <div className="flex justify-center">
                          <Checkbox
                            checked={modes.includes(mode)}
                            disabled={disabled}
                            onCheckedChange={() => toggle(id, mode)}
                            aria-label={`${t(`users.module.${id}`)} – ${t(`users.mode.${mode}`)}`}
                          />
                        </div>
                      </TableCell>
                    ))}
                  </TableRow>
                )
              })}
            </TableBody>
          </Table>
        </div>

        {canManage && (
          <div className="flex justify-end">
            <Button
              className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
              disabled={!dirty || saving}
              onClick={handleSave}
            >
              {saving && <Loader2 className="size-4 animate-spin ms-2" />}
              {saving ? t("users.saving") : t("users.save")}
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  )
}
