// Post-Expiry Response Store — STATIC shell (clickthrough parity). The page itself
// belongs to M-07 (Reporting): no backend exposes expired surveys, late-response
// counts or a latest-response feed yet, and dev has no response pipeline at all —
// so everything below renders deterministic sample data behind the standard
// sample-data banner. Wire the loaders when the M-07 spec ships its endpoints.

import { useNavigate } from "react-router"
import { Trans, useTranslation } from "react-i18next"
import {
  ArrowLeft,
  ArrowRight,
  CalendarClock,
  ChartNoAxesColumn,
  Clock,
  Eye,
  Info,
  MailWarning,
} from "lucide-react"

import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { cn } from "@/lib/utils"
import { useDirection } from "@/hooks/use-direction"

/** Deterministic sample rows — no backend source exists (M-07 gap). */
const SAMPLE_ROWS = [
  {
    survey: "Post-disbursement satisfaction",
    journey: "Personal Loan Application",
    expiredOn: "12 Jun 2026",
    lateResponses: 742,
    lastReceived: "2 days ago",
  },
  {
    survey: "Quarterly relationship pulse",
    journey: null,
    expiredOn: "30 Apr 2026",
    lateResponses: 361,
    lastReceived: "3 weeks ago",
  },
  {
    survey: "Call centre follow-up",
    journey: "Account Onboarding",
    expiredOn: "18 May 2026",
    lateResponses: 181,
    lastReceived: "1 month ago",
  },
]

function StatTile({
  icon: IconCmp,
  label,
  value,
  caption,
  valueClassName,
}: {
  icon: typeof Clock
  label: string
  value: string
  caption: string
  valueClassName?: string
}) {
  return (
    <Card>
      <CardContent className="flex items-start gap-3 px-4">
        <span className="flex size-10 shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary">
          <IconCmp className="size-5" aria-hidden />
        </span>
        <div className="min-w-0">
          <p className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">
            {label}
          </p>
          <p
            className={cn(
              "mt-1 font-heading text-3xl font-bold tabular-nums text-foreground",
              valueClassName,
            )}
          >
            {value}
          </p>
          <p className="mt-1 truncate text-xs text-muted-foreground">{caption}</p>
        </div>
      </CardContent>
    </Card>
  )
}

export default function PostExpiryStorePage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { isRtl } = useDirection()
  const BackIcon = isRtl ? ArrowRight : ArrowLeft

  return (
    <div className="space-y-5 py-5">
      {/* Header */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
        <div className="flex min-w-0 items-start gap-3">
          <Button
            variant="outline"
            size="icon"
            className="mt-0.5 size-9 shrink-0"
            onClick={() => navigate("/surveys")}
            aria-label={t("common.back")}
          >
            <BackIcon className="size-4" aria-hidden />
          </Button>
          <div className="min-w-0">
            <h1 className="text-2xl font-heading font-bold">
              {t("surveysModule.postExpiry.title")}
            </h1>
            <p className="mt-1 max-w-2xl text-sm text-muted-foreground">
              {t("surveysModule.postExpiry.subtitle")}
            </p>
          </div>
        </div>
        <Button variant="secondary" className="shrink-0" onClick={() => navigate("/surveys")}>
          <ChartNoAxesColumn className="size-4" aria-hidden />
          {t("surveysModule.postExpiry.openAnalytics")}
        </Button>
      </div>

      {/* Static sample notice — no M-07 backend exists yet */}
      <div className="rounded-md border border-nb-cyan-200 bg-nb-cyan-100/50 px-3 py-2 text-sm text-nb-cyan-800 dark:border-nb-cyan-800 dark:bg-nb-cyan-900/25 dark:text-nb-cyan-200">
        {t("surveysModule.sample.note")}
      </div>

      {/* How-it-works banner */}
      <div className="flex items-start gap-2 rounded-lg border border-border bg-card p-4 text-sm leading-relaxed text-muted-foreground shadow-sm dark:shadow-none">
        <Info className="mt-0.5 size-4 shrink-0 text-primary" aria-hidden />
        <span>
          <Trans
            i18nKey="surveysModule.postExpiry.banner"
            components={{ b: <b className="font-semibold text-foreground" /> }}
          />
        </span>
      </div>

      {/* Stat tiles */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <StatTile
          icon={Clock}
          label={t("surveysModule.postExpiry.expiredSurveys")}
          value="3"
          caption={t("surveysModule.postExpiry.expiredCaption")}
        />
        <StatTile
          icon={MailWarning}
          label={t("surveysModule.postExpiry.lateStored")}
          value="1,284"
          caption={t("surveysModule.postExpiry.lateCaption")}
        />
        <StatTile
          icon={CalendarClock}
          label={t("surveysModule.postExpiry.newestLate")}
          value="2 days ago"
          valueClassName="text-2xl"
          caption="Post-disbursement satisfaction"
        />
      </div>

      {/* Expired surveys table */}
      <div className="overflow-hidden rounded-lg border border-border bg-card shadow-sm dark:shadow-none">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t("surveysModule.postExpiry.colSurvey")}</TableHead>
              <TableHead>{t("surveysModule.postExpiry.colJourney")}</TableHead>
              <TableHead>{t("surveysModule.postExpiry.colExpiredOn")}</TableHead>
              <TableHead>{t("surveysModule.postExpiry.colLateResponses")}</TableHead>
              <TableHead>{t("surveysModule.postExpiry.colLastReceived")}</TableHead>
              <TableHead className="w-12" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {SAMPLE_ROWS.map((row) => (
              <TableRow key={row.survey} className="hover:bg-muted/50">
                <TableCell className="font-medium text-foreground">{row.survey}</TableCell>
                <TableCell className="text-muted-foreground">{row.journey ?? "—"}</TableCell>
                <TableCell className="text-muted-foreground">
                  <span dir="ltr">{row.expiredOn}</span>
                </TableCell>
                <TableCell className="font-semibold tabular-nums text-foreground">
                  {row.lateResponses}
                </TableCell>
                <TableCell className="text-muted-foreground">{row.lastReceived}</TableCell>
                <TableCell className="text-end">
                  <Button
                    variant="outline"
                    size="icon"
                    className="ms-auto size-8"
                    aria-label={t("common.preview")}
                  >
                    <Eye className="size-4" aria-hidden />
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  )
}
