// Persona Baseline management page (T089), route /settings/persona-baselines. Lists
// all 8 personas in an accordion; each panel edits that persona's default module
// grants. Shows an isCustomised badge. Non-P-01/P-07 actors get an access-denied
// state. RTL-first.

import { useCallback, useEffect, useState } from "react"
import { useTranslation } from "react-i18next"
import { ShieldAlert } from "lucide-react"

import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from "@/components/ui/accordion"
import { Badge } from "@/components/ui/badge"
import { Skeleton } from "@/components/ui/skeleton"
import { useSession } from "@/features/auth/hooks/useSession"
import { listPersonaBaselines, type PersonaBaseline } from "@/features/persona-baselines/api"
import { PersonaBaselineEditor } from "@/features/persona-baselines/components/PersonaBaselineEditor"

const MANAGER_PERSONAS = new Set(["P-01", "P-07"])

export default function PersonaBaselinePage() {
  const { t } = useTranslation()
  const { session } = useSession()
  const persona = session?.persona ?? ""
  const allowed = MANAGER_PERSONAS.has(persona)
  const canEditCx = persona === "P-01"

  const [baselines, setBaselines] = useState<PersonaBaseline[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(false)

  const load = useCallback(async () => {
    setLoading(true)
    setError(false)
    try {
      const result = await listPersonaBaselines()
      setBaselines(result.items)
    } catch {
      setError(true)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (allowed) void load()
    else setLoading(false)
  }, [allowed, load])

  if (!allowed) {
    return (
      <div className="space-y-5 py-5">
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <ShieldAlert className="size-12 text-muted-foreground mb-4" />
          <h1 className="text-lg font-bold mb-2">{t("personaBaselines.accessDeniedTitle")}</h1>
          <p className="text-muted-foreground max-w-sm">{t("personaBaselines.accessDeniedDesc")}</p>
        </div>
      </div>
    )
  }

  return (
    <div className="space-y-5 py-5">
      <div>
        <h1 className="text-2xl font-heading font-bold">{t("personaBaselines.title")}</h1>
        <p className="text-sm text-muted-foreground mt-1">{t("personaBaselines.subtitle")}</p>
      </div>

      {error && (
        <div
          role="alert"
          className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {t("personaBaselines.loadError")}
        </div>
      )}

      {loading ? (
        <div className="space-y-2">
          {Array.from({ length: 8 }).map((_, i) => (
            <Skeleton key={i} className="h-12 w-full rounded-md" />
          ))}
        </div>
      ) : (
        <div className="rounded-lg border border-border bg-card px-4 shadow-sm dark:shadow-none">
          <Accordion>
            {baselines.map((baseline) => (
              <AccordionItem key={baseline.personaId} value={baseline.personaId}>
                <AccordionTrigger>
                  <span className="flex items-center gap-3">
                    <span className="font-bold tabular-nums">{baseline.personaId}</span>
                    {baseline.isCustomised ? (
                      <Badge className="bg-nb-cyan-100 text-nb-cyan-800 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200">
                        {t("personaBaselines.customised")}
                      </Badge>
                    ) : (
                      <Badge variant="outline">{t("personaBaselines.default")}</Badge>
                    )}
                  </span>
                </AccordionTrigger>
                <AccordionContent>
                  <PersonaBaselineEditor baseline={baseline} canEditCx={canEditCx} onSaved={() => void load()} />
                </AccordionContent>
              </AccordionItem>
            ))}
          </Accordion>
        </div>
      )}
    </div>
  )
}
