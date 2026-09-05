// M-13 Integration Hub console API client — thin endpoint functions over the shared transport in
// `./http`. Mirrors the M-06 KPI client (`features/kpi-management/api.ts`): wire types + boundary
// mappers live in `./dto`, the fetch/error handling in `./http`, the typed errors in
// `./integration-hub-api-error` and `./mapping-import-error`; all of them are re-exported so a
// caller imports everything from "@/features/integration-hub/api".
//
// Paths follow contracts/api-endpoints.md's **console** API (`/api/v1/integration-hub/...`); the
// inbound scenario API (`/v1/survey-requests/{channelId}`, …) is a different envelope and is never
// called from this SPA.
//
// Scope note: this file is the module's single API client (T019 owns it — no later task adds a
// second). It therefore covers the whole console surface, including endpoints whose controllers
// land with their own user story (integrations US3, mappings US6/US7, request logs US5). Those
// functions are correct against the ratified contract and simply 404 until their story ships —
// which is preferable to a second client appearing later and drifting from this one.

import { callBlob, callJson, callUpload, toQuery } from "./http"
import { IntegrationHubApiError } from "./integration-hub-api-error"
import { MappingImportError } from "./mapping-import-error"
import {
  mapGeneratedCredential,
  mapImportRows,
  mapIntegration,
  mapIntegrationCreated,
  mapIntegrationListResult,
  mapMappingImportResult,
  mapPage,
  mapParameter,
  mapParameterListResult,
  mapParameterMapping,
  mapParameterMappingListResult,
  mapParameterPatchResult,
  mapRequestLog,
  mapRequestLogListResult,
  mapServiceChannel,
  mapUnmappedValueQueue,
  toIntegrationCreateWire,
  toIntegrationUpdateWire,
  toParameterMappingWire,
  toParameterWire,
  toServiceChannelWire,
  type CredentialInput,
  type DataType,
  type GeneratedCredential,
  type GeneratedCredentialWire,
  type Integration,
  type IntegrationCreateInput,
  type IntegrationCreated,
  type IntegrationCreatedWire,
  type IntegrationListResult,
  type IntegrationListResultWire,
  type IntegrationUpdateInput,
  type IntegrationWire,
  type LogStatusClass,
  type LogWindow,
  type MappingImportMode,
  type MappingImportRejectionWire,
  type MappingImportResult,
  type MappingImportResultWire,
  type Page,
  type PageWire,
  type Parameter,
  type ParameterListResult,
  type ParameterListResultWire,
  type ParameterMapping,
  type ParameterMappingListResult,
  type ParameterMappingListResultWire,
  type ParameterMappingSaveInput,
  type ParameterMappingWire,
  type ParameterOrigin,
  type ParameterPatchResult,
  type ParameterPatchResultWire,
  type ParameterSaveInput,
  type ParameterWire,
  type RequestLog,
  type RequestLogListResult,
  type RequestLogListResultWire,
  type RequestLogWire,
  type ServiceChannel,
  type ServiceChannelSaveInput,
  type ServiceChannelWire,
  type UnmappedValueQueue,
  type UnmappedValueQueueWire,
} from "./dto"

export type {
  ApiErrorEnvelope,
  ChannelParameterAssignmentInput,
  CredentialInput,
  CredentialMechanism,
  CredentialStatus,
  DataType,
  GeneratedCredential,
  Integration,
  IntegrationCreateInput,
  IntegrationCreated,
  IntegrationListItem,
  IntegrationListResult,
  IntegrationHealthTiles,
  RequestLogListResult,
  RequestLogCounts,
  RequestLogAppliedFilter,
  LoggedParameter,
  IntegrationUpdateInput,
  LogStatusClass,
  LogWindow,
  MappingImportMode,
  MappingImportResult,
  MappingImportRowError,
  MappingParameter,
  ParameterMappingListResult,
  UnmappedValueQueue,
  OAuthScope,
  Page,
  Parameter,
  ParameterMapping,
  ParameterMappingSaveInput,
  ParameterOrigin,
  ParameterPatchResult,
  ParameterReference,
  ParameterSaveInput,
  RequestLog,
  Scenario,
  ServiceChannel,
  ServiceChannelSaveInput,
  UnmappedValue,
} from "./dto"
export type {
  AcceptedParameter,
  ChannelContractRow,
  Credential,
  IntegrationEndpoint,
  IntegrationFieldKey,
  ParameterCounts,
  MappingFieldKey,
  ParameterFieldKey,
  ParameterListResult,
} from "./dto"
export {
  CHANNEL_ERROR_CODES,
  CHANNEL_ID_MAX_LENGTH,
  DATA_TYPES,
  INTEGRATION_ERROR_CODES,
  MAPPING_ERROR_CODES,
  PARAMETER_ERROR_CODES,
  SCENARIOS,
  SCOPE_BY_SCENARIO,
  channelFieldForCode,
  integrationFieldForCode,
  mappingSupportFor,
  mappingFieldForCode,
  parameterFieldForCode,
  sanitizeChannelId,
  suggestApiField,
} from "./dto"
export { IntegrationHubApiError } from "./integration-hub-api-error"
export { MappingImportError } from "./mapping-import-error"
export { MAPPING_IMPORT_ERROR_CODES } from "./dto"

// ===========================================================================
// Params interfaces
// ===========================================================================

export interface PageParams {
  /** Opaque cursor from a previous page's `nextCursor`. */
  cursor?: string
  /** Page size (the server clamps to 1…200 and falls back to its default outside that). */
  limit?: number
}

export interface ListServiceChannelsParams extends PageParams {
  active?: boolean
}

export interface ListParametersParams extends PageParams {
  origin?: ParameterOrigin
  type?: DataType
  q?: string
}

export interface ListIntegrationsParams extends PageParams {
  q?: string
  channel?: string
}

export interface ListRequestLogsParams extends PageParams {
  statusClass?: LogStatusClass
  integrationId?: string
  window?: LogWindow
}

// ===========================================================================
// Service channels (SCR-03/04)
// ===========================================================================

/** `GET /service-channels` — one cursor page of channels with SCR-03's counts (FR-S3-01). */
export async function listServiceChannels(
  params: ListServiceChannelsParams = {},
): Promise<Page<ServiceChannel>> {
  const query = toQuery({
    active: params.active,
    cursor: params.cursor,
    limit: params.limit,
  })
  const wire = await callJson<PageWire<ServiceChannelWire>>(`/service-channels${query}`)
  return mapPage(wire, mapServiceChannel)
}

/** `GET /service-channels/{id}` — one channel plus its full parameter contract (SCR-04 edit). */
export async function getServiceChannel(id: string): Promise<ServiceChannel> {
  return mapServiceChannel(await callJson<ServiceChannelWire>(`/service-channels/${id}`))
}

/** `POST /service-channels` — 201 on success; 409 on a duplicate name/ID (VR-F02/F04). */
export async function createServiceChannel(
  input: ServiceChannelSaveInput,
): Promise<ServiceChannel> {
  const wire = await callJson<ServiceChannelWire>("/service-channels", {
    method: "POST",
    body: toServiceChannelWire(input),
  })
  return mapServiceChannel(wire)
}

/**
 * `PUT /service-channels/{id}` — replaces the channel and its contract wholesale.
 * A post-lock `channel_id` change answers 409 `channel.id_locked` (BR-05), enforced server-side
 * regardless of what the client rendered.
 *
 * There is deliberately **no** `deleteServiceChannel` — BR-07/FR-S3-02: no DELETE route exists.
 */
export async function updateServiceChannel(
  id: string,
  input: ServiceChannelSaveInput,
): Promise<ServiceChannel> {
  const wire = await callJson<ServiceChannelWire>(`/service-channels/${id}`, {
    method: "PUT",
    body: toServiceChannelWire(input),
  })
  return mapServiceChannel(wire)
}

// ===========================================================================
// Parameters (SCR-05/06)
// ===========================================================================

/** `GET /parameters` — `origin`/`type`/`q` AND-combined (FR-S5-01). Counts stay global (AC-S5-01). */
export async function listParameters(
  params: ListParametersParams = {},
): Promise<ParameterListResult> {
  const query = toQuery({
    origin: params.origin,
    type: params.type,
    q: params.q,
    cursor: params.cursor,
    limit: params.limit,
  })
  return mapParameterListResult(await callJson<ParameterListResultWire>(`/parameters${query}`))
}

/** `GET /parameters/{id}`. */
export async function getParameter(id: string): Promise<Parameter> {
  return mapParameter(await callJson<ParameterWire>(`/parameters/${id}`))
}

/** `POST /parameters` — 201; 409 on a duplicate `api_field` incl. disabled/built-ins (VR-F06). */
export async function createParameter(input: ParameterSaveInput): Promise<Parameter> {
  const wire = await callJson<ParameterWire>("/parameters", {
    method: "POST",
    body: toParameterWire(input),
  })
  return mapParameter(wire)
}

/**
 * `PATCH /parameters/{id}` — partial update. Only the supplied fields are sent, so a patch never
 * silently resets a field the caller didn't touch. A built-in `data_type` change answers 409
 * `parameter.type_locked` (`[PO-G27]`).
 */
export async function updateParameter(
  id: string,
  input: Partial<ParameterSaveInput>,
): Promise<ParameterPatchResult> {
  const wire = await callJson<ParameterPatchResultWire>(`/parameters/${id}`, {
    method: "PATCH",
    body: toParameterPatchWire(input),
  })
  return mapParameterPatchResult(wire)
}

/**
 * `PATCH /parameters/{id}` with just `{ enabled }` — BR-10's two-step disable. Disabling a
 * referenced parameter comes back **200 with `requiresConfirmation`** and the reference list
 * (nothing was written); re-issue with `confirmDisable: true` to apply it.
 */
export async function setParameterEnabled(
  id: string,
  enabled: boolean,
  confirmDisable = false,
): Promise<ParameterPatchResult> {
  const wire = await callJson<ParameterPatchResultWire>(`/parameters/${id}`, {
    method: "PATCH",
    body: { enabled, confirm_disable: confirmDisable },
  })
  return mapParameterPatchResult(wire)
}

/**
 * Projects a partial save input onto the PATCH wire shape, omitting every field the caller left
 * `undefined`. `toParameterWire` is deliberately not reused here — it fills defaults for absent
 * fields, which is right for a create and wrong for a patch.
 */
function toParameterPatchWire(input: Partial<ParameterSaveInput>): Record<string, unknown> {
  const wire: Record<string, unknown> = {}
  if (input.nameEn !== undefined) wire.name_en = input.nameEn
  if (input.nameAr !== undefined) wire.name_ar = input.nameAr
  if (input.apiField !== undefined) wire.api_field = input.apiField
  if (input.dataType !== undefined) wire.data_type = input.dataType
  if (input.rangeMin !== undefined) wire.range_min = input.rangeMin
  if (input.rangeMax !== undefined) wire.range_max = input.rangeMax
  if (input.rangeUnit !== undefined) wire.range_unit = input.rangeUnit
  if (input.validationRule !== undefined) wire.validation_rule = input.validationRule
  if (input.enabled !== undefined) wire.enabled = input.enabled
  if (input.requiredByDefault !== undefined) wire.required_by_default = input.requiredByDefault
  if (input.filterable !== undefined) wire.filterable = input.filterable
  if (input.reportingVisibility !== undefined) wire.reporting_visibility = input.reportingVisibility
  if (input.dashboardVisibility !== undefined) wire.dashboard_visibility = input.dashboardVisibility
  if (input.mappingSupport !== undefined) wire.mapping_support = input.mappingSupport
  if (input.channelIds !== undefined) wire.channel_ids = input.channelIds
  return wire
}

// ===========================================================================
// Integrations + credentials (SCR-01/02)
// ===========================================================================

/** `GET /integrations` — SCR-01 stat tiles + rows; `q` AND `channel` (FR-S1-02). Tiles are global. */
export async function listIntegrations(
  params: ListIntegrationsParams = {},
): Promise<IntegrationListResult> {
  const query = toQuery({
    q: params.q,
    channel: params.channel,
    cursor: params.cursor,
    limit: params.limit,
  })
  return mapIntegrationListResult(await callJson<IntegrationListResultWire>(`/integrations${query}`))
}

/** `GET /integrations/{id}` — the SCR-02 detail (endpoint, credential, accepted parameters). */
export async function getIntegration(id: string): Promise<Integration> {
  return mapIntegration(await callJson<IntegrationWire>(`/integrations/${id}`))
}

/** `POST /integrations` — 201 with the provisioned endpoint + show-once plaintext secret. */
export async function createIntegration(
  input: IntegrationCreateInput,
): Promise<IntegrationCreated> {
  const wire = await callJson<IntegrationCreatedWire>("/integrations", {
    method: "POST",
    body: toIntegrationCreateWire(input),
  })
  return mapIntegrationCreated(wire)
}

/** `PUT /integrations/{id}` — `scenario` is immutable (BR-02) → 409 `integration.scenario_immutable`. */
export async function updateIntegration(
  id: string,
  input: IntegrationUpdateInput,
): Promise<Integration> {
  const wire = await callJson<IntegrationWire>(`/integrations/${id}`, {
    method: "PUT",
    body: toIntegrationUpdateWire(input),
  })
  return mapIntegration(wire)
}

/** `PATCH /integrations/{id}` `{ active }` — the Active ⇄ Inactive toggle (US10). No DELETE exists. */
export async function setIntegrationActive(id: string, active: boolean): Promise<Integration> {
  const wire = await callJson<IntegrationWire>(`/integrations/${id}`, {
    method: "PATCH",
    body: { active },
  })
  return mapIntegration(wire)
}

/**
 * `POST /integrations/{id}/credentials` — generates a credential, implicitly revoking the current
 * Active one (BR-16). The plaintext secret is returned **once** and never again.
 */
export async function generateCredential(
  integrationId: string,
  input: CredentialInput,
): Promise<GeneratedCredential> {
  const wire = await callJson<GeneratedCredentialWire>(
    `/integrations/${integrationId}/credentials`,
    { method: "POST", body: toCredentialRequestWire(input) },
  )
  return mapGeneratedCredential(wire)
}

/** `POST /integrations/{id}/credentials/revoke` — 409 `credential.already_revoked` if not Active. */
export async function revokeCredential(integrationId: string): Promise<void> {
  await callJson<void>(`/integrations/${integrationId}/credentials/revoke`, { method: "POST" })
}

/** The credential half of a create/generate request body (BR-16/BR-17). */
function toCredentialRequestWire(credential: CredentialInput): Record<string, unknown> {
  return credential.mechanism === "api_key"
    ? { mechanism: "api_key", key_label: credential.keyLabel }
    : {
        mechanism: "oauth_client",
        client_name: credential.clientName,
        scopes: credential.scopes,
      }
}

// ===========================================================================
// Parameter mappings (SCR-07)
// ===========================================================================

/** `GET /parameters/{id}/mappings` — 400 when the parameter is not mapping-enabled. */
export async function listMappings(
  parameterId: string,
  params: PageParams = {},
): Promise<ParameterMappingListResult> {
  const query = toQuery({ cursor: params.cursor, limit: params.limit })
  const wire = await callJson<ParameterMappingListResultWire>(
    `/parameters/${parameterId}/mappings${query}`,
  )
  return mapParameterMappingListResult(wire)
}

/** `GET /parameters/{id}/mappings/unmapped-queue` — the trailing-7-day queue (FR-S7-02). */
export async function listUnmappedValues(parameterId: string): Promise<UnmappedValueQueue> {
  const wire = await callJson<UnmappedValueQueueWire>(
    `/parameters/${parameterId}/mappings/unmapped-queue`,
  )
  return mapUnmappedValueQueue(wire)
}

/** `POST /parameters/{id}/mappings` — 409 `validation.duplicate_source_value` (VR-F08). */
export async function createMapping(
  parameterId: string,
  input: ParameterMappingSaveInput,
): Promise<ParameterMapping> {
  const wire = await callJson<ParameterMappingWire>(`/parameters/${parameterId}/mappings`, {
    method: "POST",
    body: toParameterMappingWire(input),
  })
  return mapParameterMapping(wire)
}

/** `PUT /parameters/{id}/mappings/{mappingId}`. Read-time effective — no version history (BR-13). */
export async function updateMapping(
  parameterId: string,
  mappingId: string,
  input: ParameterMappingSaveInput,
): Promise<ParameterMapping> {
  const wire = await callJson<ParameterMappingWire>(
    `/parameters/${parameterId}/mappings/${mappingId}`,
    { method: "PUT", body: toParameterMappingWire(input) },
  )
  return mapParameterMapping(wire)
}

/** `DELETE /parameters/{id}/mappings/{mappingId}` — immediate, read-time-effective (BR-13). */
export async function deleteMapping(parameterId: string, mappingId: string): Promise<void> {
  await callJson<void>(`/parameters/${parameterId}/mappings/${mappingId}`, { method: "DELETE" })
}

/** `GET /parameters/{id}/mappings/export` — the 3-column file (FR-S7-05). */
export async function exportMappings(
  parameterId: string,
): Promise<{ blob: Blob; filename: string }> {
  return callBlob(`/parameters/${parameterId}/mappings/export`, "mappings.xlsx")
}

/**
 * `POST /parameters/{id}/mappings/import` (multipart) — strictly all-or-nothing (VR-F09).
 *
 * A rejection answers with the import endpoint's **own** shape (`{ error, rows: [...] }`), not the
 * API-05 envelope, so it is re-thrown as `MappingImportError` carrying the row-level report; the
 * generic `IntegrationHubApiError` would drop the one thing the user needs to fix their file.
 */
export async function importMappings(
  parameterId: string,
  file: File,
  mode: MappingImportMode,
): Promise<MappingImportResult> {
  const form = new FormData()
  form.set("mode", mode)
  form.set("file", file)

  try {
    const wire = await callUpload<MappingImportResultWire>(
      `/parameters/${parameterId}/mappings/import`,
      form,
    )
    return mapMappingImportResult(wire)
  } catch (error) {
    throw toImportError(error)
  }
}

/** Replace-all import — the same endpoint at `mode: "replace_all"` (`m13.mapping.replace`). */
export async function replaceAllMappings(
  parameterId: string,
  file: File,
): Promise<MappingImportResult> {
  return importMappings(parameterId, file, "replace_all")
}

/** Re-shapes a failed import response into `MappingImportError`, preserving the row report. */
function toImportError(error: unknown): unknown {
  if (!(error instanceof IntegrationHubApiError)) return error
  const rejection = { rows: error.details ?? [] } as unknown as MappingImportRejectionWire
  return new MappingImportError(error.status, error.code, error.message, mapImportRows(rejection))
}

// ===========================================================================
// Request logs (SCR-08)
// ===========================================================================

/**
 * `GET /request-logs` — `status_class` / `integration_id` / `window` AND-combined (FR-S8-01),
 * newest-first, PII masked server-side (FR-S8-03). `statusClass: "all"` is the absence of the
 * filter, so it is not sent.
 */
export async function listRequestLogs(
  params: ListRequestLogsParams = {},
): Promise<RequestLogListResult> {
  const wire = await callJson<RequestLogListResultWire>(`/request-logs${logQuery(params)}`)
  return mapRequestLogListResult(wire)
}

/** `GET /request-logs/{id}` — one expanded row. */
export async function getRequestLog(id: string): Promise<RequestLog> {
  return mapRequestLog(await callJson<RequestLogWire>(`/request-logs/${id}`))
}

/** `GET /request-logs/export` — the **current filtered view**, masked identically (FR-S8-04). */
export async function exportRequestLogs(
  params: ListRequestLogsParams = {},
): Promise<{ blob: Blob; filename: string }> {
  return callBlob(`/request-logs/export${logQuery(params)}`, "request-logs.csv")
}

function logQuery(params: ListRequestLogsParams): string {
  return toQuery({
    status_class: params.statusClass === "all" ? undefined : params.statusClass,
    integration_id: params.integrationId,
    window: params.window,
    cursor: params.cursor,
    limit: params.limit,
  })
}
