// Builder canvas model (US1 + US3, reworked for clickthrough parity). Writes persist
// through the T152 API modules (`sections-api.ts` / `questions-sets-api.ts` /
// `questions-api.ts`) — `serverId` / `rowVersion` track the persisted row + its ETag.
// The backend ships no structure GET (TODO-M01-028), so the model is (re)built from
// write responses within a session. Vocabulary mirrors the backend QuestionType /
// QuestionSubType enums 1:1, plus the local-only "Paragraph" element (static text —
// no backend type exists for it, so it never persists; see TODO note in the page).

export type BuilderQuestionType =
  | "Scale"
  | "InputField"
  | "SingleSelect"
  | "MultiSelect"
  | "YesNo"
  | "Matrix"
  | "Ranking"
  | "Kpi"
  /** Local-only static text element (section ⋮ menu → Add paragraph). */
  | "Paragraph"

export type BuilderSubType =
  | "None"
  // Scale
  | "Labels"
  | "Stars"
  | "Smileys"
  | "Slider"
  // InputField
  | "Text"
  | "Paragraph"
  | "Number"
  | "Date"
  | "Time"
  | "DateTime"
  | "Month"
  // SingleSelect
  | "List"
  | "Dropdown"
  // Matrix
  | "CustomColumns"
  | "KpiScale"

/** Sub-type vocabulary per type (FR-8.8 — types with an empty list need no sub-type). */
export const SUBTYPES_BY_TYPE: Record<BuilderQuestionType, BuilderSubType[]> = {
  Scale: ["Labels", "Stars", "Smileys", "Slider"],
  InputField: ["Text", "Paragraph", "Number", "Date", "Time", "DateTime", "Month"],
  SingleSelect: ["List", "Dropdown"],
  MultiSelect: [],
  YesNo: [],
  Matrix: ["CustomColumns", "KpiScale"],
  Ranking: [],
  Kpi: [],
  Paragraph: [],
}

/** Sentiment analysis applies to free-text answers only (FR-8.11). */
export function isSentimentEligible(type: BuilderQuestionType, subType: BuilderSubType): boolean {
  return type === "InputField" && (subType === "Text" || subType === "Paragraph")
}

/**
 * FR-9.5 + slider exclusion — mirrors the backend `IsRoutingEligible`: set questions are
 * never routable; otherwise Scale (non-slider), Single select, Yes/No and KPI are.
 */
export function isRoutingEligible(
  type: BuilderQuestionType,
  subType: BuilderSubType,
  insideSet: boolean
): boolean {
  if (insideSet) return false
  switch (type) {
    case "Scale":
      return subType !== "Slider"
    case "SingleSelect":
    case "YesNo":
    case "Kpi":
      return true
    default:
      return false
  }
}

/**
 * The answer keys a question can route FROM (one editor row each). Mirrors the wire
 * `answer_key` vocabulary: scale/KPI points are "1".."N", selects use the option text,
 * Yes/No uses "yes"/"no".
 */
export function routingAnswerKeys(q: BuilderQuestion): string[] {
  switch (q.type) {
    case "Scale":
      return Array.from({ length: Math.max(2, q.scalePoints) }, (_, i) => String(i + 1))
    case "Kpi":
      return Array.from({ length: 5 }, (_, i) => String(i + 1))
    case "SingleSelect":
      return q.options
    case "YesNo":
      return ["yes", "no"]
    default:
      return []
  }
}

export interface KpiBindingDraft {
  /** M-06 catalogue shortName key (e.g. "CSAT"). */
  kpiKey: string | null
  perspectiveLabel: string | null
  /** Default ON for KPI questions (FR-8.4). */
  boundJourneyOn: boolean
  stageId: string | null
  touchpointId: string | null
  /** Resolved "stage → touchpoint" names for the card chip (UI-only). */
  touchpointDisplay: string | null
}

/** Score-based reason follow-up (KPI questions) — builder-local config; the wire has
 * no reason fields yet, so this never persists (UI parity with the reference). */
export interface ReasonFollowUp {
  on: boolean
  prompt: string
  multi: boolean
  reasons: string[]
  hasOther: boolean
}

export interface BuilderQuestion {
  localId: string
  /** Persisted question id + ETag row version; null until the create lands. */
  serverId: string | null
  rowVersion: number | null
  /** True when a saved routing override map exists ("Routing set" badge, US4). */
  hasRoutingMap: boolean
  type: BuilderQuestionType
  subType: BuilderSubType
  text: string
  description: string
  required: boolean
  /** FR-8.9 — adds the optional "Comments" field that travels to NLP. */
  showComments: boolean
  /** FR-8.11 — only honoured for free-text (isSentimentEligible). */
  applySentiment: boolean
  /** "Routing set" badge slot — populated by the US4 routing tasks. */
  hasRouting: boolean
  // Per-type settings (only the group matching `type` is meaningful)
  scalePoints: number
  /** Per-point display labels (Labels view); blank entry = show the number. */
  pointLabels: string[]
  scaleMinLabel: string
  scaleMaxLabel: string
  sliderMin: number
  sliderMax: number
  sliderSteps: number
  options: string[]
  yesLabel: string
  noLabel: string
  matrixRows: string[]
  matrixColumns: string[]
  /** Matrix KPI-scale mode: which KPI's scale drives the columns (UI-only). */
  matrixKpi: string | null
  /** The matrix KPI's scale point count (faces count, capped at 7 in preview). */
  matrixKpiPoints: number | null
  rankingItems: string[]
  /** KPI question visual representation (stars / smileys / scale). */
  kpiRepresentation: "Scale" | "Stars" | "Smileys"
  /** The selected KPI's own scale values (e.g. 0..10 for NPS) — drives the canvas
   * preview (UI-only, resolved from the M-06 catalogue when the KPI is picked). */
  kpiScaleValues: number[] | null
  /** Adds an N/A choice (KPI + Matrix). */
  allowNA: boolean
  kpi: KpiBindingDraft
  /** Score-based reason follow-up (KPI only, UI-local). */
  reason: ReasonFollowUp
  // Display variants with no backend column (UI-local, still shown per the reference)
  multiDisplay: "checkboxes" | "dropdown"
  boolDisplay: "buttons" | "toggle"
  rankDisplay: "drag" | "numbered"
}

/** A Questions Set (F10) — a sub-window inside a section showing k of n members. */
export interface BuilderSet {
  localId: string
  serverId: string | null
  rowVersion: number | null
  title: string
  description: string
  selectionMode: "random" | "low_response"
  /** How many member questions each respondent receives (≤ questions.length). */
  count: number
  questions: BuilderQuestion[]
}

export interface BuilderSection {
  localId: string
  serverId: string | null
  rowVersion: number | null
  title: string
  questions: BuilderQuestion[]
  sets: BuilderSet[]
}

let counter = 0
function nextLocalId(prefix: string): string {
  counter += 1
  return `${prefix}-${Date.now().toString(36)}-${counter}`
}

/** Per-type seeds — mirrors the reference `makeQuestion` so a fresh question renders
 * a meaningful preview immediately. */
const TYPE_SEED_LABEL: Record<BuilderQuestionType, string> = {
  Scale: "Scale",
  InputField: "Input Field",
  SingleSelect: "Single select",
  MultiSelect: "Multi-select",
  YesNo: "Yes/No",
  Matrix: "Single-select matrix",
  Ranking: "Ranking",
  Kpi: "KPI",
  Paragraph: "Paragraph",
}

export function newBuilderQuestion(type: BuilderQuestionType): BuilderQuestion {
  const subTypes = SUBTYPES_BY_TYPE[type]
  return {
    localId: nextLocalId("q"),
    serverId: null,
    rowVersion: null,
    hasRoutingMap: false,
    type,
    subType: subTypes.length > 0 ? subTypes[0] : "None",
    // Reference seeds: a type-specific working title, so the card and the auto-
    // persist have a valid text immediately (question.text.required).
    text:
      type === "Paragraph"
        ? "New paragraph text…"
        : `Untitled ${TYPE_SEED_LABEL[type]} question`,
    description: "",
    required: false,
    showComments: false,
    applySentiment: false,
    hasRouting: false,
    scalePoints: 5,
    pointLabels: [],
    scaleMinLabel: "",
    scaleMaxLabel: "",
    sliderMin: 0,
    sliderMax: 100,
    sliderSteps: 5,
    options:
      type === "SingleSelect" || type === "MultiSelect" ? ["Option 1", "Option 2"] : [],
    yesLabel: type === "YesNo" ? "Yes" : "",
    noLabel: type === "YesNo" ? "No" : "",
    matrixRows: type === "Matrix" ? ["Row 1", "Row 2"] : [],
    matrixColumns: type === "Matrix" ? ["Column 1", "Column 2"] : [],
    matrixKpi: null,
    matrixKpiPoints: null,
    rankingItems: type === "Ranking" ? ["Item 1", "Item 2"] : [],
    kpiRepresentation: "Scale",
    kpiScaleValues: null,
    allowNA: false,
    kpi: {
      kpiKey: null,
      perspectiveLabel: null,
      boundJourneyOn: true,
      stageId: null,
      touchpointId: null,
      touchpointDisplay: null,
    },
    reason: { on: false, prompt: "", multi: false, reasons: [], hasOther: false },
    multiDisplay: "checkboxes",
    boolDisplay: "buttons",
    rankDisplay: "drag",
  }
}

/** Maps an M-06 catalogue Scale enum member to its concrete point values —
 * the KPI question preview renders on the KPI's own scale (reference parity). */
export function kpiScaleRange(scale: string | null | undefined): number[] | null {
  switch (scale) {
    case "Scale0_10":
      return Array.from({ length: 11 }, (_, i) => i)
    case "Scale1_3":
      return [1, 2, 3]
    case "Scale1_5":
      return [1, 2, 3, 4, 5]
    case "Scale1_7":
      return [1, 2, 3, 4, 5, 6, 7]
    case "Scale1_10":
      return Array.from({ length: 10 }, (_, i) => i + 1)
    case "Scale1_100":
      return Array.from({ length: 100 }, (_, i) => i + 1)
    default:
      return null
  }
}

/** Change a question's type in place — reseeds type-specific fields from a fresh
 * template but PRESERVES identity, text, description, required and comments (the
 * reference `changeType` contract). Server row is kept so the next persist PUTs. */
export function changeBuilderQuestionType(
  q: BuilderQuestion,
  next: BuilderQuestionType
): BuilderQuestion {
  const fresh = newBuilderQuestion(next)
  return {
    ...fresh,
    localId: q.localId,
    serverId: q.serverId,
    rowVersion: q.rowVersion,
    hasRoutingMap: q.hasRoutingMap,
    text: q.text,
    description: q.description,
    required: q.required,
    showComments: q.showComments,
  }
}

export function newBuilderSection(title: string): BuilderSection {
  return { localId: nextLocalId("s"), serverId: null, rowVersion: null, title, questions: [], sets: [] }
}

export function newBuilderSet(title: string): BuilderSet {
  return {
    localId: nextLocalId("set"),
    serverId: null,
    rowVersion: null,
    title,
    description: "",
    selectionMode: "random",
    count: 2,
    questions: [],
  }
}

/** Total question count — standalone + set members (BR-1.7 publish gate input).
 * Paragraph elements are static text, not questions — excluded. */
export function countQuestions(sections: BuilderSection[]): number {
  const real = (qs: BuilderQuestion[]) => qs.filter((q) => q.type !== "Paragraph").length
  return sections.reduce(
    (sum, s) => sum + real(s.questions) + s.sets.reduce((inner, set) => inner + real(set.questions), 0),
    0
  )
}
