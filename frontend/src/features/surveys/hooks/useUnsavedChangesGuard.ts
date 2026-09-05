// Unsaved-changes guard (NFR-5, Q1: explicit Save — a dirty form is real unsaved work).
// Reused by every M-01 form page (Settings, Appearance, Builder, Translate).
//
// Scope note: this app mounts a declarative <BrowserRouter>, and react-router's
// `useBlocker` only exists on data routers — so in-app <Link>/sidebar navigation cannot
// be intercepted centrally here (tracked as TODO-M01-027). What this hook guarantees:
//
// 1. Browser-level `beforeunload` (tab close / refresh / external navigation) prompts
//    the native "leave site?" dialog while `isDirty` is true.
// 2. `confirmIfDirty()` for the page's own programmatic navigations (Cancel/Back
//    buttons, post-action redirects): returns true when it is safe to navigate.

import { useCallback, useEffect, useRef } from "react"

export interface UnsavedChangesGuard {
  /**
   * Returns true when navigation may proceed — either the form is clean, or the user
   * confirmed discarding their changes. Call before `navigate(...)` in page handlers.
   */
  confirmIfDirty: (message?: string) => boolean
}

export function useUnsavedChangesGuard(isDirty: boolean): UnsavedChangesGuard {
  // Ref mirror so confirmIfDirty stays referentially stable across dirty toggles.
  const dirtyRef = useRef(isDirty)
  dirtyRef.current = isDirty

  useEffect(() => {
    if (!isDirty) return
    const handler = (event: BeforeUnloadEvent) => {
      // Browsers show their own generic message; both calls are needed cross-browser.
      event.preventDefault()
      event.returnValue = ""
    }
    window.addEventListener("beforeunload", handler)
    return () => window.removeEventListener("beforeunload", handler)
  }, [isDirty])

  const confirmIfDirty = useCallback((message?: string) => {
    if (!dirtyRef.current) return true
    // window.confirm is the deliberate fallback for programmatic exits; pages that need
    // the design-system dialog render their own <Dialog> and gate on `isDirty` directly.
    return window.confirm(message ?? "You have unsaved changes. Leave without saving?")
  }, [])

  return { confirmIfDirty }
}
