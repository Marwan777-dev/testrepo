/**
 * Tenant theme contract.
 *
 * A tenant's brand is fully described by a handful of seed colors. Three are
 * REQUIRED (the real independent inputs); three are OPTIONAL refinements that
 * default to derived values. Everything else in the design system — the 7-step
 * `nb-*` ramps, every `*-foreground`, `ring`, `chart-*`, all neutral surfaces,
 * and the dark-mode variants — is generated from these by `deriveThemeVars`.
 *
 * NEVER part of a tenant theme: the D1–D5 KPI scale and `destructive`. Those are
 * semantic constants that must mean the same thing for every tenant (and stay
 * consistent with QBS), so they live permanently in `index.css` and are never
 * touched here. See CLAUDE.md → Color System → Multi-Tenant Theming.
 */

export interface TenantThemeSeed {
  /** REQUIRED — brand primary. CTAs, active nav, headline KPI values, ring, chart-1. (Nabadat: #0D8BBC) */
  primary: string
  /** REQUIRED — brand secondary/accent. Secondary accents, chart-2. (Nabadat: #13DB9B) */
  secondary: string
  /**
   * REQUIRED — neutral/navy anchor. Drives text (`foreground`), sidebar, borders,
   * muted surfaces, and ALL dark-mode surfaces. Pick a deep, low-chroma color.
   * (Nabadat: #1E2235)
   */
  neutral: string

  /** OPTIONAL — sidebar background. Defaults to `neutral`. Set when the chrome should differ from body text. */
  sidebar?: string
  /** OPTIONAL — feature-highlight surface. Defaults to a derived light tint of `neutral`. */
  accent?: string
  /** OPTIONAL — light-mode page background tint. Defaults to a near-white tint of `neutral`. */
  background?: string
}

/** A seed with every optional field resolved. Produced by `resolveSeed`. */
export type ResolvedSeed = Required<TenantThemeSeed>

/**
 * The pinned Nabadat brand. This mirrors the exact values already in `index.css`
 * for the REQUIRED seeds. The default tenant continues to render from `index.css`
 * verbatim — this constant exists for parity checks and as the fallback seed.
 */
export const NABADAT_SEED: TenantThemeSeed = {
  primary: "#0D8BBC",
  secondary: "#13DB9B",
  neutral: "#1E2235",
  sidebar: "#1E2235",
  accent: "#EEF1F7",
  background: "#F7F9FC",
}

/** Global semantic constants — identical for every tenant, never themed. Kept here for reference; the live values live in `index.css`. */
export const FIXED_TOKENS = {
  /** `destructive` === D5. */
  destructive: { light: "#C01B2A", dark: "#FFD6DA" },
} as const

/** Fill optional seed fields with their derived-or-pinned defaults. */
export function resolveSeed(seed: TenantThemeSeed): ResolvedSeed {
  return {
    primary: seed.primary,
    secondary: seed.secondary,
    neutral: seed.neutral,
    sidebar: seed.sidebar ?? seed.neutral,
    accent: seed.accent ?? "", // "" → derive in deriveThemeVars
    background: seed.background ?? "", // "" → derive in deriveThemeVars
  }
}

/* ----------------------------------------------------------------------------
 * Suggested DB schema (control-plane). Colors change rarely, so precompute the
 * derived CSS on write (run buildThemeCss once when an admin saves) and store it
 * alongside the seeds — runtime just injects the string, no color math per request.
 *
 *   CREATE TABLE tenant_theme (
 *     tenant_id    uuid PRIMARY KEY REFERENCES tenant(id),
 *     primary_hex     varchar(7)  NOT NULL,   -- #RRGGBB, required
 *     secondary_hex   varchar(7)  NOT NULL,
 *     neutral_hex     varchar(7)  NOT NULL,
 *     sidebar_hex     varchar(7)  NULL,        -- optional, NULL → derive
 *     accent_hex      varchar(7)  NULL,
 *     background_hex  varchar(7)  NULL,
 *     derived_css     text        NOT NULL,    -- buildThemeCss() output, recomputed on save
 *     updated_at      timestamptz NOT NULL DEFAULT now()
 *   );
 *
 * API: GET /api/tenants/{id}/theme  ->  { primary, secondary, neutral, sidebar?, accent?, background? }
 *      (or serve derived_css directly at GET /tenants/{id}/theme.css)
 * -------------------------------------------------------------------------- */
