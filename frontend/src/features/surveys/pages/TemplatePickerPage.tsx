// F6 Template picker (T199, clickthrough parity): label-less filter row (search by
// name/tag + All Types + All Sectors), reference card anatomy — Built-in navy badge
// with a padlock or a muted "Customized" tag, bold title, sector chips (built-in) or
// #tag chips (customized), description meta, "Use as Survey" + edit pencil for
// customized. Server sorts customized-first (FR-6.1). "Use as Survey" instantiates a
// new Draft survey and deep-links into its prefilled Survey details. Rendered as the
// Templates tab of the Survey Library — the old /templates route redirects there.

import { useCallback, useEffect, useState } from "react"
import { useNavigate } from "react-router"
import { useTranslation } from "react-i18next"
import { LayoutTemplate, Lock, PencilLine, Search } from "lucide-react"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Skeleton } from "@/components/ui/skeleton"
import { useSession } from "@/features/auth/hooks/useSession"
import { newIdempotencyKey } from "../api/surveys-api"
import {
  getTemplate,
  instantiateTemplate,
  listTemplates,
  type TemplateClass,
  type TemplateListItem,
} from "../api/templates-api"

/** Curated platform sectors (mirror the M-06 organization industries). */
const SECTORS = [
  "Banking",
  "Telecommunications",
  "Government",
  "Automotive",
  "Entertainment",
  "Services",
] as const

export function TemplateLibraryContent({
  onCountChange,
  refreshToken = 0,
}: {
  /** Reports the unfiltered template total up to the library tabs. */
  onCountChange?: (count: number) => void
  /** Bump to force a refetch (e.g. after "Save as template" creates a row). */
  refreshToken?: number
}) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { session } = useSession()
  const [items, setItems] = useState<TemplateListItem[]>([])
  const [totalCount, setTotalCount] = useState(0)
  // Question counts live only on the single-template view (not the list wire) —
  // batch-fetched per card. "Used by N surveys" has no endpoint at all (backend gap).
  const [questionCounts, setQuestionCounts] = useState<Record<string, number>>({})
  const [search, setSearch] = useState("")
  const [classFilter, setClassFilter] = useState<"all" | TemplateClass>("all")
  const [sectorFilter, setSectorFilter] = useState<string>("all")
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(false)
  const [busyId, setBusyId] = useState<string | null>(null)

  const canAuthor = session?.persona === "P-01" || session?.persona === "P-03"
  const isFiltered = search !== "" || classFilter !== "all" || sectorFilter !== "all"

  const load = useCallback(async () => {
    setLoading(true)
    setError(false)
    try {
      const result = await listTemplates({
        q: search || undefined,
        class: classFilter === "all" ? undefined : classFilter,
        sector: sectorFilter === "all" ? undefined : sectorFilter,
        pageSize: 100,
      })
      setItems(result.items)
      if (!search && classFilter === "all" && sectorFilter === "all") {
        setTotalCount(result.totalCount)
        onCountChange?.(result.totalCount)
      }
      // Small libraries only — avoid an N+1 flood on very large template sets.
      if (result.items.length <= 30) {
        void Promise.allSettled(
          result.items.map((item) =>
            getTemplate(item.id).then(({ data }) =>
              setQuestionCounts((prev) => ({ ...prev, [item.id]: data.questionCount }))
            )
          )
        )
      }
    } catch {
      setError(true)
    } finally {
      setLoading(false)
    }
  }, [search, classFilter, sectorFilter, onCountChange, refreshToken])

  useEffect(() => {
    const handle = window.setTimeout(() => void load(), 300)
    return () => window.clearTimeout(handle)
  }, [load])

  const useTemplate = async (template: TemplateListItem) => {
    setBusyId(template.id)
    try {
      // Lands on the new Draft's prefilled Survey details (reference flow).
      const { surveyId } = await instantiateTemplate(template.id, undefined, newIdempotencyKey())
      navigate(`/surveys/${surveyId}/settings`)
    } catch {
      setError(true)
      setBusyId(null)
    }
  }

  const sectorLabel = (key: string) => {
    const translated = t(`settings.organization.industries.${key}`, { defaultValue: "" })
    return translated || key
  }

  return (
    <div className="space-y-4">
      {/* Filter row — label-less like the reference: search + Type + Sector */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="relative min-w-48 flex-1">
          <Search
            className="pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
            aria-hidden
          />
          <Input
            id="template-search"
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={t("surveysModule.templates.searchPlaceholder")}
            aria-label={t("surveysModule.templates.searchLabel")}
            className="ps-9"
          />
        </div>
        <Select
          value={classFilter}
          onValueChange={(v) => setClassFilter((v ?? "all") as "all" | TemplateClass)}
        >
          <SelectTrigger className="w-44" aria-label={t("surveysModule.library.typeFilter")}>
            <SelectValue>
              {(v) =>
                v === "all" || !v
                  ? t("surveysModule.library.allTypes")
                  : v === "BuiltIn"
                    ? t("surveysModule.templates.builtIn")
                    : t("surveysModule.templates.customized")
              }
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t("surveysModule.library.allTypes")}</SelectItem>
            <SelectItem value="BuiltIn">{t("surveysModule.templates.builtIn")}</SelectItem>
            <SelectItem value="Customized">{t("surveysModule.templates.customized")}</SelectItem>
          </SelectContent>
        </Select>
        <Select value={sectorFilter} onValueChange={(v) => setSectorFilter(v ?? "all")}>
          <SelectTrigger className="w-44" aria-label={t("surveysModule.templates.sectors")}>
            <SelectValue>
              {(v) =>
                v === "all" || !v
                  ? t("surveysModule.templates.allSectors")
                  : sectorLabel(String(v))
              }
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t("surveysModule.templates.allSectors")}</SelectItem>
            {SECTORS.map((s) => (
              <SelectItem key={s} value={s}>
                {sectorLabel(s)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {error && (
        <div
          role="alert"
          className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {t("surveysModule.templates.loadError")}
        </div>
      )}

      {loading ? (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-44 w-full" />
          ))}
        </div>
      ) : items.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-lg border border-border bg-card py-16 text-center shadow-sm dark:shadow-none">
          <LayoutTemplate className="mb-4 size-12 text-muted-foreground" aria-hidden />
          <h3 className="mb-2 text-lg font-bold">
            {isFiltered
              ? t("surveysModule.templates.noResults")
              : t("surveysModule.templates.empty")}
          </h3>
          <p className="max-w-sm text-muted-foreground">
            {isFiltered
              ? t("surveysModule.templates.noResultsHelp")
              : t("surveysModule.templates.emptyHelp")}
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
          {items.map((template) => {
            const builtIn = template.class === "BuiltIn"
            const chips = builtIn
              ? template.sectors.map((s) => ({ key: s, label: sectorLabel(s) }))
              : template.tags.map((tag) => ({ key: tag, label: `#${tag}` }))
            return (
              <div
                key={template.id}
                className="flex flex-col gap-3 rounded-lg border border-border bg-card p-5 transition-shadow duration-150 hover:shadow-md"
              >
                {/* Top: Built-in badge + lock, or "Customized" label */}
                <div className="flex min-h-5 items-center gap-2">
                  {builtIn ? (
                    <>
                      <Badge className="border-transparent bg-nb-navy-100 text-xs font-medium text-nb-navy-800 dark:bg-nb-navy-700/40 dark:text-nb-navy-100">
                        {t("surveysModule.templates.builtIn")}
                      </Badge>
                      <Lock
                        className="size-3.5 shrink-0 text-muted-foreground"
                        aria-label={t("surveysModule.templates.builtInNotice")}
                      />
                    </>
                  ) : (
                    <span className="text-xs text-muted-foreground">
                      {t("surveysModule.templates.customized")}
                    </span>
                  )}
                </div>

                <h3 className="text-base font-bold leading-snug text-foreground">
                  {template.nameEn}
                </h3>

                {chips.length > 0 && (
                  <div className="flex flex-wrap gap-1.5">
                    {chips.map((chip) => (
                      <Badge
                        key={chip.key}
                        variant="outline"
                        className="text-xs text-muted-foreground"
                      >
                        {chip.label}
                      </Badge>
                    ))}
                  </div>
                )}

                {template.description && (
                  <p className="line-clamp-2 text-xs leading-relaxed text-muted-foreground">
                    {template.description}
                  </p>
                )}

                {/* Meta row — "Used by N surveys" needs a backend endpoint (gap). */}
                <div className="mt-auto flex items-center justify-between pt-1 text-xs text-muted-foreground">
                  <span>
                    {questionCounts[template.id] != null
                      ? t("surveysModule.templates.questionCount", {
                          count: questionCounts[template.id],
                        })
                      : "\u00A0"}
                  </span>
                </div>

                <div className="flex items-center gap-2">
                  {canAuthor && (
                    <Button
                      className="flex-1 bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
                      onClick={() => void useTemplate(template)}
                      disabled={busyId !== null}
                    >
                      {t("surveysModule.templates.useTemplate")}
                    </Button>
                  )}
                  {!builtIn && canAuthor && (
                    <Button
                      variant="secondary"
                      size="icon"
                      className="size-10 shrink-0"
                      aria-label={t("common.edit")}
                      onClick={() => navigate(`/templates/${template.id}/edit`)}
                    >
                      <PencilLine className="size-4" aria-hidden />
                    </Button>
                  )}
                </div>
              </div>
            )
          })}
        </div>
      )}

      {!loading && items.length > 0 && (
        <p className="text-xs text-muted-foreground">
          {t("surveysModule.templates.showing", {
            shown: items.length,
            total: totalCount || items.length,
          })}
        </p>
      )}
    </div>
  )
}
