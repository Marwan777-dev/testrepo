// SCR-06 Parameter Editor drawer (T061, US2).
//
// The five behaviours the acceptance criteria pin down, all implemented here:
//   AC-S6-01  switching the type between Range and List swaps the Range card for the List panel
//   AC-S6-02  the API field auto-suggests from the EN name and stays manually editable pre-lock
//   AC-S6-03  a duplicate API field is blocked with an inline error (server-adjudicated, VR-F06)
//   BR-09     a built-in's API field AND data type render permanently read-only
//   VR-F07    Range Min/Max are required and Min must be < Max
//
// BR-27 (`[PO-G25]`) drives the Mapping-support flag off the selected data type rather than off
// user intent: List forces it on and locks it, Text/Boolean/URL offer it off-by-default, and every
// other type disables it outright. `mappingSupportFor` mirrors the server rule so the control
// renders correctly without a round-trip; the server re-decides on save regardless.
//
// FR-GBL-03's unsaved-changes guard lives here too (spec.md names SCR-02/04/06). Dirtiness is
// **derived** by digesting the whole field set against the baseline captured on open — not by
// flipping a flag in each `onChange`, which goes stale the moment a field is added to the form.

import { useEffect, useMemo, useRef, useState } from "react"
import { useNavigate } from "react-router"
import { Trans, useTranslation } from "react-i18next"
import { Check, Loader2, Minus, Save, SquareArrowOutUpRight } from "lucide-react"

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
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet"
import { Switch } from "@/components/ui/switch"
import { cn } from "@/lib/utils"
import {
  DATA_TYPES,
  IntegrationHubApiError,
  mappingSupportFor,
  parameterFieldForCode,
  suggestApiField,
  type DataType,
  type Parameter,
  type ParameterFieldKey,
  type ParameterSaveInput,
  type ServiceChannel,
} from "@/features/integration-hub/api"

/** VR-F05 — parameter names are capped at 50 characters, both languages. */
const NAME_MAX_LENGTH = 50

/**
 * Read-only yes/no cell for a capability flag. **Brand cyan, not semantic green** — a capability
 * ("filterable: yes") is not a health state, and spending D2 green here weakens it everywhere it
 * means "good" (Two-Palette Rule). The shape carries the meaning too, so the column survives
 * greyscale and colour-blindness.
 *
 * Lives here rather than in the page because the drawer defines the flags; SCR-05's table imports it.
 */
export function FlagGlyph({ on, label }: { on: boolean; label: string }) {
  return (
    <span role="img" aria-label={label} className="inline-flex">
      {on ? (
        <Check className="size-4 text-nb-cyan-700 dark:text-nb-cyan-300" strokeWidth={3} />
      ) : (
        <Minus className="size-4 text-muted-foreground/40" strokeWidth={2.5} />
      )}
    </span>
  )
}

/** Every field the drawer owns, in one object so dirtiness can be digested wholesale. */
interface FormState {
  nameEn: string
  nameAr: string
  apiField: string
  dataType: DataType
  rangeMin: string
  rangeMax: string
  rangeUnit: string
  validationRule: string
  requiredByDefault: boolean
  filterable: boolean
  reportingVisibility: boolean
  dashboardVisibility: boolean
  mappingSupport: boolean
  channelIds: string[]
}

/** FR-S6-04's ratified defaults for a brand-new parameter (Searchable removed, `[PO-G26]`). */
function initialState(parameter?: Parameter): FormState {
  if (!parameter) {
    return {
      nameEn: "",
      nameAr: "",
      apiField: "",
      dataType: "text",
      rangeMin: "",
      rangeMax: "",
      rangeUnit: "",
      validationRule: "",
      requiredByDefault: false, // Off
      filterable: true, // On
      reportingVisibility: true, // On
      dashboardVisibility: false, // Off
      mappingSupport: mappingSupportFor("text").enabled, // per type (BR-27)
      channelIds: [],
    }
  }
  return {
    nameEn: parameter.nameEn,
    nameAr: parameter.nameAr,
    apiField: parameter.apiField,
    dataType: parameter.dataType,
    rangeMin: parameter.rangeMin?.toString() ?? "",
    rangeMax: parameter.rangeMax?.toString() ?? "",
    rangeUnit: parameter.rangeUnit ?? "",
    validationRule: parameter.validationRule ?? "",
    requiredByDefault: parameter.requiredByDefault,
    filterable: parameter.filterable,
    reportingVisibility: parameter.reportingVisibility,
    dashboardVisibility: parameter.dashboardVisibility,
    mappingSupport: parameter.mappingSupport,
    channelIds: [...parameter.channelIds].sort(),
  }
}

/** Order-insensitive digest of the form, so "dirty" survives a field being added later. */
function digest(state: FormState): string {
  return JSON.stringify({ ...state, channelIds: [...state.channelIds].sort() })
}

export interface ParameterDrawerProps {
  open: boolean
  /** Absent ⇒ create mode; the loaded row ⇒ edit mode. */
  parameter?: Parameter
  /** Active channels only — FR-S6-05's pills assign the parameter as *supported*. */
  channels: ServiceChannel[]
  saving: boolean
  /** FR-GBL-05 / BR-24 — P-07 sees the parameter, every write control is hidden. */
  readOnly?: boolean
  /** Rejects with `IntegrationHubApiError` so this component can map codes onto its fields. */
  onSave: (input: ParameterSaveInput) => Promise<void>
  onClose: () => void
}

export function ParameterDrawer({
  open,
  parameter,
  channels,
  saving,
  readOnly = false,
  onSave,
  onClose,
}: ParameterDrawerProps) {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const isRtl = i18n.language === "ar"
  const isEdit = parameter != null
  const isBuiltIn = parameter?.origin === "built_in"

  const [form, setForm] = useState<FormState>(() => initialState(parameter))
  const [baseline, setBaseline] = useState<string>(() => digest(initialState(parameter)))
  const [errors, setErrors] = useState<Partial<Record<ParameterFieldKey, string>>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [confirmLeaveOpen, setConfirmLeaveOpen] = useState(false)
  // AC-S6-02: auto-suggest stops the moment the user takes the field over, so their edit is
  // never overwritten by a later keystroke in the EN name.
  const [apiFieldTouched, setApiFieldTouched] = useState(isEdit)

  // Re-seed whenever the drawer opens (or swaps which parameter it edits). The Sheet keeps this
  // component mounted between opens, so without this the next open would show the last edit.
  useEffect(() => {
    if (!open) return
    const seeded = initialState(parameter)
    setForm(seeded)
    setBaseline(digest(seeded))
    setErrors({})
    setFormError(null)
    setApiFieldTouched(parameter != null)
  }, [open, parameter])

  const dirty = digest(form) !== baseline

  // BR-11 / `[PO-G27]` — both locks are server-decided; a built-in is locked on both counts.
  const apiFieldLocked = (parameter?.apiFieldLocked ?? false) || isBuiltIn || readOnly
  const typeLocked = (parameter?.dataTypeLocked ?? false) || isBuiltIn || readOnly

  const mappingRule = mappingSupportFor(form.dataType)

  const nameEnRef = useRef<HTMLInputElement>(null)
  const nameArRef = useRef<HTMLInputElement>(null)
  const apiFieldRef = useRef<HTMLInputElement>(null)
  const rangeMinRef = useRef<HTMLInputElement>(null)
  const rangeMaxRef = useRef<HTMLInputElement>(null)
  const refs = useMemo(
    () => ({
      nameEn: nameEnRef,
      nameAr: nameArRef,
      apiField: apiFieldRef,
      dataType: nameEnRef, // the type is a Select; fall back to the first field
      rangeMin: rangeMinRef,
      rangeMax: rangeMaxRef,
    }),
    [],
  )

  // Auto-focus the first invalid field on submit failure (design system: Forms).
  useEffect(() => {
    const order: ParameterFieldKey[] = ["nameEn", "nameAr", "apiField", "rangeMin", "rangeMax"]
    const first = order.find((key) => errors[key])
    if (first) refs[first].current?.focus()
  }, [errors, refs])

  function patch(next: Partial<FormState>) {
    setForm((prev) => ({ ...prev, ...next }))
  }

  function setNameEn(value: string) {
    // AC-S6-02 — the suggestion tracks the EN name until the user edits the field themselves.
    patch(apiFieldTouched ? { nameEn: value } : { nameEn: value, apiField: suggestApiField(value) })
  }

  /** BR-27 — the type decides the Mapping-support flag; a change re-derives it. */
  function setDataType(value: DataType) {
    const rule = mappingSupportFor(value)
    patch({
      dataType: value,
      mappingSupport: rule.changeable ? form.mappingSupport : rule.enabled,
      // Range bounds only apply to a Range parameter (`validation.range_not_applicable`).
      ...(value === "range" ? {} : { rangeMin: "", rangeMax: "", rangeUnit: "" }),
    })
  }

  function toggleChannel(channelId: string) {
    patch({
      channelIds: form.channelIds.includes(channelId)
        ? form.channelIds.filter((id) => id !== channelId)
        : [...form.channelIds, channelId],
    })
  }

  /** FR-GBL-03 — never drop unsaved edits silently. */
  function requestClose() {
    if (dirty && !readOnly) {
      setConfirmLeaveOpen(true)
      return
    }
    onClose()
  }

  /** Client-side pre-validation (VR-F05/F06/F07). The server re-validates all of it. */
  function validate(): boolean {
    const next: Partial<Record<ParameterFieldKey, string>> = {}
    if (!form.nameEn.trim()) next.nameEn = t("integrationHub.parameterDrawer.errors.nameEnRequired")
    if (!form.nameAr.trim()) next.nameAr = t("integrationHub.parameterDrawer.errors.nameArRequired")
    if (!form.apiField.trim())
      next.apiField = t("integrationHub.parameterDrawer.errors.apiFieldRequired")
    else if (!/^[a-z][a-z0-9_]*$/.test(form.apiField))
      next.apiField = t("integrationHub.parameterDrawer.errors.apiFieldFormat")

    if (form.dataType === "range") {
      const min = form.rangeMin.trim()
      const max = form.rangeMax.trim()
      if (!min) next.rangeMin = t("integrationHub.parameterDrawer.errors.rangeMinRequired")
      if (!max) next.rangeMax = t("integrationHub.parameterDrawer.errors.rangeMaxRequired")
      // VR-F07 — Min < Max. Reported on Min, the field the user is asked to lower.
      if (min && max && Number(min) >= Number(max))
        next.rangeMin = t("integrationHub.parameterDrawer.errors.rangeMinMax")
    }

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
        nameEn: form.nameEn.trim(),
        nameAr: form.nameAr.trim(),
        apiField: form.apiField.trim(),
        dataType: form.dataType,
        rangeMin: form.dataType === "range" ? Number(form.rangeMin) : undefined,
        rangeMax: form.dataType === "range" ? Number(form.rangeMax) : undefined,
        rangeUnit:
          form.dataType === "range" && form.rangeUnit.trim() ? form.rangeUnit.trim() : undefined,
        validationRule: form.validationRule.trim() || undefined,
        requiredByDefault: form.requiredByDefault,
        filterable: form.filterable,
        reportingVisibility: form.reportingVisibility,
        dashboardVisibility: form.dashboardVisibility,
        mappingSupport: form.mappingSupport,
        channelIds: form.channelIds,
      })
      // Saved state becomes the new baseline, so closing afterwards raises no guard.
      setBaseline(digest(form))
    } catch (error) {
      if (!(error instanceof IntegrationHubApiError)) {
        setFormError(t("integrationHub.parameterDrawer.errors.unexpected"))
        return
      }
      // API-05 `details` carries every accumulated failure; attach each to its own field so a
      // multi-error save surfaces all of them rather than only the first.
      const next: Partial<Record<ParameterFieldKey, string>> = {}
      const codes = error.details?.length ? error.details.map((d) => d.code) : [error.code]
      for (const code of codes) {
        const field = parameterFieldForCode(code)
        if (field) {
          next[field] = t(`integrationHub.parameterDrawer.serverErrors.${code}`, {
            defaultValue: error.message,
          })
        }
      }
      setErrors(next)
      if (Object.keys(next).length === 0) setFormError(error.message)
    }
  }

  const flagRows: {
    key: keyof Pick<
      FormState,
      | "requiredByDefault"
      | "filterable"
      | "reportingVisibility"
      | "dashboardVisibility"
      | "mappingSupport"
    >
    disabled: boolean
  }[] = [
    { key: "requiredByDefault", disabled: readOnly },
    { key: "filterable", disabled: readOnly },
    { key: "reportingVisibility", disabled: readOnly },
    { key: "dashboardVisibility", disabled: readOnly },
    // BR-27 — unavailable types and List both render the switch disabled; only its value differs.
    { key: "mappingSupport", disabled: readOnly || !mappingRule.changeable },
  ]

  return (
    <Sheet
      open={open}
      // FR-S6-01 — ✕, scrim click and Esc all route through the same guard.
      onOpenChange={(next) => {
        if (!next) requestClose()
      }}
    >
      <SheetContent
        // The drawer slides in from the reading-trailing edge, so it flips with the locale.
        // `side` is physical (the Sheet positions with `left`/`right`), hence the JS branch.
        side={isRtl ? "left" : "right"}
        // The base pins `data-[side=right]:sm:max-w-sm`; a bare `sm:max-w-xl` would not override
        // it (same property, different variant chain) and the drawer would stay 384px wide.
        className="data-[side=left]:sm:max-w-xl data-[side=right]:sm:max-w-xl"
        data-testid="parameter-drawer"
      >
        <form onSubmit={handleSubmit} className="flex min-h-0 flex-1 flex-col">
          <SheetHeader className="shrink-0 gap-1 border-b border-border pe-12">
            <SheetTitle className="text-base font-bold">
              {isEdit
                ? t("integrationHub.parameterDrawer.editTitle")
                : t("integrationHub.parameterDrawer.createTitle")}
            </SheetTitle>
            <SheetDescription className="text-xs leading-relaxed">
              {t("integrationHub.parameterDrawer.subtitle")}
            </SheetDescription>
          </SheetHeader>

          {/* `min-h-0` is what makes this the sole scroll container — without it a flex child
              refuses to shrink below its content and pushes the footer off-screen. */}
          <div className="min-h-0 flex-1 space-y-5 overflow-y-auto p-4">
            {isBuiltIn && (
              <Alert data-testid="parameter-builtin-notice">
                <AlertDescription className="text-xs leading-relaxed">
                  {t("integrationHub.parameterDrawer.builtInNotice")}
                </AlertDescription>
              </Alert>
            )}

            {readOnly && (
              <Alert data-testid="parameter-read-only">
                <AlertDescription className="text-xs leading-relaxed">
                  {t("integrationHub.parameterDrawer.readOnlyNotice")}
                </AlertDescription>
              </Alert>
            )}

            {formError && (
              <Alert variant="destructive" role="alert">
                <AlertDescription>{formError}</AlertDescription>
              </Alert>
            )}

            {/* Identity ------------------------------------------------------------------ */}
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="parameter-name-en">
                  {t("integrationHub.parameterDrawer.nameEn")}{" "}
                  <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="parameter-name-en"
                  ref={nameEnRef}
                  value={form.nameEn}
                  dir="ltr"
                  maxLength={NAME_MAX_LENGTH}
                  readOnly={readOnly}
                  className={cn("text-xs md:text-xs", readOnly && "bg-muted text-muted-foreground")}
                  placeholder={t("integrationHub.parameterDrawer.nameEnPlaceholder")}
                  aria-invalid={errors.nameEn ? true : undefined}
                  data-testid="parameter-name-en"
                  onChange={(e) => setNameEn(e.target.value)}
                />
                <p className="text-xs leading-relaxed text-muted-foreground">
                  {t("integrationHub.parameterDrawer.nameEnHelp")}
                </p>
                {errors.nameEn && (
                  <p className="text-sm text-destructive" role="alert">
                    {errors.nameEn}
                  </p>
                )}
              </div>

              <div className="flex flex-col gap-1.5">
                <Label htmlFor="parameter-name-ar">
                  {t("integrationHub.parameterDrawer.nameAr")}{" "}
                  <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="parameter-name-ar"
                  ref={nameArRef}
                  value={form.nameAr}
                  dir="rtl"
                  lang="ar"
                  maxLength={NAME_MAX_LENGTH}
                  readOnly={readOnly}
                  className={cn("text-xs md:text-xs", readOnly && "bg-muted text-muted-foreground")}
                  placeholder={t("integrationHub.parameterDrawer.nameArPlaceholder")}
                  aria-invalid={errors.nameAr ? true : undefined}
                  data-testid="parameter-name-ar"
                  onChange={(e) => patch({ nameAr: e.target.value })}
                />
                {errors.nameAr && (
                  <p className="text-sm text-destructive" role="alert">
                    {errors.nameAr}
                  </p>
                )}
              </div>

              <div className="flex flex-col gap-1.5 sm:col-span-2">
                <Label htmlFor="parameter-api-field">
                  {t("integrationHub.parameterDrawer.apiField")}{" "}
                  <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="parameter-api-field"
                  ref={apiFieldRef}
                  value={form.apiField}
                  dir="ltr"
                  readOnly={apiFieldLocked}
                  className={cn(
                    "font-mono text-xs md:text-xs",
                    apiFieldLocked && "bg-muted text-muted-foreground",
                  )}
                  placeholder={t("integrationHub.parameterDrawer.apiFieldPlaceholder")}
                  aria-invalid={errors.apiField ? true : undefined}
                  data-testid="parameter-api-field"
                  onChange={(e) => {
                    setApiFieldTouched(true)
                    patch({ apiField: e.target.value })
                  }}
                />
                <p className="text-xs leading-relaxed text-muted-foreground">
                  {apiFieldLocked && !readOnly
                    ? t("integrationHub.parameterDrawer.apiFieldLocked")
                    : t("integrationHub.parameterDrawer.apiFieldHelp")}
                </p>
                {errors.apiField && (
                  <p className="text-sm text-destructive" role="alert">
                    {errors.apiField}
                  </p>
                )}
              </div>

              <div className="flex flex-col gap-1.5 sm:col-span-2">
                <Label htmlFor="parameter-type">
                  {t("integrationHub.parameterDrawer.dataType")}{" "}
                  <span className="text-destructive">*</span>
                </Label>
                {/* The select offers exactly the 13 ratified types — `duration` and `identifier`
                    were evaluated and rejected (`[PO-G17]`), so they exist nowhere in DATA_TYPES. */}
                <Select
                  value={form.dataType}
                  disabled={typeLocked}
                  onValueChange={(value) => setDataType((value ?? "text") as DataType)}
                >
                  <SelectTrigger
                    id="parameter-type"
                    className="w-full"
                    data-testid="parameter-type"
                    aria-invalid={errors.dataType ? true : undefined}
                  >
                    <SelectValue>
                      {(value) => t(`integrationHub.dataTypes.${(value as DataType) ?? "text"}`)}
                    </SelectValue>
                  </SelectTrigger>
                  <SelectContent>
                    {DATA_TYPES.map((option) => (
                      <SelectItem
                        key={option}
                        value={option}
                        data-testid={`parameter-type-option-${option}`}
                      >
                        {t(`integrationHub.dataTypes.${option}`)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-xs leading-relaxed text-muted-foreground">
                  {typeLocked ? (
                    <Trans
                      i18nKey="integrationHub.parameterDrawer.dataTypeHelp"
                      components={{ b: <span className="font-semibold text-foreground" /> }}
                    />
                  ) : (
                    t("integrationHub.parameterDrawer.dataTypeHelpEditable")
                  )}
                </p>
                {errors.dataType && (
                  <p className="text-sm text-destructive" role="alert">
                    {errors.dataType}
                  </p>
                )}
              </div>
            </div>

            {/* AC-S6-01 — exactly one of these two panels is on screen at a time. --------- */}
            {form.dataType === "range" && (
              <section
                className="rounded-md border border-border bg-muted/30 p-4"
                data-testid="parameter-range-card"
              >
                <h3 className="mb-3 text-xs font-medium uppercase tracking-widest text-muted-foreground">
                  {t("integrationHub.parameterDrawer.rangeTitle")}
                </h3>
                <div className="grid gap-4 sm:grid-cols-3">
                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor="parameter-range-min">
                      {t("integrationHub.parameterDrawer.rangeMin")}{" "}
                      <span className="text-destructive">*</span>
                    </Label>
                    <Input
                      id="parameter-range-min"
                      ref={rangeMinRef}
                      type="number"
                      dir="ltr"
                      value={form.rangeMin}
                      readOnly={readOnly}
                      className={cn(
                        "text-xs tabular-nums md:text-xs",
                        readOnly && "bg-muted text-muted-foreground",
                      )}
                      aria-invalid={errors.rangeMin ? true : undefined}
                      data-testid="parameter-range-min"
                      onChange={(e) => patch({ rangeMin: e.target.value })}
                    />
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor="parameter-range-max">
                      {t("integrationHub.parameterDrawer.rangeMax")}{" "}
                      <span className="text-destructive">*</span>
                    </Label>
                    <Input
                      id="parameter-range-max"
                      ref={rangeMaxRef}
                      type="number"
                      dir="ltr"
                      value={form.rangeMax}
                      readOnly={readOnly}
                      className={cn(
                        "text-xs tabular-nums md:text-xs",
                        readOnly && "bg-muted text-muted-foreground",
                      )}
                      aria-invalid={errors.rangeMax ? true : undefined}
                      data-testid="parameter-range-max"
                      onChange={(e) => patch({ rangeMax: e.target.value })}
                    />
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor="parameter-range-unit">
                      {t("integrationHub.parameterDrawer.rangeUnit")}
                    </Label>
                    <Input
                      id="parameter-range-unit"
                      value={form.rangeUnit}
                      readOnly={readOnly}
                      className={cn(
                        "text-xs md:text-xs",
                        readOnly && "bg-muted text-muted-foreground",
                      )}
                      placeholder={t("integrationHub.parameterDrawer.rangeUnitPlaceholder")}
                      data-testid="parameter-range-unit"
                      onChange={(e) => patch({ rangeUnit: e.target.value })}
                    />
                  </div>
                </div>
                {/* One error slot for the pair: VR-F07's Min-vs-Max message is about the
                    relationship, not either field on its own. */}
                {(errors.rangeMin || errors.rangeMax) && (
                  <p className="mt-2 text-sm text-destructive" role="alert">
                    {errors.rangeMin ?? errors.rangeMax}
                  </p>
                )}
              </section>
            )}

            {form.dataType === "list" && (
              <section
                className="flex flex-col items-start gap-3 rounded-md border border-border bg-muted/30 p-4"
                data-testid="parameter-list-panel"
              >
                <p className="text-xs leading-relaxed text-muted-foreground">
                  {t("integrationHub.parameterDrawer.listPanel")}
                </p>
                {/* BR-12 — the drawer points at SCR-07 rather than duplicating the mapping table.
                    Routed through the guard so an unsaved edit isn't lost by following the link. */}
                <Button
                  type="button"
                  variant="secondary"
                  size="compact"
                  data-testid="parameter-open-mappings"
                  onClick={() => {
                    if (dirty && !readOnly) {
                      setConfirmLeaveOpen(true)
                      return
                    }
                    navigate("/integration-hub/mappings")
                  }}
                >
                  <SquareArrowOutUpRight className="size-4" />
                  {t("integrationHub.parameterDrawer.openMappings")}
                </Button>
              </section>
            )}

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="parameter-validation-rule">
                {t("integrationHub.parameterDrawer.validationRule")}
              </Label>
              <Input
                id="parameter-validation-rule"
                value={form.validationRule}
                dir="ltr"
                readOnly={readOnly}
                className={cn(
                  "font-mono text-xs md:text-xs",
                  readOnly && "bg-muted text-muted-foreground",
                )}
                placeholder={t("integrationHub.parameterDrawer.validationRulePlaceholder")}
                data-testid="parameter-validation-rule"
                onChange={(e) => patch({ validationRule: e.target.value })}
              />
              <p className="text-xs leading-relaxed text-muted-foreground">
                {t("integrationHub.parameterDrawer.validationRuleHelp")}
              </p>
            </div>

            {/* Usage flags — FR-S6-04 ---------------------------------------------------- */}
            <section className="space-y-3">
              <div className="flex items-center gap-3">
                <h3 className="text-xs font-medium uppercase tracking-widest text-muted-foreground">
                  {t("integrationHub.parameterDrawer.flagsTitle")}
                </h3>
                <div aria-hidden className="h-px flex-1 bg-border" />
              </div>
              <div className="space-y-3">
                {flagRows.map(({ key, disabled }) => (
                  <div key={key} className="flex items-start gap-3">
                    <Switch
                      id={`parameter-flag-${key}`}
                      checked={form[key]}
                      disabled={disabled}
                      data-testid={`parameter-flag-${key}`}
                      onCheckedChange={(checked) => patch({ [key]: checked === true } as Partial<FormState>)}
                    />
                    <div className="flex flex-col gap-1">
                      <Label htmlFor={`parameter-flag-${key}`}>
                        {t(`integrationHub.parameterDrawer.flags.${key}.label`)}
                      </Label>
                      <p className="text-xs leading-relaxed text-muted-foreground">
                        {t(`integrationHub.parameterDrawer.flags.${key}.help`)}
                      </p>
                    </div>
                  </div>
                ))}
              </div>
            </section>

            {/* Channel assignment — FR-S6-05 --------------------------------------------- */}
            <section className="space-y-3">
              <div className="flex items-center gap-3">
                <h3 className="text-xs font-medium uppercase tracking-widest text-muted-foreground">
                  {t("integrationHub.parameterDrawer.channelsTitle")}
                </h3>
                <div aria-hidden className="h-px flex-1 bg-border" />
              </div>
              <p className="text-xs leading-relaxed text-muted-foreground">
                <Trans
                  i18nKey="integrationHub.parameterDrawer.channelsHelp"
                  components={{ b: <span className="font-semibold text-foreground" /> }}
                />
              </p>
              {channels.length === 0 ? (
                <p className="text-xs text-muted-foreground">
                  {t("integrationHub.parameterDrawer.noChannels")}
                </p>
              ) : (
                // Pills, not a stacked checkbox column: these are short peer options, and a
                // vertical list of them would read as another form section.
                <div role="group" aria-label={t("integrationHub.parameterDrawer.channelsTitle")} className="flex flex-wrap gap-2">
                  {channels.map((channel) => {
                    const selected = form.channelIds.includes(channel.id)
                    return (
                      <button
                        key={channel.id}
                        type="button"
                        role="checkbox"
                        aria-checked={selected}
                        disabled={readOnly}
                        data-testid={`parameter-channel-${channel.channelId}`}
                        className={cn(
                          "inline-flex items-center gap-1.5 rounded-full border px-3 py-1.5 text-xs font-medium transition-colors duration-150 disabled:cursor-not-allowed disabled:opacity-50",
                          selected
                            ? "border-nb-cyan-200 bg-nb-cyan-100 text-nb-cyan-800 dark:border-nb-cyan-800 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200"
                            : "border-border bg-card text-muted-foreground hover:bg-muted",
                        )}
                        onClick={() => toggleChannel(channel.id)}
                      >
                        {selected && <Check className="size-3.5" strokeWidth={3} />}
                        <bdi dir="ltr">{channel.nameEn}</bdi>
                      </button>
                    )
                  })}
                </div>
              )}
            </section>
          </div>

          <SheetFooter className="shrink-0 flex-row justify-end gap-2 border-t border-border">
            <Button type="button" variant="outline" onClick={requestClose}>
              {readOnly
                ? t("integrationHub.parameterDrawer.close")
                : t("common.cancel", { defaultValue: "Cancel" })}
            </Button>
            {!readOnly && (
              <Button type="submit" disabled={saving} data-testid="parameter-save">
                {saving ? <Loader2 className="size-4 animate-spin" /> : <Save className="size-4" />}
                {isEdit
                  ? t("integrationHub.parameterDrawer.save")
                  : t("integrationHub.parameterDrawer.create")}
              </Button>
            )}
          </SheetFooter>
        </form>
      </SheetContent>

      {/* FR-GBL-03 — unsaved-changes guard. */}
      <AlertDialog open={confirmLeaveOpen} onOpenChange={setConfirmLeaveOpen}>
        <AlertDialogContent data-testid="parameter-unsaved-dialog">
          <AlertDialogHeader>
            <AlertDialogTitle>
              {t("integrationHub.parameterDrawer.unsavedTitle")}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {t("integrationHub.parameterDrawer.unsavedMessage")}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel data-testid="parameter-unsaved-stay">
              {t("integrationHub.parameterDrawer.unsavedStay")}
            </AlertDialogCancel>
            <AlertDialogAction
              data-testid="parameter-unsaved-leave"
              onClick={() => {
                setConfirmLeaveOpen(false)
                onClose()
              }}
            >
              {t("integrationHub.parameterDrawer.unsavedLeave")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Sheet>
  )
}
