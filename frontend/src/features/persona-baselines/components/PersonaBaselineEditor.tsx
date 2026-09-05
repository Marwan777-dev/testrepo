// Per-persona baseline editor (one accordion panel). Renders the DOC-02 module
// catalogue as mode checkboxes; CX-domain rows are disabled for non-P-01 actors.
// Saving asks for confirmation, then PUTs the full module set via updatePersonaBaseline.

import { useState } from "react"
import { useTranslation } from "react-i18next"
import { Loader2 } from "lucide-react"

import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { PERMISSION_MODES, PERMISSION_MODULES } from "@/lib/permission-modules"
import {
  updatePersonaBaseline,
  type ModuleAssignment,
  type PersonaBaseline,
} from "@/features/persona-baselines/api"

interface PersonaBaselineEditorProps {
  baseline: PersonaBaseline
  /** Actor may assign CX-domain modules (P-01 only). */
  canEditCx: boolean
  onSaved: () => void
}

type Draft = Record<string, string[]>

function toDraft(assignments: ModuleAssignment[]): Draft {
  const draft: Draft = {}
  for (const a of assignments) draft[a.moduleId] = [...a.allowedModes]
  return draft
}

export function PersonaBaselineEditor({ baseline, canEditCx, onSaved }: PersonaBaselineEditorProps) {
  const { t } = useTranslation()
  const [draft, setDraft] = useState<Draft>(() => toDraft(baseline.permissionModuleAssignments))
  const [dirty, setDirty] = useState(false)
  const [confirmOpen, setConfirmOpen] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState(false)

  function toggle(moduleId: string, mode: string) {
    setDraft((d) => {
      const current = new Set(d[moduleId] ?? [])
      if (current.has(mode)) current.delete(mode)
      else current.add(mode)
      return { ...d, [moduleId]: [...current] }
    })
    setDirty(true)
  }

  async function handleConfirm() {
    setConfirmOpen(false)
    setSaving(true)
    setError(false)
    try {
      const permissionModuleAssignments = PERMISSION_MODULES
        .map(({ id }) => ({ moduleId: id, allowedModes: draft[id] ?? [] }))
        .filter((a) => a.allowedModes.length > 0)
      await updatePersonaBaseline(baseline.personaId, { permissionModuleAssignments })
      setDirty(false)
      onSaved()
    } catch {
      setError(true)
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="space-y-4">
      {!canEditCx && <p className="text-sm text-muted-foreground">{t("users.cxLocked")}</p>}
      {error && (
        <div
          role="alert"
          className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {t("personaBaselines.saveFailed")}
        </div>
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
              const disabled = cx && !canEditCx
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

      <div className="flex justify-end">
        <Button
          className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
          disabled={!dirty || saving}
          onClick={() => setConfirmOpen(true)}
        >
          {saving && <Loader2 className="size-4 animate-spin ms-2" />}
          {saving ? t("personaBaselines.saving") : t("personaBaselines.save")}
        </Button>
      </div>

      <Dialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t("personaBaselines.confirmTitle", { persona: baseline.personaId })}</DialogTitle>
            <DialogDescription>
              {t("personaBaselines.confirmDesc", { persona: baseline.personaId })}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter className="gap-2 sm:gap-2">
            <Button variant="secondary" onClick={() => setConfirmOpen(false)}>
              {t("personaBaselines.cancel")}
            </Button>
            <Button
              className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
              onClick={handleConfirm}
            >
              {t("personaBaselines.confirmSave")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
