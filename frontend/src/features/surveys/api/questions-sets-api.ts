// Typed wrappers for QuestionsSetsController (contracts/sections-and-sets.md):
// POST / PATCH / DELETE /api/v1/surveys/{id}/sections/{sectionId}/sets. Selection mode
// travels as an INTEGER enum (Random=0, LowResponse=1 — no JsonStringEnumConverter);
// deleting a non-empty set without `confirm=true` returns 409 with
// `details.questionsCount` for the FR-2.6 confirmation dialog.

import { callJsonWithEtag, type EtagResult } from "./etag"

export type QuestionsSetSelectionMode = "random" | "low_response"

const SELECTION_MODES: QuestionsSetSelectionMode[] = ["random", "low_response"]

export function normalizeSelectionMode(value: unknown): QuestionsSetSelectionMode {
  if (typeof value === "number") return SELECTION_MODES[value] ?? "random"
  if (typeof value === "string") {
    const lower = value.toLowerCase()
    if (lower === "low_response" || lower === "lowresponse") return "low_response"
  }
  return "random"
}

export const selectionModeToInt = (v: QuestionsSetSelectionMode): number =>
  SELECTION_MODES.indexOf(v)

export interface QuestionsSetView {
  id: string
  sectionId: string
  title: string
  description: string | null
  selectionMode: QuestionsSetSelectionMode
  /** How many member questions each respondent receives ("shows k of n"). */
  count: number
  order: number
  rowVersion: number
}

export interface QuestionsSetInput {
  title: string
  description?: string | null
  selectionMode: QuestionsSetSelectionMode
  count: number
  order?: number
}

/* eslint-disable @typescript-eslint/no-explicit-any -- wire boundary */
function toSetView(w: any): QuestionsSetView {
  return {
    id: w.id,
    sectionId: w.sectionId,
    title: w.title ?? "",
    description: w.description ?? null,
    selectionMode: normalizeSelectionMode(w.selectionMode),
    count: w.count ?? 0,
    order: w.order ?? 0,
    rowVersion: w.rowVersion ?? 0,
  }
}
/* eslint-enable @typescript-eslint/no-explicit-any */

function toWire(input: QuestionsSetInput): Record<string, unknown> {
  return {
    title: input.title,
    description: input.description ?? null,
    selectionMode: selectionModeToInt(input.selectionMode),
    count: input.count,
    order: input.order ?? 0,
  }
}

/** POST /surveys/{id}/sections/{sectionId}/sets — create a Questions Set (F10). */
export async function createQuestionsSet(
  surveyId: string,
  sectionId: string,
  input: QuestionsSetInput,
  ifMatch?: string
): Promise<EtagResult<QuestionsSetView>> {
  const { data, etag } = await callJsonWithEtag<unknown>(
    `/surveys/${surveyId}/sections/${sectionId}/sets`,
    { method: "POST", body: toWire(input), ifMatch }
  )
  return { data: toSetView(data), etag }
}

/** PATCH /surveys/{id}/sections/{sectionId}/sets/{setId}. If-Match = set ETag. */
export async function updateQuestionsSet(
  surveyId: string,
  sectionId: string,
  setId: string,
  input: QuestionsSetInput,
  ifMatch?: string
): Promise<EtagResult<QuestionsSetView>> {
  const { data, etag } = await callJsonWithEtag<unknown>(
    `/surveys/${surveyId}/sections/${sectionId}/sets/${setId}`,
    { method: "PATCH", body: toWire(input), ifMatch }
  )
  return { data: toSetView(data), etag }
}

/**
 * DELETE /surveys/{id}/sections/{sectionId}/sets/{setId} — non-empty set needs
 * `confirm=true` after the 409 `questionsset.delete.requires_confirmation` (FR-2.6).
 */
export async function deleteQuestionsSet(
  surveyId: string,
  sectionId: string,
  setId: string,
  confirm: boolean,
  ifMatch?: string
): Promise<void> {
  await callJsonWithEtag<void>(
    `/surveys/${surveyId}/sections/${sectionId}/sets/${setId}?confirm=${confirm ? "true" : "false"}`,
    { method: "DELETE", ifMatch }
  )
}
