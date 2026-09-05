import { useRef, useEffect, memo } from "react"
import "@/styles/surveyjs-nabadat.css"

// SurveyJS is loaded via dynamic import below to avoid hard build failure
// if packages are not yet installed. The canvas shows a placeholder until loaded.

let SurveyCreatorClass: any = null
let SurveyCreatorComponentClass: any = null
let ComponentCollectionClass: any = null
let surveyJsLoaded = false
let surveyJsLoadFailed = false

async function loadSurveyJs() {
  if (surveyJsLoaded || surveyJsLoadFailed) return
  try {
    const creatorCore = await import("survey-creator-core")
    const creatorReact = await import("survey-creator-react")
    const surveyCore = await import("survey-core")

    // Import SurveyJS CSS
    await import("survey-core/survey-core.min.css")
    await import("survey-creator-core/survey-creator-core.min.css")

    SurveyCreatorClass = creatorCore.SurveyCreatorModel
    SurveyCreatorComponentClass = creatorReact.SurveyCreatorComponent
    ComponentCollectionClass = surveyCore.ComponentCollection

    // Register the Nabadat KPI question type
    if (ComponentCollectionClass?.Instance) {
      try {
        ComponentCollectionClass.Instance.add({
          name: "kpiQuestion",
          title: "سؤال KPI",
          questionJSON: {
            type: "rating",
            rateMin: 1,
            rateMax: 5,
          },
          onCreated: (q: any) => {
            q.title = q.title || "كيف تقيّم هذه التجربة؟"
          },
        })
      } catch {
        // Already registered — fine
      }
    }

    surveyJsLoaded = true
  } catch {
    surveyJsLoadFailed = true
  }
}

export interface SurveyBuilderCanvasHandle {
  getJson: () => object
}

interface SurveyBuilderCanvasProps {
  initialJson?: object
  isTemplate?: boolean
  locale?: string
  onJsonChange?: (json: object) => void
  onQuestionSelected?: (questionName: string | null, isKpi: boolean) => void
}

// ── Mock builder (shown while SurveyJS loads or if it fails) ─────────────────
const MOCK_QUESTION_TYPES = [
  { type: "kpiQuestion", label: "سؤال KPI", labelEn: "KPI Question", icon: "📊", isKpi: true },
  { type: "rating",      label: "تقييم",     labelEn: "Rating",       icon: "⭐" },
  { type: "text",        label: "نص قصير",   labelEn: "Short Text",   icon: "T" },
  { type: "comment",     label: "نص طويل",   labelEn: "Long Text",    icon: "¶" },
  { type: "radiogroup",  label: "اختيار واحد", labelEn: "Single Choice", icon: "●" },
  { type: "checkbox",    label: "متعدد",     labelEn: "Multi-Choice", icon: "☑" },
  { type: "dropdown",    label: "قائمة منسدلة", labelEn: "Dropdown",  icon: "▾" },
  { type: "boolean",     label: "نعم / لا",  labelEn: "Yes / No",    icon: "?" },
]

interface MockQuestion {
  name: string
  type: string
  title: string
  isKpi: boolean
}

function MockBuilder({
  questions,
  selectedQuestion,
  onSelect,
  onAdd,
  locale,
}: {
  questions: MockQuestion[]
  selectedQuestion: string | null
  onSelect: (name: string, isKpi: boolean) => void
  onAdd: (type: string, isKpi: boolean) => void
  locale: string
}) {
  const isAr = locale === "ar"

  return (
    <div className="flex h-full bg-background">
      {/* Toolbox */}
      <div className="w-56 shrink-0 border-e border-border bg-card overflow-y-auto">
        <div className="p-3 border-b border-border">
          <p className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">
            {isAr ? "أنواع الأسئلة" : "Question Types"}
          </p>
        </div>
        <div className="p-2 space-y-1">
          {MOCK_QUESTION_TYPES.map((qt) => (
            <button
              key={qt.type}
              onClick={() => onAdd(qt.type, qt.isKpi ?? false)}
              className="w-full flex items-center gap-2.5 rounded-md px-3 py-2 text-sm text-start hover:bg-accent transition-colors group"
            >
              <span
                className={`size-7 rounded flex items-center justify-center text-xs font-bold shrink-0 ${
                  qt.isKpi
                    ? "bg-nb-cyan-100 text-nb-cyan dark:bg-nb-cyan-900/40"
                    : "bg-muted text-muted-foreground"
                }`}
              >
                {qt.icon}
              </span>
              <span className="text-xs font-medium text-foreground">
                {isAr ? qt.label : qt.labelEn}
              </span>
            </button>
          ))}
        </div>
      </div>

      {/* Canvas */}
      <div className="flex-1 overflow-y-auto p-6 space-y-3">
        {questions.length === 0 && (
          <div className="flex flex-col items-center justify-center h-64 rounded-xl border-2 border-dashed border-border text-center">
            <p className="text-sm font-medium text-muted-foreground mb-1">
              {isAr ? "أضف أسئلتك من القائمة الجانبية" : "Add questions from the toolbox"}
            </p>
            <p className="text-xs text-muted-foreground">
              {isAr ? "أو اسحب وأفلت هنا" : "Or drag and drop here"}
            </p>
          </div>
        )}
        {questions.map((q, idx) => (
          <div
            key={q.name}
            onClick={() => onSelect(q.name, q.isKpi)}
            className={`rounded-lg border p-4 cursor-pointer transition-all ${
              selectedQuestion === q.name
                ? "border-primary ring-2 ring-primary/20 bg-primary/5"
                : "border-border hover:border-primary/40 bg-card hover:shadow-sm"
            }`}
          >
            <div className="flex items-start justify-between gap-3">
              <div className="flex items-start gap-3 flex-1 min-w-0">
                <span className="text-xs font-bold text-muted-foreground mt-0.5 tabular-nums shrink-0">
                  {idx + 1}.
                </span>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    {q.isKpi && (
                      <span className="text-xs bg-nb-cyan-100 text-nb-cyan dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200 px-1.5 py-0.5 rounded font-medium">
                        KPI
                      </span>
                    )}
                    <span className="text-xs text-muted-foreground uppercase tracking-wide">
                      {MOCK_QUESTION_TYPES.find((t) => t.type === q.type)?.[isAr ? "label" : "labelEn"] ?? q.type}
                    </span>
                  </div>
                  <p className="text-sm font-medium text-foreground">{q.title}</p>
                  <p className="text-xs text-muted-foreground mt-1 font-mono">{q.name}</p>
                </div>
              </div>
            </div>
          </div>
        ))}

        {/* Add question button */}
        <button
          onClick={() => onAdd("text", false)}
          className="w-full flex items-center justify-center gap-2 rounded-xl border-2 border-dashed border-border py-4 text-sm text-muted-foreground hover:border-primary hover:text-primary transition-colors"
        >
          <span className="text-lg">+</span>
          <span>{isAr ? "إضافة سؤال" : "Add Question"}</span>
        </button>
      </div>
    </div>
  )
}

// ── Actual canvas component ───────────────────────────────────────────────────
import { useState } from "react"

let questionCounter = 1

export const SurveyBuilderCanvas = memo(function SurveyBuilderCanvas({
  initialJson,
  isTemplate = false,
  locale = "ar",
  onJsonChange,
  onQuestionSelected,
}: SurveyBuilderCanvasProps) {
  const [loaded, setLoaded] = useState(false)
  const creatorRef = useRef<any>(null)

  // Mock question state for fallback
  const [mockQuestions, setMockQuestions] = useState<MockQuestion[]>(() => {
    if (!initialJson) return []
    try {
      const json = initialJson as any
      const elements = json?.pages?.[0]?.elements ?? []
      return elements.map((el: any) => ({
        name: el.name,
        type: el.type,
        title: typeof el.title === "object" ? (el.title.ar ?? el.title.en ?? el.name) : (el.title ?? el.name),
        isKpi: el.type === "kpiQuestion",
      }))
    } catch {
      return []
    }
  })
  const [selectedMock, setSelectedMock] = useState<string | null>(null)

  useEffect(() => {
    loadSurveyJs().then(() => {
      if (surveyJsLoadFailed) return
      if (!SurveyCreatorClass || !SurveyCreatorComponentClass) return

      try {
        const creator = new SurveyCreatorClass({
          showLogicTab: false,
          showTranslationTab: false,
          showThemeTab: false,
          showJSONEditorTab: !isTemplate,
          isAutoSave: false,
          locale: locale,
        })

        if (initialJson) {
          creator.JSON = initialJson
        }

        creator.sidebar.visible = false

        creator.onModified.add(() => {
          onJsonChange?.(creator.JSON)
        })

        creator.onSelectedElementChanged.add((_sender: any, options: any) => {
          const el = options.newSelectedElement
          if (el && el.getType) {
            const isKpi = el.getType() === "kpiQuestion"
            onQuestionSelected?.(el.name ?? null, isKpi)
          } else {
            onQuestionSelected?.(null, false)
          }
        })

        creatorRef.current = creator
        setLoaded(true)
      } catch {
        // SurveyJS failed to initialize — fall through to mock builder
      }
    })
  }, [])

  function handleMockSelect(name: string, isKpi: boolean) {
    setSelectedMock(name)
    onQuestionSelected?.(name, isKpi)
  }

  function handleMockAdd(type: string, isKpi: boolean) {
    const isAr = locale === "ar"
    const defaultTitle = isAr
      ? `سؤال ${questionCounter++}`
      : `Question ${questionCounter++}`
    const name = `q_${type}_${Date.now()}`
    const newQ: MockQuestion = { name, type, title: defaultTitle, isKpi }
    setMockQuestions((prev) => {
      const updated = [...prev, newQ]
      const json = {
        pages: [
          {
            name: "page1",
            elements: updated.map((q) => ({ type: q.type, name: q.name, title: q.title })),
          },
        ],
      }
      onJsonChange?.(json)
      return updated
    })
    setSelectedMock(name)
    onQuestionSelected?.(name, isKpi)
  }

  // Show real SurveyJS if loaded
  if (loaded && creatorRef.current && SurveyCreatorComponentClass) {
    const SurveyCreatorComp = SurveyCreatorComponentClass
    return (
      <div className="nabadat-survey-builder h-full">
        <SurveyCreatorComp creator={creatorRef.current} />
      </div>
    )
  }

  // Fallback mock builder (loads faster or when SurveyJS unavailable)
  return (
    <MockBuilder
      questions={mockQuestions}
      selectedQuestion={selectedMock}
      onSelect={handleMockSelect}
      onAdd={handleMockAdd}
      locale={locale}
    />
  )
})
