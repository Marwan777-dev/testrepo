// SCR-05 Parameters list (T062, US2).
//
// FR-S5-01  origin tabs (All / Built-in / Custom, live counts) + name/API-field search + type
//           filter, **AND-combined**. The counts stay global (AC-S5-01) — they come off the
//           response, not off the filtered rows, so narrowing by type never moves a tab count.
// FR-S5-02  columns: Parameter (dimmed when disabled) · API field chip · Type · Origin badge ·
//           Enabled toggle · Required/Filterable/Reporting/Dashboard glyphs · Mapping · Channels ·
//           row action.
// FR-S5-03  the inline Enabled toggle is guarded by Dialog D-6 (BR-10) and audited server-side.
// FR-S5-04  New parameter → the SCR-06 drawer; the per-row "Mapped" link → SCR-07.
//
// BR-09 shows up as an **absence**: no delete control exists on any row, built-in or custom, and
// there is no DELETE endpoint behind one either.

import { useState } from "react"
import { useNavigate } from "react-router"
import { useTranslation } from "react-i18next"
import { Eye, Pencil, Plus, Table2 } from "lucide-react"
import { toast } from "sonner"

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
import { Badge } from "@/components/ui/badge"
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
import { Tabs, TabsCountPill, TabsListSegmented, TabsTrigger } from "@/components/ui/tabs"
import { cn } from "@/lib/utils"
import { AccessDenied } from "@/features/integration-hub/components/AccessDenied"
import { FlagGlyph, ParameterDrawer } from "@/features/integration-hub/components/ParameterDrawer"
import { useIntegrationHubAccess } from "@/features/integration-hub/hooks/useIntegrationHubAccess"
import {
  useParameterEditor,
  useParameters,
  type OriginTab,
  type TypeFilter,
} from "@/features/integration-hub/hooks/useParameters"
import {
  DATA_TYPES,
  type Parameter,
  type ParameterReference,
  type ParameterSaveInput,
} from "@/features/integration-hub/api"

/** Design-system UI Label: small-caps, tracked, muted — the table-header treatment. */
const TH = "text-xs font-medium uppercase tracking-widest text-muted-foreground"

const ORIGIN_TABS: OriginTab[] = ["all", "built_in", "custom"]
const ORIGIN_TAB_LABEL: Record<OriginTab, string> = {
  all: "integrationHub.parameters.tabAll",
  built_in: "integrationHub.parameters.tabBuiltIn",
  custom: "integrationHub.parameters.tabCustom",
}

/** BR-10's withheld disable, held until the user confirms it in Dialog D-6. */
interface PendingDisable {
  parameter: Parameter
  references: ParameterReference[]
}

export default function AllParametersPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  // FR-GBL-05 — P-07 sees this screen read-only (BR-24): no create, View instead of Edit.
  const access = useIntegrationHubAccess()
  const canManage = access.canManage("parameters")

  const {
    items,
    counts,
    truncated,
    loading,
    error,
    origin,
    setOrigin,
    search,
    setSearch,
    type,
    setType,
    isFiltered,
    clearFilters,
    reload,
    setEnabled,
  } = useParameters()
  const { channels, saving, save } = useParameterEditor()

  const [drawerOpen, setDrawerOpen] = useState(false)
  const [editing, setEditing] = useState<Parameter | undefined>(undefined)
  const [pendingDisable, setPendingDisable] = useState<PendingDisable | null>(null)

  async function handleToggle(parameter: Parameter, enabled: boolean) {
    try {
      const result = await setEnabled(parameter, enabled)
      if (result.requiresConfirmation) {
        // Nothing was written — BR-10 wants the consequences on screen before the change.
        setPendingDisable({ parameter, references: result.references })
        return
      }
      toast.success(
        enabled
          ? t("integrationHub.parameters.enabledToast", { name: parameter.nameEn })
          : t("integrationHub.parameters.disabledToast", { name: parameter.nameEn }),
      )
    } catch {
      toast.error(t("integrationHub.parameters.toggleError"))
    }
  }

  async function confirmDisable() {
    if (!pendingDisable) return
    const { parameter } = pendingDisable
    setPendingDisable(null)
    try {
      await setEnabled(parameter, false, true)
      toast.success(t("integrationHub.parameters.disabledToast", { name: parameter.nameEn }))
    } catch {
      toast.error(t("integrationHub.parameters.toggleError"))
    }
  }

  async function handleSave(input: ParameterSaveInput) {
    // Let the error propagate — the drawer maps API-05 codes onto its own fields.
    const saved = await save(input, editing?.id)
    toast.success(
      editing
        ? t("integrationHub.parameterDrawer.savedToast", { name: saved.nameEn })
        : t("integrationHub.parameterDrawer.createdToast", { name: saved.nameEn }),
    )
    setDrawerOpen(false)
    setEditing(undefined)
    await reload()
  }

  // Hydration guard: `ready` false means the session hasn't resolved, not that the persona lacks
  // a grant — deciding on it would flash access-denied at an allowed user.
  if (!access.ready) {
    return (
      <div className="space-y-5 py-5">
        <Skeleton className="h-9 w-72" />
        <Skeleton className="h-4 w-96" />
        <Skeleton className="h-64 w-full" />
      </div>
    )
  }

  // FR-GBL-02 — a persona with no view grant reaching this route directly (deep link, bookmark).
  if (!access.canView("parameters")) {
    return <AccessDenied screenName={t("integrationHub.parameters.title")} />
  }

  return (
    <div className="space-y-5 py-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
        <div className="min-w-0">
          <h1 className="font-heading text-2xl font-bold">
            {t("integrationHub.parameters.title")}
          </h1>
          <p className="mt-1 max-w-2xl text-sm leading-relaxed text-muted-foreground">
            {t("integrationHub.parameters.description")}
          </p>
        </div>
        <div className="flex items-center gap-2">
          {/* Secondary, not a second filled blue — the page keeps exactly one primary. */}
          <Button
            variant="secondary"
            onClick={() => navigate("/integration-hub/mappings")}
            data-testid="manage-mappings"
          >
            {t("integrationHub.parameters.manageMappings")}
          </Button>
          {canManage && (
            <Button
              data-testid="new-parameter"
              onClick={() => {
                setEditing(undefined)
                setDrawerOpen(true)
              }}
            >
              <Plus className="size-4" />
              {t("integrationHub.parameters.newParameter")}
            </Button>
          )}
        </div>
      </div>

      {/* FR-S5-01 — origin tabs. There is no TabsContent: all three tabs render the same table
          below, only the query behind it changes (the Parameters pattern in CLAUDE.md). */}
      <Tabs value={origin} onValueChange={(value) => setOrigin((value ?? "all") as OriginTab)}>
        <TabsListSegmented>
          {ORIGIN_TABS.map((tab) => (
            <TabsTrigger key={tab} value={tab} data-testid={`tab-${tab}`}>
              {t(ORIGIN_TAB_LABEL[tab])}
              <TabsCountPill
                count={
                  counts == null
                    ? null
                    : tab === "all"
                      ? counts.all
                      : tab === "built_in"
                        ? counts.builtIn
                        : counts.custom
                }
              />
            </TabsTrigger>
          ))}
        </TabsListSegmented>
      </Tabs>

      <div className="flex flex-col gap-4 sm:flex-row sm:items-end">
        <div className="flex flex-1 flex-col gap-1.5 sm:max-w-sm">
          <Label htmlFor="parameter-search">{t("integrationHub.parameters.searchLabel")}</Label>
          <Input
            id="parameter-search"
            value={search}
            placeholder={t("integrationHub.parameters.searchPlaceholder")}
            data-testid="parameter-search"
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        {/* `gap-1.5`, not `space-y-1.5`: base-ui's Select renders a trailing hidden node, so
            `space-y-*` lands a stray 6px bottom margin on the trigger and lifts it out of the
            `sm:items-end` alignment with the search input beside it. */}
        <div className="flex flex-col gap-1.5 sm:w-48">
          <Label htmlFor="parameter-type-filter">{t("integrationHub.parameters.typeLabel")}</Label>
          <Select value={type} onValueChange={(value) => setType((value ?? "all") as TypeFilter)}>
            <SelectTrigger id="parameter-type-filter" className="w-full" data-testid="parameter-type-filter">
              <SelectValue>
                {(value) =>
                  value == null || value === "all"
                    ? t("integrationHub.parameters.typeAll")
                    : t(`integrationHub.dataTypes.${value as string}`)
                }
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all" data-testid="parameter-type-filter-all">
                {t("integrationHub.parameters.typeAll")}
              </SelectItem>
              {DATA_TYPES.map((option) => (
                <SelectItem
                  key={option}
                  value={option}
                  data-testid={`parameter-type-filter-${option}`}
                >
                  {t(`integrationHub.dataTypes.${option}`)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      {error ? (
        <div className="flex flex-col items-center justify-center rounded-lg border border-border bg-card py-16 text-center shadow-sm dark:shadow-none">
          <h2 className="mb-2 text-lg font-bold">{t("integrationHub.parameters.errorTitle")}</h2>
          <p className="text-sm text-muted-foreground">
            {t("integrationHub.parameters.errorHint")}
          </p>
        </div>
      ) : (
        // `overflow-hidden` is required, not cosmetic: Table ships its own overflow-x-auto div and
        // the sticky header paints an opaque band, both of which push square corners past the
        // card's rounded border without it.
        <div className="overflow-hidden rounded-lg border border-border bg-card shadow-sm dark:shadow-none">
          <Table>
            <TableHeader className="sticky top-0 z-10">
              <TableRow>
                <TableHead className={cn(TH, "w-[22%]")}>
                  {t("integrationHub.parameters.colParameter")}
                </TableHead>
                <TableHead className={cn(TH, "w-[14%]")}>
                  {t("integrationHub.parameters.colApiField")}
                </TableHead>
                <TableHead className={cn(TH, "w-[10%]")}>
                  {t("integrationHub.parameters.colType")}
                </TableHead>
                <TableHead className={cn(TH, "w-[9%]")}>
                  {t("integrationHub.parameters.colOrigin")}
                </TableHead>
                <TableHead className={cn(TH, "w-[8%]")}>
                  {t("integrationHub.parameters.colEnabled")}
                </TableHead>
                <TableHead className={cn(TH, "w-[6%] text-center")}>
                  {t("integrationHub.parameters.colRequired")}
                </TableHead>
                <TableHead className={cn(TH, "w-[6%] text-center")}>
                  {t("integrationHub.parameters.colFilterable")}
                </TableHead>
                <TableHead className={cn(TH, "w-[6%] text-center")}>
                  {t("integrationHub.parameters.colReporting")}
                </TableHead>
                <TableHead className={cn(TH, "w-[6%] text-center")}>
                  {t("integrationHub.parameters.colDashboard")}
                </TableHead>
                <TableHead className={cn(TH, "w-[8%]")}>
                  {t("integrationHub.parameters.colMapping")}
                </TableHead>
                <TableHead className={cn(TH, "w-[7%] text-end")}>
                  {t("integrationHub.parameters.colChannels")}
                </TableHead>
                {/* Edit sits directly in the row, so this column carries no visible label — the
                    accessible name is kept for screen readers rather than dropped. */}
                <TableHead className="w-16 text-center">
                  <span className="sr-only">{t("integrationHub.parameters.colActions")}</span>
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {loading &&
                Array.from({ length: 6 }).map((_, index) => (
                  <TableRow key={index}>
                    <TableCell colSpan={12}>
                      <Skeleton className="h-6 w-full" />
                    </TableCell>
                  </TableRow>
                ))}

              {!loading && items.length === 0 && (
                <TableRow>
                  <TableCell colSpan={12}>
                    <div className="flex flex-col items-center justify-center py-16 text-center">
                      <Table2 className="mb-4 size-12 text-muted-foreground" />
                      <h3 className="mb-2 text-lg font-bold">
                        {isFiltered
                          ? t("integrationHub.parameters.emptyFilteredTitle")
                          : t("integrationHub.parameters.emptyTitle")}
                      </h3>
                      <p className="mb-4 max-w-sm text-sm leading-relaxed text-muted-foreground">
                        {isFiltered
                          ? t("integrationHub.parameters.emptyFilteredHint")
                          : t("integrationHub.parameters.emptyHint")}
                      </p>
                      {isFiltered ? (
                        <Button variant="outline" onClick={clearFilters} data-testid="clear-filters">
                          {t("integrationHub.parameters.clearFilters")}
                        </Button>
                      ) : (
                        canManage && (
                          <Button
                            onClick={() => {
                              setEditing(undefined)
                              setDrawerOpen(true)
                            }}
                          >
                            <Plus className="size-4" />
                            {t("integrationHub.parameters.newParameter")}
                          </Button>
                        )
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              )}

              {!loading &&
                items.map((parameter) => {
                  const actionLabel = canManage
                    ? t("integrationHub.parameters.edit")
                    : t("integrationHub.parameters.view")
                  return (
                    <TableRow
                      key={parameter.id}
                      className="hover:bg-muted/50"
                      data-testid={`parameter-row-${parameter.apiField}`}
                    >
                      <TableCell>
                        {/* Dimmed when disabled (FR-S5-02) — the row stays legible, but reads as
                            inactive at a glance alongside the toggle three columns over. */}
                        <div
                          className={cn(
                            "flex min-w-0 flex-col items-start",
                            !parameter.enabled && "opacity-55",
                          )}
                        >
                          <bdi dir="ltr" className="max-w-full truncate font-semibold">
                            {parameter.nameEn}
                          </bdi>
                          <bdi
                            dir="rtl"
                            lang="ar"
                            className="max-w-full truncate text-sm text-muted-foreground"
                          >
                            {parameter.nameAr}
                          </bdi>
                        </div>
                      </TableCell>
                      <TableCell>
                        <code
                          dir="ltr"
                          className="inline-block rounded-sm bg-muted px-2 py-1 font-mono text-xs tracking-wide text-foreground"
                        >
                          {parameter.apiField}
                        </code>
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {t(`integrationHub.dataTypes.${parameter.dataType}`)}
                      </TableCell>
                      <TableCell>
                        {/* Cyan, never mint: a mint pill on a data screen reads as the D2 "Good"
                            state, and origin is a category, not a health signal. */}
                        {parameter.origin === "built_in" ? (
                          <Badge className="bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200">
                            {t("integrationHub.parameters.origin.built_in")}
                          </Badge>
                        ) : (
                          <Badge variant="outline">
                            {t("integrationHub.parameters.origin.custom")}
                          </Badge>
                        )}
                      </TableCell>
                      <TableCell>
                        <Switch
                          checked={parameter.enabled}
                          disabled={!canManage}
                          aria-label={t("integrationHub.parameters.enabledFor", {
                            name: parameter.nameEn,
                          })}
                          data-testid={`enabled-${parameter.apiField}`}
                          onCheckedChange={(checked) => void handleToggle(parameter, checked === true)}
                        />
                      </TableCell>
                      <TableCell className="text-center">
                        <FlagGlyph
                          on={parameter.requiredByDefault}
                          label={`${t("integrationHub.parameters.colRequired")}: ${parameter.requiredByDefault ? t("common.yes") : t("common.no")}`}
                        />
                      </TableCell>
                      <TableCell className="text-center">
                        <FlagGlyph
                          on={parameter.filterable}
                          label={`${t("integrationHub.parameters.colFilterable")}: ${parameter.filterable ? t("common.yes") : t("common.no")}`}
                        />
                      </TableCell>
                      <TableCell className="text-center">
                        <FlagGlyph
                          on={parameter.reportingVisibility}
                          label={`${t("integrationHub.parameters.colReporting")}: ${parameter.reportingVisibility ? t("common.yes") : t("common.no")}`}
                        />
                      </TableCell>
                      <TableCell className="text-center">
                        <FlagGlyph
                          on={parameter.dashboardVisibility}
                          label={`${t("integrationHub.parameters.colDashboard")}: ${parameter.dashboardVisibility ? t("common.yes") : t("common.no")}`}
                        />
                      </TableCell>
                      <TableCell>
                        {/* FR-S5-04 — the "Mapped" link is the row's shortcut into SCR-07. A
                            parameter with no mapping support shows a dash, not an empty cell. */}
                        {parameter.mappingSupport ? (
                          <Button
                            variant="link"
                            size="sm"
                            className="h-auto p-0 text-sm"
                            data-testid={`mapped-${parameter.apiField}`}
                            onClick={() => navigate("/integration-hub/mappings")}
                          >
                            {t("integrationHub.parameters.mapped")}
                          </Button>
                        ) : (
                          <span className="text-muted-foreground">—</span>
                        )}
                      </TableCell>
                      <TableCell className="text-end tabular-nums">
                        {parameter.channelIds.length}
                      </TableCell>
                      <TableCell className="w-16 text-center">
                        {/* Edit is the ONLY row action — BR-09: no delete exists, for built-in or
                            custom alike, and no DELETE endpoint stands behind one. */}
                        <Button
                          variant="ghost"
                          size="icon"
                          className="text-muted-foreground hover:text-foreground"
                          aria-label={actionLabel}
                          title={actionLabel}
                          data-testid={
                            canManage
                              ? `edit-${parameter.apiField}`
                              : `view-${parameter.apiField}`
                          }
                          onClick={() => {
                            setEditing(parameter)
                            setDrawerOpen(true)
                          }}
                        >
                          {canManage ? <Pencil className="size-4" /> : <Eye className="size-4" />}
                        </Button>
                      </TableCell>
                    </TableRow>
                  )
                })}
            </TableBody>
          </Table>
        </div>
      )}

      {truncated && (
        <p className="text-sm text-muted-foreground">{t("integrationHub.parameters.truncated")}</p>
      )}

      <p className="text-sm leading-relaxed text-muted-foreground">
        {t("integrationHub.parameters.footerNote")}
      </p>

      <ParameterDrawer
        open={drawerOpen}
        parameter={editing}
        channels={channels}
        saving={saving}
        readOnly={!canManage}
        onSave={handleSave}
        onClose={() => {
          setDrawerOpen(false)
          setEditing(undefined)
        }}
      />

      {/* Dialog D-6 — BR-10's impact warning. The server withheld the write and handed back the
          reference list; nothing changes unless the user confirms here. */}
      <AlertDialog
        open={pendingDisable != null}
        onOpenChange={(open) => {
          if (!open) setPendingDisable(null)
        }}
      >
        <AlertDialogContent data-testid="parameter-impact-dialog">
          <AlertDialogHeader>
            <AlertDialogTitle>{t("integrationHub.parameters.impactTitle")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("integrationHub.parameters.impactDescription", {
                name: pendingDisable?.parameter.nameEn ?? "",
              })}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <ul className="max-h-48 space-y-1.5 overflow-y-auto text-sm">
            {pendingDisable?.references.map((reference, index) => (
              <li key={`${reference.kind}-${reference.name}-${index}`} className="flex items-center gap-2">
                <Badge variant="outline" className="shrink-0">
                  {t(`integrationHub.parameters.referenceKinds.${reference.kind}`, {
                    defaultValue: reference.kind,
                  })}
                </Badge>
                <span className="truncate">{reference.name}</span>
              </li>
            ))}
          </ul>
          <AlertDialogFooter>
            <AlertDialogCancel data-testid="parameter-impact-cancel">
              {t("common.cancel")}
            </AlertDialogCancel>
            <AlertDialogAction
              data-testid="parameter-impact-confirm"
              onClick={() => void confirmDisable()}
            >
              {t("integrationHub.parameters.disableAnyway")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}
