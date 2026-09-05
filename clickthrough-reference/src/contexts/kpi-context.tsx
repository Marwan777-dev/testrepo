// src/contexts/kpi-context.tsx
import { createContext, useContext, useState, useMemo } from "react";
import { INITIAL_KPIS } from "@/data/mock-kpis";
import type { KpiDefinition } from "@/types/kpi";

interface KpiContextValue {
  kpis: KpiDefinition[];
  activeCount: number;
  saveKpi: (updated: KpiDefinition) => void;
}
//test
const KpiContext = createContext<KpiContextValue | null>(null);

export function KpiProvider({ children }: { children: React.ReactNode }) {
  const [kpis, setKpis] = useState<KpiDefinition[]>(INITIAL_KPIS);

  const activeCount = useMemo(
    () => kpis.filter((k) => k.isActive).length,
    [kpis],
  );

  function saveKpi(updated: KpiDefinition) {
    setKpis((prev) => {
      const exists = prev.some((k) => k.id === updated.id);
      if (exists) return prev.map((k) => (k.id === updated.id ? updated : k));
      return [...prev, { ...updated, createdAt: new Date().toISOString() }];
    });
  }

  return (
    <KpiContext.Provider value={{ kpis, activeCount, saveKpi }}>
      {children}
    </KpiContext.Provider>
  );
}

export function useKpis() {
  const ctx = useContext(KpiContext);
  if (!ctx) throw new Error("useKpis must be used within KpiProvider");
  return ctx;
}
