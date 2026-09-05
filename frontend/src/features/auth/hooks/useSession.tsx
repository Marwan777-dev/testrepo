// Session context for M-10 auth. Hydrates the current session from the token in
// `sessionStorage` on mount, exposes the live `session` + permission snapshot,
// and centralizes the "401 → back to /login" reaction so every consumer reacts
// to an expired/invalid session the same way.

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react"
import { useNavigate } from "react-router"
import {
  AuthApiError,
  clearSessionToken,
  getSession,
  getSessionToken,
  logout as logoutApi,
} from "@/features/auth/api"
import type { SessionResponse } from "@/features/auth/api"

interface SessionState {
  /** Current session + permission snapshot, or null when signed out. */
  session: SessionResponse | null
  /** True while the initial hydration is in flight. */
  loading: boolean
  /** Revokes the session server-side (best-effort) and returns to /login. */
  signOut: () => Promise<void>
  /** Re-fetches the session to pick up a changed permission snapshot. */
  refreshPermissions: () => Promise<void>
}

const SessionContext = createContext<SessionState | null>(null)

export function useSession(): SessionState {
  const ctx = useContext(SessionContext)
  if (!ctx) throw new Error("useSession must be used within a SessionProvider")
  return ctx
}

export function SessionProvider({ children }: { children: ReactNode }) {
  const navigate = useNavigate()
  const [session, setSession] = useState<SessionResponse | null>(null)
  const [loading, setLoading] = useState(true)

  const clearAndRedirect = useCallback(() => {
    clearSessionToken()
    setSession(null)
    navigate("/login", { replace: true })
  }, [navigate])

  const refreshPermissions = useCallback(async () => {
    if (!getSessionToken()) {
      setSession(null)
      return
    }
    try {
      setSession(await getSession())
    } catch (err) {
      if (err instanceof AuthApiError && err.status === 401) {
        clearAndRedirect()
        return
      }
      throw err
    }
  }, [clearAndRedirect])

  const signOut = useCallback(async () => {
    try {
      await logoutApi()
    } catch {
      // Best-effort: clear local state and redirect even if the call fails.
    }
    clearAndRedirect()
  }, [clearAndRedirect])

  // Hydrate once on mount. A non-401 failure leaves us signed out; the error is
  // swallowed here (surfaced by explicit refreshPermissions calls instead).
  useEffect(() => {
    refreshPermissions()
      .catch(() => undefined)
      .finally(() => setLoading(false))
  }, [refreshPermissions])

  return (
    <SessionContext.Provider value={{ session, loading, signOut, refreshPermissions }}>
      {children}
    </SessionContext.Provider>
  )
}
