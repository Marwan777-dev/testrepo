// SCR-04 Service Channel Create/Edit (T037, US1).
//
// Four behaviours the acceptance criteria pin down, all implemented here:
//   AC-S4-01  channel ID sanitises live as typed and caps at 19 chars (VR-F04)
//   AC-S4-02  the ID field is read-only once the channel has served its first 2xx request (BR-05)
//   AC-S4-03  Required is enabled only while Supported is on; clearing Supported clears Required
//             (FR-S4-04), and the live contract-summary alert's counts follow
//   VR-F02/F04 duplicate EN name / channel ID are blocked with an inline error
//
// Server errors arrive as an API-05 envelope whose `details` carry one entry per accumulated
// failure, so every inline message can be attached to its own field in one pass rather than
// surfacing only the first.
//
// This file carries BOTH roles the click-through splits across `pages/ServiceChannelFormPage.tsx`
// (load + save + access gate) and `components/ServiceChannelForm.tsx` (the fields): tasks.md gives
// SCR-04 one component (T037) and the route wires it directly (T039). The rendered output matches
// the design; only the file split differs.
//
// Two product-side additions the click-through does not carry, both deliberate:
//   - `readOnly` (P-07). The click-through's form page returns AccessDenied on `!canManage`, which
//     would deny P-07 outright. spec.md BR-24 / FR-GBL-05 require the mirrored read-only view, and
//     E2E `M13-E2E-06` asserts it — so access is gated on `canView` and write controls are hidden.
//   - the unsaved-changes guard, required by FR-GBL-03 (the click-through has none here).

import { useEffect, useMemo, useRef, useState } from "react"
import { useNavigate, useParams } from "react-router"
import { Trans, useTranslation } from "react-i18next"
import { toast } from "sonner"
import { Info, Loader2, Save, Search } from "lucide-react"

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Checkbox } from "@/components/ui/checkbox"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Skeleton } from "@/components/ui/skeleton"
import { Switch } from "@/components/ui/switch"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { Textarea } from "@/components/ui/textarea"
import { cn } from "@/lib/utils"
import { AccessDenied } from "@/features/integration-hub/components/AccessDenied"
import { useIntegrationHubAccess } from "@/features/integration-hub/hooks/useIntegrationHubAccess"
import { useServiceChannelForm } from "@/features/integration-hub/hooks/useServiceChannels"
import {
  CHANNEL_ID_MAX_LENGTH,
  IntegrationHubApiError,
  channelFieldForCode,
  sanitizeChannelId,
  type Parameter,
  type ServiceChannel,
  type ServiceChannelSaveInput,
} from "@/features/integration-hub/api"

/** Design-system UI Label: small-caps, tracked, muted — the table-header treatment. */
const TH = "text-xs font-medium uppercase tracking-widest text-muted-foreground"

type FieldKey = "nameEn" | "nameAr" | "channelId"

/**
 * Route component for `/integration-hub/service-channels/new` and `/:id`. Owns the data load and
 * the save round-trip; `ServiceChannelForm` below owns the fields and validation.
 */
export default function ServiceChannelFormPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { id } = useParams<{ id: string }>()
  const access = useIntegrationHubAccess()
  const { channel, parameters, loading, loadError, saving, save } = useServiceChannelForm(id)

  async function handleSave(input: ServiceChannelSaveInput) {
    // Let the error propagate — the form maps API-05 codes onto its own fields.
    const saved = await save(input)
    toast.success(
      id
        ? t("integrationHub.channelForm.savedToast", { name: saved.nameEn })
        : t("integrationHub.channelForm.createdToast", { name: saved.nameEn }),
    )
    navigate("/integration-hub/service-channels")
  }

  // Hydration guard first: `ready` false means the session hasn't resolved, not that the persona
  // lacks a grant — deciding on it would flash access-denied at an allowed user.
  if (!access.ready) return <FormSkeleton />

  if (!access.canView("serviceChannels")) {
    return <AccessDenied screenName={t("integrationHub.channels.title")} />
  }

  if (loading) return <FormSkeleton withTable />

  if (loadError) {
    return (
      <div className="space-y-5 py-5">
        <div className="flex flex-col items-center justify-center rounded-lg border border-border bg-card py-16 text-center shadow-sm dark:shadow-none">
          <h2 className="mb-2 text-lg font-bold">
            {t("integrationHub.channelForm.loadErrorTitle")}
          </h2>
          <p className="text-sm text-muted-foreground">
            {t("integrationHub.channelForm.loadErrorHint")}
          </p>
        </div>
      </div>
    )
  }

  return (
    <ServiceChannelForm
      // Remount when switching between create and a specific channel so the form's seeded
      // state is rebuilt rather than carried over from the previous route.
      key={id ?? "new"}
      channel={channel}
      parameters={parameters}
      saving={saving}
      readOnly={access.isReadOnly("serviceChannels")}
      onSave={handleSave}
    />
  )
}

function FormSkeleton({ withTable = false }: { withTable?: boolean }) {
  return (
    <div className="space-y-5 py-5">
      <Skeleton className="h-9 w-72" />
      <Skeleton className="h-4 w-96" />
      <Skeleton className="h-64 w-full" />
      {withTable && <Skeleton className="h-96 w-full" />}
    </div>
  )
}

interface ContractState {
  supported: boolean
  required: boolean
}

export interface ServiceChannelFormProps {
  /** Absent in create mode; the pre-filled channel (with its contract) in edit mode. */
  channel?: ServiceChannel
  /** The enabled parameter catalogue — SCR-04 lists only active parameters. */
  parameters: Parameter[]
  saving: boolean
  /** FR-GBL-05 / BR-24 — P-07 sees the channel, every write control is hidden. */
  readOnly?: boolean
  /** Rejects with `IntegrationHubApiError` so this component can map codes onto fields. */
  onSave: (input: ServiceChannelSaveInput) => Promise<void>
}

export function ServiceChannelForm({
  channel,
  parameters,
  saving,
  readOnly = false,
  onSave,
}: ServiceChannelFormProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const isEdit = channel != null

  const [nameEn, setNameEn] = useState(channel?.nameEn ?? "")
  const [nameAr, setNameAr] = useState(channel?.nameAr ?? "")
  const [channelId, setChannelId] = useState(channel?.channelId ?? "")
  const [description, setDescription] = useState(channel?.description ?? "")
  const [active, setActive] = useState(channel?.active ?? true)
  const [filter, setFilter] = useState("")
  const [errors, setErrors] = useState<Partial<Record<FieldKey, string>>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [dirty, setDirty] = useState(false)
  const [confirmLeaveOpen, setConfirmLeaveOpen] = useState(false)

  // Contract state keyed by parameter id, seeded from the channel's existing contract.
  const [contract, setContract] = useState<Record<string, ContractState>>(() => {
    const seed: Record<string, ContractState> = {}
    for (const row of channel?.contract ?? []) {
      seed[row.parameterId] = { supported: row.supported, required: row.required }
    }
    return seed
  })

  // BR-05: the ID is permanently locked once the channel has served a 2xx request.
  const idLocked = (channel?.channelIdLocked ?? false) || readOnly

  const nameEnRef = useRef<HTMLInputElement>(null)
  const nameArRef = useRef<HTMLInputElement>(null)
  const channelIdRef = useRef<HTMLInputElement>(null)
  const refs: Record<FieldKey, React.RefObject<HTMLInputElement | null>> = useMemo(
    () => ({ nameEn: nameEnRef, nameAr: nameArRef, channelId: channelIdRef }),
    [],
  )

  // Auto-focus the first invalid field on submit failure (design system: Forms).
  useEffect(() => {
    const first = (["nameEn", "nameAr", "channelId"] as FieldKey[]).find((key) => errors[key])
    if (first) refs[first].current?.focus()
  }, [errors, refs])

  const supportedCount = useMemo(
    () => Object.values(contract).filter((row) => row.supported).length,
    [contract],
  )
  const requiredCount = useMemo(
    () => Object.values(contract).filter((row) => row.supported && row.required).length,
    [contract],
  )

  const visibleParameters = useMemo(() => {
    const q = filter.trim().toLowerCase()
    if (!q) return parameters
    return parameters.filter(
      (p) =>
        p.nameEn.toLowerCase().includes(q) ||
        p.nameAr.toLowerCase().includes(q) ||
        p.apiField.toLowerCase().includes(q),
    )
  }, [parameters, filter])

  function setSupported(parameterId: string, supported: boolean) {
    setDirty(true)
    setContract((prev) => ({
      ...prev,
      // FR-S4-04: clearing Supported force-clears Required in the same update.
      [parameterId]: { supported, required: supported ? (prev[parameterId]?.required ?? false) : false },
    }))
  }

  function setRequired(parameterId: string, required: boolean) {
    setDirty(true)
    setContract((prev) => {
      const row = prev[parameterId]
      if (!row?.supported) return prev // Required is only meaningful while Supported is on.
      return { ...prev, [parameterId]: { supported: true, required } }
    })
  }

  /** FR-GBL-03 — never drop unsaved edits silently on the way out. */
  function leave() {
    if (dirty) {
      setConfirmLeaveOpen(true)
      return
    }
    navigate("/integration-hub/service-channels")
  }

  /** Client-side pre-validation (VR-F02/F03/F04). The server re-validates all of it. */
  function validate(): boolean {
    const next: Partial<Record<FieldKey, string>> = {}
    if (!nameEn.trim()) next.nameEn = t("integrationHub.channelForm.errors.nameEnRequired")
    else if (nameEn.length > 50) next.nameEn = t("integrationHub.channelForm.errors.nameEnTooLong")
    if (!nameAr.trim()) next.nameAr = t("integrationHub.channelForm.errors.nameArRequired")
    if (!channelId.trim()) next.channelId = t("integrationHub.channelForm.errors.channelIdRequired")
    setErrors(next)
    return Object.keys(next).length === 0
  }

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault()
    setFormError(null)
    if (readOnly) return
    if (!validate()) return

    try {
      await onSave({
        nameEn: nameEn.trim(),
        nameAr: nameAr.trim(),
        channelId,
        description: description.trim() || undefined,
        active,
        contract: Object.entries(contract)
          .filter(([, row]) => row.supported)
          .map(([parameterId, row]) => ({
            parameterId,
            supported: true,
            required: row.required,
          })),
      })
      setDirty(false)
    } catch (error) {
      if (!(error instanceof IntegrationHubApiError)) {
        setFormError(t("integrationHub.channelForm.errors.unexpected"))
        return
      }
      // API-05 `details` carries every accumulated failure; attach each to its own field.
      const next: Partial<Record<FieldKey, string>> = {}
      const codes: string[] = error.details?.length
        ? error.details.map((d) => d.code)
        : [error.code]
      for (const code of codes) {
        const field = channelFieldForCode(code)
        if (field) {
          next[field] = t(`integrationHub.channelForm.serverErrors.${code}`, {
            defaultValue: error.message,
          })
        }
      }
      setErrors(next)
      if (Object.keys(next).length === 0) setFormError(error.message)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-5 py-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
        <div className="min-w-0">
          <h1 className="font-heading text-2xl font-bold">
            {isEdit
              ? t("integrationHub.channelForm.editTitle")
              : t("integrationHub.channelForm.createTitle")}
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            {t("integrationHub.channelForm.subtitle")}
          </p>
        </div>
        <div className="flex items-center gap-2">
          {/* Read-only (P-07): the pair collapses to a single dismiss, since there is nothing to
              cancel out of and nothing to save. */}
          <Button type="button" variant="outline" onClick={leave}>
            {readOnly ? t("integrationHub.channelForm.close") : t("common.cancel")}
          </Button>
          {!readOnly && (
            <Button type="submit" disabled={saving} data-testid="channel-save">
              {saving ? <Loader2 className="size-4 animate-spin" /> : <Save className="size-4" />}
              {isEdit
                ? t("integrationHub.channelForm.save")
                : t("integrationHub.channelForm.create")}
            </Button>
          )}
        </div>
      </div>

      {readOnly && (
        <Alert data-testid="channel-read-only">
          <AlertDescription>{t("integrationHub.channelForm.readOnlyNotice")}</AlertDescription>
        </Alert>
      )}

      {formError && (
        <Alert variant="destructive" role="alert">
          <AlertDescription>{formError}</AlertDescription>
        </Alert>
      )}

      {/* Two columns, matching the ratified prototype: identity + the live summary on the start
          side, the contract table on the end side. The summary sits beside the toggles that move
          it, so a change and its consequence are visible together without scrolling. */}
      <div className="grid gap-5 lg:grid-cols-2 lg:items-start">
        <div className="space-y-5">
          {/* Identity — FR-S4-01. No section header: the page title already says what this is,
              and the prototype's baseline drops it. */}
          <Card>
            <CardContent className="grid gap-4 md:grid-cols-2">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="channel-name-en">
                  {t("integrationHub.channelForm.nameEn")}{" "}
                  <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="channel-name-en"
                  ref={nameEnRef}
                  value={nameEn}
                  maxLength={50}
                  dir="ltr"
                  readOnly={readOnly}
                  className={cn("text-xs md:text-xs", readOnly && "bg-muted text-muted-foreground")}
                  placeholder={t("integrationHub.channelForm.nameEnPlaceholder")}
                  aria-invalid={errors.nameEn ? true : undefined}
                  onChange={(e) => {
                    setDirty(true)
                    setNameEn(e.target.value)
                  }}
                />
                <p className="text-xs leading-relaxed text-muted-foreground">
                  {t("integrationHub.channelForm.nameEnHelp")}
                </p>
                {errors.nameEn && (
                  <p className="text-sm text-destructive" role="alert">
                    {errors.nameEn}
                  </p>
                )}
              </div>

              <div className="flex flex-col gap-1.5">
                <Label htmlFor="channel-name-ar">
                  {t("integrationHub.channelForm.nameAr")}{" "}
                  <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="channel-name-ar"
                  ref={nameArRef}
                  value={nameAr}
                  maxLength={50}
                  dir="rtl"
                  lang="ar"
                  readOnly={readOnly}
                  className={cn("text-xs md:text-xs", readOnly && "bg-muted text-muted-foreground")}
                  placeholder={t("integrationHub.channelForm.nameArPlaceholder")}
                  aria-invalid={errors.nameAr ? true : undefined}
                  onChange={(e) => {
                    setDirty(true)
                    setNameAr(e.target.value)
                  }}
                />
                {errors.nameAr && (
                  <p className="text-sm text-destructive" role="alert">
                    {errors.nameAr}
                  </p>
                )}
              </div>

              {/* Full width below the names — the ID's help text is long enough that a half-width
                  column wraps it to four lines. */}
              <div className="flex flex-col gap-1.5 md:col-span-2">
                <Label htmlFor="channel-id">
                  {t("integrationHub.channelForm.channelId")}{" "}
                  <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="channel-id"
                  ref={channelIdRef}
                  value={channelId}
                  dir="ltr"
                  readOnly={idLocked}
                  maxLength={CHANNEL_ID_MAX_LENGTH}
                  className={cn(
                    "font-mono text-xs md:text-xs",
                    idLocked && "bg-muted text-muted-foreground",
                  )}
                  placeholder={t("integrationHub.channelForm.channelIdPlaceholder")}
                  aria-invalid={errors.channelId ? true : undefined}
                  data-testid="channel-id-input"
                  // AC-S4-01 — strip disallowed characters live and cap at 19.
                  onChange={(e) => {
                    setDirty(true)
                    setChannelId(sanitizeChannelId(e.target.value))
                  }}
                />
                <p className="text-xs leading-relaxed text-muted-foreground">
                  {channel?.channelIdLocked
                    ? t("integrationHub.channelForm.channelIdLocked")
                    : t("integrationHub.channelForm.channelIdHelp")}
                </p>
                {errors.channelId && (
                  <p className="text-sm text-destructive" role="alert">
                    {errors.channelId}
                  </p>
                )}
              </div>

              <div className="flex flex-col gap-1.5 md:col-span-2">
                <Label htmlFor="channel-description">
                  {t("integrationHub.channelForm.description")}
                </Label>
                <Textarea
                  id="channel-description"
                  value={description}
                  rows={3}
                  readOnly={readOnly}
                  className={cn("text-xs md:text-xs", readOnly && "bg-muted text-muted-foreground")}
                  placeholder={t("integrationHub.channelForm.descriptionPlaceholder")}
                  onChange={(e) => {
                    setDirty(true)
                    setDescription(e.target.value)
                  }}
                />
              </div>

              <div className="flex items-start gap-3 md:col-span-2">
                <Switch
                  id="channel-active"
                  checked={active}
                  disabled={readOnly}
                  data-testid="channel-active"
                  onCheckedChange={(checked) => {
                    setDirty(true)
                    setActive(checked === true)
                  }}
                />
                <div className="flex flex-col gap-1">
                  <Label htmlFor="channel-active">{t("integrationHub.channelForm.active")}</Label>
                  <p className="text-xs leading-relaxed text-muted-foreground">
                    {t("integrationHub.channelForm.activeHelp")}
                  </p>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Live contract summary — FR-S4-03. An info-tinted banner rather than a bare bordered
              strip: it reports a consequence (E-1002 rejections), so it should read as guidance. */}
          <div
            className="flex items-start gap-3 rounded-md border border-nb-cyan-200 bg-nb-cyan-100/60 p-4 dark:border-nb-cyan-800 dark:bg-nb-cyan-900/25"
            data-testid="contract-summary"
          >
            <Info className="mt-0.5 size-4 shrink-0 text-nb-cyan-800 dark:text-nb-cyan-200" />
            <p className="text-xs leading-relaxed text-foreground">
              <span className="font-semibold">
                {t("integrationHub.channelForm.contractSummaryLabel")}
              </span>{" "}
              {t("integrationHub.channelForm.contractSummary", {
                supported: supportedCount,
                required: requiredCount,
              })}
            </p>
          </div>
        </div>

        {/* Parameter contract — FR-S4-04 */}
        <Card>
          <CardHeader>
            <CardTitle>{t("integrationHub.channelForm.contractTitle")}</CardTitle>
            <CardDescription className="text-xs leading-relaxed">
              <Trans
                i18nKey="integrationHub.channelForm.contractDescription"
                components={{
                  s: <span className="font-semibold text-foreground" />,
                  r: <span className="font-medium text-foreground" />,
                }}
              />
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {/* Search-affordance filter: the magnifier carries the meaning, so the label is
                screen-reader-only (the accessible name is still present). */}
            <div className="relative">
              <Search className="pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                id="contract-filter"
                className="ps-9 text-xs md:text-xs"
                value={filter}
                aria-label={t("integrationHub.channelForm.filterLabel")}
                placeholder={t("integrationHub.channelForm.filterPlaceholder")}
                onChange={(e) => setFilter(e.target.value)}
              />
            </div>

            <div className="overflow-hidden rounded-lg border border-border bg-card">
              <Table>
                <TableHeader className="sticky top-0 z-10">
                  <TableRow>
                    <TableHead className={cn(TH, "w-[40%]")}>
                      {t("integrationHub.channelForm.colParameter")}
                    </TableHead>
                    <TableHead className={cn(TH, "w-[24%]")}>
                      {t("integrationHub.channelForm.colType")}
                    </TableHead>
                    <TableHead className={cn(TH, "w-[18%]")}>
                      {t("integrationHub.channelForm.colSupported")}
                    </TableHead>
                    <TableHead className={cn(TH, "w-[18%]")}>
                      {t("integrationHub.channelForm.colRequired")}
                    </TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {visibleParameters.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={4} className="py-10 text-center text-muted-foreground">
                        {t("integrationHub.channelForm.noParameters")}
                      </TableCell>
                    </TableRow>
                  )}
                  {visibleParameters.map((parameter) => {
                    const row = contract[parameter.id] ?? { supported: false, required: false }
                    return (
                      <TableRow key={parameter.id} className="hover:bg-muted/50">
                        <TableCell>
                          {/* Name over the API field it maps to. The API field is what the
                              integrator actually puts in a payload, so it belongs next to the
                              name — the Arabic name that used to sit here duplicated the primary
                              and pushed the useful identifier into its own column. */}
                          <div className="flex min-w-0 flex-col items-start gap-0.5">
                            <bdi dir="ltr" className="max-w-full truncate font-medium">
                              {parameter.nameEn}
                            </bdi>
                            <code
                              dir="ltr"
                              className="max-w-full truncate font-mono text-xs text-muted-foreground"
                            >
                              {parameter.apiField}
                            </code>
                          </div>
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {t(`integrationHub.dataTypes.${parameter.dataType}`)}
                        </TableCell>
                        <TableCell>
                          {/* A toggle, not a checkbox: Supported turns a capability on/off for
                              this channel, and it gates Required beside it. Required stays a
                              checkbox — it qualifies the row rather than switching it on. */}
                          <Switch
                            checked={row.supported}
                            disabled={readOnly}
                            aria-label={t("integrationHub.channelForm.supportedFor", {
                              name: parameter.nameEn,
                            })}
                            data-testid={`supported-${parameter.apiField}`}
                            onCheckedChange={(checked) =>
                              setSupported(parameter.id, checked === true)
                            }
                          />
                        </TableCell>
                        <TableCell>
                          <Checkbox
                            checked={row.supported && row.required}
                            // AC-S4-03 — Required is disabled while Supported is off.
                            disabled={readOnly || !row.supported}
                            aria-label={t("integrationHub.channelForm.requiredFor", {
                              name: parameter.nameEn,
                            })}
                            data-testid={`required-${parameter.apiField}`}
                            onCheckedChange={(checked) =>
                              setRequired(parameter.id, checked === true)
                            }
                          />
                        </TableCell>
                      </TableRow>
                    )
                  })}
                </TableBody>
              </Table>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* FR-GBL-03 — unsaved-changes guard. */}
      <AlertDialog open={confirmLeaveOpen} onOpenChange={setConfirmLeaveOpen}>
        <AlertDialogContent data-testid="channel-unsaved-dialog">
          <AlertDialogHeader>
            <AlertDialogTitle>{t("integrationHub.channelForm.unsavedTitle")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("integrationHub.channelForm.unsavedMessage")}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel data-testid="channel-unsaved-stay">
              {t("integrationHub.channelForm.unsavedStay")}
            </AlertDialogCancel>
            <AlertDialogAction
              data-testid="channel-unsaved-leave"
              onClick={() => {
                setConfirmLeaveOpen(false)
                navigate("/integration-hub/service-channels")
              }}
            >
              {t("integrationHub.channelForm.unsavedLeave")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </form>
  )
}
