/**
 * Persona lifecycle status (wire form of M-16 `PersonaStatus`). Mirrors the journey lifecycle:
 * `Archived` is terminal — the backend rejects any transition out of it with
 * `persona.archived_terminal`. Only `Active` personas may be bound to journeys (FR-005).
 */
export type PersonaStatus = "Draft" | "Active" | "Inactive" | "Archived"
