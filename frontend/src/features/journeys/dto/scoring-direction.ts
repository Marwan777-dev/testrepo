/**
 * Direction in which a KPI's raw value improves. `Ascending` → higher is better (NPS, CSAT);
 * `Descending` → lower is better (CES). M-06 owns scoring; M-16 only surfaces the direction so
 * the UI can label KPIs. Resolved server-side (platform-standard types derive it — CES is the
 * only `Descending` built-in; tenant-defined read `kpi_type_definitions.scoring_direction`).
 */
export type ScoringDirection = "Ascending" | "Descending"
