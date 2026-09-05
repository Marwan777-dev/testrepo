import { createContext, useContext, useState, type ReactNode } from "react"

export interface Persona {
  id: string
  label: string
  labelAr: string
}

const PERSONAS: Persona[] = [
  { id: "cx_manager", label: "CX Program Manager", labelAr: "مدير تجربة العملاء" },
  { id: "analyst", label: "Analyst", labelAr: "محلل بيانات" },
  { id: "executive", label: "Executive", labelAr: "تنفيذي" },
  { id: "frontline", label: "Frontline Agent", labelAr: "وكيل خط المواجهة" },
  { id: "tenant_admin", label: "Tenant Admin", labelAr: "مدير النظام" },
]

interface PersonaState {
  persona: Persona
  personas: Persona[]
  setPersona: (p: Persona) => void
}

const PersonaContext = createContext<PersonaState | null>(null)

export function usePersona() {
  const ctx = useContext(PersonaContext)
  if (!ctx) throw new Error("usePersona must be used within PersonaProvider")
  return ctx
}

export function PersonaProvider({ children }: { children: ReactNode }) {
  const [persona, setPersona] = useState(PERSONAS[0])
  return (
    <PersonaContext.Provider value={{ persona, personas: PERSONAS, setPersona }}>
      {children}
    </PersonaContext.Provider>
  )
}
