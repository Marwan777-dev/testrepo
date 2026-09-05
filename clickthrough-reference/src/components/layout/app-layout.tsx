import { useEffect, useRef } from "react"
import { useLocation } from "react-router"
import { SidebarProvider, SidebarInset } from "@/components/ui/sidebar"
import { useIsMobile } from "@/hooks/use-mobile"
import { AppSidebar } from "./app-sidebar"
import { AppTopbar } from "./app-topbar"
import { PersonaSwitcher } from "@/components/cx/persona-switcher"
import { MobileNav, MobileFAB } from "@/components/cx/mobile-nav"

export function AppLayout({ children }: { children: React.ReactNode }) {
  const isMobile = useIsMobile()
  const location = useLocation()
  const mainRef = useRef<HTMLDivElement>(null)

  // Scroll content area to top on route change
  useEffect(() => {
    mainRef.current?.scrollTo(0, 0)
  }, [location.pathname])

  return (
    <SidebarProvider>
      {!isMobile && <AppSidebar />}
      <SidebarInset>
        <AppTopbar />
        <div ref={mainRef} className="flex-1 overflow-auto pb-20 lg:pb-[50px]">
          {children}
        </div>
      </SidebarInset>
      {isMobile && <MobileNav />}
      {isMobile && <MobileFAB />}
      <PersonaSwitcher />
    </SidebarProvider>
  )
}
