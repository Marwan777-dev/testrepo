import { createContext, useContext, useState, type ReactNode } from "react"

interface User {
  name: string
  nameAr: string
  email: string
  initials: string
}

interface AuthState {
  isLoggedIn: boolean
  user: User | null
  login: () => void
  logout: () => void
}

const AuthContext = createContext<AuthState | null>(null)

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error("useAuth must be used within AuthProvider")
  return ctx
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [isLoggedIn, setIsLoggedIn] = useState(false)
  const [user, setUser] = useState<User | null>(null)

  const login = () => {
    setUser({ name: "Sarah Al-Omar", nameAr: "سارة الخيّر", email: "sarah@asfour.com", initials: "SO" })
    setIsLoggedIn(true)
  }

  const logout = () => {
    setUser(null)
    setIsLoggedIn(false)
  }

  return (
    <AuthContext.Provider value={{ isLoggedIn, user, login, logout }}>
      {children}
    </AuthContext.Provider>
  )
}
