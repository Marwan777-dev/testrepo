// T089 [US3] — CXI weights table. Lists every currently active, non-composite KPI on the tenant
// (the candidate members), each with a positive-integer weight input and a live Effective % column
// that mirrors the backend CxiWeightNormaliser for instant feedback (still re-asserted server-side
// on save). A Total row closes the table. The weights are NOT saved here — the editable payload is
// reported up through `onChange` and persisted by the page's main Save button (which calls the
// full-replace PUT /api/v1/kpis/{cxi_id}/weights after the KPI config save). The live member count is
// also reported so the page can disable the CXI's Active checkbox when fewer than two members carry a
// weight (FR-043).

import { useEffect, useMemo, useState } from "react"
import { useTranslation } from "react-i18next"

import { Input } from "@/components/ui/input"
import { Skeleton } from "@/components/ui/skeleton"
import {
  Table,
  TableBody,
  TableCell,
  TableFooter,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { useKpiList } from "@/features/kpi-management/hooks/useKpiList"
import type { CxiWeightItem, CxiWeightUpdateItem } from "@/features/kpi-management/api"

export interface CxiWeightsSummary {
  /** Members carrying a positive weight — drives the CXI Active-checkbox gate (FR-043). */
  positiveCount: number
  /** Live legend (positive members only) for the dashboard preview. */
  legend: {
    memberKpiId: string
    memberShortName: string
    weight: number
    effectivePercentage: number
  }[]
  /** Editable payload (positive members only) for the page's main Save — the full-replace set. */
  weights: CxiWeightUpdateItem[]
}

/**
 * Relative integer weights → Effective % rounded to 1 dp, summing to 100 (±0.1). Mirrors the backend
 * `CxiWeightNormaliser` (away-from-zero rounding); total ≤ 0 yields all zeros.
 */
export function cxiEffectivePercentages(weights: number[]): number[] {
  const total = weights.reduce((sum, w) => sum + (w > 0 ? w : 0), 0)
  if (total <= 0) return weights.map(() => 0)
  return weights.map((w) => (w > 0 ? Math.round((w / total) * 1000) / 10 : 0))
}

export function CxiWeightsTable({
  cxiId,
  initialWeights,
  readOnly,
  onChange,
}: {
  cxiId: string
  initialWeights: CxiWeightItem[]
  readOnly: boolean
  onChange?: (summary: CxiWeightsSummary) => void
}) {
  const { t } = useTranslation()
  const { items, loading, error } = useKpiList()

  // Seed the editable weight map from the saved table, keyed by member id.
  const seed = useMemo(() => {
    const map: Record<string, number> = {}
    for (const w of initialWeights) map[w.memberKpiId] = w.weight
    return map
  }, [initialWeights])
  const [weights, setWeights] = useState<Record<string, number>>(seed)

  // Candidate members: active, non-composite, and never the CXI itself (FR — cannot include itself).
  const members = useMemo(
    () => items.filter((k) => !k.isComposite && k.id !== cxiId),
    [items, cxiId],
  )

  const memberWeights = members.map((k) => weights[k.id] ?? 0)
  const effPcts = cxiEffectivePercentages(memberWeights)
  const totalWeight = memberWeights.reduce((sum, w) => sum + (w > 0 ? w : 0), 0)
  const positiveCount = memberWeights.filter((w) => w > 0).length

  // Report the live count + legend + editable payload upward (positive members only). `onChange` is
  // intentionally excluded from deps — the parent re-creates it each render, and only real edits
  // should refire.
  //
  // Crucially, do NOT report while the candidate list is still loading: members is momentarily empty,
  // which would report positiveCount 0 and (via the parent's normalize) latch an already-active CXI's
  // Active checkbox off before the real members arrive. The parent's seeded count holds until load.
  useEffect(() => {
    if (loading || error) return
    const positiveMembers = members
      .map((k, i) => ({ member: k, weight: memberWeights[i], effectivePercentage: effPcts[i] }))
      .filter((row) => row.weight > 0)
    onChange?.({
      positiveCount,
      legend: positiveMembers.map(({ member, weight, effectivePercentage }) => ({
        memberKpiId: member.id,
        memberShortName: member.shortName,
        weight,
        effectivePercentage,
      })),
      weights: positiveMembers.map(({ member, weight }) => ({ memberKpiId: member.id, weight })),
    })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [members, weights])

  const setWeight = (id: string, raw: string) => {
    const parsed = raw === "" ? 0 : Math.max(0, Math.round(Number(raw)))
    setWeights((prev) => ({ ...prev, [id]: Number.isFinite(parsed) ? parsed : 0 }))
  }

  return (
    <section className="space-y-3" data-testid="cxi-weights-table">
      <div>
        <h3 className="text-base font-bold">{t("kpi.cxiWeightsTitle")}</h3>
        <p className="mt-1 text-sm text-muted-foreground">{t("kpi.cxiWeightsHelp")}</p>
      </div>

      {loading ? (
        <div className="space-y-2">
          <Skeleton className="h-9 w-full" />
          <Skeleton className="h-9 w-full" />
          <Skeleton className="h-9 w-full" />
        </div>
      ) : error ? (
        <p
          role="alert"
          className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {t("kpi.cxiWeightsLoadError")}
        </p>
      ) : members.length === 0 ? (
        <p className="rounded-md border border-border bg-muted/40 px-3 py-3 text-sm text-muted-foreground">
          {t("kpi.cxiNoMembers")}
        </p>
      ) : (
        <>
          <div className="overflow-x-auto rounded-md border border-border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("kpi.cxiColMember")}</TableHead>
                  <TableHead className="w-32 text-end">{t("kpi.cxiColWeight")}</TableHead>
                  <TableHead className="w-28 text-end">{t("kpi.cxiColEffective")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {members.map((member, i) => (
                  <TableRow key={member.id} className="hover:bg-muted/50">
                    <TableCell>
                      <span className="font-medium">{member.shortName}</span>
                      <span className="ms-2 text-xs text-muted-foreground">{member.fullName}</span>
                    </TableCell>
                    <TableCell className="text-end">
                      <Input
                        type="number"
                        inputMode="numeric"
                        min={0}
                        step={1}
                        value={weights[member.id] ?? ""}
                        disabled={readOnly}
                        aria-label={t("kpi.cxiColWeight") + " — " + member.shortName}
                        data-testid={`cxi-weight-${member.shortName}`}
                        onChange={(e) => setWeight(member.id, e.target.value)}
                        className="ms-auto h-9 w-24 text-end tabular-nums"
                      />
                    </TableCell>
                    <TableCell
                      className="text-end tabular-nums text-muted-foreground"
                      data-testid={`cxi-effective-${member.shortName}`}
                    >
                      {memberWeights[i] > 0 ? `${effPcts[i].toFixed(1)}%` : "—"}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
              <TableFooter>
                <TableRow>
                  <TableCell className="font-bold">{t("kpi.cxiTotal")}</TableCell>
                  <TableCell className="text-end font-bold tabular-nums" data-testid="cxi-total-weight">
                    {totalWeight}
                  </TableCell>
                  <TableCell className="text-end font-bold tabular-nums">
                    {totalWeight > 0 ? "100.0%" : "—"}
                  </TableCell>
                </TableRow>
              </TableFooter>
            </Table>
          </div>

          {positiveCount < 2 && (
            <p
              className="text-sm text-d3-dark dark:text-d3-light"
              data-testid="cxi-insufficient-members"
            >
              {t("kpi.cxiInsufficientMembers")}
            </p>
          )}
        </>
      )}
    </section>
  )
}
