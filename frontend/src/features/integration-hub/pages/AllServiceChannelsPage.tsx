// SCR-03 Service Channels list (T038, US1).
//
// FR-S3-01 columns: channel (bilingual name) · channel ID chip · status · supported count ·
// required count · integrations count · Edit action.
// FR-S3-02 / BR-07: **no delete control exists anywhere** — a channel is deactivated, never
// deleted, and there is no DELETE endpoint either. The absence is the enforcement (AC scenario 6).

import { useNavigate } from "react-router"
import { useTranslation } from "react-i18next"
import { Building2, Eye, Pencil, Plus } from "lucide-react"

import { cn } from "@/lib/utils"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Skeleton } from "@/components/ui/skeleton"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { AccessDenied } from "@/features/integration-hub/components/AccessDenied"
import { useServiceChannels } from "@/features/integration-hub/hooks/useServiceChannels"
import { useIntegrationHubAccess } from "@/features/integration-hub/hooks/useIntegrationHubAccess"

/** Design-system UI Label: small-caps, tracked, muted — the table-header treatment. */
const TH = "text-xs font-medium uppercase tracking-widest text-muted-foreground"

export default function AllServiceChannelsPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  // FR-GBL-05 — P-07 sees this screen read-only (BR-24): no create, View instead of Edit.
  const access = useIntegrationHubAccess()
  const canManage = access.canManage("serviceChannels")
  const { items, allItems, activeCount, truncated, loading, error, search, setSearch, isFiltered } =
    useServiceChannels()

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
  if (!access.canView("serviceChannels")) {
    return <AccessDenied screenName={t("integrationHub.channels.title")} />
  }

  return (
    <div className="space-y-5 py-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
        <div className="min-w-0">
          <h1 className="font-heading text-2xl font-bold">{t("integrationHub.channels.title")}</h1>
          {/* The teaching copy is permanent, not a loading placeholder: "service channel" is a
              domain term nobody guesses. The counts sit below it rather than replacing it. */}
          <p className="mt-1 max-w-2xl text-sm leading-relaxed text-muted-foreground">
            {t("integrationHub.channels.subtitle")}
          </p>
          {!loading && (
            <p className="mt-1.5 text-xs text-muted-foreground">
              {t("integrationHub.channels.countSummary", {
                total: allItems.length,
                active: activeCount,
              })}
            </p>
          )}
        </div>
        {canManage && (
          <Button
            onClick={() => navigate("/integration-hub/service-channels/new")}
            data-testid="new-channel"
          >
            <Plus className="size-4" />
            {t("integrationHub.channels.newChannel")}
          </Button>
        )}
      </div>

      <div className="flex flex-col gap-4 sm:flex-row sm:items-end">
        <div className="flex flex-1 flex-col gap-1.5 sm:max-w-sm">
          <Label htmlFor="channel-search">{t("integrationHub.channels.searchLabel")}</Label>
          <Input
            id="channel-search"
            value={search}
            placeholder={t("integrationHub.channels.searchPlaceholder")}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
      </div>

      {error ? (
        <div className="flex flex-col items-center justify-center rounded-lg border border-border bg-card py-16 text-center shadow-sm dark:shadow-none">
          <h2 className="mb-2 text-lg font-bold">{t("integrationHub.channels.errorTitle")}</h2>
          <p className="text-sm text-muted-foreground">{t("integrationHub.channels.errorHint")}</p>
        </div>
      ) : (
        <div className="overflow-hidden rounded-lg border border-border bg-card shadow-sm dark:shadow-none">
          <Table>
            <TableHeader className="sticky top-0 z-10">
              <TableRow>
                <TableHead className={cn(TH, "w-[38%]")}>
                  {t("integrationHub.channels.colChannel")}
                </TableHead>
                <TableHead className={cn(TH, "w-[15%]")}>
                  {t("integrationHub.channels.colChannelId")}
                </TableHead>
                <TableHead className={cn(TH, "w-[10%]")}>
                  {t("integrationHub.channels.colStatus")}
                </TableHead>
                <TableHead className={cn(TH, "w-[13%] text-end")}>
                  {t("integrationHub.channels.colSupported")}
                </TableHead>
                <TableHead className={cn(TH, "w-[8%] text-end")}>
                  {t("integrationHub.channels.colRequired")}
                </TableHead>
                <TableHead className={cn(TH, "w-[11%] text-end")}>
                  {t("integrationHub.channels.colIntegrations")}
                </TableHead>
                {/* Edit sits directly in the row, so this column carries no visible label — the
                    accessible name is kept for screen readers rather than dropped. */}
                <TableHead className="w-16 text-center">
                  <span className="sr-only">{t("integrationHub.channels.colActions")}</span>
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {loading &&
                Array.from({ length: 4 }).map((_, index) => (
                  <TableRow key={index}>
                    <TableCell colSpan={7}>
                      <Skeleton className="h-6 w-full" />
                    </TableCell>
                  </TableRow>
                ))}

              {!loading && items.length === 0 && (
                <TableRow>
                  <TableCell colSpan={7}>
                    <div className="flex flex-col items-center justify-center py-16 text-center">
                      <Building2 className="mb-4 size-12 text-muted-foreground" />
                      <h3 className="mb-2 text-lg font-bold">
                        {isFiltered
                          ? t("integrationHub.channels.emptyFilteredTitle")
                          : t("integrationHub.channels.emptyTitle")}
                      </h3>
                      <p className="mb-4 max-w-sm text-sm leading-relaxed text-muted-foreground">
                        {isFiltered
                          ? t("integrationHub.channels.emptyFilteredHint")
                          : t("integrationHub.channels.emptyHint")}
                      </p>
                      {!isFiltered && canManage && (
                        <Button onClick={() => navigate("/integration-hub/service-channels/new")}>
                          <Plus className="size-4" />
                          {t("integrationHub.channels.newChannel")}
                        </Button>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              )}

              {!loading &&
                items.map((channel) => {
                  const label = canManage
                    ? t("integrationHub.channels.edit")
                    : t("integrationHub.channels.view")
                  return (
                  <TableRow
                    key={channel.id}
                    className="hover:bg-muted/50"
                    data-testid={`channel-row-${channel.channelId}`}
                  >
                    <TableCell>
                      <div className="flex min-w-0 flex-col items-start">
                        <bdi dir="ltr" className="max-w-full truncate font-semibold">
                          {channel.nameEn}
                        </bdi>
                        {/* The API ships a human `description` explaining what the channel IS —
                            far more useful here than the Arabic name, which duplicates the primary
                            for an English reader. Falls back to `nameAr` when a channel has none,
                            so older rows don't lose their second line entirely. */}
                        <bdi
                          dir="auto"
                          className="line-clamp-2 max-w-full text-sm leading-snug text-muted-foreground"
                        >
                          {channel.description ?? channel.nameAr}
                        </bdi>
                      </div>
                    </TableCell>
                    <TableCell>
                      <code
                        dir="ltr"
                        className="inline-block rounded-sm bg-muted px-2 py-1 font-mono text-xs tracking-wide text-foreground"
                      >
                        {channel.channelId}
                      </code>
                    </TableCell>
                    <TableCell>
                      {/* Dot + label: the dot makes the column scannable, the label keeps status
                          from being conveyed by colour alone (WCAG / design-system rule). */}
                      {channel.active ? (
                        <Badge className="gap-1.5 bg-d2-light text-d2-dark dark:bg-d2-dark/25 dark:text-d2-light">
                          <span aria-hidden className="size-1.5 rounded-full bg-d2" />
                          {t("integrationHub.channels.statusActive")}
                        </Badge>
                      ) : (
                        <Badge variant="outline" className="gap-1.5 text-muted-foreground">
                          <span aria-hidden className="size-1.5 rounded-full bg-muted-foreground" />
                          {t("integrationHub.channels.statusInactive")}
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-end tabular-nums">{channel.supportedCount}</TableCell>
                    <TableCell className="text-end tabular-nums">{channel.requiredCount}</TableCell>
                    <TableCell className="text-end tabular-nums">
                      {channel.integrationsCount}
                    </TableCell>
                    <TableCell className="w-16 text-center">
                      {/* Edit is the ONLY row action — no delete exists anywhere (BR-07).
                          Icon-only and ghost: a filled button repeated down a long table reads as a
                          solid stripe of colour and competes with the page's one primary CTA. The
                          label moves to `aria-label` + tooltip, so nothing is lost non-visually. */}
                      <Button
                        variant="ghost"
                        size="icon"
                        className="text-muted-foreground hover:text-foreground"
                        aria-label={label}
                        title={label}
                        data-testid={
                          canManage ? `edit-${channel.channelId}` : `view-${channel.channelId}`
                        }
                        onClick={() => navigate(`/integration-hub/service-channels/${channel.id}`)}
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
        <p className="text-sm text-muted-foreground">{t("integrationHub.channels.truncated")}</p>
      )}

      <p className="text-sm leading-relaxed text-muted-foreground">
        {t("integrationHub.channels.footerNote")}
      </p>
    </div>
  )
}
