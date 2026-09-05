// Wire + domain types for the M-06 KPI Management API.
//
// The .NET host serialises this module's contracts in snake_case (the M-10 convention — every
// field carries [JsonPropertyName]). So the WIRE types below mirror snake_case exactly, and the
// api.ts boundary maps them to camelCase DOMAIN types the components consume. Enum-valued fields
// (kpi_type, calculation_method, scale) arrive as their string member name (the controller projects
// the enums via .ToString()); `normalizeKpiType` is defensive against an integer slipping through
// per CLAUDE.md "Backend Integration".

/** The API-05 error envelope returned on every non-2xx response. */
export interface ApiErrorEnvelope {
  error: {
    code: string
    message: string
    correlation_id?: string
    tenant_id?: string
    details?: { field: string; code: string }[]
  }
}

export type KpiType = "Standard" | "Custom"

/** The `type` catalogue filter (kpi-api.md `GET /api/v1/kpis` query param). */
export type KpiTypeFilter = "All" | "Standard" | "Custom"

// ── Wire shape (snake_case, exactly as serialised) ──────────────────────────────

export interface KpiListItemWire {
  id: string
  short_name: string
  full_name: string
  kpi_type: string | number
  is_composite: boolean
  scale: string | null
  calculation_method: string
  calculation_method_label: string
  scale_label: string
  target: number | null
  is_active: boolean
  show_on_dashboard: boolean
  created_at: string
}

export interface KpiListResponseWire {
  items: KpiListItemWire[]
  next_cursor: string | null
}

// ── Domain shape (camelCase, consumed by hooks/components) ───────────────────────

export interface KpiListItem {
  id: string
  shortName: string
  fullName: string
  kpiType: KpiType
  isComposite: boolean
  scale: string | null
  calculationMethod: string
  calculationMethodLabel: string
  scaleLabel: string
  target: number | null
  isActive: boolean
  showOnDashboard: boolean
  createdAt: string
}

export interface KpiListResult {
  items: KpiListItem[]
  nextCursor: string | null
}

/** Defensive enum normalisation: accepts the string member name or the integer ordinal. */
export function normalizeKpiType(value: string | number): KpiType {
  if (value === "Standard" || value === 0) return "Standard"
  return "Custom"
}

export function mapKpiListItem(wire: KpiListItemWire): KpiListItem {
  return {
    id: wire.id,
    shortName: wire.short_name,
    fullName: wire.full_name,
    kpiType: normalizeKpiType(wire.kpi_type),
    isComposite: wire.is_composite,
    scale: wire.scale,
    calculationMethod: wire.calculation_method,
    calculationMethodLabel: wire.calculation_method_label,
    scaleLabel: wire.scale_label,
    target: wire.target,
    isActive: wire.is_active,
    showOnDashboard: wire.show_on_dashboard,
    createdAt: wire.created_at,
  }
}

// ── KPI configuration (US-2): create / read / update ────────────────────────────
//
// Enum-valued fields are string unions in the DOMAIN types (the canonical PascalCase member
// names). On the RESPONSE boundary the controller already projects them as strings (.ToString()),
// so reads map 1:1. On the REQUEST boundary the .NET host has NO JsonStringEnumConverter
// registered (CLAUDE.md "Backend Integration"), so System.Text.Json only binds enums from their
// integer ordinal — the `*_ORDINAL` maps below convert the string union → int at the request
// boundary (toCreate/toUpdate wire builders).

export type Scale =
  | "Scale0_10"
  | "Scale1_3"
  | "Scale1_5"
  | "Scale1_7"
  | "Scale1_10"
  | "Scale1_100"
  | "Nps"

export type CalculationMethodKind =
  | "WeightedAverage"
  | "TopNBox"
  | "NPSStandard"
  | "WeightedComposite"

export type RepresentationStyle = "Number" | "Stars" | "Emoji" | "Slider"

export type EmojiSet = "FaceClassic" | "HandThumbs"

// Enum ordinals — MUST match the C# enum declaration order (Domain/ValueObjects/*.cs).
const SCALE_ORDINAL: Record<Scale, number> = {
  Scale0_10: 0,
  Scale1_3: 1,
  Scale1_5: 2,
  Scale1_7: 3,
  Scale1_10: 4,
  Scale1_100: 5,
  Nps: 6,
}
const CALC_ORDINAL: Record<CalculationMethodKind, number> = {
  WeightedAverage: 0,
  TopNBox: 1,
  NPSStandard: 2,
  WeightedComposite: 3,
}
const REPRESENTATION_ORDINAL: Record<RepresentationStyle, number> = {
  Number: 0,
  Stars: 1,
  Emoji: 2,
  Slider: 3,
}
const EMOJI_SET_ORDINAL: Record<EmojiSet, number> = {
  FaceClassic: 0,
  HandThumbs: 1,
}

export interface BilingualText {
  en: string
  ar: string
}

export interface KpiThresholdBand {
  lowerBound: number
  x: number
  y: number
  upperBound: number
}

export interface KpiPerspective {
  id?: string
  label: string
  displayOrder: number
}

export interface CxiWeightItem {
  memberKpiId: string
  memberShortName: string
  weight: number
  effectivePercentage: number
}

/** Form output for `PUT /api/v1/kpis/{cxi_id}/weights` — relative integer weights, full-replace. */
export interface CxiWeightUpdateItem {
  memberKpiId: string
  weight: number
}

export interface KpiAudit {
  createdAt: string
  createdBy: string
  updatedAt: string
  updatedBy: string
}

/** Full KPI configuration (camelCase domain shape consumed by the config form/preview). */
export interface KpiDetail {
  id: string
  shortName: string
  fullName: string
  kpiType: KpiType
  isComposite: boolean
  calculationMethod: CalculationMethodKind
  topNValue: number | null
  scale: Scale | null
  minScaleDescription: BilingualText | null
  maxScaleDescription: BilingualText | null
  representationStyle: RepresentationStyle | null
  emojiSet: EmojiSet | null
  target: number | null
  isActive: boolean
  showOnDashboard: boolean
  thresholds: KpiThresholdBand
  perspectives: KpiPerspective[]
  cxiWeights: CxiWeightItem[] | null
  audit: KpiAudit | null
}

/** Form output → mapped to the snake_case + integer-enum request body by `toKpiConfigRequestWire`. */
export interface KpiSaveInput {
  shortName: string
  fullName: string
  perspectives: KpiPerspective[]
  calculationMethod: CalculationMethodKind
  topNValue: number | null
  scale: Scale | null
  minScaleDescription: BilingualText | null
  maxScaleDescription: BilingualText | null
  representationStyle: RepresentationStyle | null
  emojiSet: EmojiSet | null
  thresholds: { lowerBound: number; x: number; y: number; upperBound: number }
  target: number | null
  isActive: boolean
  showOnDashboard: boolean
}

// ── Wire shapes ─────────────────────────────────────────────────────────────────

interface BilingualWire {
  en: string | null
  ar: string | null
}

export interface KpiConfigResponseWire {
  id: string
  short_name: string
  full_name: string
  kpi_type: string | number
  is_composite: boolean
  calculation_method: string
  top_n_value: number | null
  scale: string | null
  min_scale_description: BilingualWire | null
  max_scale_description: BilingualWire | null
  representation_style: string | null
  emoji_set: string | null
  target: number | null
  is_active: boolean
  show_on_dashboard: boolean
  thresholds: { lower_bound: number; x: number; y: number; upper_bound: number }
  perspectives: { id: string; label: string; display_order: number }[]
  cxi_weights:
    | { member_kpi_id: string; member_short_name: string; weight: number; effective_percentage: number }[]
    | null
  audit: { created_at: string; created_by: string; updated_at: string; updated_by: string } | null
}

interface CxiWeightItemWire {
  member_kpi_id: string
  member_short_name: string
  weight: number
  effective_percentage: number
}

/** `PUT /api/v1/kpis/{cxi_id}/weights` 200 body — the recomputed weights table. */
export interface CxiWeightsResponseWire {
  weights: CxiWeightItemWire[]
}

export interface BindingUsageWire {
  touchpoint_count: number
  journey_count: number
}

export interface BindingUsage {
  touchpointCount: number
  journeyCount: number
}

// ── Mappers ──────────────────────────────────────────────────────────────────────

function mapBilingual(wire: BilingualWire | null): BilingualText | null {
  if (!wire) return null
  return { en: wire.en ?? "", ar: wire.ar ?? "" }
}

export function mapKpiDetail(wire: KpiConfigResponseWire): KpiDetail {
  return {
    id: wire.id,
    shortName: wire.short_name,
    fullName: wire.full_name,
    kpiType: normalizeKpiType(wire.kpi_type),
    isComposite: wire.is_composite,
    calculationMethod: wire.calculation_method as CalculationMethodKind,
    topNValue: wire.top_n_value,
    scale: (wire.scale as Scale | null) ?? null,
    minScaleDescription: mapBilingual(wire.min_scale_description),
    maxScaleDescription: mapBilingual(wire.max_scale_description),
    representationStyle: (wire.representation_style as RepresentationStyle | null) ?? null,
    emojiSet: (wire.emoji_set as EmojiSet | null) ?? null,
    target: wire.target,
    isActive: wire.is_active,
    showOnDashboard: wire.show_on_dashboard,
    thresholds: {
      lowerBound: wire.thresholds.lower_bound,
      x: wire.thresholds.x,
      y: wire.thresholds.y,
      upperBound: wire.thresholds.upper_bound,
    },
    perspectives: wire.perspectives.map((p) => ({
      id: p.id,
      label: p.label,
      displayOrder: p.display_order,
    })),
    cxiWeights:
      wire.cxi_weights?.map((w) => ({
        memberKpiId: w.member_kpi_id,
        memberShortName: w.member_short_name,
        weight: w.weight,
        effectivePercentage: w.effective_percentage,
      })) ?? null,
    audit: wire.audit
      ? {
          createdAt: wire.audit.created_at,
          createdBy: wire.audit.created_by,
          updatedAt: wire.audit.updated_at,
          updatedBy: wire.audit.updated_by,
        }
      : null,
  }
}

function bilingualToWire(text: BilingualText | null): BilingualWire | null {
  if (!text) return null
  if (!text.en && !text.ar) return null
  return { en: text.en || null, ar: text.ar || null }
}

/** Builds the snake_case + integer-enum request body for POST/PUT (request-boundary int converters). */
export function toKpiConfigRequestWire(input: KpiSaveInput): Record<string, unknown> {
  return {
    short_name: input.shortName,
    full_name: input.fullName,
    perspectives: input.perspectives.map((p) => ({
      label: p.label,
      display_order: p.displayOrder,
    })),
    calculation_method: CALC_ORDINAL[input.calculationMethod],
    top_n_value: input.topNValue,
    scale: input.scale != null ? SCALE_ORDINAL[input.scale] : null,
    min_scale_description: bilingualToWire(input.minScaleDescription),
    max_scale_description: bilingualToWire(input.maxScaleDescription),
    representation_style:
      input.representationStyle != null ? REPRESENTATION_ORDINAL[input.representationStyle] : null,
    emoji_set: input.emojiSet != null ? EMOJI_SET_ORDINAL[input.emojiSet] : null,
    thresholds: {
      lower_bound: input.thresholds.lowerBound,
      x: input.thresholds.x,
      y: input.thresholds.y,
      upper_bound: input.thresholds.upperBound,
    },
    target: input.target,
    is_active: input.isActive,
    show_on_dashboard: input.showOnDashboard,
  }
}

export function mapBindingUsage(wire: BindingUsageWire): BindingUsage {
  return { touchpointCount: wire.touchpoint_count, journeyCount: wire.journey_count }
}

export function mapCxiWeights(wire: CxiWeightsResponseWire): CxiWeightItem[] {
  return wire.weights.map((w) => ({
    memberKpiId: w.member_kpi_id,
    memberShortName: w.member_short_name,
    weight: w.weight,
    effectivePercentage: w.effective_percentage,
  }))
}

/** Builds the snake_case full-replace body; zero/negative weights are dropped (BR-2.3). */
export function toCxiWeightsRequestWire(weights: CxiWeightUpdateItem[]): Record<string, unknown> {
  return {
    weights: weights
      .filter((w) => w.weight > 0)
      .map((w) => ({ member_kpi_id: w.memberKpiId, weight: w.weight })),
  }
}
