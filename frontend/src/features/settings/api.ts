// Platform Settings API client — thin endpoint functions over the shared transport (./http).
//
// Organization section (US-6): read the current settings, update Name + Industry, and upload a logo
// (multipart). Wire types + mappers live in ./dto, the error in ./settings-api-error; they are
// re-exported so callers import everything from "@/features/settings/api". Industry fields arrive
// as plain strings (the controller serialises member names), so there is no enum-int normalisation.
// The Customer Journey ScoringConfig surface (US-4) lands here later.

import { callJson, callMultipart } from "./http"
import {
  mapLogoUpload,
  mapOrganization,
  mapScoringConfig,
  type LogoUploadResponseWire,
  type LogoUploadResult,
  type OrganizationResponseWire,
  type OrganizationSettings,
  type ScoringConfig,
  type ScoringConfigInput,
  type ScoringConfigResponseWire,
} from "./dto"

export type {
  ApiErrorEnvelope,
  LogoUploadResult,
  OrganizationLogo,
  OrganizationSettings,
  ScoringConfig,
  ScoringConfigInput,
} from "./dto"
export { SettingsApiError } from "./settings-api-error"

/** Reads the tenant's Organization settings + canonical industry options. `GET /api/v1/tenant/organization`. */
export async function getOrganization(): Promise<OrganizationSettings> {
  const res = await callJson<OrganizationResponseWire>("/tenant/organization")
  return mapOrganization(res)
}

/** Updates the tenant Name + Industry. `PUT /api/v1/tenant/organization`. Returns the saved settings. */
export async function updateOrganization(
  name: string,
  industry: string,
): Promise<OrganizationSettings> {
  const res = await callJson<OrganizationResponseWire>("/tenant/organization", {
    method: "PUT",
    body: { name, industry },
  })
  return mapOrganization(res)
}

/**
 * Uploads (or replaces) the tenant logo. `POST /api/v1/tenant/organization/logo` (multipart, field
 * `logo`). For SVG the server persists the SANITISED bytes and reports `wasSanitised` so the caller
 * can surface the non-blocking "sanitised" notice. Throws `SettingsApiError` on a rejected upload
 * (e.g. `LOGO_CONTENT_TYPE_UNSUPPORTED`, `LOGO_SVG_UNSAFE_CONTENT`).
 */
export async function uploadLogo(file: File): Promise<LogoUploadResult> {
  const form = new FormData()
  form.append("logo", file, file.name)
  const res = await callMultipart<LogoUploadResponseWire>("/tenant/organization/logo", form)
  return mapLogoUpload(res)
}

// ── Customer Journey ScoringConfig (US-4) ──────────────────────────────────────

/** Reads the tenant's ScoringConfig (β derived). `GET /api/v1/tenant/scoring-config`. */
export async function getScoringConfig(): Promise<ScoringConfig> {
  const res = await callJson<ScoringConfigResponseWire>("/tenant/scoring-config")
  return mapScoringConfig(res)
}

/**
 * Updates the tenant ScoringConfig (P-01 only). `PUT /api/v1/tenant/scoring-config`. β is NOT sent —
 * it is derived server-side. Throws `SettingsApiError` on a rejected save (e.g. `INVALID_ALPHA_BETA_SUM`,
 * `MOT_MULTIPLIER_OUT_OF_RANGE`).
 */
export async function updateScoringConfig(input: ScoringConfigInput): Promise<ScoringConfig> {
  const res = await callJson<ScoringConfigResponseWire>("/tenant/scoring-config", {
    method: "PUT",
    body: {
      alpha: input.alpha,
      mot_multiplier: input.motMultiplier,
      n_floor: input.nFloor,
      flag_percentile: input.flagPercentile,
      rolling_window_days: input.rollingWindowDays,
    },
  })
  return mapScoringConfig(res)
}
