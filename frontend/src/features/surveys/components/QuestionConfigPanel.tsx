// Right-hand question config panel (clickthrough parity): everything about the
// selected question is edited here — common fields (text, description, type,
// sub-type, comments, required) followed by a type-specific block. Field
// dependencies mirror the reference: Scale points hide for Slider; per-point labels
// only for the Labels view; slider bounds only for Slider; sentiment only for
// free-text input fields; Matrix columns only for custom mode (KPI mode derives
// faces) and its rows relabel as perspectives; KPI gets the connection block
// (real M-06/M-16 catalogues via KpiBindingEditor), representation, N/A and the
// score-based reason follow-up. Paragraph elements get a minimal text editor.

import { useEffect, useState } from "react"
import { useTranslation } from "react-i18next"
import { Info, MessageSquare, Minus, Plus, Settings2, Trash2, X } from "lucide-react"

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
import { Switch } from "@/components/ui/switch"
import { Textarea } from "@/components/ui/textarea"
import { cn } from "@/lib/utils"
import { getKpi, listKpis, type KpiListItem } from "@/features/kpi-management/api"
import {
  changeBuilderQuestionType,
  isSentimentEligible,
  kpiScaleRange,
  SUBTYPES_BY_TYPE,
  type BuilderQuestion,
  type BuilderQuestionType,
  type KpiBindingDraft,
} from "./builder-types"
import { KpiBindingEditor } from "./KpiBindingEditor"

const TYPE_KEYS: { type: BuilderQuestionType; labelKey: string }[] = [
  { type: "Kpi", labelKey: "surveysModule.palette.kpi" },
  { type: "Scale", labelKey: "surveysModule.palette.scale" },
  { type: "InputField", labelKey: "surveysModule.palette.inputField" },
  { type: "SingleSelect", labelKey: "surveysModule.palette.singleSelect" },
  { type: "MultiSelect", labelKey: "surveysModule.palette.multiSelect" },
  { type: "YesNo", labelKey: "surveysModule.palette.yesNo" },
  { type: "Matrix", labelKey: "surveysModule.palette.matrix" },
  { type: "Ranking", labelKey: "surveysModule.palette.ranking" },
]

/** Reference-style list editor: one input row per item + remove, plus an add row. */
function ListEditor({
  items,
  onChange,
  addLabel,
  disabled,
}: {
  items: string[]
  onChange: (items: string[]) => void
  addLabel: string
  disabled?: boolean
}) {
  const { t } = useTranslation()
  return (
    <div className="space-y-2">
      {items.map((item, i) => (
        <div key={i} className="flex items-center gap-1.5">
          <Input
            value={item}
            onChange={(e) => onChange(items.map((v, j) => (j === i ? e.target.value : v)))}
            className="h-9 flex-1"
            aria-label={`${addLabel} ${i + 1}`}
            disabled={disabled}
          />
          <Button
            variant="ghost"
            size="icon-sm"
            aria-label={t("common.delete")}
            onClick={() => onChange(items.filter((_, j) => j !== i))}
            disabled={disabled}
          >
            <X className="size-4" aria-hidden />
          </Button>
        </div>
      ))}
      <button
        type="button"
        onClick={() => onChange([...items, ""])}
        disabled={disabled}
        className="inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline disabled:opacity-50"
      >
        <Plus className="size-3.5" aria-hidden />
        {addLabel}
      </button>
    </div>
  )
}

function NumberStepper({
  value,
  min,
  max,
  onChange,
  disabled,
}: {
  value: number
  min: number
  max: number
  onChange: (v: number) => void
  disabled?: boolean
}) {
  const { t } = useTranslation()
  const clamp = (v: number) => Math.max(min, Math.min(max, v))
  return (
    <div className="inline-flex items-center rounded-md border border-input bg-card">
      <button
        type="button"
        aria-label={t("surveysModule.settings.decrease")}
        onClick={() => onChange(clamp(value - 1))}
        disabled={disabled}
        className="inline-flex size-9 items-center justify-center rounded-s-md text-muted-foreground transition-colors hover:bg-accent hover:text-foreground"
      >
        <Minus className="size-4" aria-hidden />
      </button>
      <span className="w-10 text-center text-sm font-semibold tabular-nums">{value}</span>
      <button
        type="button"
        aria-label={t("surveysModule.settings.increase")}
        onClick={() => onChange(clamp(value + 1))}
        disabled={disabled}
        className="inline-flex size-9 items-center justify-center rounded-e-md text-muted-foreground transition-colors hover:bg-accent hover:text-foreground"
      >
        <Plus className="size-4" aria-hidden />
      </button>
    </div>
  )
}

function SubHeading({ children }: { children: React.ReactNode }) {
  return (
    <p className="border-t border-border pt-4 text-xs font-semibold uppercase tracking-widest text-muted-foreground">
      {children}
    </p>
  )
}

function InfoNote({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex items-start gap-2 rounded-md border border-border bg-accent p-3 text-xs leading-relaxed text-muted-foreground">
      <Info className="mt-0.5 size-3.5 shrink-0 text-primary" aria-hidden />
      <span>{children}</span>
    </div>
  )
}

function SwitchRow({
  id,
  label,
  help,
  checked,
  onChange,
  disabled,
}: {
  id: string
  label: string
  help?: string
  checked: boolean
  onChange: (v: boolean) => void
  disabled?: boolean
}) {
  return (
    <div className="flex items-start justify-between gap-3">
      <div className="min-w-0">
        <Label htmlFor={id} className="cursor-pointer">
          {label}
        </Label>
        {help && <p className="mt-0.5 text-xs leading-relaxed text-muted-foreground">{help}</p>}
      </div>
      <Switch id={id} checked={checked} onCheckedChange={onChange} disabled={disabled} />
    </div>
  )
}

export function QuestionConfigPanel({
  question: q,
  boundJourneyId,
  onChange,
  onRemove,
  disabled,
}: {
  question: BuilderQuestion | null
  boundJourneyId: string | null
  onChange: (next: BuilderQuestion) => void
  onRemove: (localId: string) => void
  disabled?: boolean
}) {
  const { t } = useTranslation()
  // KPI catalogue for the Matrix KPI-scale picker (KpiBindingEditor loads its own).
  const [kpis, setKpis] = useState<KpiListItem[]>([])
  useEffect(() => {
    if (q?.type === "Matrix" || q?.type === "Kpi") {
      listKpis({ activeOnly: true })
        .then((r) => setKpis(r.items))
        .catch(() => setKpis([]))
    }
  }, [q?.type])

  // Reference parity: a new KPI question defaults to CSAT (first active KPI
  // otherwise) so the picker and the canvas preview are populated immediately.
  useEffect(() => {
    if (q && q.type === "Kpi" && !q.kpi.kpiKey && kpis.length > 0) {
      const def = kpis.find((k) => k.shortName.toUpperCase() === "CSAT") ?? kpis[0]
      applyKpiBinding({
        ...q.kpi,
        kpiKey: def.shortName,
        perspectiveLabel: null,
        touchpointId: null,
        touchpointDisplay: null,
      })
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- fire on selection/kpis only
  }, [q?.localId, q?.type, kpis])

  if (!q) {
    return (
      <div className="flex h-full min-h-64 flex-col items-center justify-center gap-3 p-6 text-center">
        <Settings2 className="size-8 text-muted-foreground" aria-hidden />
        <p className="max-w-48 text-sm text-muted-foreground">
          {t("surveysModule.config.selectPrompt")}
        </p>
      </div>
    )
  }

  const set = (patch: Partial<BuilderQuestion>) => onChange({ ...q, ...patch })

  /** KPI binding changes flow through here so the canvas preview can follow the
   * KPI's own scale (0..10 for NPS, …) — resolved from the M-06 catalogue. */
  const applyKpiBinding = (kpi: KpiBindingDraft) => {
    const kpiChanged = kpi.kpiKey !== q.kpi.kpiKey
    onChange({ ...q, kpi, ...(kpiChanged ? { kpiScaleValues: null } : {}) })
    if (kpiChanged && kpi.kpiKey) {
      getKpi(kpi.kpiKey)
        .then((detail) =>
          onChange({ ...q, kpi, kpiScaleValues: kpiScaleRange(detail.scale) })
        )
        .catch(() => undefined)
    }
  }

  /** Matrix KPI-scale dependency (reference parity): picking the source KPI reseeds
   * the rows with that KPI's perspectives (renameable/removable afterwards). */
  const seedMatrixFromKpi = (shortName: string) => {
    onChange({ ...q, matrixKpi: shortName })
    getKpi(shortName)
      .then((detail) => {
        const rows = (detail.perspectives ?? []).map((p) => p.label)
        const points = kpiScaleRange(detail.scale)?.length ?? null
        onChange({
          ...q,
          matrixKpi: shortName,
          matrixKpiPoints: points,
          ...(rows.length > 0 ? { matrixRows: rows } : {}),
        })
      })
      .catch(() => undefined)
  }

  const changeSubType = (next: BuilderQuestion["subType"]) => {
    // Switching a Matrix to the KPI scale seeds the rows from the default KPI.
    if (q.type === "Matrix" && next === "KpiScale") {
      const first = q.matrixKpi ?? kpis[0]?.shortName
      set({ subType: next })
      if (first) seedMatrixFromKpi(first)
      return
    }
    set({ subType: next })
  }

  // ── Paragraph branch ────────────────────────────────────────────────────────
  if (q.type === "Paragraph") {
    return (
      <div className="space-y-4 p-4">
        <h2 className="text-sm font-bold">{t("surveysModule.config.paragraphTitle")}</h2>
        <div className="space-y-2">
          <Label htmlFor="cfg-paragraph">{t("surveysModule.config.paragraphText")}</Label>
          <Textarea
            id="cfg-paragraph"
            value={q.text}
            onChange={(e) => set({ text: e.target.value })}
            className="min-h-32"
            disabled={disabled}
          />
          <p className="text-xs text-muted-foreground">
            {t("surveysModule.config.paragraphHelp")}
          </p>
        </div>
        <Button
          variant="outline"
          className="w-full"
          onClick={() => onRemove(q.localId)}
          disabled={disabled}
        >
          <Trash2 className="size-4" aria-hidden />
          {t("surveysModule.config.deleteParagraph")}
        </Button>
      </div>
    )
  }

  // Sub-type slot: real backend sub-types where they exist, else the reference's
  // display variants (UI-local — MultiSelect/YesNo/Ranking have no backend sub-type).
  const subTypes = SUBTYPES_BY_TYPE[q.type]

  return (
    <div className="space-y-4">
      {/* Common fields (the page column provides the panel heading) */}
      <div className="space-y-2">
        <Label htmlFor="cfg-text">{t("surveysModule.question.text")}</Label>
        <Input
          id="cfg-text"
          value={q.text}
          onChange={(e) => set({ text: e.target.value })}
          disabled={disabled}
        />
      </div>

      <div className="space-y-2">
        {/* Reference style: bold label + muted hint flowing on the same line. */}
        <Label htmlFor="cfg-desc" className="inline leading-relaxed">
          {t("surveysModule.question.description")}{" "}
          <span className="font-normal text-muted-foreground">
            {t("surveysModule.config.descriptionHint")}
          </span>
        </Label>
        <Textarea
          id="cfg-desc"
          value={q.description}
          onChange={(e) => set({ description: e.target.value })}
          placeholder={t("surveysModule.config.descriptionPlaceholder")}
          className="min-h-16"
          disabled={disabled}
        />
      </div>

      <div className="space-y-2">
        <Label htmlFor="cfg-type">{t("surveysModule.config.typeLabel")}</Label>
        <Select
          value={q.type}
          onValueChange={(v) => v && onChange(changeBuilderQuestionType(q, v as BuilderQuestionType))}
          disabled={disabled}
        >
          <SelectTrigger id="cfg-type" className="w-full">
            <SelectValue>
              {(v) =>
                t(TYPE_KEYS.find((k) => k.type === (v ?? q.type))?.labelKey ?? "surveysModule.palette.scale")
              }
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            {TYPE_KEYS.map(({ type, labelKey }) => (
              <SelectItem key={type} value={type}>
                {t(labelKey)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* InputField renders its field-type select inside the "Field type" section
          below (reference parity), so it is excluded from the generic sub-type slot. */}
      {subTypes.length > 0 && q.type !== "InputField" && (
        <div className="space-y-2">
          <Label htmlFor="cfg-subtype" className="inline leading-relaxed">
            {t("surveysModule.question.subType")}{" "}
            <span className="font-normal text-muted-foreground">
              {t("surveysModule.config.subTypeHint")}
            </span>
          </Label>
          <Select
            value={q.subType}
            onValueChange={(v) => v && changeSubType(v as BuilderQuestion["subType"])}
            disabled={disabled}
          >
            <SelectTrigger id="cfg-subtype" className="w-full">
              <SelectValue>
                {(v) => t(`surveysModule.subType.${String(v ?? q.subType)}`)}
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              {subTypes.map((s) => (
                <SelectItem key={s} value={s}>
                  {t(`surveysModule.subType.${s}`)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      )}

      {/* KPI questions: the sub-type slot IS the KPI picker (reference parity) —
          it mirrors kpi.kpiKey, so it stays in sync with the connection block. */}
      {q.type === "Kpi" && (
        <div className="space-y-2">
          <Label htmlFor="cfg-kpi-subtype" className="inline leading-relaxed">
            {t("surveysModule.question.subType")}{" "}
            <span className="font-normal text-muted-foreground">
              {t("surveysModule.config.subTypeHint")}
            </span>
          </Label>
          <Select
            value={q.kpi.kpiKey ?? ""}
            onValueChange={(v) =>
              v &&
              applyKpiBinding({ ...q.kpi, kpiKey: v, perspectiveLabel: null, touchpointId: null })
            }
            disabled={disabled}
          >
            <SelectTrigger id="cfg-kpi-subtype" className="w-full">
              <SelectValue>
                {(v) => String(v ?? q.kpi.kpiKey ?? "") || t("surveysModule.kpi.kpiPlaceholder")}
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              {kpis.map((k) => (
                <SelectItem key={k.shortName} value={k.shortName}>
                  {k.shortName}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      )}

      {/* Display-variant selects for types without a backend sub-type */}
      {q.type === "MultiSelect" && (
        <VariantSelect
          id="cfg-multidisplay"
          value={q.multiDisplay}
          onChange={(v) => set({ multiDisplay: v as BuilderQuestion["multiDisplay"] })}
          options={[
            ["checkboxes", t("surveysModule.config.multiCheckboxes")],
            ["dropdown", t("surveysModule.config.multiDropdown")],
          ]}
          disabled={disabled}
        />
      )}
      {q.type === "YesNo" && (
        <VariantSelect
          id="cfg-booldisplay"
          value={q.boolDisplay}
          onChange={(v) => set({ boolDisplay: v as BuilderQuestion["boolDisplay"] })}
          options={[
            ["buttons", t("surveysModule.config.boolButtons")],
            ["toggle", t("surveysModule.config.boolToggle")],
          ]}
          disabled={disabled}
        />
      )}
      {q.type === "Ranking" && (
        <VariantSelect
          id="cfg-rankdisplay"
          value={q.rankDisplay}
          onChange={(v) => set({ rankDisplay: v as BuilderQuestion["rankDisplay"] })}
          options={[
            ["drag", t("surveysModule.config.rankDrag")],
            ["numbered", t("surveysModule.config.rankNumbered")],
          ]}
          disabled={disabled}
        />
      )}

      <SwitchRow
        id="cfg-comments"
        label={t("surveysModule.question.showComments")}
        help={t("surveysModule.config.commentsHelp")}
        checked={q.showComments}
        onChange={(v) => set({ showComments: v })}
        disabled={disabled}
      />
      <SwitchRow
        id="cfg-required"
        label={t("surveysModule.question.required")}
        help={t("surveysModule.config.requiredHelp")}
        checked={q.required}
        onChange={(v) => set({ required: v })}
        disabled={disabled}
      />

      {/* ── Type-specific blocks ─────────────────────────────────────────────── */}

      {q.type === "Kpi" && (
        <>
          <SubHeading>{t("surveysModule.config.kpiConnection")}</SubHeading>
          <KpiBindingEditor
            value={q.kpi}
            onChange={applyKpiBinding}
            boundJourneyId={boundJourneyId}
            disabled={disabled}
            idPrefix="cfg-kpi"
          />
          <SwitchRow
            id="cfg-allowna"
            label={t("surveysModule.config.allowNA")}
            help={t("surveysModule.config.allowNAHelp")}
            checked={q.allowNA}
            onChange={(v) => set({ allowNA: v })}
            disabled={disabled}
          />
          <InfoNote>{t("surveysModule.config.kpiInfoNote")}</InfoNote>

          {/* Score-based reason follow-up (reference: icon row after a divider) */}
          <div className="flex items-start justify-between gap-3 border-t border-border pt-4">
            <Label htmlFor="cfg-reason" className="flex cursor-pointer items-center gap-2">
              <MessageSquare className="size-4 shrink-0 text-muted-foreground" aria-hidden />
              {t("surveysModule.config.reasonToggle")}
            </Label>
            <Switch
              id="cfg-reason"
              checked={q.reason.on}
              onCheckedChange={(v) => set({ reason: { ...q.reason, on: v } })}
              disabled={disabled}
            />
          </div>
          {q.reason.on && (
            <div className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="cfg-reason-prompt">
                  {t("surveysModule.config.reasonPrompt")}
                </Label>
                <Input
                  id="cfg-reason-prompt"
                  value={q.reason.prompt}
                  onChange={(e) => set({ reason: { ...q.reason, prompt: e.target.value } })}
                  placeholder={t("surveysModule.config.reasonPromptPlaceholder")}
                  disabled={disabled}
                />
              </div>
              <div className="space-y-2">
                <span className="text-sm font-medium">
                  {t("surveysModule.config.reasonSelection")}
                </span>
                <div className="flex w-full overflow-hidden rounded-md border border-input p-0.5">
                  {(
                    [
                      [false, t("surveysModule.config.reasonSingle")],
                      [true, t("surveysModule.config.reasonMulti")],
                    ] as const
                  ).map(([multi, label]) => (
                    <button
                      key={String(multi)}
                      type="button"
                      onClick={() => set({ reason: { ...q.reason, multi } })}
                      disabled={disabled}
                      className={cn(
                        "flex-1 rounded-sm px-3 py-1.5 text-sm font-medium transition-colors",
                        q.reason.multi === multi
                          ? "bg-primary text-primary-foreground"
                          : "bg-card text-muted-foreground hover:bg-accent hover:text-foreground"
                      )}
                    >
                      {label}
                    </button>
                  ))}
                </div>
              </div>
              <div className="space-y-2">
                <span className="text-sm font-medium">{t("surveysModule.config.reasons")}</span>
                <ListEditor
                  items={q.reason.reasons}
                  onChange={(reasons) => set({ reason: { ...q.reason, reasons } })}
                  addLabel={t("surveysModule.config.addReason")}
                  disabled={disabled}
                />
              </div>
              <SwitchRow
                id="cfg-reason-other"
                label={t("surveysModule.config.reasonOther")}
                checked={q.reason.hasOther}
                onChange={(v) => set({ reason: { ...q.reason, hasOther: v } })}
                disabled={disabled}
              />
            </div>
          )}
        </>
      )}

      {q.type === "Scale" && (
        <>
          <SubHeading>{t("surveysModule.config.scaleOptions")}</SubHeading>
          {q.subType !== "Slider" && (
            // Reference layout: label and stepper share one row.
            <div className="flex items-center justify-between gap-3">
              <span className="text-sm font-medium">{t("surveysModule.config.points")}</span>
              <NumberStepper
                value={q.scalePoints}
                min={2}
                max={10}
                onChange={(v) => set({ scalePoints: v })}
                disabled={disabled}
              />
            </div>
          )}
          {q.subType === "Labels" && (
            <div className="space-y-2">
              <span className="text-sm font-medium">
                {t("surveysModule.config.pointLabels")}{" "}
                <span className="font-normal text-muted-foreground">
                  {t("surveysModule.config.pointLabelsHint")}
                </span>
              </span>
              <div className="space-y-2">
                {Array.from({ length: Math.max(2, q.scalePoints) }).map((_, i) => (
                  <Input
                    key={i}
                    value={q.pointLabels[i] ?? ""}
                    onChange={(e) => {
                      const next = [...q.pointLabels]
                      next[i] = e.target.value
                      set({ pointLabels: next })
                    }}
                    placeholder={String(i + 1)}
                    aria-label={`${t("surveysModule.config.pointLabels")} ${i + 1}`}
                    className="h-9"
                    disabled={disabled}
                  />
                ))}
              </div>
            </div>
          )}
          {q.subType === "Slider" && (
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label htmlFor="cfg-slider-min">{t("surveysModule.config.sliderMin")}</Label>
                <Input
                  id="cfg-slider-min"
                  type="number"
                  value={q.sliderMin}
                  onChange={(e) => set({ sliderMin: Number(e.target.value) })}
                  className="tabular-nums"
                  disabled={disabled}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="cfg-slider-max">{t("surveysModule.config.sliderMax")}</Label>
                <Input
                  id="cfg-slider-max"
                  type="number"
                  value={q.sliderMax}
                  onChange={(e) => set({ sliderMax: Number(e.target.value) })}
                  className="tabular-nums"
                  disabled={disabled}
                />
              </div>
              <div className="col-span-2 space-y-2">
                <span className="text-sm font-medium">
                  {t("surveysModule.question.sliderSteps")}
                </span>
                <div>
                  <NumberStepper
                    value={q.sliderSteps}
                    min={1}
                    max={20}
                    onChange={(v) => set({ sliderSteps: v })}
                    disabled={disabled}
                  />
                </div>
                <p className="text-xs text-muted-foreground">
                  {t("surveysModule.config.sliderTicksHelp", {
                    ticks: Array.from({ length: Math.max(1, q.sliderSteps) + 1 }, (_, i) =>
                      Math.round(
                        q.sliderMin + (i * (q.sliderMax - q.sliderMin)) / Math.max(1, q.sliderSteps)
                      )
                    ).join(", "),
                  })}
                </p>
              </div>
            </div>
          )}
        </>
      )}

      {q.type === "InputField" && (
        <>
          <SubHeading>{t("surveysModule.config.fieldOptions")}</SubHeading>
          <Select
            value={q.subType}
            onValueChange={(v) => v && changeSubType(v as BuilderQuestion["subType"])}
            disabled={disabled}
          >
            <SelectTrigger id="cfg-fieldtype" className="w-full">
              <SelectValue>
                {(v) => t(`surveysModule.subType.${String(v ?? q.subType)}`)}
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              {subTypes.map((s) => (
                <SelectItem key={s} value={s}>
                  {t(`surveysModule.subType.${s}`)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <p className="text-xs text-muted-foreground">
            {t("surveysModule.config.fieldTypeHelp")}
          </p>
          {isSentimentEligible(q.type, q.subType) && (
            <SwitchRow
              id="cfg-sentiment"
              label={t("surveysModule.question.applySentiment")}
              help={t("surveysModule.config.sentimentHelp")}
              checked={q.applySentiment}
              onChange={(v) => set({ applySentiment: v })}
              disabled={disabled}
            />
          )}
        </>
      )}

      {(q.type === "SingleSelect" || q.type === "MultiSelect" || q.type === "Ranking") && (
        <>
          <SubHeading>{t("surveysModule.question.options")}</SubHeading>
          <ListEditor
            items={q.type === "Ranking" ? q.rankingItems : q.options}
            onChange={(items) =>
              q.type === "Ranking" ? set({ rankingItems: items }) : set({ options: items })
            }
            addLabel={
              q.type === "Ranking"
                ? t("surveysModule.question.addItem")
                : t("surveysModule.question.addOption")
            }
            disabled={disabled}
          />
        </>
      )}

      {q.type === "YesNo" && (
        <>
          <SubHeading>{t("surveysModule.config.answerLabels")}</SubHeading>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-2">
              <Label htmlFor="cfg-yes">{t("surveysModule.question.yesLabel")}</Label>
              <Input
                id="cfg-yes"
                value={q.yesLabel}
                onChange={(e) => set({ yesLabel: e.target.value })}
                placeholder={t("common.yes")}
                disabled={disabled}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="cfg-no">{t("surveysModule.question.noLabel")}</Label>
              <Input
                id="cfg-no"
                value={q.noLabel}
                onChange={(e) => set({ noLabel: e.target.value })}
                placeholder={t("common.no")}
                disabled={disabled}
              />
            </div>
          </div>
        </>
      )}

      {q.type === "Matrix" && (
        <>
          {/* Reference order: the KPI-mode note sits ABOVE the Rating scale group. */}
          <InfoNote>{t("surveysModule.config.matrixInfoNote")}</InfoNote>
          <SubHeading>{t("surveysModule.config.matrixRatingScale")}</SubHeading>
          {q.subType === "KpiScale" && (
            <div className="space-y-2">
              <Label htmlFor="cfg-matrix-kpi">{t("surveysModule.config.matrixKpi")}</Label>
              <Select
                value={q.matrixKpi ?? ""}
                onValueChange={(v) => v && seedMatrixFromKpi(v)}
                disabled={disabled}
              >
                <SelectTrigger id="cfg-matrix-kpi" className="w-full">
                  <SelectValue>
                    {(v) => (v ? String(v) : t("surveysModule.kpi.kpiPlaceholder"))}
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  {kpis.map((k) => (
                    <SelectItem key={k.shortName} value={k.shortName}>
                      {k.shortName} ({k.fullName})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                {t("surveysModule.config.matrixKpiHelp", { kpi: q.matrixKpi ?? "" })}
              </p>
            </div>
          )}
          <div className="space-y-2">
            <span className="text-sm font-medium">
              {q.subType === "KpiScale"
                ? t("surveysModule.config.matrixPerspectives")
                : t("surveysModule.question.matrixRows")}
            </span>
            {q.subType === "KpiScale" && (
              <p className="text-xs text-muted-foreground">
                {t("surveysModule.config.matrixPerspectivesHelp")}
              </p>
            )}
            <ListEditor
              items={q.matrixRows}
              onChange={(matrixRows) => set({ matrixRows })}
              addLabel={
                q.subType === "KpiScale"
                  ? t("surveysModule.config.addPerspective")
                  : t("surveysModule.question.addRow")
              }
              disabled={disabled}
            />
          </div>
          {q.subType === "CustomColumns" && (
            <div className="space-y-2">
              <span className="text-sm font-medium">
                {t("surveysModule.question.matrixColumns")}
              </span>
              <ListEditor
                items={q.matrixColumns}
                onChange={(matrixColumns) => set({ matrixColumns })}
                addLabel={t("surveysModule.question.addColumn")}
                disabled={disabled}
              />
            </div>
          )}
          <SwitchRow
            id="cfg-matrix-na"
            label={t("surveysModule.config.allowNA")}
            help={t("surveysModule.config.allowNAColumnHelp")}
            checked={q.allowNA}
            onChange={(v) => set({ allowNA: v })}
            disabled={disabled}
          />
        </>
      )}
    </div>
  )
}

function VariantSelect({
  id,
  value,
  onChange,
  options,
  disabled,
}: {
  id: string
  value: string
  onChange: (v: string) => void
  options: [string, string][]
  disabled?: boolean
}) {
  const { t } = useTranslation()
  return (
    <div className="space-y-2">
      <Label htmlFor={id} className="inline leading-relaxed">
        {t("surveysModule.question.subType")}{" "}
        <span className="font-normal text-muted-foreground">
          {t("surveysModule.config.subTypeHint")}
        </span>
      </Label>
      <Select value={value} onValueChange={(v) => v && onChange(v)} disabled={disabled}>
        <SelectTrigger id={id} className="w-full">
          <SelectValue>
            {(v) => options.find(([key]) => key === (v ?? value))?.[1] ?? ""}
          </SelectValue>
        </SelectTrigger>
        <SelectContent>
          {options.map(([key, label]) => (
            <SelectItem key={key} value={key}>
              {label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  )
}
