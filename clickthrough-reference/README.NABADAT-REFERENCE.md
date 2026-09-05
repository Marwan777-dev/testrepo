# Click-through design reference (`ba-farah`)

This folder is a **static design prototype** copied from the click-through repo
<https://github.com/QBS-CODE/Nabadat-Click-through-.git>, branch **`ba-farah`**.

It is **not** wired to the backend and is **not** part of the real frontend build
(`frontend/`). It exists purely as the **visual reference** for frontend work —
when implementing a real page, open the matching design page here and match its
layout, spacing, components, and data-viz.

> Design **foundation** is already shared with the real app: the `ui/*` and `cx/*`
> components are identical, and `frontend/src/index.css` is a *refined* superset of
> this prototype's tokens (16px radius ceiling, navy-tinted background). So matching
> a page is about **layout & composition**, not re-importing tokens/components.

## How to view it

```sh
cd clickthrough-reference
npm install
npm run dev      # static prototype, mock data, no backend
```

## Design page → real app page map (spec 002 — Customer Journey / M-16)

| Design page (`clickthrough-reference/src/pages/`) | Real page (`frontend/src/features/journeys/pages/`) | Route (real) |
| --- | --- | --- |
| `JourneysPage.tsx` (+ `JourneyRow`, `JourneyFormDrawer`) | `JourneyListPage.tsx` | `/journeys` |
| `JourneyBuilderPage.tsx` (+ `components/builder/SwimLanes`, `TouchpointDrawer`, `TouchpointRow`) | `JourneyBuilderPage.tsx` | `/journeys/:id/builder` |
| `JourneyStatsPage.tsx` | _(no real counterpart yet — journey analytics)_ | — |
| `KpiConfigPage.tsx` / `KpiManagementPage.tsx` (+ `components/kpi/CxiWeightsTable`, `CxiSpiderPreview`, `KpiGauge`) | `KpiScoringPage.tsx` | `/journeys/:id/scoring` |
| `SettingsCustomerJourneyPage.tsx` | `DetectionRulesPage.tsx` (detection thresholds) | `/journeys/:id/detection` |

Other design pages on this branch (CX Dashboard, VOC, Surveys builder/library/stats,
Feedback, Settings) belong to other modules and have **no** 002 counterpart yet.

## Notable supporting files on `ba-farah`

- `src/data/` — `mockJourneys.ts`, `mock-kpis.ts`, `mock-surveys.ts`, `mock-settings.ts`
- `src/types/` — `journey.ts`, `kpi.ts`, `survey.ts`, `settings.ts`
- `src/components/journeys/SwimLanes.tsx` — the journey-builder swim-lane layout
- `src/components/kpi/CxiSpiderPreview.tsx` — the CXI spider/radar visualization
- `src/App.tsx` — the full route list for the prototype
