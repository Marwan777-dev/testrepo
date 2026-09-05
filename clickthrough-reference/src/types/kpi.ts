// src/types/kpi.ts

export type KpiType = "Standard" | "Custom"

export type KpiScale = "0–10" | "1–3" | "1–5" | "1–7" | "1–10" | "1–100"

export type CalculationMethod =
  | "WeightedAverage"
  | "TopNBox"
  | "NpsStandard"
  | "WeightedComposite"

export type RepresentationStyle = "Number" | "Stars" | "Emoji" | "Slider"

export type EmojiSet =
  | "FaceClassic"
  | "FaceExpressive"
  | "FaceBold"
  | "Thumbs"
  | "Hearts"
  | "Weather"
  | "TrafficLights"
  | "Shapes"

export interface KpiDefinition {
  id: string                        // lowercase unique key, e.g. "nps"
  shortName: string                 // display label, e.g. "NPS"
  fullName: string
  type: KpiType
  scale: KpiScale | null            // null for CXI only
  calculationMethod: CalculationMethod
  topNValue?: number                // only when calculationMethod = "TopNBox"
  representationStyle: RepresentationStyle
  emojiSet?: EmojiSet               // only when representationStyle = "Emoji"
  perspectives: string[]
  thresholdX: number                // default 20
  thresholdY: number                // default 70
  target: number
  isActive: boolean
  showOnDashboard: boolean
  cxiWeights?: Record<string, number>   // kpiId → relative weight; CXI only
  scaleMinLabel?: string            // label shown under the minimum scale point
  scaleMaxLabel?: string            // label shown under the maximum scale point
  createdAt?: string                // ISO string; custom KPIs only
}
