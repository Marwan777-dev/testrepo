// Typed wrappers for SurveyRoutingController (contracts/questions.md, F9/US4):
// POST /surveys/{id}/routing (survey-level toggle, If-Match = survey ETag, returns the
// refreshed SurveyView with shuffleLocked), and GET/PUT
// /surveys/{id}/questions/{qid}/routing (sparse override map, If-Match = question ETag).
// The map is SPARSE: only answers deviating from next-in-order carry an entry; the
// "__end" sentinel means end-of-survey (research.md §6).

import { callJsonWithEtag, type EtagResult } from "./etag"
import {
  normalizeLayoutMode,
  normalizeSurveyStatus,
  normalizeSurveyType,
  normalizeThemeMode,
  type SurveyView,
} from "./surveys-api"

/** Reserved routing target meaning "end the survey". */
export const ROUTING_END_SENTINEL = "__end"

export interface RoutingMapView {
  /** answerKey → target questionId | "__end" — override entries only. */
  map: Record<string, string>
  /** True when at least one override exists (drives the "Routing set" badge). */
  hasRouting: boolean
}

/* eslint-disable @typescript-eslint/no-explicit-any -- wire boundary */
function toRoutingView(w: any): RoutingMapView {
  return { map: w.map ?? {}, hasRouting: w.hasRouting ?? false }
}
/* eslint-enable @typescript-eslint/no-explicit-any */

/**
 * POST /surveys/{id}/routing — survey-level routing toggle (FR-9.1). Enabling requires
 * `confirm: true` (the UI's confirmation modal) and turns shuffle off + locked
 * (shuffleLocked on the returned SurveyView). If-Match = survey ETag.
 */
export async function toggleSurveyRouting(
  surveyId: string,
  enabled: boolean,
  confirm: boolean,
  ifMatch: string | undefined
): Promise<EtagResult<SurveyView>> {
  const { data, etag } = await callJsonWithEtag<unknown>(`/surveys/${surveyId}/routing`, {
    method: "POST",
    body: { enabled, confirm },
    ifMatch,
  })
  /* eslint-disable @typescript-eslint/no-explicit-any -- wire boundary */
  const w = data as any
  const view: SurveyView = {
    id: w.id,
    nameEn: w.nameEn,
    description: w.description ?? null,
    surveyType: normalizeSurveyType(w.surveyType),
    boundJourneyId: w.boundJourneyId ?? null,
    status: normalizeSurveyStatus(w.status),
    themeMode: normalizeThemeMode(w.themeMode),
    welcomeHtml: w.welcomeHtml ?? null,
    thanksHtml: w.thanksHtml ?? null,
    redirectUrl: w.redirectUrl ?? null,
    redirectAfterS: w.redirectAfterS ?? 0,
    layout: normalizeLayoutMode(w.layout),
    questionsPerPage: w.questionsPerPage ?? null,
    activePeriod: w.activePeriod ?? null,
    shuffle: w.shuffle ?? false,
    shuffleMode: w.shuffleMode ?? "random",
    routingOn: w.routingOn ?? false,
    shuffleLocked: w.shuffleLocked ?? false,
    updatedAt: w.updatedAt,
    updatedBy: w.updatedBy,
    rowVersion: w.rowVersion ?? 0,
  }
  /* eslint-enable @typescript-eslint/no-explicit-any */
  return { data: view, etag }
}

/** GET /surveys/{id}/questions/{qid}/routing — the question's override map. */
export async function getQuestionRouting(
  surveyId: string,
  questionId: string
): Promise<RoutingMapView> {
  const { data } = await callJsonWithEtag<unknown>(
    `/surveys/${surveyId}/questions/${questionId}/routing`
  )
  return toRoutingView(data)
}

/**
 * PUT /surveys/{id}/questions/{qid}/routing — replaces the question's override map.
 * An empty map clears every override. If-Match = question ETag.
 */
export async function saveQuestionRouting(
  surveyId: string,
  questionId: string,
  map: Record<string, string>,
  ifMatch: string | undefined
): Promise<EtagResult<RoutingMapView>> {
  const { data, etag } = await callJsonWithEtag<unknown>(
    `/surveys/${surveyId}/questions/${questionId}/routing`,
    { method: "PUT", body: { map }, ifMatch }
  )
  return { data: toRoutingView(data), etag }
}
