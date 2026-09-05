// src/contexts/settings-context.tsx
import { createContext, useContext, useState } from "react"
import { INITIAL_ORG_CONFIG, INITIAL_SCORING_CONFIG } from "@/data/mock-settings"
import type { OrgConfig, ScoringConfig } from "@/types/settings"

interface SettingsContextValue {
  orgConfig: OrgConfig
  scoringConfig: ScoringConfig
  saveOrg: (updated: OrgConfig) => void
  saveScoring: (updated: ScoringConfig) => void
}

const SettingsContext = createContext<SettingsContextValue | null>(null)

export function SettingsProvider({ children }: { children: React.ReactNode }) {
  const [orgConfig, setOrgConfig] = useState<OrgConfig>(INITIAL_ORG_CONFIG)
  const [scoringConfig, setScoringConfig] = useState<ScoringConfig>(INITIAL_SCORING_CONFIG)

  function saveOrg(updated: OrgConfig) {
    setOrgConfig(updated)
  }

  function saveScoring(updated: ScoringConfig) {
    setScoringConfig(updated)
  }

  return (
    <SettingsContext.Provider value={{ orgConfig, scoringConfig, saveOrg, saveScoring }}>
      {children}
    </SettingsContext.Provider>
  )
}

export function useSettings() {
  const ctx = useContext(SettingsContext)
  if (!ctx) throw new Error("useSettings must be used within SettingsProvider")
  return ctx
}
