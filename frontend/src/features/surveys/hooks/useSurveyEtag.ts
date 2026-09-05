// Holds the current survey's ETag across the editing session (Q1 optimistic
// concurrency). Reads capture the ETag from the response; writes send it as `If-Match`
// and adopt the refreshed ETag the server returns — so consecutive saves keep working
// without re-fetching. A stale write surfaces as `ETagConflictError` (see api/etag.ts),
// which pages catch to open `EtagConflictDialog`.

import { useCallback, useRef, useState } from "react"

import type { EtagResult } from "../api/etag"

export interface SurveyEtagState {
  /** The last ETag seen for this survey, or null before the first read. Reactive. */
  etag: string | null
  /**
   * Runs a read fetch and captures the ETag it returns.
   * Usage: `const survey = await captureFrom(() => getSurvey(id))`.
   */
  captureFrom: <T>(read: () => Promise<EtagResult<T>>) => Promise<T>
  /**
   * Runs a mutating fetch with the current ETag as `If-Match`, then adopts the refreshed
   * ETag from the response.
   * Usage: `await withIfMatch((ifMatch) => updateSurvey(id, body, ifMatch))`.
   */
  withIfMatch: <T>(mutate: (ifMatch: string | undefined) => Promise<EtagResult<T>>) => Promise<T>
  /** Overwrites the held ETag (e.g. after `EtagConflictDialog`'s "Reload latest"). */
  setEtag: (value: string | null) => void
  /** Clears the held ETag (e.g. when the page switches to a different survey). */
  reset: () => void
}

export function useSurveyEtag(): SurveyEtagState {
  // Ref carries the authoritative value so async chains never read a stale render;
  // state mirrors it so consumers re-render (e.g. to enable the Save button).
  const etagRef = useRef<string | null>(null)
  const [etag, setEtagState] = useState<string | null>(null)

  const setEtag = useCallback((value: string | null) => {
    etagRef.current = value
    setEtagState(value)
  }, [])

  const captureFrom = useCallback(
    async <T,>(read: () => Promise<EtagResult<T>>): Promise<T> => {
      const { data, etag: next } = await read()
      if (next !== null) setEtag(next)
      return data
    },
    [setEtag]
  )

  const withIfMatch = useCallback(
    async <T,>(mutate: (ifMatch: string | undefined) => Promise<EtagResult<T>>): Promise<T> => {
      const { data, etag: next } = await mutate(etagRef.current ?? undefined)
      if (next !== null) setEtag(next)
      return data
    },
    [setEtag]
  )

  const reset = useCallback(() => setEtag(null), [setEtag])

  return { etag, captureFrom, withIfMatch, setEtag, reset }
}
