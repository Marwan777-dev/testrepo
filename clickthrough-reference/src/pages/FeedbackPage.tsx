import { useState } from "react"
import { useTranslation } from "react-i18next"
import { Card, CardContent } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Badge } from "@/components/ui/badge"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import {
  Plus,
  Search,
  Calendar,
  Star,
} from "lucide-react"
import { cn } from "@/lib/utils"

// ─── Mock Data ────────────────────────────────────────────

interface FeedbackEntry {
  id: number
  nameKey: string
  branchKey: string
  rating: number
  sentiment: "positive" | "neutral" | "negative"
  commentKey: string
  date: string
}

const MOCK_FEEDBACK: FeedbackEntry[] = [
  { id: 1, nameKey: "feedback.customer1", branchKey: "feedback.branchRiyadh", rating: 5, sentiment: "positive", commentKey: "feedback.comment1", date: "2026-05-10" },
  { id: 2, nameKey: "feedback.customer2", branchKey: "feedback.branchJeddah", rating: 2, sentiment: "negative", commentKey: "feedback.comment2", date: "2026-05-09" },
  { id: 3, nameKey: "feedback.customer3", branchKey: "feedback.branchDammam", rating: 4, sentiment: "positive", commentKey: "feedback.comment3", date: "2026-05-08" },
  { id: 4, nameKey: "feedback.customer4", branchKey: "feedback.branchRiyadh", rating: 3, sentiment: "neutral", commentKey: "feedback.comment4", date: "2026-05-07" },
  { id: 5, nameKey: "feedback.customer5", branchKey: "feedback.branchJeddah", rating: 1, sentiment: "negative", commentKey: "feedback.comment5", date: "2026-05-06" },
  { id: 6, nameKey: "feedback.customer6", branchKey: "feedback.branchDammam", rating: 5, sentiment: "positive", commentKey: "feedback.comment6", date: "2026-05-05" },
]

// ─── Sentiment Badge Styles ───────────────────────────────

const sentimentStyles = {
  positive: "bg-nb-cyan-100 text-nb-cyan-700 border-nb-cyan/20 dark:bg-nb-cyan-700/20 dark:text-nb-cyan-300",
  neutral: "bg-d3-light text-d3-dark border-d3/20 dark:bg-d3-dark/20 dark:text-d3-light",
  negative: "bg-d5-light text-d5-dark border-d5/20 dark:bg-d5-dark/20 dark:text-d5-light",
}

// ─── Star Rating ──────────────────────────────────────────

function StarRating({ rating }: { rating: number }) {
  return (
    <div className="flex items-center gap-0.5">
      {Array.from({ length: 5 }, (_, i) => (
        <Star
          key={i}
          className={cn(
            "size-3.5",
            i < rating
              ? "fill-d3 text-d3"
              : "fill-none text-muted-foreground/30"
          )}
        />
      ))}
    </div>
  )
}

// ─── Page Component ───────────────────────────────────────

export default function FeedbackPage() {
  const { t, i18n } = useTranslation()
  const [search, setSearch] = useState("")
  const [branch, setBranch] = useState("all")
  const [dateRange, setDateRange] = useState("30")

  const sentimentLabels = {
    positive: t("feedback.sentimentPositive"),
    neutral: t("feedback.sentimentNeutral"),
    negative: t("feedback.sentimentNegative"),
  }

  // Filter mock data
  const filtered = MOCK_FEEDBACK.filter((entry) => {
    const nameMatch = search === "" || t(entry.nameKey).toLowerCase().includes(search.toLowerCase())
    const branchMatch = branch === "all" || entry.branchKey === `feedback.branch${branch}`
    return nameMatch && branchMatch
  })

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6">
      {/* ── Page Header ───────────────────────── */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0">
          <h1 className="text-2xl font-heading font-bold">{t("feedback.title")}</h1>
          <p className="text-sm text-muted-foreground mt-1">{t("feedback.subtitle")}</p>
        </div>
        <Button className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground shrink-0">
          <Plus className="size-4 ms-1.5" />
          {t("feedback.newFeedback")}
        </Button>
      </div>

      {/* ── Filter Bar ────────────────────────── */}
      <Card>
        <CardContent className="py-3">
          <div className="flex flex-col sm:flex-row items-start sm:items-center gap-3">
            {/* Search */}
            <div className="relative flex-1 w-full sm:max-w-xs">
              <Search className="absolute start-2.5 top-1/2 -translate-y-1/2 size-4 text-muted-foreground pointer-events-none" />
              <Input
                placeholder={t("feedback.searchPlaceholder")}
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="ps-8 h-9"
              />
            </div>

            {/* Branch dropdown */}
            <Select value={branch} onValueChange={(v) => setBranch(v ?? "all")}>
              <SelectTrigger className="w-full sm:w-44 h-9">
                <SelectValue placeholder={t("feedback.allBranches")} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">{t("feedback.allBranches")}</SelectItem>
                <SelectItem value="Riyadh">{t("feedback.branchRiyadh")}</SelectItem>
                <SelectItem value="Jeddah">{t("feedback.branchJeddah")}</SelectItem>
                <SelectItem value="Dammam">{t("feedback.branchDammam")}</SelectItem>
              </SelectContent>
            </Select>

            {/* Date range */}
            <Select value={dateRange} onValueChange={(v) => setDateRange(v ?? "30")}>
              <SelectTrigger className="w-full sm:w-40 h-9">
                <Calendar className="size-4 text-muted-foreground" />
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="7">{t("feedback.last7")}</SelectItem>
                <SelectItem value="30">{t("feedback.last30")}</SelectItem>
                <SelectItem value="90">{t("feedback.last90")}</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* ── Data Table ────────────────────────── */}
      <Card className="overflow-hidden min-w-0">
        <div className="overflow-x-auto">
          <Table>
            <TableHeader>
              <TableRow className="hover:bg-transparent">
                <TableHead className="text-start ps-6">{t("feedback.colName")}</TableHead>
                <TableHead className="text-start">{t("feedback.colBranch")}</TableHead>
                <TableHead className="text-start">{t("feedback.colRating")}</TableHead>
                <TableHead className="text-start">{t("feedback.colSentiment")}</TableHead>
                <TableHead className="text-start">{t("feedback.colComment")}</TableHead>
                <TableHead className="text-end pe-6">{t("feedback.colDate")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filtered.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={6} className="text-center py-12 text-muted-foreground">
                    {t("feedback.noResults")}
                  </TableCell>
                </TableRow>
              ) : (
                filtered.map((entry) => (
                  <TableRow
                    key={entry.id}
                    className="cursor-pointer hover:bg-muted/50 motion-safe:transition-colors"
                  >
                    <TableCell className="ps-6 font-medium">{t(entry.nameKey)}</TableCell>
                    <TableCell>{t(entry.branchKey)}</TableCell>
                    <TableCell>
                      <StarRating rating={entry.rating} />
                    </TableCell>
                    <TableCell>
                      <Badge variant="outline" className={cn("text-xs", sentimentStyles[entry.sentiment])}>
                        {sentimentLabels[entry.sentiment]}
                      </Badge>
                    </TableCell>
                    <TableCell className="max-w-[200px] truncate text-muted-foreground">
                      {t(entry.commentKey)}
                    </TableCell>
                    <TableCell className="text-end pe-6 tabular-nums text-muted-foreground">
                      {new Date(entry.date).toLocaleDateString(
                        i18n.language === "ar" ? "ar-SA" : "en-GB",
                        { day: "numeric", month: "short" }
                      )}
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </div>
      </Card>
    </div>
  )
}
