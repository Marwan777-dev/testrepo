// Wire types + boundary mappers for the M-13 Integration Hub console API.
//
// Two conventions this file enforces, both from CLAUDE.md "Backend Integration":
//
//  1. **Enums may arrive as integers.** `grep -rn "JsonStringEnumConverter\|AddJsonOptions" src/`
//     returns no global registration, so a .NET enum property serializes as its ordinal unless the
//     controller projects it via `.ToString()`. M-13's DB value converters persist the snake_case
//     wire value (`dispatch`, `api_key`, `date_time` — data-model.md), so a response may legitimately
//     carry EITHER form depending on how each controller (T035/T058/T083, not yet written) projects.
//     Every enum therefore gets a `normalize*(value: T | string | number): T` helper used at this
//     boundary; components only ever see the string union. Never index a display map with a raw
//     wire value.
//  2. **snake_case wire → camelCase domain.** Wire rows are suffixed `*Wire`; `map*` functions
//     convert. Components import only the camelCase domain types.

/** API-05 console error envelope (every non-2xx from `/api/v1/integration-hub/...`). */
export interface ApiErrorEnvelope {
  error: {
    code: string
    message: string
    correlation_id?: string
    tenant_id?: string
    /** Row-level import report (VR-F09) rides here for the Excel import failure path. */
    details?: { field: string; code: string; row?: number }[]
  }
}

// ---------------------------------------------------------------------------
// Enums (wire values mirror the backend value converters, Domain/ValueObjects/*)
// ---------------------------------------------------------------------------

/** SCN-01…05. Immutable after create (BR-02). */
export type Scenario =
  | "dispatch"
  | "redirect_link"
  | "json_render"
  | "iframe_embed"
  | "response_ingestion"

const SCENARIO_BY_ORDINAL: Scenario[] = [
  "dispatch", // 1 — SCN-01
  "redirect_link", // 2 — SCN-02
  "json_render", // 3 — SCN-03
  "iframe_embed", // 4 — SCN-04
  "response_ingestion", // 5 — SCN-05
]

/** BR-16/BR-17 — how a caller authenticates. Fixed at first credential generation. */
export type CredentialMechanism = "api_key" | "oauth_client"

const CREDENTIAL_MECHANISM_BY_ORDINAL: CredentialMechanism[] = ["api_key", "oauth_client"]

/** One-way lifecycle; there is no un-revoke (Status Lifecycle). */
export type CredentialStatus = "active" | "revoked"

const CREDENTIAL_STATUS_BY_ORDINAL: CredentialStatus[] = ["active", "revoked"]

/**
 * The thirteen ratified data types (FR-F0-04, VR-T01…T13). **The list is closed** — `duration` and
 * `identifier` were evaluated and rejected (`[PO-G17]`) and must never appear here or in SCR-06's
 * type select.
 */
export type DataType =
  | "text"
  | "number"
  | "boolean"
  | "email"
  | "phone"
  | "list"
  | "range"
  | "date"
  | "date_time"
  | "currency"
  | "percentage"
  | "url"
  | "geolocation"

const DATA_TYPE_BY_ORDINAL: DataType[] = [
  "text", // 1
  "number", // 2
  "boolean", // 3
  "email", // 4
  "phone", // 5
  "list", // 6
  "range", // 7
  "date", // 8
  "date_time", // 9
  "currency", // 10
  "percentage", // 11
  "url", // 12
  "geolocation", // 13
]

/** The closed, ordered type list SCR-06's select renders (FR-F0-04). */
export const DATA_TYPES: readonly DataType[] = DATA_TYPE_BY_ORDINAL

/** BR-09 — built-ins are enable/disable only; only `custom` rows count toward VR-F13's ≤200. */
export type ParameterOrigin = "built_in" | "custom"

const PARAMETER_ORIGIN_BY_ORDINAL: ParameterOrigin[] = ["built_in", "custom"]

/** BR-26 — one OAuth scope per scenario endpoint. */
export type OAuthScope =
  | "survey-requests:write"
  | "survey-links:read"
  | "survey-definitions:read"
  | "survey-embed:read"
  | "responses:write"

/** BR-26's scenario → required-scope map, used by the SCR-02 Step-2 scope picker. */
export const SCOPE_BY_SCENARIO: Record<Scenario, OAuthScope> = {
  dispatch: "survey-requests:write",
  redirect_link: "survey-links:read",
  json_render: "survey-definitions:read",
  iframe_embed: "survey-embed:read",
  response_ingestion: "responses:write",
}

/**
 * BR-27 (`[PO-G25]`) — mapping capability is determined by the data type. Returns whether the
 * Mapping-support flag is on and whether the user may change it. The server enforces the same rule;
 * this mirrors it so SCR-06 can render the control correctly without a round-trip.
 */
export function mappingSupportFor(type: DataType): { enabled: boolean; changeable: boolean } {
  if (type === "list") return { enabled: true, changeable: false } // always on, not changeable
  if (type === "text" || type === "boolean" || type === "url")
    return { enabled: false, changeable: true } // available, off by default
  return { enabled: false, changeable: false } // unavailable
}

/** Generic normalizer: accepts the string union, a stray ordinal, or an unknown value. */
function normalizeEnum<T extends string>(
  value: T | string | number | null | undefined,
  byOrdinal: T[],
  fallback: T,
): T {
  if (typeof value === "number") return byOrdinal[value - 1] ?? fallback
  if (typeof value === "string") {
    const lowered = value.toLowerCase()
    const direct = byOrdinal.find((member) => member === lowered)
    if (direct) return direct
    // Tolerate a PascalCase member name (`RedirectLink`, `DateTime`) should a controller ever
    // project via .ToString() instead of the converter's snake_case value.
    const fromPascal = lowered.replace(/([a-z])([A-Z])/g, "$1_$2")
    const matched = byOrdinal.find((member) => member === fromPascal)
    if (matched) return matched
    // Numeric string ("3") — some serializers emit ordinals as strings.
    const asNumber = Number(value)
    if (!Number.isNaN(asNumber)) return byOrdinal[asNumber - 1] ?? fallback
  }
  return fallback
}

export const normalizeScenario = (v: Scenario | string | number | null | undefined): Scenario =>
  normalizeEnum(v, SCENARIO_BY_ORDINAL, "dispatch")

export const normalizeCredentialMechanism = (
  v: CredentialMechanism | string | number | null | undefined,
): CredentialMechanism => normalizeEnum(v, CREDENTIAL_MECHANISM_BY_ORDINAL, "api_key")

export const normalizeCredentialStatus = (
  v: CredentialStatus | string | number | null | undefined,
): CredentialStatus => normalizeEnum(v, CREDENTIAL_STATUS_BY_ORDINAL, "revoked")

/** Defaults to `text` — the safest member: it never implies mapping support or range config. */
export const normalizeDataType = (v: DataType | string | number | null | undefined): DataType =>
  normalizeEnum(v, DATA_TYPE_BY_ORDINAL, "text")

/** Defaults to `built_in` — the more restrictive origin (no delete, locked type/field, BR-09). */
export const normalizeParameterOrigin = (
  v: ParameterOrigin | string | number | null | undefined,
): ParameterOrigin => normalizeEnum(v, PARAMETER_ORIGIN_BY_ORDINAL, "built_in")

// ---------------------------------------------------------------------------
// Cursor pagination (API-04)
// ---------------------------------------------------------------------------
// NOTE: the shipped controllers take `cursor` + `limit` query params and answer
// `{ items, next_cursor }` — NOT the `page_token`/`page_size`/`next_page_token` shape
// contracts/api-endpoints.md proposed. The controllers are the source of truth; verified against
// ServiceChannelsController.ListChannels and ParametersController.ListParameters. `limit` outside
// 1…200 silently falls back to the default page size rather than erroring (M-06 convention).

export interface PageWire<TRow> {
  items: TRow[]
  next_cursor?: string | null
}

export interface Page<TRow> {
  items: TRow[]
  /** Opaque cursor for the next page, or `null` on the last page. */
  nextCursor: string | null
}

function mapPage<TWire, TRow>(wire: PageWire<TWire>, map: (row: TWire) => TRow): Page<TRow> {
  return {
    items: (wire.items ?? []).map(map),
    nextCursor: wire.next_cursor ?? null,
  }
}

export { mapPage }

// ---------------------------------------------------------------------------
// Service channels (SCR-03/04)
// ---------------------------------------------------------------------------

/** One row of the per-channel parameter contract, as returned inside a channel detail. */
export interface ChannelContractRowWire {
  parameter_id: string
  api_field: string
  name_en: string
  name_ar: string
  supported: boolean
  required: boolean
}

export interface ChannelContractRow {
  parameterId: string
  apiField: string
  nameEn: string
  nameAr: string
  supported: boolean
  /** FR-S4-04 — only ever `true` while `supported` is `true`. */
  required: boolean
}

export interface ServiceChannelWire {
  id: string
  name_en: string
  name_ar: string
  channel_id: string
  description?: string | null
  active: boolean
  channel_id_locked: boolean
  supported_count: number
  required_count: number
  integrations_count: number
  /** Present on the detail response (`GET .../{id}`), absent on list rows. */
  contract?: ChannelContractRowWire[] | null
  created_at?: string
  updated_at?: string
}

export interface ServiceChannel {
  id: string
  nameEn: string
  nameAr: string
  /** The only mandatory API path parameter (BR-03). Case-preserved exactly as entered. */
  channelId: string
  description: string | null
  active: boolean
  /** BR-05 — one-way; `true` after the channel's first 2xx request, `channelId` then immutable. */
  channelIdLocked: boolean
  supportedCount: number
  requiredCount: number
  integrationsCount: number
  /** Empty on list rows; populated on the detail fetch that SCR-04 edit mode uses. */
  contract: ChannelContractRow[]
}

export function mapServiceChannel(wire: ServiceChannelWire): ServiceChannel {
  return {
    id: wire.id,
    nameEn: wire.name_en,
    nameAr: wire.name_ar,
    channelId: wire.channel_id,
    description: wire.description ?? null,
    active: wire.active,
    channelIdLocked: wire.channel_id_locked ?? false,
    supportedCount: wire.supported_count ?? 0,
    requiredCount: wire.required_count ?? 0,
    integrationsCount: wire.integrations_count ?? 0,
    contract: (wire.contract ?? []).map((row) => ({
      parameterId: row.parameter_id,
      apiField: row.api_field,
      nameEn: row.name_en,
      nameAr: row.name_ar,
      supported: row.supported,
      required: row.required,
    })),
  }
}

/** One contract row — FR-S4-04: `required` may only be true while `supported` is true. */
export interface ChannelParameterAssignmentInput {
  parameterId: string
  supported: boolean
  required: boolean
}

export interface ServiceChannelSaveInput {
  nameEn: string
  nameAr: string
  channelId: string
  description?: string
  active: boolean
  contract: ChannelParameterAssignmentInput[]
}

export function toServiceChannelWire(input: ServiceChannelSaveInput) {
  return {
    name_en: input.nameEn,
    name_ar: input.nameAr,
    channel_id: input.channelId,
    description: input.description?.trim() ? input.description : null,
    active: input.active,
    // FR-S4-04 enforced client-side too, so a stale UI state can never post `required` without
    // `supported`; the server force-clears it in the same write regardless.
    contract: input.contract.map((row) => ({
      parameter_id: row.parameterId,
      supported: row.supported,
      required: row.supported && row.required,
    })),
  }
}

/**
 * Sanitises a channel ID as the user types (VR-F04) — mirrors the server's `ChannelIdSanitizer`
 * exactly: keep only `[A-Za-z0-9-]`, preserve case, stop at 19 characters. Client-side this is a
 * live-typing affordance; the server re-applies it regardless, so the two must not diverge.
 */
export const CHANNEL_ID_MAX_LENGTH = 19

export function sanitizeChannelId(raw: string): string {
  let kept = ""
  for (const character of raw) {
    if (!/[A-Za-z0-9-]/.test(character)) continue
    kept += character
    if (kept.length === CHANNEL_ID_MAX_LENGTH) break
  }
  return kept
}

/** Server error codes for the channel endpoints (`ChannelErrorCodes.cs`), for inline mapping. */
export const CHANNEL_ERROR_CODES = {
  nameEnRequired: "validation.name_en_required",
  nameEnTooLong: "validation.name_en_too_long",
  nameArRequired: "validation.name_ar_required",
  nameArTooLong: "validation.name_ar_too_long",
  duplicateName: "validation.duplicate_name",
  channelIdRequired: "validation.channel_id_required",
  duplicateChannelId: "validation.duplicate_channel_id",
  channelIdLocked: "channel.id_locked",
  capacityExceeded: "validation.capacity_exceeded",
  unknownParameter: "validation.unknown_parameter",
  channelNotFound: "channel.not_found",
} as const

/** Maps a server error code onto the form field it belongs to, for inline rendering. */
export function channelFieldForCode(code: string): "nameEn" | "nameAr" | "channelId" | null {
  switch (code) {
    case CHANNEL_ERROR_CODES.nameEnRequired:
    case CHANNEL_ERROR_CODES.nameEnTooLong:
    case CHANNEL_ERROR_CODES.duplicateName:
      return "nameEn"
    case CHANNEL_ERROR_CODES.nameArRequired:
    case CHANNEL_ERROR_CODES.nameArTooLong:
      return "nameAr"
    case CHANNEL_ERROR_CODES.channelIdRequired:
    case CHANNEL_ERROR_CODES.duplicateChannelId:
    case CHANNEL_ERROR_CODES.channelIdLocked:
      return "channelId"
    default:
      return null
  }
}

// ---------------------------------------------------------------------------
// Parameters (SCR-05/06)
// ---------------------------------------------------------------------------

export interface ParameterWire {
  id: string
  name_en: string
  name_ar: string
  api_field: string
  api_field_locked: boolean
  data_type: DataType | string | number
  data_type_locked: boolean
  range_min?: number | null
  range_max?: number | null
  range_unit?: string | null
  validation_rule?: string | null
  origin: ParameterOrigin | string | number
  enabled: boolean
  required_by_default: boolean
  filterable: boolean
  reporting_visibility: boolean
  dashboard_visibility: boolean
  mapping_support: boolean
  /** BR-27 — whether the user may toggle `mapping_support`, derived server-side from `data_type`. */
  mapping_support_changeable?: boolean
  mappings_count?: number | null
  /** Channel assignments (FR-S6-05); its length is SCR-05's "Channels" count. */
  channel_ids?: string[] | null
}

export interface Parameter {
  id: string
  nameEn: string
  nameAr: string
  /** The wire key the caller sends. Locked after first use (BR-11); built-ins always locked. */
  apiField: string
  apiFieldLocked: boolean
  dataType: DataType
  /** `[PO-G27]` — read-only for built-ins (derived from origin server-side). */
  dataTypeLocked: boolean
  rangeMin: number | null
  rangeMax: number | null
  rangeUnit: string | null
  validationRule: string | null
  origin: ParameterOrigin
  enabled: boolean
  /** Assignment default only — the channel contract is authoritative at request time (BR-08). */
  requiredByDefault: boolean
  filterable: boolean
  reportingVisibility: boolean
  dashboardVisibility: boolean
  /** BR-27 — derived from `dataType`; see `mappingSupportFor`. */
  mappingSupport: boolean
  /** BR-27 — server's verdict on whether the Mapping-support flag is user-changeable. */
  mappingSupportChangeable: boolean
  mappingsCount: number
  channelIds: string[]
}

export function mapParameter(wire: ParameterWire): Parameter {
  return {
    id: wire.id,
    nameEn: wire.name_en,
    nameAr: wire.name_ar,
    apiField: wire.api_field,
    apiFieldLocked: wire.api_field_locked ?? false,
    dataType: normalizeDataType(wire.data_type),
    dataTypeLocked: wire.data_type_locked ?? false,
    rangeMin: wire.range_min ?? null,
    rangeMax: wire.range_max ?? null,
    rangeUnit: wire.range_unit ?? null,
    validationRule: wire.validation_rule ?? null,
    origin: normalizeParameterOrigin(wire.origin),
    enabled: wire.enabled,
    requiredByDefault: wire.required_by_default ?? false,
    filterable: wire.filterable ?? false,
    reportingVisibility: wire.reporting_visibility ?? false,
    dashboardVisibility: wire.dashboard_visibility ?? false,
    mappingSupport: wire.mapping_support ?? false,
    mappingSupportChangeable:
      wire.mapping_support_changeable ?? mappingSupportFor(normalizeDataType(wire.data_type)).changeable,
    mappingsCount: wire.mappings_count ?? 0,
    channelIds: wire.channel_ids ?? [],
  }
}

/** `GET /parameters` additionally returns the SCR-05 origin-tab counts (global, filter-independent). */
export interface ParameterCounts {
  all: number
  builtIn: number
  custom: number
}

export interface ParameterListResultWire extends PageWire<ParameterWire> {
  counts?: { all: number; built_in: number; custom: number } | null
}

export interface ParameterListResult extends Page<Parameter> {
  counts: ParameterCounts
}

export function mapParameterListResult(wire: ParameterListResultWire): ParameterListResult {
  return {
    ...mapPage(wire, mapParameter),
    counts: {
      all: wire.counts?.all ?? 0,
      builtIn: wire.counts?.built_in ?? 0,
      custom: wire.counts?.custom ?? 0,
    },
  }
}

export interface ParameterSaveInput {
  nameEn: string
  nameAr: string
  apiField: string
  dataType: DataType
  enabled?: boolean
  rangeMin?: number
  rangeMax?: number
  rangeUnit?: string
  validationRule?: string
  requiredByDefault: boolean
  filterable: boolean
  reportingVisibility: boolean
  dashboardVisibility: boolean
  mappingSupport: boolean
  /** Channel-assignment pills (FR-S6-05) — adds the parameter as *supported* on each channel. */
  channelIds?: string[]
}

export function toParameterWire(input: ParameterSaveInput) {
  return {
    name_en: input.nameEn,
    name_ar: input.nameAr,
    api_field: input.apiField,
    data_type: input.dataType,
    range_min: input.rangeMin ?? null,
    range_max: input.rangeMax ?? null,
    range_unit: input.rangeUnit ?? null,
    validation_rule: input.validationRule?.trim() ? input.validationRule : null,
    enabled: input.enabled ?? true,
    required_by_default: input.requiredByDefault,
    filterable: input.filterable,
    reporting_visibility: input.reportingVisibility,
    dashboard_visibility: input.dashboardVisibility,
    // BR-27 is server-enforced; sending the type-derived value keeps the two in agreement.
    mapping_support: mappingSupportFor(input.dataType).changeable
      ? input.mappingSupport
      : mappingSupportFor(input.dataType).enabled,
    channel_ids: input.channelIds ?? [],
  }
}

/** One reference blocking a disable, rendered by Dialog D-6 (BR-10). */
export interface ParameterReferenceWire {
  kind: string
  name: string
}

export interface ParameterReference {
  /** e.g. `scope_filter` | `rule` | `channel_contract` — rendered as a labelled group in D-6. */
  kind: string
  name: string
}

export interface ParameterPatchResultWire {
  parameter: ParameterWire
  requires_confirmation?: boolean
  references?: ParameterReferenceWire[] | null
}

export interface ParameterPatchResult {
  parameter: Parameter
  /**
   * BR-10's two-step disable. When `true` the server made **no change** and returned the reference
   * list so the client can render Dialog D-6; re-issue the same PATCH with `confirmDisable: true`
   * to apply it. This is a 200, not a 4xx — nothing failed, the write is just awaiting consent.
   */
  requiresConfirmation: boolean
  /** Referencing scope filters / rules / channel contracts, by name (BR-10). */
  references: ParameterReference[]
}

export function mapParameterPatchResult(wire: ParameterPatchResultWire): ParameterPatchResult {
  return {
    parameter: mapParameter(wire.parameter),
    requiresConfirmation: wire.requires_confirmation ?? false,
    references: (wire.references ?? []).map((r) => ({ kind: r.kind, name: r.name })),
  }
}

/** Server error codes for the parameter endpoints (`ParameterErrorCodes.cs`), for inline mapping. */
export const PARAMETER_ERROR_CODES = {
  nameEnRequired: "validation.name_en_required",
  nameEnTooLong: "validation.name_en_too_long",
  nameArRequired: "validation.name_ar_required",
  nameArTooLong: "validation.name_ar_too_long",
  apiFieldRequired: "validation.api_field_required",
  apiFieldFormat: "validation.api_field_format",
  duplicateApiField: "validation.duplicate_api_field",
  apiFieldLocked: "parameter.api_field_locked",
  rangeMinRequired: "validation.range_min_required",
  rangeMaxRequired: "validation.range_max_required",
  rangeMinMax: "validation.range_min_max",
  rangeNotApplicable: "validation.range_not_applicable",
  parameterTypeLocked: "parameter.type_locked",
  capacityExceeded: "validation.capacity_exceeded",
  invalidDataType: "validation.invalid_data_type",
  unknownChannel: "validation.unknown_channel",
  parameterNotFound: "parameter.not_found",
} as const

export type ParameterFieldKey = "nameEn" | "nameAr" | "apiField" | "dataType" | "rangeMin" | "rangeMax"

/** Maps a server error code onto the drawer field it belongs to, for inline rendering. */
export function parameterFieldForCode(code: string): ParameterFieldKey | null {
  switch (code) {
    case PARAMETER_ERROR_CODES.nameEnRequired:
    case PARAMETER_ERROR_CODES.nameEnTooLong:
      return "nameEn"
    case PARAMETER_ERROR_CODES.nameArRequired:
    case PARAMETER_ERROR_CODES.nameArTooLong:
      return "nameAr"
    case PARAMETER_ERROR_CODES.apiFieldRequired:
    case PARAMETER_ERROR_CODES.apiFieldFormat:
    case PARAMETER_ERROR_CODES.duplicateApiField:
    case PARAMETER_ERROR_CODES.apiFieldLocked:
      return "apiField"
    case PARAMETER_ERROR_CODES.invalidDataType:
    case PARAMETER_ERROR_CODES.parameterTypeLocked:
      return "dataType"
    case PARAMETER_ERROR_CODES.rangeMinRequired:
    case PARAMETER_ERROR_CODES.rangeMinMax:
      return "rangeMin"
    case PARAMETER_ERROR_CODES.rangeMaxRequired:
      return "rangeMax"
    default:
      return null
  }
}

/**
 * SCR-06's API-field auto-suggest (AC-S6-02): lowercase, spaces → `_`, strip anything outside
 * `[a-z0-9_]`. Mirrors the server's `ApiFieldNameSuggester`; the server's uniqueness validator
 * additionally requires the result to match `^[a-z][a-z0-9_]*$`.
 */
export function suggestApiField(nameEn: string): string {
  return nameEn
    .toLowerCase()
    .replace(/\s+/g, "_")
    .replace(/[^a-z0-9_]/g, "")
    .replace(/_+/g, "_")
    .replace(/^_+|_+$/g, "")
}

// ---------------------------------------------------------------------------
// Integrations + credentials (SCR-01/02)
// ---------------------------------------------------------------------------
// NOTE: verified against the shipped `IntegrationsController`. Two things the server computes
// that this client must NOT re-derive: the **endpoint preview** (method/path/scope/success shape,
// FR-S2-07/09) and the **accepted-parameters table** (the selected channel's contract, FR-S2-08).
// Both arrive on the detail response and re-render whenever the channel or scenario changes.

/** One row of SCR-02 step 3's Accepted-parameters table, projected from the channel contract. */
export interface AcceptedParameterWire {
  parameter_id: string
  api_field: string
  name_en: string
  name_ar: string
  data_type: DataType | string | number
  required: boolean
}

export interface AcceptedParameter {
  parameterId: string
  apiField: string
  nameEn: string
  nameAr: string
  dataType: DataType
  /** Per the channel contract (BR-08) — the runtime authority on requiredness. */
  required: boolean
}

/** The generated endpoint preview for the chosen scenario + channel (FR-S2-07). */
export interface IntegrationEndpointWire {
  method: string
  path: string
  path_template: string
  required_scope: string
  success_status: number
  success_description: string
}

export interface IntegrationEndpoint {
  method: string
  /** Concrete path with the channel id substituted — what the caller actually calls. */
  path: string
  /** Same path with `{channelId}` left as a token, for highlighting it in the preview. */
  pathTemplate: string
  /** The OAuth scope a token must carry to call it (BR-26). */
  requiredScope: string
  successStatus: number
  successDescription: string
}

/** The credential as the console may see it — masked; the plaintext is show-once (BR-16). */
export interface CredentialWire {
  id: string
  mechanism: CredentialMechanism | string | number
  label_or_client_name: string
  scopes?: string[] | null
  status: CredentialStatus | string | number
  generated_at: string
  generated_by?: string | null
  revoked_at?: string | null
}

export interface Credential {
  id: string
  mechanism: CredentialMechanism
  labelOrClientName: string
  scopes: OAuthScope[]
  status: CredentialStatus
  generatedAt: string
  generatedBy: string | null
  revokedAt: string | null
}

export function mapCredential(wire: CredentialWire): Credential {
  return {
    id: wire.id,
    mechanism: normalizeCredentialMechanism(wire.mechanism),
    labelOrClientName: wire.label_or_client_name,
    scopes: (wire.scopes ?? []) as OAuthScope[],
    status: normalizeCredentialStatus(wire.status),
    generatedAt: wire.generated_at,
    generatedBy: wire.generated_by ?? null,
    revokedAt: wire.revoked_at ?? null,
  }
}

/** Full detail (`GET /integrations/{id}`, and the body of a create/update response). */
export interface IntegrationWire {
  id: string
  name: string
  description?: string | null
  service_channel_id: string
  service_channel_name: string
  channel_id: string
  service_channel_active: boolean
  scenario: Scenario | string | number
  active: boolean
  allowed_origins?: string[] | null
  link_expiry_override_hours?: number | null
  endpoint: IntegrationEndpointWire
  accepted_parameters: AcceptedParameterWire[]
  credential?: CredentialWire | null
  created_at: string
  updated_at: string
}

export interface Integration {
  id: string
  name: string
  description: string | null
  serviceChannelId: string
  serviceChannelName: string
  /** The channel's URL-safe id — the monospace chip and the endpoint path token. */
  channelId: string
  /** False when the serving channel was deactivated — SCR-02 shows a warning (edge case). */
  serviceChannelActive: boolean
  /** Immutable after create (BR-02). */
  scenario: Scenario
  active: boolean
  /** SCN-04 only (FR-S2-10). */
  allowedOrigins: string[]
  /** SCN-02 only; null falls back to the 24h default (FR-F0-08). */
  linkExpiryOverrideHours: number | null
  endpoint: IntegrationEndpoint
  acceptedParameters: AcceptedParameter[]
  /** Masked; null when no credential has been generated yet. */
  credential: Credential | null
  createdAt: string
  updatedAt: string
}

export function mapIntegration(wire: IntegrationWire): Integration {
  return {
    id: wire.id,
    name: wire.name,
    description: wire.description ?? null,
    serviceChannelId: wire.service_channel_id,
    serviceChannelName: wire.service_channel_name,
    channelId: wire.channel_id,
    serviceChannelActive: wire.service_channel_active ?? true,
    scenario: normalizeScenario(wire.scenario),
    active: wire.active,
    allowedOrigins: wire.allowed_origins ?? [],
    linkExpiryOverrideHours: wire.link_expiry_override_hours ?? null,
    endpoint: {
      method: wire.endpoint?.method ?? "POST",
      path: wire.endpoint?.path ?? "",
      pathTemplate: wire.endpoint?.path_template ?? "",
      requiredScope: wire.endpoint?.required_scope ?? "",
      successStatus: wire.endpoint?.success_status ?? 202,
      successDescription: wire.endpoint?.success_description ?? "",
    },
    acceptedParameters: (wire.accepted_parameters ?? []).map((row) => ({
      parameterId: row.parameter_id,
      apiField: row.api_field,
      nameEn: row.name_en,
      nameAr: row.name_ar,
      dataType: normalizeDataType(row.data_type),
      required: row.required,
    })),
    credential: wire.credential ? mapCredential(wire.credential) : null,
    createdAt: wire.created_at,
    updatedAt: wire.updated_at,
  }
}

/** The lighter row shape returned by `GET /integrations` (SCR-01's table). */
/** FR-S1-03 per-row traffic, on the same rolling 24h window as the tiles (T130). */
export interface IntegrationTrafficWire {
  requests_24h: number
  failed_requests_24h: number
  /** null for a brand-new integration with no traffic yet (AC-S1-03). */
  error_rate?: number | null
  error_rate_display: string
  /** `neutral` | `d2` | `d3` | `d4` — the server owns FR-S1-06's thresholds. */
  error_rate_band: string
  last_activity_at?: string | null
}

export interface IntegrationTraffic {
  requests24h: number
  failedRequests24h: number
  errorRate: number | null
  errorRateDisplay: string
  errorRateBand: string
  lastActivityAt: string | null
}

export function mapIntegrationTraffic(wire?: IntegrationTrafficWire | null): IntegrationTraffic {
  // A never-called integration carries a zeroed row, not null — but tolerate a missing object so
  // an older backend can't blank the whole table.
  return {
    requests24h: wire?.requests_24h ?? 0,
    failedRequests24h: wire?.failed_requests_24h ?? 0,
    errorRate: wire?.error_rate ?? null,
    errorRateDisplay: wire?.error_rate_display ?? "—",
    errorRateBand: wire?.error_rate_band ?? "neutral",
    lastActivityAt: wire?.last_activity_at ?? null,
  }
}

export interface IntegrationListItemWire {
  id: string
  name: string
  description?: string | null
  service_channel_id: string
  service_channel_name: string
  channel_id: string
  scenario: Scenario | string | number
  active: boolean
  endpoint_path: string
  credential_mechanism?: CredentialMechanism | string | number | null
  credential_status?: CredentialStatus | string | number | null
  created_at: string
  traffic?: IntegrationTrafficWire | null
}

export interface IntegrationListItem {
  id: string
  name: string
  description: string | null
  serviceChannelId: string
  serviceChannelName: string
  channelId: string
  scenario: Scenario
  active: boolean
  endpointPath: string
  credentialMechanism: CredentialMechanism | null
  credentialStatus: CredentialStatus | null
  createdAt: string
  traffic: IntegrationTraffic
}

export function mapIntegrationListItem(wire: IntegrationListItemWire): IntegrationListItem {
  return {
    id: wire.id,
    name: wire.name,
    description: wire.description ?? null,
    serviceChannelId: wire.service_channel_id,
    serviceChannelName: wire.service_channel_name,
    channelId: wire.channel_id,
    scenario: normalizeScenario(wire.scenario),
    active: wire.active,
    endpointPath: wire.endpoint_path,
    credentialMechanism:
      wire.credential_mechanism == null
        ? null
        : normalizeCredentialMechanism(wire.credential_mechanism),
    credentialStatus:
      wire.credential_status == null ? null : normalizeCredentialStatus(wire.credential_status),
    createdAt: wire.created_at,
    traffic: mapIntegrationTraffic(wire.traffic),
  }
}

/**
 * SCR-02 Step-2 credential configuration — an API key OR an OAuth client, never both.
 * The wire payload is one flat object with a `mechanism` discriminator (`CredentialPayload`).
 */
export type CredentialInput =
  | { mechanism: "api_key"; keyLabel: string }
  | { mechanism: "oauth_client"; clientName: string; scopes: OAuthScope[] }

export function toCredentialWire(credential: CredentialInput) {
  return credential.mechanism === "api_key"
    ? { mechanism: "api_key", key_label: credential.keyLabel }
    : {
        mechanism: "oauth_client",
        client_name: credential.clientName,
        scopes: credential.scopes,
      }
}

export interface IntegrationCreateInput {
  name: string
  description?: string
  serviceChannelId: string
  scenario: Scenario
  allowedOrigins?: string[]
  linkExpiryOverrideHours?: number
  credential: CredentialInput
}

export function toIntegrationCreateWire(input: IntegrationCreateInput) {
  return {
    name: input.name,
    description: input.description?.trim() ? input.description : null,
    service_channel_id: input.serviceChannelId,
    scenario: input.scenario,
    allowed_origins: input.allowedOrigins?.length ? input.allowedOrigins : null,
    link_expiry_override_hours: input.linkExpiryOverrideHours ?? null,
    credential: toCredentialWire(input.credential),
  }
}

/**
 * Edit mode (FR-S2-01). `scenario` IS accepted by the server, which rejects an actual change with
 * 409 `integration.scenario_immutable` (BR-02) — so send the unchanged value rather than omitting
 * it, and never offer the scenario cards for editing.
 */
export interface IntegrationUpdateInput {
  name: string
  description?: string
  serviceChannelId: string
  scenario: Scenario
  allowedOrigins?: string[]
  linkExpiryOverrideHours?: number
}

export function toIntegrationUpdateWire(input: IntegrationUpdateInput) {
  return {
    name: input.name,
    description: input.description?.trim() ? input.description : null,
    service_channel_id: input.serviceChannelId,
    scenario: input.scenario,
    allowed_origins: input.allowedOrigins?.length ? input.allowedOrigins : null,
    link_expiry_override_hours: input.linkExpiryOverrideHours ?? null,
  }
}

/**
 * The show-once credential payload (Dialogs D-1/D-2, BR-16). `secret` is the ONLY time this value
 * is ever available — never persisted client-side, never re-fetchable. Hold it in component state
 * only; never localStorage, never a log.
 */
export interface GeneratedCredentialWire {
  credential: CredentialWire
  secret: string
  client_id?: string | null
  grant_type?: string | null
  access_token_lifetime_seconds?: number | null
  token_endpoint?: string | null
}

export interface GeneratedCredential {
  credential: Credential
  /** Show-once plaintext (BR-16). */
  secret: string
  /** OAuth only — the client id paired with the secret. */
  clientId: string | null
  /** Always `client_credentials`, fixed in code and not configurable (BR-17). */
  grantType: string | null
  /** Always 900s / 15 minutes, fixed in code and not configurable (BR-17). */
  accessTokenLifetimeSeconds: number | null
  tokenEndpoint: string | null
}

export function mapGeneratedCredential(wire: GeneratedCredentialWire): GeneratedCredential {
  return {
    credential: mapCredential(wire.credential),
    secret: wire.secret,
    clientId: wire.client_id ?? null,
    grantType: wire.grant_type ?? null,
    accessTokenLifetimeSeconds: wire.access_token_lifetime_seconds ?? null,
    tokenEndpoint: wire.token_endpoint ?? null,
  }
}

/** `POST /integrations` returns the row AND its show-once secret in one response. */
export interface IntegrationCreatedWire {
  integration: IntegrationWire
  credential: GeneratedCredentialWire
}

export interface IntegrationCreated {
  integration: Integration
  credential: GeneratedCredential
}

export function mapIntegrationCreated(wire: IntegrationCreatedWire): IntegrationCreated {
  return {
    integration: mapIntegration(wire.integration),
    credential: mapGeneratedCredential(wire.credential),
  }
}

/** Server error codes for the integration endpoints (`IntegrationErrorCodes.cs`). */
export const INTEGRATION_ERROR_CODES = {
  nameRequired: "validation.name_required",
  nameTooLong: "validation.name_too_long",
  duplicateName: "validation.duplicate_name",
  scenarioRequired: "validation.scenario_required",
  scenarioImmutable: "integration.scenario_immutable",
  serviceChannelRequired: "validation.service_channel_required",
  serviceChannelNotFound: "validation.service_channel_not_found",
  channelNotActive: "channel.not_active",
  capacityExceeded: "validation.capacity_exceeded",
  integrationNotFound: "integration.not_found",
  credentialRequired: "validation.credential_required",
  mechanismRequired: "validation.credential_mechanism_required",
  mechanismImmutable: "credential.mechanism_immutable",
  keyLabelRequired: "validation.key_label_required",
  clientNameRequired: "validation.client_name_required",
  scopesRequired: "validation.scopes_required",
  unknownScope: "validation.unknown_scope",
  credentialNotFound: "credential.not_found",
  credentialAlreadyRevoked: "credential.already_revoked",
  allowedOriginsNotApplicable: "validation.allowed_origins_not_applicable",
  allowedOriginInvalid: "validation.allowed_origin_invalid",
  linkExpiryNotApplicable: "validation.link_expiry_not_applicable",
  linkExpiryInvalid: "validation.link_expiry_invalid",
} as const

export type IntegrationFieldKey =
  | "name"
  | "serviceChannelId"
  | "scenario"
  | "keyLabel"
  | "clientName"
  | "scopes"
  | "allowedOrigins"
  | "linkExpiryOverrideHours"

/** Maps a server error code onto the wizard field it belongs to, for inline rendering. */
export function integrationFieldForCode(code: string): IntegrationFieldKey | null {
  switch (code) {
    case INTEGRATION_ERROR_CODES.nameRequired:
    case INTEGRATION_ERROR_CODES.nameTooLong:
    case INTEGRATION_ERROR_CODES.duplicateName:
      return "name"
    case INTEGRATION_ERROR_CODES.serviceChannelRequired:
    case INTEGRATION_ERROR_CODES.serviceChannelNotFound:
    case INTEGRATION_ERROR_CODES.channelNotActive:
      return "serviceChannelId"
    case INTEGRATION_ERROR_CODES.scenarioRequired:
    case INTEGRATION_ERROR_CODES.scenarioImmutable:
      return "scenario"
    case INTEGRATION_ERROR_CODES.keyLabelRequired:
      return "keyLabel"
    case INTEGRATION_ERROR_CODES.clientNameRequired:
      return "clientName"
    case INTEGRATION_ERROR_CODES.scopesRequired:
    case INTEGRATION_ERROR_CODES.unknownScope:
      return "scopes"
    case INTEGRATION_ERROR_CODES.allowedOriginsNotApplicable:
    case INTEGRATION_ERROR_CODES.allowedOriginInvalid:
      return "allowedOrigins"
    case INTEGRATION_ERROR_CODES.linkExpiryNotApplicable:
    case INTEGRATION_ERROR_CODES.linkExpiryInvalid:
      return "linkExpiryOverrideHours"
    default:
      return null
  }
}

/** The five scenarios in their ratified SCR-02 card order (SCN-01…05). */
export const SCENARIOS: readonly Scenario[] = [
  "dispatch",
  "redirect_link",
  "json_render",
  "iframe_embed",
  "response_ingestion",
]


// ---------------------------------------------------------------------------
// Parameter mappings (SCR-07)
// ---------------------------------------------------------------------------
// Verified against the shipped `ParameterMappingsController`. Routes are nested under the
// parameter: `/parameters/{parameterId}/mappings[...]`. The list response carries a `parameter`
// header block (name/api_field/count/label) so the page can render the selector's chosen entry
// without a second fetch.

/** The parameter a mapping set belongs to, as returned in the list header. */
export interface MappingParameterWire {
  id: string
  name_en: string
  name_ar: string
  api_field: string
  mapping_count: number
  label: string
}

export interface MappingParameter {
  id: string
  nameEn: string
  nameAr: string
  apiField: string
  mappingCount: number
  /** Server-composed "Name — api_field (n values)" label (FR-S7-01). */
  label: string
}

export interface ParameterMappingWire {
  id: string
  parameter_id: string
  source_value: string
  display_en: string
  display_ar: string
  status: string
  created_at: string
  updated_at: string
}

export interface ParameterMapping {
  id: string
  parameterId: string
  /** Unique within the parameter, case-insensitively (VR-F08). */
  sourceValue: string
  displayEn: string
  displayAr: string
  /** `active` — `draft` is a client-only pre-save state and is never persisted (data-model §6). */
  status: string
  createdAt: string
  updatedAt: string
}

export function mapParameterMapping(wire: ParameterMappingWire): ParameterMapping {
  return {
    id: wire.id,
    parameterId: wire.parameter_id,
    sourceValue: wire.source_value,
    displayEn: wire.display_en,
    displayAr: wire.display_ar,
    status: wire.status ?? "active",
    createdAt: wire.created_at,
    updatedAt: wire.updated_at,
  }
}

export interface ParameterMappingListResultWire {
  parameter: MappingParameterWire
  items: ParameterMappingWire[]
  next_cursor?: string | null
  total_count: number
}

export interface ParameterMappingListResult {
  parameter: MappingParameter
  items: ParameterMapping[]
  nextCursor: string | null
  totalCount: number
}

export function mapParameterMappingListResult(
  wire: ParameterMappingListResultWire,
): ParameterMappingListResult {
  const parameter = wire.parameter
  return {
    parameter: {
      id: parameter?.id ?? "",
      nameEn: parameter?.name_en ?? "",
      nameAr: parameter?.name_ar ?? "",
      apiField: parameter?.api_field ?? "",
      mappingCount: parameter?.mapping_count ?? 0,
      label: parameter?.label ?? "",
    },
    items: (wire.items ?? []).map(mapParameterMapping),
    nextCursor: wire.next_cursor ?? null,
    totalCount: wire.total_count ?? 0,
  }
}

export interface ParameterMappingSaveInput {
  sourceValue: string
  displayEn: string
  displayAr: string
}

export function toParameterMappingWire(input: ParameterMappingSaveInput) {
  return {
    source_value: input.sourceValue,
    display_en: input.displayEn,
    display_ar: input.displayAr,
  }
}

export interface UnmappedValueWire {
  id: string
  parameter_id: string
  raw_value: string
  first_seen_at: string
  last_seen_at: string
  occurrence_count: number
}

export interface UnmappedValue {
  id: string
  parameterId: string
  /** Case-preserved as received; matched case-insensitively against existing mappings. */
  rawValue: string
  firstSeenAt: string
  lastSeenAt: string
  occurrenceCount: number
}

export function mapUnmappedValue(wire: UnmappedValueWire): UnmappedValue {
  return {
    id: wire.id,
    parameterId: wire.parameter_id,
    rawValue: wire.raw_value,
    firstSeenAt: wire.first_seen_at,
    lastSeenAt: wire.last_seen_at,
    occurrenceCount: wire.occurrence_count ?? 1,
  }
}

export interface UnmappedValueQueueWire {
  items: UnmappedValueWire[]
  window_days: number
  window_start: string
}

export interface UnmappedValueQueue {
  items: UnmappedValue[]
  /** 7 per FR-S7-02 — read from the server rather than hard-coded. */
  windowDays: number
  windowStart: string
}

export function mapUnmappedValueQueue(wire: UnmappedValueQueueWire): UnmappedValueQueue {
  return {
    items: (wire.items ?? []).map(mapUnmappedValue),
    windowDays: wire.window_days ?? 7,
    windowStart: wire.window_start ?? "",
  }
}

/** Server error codes for the mapping endpoints (`MappingErrorCodes.cs`). */
export const MAPPING_ERROR_CODES = {
  sourceValueRequired: "validation.source_value_required",
  duplicateSourceValue: "validation.duplicate_source_value",
  displayEnRequired: "validation.display_en_required",
  displayArRequired: "validation.display_ar_required",
  capacityExceeded: "validation.capacity_exceeded",
  parameterNotMappingEnabled: "validation.parameter_not_mapping_enabled",
  parameterNotFound: "parameter.not_found",
  mappingNotFound: "mapping.not_found",
} as const

export type MappingFieldKey = "sourceValue" | "displayEn" | "displayAr"

/** Maps a server error code onto the draft-row field it belongs to, for inline rendering. */
export function mappingFieldForCode(code: string): MappingFieldKey | null {
  switch (code) {
    case MAPPING_ERROR_CODES.sourceValueRequired:
    case MAPPING_ERROR_CODES.duplicateSourceValue:
      return "sourceValue"
    case MAPPING_ERROR_CODES.displayEnRequired:
      return "displayEn"
    case MAPPING_ERROR_CODES.displayArRequired:
      return "displayAr"
    default:
      return null
  }
}

/** Dialog D-4's two import modes. `merge` is the pre-selected, non-destructive default (US7). */
export type MappingImportMode = "merge" | "replace_all"

/** Successful import outcome — counts of what actually changed (FR-S7-06/07). */
export interface MappingImportResultWire {
  mode: string
  rows_added: number
  rows_updated: number
  rows_removed: number
  total_count: number
}

export interface MappingImportResult {
  mode: MappingImportMode
  rowsAdded: number
  rowsUpdated: number
  /** Non-zero only for Replace-all, which deletes the prior set (FR-S7-07). */
  rowsRemoved: number
  /** The parameter's mapping count after the import. */
  totalCount: number
}

export function mapMappingImportResult(wire: MappingImportResultWire): MappingImportResult {
  return {
    mode: wire.mode === "replace_all" ? "replace_all" : "merge",
    rowsAdded: wire.rows_added ?? 0,
    rowsUpdated: wire.rows_updated ?? 0,
    rowsRemoved: wire.rows_removed ?? 0,
    totalCount: wire.total_count ?? 0,
  }
}

/**
 * One failing row in the all-or-nothing validation report (VR-F09).
 *
 * NOTE the shape: the server returns these as a **top-level `rows` array**, NOT inside the API-05
 * envelope's `error.details`. That is why the import path throws `MappingImportError` rather than
 * the usual `IntegrationHubApiError` — a plain envelope parse would silently drop the report.
 */
export interface MappingImportRowErrorWire {
  row: number
  column: string
  reason: string
}

export interface MappingImportRowError {
  /** 1-based row number in the uploaded file, as the user sees it in Excel. */
  row: number
  column: string
  reason: string
}

export interface MappingImportRejectionWire {
  error: ApiErrorEnvelope["error"]
  rows?: MappingImportRowErrorWire[] | null
}

export function mapImportRows(wire: MappingImportRejectionWire): MappingImportRowError[] {
  return (wire.rows ?? []).map((row) => ({
    row: row.row,
    column: row.column,
    reason: row.reason,
  }))
}

/** Import-specific server error codes, on top of the shared mapping ones. */
export const MAPPING_IMPORT_ERROR_CODES = {
  rowLimitExceeded: "validation.import_row_limit_exceeded",
  invalid: "validation.import_invalid",
  fileRequired: "validation.import_file_required",
  fileTooLarge: "validation.import_file_too_large",
  fileUnreadable: "validation.import_file_unreadable",
  modeInvalid: "validation.import_mode_invalid",
} as const


// ---------------------------------------------------------------------------
// Request logs (SCR-08) + integration health tiles (SCR-01)
// ---------------------------------------------------------------------------
// Verified against the shipped `RequestLogsController` / `IntegrationHealthTilesResponse`.
// The server does the *presentation* work here — it returns pre-formatted `*_display` strings,
// the tile sub-texts, and the error-rate band — so the client renders what it is given rather
// than re-deriving thresholds and risking a mismatch with FR-S1-06.

/** FR-S1-01 — the three SCR-01 stat tiles, computed over the rolling 24h window. */
export interface IntegrationHealthTilesWire {
  total_integrations: number
  active_integrations: number
  integrations_display: string
  integrations_sub_text: string
  requests_24h: number
  requests_24h_display: string
  requests_24h_sub_text: string
  failed_requests_24h: number
  error_rate?: number | null
  error_rate_display: string
  error_rate_sub_text: string
  error_rate_band: string
}

export interface IntegrationHealthTiles {
  totalIntegrations: number
  activeIntegrations: number
  /** e.g. "6 / 5 active" — server-formatted (AC-S1-01). */
  integrationsDisplay: string
  integrationsSubText: string
  requests24h: number
  requests24hDisplay: string
  requests24hSubText: string
  failedRequests24h: number
  /** Fractional rate, or null when there is no traffic — renders "—" (FR-S1-05). */
  errorRate: number | null
  errorRateDisplay: string
  errorRateSubText: string
  /** FR-S1-06 band the server resolved: `d2` (<1%) · `d3` (1–5%) · `d4` (>5%) · `none`. */
  errorRateBand: string
}

export function mapHealthTiles(wire: IntegrationHealthTilesWire): IntegrationHealthTiles {
  return {
    totalIntegrations: wire.total_integrations ?? 0,
    activeIntegrations: wire.active_integrations ?? 0,
    integrationsDisplay: wire.integrations_display ?? "—",
    integrationsSubText: wire.integrations_sub_text ?? "",
    requests24h: wire.requests_24h ?? 0,
    requests24hDisplay: wire.requests_24h_display ?? "—",
    requests24hSubText: wire.requests_24h_sub_text ?? "",
    failedRequests24h: wire.failed_requests_24h ?? 0,
    errorRate: wire.error_rate ?? null,
    errorRateDisplay: wire.error_rate_display ?? "—",
    errorRateSubText: wire.error_rate_sub_text ?? "",
    errorRateBand: wire.error_rate_band ?? "none",
  }
}

export interface IntegrationListResultWire extends PageWire<IntegrationListItemWire> {
  tiles: IntegrationHealthTilesWire
}

export interface IntegrationListResult extends Page<IntegrationListItem> {
  tiles: IntegrationHealthTiles
}

export function mapIntegrationListResult(wire: IntegrationListResultWire): IntegrationListResult {
  return {
    ...mapPage(wire, mapIntegrationListItem),
    tiles: mapHealthTiles(wire.tiles ?? ({} as IntegrationHealthTilesWire)),
  }
}

/** FR-S8-01 status-class chips. */
export type LogStatusClass = "all" | "success" | "client_error" | "server_error"

/** FR-S8-01 time select. The server defaults to `24h` when omitted. */
export type LogWindow = "last_hour" | "24h" | "7d" | "30d"

/** One parameter as logged — already PII-masked server-side (FR-S8-03). */
export interface LoggedParameterWire {
  api_field: string
  value?: string | null
  registered: boolean
}

export interface LoggedParameter {
  apiField: string
  /** Masked at display time by the server; the client never receives raw PII. */
  value: string | null
  /** False for an unregistered key–value pair (BR-14) — surfaced only here. */
  registered: boolean
}

export interface RequestLogParametersWire {
  rejected_before_parameter_parsing: boolean
  notice?: string | null
  items: LoggedParameterWire[]
}

export interface RequestLogParameters {
  /** AC-S8-03 — true when the request died before parameters were parsed (e.g. auth failure). */
  rejectedBeforeParameterParsing: boolean
  /** The server's notice copy, e.g. "— request rejected before parameter parsing". */
  notice: string | null
  items: LoggedParameter[]
}

export interface RequestLogWire {
  id: string
  integration_id?: string | null
  integration_name?: string | null
  timestamp: string
  method: string
  path: string
  scenario?: Scenario | string | number | null
  http_status: number
  result_code: string
  status_class?: string | null
  latency_ms: number
  credential_label?: string | null
  rejection_stage?: string | null
  parameters_received: RequestLogParametersWire
  response_returned?: unknown
}

export interface RequestLog {
  id: string
  integrationId: string | null
  integrationName: string | null
  timestamp: string
  method: string
  path: string
  scenario: Scenario | null
  httpStatus: number
  /** `E-1001`…`E-1500`, `202`, `200` — the wire code exactly as returned to the caller. */
  resultCode: string
  statusClass: string | null
  latencyMs: number
  credentialLabel: string | null
  rejectionStage: string | null
  parametersReceived: RequestLogParameters
  responseReturned: unknown
}

export function mapRequestLog(wire: RequestLogWire): RequestLog {
  const parameters = wire.parameters_received
  return {
    id: wire.id,
    integrationId: wire.integration_id ?? null,
    integrationName: wire.integration_name ?? null,
    timestamp: wire.timestamp,
    method: wire.method,
    path: wire.path,
    scenario: wire.scenario == null ? null : normalizeScenario(wire.scenario),
    httpStatus: wire.http_status,
    resultCode: wire.result_code,
    statusClass: wire.status_class ?? null,
    latencyMs: wire.latency_ms ?? 0,
    credentialLabel: wire.credential_label ?? null,
    rejectionStage: wire.rejection_stage ?? null,
    parametersReceived: {
      rejectedBeforeParameterParsing: parameters?.rejected_before_parameter_parsing ?? false,
      notice: parameters?.notice ?? null,
      items: (parameters?.items ?? []).map((row) => ({
        apiField: row.api_field,
        value: row.value ?? null,
        registered: row.registered,
      })),
    },
    responseReturned: wire.response_returned ?? null,
  }
}

/** Per-chip counts for the active window (FR-S8-01). */
export interface RequestLogCounts {
  all: number
  success: number
  clientError: number
  serverError: number
}

/** Echo of the filter the server actually applied, including the resolved window bounds. */
export interface RequestLogAppliedFilter {
  statusClass: string
  integrationId: string | null
  window: string
  windowStart: string
  windowEnd: string
}

export interface RequestLogListResultWire extends PageWire<RequestLogWire> {
  counts: {
    all: number
    success: number
    client_error: number
    server_error: number
  }
  applied_filter: {
    status_class: string
    integration_id?: string | null
    window: string
    window_start: string
    window_end: string
  }
}

export interface RequestLogListResult extends Page<RequestLog> {
  counts: RequestLogCounts
  appliedFilter: RequestLogAppliedFilter
}

export function mapRequestLogListResult(wire: RequestLogListResultWire): RequestLogListResult {
  return {
    ...mapPage(wire, mapRequestLog),
    counts: {
      all: wire.counts?.all ?? 0,
      success: wire.counts?.success ?? 0,
      clientError: wire.counts?.client_error ?? 0,
      serverError: wire.counts?.server_error ?? 0,
    },
    appliedFilter: {
      statusClass: wire.applied_filter?.status_class ?? "all",
      integrationId: wire.applied_filter?.integration_id ?? null,
      window: wire.applied_filter?.window ?? "24h",
      windowStart: wire.applied_filter?.window_start ?? "",
      windowEnd: wire.applied_filter?.window_end ?? "",
    },
  }
}
