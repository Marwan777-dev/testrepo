// Wire types + domain types + mappers for the Platform Settings API (Organization section, US-6).
// The snake_case wire shapes (contracts/settings-api.md) are mapped to camelCase domain shapes at
// the api.ts boundary (CLAUDE.md "Backend Integration"). `industry` and `industry_options` arrive
// as plain strings (the controller serialises the canonical member names), so no enum-int
// normalisation is needed here.

/** API-05 error envelope (shared shape). */
export interface ApiErrorEnvelope {
  error?: {
    code: string
    message: string
    request_id?: string
    tenant_id?: string
  }
}

// ── Wire shapes ──────────────────────────────────────────────────────────────

export interface OrganizationLogoWire {
  url: string
  content_type: string
  size_bytes: number
}

export interface OrganizationResponseWire {
  name: string
  logo: OrganizationLogoWire | null
  industry: string
  industry_options: string[]
  audit: { updated_at: string; updated_by: string } | null
}

export interface LogoUploadResponseWire {
  url: string
  content_type: string
  size_bytes: number
  was_sanitised: boolean
}

// ── Domain shapes ────────────────────────────────────────────────────────────

export interface OrganizationLogo {
  url: string
  contentType: string
  sizeBytes: number
}

export interface OrganizationSettings {
  name: string
  logo: OrganizationLogo | null
  industry: string
  industryOptions: string[]
  updatedAt: string | null
  updatedBy: string | null
}

export interface LogoUploadResult {
  url: string
  contentType: string
  sizeBytes: number
  wasSanitised: boolean
}

// ── Mappers ──────────────────────────────────────────────────────────────────

export function mapOrganization(wire: OrganizationResponseWire): OrganizationSettings {
  return {
    name: wire.name,
    logo: wire.logo
      ? { url: wire.logo.url, contentType: wire.logo.content_type, sizeBytes: wire.logo.size_bytes }
      : null,
    industry: wire.industry,
    industryOptions: wire.industry_options ?? [],
    updatedAt: wire.audit?.updated_at ?? null,
    updatedBy: wire.audit?.updated_by ?? null,
  }
}

export function mapLogoUpload(wire: LogoUploadResponseWire): LogoUploadResult {
  return {
    url: wire.url,
    contentType: wire.content_type,
    sizeBytes: wire.size_bytes,
    wasSanitised: wire.was_sanitised,
  }
}

// ── Customer Journey ScoringConfig (US-4) ──────────────────────────────────────
// Tenant-level scoring parameters (one row per tenant). snake_case on the wire; β is derived
// server-side (1 − α) and read-only — never sent in the update body.

export interface ScoringConfigResponseWire {
  alpha: number
  beta: number
  mot_multiplier: number
  n_floor: number
  flag_percentile: number
  rolling_window_days: number
  audit: { updated_at: string; updated_by: string } | null
}

export interface ScoringConfig {
  alpha: number
  beta: number
  motMultiplier: number
  nFloor: number
  flagPercentile: number
  rollingWindowDays: number
  updatedAt: string | null
  updatedBy: string | null
}

/** The five editable parameters (camelCase). β is excluded — it is derived server-side. */
export interface ScoringConfigInput {
  alpha: number
  motMultiplier: number
  nFloor: number
  flagPercentile: number
  rollingWindowDays: number
}

export function mapScoringConfig(wire: ScoringConfigResponseWire): ScoringConfig {
  return {
    alpha: wire.alpha,
    beta: wire.beta,
    motMultiplier: wire.mot_multiplier,
    nFloor: wire.n_floor,
    flagPercentile: wire.flag_percentile,
    rollingWindowDays: wire.rolling_window_days,
    updatedAt: wire.audit?.updated_at ?? null,
    updatedBy: wire.audit?.updated_by ?? null,
  }
}
