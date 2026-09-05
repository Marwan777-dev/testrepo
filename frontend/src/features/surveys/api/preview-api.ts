// Typed wrapper for SurveyPreviewController (US7): GET /api/v1/surveys/{id}/preview
// ?channel=desktop|mobile|whatsapp|email&locale=en|ar. Returns the full client-side
// render payload (survey + resolved theme + sections + questions + translation bundle)
// — the frontend re-renders the same payload inside per-channel chrome (F12).
// Nested views are camelCase (no JsonPropertyName on PreviewView) and reuse the
// surveys/sections/questions normalizers.

import { callJsonWithEtag } from "./etag"
import { normalizeQuestionSubType, normalizeQuestionType, type QuestionView } from "./questions-api"
import {
  normalizeLayoutMode,
  normalizeSurveyStatus,
  normalizeSurveyType,
  normalizeThemeMode,
  type SurveyView,
  type ThemeView,
} from "./surveys-api"
import type { SectionView } from "./sections-api"

export type PreviewChannel = "desktop" | "mobile" | "whatsapp" | "email"

export interface PreviewPayload {
  channel: PreviewChannel
  locale: string
  survey: SurveyView
  theme: ThemeView
  sections: SectionView[]
  questions: QuestionView[]
  translations: Record<string, string>
  missingKeys: string[]
}

/* eslint-disable @typescript-eslint/no-explicit-any -- wire boundary */
export async function getSurveyPreview(
  surveyId: string,
  channel: PreviewChannel,
  locale: string
): Promise<PreviewPayload> {
  const { data } = await callJsonWithEtag<any>(
    `/surveys/${surveyId}/preview?channel=${channel}&locale=${encodeURIComponent(locale)}`
  )
  const s = data.survey ?? {}
  return {
    channel: (data.channel ?? channel) as PreviewChannel,
    locale: data.locale ?? locale,
    survey: {
      id: s.id,
      nameEn: s.nameEn,
      description: s.description ?? null,
      surveyType: normalizeSurveyType(s.surveyType),
      boundJourneyId: s.boundJourneyId ?? null,
      status: normalizeSurveyStatus(s.status),
      themeMode: normalizeThemeMode(s.themeMode),
      welcomeHtml: s.welcomeHtml ?? null,
      thanksHtml: s.thanksHtml ?? null,
      redirectUrl: s.redirectUrl ?? null,
      redirectAfterS: s.redirectAfterS ?? 0,
      layout: normalizeLayoutMode(s.layout),
      questionsPerPage: s.questionsPerPage ?? null,
      activePeriod: s.activePeriod ?? null,
      shuffle: s.shuffle ?? false,
      shuffleMode: s.shuffleMode ?? "random",
      routingOn: s.routingOn ?? false,
      shuffleLocked: s.shuffleLocked ?? false,
      updatedAt: s.updatedAt,
      updatedBy: s.updatedBy,
      rowVersion: s.rowVersion ?? 0,
    },
    theme: {
      primaryColour: data.theme?.primaryColour ?? "#0D8BBC",
      textColour: data.theme?.textColour ?? null,
      buttonRadiusPx: data.theme?.buttonRadiusPx ?? null,
    },
    sections: (data.sections ?? []).map((w: any) => ({
      id: w.id,
      surveyId: w.surveyId,
      name: w.name ?? "",
      description: w.description ?? null,
      order: w.order ?? 0,
      rowVersion: w.rowVersion ?? 0,
    })),
    questions: (data.questions ?? []).map((w: any) => ({
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
      payload: w.typePayload ?? null,
      order: w.order ?? 0,
      rowVersion: w.rowVersion ?? 0,
    })),
    translations: data.translations ?? {},
    missingKeys: data.missingKeys ?? [],
  }
}
/* eslint-enable @typescript-eslint/no-explicit-any */
