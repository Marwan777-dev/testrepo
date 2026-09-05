// src/types/settings.ts

export type Industry =
  | "Banking"
  | "Telecommunications"
  | "Government"
  | "Automotive"
  | "Entertainment"
  | "Services"

export interface OrgConfig {
  name: string         // max 150 chars, required
  logoUrl: string | null
  industry: Industry
}

export interface ScoringConfig {
  alpha: number          // 0.000–1.000; β = 1 − α (never stored)
  motMultiplier: number  // 1.0–2.0
  nFloor: number         // integer ≥ 1
  flagPercentile: number // integer 1–49
  rollingWindowDays: number // integer ≥ 7
}
