// Version History page (T079, US-3): the published-version timeline for a single journey. Lists
// immutable version snapshots newest-first (`GET /journeys/{id}/versions`), lets P-01 publish a new
// version (`POST /journeys/{id}/publish`), and opens any version's frozen snapshot in a read-only
// Sheet (`GET /journeys/{id}/versions/{n}` → `VersionSnapshotViewer`, with a clear isSnapshot
// indicator). RTL-first, design-system styled. Reachable from the Journey Builder header
// ("Version History") at `/journeys/:id/versions`.

import { useCallback, useEffect, useState } from "react"
import { Link, useParams } from "react-router"
import { useTranslation } from "react-i18next"
import {
  AlertTriangle,
  ChevronLeft,
  Eye,
  FileClock,
  History,
  Loader2,
  Lock,
  Rocket,
} from "lucide-react"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { useDirection } from "@/hooks/use-direction"
import { useSession } from "@/features/auth/hooks/useSession"
import {
  getJourney,
  getJourneyVersion,
  JourneysApiError,
  listJourneyVersions,
  publishJourneyVersion,
  type JourneyDetail,
  type JourneyVersionSnapshot,
  type JourneyVersionSummary,
} from "@/features/journeys/api"
import { JourneyStatusBadge } from "@/features/journeys/components/JourneyStatusBadge"
import { VersionSnapshotViewer } from "@/features/journeys/components/VersionSnapshotViewer"

const FETCH_SIZE = 100

/** Formats an ISO-8601 timestamp for the active locale, keeping Western digits in Arabic (CLAUDE.md). */
function formatPublishedAt(iso: string, lang: string): string {
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return "—"
  const locale = lang.startsWith("ar") ? "ar-u-nu-latn" : "en"
  return new Intl.DateTimeFormat(locale, {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  }).format(date)
}

export default function VersionHistoryPage() {
  const { t, i18n } = useTranslation()
  const { isRtl } = useDirection()
  const { session } = useSession()
  const { id } = useParams<{ id: string }>()
  const journeyId = id ?? ""

  const [journey, setJourney] = useState<JourneyDetail | null>(null)
  const [versions, setVersions] = useState<JourneyVersionSummary[]>([])
  const [truncated, setTruncated] = useState(false)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(false)
  const [publishing, setPublishing] = useState(false)

  // Snapshot Sheet state.
  const [openVersion, setOpenVersion] = useState<number | null>(null)
  const [snapshot, setSnapshot] = useState<JourneyVersionSnapshot | null>(null)
  const [snapshotLoading, setSnapshotLoading] = useState(false)
  const [snapshotError, setSnapshotError] = useState(false)

  // Publishing is P-01 only (contract); Archived journeys are immutable and can't be published.
  const canPublish = session?.persona === "P-01"
  const archived = journey?.status === "Archived"
  const sheetSide = isRtl ? "left" : "right"

  const load = useCallback(async () => {
    if (!journeyId) return
    setLoading(true)
    setLoadError(false)
    try {
      const [detail, page] = await Promise.all([
        getJourney(journeyId),
        listJourneyVersions(journeyId, { pageSize: FETCH_SIZE }),
      ])
      setJourney(detail)
      setVersions(page.items)
      setTruncated(Boolean(page.nextPageToken))
    } catch {
      setLoadError(true)
    } finally {
      setLoading(false)
    }
  }, [journeyId])

  useEffect(() => {
    void load()
  }, [load])

  async function handlePublish() {
    if (!canPublish || publishing) return
    setPublishing(true)
    try {
      const result = await publishJourneyVersion(journeyId)
      toast.success(t("journey.publishSuccess", { version: result.versionNumber }))
      // Refresh the list so the new version appears at the top.
      const page = await listJourneyVersions(journeyId, { pageSize: FETCH_SIZE })
      setVersions(page.items)
      setTruncated(Boolean(page.nextPageToken))
    } catch (err) {
      if (err instanceof JourneysApiError && err.code === "journey.no_stages")
        toast.error(t("journey.publishNoStages"))
      else if (err instanceof JourneysApiError && err.status === 403)
        toast.error(t("journey.archivedImmutable"))
      else toast.error(t("journey.publishFailed"))
    } finally {
      setPublishing(false)
    }
  }

  const openSnapshot = useCallback(
    async (versionNumber: number) => {
      setOpenVersion(versionNumber)
      setSnapshot(null)
      setSnapshotError(false)
      setSnapshotLoading(true)
      try {
        const snap = await getJourneyVersion(journeyId, versionNumber)
        setSnapshot(snap)
      } catch {
        setSnapshotError(true)
      } finally {
        setSnapshotLoading(false)
      }
    },
    [journeyId],
  )

  const backLink = (
    <Link
      to={`/journeys/${journeyId}/builder`}
      className="inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground"
    >
      <ChevronLeft className="size-4 rtl:rotate-180" aria-hidden />
      {t("journey.backToBuilder")}
    </Link>
  )

  if (loading) {
    return (
      <div className="space-y-5 py-5">
        {backLink}
        <Skeleton className="h-24 w-full rounded-lg" />
        <Skeleton className="h-64 w-full rounded-lg" />
      </div>
    )
  }

  if (loadError || !journey) {
    return (
      <div className="space-y-5 py-5">
        {backLink}
        <div className="flex flex-col items-center justify-center rounded-lg border border-border bg-card py-16 text-center shadow-sm dark:shadow-none">
          <AlertTriangle className="mb-4 size-12 text-muted-foreground" aria-hidden />
          <h3 className="mb-2 text-lg font-bold">{t("journey.builderLoadErrorTitle")}</h3>
          <p className="mb-4 max-w-sm text-muted-foreground">{t("journey.versionsLoadError")}</p>
          <Button variant="secondary" onClick={() => void load()}>
            {t("journey.retry")}
          </Button>
        </div>
      </div>
    )
  }

  const publishButton = canPublish ? (
    <Button
      className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
      onClick={() => void handlePublish()}
      disabled={publishing || archived}
      title={archived ? t("journey.archivedImmutable") : undefined}
    >
      {publishing ? (
        <Loader2 className="size-4 animate-spin ms-2" aria-hidden />
      ) : (
        <Rocket className="size-4 ms-2" aria-hidden />
      )}
      {publishing ? t("journey.publishing") : t("journey.publishVersion")}
    </Button>
  ) : null

  return (
    <div className="space-y-5 py-5">
      {backLink}

      {/* Header card */}
      <Card>
        <CardContent>
          <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
            <div className="min-w-0 space-y-1.5">
              <div className="flex flex-wrap items-center gap-2.5">
                <History className="size-6 shrink-0 text-primary" aria-hidden />
                <h1 className="truncate text-2xl font-heading font-bold">{t("journey.versionHistoryTitle")}</h1>
                <JourneyStatusBadge status={journey.status} />
              </div>
              <p className="text-sm leading-relaxed text-muted-foreground">
                {t("journey.versionHistorySubtitle")}
              </p>
              <p className="truncate text-sm font-semibold text-foreground">{journey.name}</p>
            </div>
            {publishButton}
          </div>
        </CardContent>
      </Card>

      {/* Versions table / states */}
      {versions.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-lg border border-border bg-card py-16 text-center shadow-sm dark:shadow-none">
          <FileClock className="mb-4 size-12 text-muted-foreground" aria-hidden />
          <h3 className="mb-2 text-lg font-bold">{t("journey.noVersions")}</h3>
          <p className="mb-4 max-w-sm text-muted-foreground">{t("journey.noVersionsHelp")}</p>
          {publishButton}
        </div>
      ) : (
        <div className="overflow-hidden rounded-lg border border-border bg-card shadow-sm dark:shadow-none">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="text-start font-semibold text-foreground">{t("journey.colVersion")}</TableHead>
                <TableHead className="text-start font-semibold text-foreground">{t("journey.publishedOn")}</TableHead>
                <TableHead className="text-start font-semibold text-foreground">{t("journey.publishedBy")}</TableHead>
                <TableHead className="text-end font-semibold text-foreground">{t("journey.colActions")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {versions.map((v) => (
                <TableRow
                  key={v.versionId}
                  className="cursor-pointer transition-colors hover:bg-muted/50"
                  onClick={() => void openSnapshot(v.versionNumber)}
                >
                  <TableCell>
                    <span className="font-semibold text-foreground">
                      {t("journey.versionLabel", { number: v.versionNumber })}
                    </span>
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground tabular-nums">
                    {formatPublishedAt(v.publishedAt, i18n.language)}
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {v.publishedByName || "—"}
                  </TableCell>
                  <TableCell className="text-end">
                    <button
                      type="button"
                      onClick={(e) => {
                        e.stopPropagation()
                        void openSnapshot(v.versionNumber)
                      }}
                      className="inline-flex items-center gap-1.5 text-sm font-medium text-primary transition-colors hover:text-nb-cyan-700"
                    >
                      <Eye className="size-4" aria-hidden />
                      {t("journey.viewSnapshot")}
                    </button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      {!loading && truncated && (
        <p className="text-xs text-muted-foreground">{t("journey.versionsTruncated", { count: FETCH_SIZE })}</p>
      )}

      {/* Read-only snapshot Sheet */}
      <Sheet open={openVersion !== null} onOpenChange={(o) => !o && setOpenVersion(null)}>
        <SheetContent side={sheetSide} className="w-full sm:max-w-lg md:max-w-xl">
          <SheetHeader className="border-b border-border">
            <SheetTitle className="flex items-center gap-2 text-lg font-heading font-bold">
              <Lock className="size-4 text-primary" aria-hidden />
              {openVersion !== null
                ? t("journey.snapshotTitle", { version: openVersion })
                : t("journey.snapshotReadOnly")}
            </SheetTitle>
            <SheetDescription>{t("journey.snapshotSheetDesc")}</SheetDescription>
          </SheetHeader>
          <div className="flex-1 overflow-y-auto p-4">
            {snapshotLoading ? (
              <div className="space-y-4">
                <Skeleton className="h-16 w-full rounded-md" />
                <Skeleton className="h-24 w-full rounded-md" />
                <Skeleton className="h-40 w-full rounded-md" />
              </div>
            ) : snapshotError || !snapshot ? (
              <div className="flex flex-col items-center justify-center py-16 text-center">
                <AlertTriangle className="mb-4 size-10 text-muted-foreground" aria-hidden />
                <p className="mb-4 max-w-xs text-sm text-muted-foreground">{t("journey.snapshotLoadError")}</p>
                {openVersion !== null && (
                  <Button variant="secondary" onClick={() => void openSnapshot(openVersion)}>
                    {t("journey.retry")}
                  </Button>
                )}
              </div>
            ) : (
              <VersionSnapshotViewer snapshot={snapshot} />
            )}
          </div>
        </SheetContent>
      </Sheet>
    </div>
  )
}
