# Product

## Register

product

## Users

Business analysts, customer success managers, quality teams, and executives in
banking, telecom, and government sectors. Desktop-first, mixed technical
proficiency: from data-savvy analysts running daily reports to executives scanning
weekly summaries. Arabic-speaking primary audience (Saudi Arabia, Gulf region)
with English as secondary language. They make consequential business decisions
based on the data this product surfaces.

## Product Purpose

Nabadat is a Voice of Customer (VOC) SaaS platform for enterprise and government.
It aggregates survey data, customer feedback, and operational KPIs into actionable
dashboards. Success means: analysts find problems faster, managers close the loop
on complaints sooner, and executives trust the numbers enough to act on them
without asking for a second source.

## Brand Personality

**Precise, Human, Action-Oriented, Credible**

- Precise: numbers, percentages, and trends stated clearly. No hedging or vague
  positives. Data speaks first.
- Human: behind every score is a person. Surfaces feelings, not just figures.
  Warm even when the data is hard.
- Action-Oriented: every insight surfaces a next step. Recommends, doesn't just
  report. Passive voice is avoided.
- Credible: no jargon, no buzzwords. Government and banking clients demand
  institutional confidence. Substance over style.

Arabic copy must be written natively in Modern Standard Arabic. Never translated
from English. Formal register for government and banking contexts.

## Anti-references

- Generic Bootstrap dashboards with default blue-grey color schemes
- Overly colorful, busy interfaces that compete with the data
- "AI slop" aesthetics: neon accents on dark backgrounds, gradient text, hero
  metrics with big numbers and tiny labels, identical card grids
- Clinical, cold enterprise tools that feel hostile to non-technical users
- SaaS landing page cliches: glassmorphism cards, bouncy animations, em dashes
- Any interface where decoration competes with the data for attention

## Design Principles

1. **Data first**: content and metrics are the hero. Chrome and decoration are
   secondary. Every element must earn its place.
2. **Precision builds trust**: government officials and banking executives need
   to trust the numbers. Consistent formatting, clear labels, and honest
   representation of data (even when the story is bad).
3. **Always recommend a next step**: the platform doesn't just report. Insights
   lead to actions. Passive displays become active prompts.
4. **Bilingual by design**: Arabic and English must both feel native, not adapted.
   RTL-first architecture with logical properties throughout.
5. **Accessible by default**: WCAG AA minimum. Color is never the only indicator.
   Focus rings always visible. Screen readers supported.

## Accessibility & Inclusion

- WCAG AA compliance minimum across both themes (light and dark)
- Color never used as the sole indicator: always paired with icons, text, or
  patterns (especially critical for the D1-D5 semantic KPI scale)
- `prefers-reduced-motion` respected on all animations
- Arabic text considerations: minimum text-sm (14px) for body, leading-relaxed
  for paragraph text, no text-justify, no break-all
- Sequential heading hierarchy enforced (h1, h2, h3, no skipping)
- All icon-only buttons require aria-label
- Form errors use role="alert" with cause + fix messaging
- Skip-to-content link for keyboard navigation
