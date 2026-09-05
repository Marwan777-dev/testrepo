// Typed wrappers for SurveyTranslationsController (contracts/translations.md, US6):
// GET /api/v1/surveys/{id}/translations (per-locale coverage), GET /{locale} (resolved
// bundle + missing keys), PUT /{locale} (replace the bundle — explicit Save per Q1,
// If-Match = survey ETag). Bundle keys are `<entity>.<id>.<field>` strings.

import { callJsonWithEtag, type EtagResult } from "./etag"

export interface LocaleSummary {
  locale: string
  coveragePercent: number
  keysCount: number
  keysTranslated: number
  updatedAt: string | null
}

export interface TranslationBundle {
  locale: string
  /** key → translated text (resolved with EN fallback for missing keys). */
  keys: Record<string, string>
  /** Keys still falling back to English — drives the coverage indicator. */
  missingKeys: string[]
}

/* eslint-disable @typescript-eslint/no-explicit-any -- wire boundary */
function toBundle(w: any): TranslationBundle {
  return { locale: w.locale, keys: w.keys ?? {}, missingKeys: w.missingKeys ?? [] }
}
/* eslint-enable @typescript-eslint/no-explicit-any */

/** GET /surveys/{id}/translations — locales + coverage percentages. */
export async function listTranslationLocales(surveyId: string): Promise<LocaleSummary[]> {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- wire boundary
  const { data } = await callJsonWithEtag<any>(`/surveys/${surveyId}/translations`)
  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- wire boundary
  return (data.locales ?? []).map((l: any) => ({
    locale: l.locale,
    coveragePercent: l.coveragePercent ?? 0,
    keysCount: l.keysCount ?? 0,
    keysTranslated: l.keysTranslated ?? 0,
    updatedAt: l.updatedAt ?? null,
  }))
}

/** GET /surveys/{id}/translations/{locale} — the resolved bundle. */
export async function getTranslationBundle(
  surveyId: string,
  locale: string
): Promise<EtagResult<TranslationBundle>> {
  const { data, etag } = await callJsonWithEtag<unknown>(
    `/surveys/${surveyId}/translations/${encodeURIComponent(locale)}`
  )
  return { data: toBundle(data), etag }
}

/** PUT /surveys/{id}/translations/{locale} — replaces the bundle (explicit Save). */
export async function putTranslationBundle(
  surveyId: string,
  locale: string,
  keys: Record<string, string>,
  ifMatch?: string
): Promise<EtagResult<TranslationBundle>> {
  const { data, etag } = await callJsonWithEtag<unknown>(
    `/surveys/${surveyId}/translations/${encodeURIComponent(locale)}`,
    { method: "PUT", body: { keys }, ifMatch }
  )
  return { data: toBundle(data), etag }
}
