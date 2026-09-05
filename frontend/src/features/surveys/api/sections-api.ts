// Typed wrappers for SectionsController (contracts/sections-and-sets.md):
// POST / PATCH / DELETE /api/v1/surveys/{id}/sections. There is NO list/GET endpoint —
// the builder rebuilds its view from write responses (see TODO-M01-028). Deleting a
// non-empty section without `confirm=true` returns 409 with the cascade breakdown in
// `SurveysApiError.details` ({standaloneQuestions, questionsSets, setQuestions}) so the
// FR-2.5 destructive dialog can list exactly what will be removed.

import { callJsonWithEtag, type EtagResult } from "./etag"

export interface SectionView {
  id: string
  surveyId: string
  name: string
  description: string | null
  order: number
  rowVersion: number
}

export interface SectionInput {
  name: string
  description?: string | null
  order?: number | null
}

/* eslint-disable @typescript-eslint/no-explicit-any -- wire boundary */
function toSectionView(w: any): SectionView {
  return {
    id: w.id,
    surveyId: w.surveyId,
    name: w.name ?? "",
    description: w.description ?? null,
    order: w.order ?? 0,
    rowVersion: w.rowVersion ?? 0,
  }
}
/* eslint-enable @typescript-eslint/no-explicit-any */

/** POST /surveys/{id}/sections — create a section (FR-2.1). */
export async function createSection(
  surveyId: string,
  input: SectionInput,
  ifMatch?: string
): Promise<EtagResult<SectionView>> {
  const { data, etag } = await callJsonWithEtag<unknown>(`/surveys/${surveyId}/sections`, {
    method: "POST",
    body: { name: input.name, description: input.description ?? null, order: input.order ?? null },
    ifMatch,
  })
  return { data: toSectionView(data), etag }
}

/** PATCH /surveys/{id}/sections/{sectionId} — rename / reorder. If-Match = section ETag. */
export async function updateSection(
  surveyId: string,
  sectionId: string,
  input: SectionInput,
  ifMatch?: string
): Promise<EtagResult<SectionView>> {
  const { data, etag } = await callJsonWithEtag<unknown>(
    `/surveys/${surveyId}/sections/${sectionId}`,
    {
      method: "PATCH",
      body: { name: input.name, description: input.description ?? null, order: input.order ?? null },
      ifMatch,
    }
  )
  return { data: toSectionView(data), etag }
}

/**
 * DELETE /surveys/{id}/sections/{sectionId} — `confirm=false` first; a 409
 * `section.delete.requires_confirmation` carries the cascade counts for the dialog,
 * then the caller re-sends with `confirm=true` (FR-2.5 cascade).
 */
export async function deleteSection(
  surveyId: string,
  sectionId: string,
  confirm: boolean,
  ifMatch?: string
): Promise<void> {
  await callJsonWithEtag<void>(
    `/surveys/${surveyId}/sections/${sectionId}?confirm=${confirm ? "true" : "false"}`,
    { method: "DELETE", ifMatch }
  )
}
