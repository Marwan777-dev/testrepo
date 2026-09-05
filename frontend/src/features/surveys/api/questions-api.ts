// Typed wrappers for QuestionsController (contracts/questions.md):
// POST / PUT / DELETE /api/v1/surveys/{id}/sections/{sectionId}/questions (+ /move).
// Wire specifics:
// - QuestionType / QuestionSubType travel as INTEGER enums (no JsonStringEnumConverter).
// - `payload` is polymorphic — System.Text.Json requires the `$type` discriminator to be
//   the FIRST property, so the wire objects below always spell `$type` first.
// - KpiBinding travels camelCase: {kpiCode, perspective, boundJourneyOn, stageId, touchpointId}.

import { callJsonWithEtag, type EtagResult } from "./etag"
import type {
  BuilderQuestion,
  BuilderQuestionType,
  BuilderSubType,
} from "../components/builder-types"

// ── Enum maps (order mirrors the C# enums exactly) ───────────────────────────────

const QUESTION_TYPES: BuilderQuestionType[] = [
  "Scale",
  "InputField",
  "SingleSelect",
  "MultiSelect",
  "YesNo",
  "Matrix",
  "Ranking",
  "Kpi",
]

const QUESTION_SUB_TYPES: BuilderSubType[] = [
  "None",
  "Labels",
  "Stars",
  "Smileys",
  "Slider",
  "Text",
  "Paragraph",
  "Number",
  "Date",
  "Time",
  "DateTime",
  "Month",
  "List",
  "Dropdown",
  "CustomColumns",
  "KpiScale",
]

export function normalizeQuestionType(value: unknown): BuilderQuestionType {
  if (typeof value === "number") return QUESTION_TYPES[value] ?? "Scale"
  if (typeof value === "string") {
    const hit = QUESTION_TYPES.find((m) => m.toLowerCase() === value.toLowerCase())
    if (hit) return hit
  }
  return "Scale"
}

export function normalizeQuestionSubType(value: unknown): BuilderSubType {
  if (typeof value === "number") return QUESTION_SUB_TYPES[value] ?? "None"
  if (typeof value === "string") {
    const hit = QUESTION_SUB_TYPES.find((m) => m.toLowerCase() === value.toLowerCase())
    if (hit) return hit
  }
  return "None"
}

export const questionTypeToInt = (v: BuilderQuestionType): number => QUESTION_TYPES.indexOf(v)
export const questionSubTypeToInt = (v: BuilderSubType): number => QUESTION_SUB_TYPES.indexOf(v)

// ── Polymorphic type payload ($type discriminator MUST be first) ─────────────────

export type QuestionPayloadWire =
  | { $type: "scale"; pointCount?: number | null; labels?: string[] | null; sliderLower?: number | null; sliderHigher?: number | null; sliderSteps?: number | null }
  | { $type: "input_field"; maxLength?: number | null; placeholder?: string | null }
  | { $type: "single_select"; options: string[] }
  | { $type: "multi_select"; options: string[]; minSelections?: number | null; maxSelections?: number | null }
  | { $type: "yes_no"; yesLabel?: string; noLabel?: string }
  | { $type: "matrix"; rows: string[]; columns: string[] }
  | { $type: "ranking"; items: string[] }
  | { $type: "kpi"; representation?: string | null; allowNa?: boolean }

export interface KpiBindingWire {
  kpiCode: string
  perspective: string | null
  boundJourneyOn: boolean
  stageId: string | null
  touchpointId: string | null
}

export interface QuestionInput {
  setId?: string | null
  text: string
  description?: string | null
  type: BuilderQuestionType
  subType: BuilderSubType
  sliderSteps?: number | null
  required: boolean
  showComments: boolean
  sentiment: boolean
  order?: number
  binding?: KpiBindingWire | null
  payload: QuestionPayloadWire
}

export interface QuestionView {
  id: string
  surveyId: string
  sectionId: string
  setId: string | null
  type: BuilderQuestionType
  subType: BuilderSubType
  text: string
  description: string | null
  required: boolean
  comments: boolean
  commentLabel: string
  commentMaxLength: number
  sentiment: boolean
  kpiCode: string | null
  perspective: string | null
  boundJourneyOn: boolean
  stageId: string | null
  touchpointId: string | null
  payload: QuestionPayloadWire | null
  order: number
  rowVersion: number
}

/* eslint-disable @typescript-eslint/no-explicit-any -- wire boundary */
function toQuestionView(w: any): QuestionView {
  return {
    id: w.id,
    surveyId: w.surveyId,
    sectionId: w.sectionId,
    setId: w.setId ?? null,
    type: normalizeQuestionType(w.type),
    subType: normalizeQuestionSubType(w.subtype ?? w.subType),
    text: w.text ?? "",
    description: w.description ?? null,
    required: w.required ?? false,
    comments: w.comments ?? false,
    commentLabel: w.commentLabel ?? "Comments",
    commentMaxLength: w.commentMaxLength ?? 200,
    sentiment: w.sentiment ?? false,
    kpiCode: w.kpiCode ?? null,
    perspective: w.perspective ?? null,
    boundJourneyOn: w.boundJourneyOn ?? false,
    stageId: w.stageId ?? null,
    touchpointId: w.touchpointId ?? null,
    payload: (w.typePayload ?? null) as QuestionPayloadWire | null,
    order: w.order ?? 0,
    rowVersion: w.rowVersion ?? 0,
  }
}
/* eslint-enable @typescript-eslint/no-explicit-any */

function toWire(input: QuestionInput, includePlacement: boolean): Record<string, unknown> {
  const body: Record<string, unknown> = {
    text: input.text,
    description: input.description ?? null,
    type: questionTypeToInt(input.type),
    subType: questionSubTypeToInt(input.subType),
    sliderSteps: input.sliderSteps ?? null,
    required: input.required,
    showComments: input.showComments,
    sentiment: input.sentiment,
    binding: input.binding ?? null,
    payload: input.payload,
  }
  if (includePlacement) {
    body.setId = input.setId ?? null
    body.order = input.order ?? 0
  }
  return body
}

/** Maps a canvas BuilderQuestion onto the wire QuestionInput ($type-first payload). */
export function builderQuestionToInput(
  q: BuilderQuestion,
  setId: string | null,
  order: number
): QuestionInput {
  let payload: QuestionPayloadWire
  switch (q.type) {
    case "Paragraph":
      // Local-only static text element — no backend QuestionType exists for it.
      // Callers filter paragraphs out before persisting; reaching here is a bug.
      throw new Error("Paragraph elements cannot be persisted")
    case "Scale":
      payload =
        q.subType === "Slider"
          ? {
              $type: "scale",
              sliderLower: q.sliderMin,
              sliderHigher: q.sliderMax,
              sliderSteps: q.sliderSteps,
            }
          : {
              $type: "scale",
              pointCount: q.scalePoints,
              // Per-point labels when any are set (Labels view); else min/max pair.
              labels: q.pointLabels.some((l) => l.trim() !== "")
                ? q.pointLabels.slice(0, q.scalePoints)
                : q.scaleMinLabel || q.scaleMaxLabel
                  ? [q.scaleMinLabel, q.scaleMaxLabel]
                  : null,
            }
      break
    case "InputField":
      payload = { $type: "input_field" }
      break
    case "SingleSelect":
      payload = { $type: "single_select", options: q.options }
      break
    case "MultiSelect":
      payload = { $type: "multi_select", options: q.options }
      break
    case "YesNo":
      payload = { $type: "yes_no", yesLabel: q.yesLabel || "Yes", noLabel: q.noLabel || "No" }
      break
    case "Matrix":
      payload = { $type: "matrix", rows: q.matrixRows, columns: q.matrixColumns }
      break
    case "Ranking":
      payload = { $type: "ranking", items: q.rankingItems }
      break
    case "Kpi":
      payload = { $type: "kpi", representation: q.kpiRepresentation, allowNa: q.allowNA }
      break
  }
  return {
    setId,
    text: q.text,
    description: q.description || null,
    type: q.type,
    subType: q.subType,
    sliderSteps: q.subType === "Slider" ? q.sliderSteps : null,
    required: q.required,
    showComments: q.showComments,
    sentiment: q.applySentiment,
    order,
    binding:
      q.type === "Kpi" && q.kpi.kpiKey
        ? {
            kpiCode: q.kpi.kpiKey,
            perspective: q.kpi.perspectiveLabel,
            boundJourneyOn: q.kpi.boundJourneyOn,
            stageId: q.kpi.stageId,
            touchpointId: q.kpi.touchpointId,
          }
        : null,
    payload,
  }
}

/** POST /surveys/{id}/sections/{sectionId}/questions — create (FR-8.x). */
export async function createQuestion(
  surveyId: string,
  sectionId: string,
  input: QuestionInput,
  ifMatch?: string
): Promise<EtagResult<QuestionView>> {
  const { data, etag } = await callJsonWithEtag<unknown>(
    `/surveys/${surveyId}/sections/${sectionId}/questions`,
    { method: "POST", body: toWire(input, true), ifMatch }
  )
  return { data: toQuestionView(data), etag }
}

/** PUT /surveys/{id}/sections/{sectionId}/questions/{qid}. If-Match = question ETag. */
export async function updateQuestion(
  surveyId: string,
  sectionId: string,
  questionId: string,
  input: QuestionInput,
  ifMatch?: string
): Promise<EtagResult<QuestionView>> {
  const { data, etag } = await callJsonWithEtag<unknown>(
    `/surveys/${surveyId}/sections/${sectionId}/questions/${questionId}`,
    { method: "PUT", body: toWire(input, false), ifMatch }
  )
  return { data: toQuestionView(data), etag }
}

/** DELETE /surveys/{id}/sections/{sectionId}/questions/{qid} (FR-2.7 routing cleanup). */
export async function deleteQuestion(
  surveyId: string,
  sectionId: string,
  questionId: string,
  ifMatch?: string
): Promise<void> {
  await callJsonWithEtag<void>(
    `/surveys/${surveyId}/sections/${sectionId}/questions/${questionId}`,
    { method: "DELETE", ifMatch }
  )
}

/**
 * POST /surveys/{id}/sections/{sectionId}/questions/{qid}/move — cross-section /
 * cross-set placement; both containers re-compact server-side. Moving into a set strips
 * the question's routing (FR-9.5).
 */
export async function moveQuestion(
  surveyId: string,
  sectionId: string,
  questionId: string,
  target: { targetSectionId: string; targetSetId?: string | null; targetOrder: number },
  ifMatch?: string
): Promise<EtagResult<QuestionView>> {
  const { data, etag } = await callJsonWithEtag<unknown>(
    `/surveys/${surveyId}/sections/${sectionId}/questions/${questionId}/move`,
    {
      method: "POST",
      body: {
        targetSectionId: target.targetSectionId,
        targetSetId: target.targetSetId ?? null,
        targetOrder: target.targetOrder,
      },
      ifMatch,
    }
  )
  return { data: toQuestionView(data), etag }
}
