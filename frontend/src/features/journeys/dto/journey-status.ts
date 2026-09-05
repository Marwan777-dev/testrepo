/**
 * Journey lifecycle status (wire form of M-16 `JourneyStatus`). `Archived` is terminal —
 * the backend rejects any transition out of it with `journey.archived_terminal`.
 */
export type JourneyStatus = "Draft" | "Active" | "Inactive" | "Archived"
