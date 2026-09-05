// BR-1.7 publish gate (T098): non-modal affordance on the Publish action. When the
// survey has no section or no question, the button renders disabled with a tooltip
// stating the requirement — matching the API's 409 `survey.publish.requires_content`.
// (A disabled element doesn't fire pointer events, so the tooltip anchors on a wrapper.)

import type { ReactNode } from "react"
import { useTranslation } from "react-i18next"

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip"

export function publishGateBlocked(sectionsCount: number, questionsCount: number): boolean {
  return sectionsCount === 0 || questionsCount === 0
}

/**
 * Wraps the Publish button. When blocked, the child should be rendered disabled and
 * this wrapper supplies the explanatory tooltip; when not blocked it renders children
 * untouched.
 */
export function PublishGateBanner({
  blocked,
  children,
}: {
  blocked: boolean
  children: ReactNode
}) {
  const { t } = useTranslation()
  if (!blocked) return <>{children}</>
  return (
    <TooltipProvider delay={150}>
      <Tooltip>
        <TooltipTrigger render={<span className="inline-flex" tabIndex={0} />}>
          {children}
        </TooltipTrigger>
        <TooltipContent>{t("surveysModule.publishGate.tooltip")}</TooltipContent>
      </Tooltip>
    </TooltipProvider>
  )
}
