// Custom authorization rule editor (T109). Manages a user's fine-grained rules:
// each rule grants a set of DOC-02 action codes (multi-select) and an optional
// per-parameter scope (value pickers). Supports add (createCustomRule), edit
// (updateCustomRule), and delete (deleteCustomRule). Read-only without Manage.
// RTL-first: logical properties throughout.

import { useState } from "react"
import { useTranslation } from "react-i18next"
import { Loader2, Plus, Trash2, X } from "lucide-react"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  createCustomRule,
  deleteCustomRule,
  updateCustomRule,
  type CustomRule,
} from "@/features/data-scope/api"

// DOC-02 action codes M-10 currently guards (mirrors PermissionEvaluationService).
const ACTION_CODES = ["CreateSurvey", "UpdateSurvey", "DeleteSurvey"] as const

type ScopeMap = Record<string, string[]>

interface CustomRuleEditorProps {
  userId: string
  rules: CustomRule[]
  /** Without UserManagement.Manage the editor is read-only. */
  canManage: boolean
  onChanged: () => void | Promise<void>
}

export function CustomRuleEditor({ userId, rules, canManage, onChanged }: CustomRuleEditorProps) {
  const { t } = useTranslation()

  if (rules.length === 0 && !canManage) {
    return <p className="text-sm text-muted-foreground">{t("dataScope.noRules")}</p>
  }

  return (
    <div className="space-y-4">
      {rules.length === 0 && (
        <p className="text-sm text-muted-foreground">{t("dataScope.noRules")}</p>
      )}

      {rules.map((rule, index) => (
        <RuleRow
          key={rule.ruleId}
          label={`${t("dataScope.rule")} ${index + 1}`}
          initialActions={rule.allowedActions}
          initialScope={rule.parameterScopeAssignments}
          canManage={canManage}
          onSave={(data) => updateCustomRule(userId, rule.ruleId, data).then(onChanged)}
          onDelete={() => deleteCustomRule(userId, rule.ruleId).then(onChanged)}
        />
      ))}

      {canManage && (
        <RuleRow
          // Re-key on rule count so the draft resets after a successful create.
          key={`new-${rules.length}`}
          label={t("dataScope.addRule")}
          initialActions={[]}
          initialScope={{}}
          canManage
          isNew
          onSave={(data) => createCustomRule(userId, data).then(onChanged)}
        />
      )}
    </div>
  )
}

interface RuleRowProps {
  label: string
  initialActions: string[]
  initialScope: ScopeMap
  canManage: boolean
  isNew?: boolean
  onSave: (data: { allowedActions: string[]; parameterScopeAssignments: ScopeMap }) => Promise<unknown>
  onDelete?: () => Promise<unknown>
}

function RuleRow({ label, initialActions, initialScope, canManage, isNew, onSave, onDelete }: RuleRowProps) {
  const { t } = useTranslation()
  const [actions, setActions] = useState<string[]>(initialActions)
  const [scope, setScope] = useState<ScopeMap>(initialScope)
  const [saving, setSaving] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const [error, setError] = useState(false)

  function toggleAction(code: string) {
    setActions((prev) => (prev.includes(code) ? prev.filter((a) => a !== code) : [...prev, code]))
  }

  async function handleSave() {
    setSaving(true)
    setError(false)
    try {
      await onSave({ allowedActions: actions, parameterScopeAssignments: scope })
      if (isNew) {
        setActions([])
        setScope({})
      }
    } catch {
      setError(true)
    } finally {
      setSaving(false)
    }
  }

  async function handleDelete() {
    if (!onDelete) return
    setDeleting(true)
    setError(false)
    try {
      await onDelete()
    } catch {
      setError(true)
      setDeleting(false)
    }
  }

  return (
    <div className="rounded-md border border-border bg-card p-4 space-y-4">
      <div className="flex items-center justify-between">
        <span className="text-sm font-bold">{label}</span>
        {!isNew && onDelete && canManage && (
          <Button
            variant="ghost"
            size="icon"
            aria-label={t("dataScope.deleteRule")}
            disabled={deleting || saving}
            onClick={handleDelete}
          >
            {deleting ? <Loader2 className="size-4 animate-spin" /> : <Trash2 className="size-4 text-destructive" />}
          </Button>
        )}
      </div>

      <div className="space-y-2">
        <Label>{t("dataScope.allowedActions")}</Label>
        <div className="flex flex-wrap gap-3">
          {ACTION_CODES.map((code) => (
            <label key={code} className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={actions.includes(code)}
                disabled={!canManage}
                onCheckedChange={() => toggleAction(code)}
                aria-label={t(`dataScope.action.${code}`)}
              />
              <span>{t(`dataScope.action.${code}`)}</span>
            </label>
          ))}
        </div>
      </div>

      <div className="space-y-2">
        <Label>{t("dataScope.ruleScope")}</Label>
        <ScopeEditor value={scope} canManage={canManage} onChange={setScope} />
      </div>

      {error && (
        <p role="alert" className="text-sm text-destructive">
          {t("dataScope.ruleSaveFailed")}
        </p>
      )}

      {canManage && (
        <div className="flex justify-end">
          <Button
            className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            disabled={saving}
            onClick={handleSave}
          >
            {saving ? (
              <Loader2 className="size-4 animate-spin" />
            ) : (
              isNew && <Plus className="size-4" />
            )}
            {isNew
              ? saving
                ? t("dataScope.creating")
                : t("dataScope.createRule")
              : saving
                ? t("dataScope.saving")
                : t("dataScope.updateRule")}
          </Button>
        </div>
      )}
    </div>
  )
}

interface ScopeEditorProps {
  value: ScopeMap
  canManage: boolean
  onChange: (next: ScopeMap) => void
}

/** Per-parameter value picker: a row of value tags per parameter, plus add-parameter. */
function ScopeEditor({ value, canManage, onChange }: ScopeEditorProps) {
  const { t } = useTranslation()
  const [newParam, setNewParam] = useState("")
  const entries = Object.entries(value)

  function addParameter() {
    const name = newParam.trim()
    if (!name || value[name]) return
    onChange({ ...value, [name]: [] })
    setNewParam("")
  }

  function removeParameter(param: string) {
    const next = { ...value }
    delete next[param]
    onChange(next)
  }

  function setValues(param: string, values: string[]) {
    onChange({ ...value, [param]: values })
  }

  return (
    <div className="space-y-3">
      {entries.length === 0 && (
        <p className="text-sm text-muted-foreground">{t("dataScope.noParameters")}</p>
      )}

      {entries.map(([param, values]) => (
        <div key={param} className="rounded-md border border-border p-3 space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-sm font-medium" dir="ltr">{param}</span>
            {canManage && (
              <Button
                variant="ghost"
                size="icon"
                aria-label={`${t("dataScope.deleteRule")} ${param}`}
                onClick={() => removeParameter(param)}
              >
                <X className="size-4" />
              </Button>
            )}
          </div>
          <TagInput values={values} canManage={canManage} onChange={(next) => setValues(param, next)} />
        </div>
      ))}

      {canManage && (
        <div className="flex items-center gap-2">
          <Input
            value={newParam}
            onChange={(e) => setNewParam(e.target.value)}
            placeholder={t("dataScope.parameterNamePlaceholder")}
            aria-label={t("dataScope.parameterNamePlaceholder")}
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                e.preventDefault()
                addParameter()
              }
            }}
          />
          <Button variant="secondary" size="compact" onClick={addParameter}>
            <Plus className="size-4" />
            {t("dataScope.addParameter")}
          </Button>
        </div>
      )}
    </div>
  )
}

interface TagInputProps {
  values: string[]
  canManage: boolean
  onChange: (next: string[]) => void
}

/** A tag-style multi-value input: existing values as removable pills + an add field. */
function TagInput({ values, canManage, onChange }: TagInputProps) {
  const { t } = useTranslation()
  const [draft, setDraft] = useState("")

  function add() {
    const v = draft.trim()
    if (!v || values.includes(v)) return
    onChange([...values, v])
    setDraft("")
  }

  return (
    <div className="space-y-2">
      {values.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {values.map((v) => (
            <Badge key={v} variant="outline" className="gap-1">
              <span dir="ltr">{v}</span>
              {canManage && (
                <button
                  type="button"
                  aria-label={`${t("dataScope.deleteRule")} ${v}`}
                  className="ms-1 text-muted-foreground hover:text-foreground"
                  onClick={() => onChange(values.filter((x) => x !== v))}
                >
                  <X className="size-3" />
                </button>
              )}
            </Badge>
          ))}
        </div>
      )}
      {canManage && (
        <div className="flex items-center gap-2">
          <Input
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            placeholder={t("dataScope.valuePlaceholder")}
            aria-label={t("dataScope.valuePlaceholder")}
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                e.preventDefault()
                add()
              }
            }}
          />
          <Button variant="secondary" size="compact" onClick={add}>
            {t("dataScope.add")}
          </Button>
        </div>
      )}
    </div>
  )
}
