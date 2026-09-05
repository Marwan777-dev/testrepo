/**
 * Relative importance of a touchpoint within its stage. The backend stores this as a free
 * string and defaults to `Medium`; the union captures the four values the builder UI offers.
 */
export type TouchpointImportance = "Low" | "Medium" | "High" | "Critical"
