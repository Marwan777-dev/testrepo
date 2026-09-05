// Verbatim table (T247, FR-13.7): the sample list for Text/Paragraph questions with a
// "show more" action fetching up to 100 via GET /report/verbatims. Timestamps stay
// LTR inside a start-aligned cell (dates are NOT end-aligned — CLAUDE.md table rule).

import { useState } from "react"
import { useTranslation } from "react-i18next"
import { Loader2 } from "lucide-react"

import { Button } from "@/components/ui/button"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { getReportVerbatims, type VerbatimEntry } from "../api/report-api"

export function VerbatimTable({
  surveyId,
  questionId,
  sample,
  totalAvailable,
}: {
  surveyId: string
  questionId: string
  sample: VerbatimEntry[]
  totalAvailable: number | null
}) {
  const { t } = useTranslation()
  const [rows, setRows] = useState<VerbatimEntry[]>(sample)
  const [expanded, setExpanded] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(false)

  const canShowMore = !expanded && (totalAvailable ?? 0) > rows.length

  const showMore = async () => {
    setLoading(true)
    setError(false)
    try {
      setRows(await getReportVerbatims(surveyId, questionId, 100))
      setExpanded(true)
    } catch {
      setError(true)
    } finally {
      setLoading(false)
    }
  }

  if (rows.length === 0) {
    return <p className="text-sm text-muted-foreground">{t("surveysModule.report.noVerbatims")}</p>
  }

  return (
    <div className="space-y-3">
      <div className="overflow-hidden rounded-md border border-border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t("surveysModule.report.verbatimText")}</TableHead>
              <TableHead>{t("surveysModule.report.verbatimChannel")}</TableHead>
              <TableHead>{t("surveysModule.report.verbatimTime")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((v) => (
              <TableRow key={v.responseId} className="hover:bg-muted/50 transition-colors">
                <TableCell className="max-w-md">
                  <p className="line-clamp-3 whitespace-normal leading-relaxed">{v.text}</p>
                </TableCell>
                <TableCell>{v.channel}</TableCell>
                <TableCell>
                  <span dir="ltr" className="tabular-nums">
                    {new Date(v.submittedAt).toLocaleString()}
                  </span>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {t("surveysModule.report.verbatimError")}
        </p>
      )}
      {canShowMore && (
        <Button variant="secondary" size="compact" onClick={() => void showMore()} disabled={loading}>
          {loading && <Loader2 className="size-4 animate-spin" aria-hidden />}
          {t("surveysModule.report.showMore", { count: Math.min(totalAvailable ?? 100, 100) })}
        </Button>
      )}
      <p className="text-sm text-muted-foreground">
        {t("surveysModule.report.verbatimFooter", { count: rows.length })}
      </p>
    </div>
  )
}
