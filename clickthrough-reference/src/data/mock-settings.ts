// src/data/mock-settings.ts
import type { OrgConfig, ScoringConfig } from "@/types/settings"

export const INITIAL_ORG_CONFIG: OrgConfig = {
  name: "Nabadat Demo Tenant",
  logoUrl: null,
  industry: "Banking",
}

// n_floor default: 500 (resolved for this UI as high-volume tenant default;
// M-16 SRS specifies 5 — reconcile before production deployment, Q-S1)
export const INITIAL_SCORING_CONFIG: ScoringConfig = {
  alpha: 0.5,
  motMultiplier: 1.5,
  nFloor: 500,
  flagPercentile: 25,
  rollingWindowDays: 30,
}
