import { useState } from "react"
import { useTranslation } from "react-i18next"
import { cn } from "@/lib/utils"
import { usePersona } from "@/contexts/persona-context"
import { useDirection } from "@/hooks/use-direction"
import { useIsMobile } from "@/hooks/use-mobile"
import { ChevronUp, ChevronDown } from "lucide-react"

export function PersonaSwitcher() {
  const { t } = useTranslation()
  const { persona, personas, setPersona } = usePersona()
  const { isRtl } = useDirection()
  const isMobile = useIsMobile()
  const [expanded, setExpanded] = useState(false)

  // On mobile, position above the bottom nav (bottom-14); on desktop, at very bottom
  return (
    <div className={cn("fixed inset-x-0 z-[100]", isMobile ? "bottom-14" : "bottom-0")}>
      {/* Expanded panel — animated with grid rows trick */}
      <div
        className={cn(
          "grid transition-[grid-template-rows,opacity] duration-300 ease-[cubic-bezier(0.16,1,0.3,1)]",
          expanded ? "grid-rows-[1fr] opacity-100" : "grid-rows-[0fr] opacity-0"
        )}
      >
        <div className="overflow-hidden">
          <div className="bg-nb-navy border-t border-dashed border-nb-cyan/30 px-5 py-2.5 flex flex-wrap items-center gap-1.5">
            <span className="text-nb-cyan/60 text-[10px] font-bold uppercase tracking-widest me-2">
              {t("cx.protoTool")} — {t("cx.viewingAs")}:
            </span>
            {personas.map((p) => (
              <button
                key={p.id}
                onClick={() => {
                  setPersona(p)
                  setExpanded(false)
                }}
                className={cn(
                  "rounded-full px-3 py-1 text-xs transition-colors duration-150",
                  persona.id === p.id
                    ? "bg-nb-cyan text-white font-bold"
                    : "bg-white/10 text-white/60 hover:bg-white/15"
                )}
              >
                {isRtl ? p.labelAr : p.label}
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* Collapsed bar (always visible) */}
      <button
        onClick={() => setExpanded((prev) => !prev)}
        className="w-full bg-nb-navy text-white py-1.5 px-5 flex items-center justify-between cursor-pointer"
      >
        <div className="flex items-center gap-2 text-xs">
          <span className="text-nb-cyan text-[10px] font-bold uppercase tracking-widest">
            PROTO
          </span>
          <span className="text-white/30">|</span>
          <span className="text-white/70">{t("cx.viewingAs")}:</span>
          <span className="text-nb-cyan font-bold">
            {isRtl ? persona.labelAr : persona.label}
          </span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="text-nb-cyan/50 text-[10px]">
            {t("cx.switchRole")}
          </span>
          {expanded ? (
            <ChevronDown className="size-3 text-nb-cyan/50" />
          ) : (
            <ChevronUp className="size-3 text-nb-cyan/50" />
          )}
        </div>
      </button>
    </div>
  )
}
