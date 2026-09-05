// Route guard for authenticated areas. While the session is hydrating it shows
// a neutral spinner; once resolved, an absent session sends the user to /login.
// Use it as a layout route (renders <Outlet/>) or as a wrapper around children.

import type { ReactNode } from "react"
import { Navigate, Outlet } from "react-router"
import { Loader2 } from "lucide-react"

import { useSession } from "@/features/auth/hooks/useSession"

export function AuthGuard({ children }: { children?: ReactNode }) {
  const { session, loading } = useSession()

  if (loading) {
    return (
      <div className="flex min-h-svh items-center justify-center bg-background">
        <Loader2 className="size-6 animate-spin text-muted-foreground" />
      </div>
    )
  }

  if (!session) return <Navigate to="/login" replace />

  return <>{children ?? <Outlet />}</>
}
