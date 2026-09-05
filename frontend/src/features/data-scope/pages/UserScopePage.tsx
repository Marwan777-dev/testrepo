// Data Scope & Custom Rules page (T108), route /users/:userId/scope. Lets a P-01/P-07
// admin assign a hierarchy node and per-parameter scope values to a user, and manage
// their custom authorization rules. Non-admins get an access-denied state. RTL-first:
// logical properties throughout.
//
// API note: M-10 exposes no list endpoint for hierarchy nodes (M-11-owned) or parameter
// definitions (M-13-owned), so the hierarchy node is entered as an id and parameter
// values are free-entry tags — the backend validates values against the definitions and
// returns 422 on a value outside its parameter's allowed set.

import { useCallback, useEffect, useState } from "react"
import { useNavigate, useParams } from "react-router"
import { useTranslation } from "react-i18next"
import { Loader2, Plus, ShieldAlert, X } from "lucide-react"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Skeleton } from "@/components/ui/skeleton"
import { useSession } from "@/features/auth/hooks/useSession"
import {
  DataScopeApiError,
  getUserScope,
  updateUserScope,
  type CustomRule,
  type DataScopeAssignment,
} from "@/features/data-scope/api"
import { CustomRuleEditor } from "@/features/data-scope/components/CustomRuleEditor"

const MANAGER_PERSONAS = new Set(["P-01", "P-07"])

export default function UserScopePage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { userId } = useParams<{ userId: string }>()
  const { session } = useSession()
  const persona = session?.persona ?? ""
  const allowed = MANAGER_PERSONAS.has(persona)
  const canManage = session?.permissionSnapshot.modules["UserManagement"]?.includes("Manage") ?? false

  const [orgNode, setOrgNode] = useState("")
  const [assignments, setAssignments] = useState<DataScopeAssignment[]>([])
  const [rulesKey, setRulesKey] = useState(0)
  const [rules, setRules] = useState<CustomRule[]>([])
  const [loaded, setLoaded] = useState(false)
  const [newParam, setNewParam] = useState("")
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(false)
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)
  const [invalid, setInvalid] = useState(false)

  const load = useCallback(async () => {
    if (!userId) return
    setLoading(true)
    setError(false)
    try {
      const scope = await getUserScope(userId)
      setOrgNode(scope.organizationNodeId ?? "")
      setAssignments(scope.dataScopeAssignments)
      setRules(scope.customRules)
      setLoaded(true)
    } catch {
      setError(true)
    } finally {
      setLoading(false)
    }
  }, [userId])

  useEffect(() => {
    if (allowed) void load()
    else setLoading(false)
  }, [allowed, load])

  function addParameter() {
    const name = newParam.trim()
    if (!name || assignments.some((a) => a.parameterName === name)) return
    setAssignments((prev) => [...prev, { parameterName: name, allowedValues: [] }])
    setNewParam("")
  }

  function removeParameter(name: string) {
    setAssignments((prev) => prev.filter((a) => a.parameterName !== name))
  }

  function setValues(name: string, values: string[]) {
    setAssignments((prev) => prev.map((a) => (a.parameterName === name ? { ...a, allowedValues: values } : a)))
  }

  async function handleSaveScope() {
    if (!userId) return
    setSaving(true)
    setSaved(false)
    setError(false)
    setInvalid(false)
    try {
      const updated = await updateUserScope(userId, {
        organizationNodeId: orgNode.trim() ? orgNode.trim() : null,
        dataScopeAssignments: assignments,
      })
      setOrgNode(updated.organizationNodeId ?? "")
      setAssignments(updated.dataScopeAssignments)
      setRules(updated.customRules)
      setSaved(true)
    } catch (err) {
      if (err instanceof DataScopeApiError && err.status === 422) setInvalid(true)
      else setError(true)
    } finally {
      setSaving(false)
    }
  }

  if (!allowed) {
    return (
      <div className="space-y-5 py-5">
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <ShieldAlert className="size-12 text-muted-foreground mb-4" />
          <h1 className="text-lg font-bold mb-2">{t("dataScope.accessDeniedTitle")}</h1>
          <p className="text-muted-foreground max-w-sm">{t("dataScope.accessDeniedDesc")}</p>
        </div>
      </div>
    )
  }

  return (
    <div className="space-y-5 py-5">
      <Button
        variant="ghost"
        className="px-2 text-muted-foreground"
        onClick={() => navigate(`/users/${userId}`)}
      >
        {t("dataScope.back")}
      </Button>

      <div>
        <h1 className="text-2xl font-heading font-bold">{t("dataScope.title")}</h1>
        <p className="text-sm text-muted-foreground mt-1">{t("dataScope.subtitle")}</p>
      </div>

      {error && (
        <div role="alert" className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {t("dataScope.loadError")}
        </div>
      )}

      {loading ? (
        <div className="space-y-4">
          <Skeleton className="h-32 w-full rounded-lg" />
          <Skeleton className="h-48 w-full rounded-lg" />
          <Skeleton className="h-48 w-full rounded-lg" />
        </div>
      ) : loaded ? (
        <div className="space-y-5">
          {/* Hierarchy scope */}
          <Card>
            <CardHeader>
              <CardTitle className="text-base font-bold">{t("dataScope.hierarchyTitle")}</CardTitle>
              <CardDescription>{t("dataScope.hierarchyDesc")}</CardDescription>
            </CardHeader>
            <CardContent className="space-y-2">
              <Label htmlFor="org-node">{t("dataScope.hierarchyNodeLabel")}</Label>
              <Input
                id="org-node"
                dir="ltr"
                value={orgNode}
                disabled={!canManage}
                onChange={(e) => setOrgNode(e.target.value)}
                placeholder={t("dataScope.hierarchyNodePlaceholder")}
              />
            </CardContent>
          </Card>

          {/* Parameter scope */}
          <Card>
            <CardHeader>
              <CardTitle className="text-base font-bold">{t("dataScope.parametersTitle")}</CardTitle>
              <CardDescription>{t("dataScope.parametersDesc")}</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              {assignments.length === 0 && (
                <p className="text-sm text-muted-foreground">{t("dataScope.noParameters")}</p>
              )}

              {assignments.map((assignment) => (
                <div key={assignment.parameterName} className="rounded-md border border-border p-3 space-y-2">
                  <div className="flex items-center justify-between">
                    <span className="text-sm font-medium" dir="ltr">{assignment.parameterName}</span>
                    {canManage && (
                      <Button
                        variant="ghost"
                        size="icon"
                        aria-label={`${t("dataScope.deleteRule")} ${assignment.parameterName}`}
                        onClick={() => removeParameter(assignment.parameterName)}
                      >
                        <X className="size-4" />
                      </Button>
                    )}
                  </div>
                  <ValueTags
                    values={assignment.allowedValues}
                    canManage={canManage}
                    onChange={(next) => setValues(assignment.parameterName, next)}
                  />
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

              {invalid && (
                <p role="alert" className="text-sm text-destructive">
                  {t("dataScope.invalid")}
                </p>
              )}
              {saved && (
                <p role="status" className="text-sm text-d2-dark dark:text-d2-light">
                  {t("dataScope.saved")}
                </p>
              )}

              {canManage && (
                <div className="flex justify-end">
                  <Button
                    className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
                    disabled={saving}
                    onClick={handleSaveScope}
                  >
                    {saving && <Loader2 className="size-4 animate-spin ms-2" />}
                    {saving ? t("dataScope.saving") : t("dataScope.save")}
                  </Button>
                </div>
              )}
            </CardContent>
          </Card>

          {/* Custom rules */}
          <Card>
            <CardHeader>
              <CardTitle className="text-base font-bold">{t("dataScope.customRulesTitle")}</CardTitle>
              <CardDescription>{t("dataScope.customRulesDesc")}</CardDescription>
            </CardHeader>
            <CardContent>
              <CustomRuleEditor
                key={rulesKey}
                userId={userId!}
                rules={rules}
                canManage={canManage}
                onChanged={async () => {
                  await load()
                  setRulesKey((k) => k + 1)
                }}
              />
            </CardContent>
          </Card>
        </div>
      ) : null}
    </div>
  )
}

interface ValueTagsProps {
  values: string[]
  canManage: boolean
  onChange: (next: string[]) => void
}

/** Tag-style multi-value editor for one parameter's allowed values. */
function ValueTags({ values, canManage, onChange }: ValueTagsProps) {
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
