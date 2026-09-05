// Typed wrappers for every SurveysController route (contracts/surveys.md) plus the
// SurveyThemesController F4 routes consumed by the Appearance page. Built on the ETag
// transport in ./etag.ts (Bearer + If-Match/Idempotency-Key + ETagConflictError).
//
// Wire-format notes (CLAUDE.md § Backend Integration, verified against the host):
// - The host registers `AddControllers()` with NO JsonStringEnumConverter → every .NET
//   enum travels as an INTEGER in JSON bodies (both directions). The TS domain types
//   stay string unions; `normalize*`/`*ToInt` convert at this boundary only.
// - Query-string filters are the asymmetric exception: `SurveyListQuery` parses
//   `type`/`status` as comma-separated enum NAMES ("Active,Paused"), not ints.
// - Property names are camelCase (System.Text.Json default), not the snake_case shown
//   in the contract doc; query param names ARE snake_case (`[FromQuery(Name = …)]`).

import { getSessionToken } from "@/features/auth/session-token"

import { callJsonWithEtag, SurveysApiError, type ApiErrorEnvelope, type EtagResult } from "./etag"

// ── Domain types (string unions — components never see wire ints) ────────────────

export type SurveyStatus = "Draft" | "PendingReview" | "Active" | "Paused" | "Archived"
export type SurveyType = "Transactional" | "SeasonalRelational"
export type ThemeMode = "Inherited" | "Customized"
export type LayoutMode = "single" | "section" | "question" | "count"
export type BackgroundType = "Solid" | "Gradient" | "Image" | "Pattern"

export interface ActivePeriod {
  days: number
  hours: number
}

export interface SurveyView {
  id: string
  nameEn: string
  description: string | null
  surveyType: SurveyType
  boundJourneyId: string | null
  status: SurveyStatus
  themeMode: ThemeMode
  welcomeHtml: string | null
  thanksHtml: string | null
  redirectUrl: string | null
  redirectAfterS: number
  layout: LayoutMode
  questionsPerPage: number | null
  activePeriod: ActivePeriod | null
  shuffle: boolean
  shuffleMode: string
  routingOn: boolean
  shuffleLocked: boolean
  updatedAt: string
  updatedBy: string
  rowVersion: number
}

export interface SurveyListItem {
  id: string
  nameEn: string
  surveyType: SurveyType
  boundJourneyId: string | null
  status: SurveyStatus
  rulesCount: number
  themeMode: ThemeMode
  updatedAt: string
  updatedBy: string
}

export interface SurveyListResult {
  items: SurveyListItem[]
  nextPageToken: string | null
  totalCount: number
}

export interface SurveyDraftInput {
  nameEn: string
  description?: string | null
  boundJourneyId?: string | null
  welcomeHtml?: string | null
  thanksHtml?: string | null
  redirectUrl?: string | null
  redirectAfterS?: number
  layout?: LayoutMode
  questionsPerPage?: number | null
  activePeriod?: ActivePeriod | null
  shuffle?: boolean
  shuffleMode?: string
  routingOn?: boolean
  themeMode?: ThemeMode
}

export interface ThemeView {
  primaryColour: string
  textColour: string | null
  buttonRadiusPx: number | null
}

export interface UpdateThemeInput {
  mode: ThemeMode
  backgroundType: BackgroundType
  backgroundImageHandle?: string | null
  primaryColour?: string | null
}

export interface RenderPlanItem {
  kind: "question" | "set"
  questionId?: string
  setId?: string
  questions?: string[]
}

export interface RenderPlanSection {
  sectionId: string
  items: RenderPlanItem[]
}

export interface RenderPlanResult {
  surveyId: string
  layout: LayoutMode
  sectionsOrder: RenderPlanSection[]
  routingMap: Record<string, Record<string, string>>
}

// ── Enum normalisation (wire int|string → union) + request-side int converters ───

const SURVEY_STATUSES: SurveyStatus[] = ["Draft", "PendingReview", "Active", "Paused", "Archived"]
const SURVEY_TYPES: SurveyType[] = ["Transactional", "SeasonalRelational"]
const THEME_MODES: ThemeMode[] = ["Inherited", "Customized"]
const LAYOUT_MODES: LayoutMode[] = ["single", "section", "question", "count"]
const BACKGROUND_TYPES: BackgroundType[] = ["Solid", "Gradient", "Image", "Pattern"]

function normalizeEnum<T extends string>(value: unknown, members: T[], fallback: T): T {
  if (typeof value === "number") return members[value] ?? fallback
  if (typeof value === "string") {
    const hit = members.find((m) => m.toLowerCase() === value.toLowerCase())
    if (hit) return hit
  }
  return fallback
}

export function normalizeSurveyStatus(value: unknown): SurveyStatus {
  return normalizeEnum(value, SURVEY_STATUSES, "Draft")
}

export function normalizeSurveyType(value: unknown): SurveyType {
  return normalizeEnum(value, SURVEY_TYPES, "SeasonalRelational")
}

export function normalizeThemeMode(value: unknown): ThemeMode {
  return normalizeEnum(value, THEME_MODES, "Inherited")
}

export function normalizeLayoutMode(value: unknown): LayoutMode {
  return normalizeEnum(value, LAYOUT_MODES, "section")
}

export function normalizeBackgroundType(value: unknown): BackgroundType {
  return normalizeEnum(value, BACKGROUND_TYPES, "Solid")
}

export const surveyStatusToInt = (v: SurveyStatus): number => SURVEY_STATUSES.indexOf(v)
export const layoutModeToInt = (v: LayoutMode): number => LAYOUT_MODES.indexOf(v)
export const themeModeToInt = (v: ThemeMode): number => THEME_MODES.indexOf(v)
export const backgroundTypeToInt = (v: BackgroundType): number => BACKGROUND_TYPES.indexOf(v)

// ── Wire → domain mappers ────────────────────────────────────────────────────────

/* eslint-disable @typescript-eslint/no-explicit-any -- wire boundary */
function toSurveyView(w: any): SurveyView {
  return {
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
}

function toSurveyListItem(w: any): SurveyListItem {
  return {
    id: w.id,
    nameEn: w.nameEn,
    surveyType: normalizeSurveyType(w.surveyType),
    boundJourneyId: w.boundJourneyId ?? null,
    status: normalizeSurveyStatus(w.status),
    rulesCount: w.rulesCount ?? 0,
    themeMode: normalizeThemeMode(w.themeMode),
    updatedAt: w.updatedAt,
    updatedBy: w.updatedBy,
  }
}

function draftToWire(input: SurveyDraftInput): Record<string, unknown> {
  return {
    nameEn: input.nameEn,
    description: input.description ?? null,
    boundJourneyId: input.boundJourneyId ?? null,
    welcomeHtml: input.welcomeHtml ?? null,
    thanksHtml: input.thanksHtml ?? null,
    redirectUrl: input.redirectUrl ?? null,
    redirectAfterS: input.redirectAfterS ?? 0,
    layout: layoutModeToInt(input.layout ?? "section"),
    questionsPerPage: input.questionsPerPage ?? null,
    activePeriod: input.activePeriod ?? null,
    shuffle: input.shuffle ?? false,
    shuffleMode: input.shuffleMode ?? "random",
    routingOn: input.routingOn ?? false,
    themeMode: themeModeToInt(input.themeMode ?? "Inherited"),
  }
}
/* eslint-enable @typescript-eslint/no-explicit-any */

// ── Routes ───────────────────────────────────────────────────────────────────────

export interface ListSurveysParams {
  q?: string
  /** Enum NAMES on the query string (comma-joined server-side parse). */
  type?: SurveyType[]
  status?: SurveyStatus[]
  journeyId?: string
  sort?: "name_en" | "updated_at" | "status"
  order?: "asc" | "desc"
  pageSize?: number
  pageToken?: string
}

/** GET /api/v1/surveys — F1 library listing. No ETag (collection endpoint). */
export async function listSurveys(params: ListSurveysParams = {}): Promise<SurveyListResult> {
  const qs = new URLSearchParams()
  if (params.q) qs.set("q", params.q)
  if (params.type?.length) qs.set("type", params.type.join(","))
  if (params.status?.length) qs.set("status", params.status.join(","))
  if (params.journeyId) qs.set("journey_id", params.journeyId)
  if (params.sort) qs.set("sort", params.sort)
  if (params.order) qs.set("order", params.order)
  if (params.pageSize) qs.set("page_size", String(params.pageSize))
  if (params.pageToken) qs.set("page_token", params.pageToken)
  const query = qs.toString()
  const { data } = await callJsonWithEtag<{
    items: unknown[]
    nextPageToken: string | null
    totalCount: number
  }>(`/surveys${query ? `?${query}` : ""}`)
  return {
    items: (data.items ?? []).map(toSurveyListItem),
    nextPageToken: data.nextPageToken ?? null,
    totalCount: data.totalCount ?? 0,
  }
}

/** GET /api/v1/surveys/{id} — settings payload + the ETag for the next write. */
export async function getSurvey(id: string): Promise<EtagResult<SurveyView>> {
  const { data, etag } = await callJsonWithEtag<unknown>(`/surveys/${id}`)
  return { data: toSurveyView(data), etag }
}

/** POST /api/v1/surveys — create Draft (F5 Continue). Idempotency-Key required. */
export async function createSurvey(
  input: SurveyDraftInput,
  idempotencyKey: string
): Promise<EtagResult<SurveyView>> {
  const { data, etag } = await callJsonWithEtag<unknown>("/surveys", {
    method: "POST",
    body: draftToWire(input),
    idempotencyKey,
  })
  return { data: toSurveyView(data), etag }
}

/** PUT /api/v1/surveys/{id} — settings save. If-Match required (Q1). */
export async function updateSurvey(
  id: string,
  input: SurveyDraftInput,
  ifMatch: string | undefined
): Promise<EtagResult<SurveyView>> {
  const { data, etag } = await callJsonWithEtag<unknown>(`/surveys/${id}`, {
    method: "PUT",
    body: draftToWire(input),
    ifMatch,
  })
  return { data: toSurveyView(data), etag }
}

/** POST /api/v1/surveys/{id}/clone — FR-1.8 "Copy of — <name>" Draft. */
export async function cloneSurvey(
  id: string,
  idempotencyKey: string
): Promise<EtagResult<SurveyView>> {
  const { data, etag } = await callJsonWithEtag<unknown>(`/surveys/${id}/clone`, {
    method: "POST",
    body: {},
    idempotencyKey,
  })
  return { data: toSurveyView(data), etag }
}

export interface StatusChangeInput {
  to: SurveyStatus
  reason?: string
  /** Set true after the user confirms a destructive / rules-pause dialog. */
  confirm?: boolean
}

/**
 * POST /api/v1/surveys/{id}/status — self-serve transitions (Pause / Reactivate /
 * Archive / Unarchive / destructive Return-to-Draft). 409 payloads carry the dialog
 * details (`rulesCount`, `responsesCount`, publish-gate flags) in SurveysApiError.details.
 */
export async function changeSurveyStatus(
  id: string,
  input: StatusChangeInput,
  ifMatch: string | undefined,
  idempotencyKey?: string
): Promise<EtagResult<SurveyView>> {
  const { data, etag } = await callJsonWithEtag<unknown>(`/surveys/${id}/status`, {
    method: "POST",
    body: {
      to: surveyStatusToInt(input.to),
      reason: input.reason ?? null,
      confirm: input.confirm ?? false,
    },
    ifMatch,
    idempotencyKey,
  })
  return { data: toSurveyView(data), etag }
}

/** GET /api/v1/surveys/{id}/render-plan — FR-10.4 diagnostics view. */
export async function getRenderPlan(id: string, respondentId: string): Promise<RenderPlanResult> {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- wire boundary
  const { data } = await callJsonWithEtag<any>(
    `/surveys/${id}/render-plan?respondent_id=${encodeURIComponent(respondentId)}`
  )
  return {
    surveyId: data.surveyId,
    layout: normalizeLayoutMode(data.layout),
    sectionsOrder: data.sectionsOrder ?? [],
    routingMap: data.routingMap ?? {},
  }
}

// ── Approval workflow routes (SurveyLifecycleController, US2) ────────────────────

/** Result of a submit / publish / return-to-draft action (ApprovalActionResult). */
export interface ApprovalActionResult {
  surveyId: string
  status: SurveyStatus
  rowVersion: number
}

/* eslint-disable @typescript-eslint/no-explicit-any -- wire boundary */
function toApprovalResult(w: any): ApprovalActionResult {
  return {
    surveyId: w.surveyId,
    // Serialised as the PascalCase member NAME here (string on the record), unlike the
    // int-enum DTOs — normalize handles both regardless.
    status: normalizeSurveyStatus(w.status),
    rowVersion: w.rowVersion ?? 0,
  }
}
/* eslint-enable @typescript-eslint/no-explicit-any */

/** POST /api/v1/surveys/{id}/submit — P-03 submits a Draft for review (FR-15.1). */
export async function submitSurvey(
  id: string,
  ifMatch: string | undefined
): Promise<EtagResult<ApprovalActionResult>> {
  const { data, etag } = await callJsonWithEtag<unknown>(`/surveys/${id}/submit`, {
    method: "POST",
    body: {},
    ifMatch,
  })
  return { data: toApprovalResult(data), etag }
}

/** POST /api/v1/surveys/{id}/publish — reviewer (or self-publish grant) → Active. */
export async function publishSurvey(
  id: string,
  remarks: string | undefined,
  ifMatch: string | undefined,
  idempotencyKey: string
): Promise<EtagResult<ApprovalActionResult>> {
  const { data, etag } = await callJsonWithEtag<unknown>(`/surveys/${id}/publish`, {
    method: "POST",
    body: { remarks: remarks ?? null },
    ifMatch,
    idempotencyKey,
  })
  return { data: toApprovalResult(data), etag }
}

/**
 * POST /api/v1/surveys/{id}/return-to-draft — reviewer returns a PendingReview survey
 * (FR-15.3, non-destructive). Remarks are required (400 `…remarks_required` when blank).
 */
export async function returnSurveyToDraft(
  id: string,
  remarks: string,
  ifMatch: string | undefined
): Promise<EtagResult<ApprovalActionResult>> {
  const { data, etag } = await callJsonWithEtag<unknown>(`/surveys/${id}/return-to-draft`, {
    method: "POST",
    body: { remarks },
    ifMatch,
  })
  return { data: toApprovalResult(data), etag }
}

// ── Theme routes (SurveyThemesController, F4 — consumed by the Appearance page) ──

/** GET /api/v1/surveys/{id}/theme — resolved appearance (Inherited or Customized). */
export async function getSurveyTheme(id: string): Promise<ThemeView> {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- wire boundary
  const { data } = await callJsonWithEtag<any>(`/surveys/${id}/theme`)
  return {
    primaryColour: data.primaryColour ?? "#0D8BBC",
    textColour: data.textColour ?? null,
    buttonRadiusPx: data.buttonRadiusPx ?? null,
  }
}

/** PUT /api/v1/surveys/{id}/theme — Customize mode save. */
export async function updateSurveyTheme(id: string, input: UpdateThemeInput): Promise<void> {
  await callJsonWithEtag<void>(`/surveys/${id}/theme`, {
    method: "PUT",
    body: {
      mode: themeModeToInt(input.mode),
      backgroundType: backgroundTypeToInt(input.backgroundType),
      backgroundImageHandle: input.backgroundImageHandle ?? null,
      primaryColour: input.primaryColour ?? null,
    },
  })
}

/**
 * POST /api/v1/surveys/{id}/theme/logo — multipart logo upload (→ IFileStorageService,
 * ClamAV + CMK envelope encryption server-side). Multipart, so it bypasses the JSON
 * transport; auth header only, browser sets the multipart boundary.
 */
export async function uploadSurveyThemeLogo(id: string, file: File): Promise<void> {
  const token = getSessionToken()
  const body = new FormData()
  body.append("file", file)
  const response = await fetch(`/api/v1/surveys/${id}/theme/logo`, {
    method: "POST",
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    body,
  })
  if (!response.ok) {
    let envelope: ApiErrorEnvelope | undefined
    try {
      envelope = (await response.json()) as ApiErrorEnvelope
    } catch {
      // Non-JSON error body — status only.
    }
    throw new SurveysApiError(response.status, envelope)
  }
}

/** Generates the Idempotency-Key for creates (APIs-constitution Art. 7.1). */
export function newIdempotencyKey(): string {
  return crypto.randomUUID()
}
