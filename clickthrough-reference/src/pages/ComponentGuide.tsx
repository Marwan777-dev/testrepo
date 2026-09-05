import { useState } from "react"
import { useTranslation } from "react-i18next"
import { useDirection } from "@/hooks/use-direction"
import { cn } from "@/lib/utils"
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from "@/components/ui/accordion"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Badge } from "@/components/ui/badge"  
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage, 
  BreadcrumbSeparator,
} from "@/components/ui/breadcrumb"
import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Checkbox } from "@/components/ui/checkbox"
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from "@/components/ui/collapsible"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import {
  HoverCard,
  HoverCardContent,
  HoverCardTrigger,
} from "@/components/ui/hover-card"
import { Input } from "@/components/ui/input"
import { Kbd } from "@/components/ui/kbd"
import { Label } from "@/components/ui/label"
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationLink,
  PaginationNext,
  PaginationPrevious,
} from "@/components/ui/pagination"
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover"
import { Progress } from "@/components/ui/progress"
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group"
import { ScrollArea } from "@/components/ui/scroll-area"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Separator } from "@/components/ui/separator"
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from "@/components/ui/sheet"
import { Skeleton } from "@/components/ui/skeleton"
import { Slider } from "@/components/ui/slider"
import { Switch } from "@/components/ui/switch"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Textarea } from "@/components/ui/textarea"
import { Toggle } from "@/components/ui/toggle"
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip"
import {
  AlertTriangle,
  BarChart3,
  Bold,
  CheckCircle,
  ChevronDown,
  Copy,
  Download,
  Edit,
  Eye,
  FileText,
  Filter,
  Info,
  Italic,
  LayoutDashboard,
  Layers,
  Loader2,
  Mail,
  MoreHorizontal,
  MousePointerClick,
  Navigation,
  Palette,
  Plus,
  Search,
  Settings,
  Sun,
  Moon,
  TextCursorInput,
  Trash2,
  Upload,
  User,
  XCircle,
} from "lucide-react"
import {
  BarChart,
  Bar,
  LineChart,
  Line,
  AreaChart,
  Area,
  PieChart,
  Pie,
  Cell,
  RadarChart,
  Radar,
  PolarGrid,
  PolarAngleAxis,
  PolarRadiusAxis,
  ResponsiveContainer,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip as RechartsTooltip,
  Legend,
} from "recharts"
import type { TFunction } from "i18next"
import type { LucideIcon } from "lucide-react"

/* ──────────────────────────────────────────────
   Chart data & colors
   ────────────────────────────────────────────── */
const CHART_COLORS = ["#95C11F", "#00A19A", "#2B7BD5", "#D4A843", "#E85D5D"]

const monthlyData = [
  { month: "Jan", responses: 186, satisfaction: 80 },
  { month: "Feb", responses: 305, satisfaction: 72 },
  { month: "Mar", responses: 237, satisfaction: 78 },
  { month: "Apr", responses: 473, satisfaction: 85 },
  { month: "May", responses: 209, satisfaction: 82 },
  { month: "Jun", responses: 384, satisfaction: 90 },
]

const categoryData = [
  { name: "Product", value: 35 },
  { name: "Service", value: 25 },
  { name: "Support", value: 20 },
  { name: "Pricing", value: 12 },
  { name: "UX", value: 8 },
]

const radarData = [
  { metric: "Quality", A: 90, B: 70 },
  { metric: "Speed", A: 75, B: 85 },
  { metric: "Support", A: 88, B: 60 },
  { metric: "UX", A: 82, B: 78 },
  { metric: "Value", A: 70, B: 90 },
  { metric: "Trust", A: 95, B: 72 },
]

/* ──────────────────────────────────────────────
   Showcase card wrapper for individual demos
   ────────────────────────────────────────────── */
function ShowcaseCard({
  label,
  description,
  children,
}: {
  label: string
  description?: string
  children: React.ReactNode
}) {
  return (
    <div className="group rounded-xl border border-border bg-card p-6 space-y-4 shadow-sm hover:shadow-md transition-all duration-200 dark:hover:border-primary/20">
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm font-semibold text-foreground">{label}</p>
          {description && (
            <p className="text-xs text-muted-foreground mt-0.5">
              {description}
            </p>
          )}
        </div>
      </div>
      <div>{children}</div>
    </div>
  )
}

/* ──────────────────────────────────────────────
   Section header used inside each content section
   ────────────────────────────────────────────── */
function SectionHeader({
  title,
  description,
}: {
  title: string
  description: string
}) {
  return (
    <div className="mb-8">
      <h2 className="text-2xl font-bold tracking-tight">{title}</h2>
      <p className="text-muted-foreground mt-1">{description}</p>
      <div className="h-1 w-12 bg-secondary rounded-full mt-3" />
    </div>
  )
}

/* ══════════════════════════════════════════════
   1. COLOR PALETTE SECTION
   ══════════════════════════════════════════════ */
function ColorsSection({ t }: { t: TFunction }) {
  return (
    <div className="space-y-6">
      <SectionHeader
        title={t("guide.colorPalette")}
        description={t("guide.colorPaletteDesc")}
      />

      {/* Semantic Colors */}
      <ShowcaseCard label="Semantic Colors" description="Theme-aware tokens for backgrounds, text, and borders">
        <div>
          <p className="text-sm font-bold mb-3">{t("guide.semanticColors")}</p>
          <div className="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-6 gap-3">
            {[
              { name: "Primary", ar: t("color.primary"), cls: "bg-primary text-primary-foreground" },
              { name: "Secondary", ar: t("color.secondary"), cls: "bg-secondary text-secondary-foreground" },
              { name: "Accent", ar: t("color.accent"), cls: "bg-accent text-accent-foreground" },
              { name: "Muted", ar: t("color.muted"), cls: "bg-muted text-muted-foreground" },
              { name: "Destructive", ar: t("color.destructive"), cls: "bg-destructive text-white" },
              { name: "Card", ar: t("color.card"), cls: "bg-card text-card-foreground border border-border" },
            ].map((c) => (
              <div key={c.name} className={`rounded-lg p-4 text-center ${c.cls}`}>
                <p className="text-xs font-bold">{c.ar}</p>
                <p className="text-xs opacity-75">{c.name}</p>
              </div>
            ))}
          </div>
        </div>
      </ShowcaseCard>

      {/* Brand Colors */}
      <ShowcaseCard label="Brand Colors (nb-*)" description="Full brand palette with shade ramps">
        <div>
          <p className="text-sm font-bold mb-3">{t("guide.brandColors")} — Brand (nb-*)</p>
          <div className="grid grid-cols-3 sm:grid-cols-5 lg:grid-cols-9 gap-2">
            {[
              { name: "Green", cls: "bg-nb-cyan" },
              { name: "Teal", cls: "bg-nb-mint" },
              { name: "Charcoal", cls: "bg-nb-navy" },
              { name: "Grey", cls: "bg-nb-stone" },
              { name: "Gold", cls: "bg-d3" },
              { name: "Coral", cls: "bg-d5" },
              { name: "Blue", cls: "bg-nb-cyan" },
            ].map((c) => (
              <div key={c.name} className="text-center space-y-1">
                <div className={`h-10 rounded-md ${c.cls}`} />
                <p className="text-[10px] text-muted-foreground">{c.name}</p>
              </div>
            ))}
          </div>
          {/* Shades */}
          <div className="mt-3 grid grid-cols-4 gap-1">
            <div className="space-y-1">
              <div className="h-6 rounded bg-nb-cyan-700" />
              <div className="h-6 rounded bg-nb-cyan" />
              <div className="h-6 rounded bg-nb-cyan-300" />
              <div className="h-6 rounded bg-nb-cyan-100" />
            </div>
            <div className="space-y-1">
              <div className="h-6 rounded bg-nb-mint-700" />
              <div className="h-6 rounded bg-nb-mint" />
              <div className="h-6 rounded bg-nb-mint-300" />
              <div className="h-6 rounded bg-nb-mint-100" />
            </div>
            <div className="space-y-1">
              <div className="h-6 rounded bg-d3-dark" />
              <div className="h-6 rounded bg-d3" />
              <div className="h-6 rounded bg-d3-light" />
              <div className="h-6 rounded bg-d3-light" />
            </div>
            <div className="space-y-1">
              <div className="h-6 rounded bg-nb-cyan-800" />
              <div className="h-6 rounded bg-nb-cyan" />
              <div className="h-6 rounded bg-nb-cyan-300" />
              <div className="h-6 rounded bg-nb-cyan-100" />
            </div>
          </div>
          <div className="flex gap-4 mt-2 text-[10px] text-muted-foreground">
            <span>Dark</span>
            <span>Base</span>
            <span>Lite</span>
            <span>Ultra-lite</span>
          </div>
        </div>
      </ShowcaseCard>

      {/* Status Colors */}
      <ShowcaseCard label="Status Colors" description="Contextual colors for success, error, warning, and info states">
        <div>
          <p className="text-sm font-bold mb-3">{t("guide.statusColors")} — Status</p>
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
            <div className="flex items-center gap-2 rounded-lg bg-nb-mint-100 dark:bg-nb-mint-700/20 p-3">
              <CheckCircle className="size-4 text-nb-mint-700 dark:text-nb-mint-300" />
              <span className="text-sm text-nb-mint-700 dark:text-nb-mint-300">{t("status.success")}</span>
            </div>
            <div className="flex items-center gap-2 rounded-lg bg-d5-light dark:bg-d5-dark/20 p-3">
              <XCircle className="size-4 text-destructive dark:text-d5-light" />
              <span className="text-sm text-destructive dark:text-d5-light">{t("status.error")}</span>
            </div>
            <div className="flex items-center gap-2 rounded-lg bg-d3-light dark:bg-d3-dark/20 p-3">
              <AlertTriangle className="size-4 text-d3-dark dark:text-d3-light" />
              <span className="text-sm text-d3-dark dark:text-d3-light">{t("status.warning")}</span>
            </div>
            <div className="flex items-center gap-2 rounded-lg bg-nb-cyan-100 dark:bg-nb-cyan-800/20 p-3">
              <Info className="size-4 text-nb-cyan-800 dark:text-nb-cyan-300" />
              <span className="text-sm text-nb-cyan-800 dark:text-nb-cyan-300">{t("status.info")}</span>
            </div>
          </div>
        </div>
      </ShowcaseCard>
    </div>
  )
}

/* ══════════════════════════════════════════════
   2. BUTTONS & ACTIONS SECTION
   ══════════════════════════════════════════════ */
function ButtonsSection({ t }: { t: TFunction }) {
  return (
    <div className="space-y-6">
      <SectionHeader
        title={t("guide.buttonsActions")}
        description="Button, Toggle, DropdownMenu"
      />

      <ShowcaseCard label="Button variants" description="Primary actions use coral CTA color">
        <div className="flex flex-wrap gap-3">
          <Button className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground">
            <Plus className="size-4 ms-2" />
            {t("button.primaryAction")}
          </Button>
          <Button variant="default">{t("button.default")}</Button>
          <Button variant="outline">{t("button.outline")}</Button>
          <Button variant="ghost">{t("button.ghost")}</Button>
          <Button variant="destructive">
            <Trash2 className="size-4 ms-2" />
            {t("common.delete")}
          </Button>
          <Button variant="link">{t("button.link")}</Button>
        </div>
      </ShowcaseCard>

      <ShowcaseCard label="Button sizes" description="Consistent sizing tokens from lg to icon">
        <div className="flex flex-wrap items-center gap-3">
          <Button size="lg">{t("button.large")}</Button>
          <Button size="default">{t("button.default")}</Button>
          <Button size="sm">{t("button.small")}</Button>
          <Button size="icon" aria-label={t("common.settings")}>
            <Settings className="size-4" />
          </Button>
        </div>
      </ShowcaseCard>

      <ShowcaseCard label="Button states" description="Loading spinners and disabled states">
        <div className="flex flex-wrap items-center gap-3">
          <Button disabled>
            <Loader2 className="size-4 animate-spin ms-2" />
            {t("common.loading")}
          </Button>
          <Button disabled>{t("common.disabled")}</Button>
        </div>
      </ShowcaseCard>

      <ShowcaseCard label="Toggle" description="Stateful toggle buttons for formatting actions">
        <div className="flex gap-2">
          <Toggle aria-label="Bold">
            <Bold className="size-4" />
          </Toggle>
          <Toggle aria-label="Italic">
            <Italic className="size-4" />
          </Toggle>
        </div>
      </ShowcaseCard>

      <ShowcaseCard label="DropdownMenu" description="Contextual actions with keyboard navigation">
        <DropdownMenu>
          <DropdownMenuTrigger className="inline-flex items-center justify-center rounded-md border border-input bg-background px-4 py-2 text-sm font-medium hover:bg-accent hover:text-accent-foreground transition-colors">
            {t("common.options")}
            <ChevronDown className="size-4 ms-2" />
          </DropdownMenuTrigger>
          <DropdownMenuContent>
            <DropdownMenuItem>
              <Edit className="size-4 ms-2" />
              {t("common.edit")}
            </DropdownMenuItem>
            <DropdownMenuItem>
              <Copy className="size-4 ms-2" />
              {t("common.copy")}
            </DropdownMenuItem>
            <DropdownMenuItem>
              <Download className="size-4 ms-2" />
              {t("common.download")}
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem className="text-destructive">
              <Trash2 className="size-4 ms-2" />
              {t("common.delete")}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </ShowcaseCard>
    </div>
  )
}

/* ══════════════════════════════════════════════
   3. FORM INPUTS SECTION
   ══════════════════════════════════════════════ */
function FormsSection({ t }: { t: TFunction }) {
  const [switchOn, setSwitchOn] = useState(true)
  const [sliderVal, setSliderVal] = useState([33])

  return (
    <div className="space-y-6">
      <SectionHeader
        title={t("guide.formInputs")}
        description="Input, Textarea, Select, Checkbox, Radio, Switch, Slider"
      />

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <ShowcaseCard label="Input" description="Text fields with icons, labels, and validation states">
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="name">{t("auth.fullName")}</Label>
              <Input id="name" placeholder={t("auth.enterName")} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="email">{t("auth.email")}</Label>
              <Input id="email" type="email" placeholder="name@example.com" dir="ltr" />
            </div>
            <div className="space-y-2">
              <Label htmlFor="search">{t("common.search")}</Label>
              <div className="relative">
                <Search className="absolute start-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground" />
                <Input id="search" placeholder={t("form.searchPlaceholder")} className="ps-10" />
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="disabled">{t("form.disabledField")}</Label>
              <Input id="disabled" disabled placeholder={t("form.notAvailable")} />
            </div>
          </div>
        </ShowcaseCard>

        <ShowcaseCard label="Textarea" description="Multi-line text input with auto-resize support">
          <div className="space-y-2">
            <Label htmlFor="notes">{t("form.notes")}</Label>
            <Textarea
              id="notes"
              placeholder={t("form.writeNotes")}
              className="leading-relaxed"
            />
          </div>
        </ShowcaseCard>

        <ShowcaseCard label="Select" description="Single-value dropdown with search support">
          <div className="space-y-2">
            <Label>{t("form.department")}</Label>
            <Select>
              <SelectTrigger>
                <SelectValue placeholder={t("form.chooseDepartment")} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="sales">{t("form.sales")}</SelectItem>
                <SelectItem value="support">{t("form.support")}</SelectItem>
                <SelectItem value="engineering">{t("form.engineering")}</SelectItem>
                <SelectItem value="marketing">{t("form.marketing")}</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </ShowcaseCard>

        <ShowcaseCard label="Checkbox & Radio" description="Selection controls for single and multi-choice">
          <div className="space-y-4">
            <div className="space-y-3">
              <p className="text-sm font-bold">Checkbox</p>
              <div className="flex items-center gap-2">
                <Checkbox id="terms" />
                <Label htmlFor="terms">{t("form.agreeTerms")}</Label>
              </div>
              <div className="flex items-center gap-2">
                <Checkbox id="newsletter" defaultChecked />
                <Label htmlFor="newsletter">{t("form.subscribeNewsletter")}</Label>
              </div>
            </div>
            <Separator />
            <div className="space-y-3">
              <p className="text-sm font-bold">RadioGroup</p>
              <RadioGroup defaultValue="daily">
                <div className="flex items-center gap-2">
                  <RadioGroupItem value="daily" id="daily" />
                  <Label htmlFor="daily">{t("form.daily")}</Label>
                </div>
                <div className="flex items-center gap-2">
                  <RadioGroupItem value="weekly" id="weekly" />
                  <Label htmlFor="weekly">{t("form.weekly")}</Label>
                </div>
                <div className="flex items-center gap-2">
                  <RadioGroupItem value="monthly" id="monthly" />
                  <Label htmlFor="monthly">{t("form.monthly")}</Label>
                </div>
              </RadioGroup>
            </div>
          </div>
        </ShowcaseCard>

        <ShowcaseCard label="Switch" description="Binary toggle for on/off settings">
          <div className="space-y-4">
            <div className="flex items-center justify-between">
              <Label htmlFor="notifications">{t("common.notifications")}</Label>
              <Switch
                id="notifications"
                checked={switchOn}
                onCheckedChange={setSwitchOn}
              />
            </div>
            <div className="flex items-center justify-between">
              <Label htmlFor="disabled-switch">{t("common.disabled")}</Label>
              <Switch id="disabled-switch" disabled />
            </div>
          </div>
        </ShowcaseCard>

        <ShowcaseCard label="Slider" description="Range input with real-time value feedback">
          <div className="space-y-4">
            <div className="space-y-2">
              <div className="flex justify-between">
                <Label>{t("form.satisfactionLevel")}</Label>
                <span className="text-sm text-muted-foreground tabular-nums">{sliderVal[0]}%</span>
              </div>
              <Slider
                value={sliderVal}
                onValueChange={(v) => setSliderVal(Array.isArray(v) ? [...v] : [v])}
                max={100}
                step={1}
              />
            </div>
          </div>
        </ShowcaseCard>
      </div>
    </div>
  )
}

/* ══════════════════════════════════════════════
   4. DATA DISPLAY SECTION
   ══════════════════════════════════════════════ */
function DataSection({ t }: { t: TFunction }) {
  const [progress, setProgress] = useState(45)

  return (
    <div className="space-y-6">
      <SectionHeader
        title={t("guide.dataDisplay")}
        description="Table, Badge, Avatar, Card, Progress, Skeleton"
      />

      <ShowcaseCard label="Table" description="Data tables with sortable columns and row actions">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t("table.name")}</TableHead>
              <TableHead>{t("table.email")}</TableHead>
              <TableHead>{t("table.status")}</TableHead>
              <TableHead className="text-end">{t("table.rating")}</TableHead>
              <TableHead className="w-10"></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {[
              { name: "\u0623\u062D\u0645\u062F \u0645\u062D\u0645\u062F", email: "ahmed@example.com", status: t("status.active"), rating: 4.8 },
              { name: "\u0641\u0627\u0637\u0645\u0629 \u0639\u0644\u064A", email: "fatima@example.com", status: t("status.pending"), rating: 3.5 },
              { name: "\u062E\u0627\u0644\u062F \u0633\u0639\u064A\u062F", email: "khaled@example.com", status: t("status.completed"), rating: 4.2 },
            ].map((row) => (
              <TableRow key={row.email} className="hover:bg-muted/50 transition-colors">
                <TableCell className="font-medium">{row.name}</TableCell>
                <TableCell dir="ltr" className="text-start">{row.email}</TableCell>
                <TableCell>
                  <Badge
                    variant={
                      row.status === t("status.active") ? "default" : row.status === t("status.pending") ? "secondary" : "outline"
                    }
                  >
                    {row.status}
                  </Badge>
                </TableCell>
                <TableCell className="text-end tabular-nums">{row.rating}</TableCell>
                <TableCell>
                  <Button variant="ghost" size="icon" aria-label="More">
                    <MoreHorizontal className="size-4" />
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </ShowcaseCard>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <ShowcaseCard label="Badge variants" description="Status indicators and category labels">
          <div className="flex flex-wrap gap-2">
            <Badge>{t("button.default")}</Badge>
            <Badge variant="secondary">{t("color.secondary")}</Badge>
            <Badge variant="outline">{t("button.outline")}</Badge>
            <Badge variant="destructive">{t("color.destructive")}</Badge>
            <Badge className="bg-nb-mint-100 text-nb-mint-700 dark:bg-nb-mint-700/20 dark:text-nb-mint-300 border-0">
              <CheckCircle className="size-3 ms-1" />
              {t("status.active")}
            </Badge>
            <Badge className="bg-d3-light text-d3-dark dark:bg-d3-dark/20 dark:text-d3-light border-0">
              <AlertTriangle className="size-3 ms-1" />
              {t("status.pending")}
            </Badge>
          </div>
        </ShowcaseCard>

        <ShowcaseCard label="Avatar" description="User profile images with fallback initials">
          <div className="flex items-center gap-3">
            <Avatar>
              <AvatarImage src="https://i.pravatar.cc/40?img=1" alt="\u0623\u062D\u0645\u062F" />
              <AvatarFallback>\u0623\u062D</AvatarFallback>
            </Avatar>
            <Avatar>
              <AvatarImage src="https://i.pravatar.cc/40?img=2" alt="\u0641\u0627\u0637\u0645\u0629" />
              <AvatarFallback>\u0641\u0627</AvatarFallback>
            </Avatar>
            <Avatar>
              <AvatarFallback className="bg-primary text-primary-foreground">\u062E\u0633</AvatarFallback>
            </Avatar>
            <Avatar>
              <AvatarFallback className="bg-secondary text-secondary-foreground">
                <User className="size-4" />
              </AvatarFallback>
            </Avatar>
          </div>
        </ShowcaseCard>

        <ShowcaseCard label="Progress" description="Visual indicators for completion and loading">
          <div className="space-y-4">
            <div className="space-y-2">
              <div className="flex justify-between text-sm">
                <span>{t("form.satisfactionLevel")}</span>
                <span className="tabular-nums text-muted-foreground">{progress}%</span>
              </div>
              <Progress value={progress} />
            </div>
            <div className="flex gap-2">
              <Button size="sm" variant="outline" onClick={() => setProgress(Math.max(0, progress - 10))}>
                -10
              </Button>
              <Button size="sm" variant="outline" onClick={() => setProgress(Math.min(100, progress + 10))}>
                +10
              </Button>
            </div>
          </div>
        </ShowcaseCard>

        <ShowcaseCard label="Skeleton (loading)" description="Placeholder shimmer while content loads">
          <div className="space-y-3">
            <div className="flex items-center gap-3">
              <Skeleton className="size-10 rounded-full" />
              <div className="space-y-2 flex-1">
                <Skeleton className="h-4 w-3/4" />
                <Skeleton className="h-3 w-1/2" />
              </div>
            </div>
            <Skeleton className="h-20 w-full" />
          </div>
        </ShowcaseCard>
      </div>

      <ShowcaseCard label="Card variants" description="Stat cards with KPI highlights and trend indicators">
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <Card>
            <CardHeader className="pb-2">
              <CardDescription>{t("dashboard.totalResponses")}</CardDescription>
              <CardTitle className="text-3xl font-heading tabular-nums">
                <span dir="ltr">1,284</span>
              </CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-xs text-nb-mint-700 dark:text-nb-mint-300">
                <span dir="ltr">+12.5%</span> {t("dashboard.fromLastMonth")}
              </p>
            </CardContent>
          </Card>
          <Card>
            <CardHeader className="pb-2">
              <CardDescription>{t("dashboard.satisfactionRate")}</CardDescription>
              <CardTitle className="text-3xl font-heading tabular-nums">
                <span dir="ltr">4.6</span>
                <span className="text-lg text-muted-foreground">/5</span>
              </CardTitle>
            </CardHeader>
            <CardContent>
              <Progress value={92} className="h-1.5" />
            </CardContent>
          </Card>
          <Card className="bg-primary text-primary-foreground">
            <CardHeader className="pb-2">
              <CardDescription className="text-primary-foreground/70">
                {t("survey.activeSurveys")}
              </CardDescription>
              <CardTitle className="text-3xl font-heading tabular-nums">8</CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-xs text-primary-foreground/70">
                {t("survey.surveysExpiring", { count: 3 })}
              </p>
            </CardContent>
          </Card>
        </div>
      </ShowcaseCard>

      <ShowcaseCard label="Kbd (keyboard shortcut)" description="Visual key cap indicators for shortcut hints">
        <div className="flex flex-wrap items-center gap-3 text-sm">
          <span>{t("common.search")}:</span>
          <Kbd>Cmd</Kbd><Kbd>K</Kbd>
          <span className="text-muted-foreground ms-4">{t("common.save")}:</span>
          <Kbd>Cmd</Kbd><Kbd>S</Kbd>
        </div>
      </ShowcaseCard>
    </div>
  )
}

/* ══════════════════════════════════════════════
   5. OVERLAYS & FEEDBACK SECTION
   ══════════════════════════════════════════════ */
function OverlaysSection({ t }: { t: TFunction }) {
  return (
    <div className="space-y-6">
      <SectionHeader
        title={t("guide.overlays")}
        description="Dialog, AlertDialog, Sheet, Popover, Tooltip, Alert, Accordion"
      />

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        <ShowcaseCard label="Dialog" description="Modal dialogs for forms and confirmations">
          <Dialog>
            <DialogTrigger className="inline-flex items-center justify-center rounded-md border border-input bg-background px-4 py-2 text-sm font-medium hover:bg-accent hover:text-accent-foreground transition-colors">
              {t("dialog.open")}
            </DialogTrigger>
            <DialogContent>
              <DialogHeader>
                <DialogTitle>{t("survey.createNew")}</DialogTitle>
                <DialogDescription>
                  {t("survey.createNewDesc")}
                </DialogDescription>
              </DialogHeader>
              <div className="space-y-4 py-4">
                <div className="space-y-2">
                  <Label htmlFor="survey-name">{t("survey.name")}</Label>
                  <Input id="survey-name" placeholder={t("survey.namePlaceholder")} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="survey-desc">{t("survey.description")}</Label>
                  <Textarea id="survey-desc" placeholder="..." />
                </div>
              </div>
              <DialogFooter>
                <Button variant="outline">{t("common.cancel")}</Button>
                <Button className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground">
                  {t("common.create")}
                </Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </ShowcaseCard>

        <ShowcaseCard label="AlertDialog" description="Destructive confirmation with clear messaging">
          <AlertDialog>
            <AlertDialogTrigger className="inline-flex items-center justify-center rounded-md bg-destructive px-4 py-2 text-sm font-medium text-white hover:bg-destructive/90 transition-colors">
              {t("survey.deleteSurvey")}
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>{t("dialog.areYouSure")}</AlertDialogTitle>
                <AlertDialogDescription>
                  {t("survey.deleteWarning")}
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>{t("common.cancel")}</AlertDialogCancel>
                <AlertDialogAction className="bg-destructive text-white hover:bg-destructive/90">
                  {t("survey.deletePermanent")}
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        </ShowcaseCard>

        <ShowcaseCard label="Sheet (side panel)" description="Slide-over panel for filters and detail views">
          <Sheet>
            <SheetTrigger className="inline-flex items-center justify-center rounded-md border border-input bg-background px-4 py-2 text-sm font-medium hover:bg-accent hover:text-accent-foreground transition-colors">
              <Filter className="size-4 ms-2" />
              {t("common.filter")}
            </SheetTrigger>
            <SheetContent>
              <SheetHeader>
                <SheetTitle>{t("dialog.filterOptions")}</SheetTitle>
                <SheetDescription>
                  {t("dialog.filterDesc")}
                </SheetDescription>
              </SheetHeader>
              <div className="space-y-4 py-4">
                <div className="space-y-2">
                  <Label>{t("table.status")}</Label>
                  <Select>
                    <SelectTrigger>
                      <SelectValue placeholder={t("common.all")} />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="active">{t("status.active")}</SelectItem>
                      <SelectItem value="pending">{t("status.pending")}</SelectItem>
                      <SelectItem value="completed">{t("status.completed")}</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
            </SheetContent>
          </Sheet>
        </ShowcaseCard>

        <ShowcaseCard label="Popover" description="Floating content panel triggered on click">
          <Popover>
            <PopoverTrigger className="inline-flex items-center justify-center rounded-md border border-input bg-background px-4 py-2 text-sm font-medium hover:bg-accent hover:text-accent-foreground transition-colors">
              {t("dialog.moreInfo")}
            </PopoverTrigger>
            <PopoverContent>
              <div className="space-y-2">
                <h4 className="text-sm font-bold">{t("dialog.ratingDetails")}</h4>
                <p className="text-sm text-muted-foreground">
                  {t("dialog.ratingCalcDesc")}
                </p>
              </div>
            </PopoverContent>
          </Popover>
        </ShowcaseCard>

        <ShowcaseCard label="HoverCard" description="Rich preview on hover for user profiles">
          <HoverCard>
            <HoverCardTrigger className="text-primary underline-offset-4 hover:underline cursor-pointer text-sm">
              @\u0623\u062D\u0645\u062F_\u0645\u062D\u0645\u062F
            </HoverCardTrigger>
            <HoverCardContent>
              <div className="flex gap-3">
                <Avatar>
                  <AvatarFallback>\u0623\u062D</AvatarFallback>
                </Avatar>
                <div>
                  <h4 className="text-sm font-bold">\u0623\u062D\u0645\u062F \u0645\u062D\u0645\u062F</h4>
                  <p className="text-xs text-muted-foreground">{t("misc.dataAnalyst")}</p>
                  <p className="text-xs text-muted-foreground mt-1">{t("misc.joinedDate")}</p>
                </div>
              </div>
            </HoverCardContent>
          </HoverCard>
        </ShowcaseCard>

        <ShowcaseCard label="Tooltip" description="Contextual hints on hover for icon buttons">
          <div className="flex gap-3">
            <Tooltip>
              <TooltipTrigger aria-label={t("common.edit")} className="inline-flex items-center justify-center size-9 rounded-md border border-input bg-background hover:bg-accent hover:text-accent-foreground transition-colors">
                <Edit className="size-4" />
              </TooltipTrigger>
              <TooltipContent>{t("common.edit")}</TooltipContent>
            </Tooltip>
            <Tooltip>
              <TooltipTrigger aria-label={t("common.copy")} className="inline-flex items-center justify-center size-9 rounded-md border border-input bg-background hover:bg-accent hover:text-accent-foreground transition-colors">
                <Copy className="size-4" />
              </TooltipTrigger>
              <TooltipContent>{t("common.copy")}</TooltipContent>
            </Tooltip>
            <Tooltip>
              <TooltipTrigger aria-label={t("common.download")} className="inline-flex items-center justify-center size-9 rounded-md border border-input bg-background hover:bg-accent hover:text-accent-foreground transition-colors">
                <Download className="size-4" />
              </TooltipTrigger>
              <TooltipContent>{t("common.download")}</TooltipContent>
            </Tooltip>
          </div>
        </ShowcaseCard>
      </div>

      <ShowcaseCard label="Alert variants" description="Inline feedback banners for system messages">
        <div className="space-y-3">
          <Alert>
            <Info className="size-4" />
            <AlertTitle>{t("feedback.infoTitle")}</AlertTitle>
            <AlertDescription>{t("feedback.systemUpdated")}</AlertDescription>
          </Alert>
          <Alert variant="destructive">
            <XCircle className="size-4" />
            <AlertTitle>{t("feedback.errorTitle")}</AlertTitle>
            <AlertDescription>{t("feedback.saveFailed")}</AlertDescription>
          </Alert>
        </div>
      </ShowcaseCard>

      <ShowcaseCard label="Accordion" description="Collapsible content sections for FAQ and details">
        <Accordion>
          <AccordionItem value="q1">
            <AccordionTrigger>{t("faq.howRatingCalc")}</AccordionTrigger>
            <AccordionContent className="leading-relaxed">
              {t("faq.ratingAnswer")}
            </AccordionContent>
          </AccordionItem>
          <AccordionItem value="q2">
            <AccordionTrigger>{t("faq.canExport")}</AccordionTrigger>
            <AccordionContent className="leading-relaxed">
              {t("faq.exportAnswer")}
            </AccordionContent>
          </AccordionItem>
          <AccordionItem value="q3">
            <AccordionTrigger>{t("faq.permissions")}</AccordionTrigger>
            <AccordionContent className="leading-relaxed">
              {t("faq.permissionsAnswer")}
            </AccordionContent>
          </AccordionItem>
        </Accordion>
      </ShowcaseCard>
    </div>
  )
}

/* ══════════════════════════════════════════════
   6. NAVIGATION SECTION
   ══════════════════════════════════════════════ */
function NavigationSection({ t }: { t: TFunction }) {
  return (
    <div className="space-y-6">
      <SectionHeader
        title={t("guide.navigation")}
        description="Tabs, Breadcrumb, Pagination, ScrollArea, Collapsible"
      />

      <ShowcaseCard label="Tabs" description="Tabbed content areas for organizing related views">
        <Tabs defaultValue="overview">
          <TabsList>
            <TabsTrigger value="overview">{t("nav.overview")}</TabsTrigger>
            <TabsTrigger value="analytics">{t("nav.analytics")}</TabsTrigger>
            <TabsTrigger value="reports">{t("nav.reports")}</TabsTrigger>
            <TabsTrigger value="settings">{t("nav.settings")}</TabsTrigger>
          </TabsList>
          <TabsContent value="overview" className="mt-4">
            <p className="text-sm text-muted-foreground">
              {t("tabs.overviewContent")}
            </p>
          </TabsContent>
          <TabsContent value="analytics" className="mt-4">
            <p className="text-sm text-muted-foreground">
              {t("tabs.analyticsContent")}
            </p>
          </TabsContent>
          <TabsContent value="reports" className="mt-4">
            <p className="text-sm text-muted-foreground">
              {t("tabs.reportsContent")}
            </p>
          </TabsContent>
          <TabsContent value="settings" className="mt-4">
            <p className="text-sm text-muted-foreground">
              {t("tabs.settingsContent")}
            </p>
          </TabsContent>
        </Tabs>
      </ShowcaseCard>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <ShowcaseCard label="Breadcrumb" description="Hierarchical navigation path indicator">
          <Breadcrumb>
            <BreadcrumbList>
              <BreadcrumbItem>
                <BreadcrumbLink href="#">{t("nav.home")}</BreadcrumbLink>
              </BreadcrumbItem>
              <BreadcrumbSeparator />
              <BreadcrumbItem>
                <BreadcrumbLink href="#">{t("nav.surveys")}</BreadcrumbLink>
              </BreadcrumbItem>
              <BreadcrumbSeparator />
              <BreadcrumbItem>
                <BreadcrumbPage>{t("misc.customerSurveyName")}</BreadcrumbPage>
              </BreadcrumbItem>
            </BreadcrumbList>
          </Breadcrumb>
        </ShowcaseCard>

        <ShowcaseCard label="Pagination" description="Page navigation for large data sets">
          <Pagination>
            <PaginationContent>
              <PaginationItem>
                <PaginationPrevious href="#" />
              </PaginationItem>
              <PaginationItem>
                <PaginationLink href="#">1</PaginationLink>
              </PaginationItem>
              <PaginationItem>
                <PaginationLink href="#" isActive>2</PaginationLink>
              </PaginationItem>
              <PaginationItem>
                <PaginationLink href="#">3</PaginationLink>
              </PaginationItem>
              <PaginationItem>
                <PaginationNext href="#" />
              </PaginationItem>
            </PaginationContent>
          </Pagination>
        </ShowcaseCard>
      </div>

      <ShowcaseCard label="ScrollArea" description="Custom scrollbar container for overflow content">
        <ScrollArea className="h-40 rounded-md border border-border p-4">
          <div className="space-y-3">
            {Array.from({ length: 15 }, (_, i) => (
              <div key={i} className="flex items-center justify-between py-2 border-b border-border last:border-0">
                <div className="flex items-center gap-3">
                  <Avatar className="size-8">
                    <AvatarFallback className="text-xs">{t("misc.userNumber")} {i + 1}</AvatarFallback>
                  </Avatar>
                  <span className="text-sm">{t("misc.userNumber")} {i + 1}</span>
                </div>
                <Badge variant="outline" className="text-xs">
                  {i % 3 === 0 ? t("status.active") : i % 3 === 1 ? t("status.pending") : t("status.completed")}
                </Badge>
              </div>
            ))}
          </div>
        </ScrollArea>
      </ShowcaseCard>

      <ShowcaseCard label="Collapsible" description="Expandable sections for progressive disclosure">
        <Collapsible>
          <div className="flex items-center justify-between">
            <h4 className="text-sm font-bold">{t("misc.advancedOptions")}</h4>
            <CollapsibleTrigger className="inline-flex items-center justify-center rounded-md size-8 hover:bg-accent hover:text-accent-foreground transition-colors">
              <ChevronDown className="size-4" />
            </CollapsibleTrigger>
          </div>
          <CollapsibleContent className="mt-2 space-y-2">
            <div className="rounded-md border border-border p-3 text-sm">
              {t("misc.advancedSetting") + " 1"}
            </div>
            <div className="rounded-md border border-border p-3 text-sm">
              {t("misc.advancedSetting") + " 2"}
            </div>
          </CollapsibleContent>
        </Collapsible>
      </ShowcaseCard>
    </div>
  )
}

/* ══════════════════════════════════════════════
   7. CHARTS SECTION
   ══════════════════════════════════════════════ */
function ChartsSection({ t }: { t: TFunction }) {
  return (
    <div className="space-y-6">
      <SectionHeader
        title={t("guide.charts")}
        description={t("guide.chartsDesc")}
      />

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Bar Chart */}
        <ShowcaseCard label="Bar Chart" description="Monthly responses with vertical bars">
          <div className="h-64">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={monthlyData} margin={{ top: 5, right: 20, left: 0, bottom: 5 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" />
                <XAxis dataKey="month" tick={{ fontSize: 12 }} stroke="hsl(var(--muted-foreground))" />
                <YAxis tick={{ fontSize: 12 }} stroke="hsl(var(--muted-foreground))" />
                <RechartsTooltip
                  contentStyle={{
                    backgroundColor: "hsl(var(--card))",
                    border: "1px solid hsl(var(--border))",
                    borderRadius: "8px",
                    fontSize: "12px",
                  }}
                />
                <Bar dataKey="responses" fill={CHART_COLORS[0]} radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </ShowcaseCard>

        {/* Line Chart */}
        <ShowcaseCard label="Line Chart" description="Satisfaction trend over time with gradient area">
          <div className="h-64">
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={monthlyData} margin={{ top: 5, right: 20, left: 0, bottom: 5 }}>
                <defs>
                  <linearGradient id="lineGradient" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor={CHART_COLORS[1]} stopOpacity={0.3} />
                    <stop offset="100%" stopColor={CHART_COLORS[1]} stopOpacity={0} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" />
                <XAxis dataKey="month" tick={{ fontSize: 12 }} stroke="hsl(var(--muted-foreground))" />
                <YAxis tick={{ fontSize: 12 }} stroke="hsl(var(--muted-foreground))" domain={[60, 100]} />
                <RechartsTooltip
                  contentStyle={{
                    backgroundColor: "hsl(var(--card))",
                    border: "1px solid hsl(var(--border))",
                    borderRadius: "8px",
                    fontSize: "12px",
                  }}
                />
                <Line
                  type="monotone"
                  dataKey="satisfaction"
                  stroke={CHART_COLORS[1]}
                  strokeWidth={2.5}
                  dot={{ fill: CHART_COLORS[1], r: 4 }}
                  activeDot={{ r: 6 }}
                />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </ShowcaseCard>

        {/* Area Chart */}
        <ShowcaseCard label="Area Chart" description="Responses over time with gradient fill">
          <div className="h-64">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={monthlyData} margin={{ top: 5, right: 20, left: 0, bottom: 5 }}>
                <defs>
                  <linearGradient id="areaGradient" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor={CHART_COLORS[2]} stopOpacity={0.4} />
                    <stop offset="100%" stopColor={CHART_COLORS[2]} stopOpacity={0.05} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" />
                <XAxis dataKey="month" tick={{ fontSize: 12 }} stroke="hsl(var(--muted-foreground))" />
                <YAxis tick={{ fontSize: 12 }} stroke="hsl(var(--muted-foreground))" />
                <RechartsTooltip
                  contentStyle={{
                    backgroundColor: "hsl(var(--card))",
                    border: "1px solid hsl(var(--border))",
                    borderRadius: "8px",
                    fontSize: "12px",
                  }}
                />
                <Area
                  type="monotone"
                  dataKey="responses"
                  stroke={CHART_COLORS[2]}
                  strokeWidth={2}
                  fill="url(#areaGradient)"
                />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </ShowcaseCard>

        {/* Pie / Donut Chart */}
        <ShowcaseCard label="Donut Chart" description="Feedback distribution by category">
          <div className="h-64">
            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie
                  data={categoryData}
                  cx="50%"
                  cy="50%"
                  innerRadius={55}
                  outerRadius={85}
                  paddingAngle={3}
                  dataKey="value"
                  strokeWidth={0}
                >
                  {categoryData.map((_, index) => (
                    <Cell key={`cell-${index}`} fill={CHART_COLORS[index % CHART_COLORS.length]} />
                  ))}
                </Pie>
                <RechartsTooltip
                  contentStyle={{
                    backgroundColor: "hsl(var(--card))",
                    border: "1px solid hsl(var(--border))",
                    borderRadius: "8px",
                    fontSize: "12px",
                  }}
                />
                <Legend
                  verticalAlign="bottom"
                  iconType="circle"
                  iconSize={8}
                  wrapperStyle={{ fontSize: "12px" }}
                />
              </PieChart>
            </ResponsiveContainer>
          </div>
        </ShowcaseCard>

        {/* Radar Chart */}
        <ShowcaseCard label="Radar Chart" description="Multi-metric comparison across dimensions">
          <div className="h-72 md:col-span-2">
            <ResponsiveContainer width="100%" height="100%">
              <RadarChart cx="50%" cy="50%" outerRadius="70%" data={radarData}>
                <PolarGrid stroke="hsl(var(--border))" />
                <PolarAngleAxis
                  dataKey="metric"
                  tick={{ fontSize: 11, fill: "hsl(var(--muted-foreground))" }}
                />
                <PolarRadiusAxis
                  angle={30}
                  domain={[0, 100]}
                  tick={{ fontSize: 10 }}
                  stroke="hsl(var(--border))"
                />
                <Radar
                  name="Product A"
                  dataKey="A"
                  stroke={CHART_COLORS[0]}
                  fill={CHART_COLORS[0]}
                  fillOpacity={0.2}
                  strokeWidth={2}
                />
                <Radar
                  name="Product B"
                  dataKey="B"
                  stroke={CHART_COLORS[1]}
                  fill={CHART_COLORS[1]}
                  fillOpacity={0.15}
                  strokeWidth={2}
                />
                <Legend
                  iconType="circle"
                  iconSize={8}
                  wrapperStyle={{ fontSize: "12px" }}
                />
                <RechartsTooltip
                  contentStyle={{
                    backgroundColor: "hsl(var(--card))",
                    border: "1px solid hsl(var(--border))",
                    borderRadius: "8px",
                    fontSize: "12px",
                  }}
                />
              </RadarChart>
            </ResponsiveContainer>
          </div>
        </ShowcaseCard>
      </div>
    </div>
  )
}

/* ══════════════════════════════════════════════
   8. COMPLETE FORM SECTION
   ══════════════════════════════════════════════ */
function CompleteFormSection({ t }: { t: TFunction }) {
  return (
    <div className="space-y-6">
      <SectionHeader
        title={t("guide.completeForm")}
        description={t("guide.completeFormDesc")}
      />

      <Card>
        <CardHeader>
          <CardTitle>{t("survey.settings")}</CardTitle>
          <CardDescription>{t("survey.settingsDesc")}</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-6">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="form-title">
                  {t("survey.title")} <span className="text-destructive">*</span>
                </Label>
                <Input id="form-title" placeholder={t("survey.enterTitle")} />
              </div>
              <div className="space-y-2">
                <Label>{t("survey.category")}</Label>
                <Select>
                  <SelectTrigger>
                    <SelectValue placeholder={t("survey.chooseCategory")} />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="satisfaction">{t("survey.customerSatisfaction")}</SelectItem>
                    <SelectItem value="product">{t("survey.productEval")}</SelectItem>
                    <SelectItem value="service">{t("survey.serviceQuality")}</SelectItem>
                    <SelectItem value="employee">{t("survey.employeeSatisfaction")}</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="form-desc">{t("survey.description")}</Label>
              <Textarea
                id="form-desc"
                placeholder={t("survey.enterDescription")}
                className="leading-relaxed"
              />
              <p className="text-xs text-muted-foreground">{t("survey.descVisibility")}</p>
            </div>

            <Separator />

            <div className="space-y-3">
              <Label>{t("survey.distributionType")}</Label>
              <RadioGroup defaultValue="email">
                <div className="flex items-center gap-2">
                  <RadioGroupItem value="email" id="dist-email" />
                  <Label htmlFor="dist-email">
                    <Mail className="size-4 inline ms-1" />
                    {t("survey.emailDist")}
                  </Label>
                </div>
                <div className="flex items-center gap-2">
                  <RadioGroupItem value="link" id="dist-link" />
                  <Label htmlFor="dist-link">
                    <Upload className="size-4 inline ms-1" />
                    {t("survey.directLink")}
                  </Label>
                </div>
              </RadioGroup>
            </div>

            <div className="flex items-center justify-between">
              <div>
                <Label htmlFor="form-anon">{t("survey.allowAnonymous")}</Label>
                <p className="text-xs text-muted-foreground">{t("survey.anonymousNote")}</p>
              </div>
              <Switch id="form-anon" />
            </div>

            <div className="flex items-center gap-2">
              <Checkbox id="form-notify" />
              <Label htmlFor="form-notify">{t("survey.notifyOnResponse")}</Label>
            </div>
          </div>
        </CardContent>
        <CardFooter className="flex justify-between border-t border-border pt-6">
          <Button variant="ghost">
            <Eye className="size-4 ms-2" />
            {t("common.preview")}
          </Button>
          <div className="flex gap-2">
            <Button variant="outline">{t("survey.saveDraft")}</Button>
            <Button className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground">
              {t("survey.publish")}
            </Button>
          </div>
        </CardFooter>
      </Card>
    </div>
  )
}

/* ══════════════════════════════════════════════
   Sidebar section definition
   ══════════════════════════════════════════════ */
interface SidebarSection {
  id: string
  icon: LucideIcon
  labelKey: string
  count?: number
}

const SECTIONS: SidebarSection[] = [
  { id: "colors", icon: Palette, labelKey: "guide.colorPalette" },
  { id: "buttons", icon: MousePointerClick, labelKey: "guide.buttonsActions", count: 6 },
  { id: "forms", icon: TextCursorInput, labelKey: "guide.formInputs" },
  { id: "data", icon: LayoutDashboard, labelKey: "guide.dataDisplay" },
  { id: "overlays", icon: Layers, labelKey: "guide.overlays" },
  { id: "navigation", icon: Navigation, labelKey: "guide.navigation" },
  { id: "charts", icon: BarChart3, labelKey: "guide.charts", count: 5 },
  { id: "complete-form", icon: FileText, labelKey: "guide.completeForm" },
]

/* ══════════════════════════════════════════════
   Component Guide Page
   ══════════════════════════════════════════════ */
export default function ComponentGuide() {
  const [activeSection, setActiveSection] = useState("colors")
  const [darkMode, setDarkMode] = useState(
    document.documentElement.classList.contains("dark")
  )

  const { t, i18n } = useTranslation()
  const { toggleLang } = useDirection()

  function toggleDark() {
    document.documentElement.classList.toggle("dark")
    setDarkMode(!darkMode)
  }

  function renderActiveSection() {
    switch (activeSection) {
      case "colors":
        return <ColorsSection t={t} />
      case "buttons":
        return <ButtonsSection t={t} />
      case "forms":
        return <FormsSection t={t} />
      case "data":
        return <DataSection t={t} />
      case "overlays":
        return <OverlaysSection t={t} />
      case "navigation":
        return <NavigationSection t={t} />
      case "charts":
        return <ChartsSection t={t} />
      case "complete-form":
        return <CompleteFormSection t={t} />
      default:
        return <ColorsSection t={t} />
    }
  }

  return (
    <TooltipProvider>
      <div className="min-h-screen bg-background text-foreground">
        {/* Sticky Header */}
        <header className="sticky top-0 z-50 border-b border-border bg-background/80 backdrop-blur-md">
          <div className="flex items-center justify-between px-6 py-3">
            <div className="flex items-center gap-4">
              <h1 className="text-lg font-bold">{t("guide.pageTitle")}</h1>
              <Badge variant="outline" className="text-xs">
                {t("guide.componentCount")}
              </Badge>
            </div>
            <div className="flex items-center gap-2">
              <Button variant="ghost" size="sm" onClick={toggleLang}>
                {i18n.language === "ar" ? "EN" : "\u0639\u0631\u0628\u064A"}
              </Button>
              <Button
                variant="ghost"
                size="icon"
                onClick={toggleDark}
                aria-label={t("guide.toggleTheme")}
              >
                {darkMode ? (
                  <Sun className="size-4" />
                ) : (
                  <Moon className="size-4" />
                )}
              </Button>
            </div>
          </div>
        </header>

        {/* Sidebar + Main Content */}
        <div className="flex">
          {/* Sidebar */}
          <aside className="w-64 shrink-0 border-e border-border bg-card sticky top-[49px] h-[calc(100vh-49px)] overflow-y-auto">
            {/* Brand mark */}
            <div className="p-4 pb-2">
              <div className="flex items-center gap-3">
                <div className="size-8 rounded-lg bg-primary flex items-center justify-center">
                  <span className="text-sm font-bold text-primary-foreground">N</span>
                </div>
                <div>
                  <p className="text-sm font-bold">Nabdat</p>
                  <p className="text-[10px] text-muted-foreground">Design System</p>
                </div>
              </div>
            </div>
            <Separator className="my-2" />

            <nav className="p-3 space-y-1">
              {SECTIONS.map((section) => (
                <button
                  key={section.id}
                  onClick={() => setActiveSection(section.id)}
                  className={cn(
                    "w-full flex items-center gap-3 px-3 py-2 rounded-lg text-sm transition-colors",
                    activeSection === section.id
                      ? "bg-accent text-accent-foreground font-medium"
                      : "text-muted-foreground hover:bg-muted hover:text-foreground"
                  )}
                >
                  <section.icon className="size-4" />
                  <span className="flex-1 text-start">{t(section.labelKey)}</span>
                  {section.count != null && (
                    <span className={cn(
                      "text-[10px] tabular-nums min-w-5 text-center rounded-full px-1.5 py-0.5",
                      activeSection === section.id
                        ? "bg-primary/10 text-primary"
                        : "bg-muted text-muted-foreground"
                    )}>
                      {section.count}
                    </span>
                  )}
                </button>
              ))}
            </nav>
          </aside>

          {/* Main content */}
          <main className="flex-1 p-6 lg:p-8 max-w-5xl">
            {renderActiveSection()}

            {/* Footer */}
            <footer className="border-t border-border mt-12 pt-8 pb-12 text-center">
              <p className="text-sm text-muted-foreground">
                {t("guide.footerTitle")}
              </p>
              <p className="text-xs text-muted-foreground mt-1">
                {t("guide.footerSub")}
              </p>
            </footer>
          </main>
        </div>
      </div>
    </TooltipProvider>
  )
}
