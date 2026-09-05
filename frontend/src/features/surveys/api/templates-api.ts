// Typed wrappers for TemplatesController (contracts/templates.md, F6/F7 — US5):
// GET /api/v1/templates (search/class/sector filters), GET /{id}, POST (save survey as
// template), PATCH /{id} (Customized only — 403 on BuiltIn per FR-7.1), DELETE /{id}
// (snapshot no-link, BR-7.1), POST /{id}/instantiate (new Draft survey from snapshot).
// `class` travels as an INTEGER enum (BuiltIn=0, Customized=1) in bodies/views but as a
// NAME on the query string (same asymmetry as the surveys list).

import { callJsonWithEtag, type EtagResult } from "./etag"

export type TemplateClass = "BuiltIn" | "Customized"

export function normalizeTemplateClass(value: unknown): TemplateClass {
  if (value === 1 || value === "Customized" || value === "customized") return "Customized"
  return "BuiltIn"
}

export interface TemplateListItem {
  id: string
  class: TemplateClass
  nameEn: string
  nameAr: string | null
  description: string | null
  tags: string[]
  sectors: string[]
  previewThumbnailFileHandle: string | null
  updatedAt: string
}

export interface TemplateListResult {
  items: TemplateListItem[]
  nextPageToken: string | null
  totalCount: number
}

export interface TemplateView extends TemplateListItem {
  sectionCount: number
  questionCount: number
  hasKpiBindings: boolean
  updatedBy: string | null
  rowVersion: number
}

/* eslint-disable @typescript-eslint/no-explicit-any -- wire boundary */
function toListItem(w: any): TemplateListItem {
  return {
    id: w.id,
    class: normalizeTemplateClass(w.class),
    nameEn: w.nameEn ?? "",
    nameAr: w.nameAr ?? null,
    description: w.description ?? null,
    tags: w.tags ?? [],
    sectors: w.sectors ?? [],
    previewThumbnailFileHandle: w.previewThumbnailFileHandle ?? null,
    updatedAt: w.updatedAt,
  }
}

function toView(w: any): TemplateView {
  return {
    ...toListItem(w),
    sectionCount: w.sectionCount ?? 0,
    questionCount: w.questionCount ?? 0,
    hasKpiBindings: w.hasKpiBindings ?? false,
    updatedBy: w.updatedBy ?? null,
    rowVersion: w.rowVersion ?? 0,
  }
}
/* eslint-enable @typescript-eslint/no-explicit-any */

export interface ListTemplatesParams {
  /** FR-6.2 — matches name OR tag. */
  q?: string
  class?: TemplateClass
  sector?: string
  pageSize?: number
  pageToken?: string
}

/** GET /api/v1/templates — F6 listing (server sorts customized-first per FR-6.1). */
export async function listTemplates(params: ListTemplatesParams = {}): Promise<TemplateListResult> {
  const qs = new URLSearchParams()
  if (params.q) qs.set("q", params.q)
  if (params.class) qs.set("class", params.class)
  if (params.sector) qs.set("sector", params.sector)
  if (params.pageSize) qs.set("page_size", String(params.pageSize))
  if (params.pageToken) qs.set("page_token", params.pageToken)
  const query = qs.toString()
  const { data } = await callJsonWithEtag<{
    items: unknown[]
    nextPageToken: string | null
    totalCount: number
  }>(`/templates${query ? `?${query}` : ""}`)
  return {
    items: (data.items ?? []).map(toListItem),
    nextPageToken: data.nextPageToken ?? null,
    totalCount: data.totalCount ?? 0,
  }
}

/** GET /api/v1/templates/{id} — full view + ETag for the next PATCH. */
export async function getTemplate(id: string): Promise<EtagResult<TemplateView>> {
  const { data, etag } = await callJsonWithEtag<unknown>(`/templates/${id}`)
  return { data: toView(data), etag }
}

export interface CreateTemplateInput {
  sourceSurveyId: string
  nameEn: string
  nameAr?: string | null
  description?: string | null
  tags?: string[]
}

/** POST /api/v1/templates — snapshot a survey as a Customized template (F7). */
export async function createTemplate(
  input: CreateTemplateInput,
  idempotencyKey: string
): Promise<EtagResult<TemplateView>> {
  const { data, etag } = await callJsonWithEtag<unknown>("/templates", {
    method: "POST",
    body: {
      sourceSurveyId: input.sourceSurveyId,
      nameEn: input.nameEn,
      nameAr: input.nameAr ?? null,
      description: input.description ?? null,
      tags: input.tags ?? [],
    },
    idempotencyKey,
  })
  return { data: toView(data), etag }
}

export interface UpdateTemplateInput {
  nameEn?: string | null
  nameAr?: string | null
  description?: string | null
  tags?: string[] | null
}

/** PATCH /api/v1/templates/{id} — Customized only (403 template.builtin.readonly). */
export async function updateTemplate(
  id: string,
  input: UpdateTemplateInput,
  ifMatch?: string
): Promise<EtagResult<TemplateView>> {
  const { data, etag } = await callJsonWithEtag<unknown>(`/templates/${id}`, {
    method: "PATCH",
    body: {
      nameEn: input.nameEn ?? null,
      nameAr: input.nameAr ?? null,
      description: input.description ?? null,
      tags: input.tags ?? null,
    },
    ifMatch,
  })
  return { data: toView(data), etag }
}

/** DELETE /api/v1/templates/{id} — instantiated surveys keep their copy (BR-7.1). */
export async function deleteTemplate(id: string, ifMatch?: string): Promise<void> {
  await callJsonWithEtag<void>(`/templates/${id}`, { method: "DELETE", ifMatch })
}

/**
 * POST /api/v1/templates/{id}/instantiate — creates a new Draft survey from the
 * template snapshot (settings + appearance + questions + KPI/journey bindings).
 * Returns the new survey's id for the redirect into Settings.
 */
export async function instantiateTemplate(
  id: string,
  nameEn: string | undefined,
  idempotencyKey: string
): Promise<{ surveyId: string }> {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- wire boundary
  const { data } = await callJsonWithEtag<any>(`/templates/${id}/instantiate`, {
    method: "POST",
    body: { nameEn: nameEn ?? null },
    idempotencyKey,
  })
  return { surveyId: data.id ?? data.surveyId }
}
