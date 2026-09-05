import { useTranslation } from "react-i18next"
import { useDirection } from "@/hooks/use-direction"
import { useAuth } from "@/contexts/auth-context"
import { useNavigate } from "react-router"
import { Input } from "@/components/ui/input"
import { Button, buttonVariants } from "@/components/ui/button"
import { Separator } from "@/components/ui/separator"
import { SidebarTrigger } from "@/components/ui/sidebar"
import {
  Popover,
  PopoverTrigger,
  PopoverContent,
} from "@/components/ui/popover"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { Search, Bell, Globe, Sun, Moon, User, Settings, LogOut } from "lucide-react"
import { cn } from "@/lib/utils"

export function AppTopbar() {
  const { t } = useTranslation()
  const { toggleLang } = useDirection()
  const { logout } = useAuth()
  const navigate = useNavigate()

  const toggleTheme = () => {
    document.documentElement.classList.toggle("dark")
    const isDark = document.documentElement.classList.contains("dark")
    localStorage.setItem("theme", isDark ? "dark" : "light")
  }

  const notifications = [
    { id: 1, text: t("cx.notif1"), time: "10m", dotColor: "bg-nb-cyan" },
    { id: 2, text: t("cx.notif2"), time: "1h", dotColor: "bg-nb-mint-700" },
    { id: 3, text: t("cx.notif3"), time: "3h", dotColor: "bg-nb-mint" },
    { id: 4, text: t("cx.notif4"), time: "5h", dotColor: "bg-nb-navy-300" },
  ]

  return (
    <header className="sticky top-0 z-30 flex h-14 shrink-0 items-center gap-2 border-b border-border bg-background/95 backdrop-blur-sm px-4">
      <SidebarTrigger className="-ms-2" />
    

      {/* Search */}
      <div className="relative flex-1 max-w-sm hidden sm:block">
        <Search className="absolute start-2.5 top-1/2 -translate-y-1/2 size-4 text-muted-foreground pointer-events-none" />
        <Input
          placeholder={t("cx.searchPlaceholder")}
          className="ps-8 h-8 bg-muted/40 border-0 focus-visible:bg-background"
        />
      </div>

      <div className="flex-1" />

      <div className="flex items-center gap-1">
        {/* Theme */}
        <Button variant="ghost" size="icon" onClick={toggleTheme} aria-label="Toggle theme" className="size-8">
          <Sun className="size-4 dark:hidden" />
          <Moon className="size-4 hidden dark:block" />
        </Button>

        {/* Language */}
        <Button variant="ghost" size="icon" onClick={toggleLang} aria-label="Toggle language" className="size-8">
          <Globe className="size-4" />
        </Button>

        {/* Notifications */}
        <Popover>
          <PopoverTrigger
            className={cn(buttonVariants({ variant: "ghost", size: "icon" }), "size-8 relative")}
            aria-label={t("cx.notifications")}
          >
            <Bell className="size-4" />
            <span className="absolute top-1 end-1 size-2 rounded-full bg-d5" />
          </PopoverTrigger>
          <PopoverContent align="end" className="w-80 p-0">
            <div className="px-4 py-3 border-b border-border">
              <p className="text-sm font-bold">{t("cx.notifications")}</p>
            </div>
            <div className="max-h-72 overflow-y-auto">
              {notifications.map((n) => (
                <div
                  key={n.id}
                  className="flex items-start gap-3 px-4 py-3 hover:bg-muted/50 motion-safe:transition-colors cursor-pointer border-b border-border/50 last:border-0"
                >
                  <span className={cn("size-2 rounded-full mt-1.5 shrink-0", n.dotColor)} />
                  <div className="flex-1 min-w-0">
                    <p className="text-sm leading-relaxed">{n.text}</p>
                    <p className="text-[10px] text-muted-foreground mt-1">{n.time}</p>
                  </div>
                </div>
              ))}
            </div>
          </PopoverContent>
        </Popover>

        <Separator orientation="vertical" className="h-6 mx-1" />

        {/* User Menu */}
        <DropdownMenu>
          <DropdownMenuTrigger className="flex items-center gap-2 rounded-lg p-1 hover:bg-muted/50 motion-safe:transition-colors focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none">
              <div className="flex size-8 items-center justify-center rounded-full bg-gradient-to-br from-nb-cyan to-nb-cyan-700 text-white text-xs font-bold">
                {t("cx.userFullName")[0]}
              </div>
              <div className="hidden sm:block text-start">
                <p className="text-xs font-medium leading-none">{t("cx.userFullName")}</p>
                <p className="text-[10px] text-muted-foreground mt-0.5">{t("cx.userRole")}</p>
              </div>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-48">
            <DropdownMenuItem>
              <User className="size-4 me-2" />
              {t("cx.profile")}
            </DropdownMenuItem>
            <DropdownMenuItem>
              <Settings className="size-4 me-2" />
              {t("cx.navSettings")}
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem
              className="text-destructive focus:text-destructive"
              onClick={() => {
                logout()
                navigate("/login")
              }}
            >
              <LogOut className="size-4 me-2" />
              {t("cx.signOut")}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  )
}
