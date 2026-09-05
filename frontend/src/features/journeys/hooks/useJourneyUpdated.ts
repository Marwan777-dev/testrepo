// T039 (US-1): concurrent-edit detection hook for the Journey Builder.
//
// Last-write-wins is the server's conflict policy (FR-018 / plan.md §4); this hook is the
// client-side awareness layer. It polls `GET /api/v1/journeys/{id}/updated-at` every 15 s and
// compares the returned timestamp against the `baselineUpdatedAt` captured when the journey was
// loaded. The first time the server timestamp diverges from the baseline it fires a single,
// non-blocking `sonner` toast and flips `changedExternally` so the page can also raise an inline
// banner. Saving is never blocked.
//
// The notification fires once per baseline: re-baselining (the page reloading to the latest
// version, which advances `baselineUpdatedAt`) re-arms it; `reset()` clears the banner without
// re-arming, so dismissing it doesn't immediately re-toast on the next tick.

import { useCallback, useEffect, useRef, useState } from "react"
import { useTranslation } from "react-i18next"
import { toast } from "sonner"

import { getJourneyUpdatedAt } from "@/features/journeys/api"

const POLL_INTERVAL_MS = 15_000

export interface UseJourneyUpdatedOptions {
  /** Journey under edit. Polling is inert until this and `baselineUpdatedAt` are set. */
  journeyId: string
  /**
   * The journey's `updatedAt` timestamp as last loaded by the page. The poll compares against
   * it; advancing it (a fresh load) re-arms the one-shot notification for the new baseline.
   */
  baselineUpdatedAt: string | null
  /** Pause polling when false (e.g. while the initial load is in flight). Defaults to true. */
  enabled?: boolean
}

export interface UseJourneyUpdatedResult {
  /** True once a newer `updatedAt` has been observed from the server for the current baseline. */
  changedExternally: boolean
  /** Hides the banner without re-arming — the toast/banner won't re-fire until the baseline advances. */
  reset: () => void
}

/**
 * Polls a journey's last-update timestamp and surfaces external edits via a non-blocking toast
 * plus a `changedExternally` flag for an inline banner.
 */
export function useJourneyUpdated({
  journeyId,
  baselineUpdatedAt,
  enabled = true,
}: UseJourneyUpdatedOptions): UseJourneyUpdatedResult {
  const { t } = useTranslation()
  const [changedExternally, setChangedExternally] = useState(false)

  // Whether the change for the *current* baseline has already been surfaced — guards against
  // re-toasting every 15 s while the journey stays stale.
  const handledRef = useRef(false)

  // A fresh baseline (the page reloaded to the latest version) re-arms the one-shot notification.
  useEffect(() => {
    handledRef.current = false
    setChangedExternally(false)
  }, [journeyId, baselineUpdatedAt])

  useEffect(() => {
    if (!enabled || !journeyId || !baselineUpdatedAt) return

    const interval = setInterval(() => {
      void (async () => {
        try {
          const res = await getJourneyUpdatedAt(journeyId)
          if (res.updatedAt !== baselineUpdatedAt && !handledRef.current) {
            handledRef.current = true
            setChangedExternally(true)
            toast.warning(t("journey.concurrentEditTitle"), {
              description: t("journey.concurrentEditBody"),
            })
          }
        } catch {
          // Transient poll failures are non-fatal — retry on the next tick.
        }
      })()
    }, POLL_INTERVAL_MS)

    return () => clearInterval(interval)
  }, [enabled, journeyId, baselineUpdatedAt, t])

  const reset = useCallback(() => setChangedExternally(false), [])

  return { changedExternally, reset }
}
