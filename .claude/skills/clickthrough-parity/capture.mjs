// clickthrough-parity · capture.mjs
// Screenshot + structural/text fingerprint for a list of routes on a running app, so the
// click-through and the real frontend can be compared on the same footing.
//
// One-time setup (in THIS skill folder, so it is resolvable — the dev repo root has no package.json):
//   cd .claude/skills/clickthrough-parity && npm install && npx playwright install chromium
//
// Usage:
//   node capture.mjs --base http://localhost:5175 --auth stub \
//        --routes '/journeys,/journeys/j1/scoring' --out ./scratch/ct
//   node capture.mjs --base http://localhost:5173 --auth token --token "$SESSION_TOKEN" \
//        --routes '/journeys' --out ./scratch/app --dir rtl
//
// Flags:
//   --base <url>            (required) running app origin
//   --routes <a,b,c>        (required) comma-separated route paths
//   --out <dir>             (required) output directory (created)
//   --auth stub|token|none  default none
//   --email / --pass        stub-login credentials (default any valid-looking values)
//   --token <t>             session token to inject (auth=token)
//   --token-key <k>         sessionStorage key for the token (default: session_token)
//   --dir ltr|rtl           reading direction to force (default ltr)
//   --themes light,dark     which themes to screenshot (default light,dark)
//   --width / --height      viewport (default 1440 x 1000)

import { mkdirSync, writeFileSync } from "node:fs"
import { join } from "node:path"

function arg(name, def) {
  const i = process.argv.indexOf(`--${name}`)
  return i >= 0 && process.argv[i + 1] && !process.argv[i + 1].startsWith("--") ? process.argv[i + 1] : def
}

const BASE = arg("base")
const ROUTES = (arg("routes") || "").split(",").map((s) => s.trim()).filter(Boolean)
const OUT = arg("out")
const AUTH = arg("auth", "none")
const EMAIL = arg("email", "reviewer@qbs.jo")
const PASS = arg("pass", "Review@2026")
const TOKEN = arg("token", "")
const TOKEN_KEY = arg("token-key", "session_token")
const DIR = arg("dir", "ltr")
const THEMES = (arg("themes", "light,dark")).split(",").map((s) => s.trim()).filter(Boolean)
const WIDTH = +arg("width", "1440")
const HEIGHT = +arg("height", "1000")

if (!BASE || !ROUTES.length || !OUT) {
  console.error("capture.mjs: --base, --routes and --out are required")
  process.exit(2)
}
mkdirSync(OUT, { recursive: true })

const { chromium } = await import("playwright")
const browser = await chromium.launch()
const ctx = await browser.newContext({ viewport: { width: WIDTH, height: HEIGHT }, ignoreHTTPSErrors: true })
const page = await ctx.newPage()
const errors = []
page.on("pageerror", (e) => errors.push(String(e)))

const slug = (r) => (r.replace(/^\//, "").replace(/[^a-z0-9]+/gi, "_") || "root").toLowerCase()

async function forceTheme(theme) {
  await page.evaluate((t) => {
    try {
      localStorage.setItem("theme", t)
    } catch {}
    const el = document.documentElement
    el.classList.toggle("dark", t === "dark")
    el.style.colorScheme = t
  }, theme)
}
async function forceDir(dir) {
  await page.evaluate((d) => {
    const el = document.documentElement
    el.setAttribute("dir", d)
    el.setAttribute("lang", d === "rtl" ? "ar" : "en")
    try {
      localStorage.setItem("i18nextLng", d === "rtl" ? "ar" : "en")
    } catch {}
  }, dir)
}

async function authenticate() {
  if (AUTH === "none") return
  if (AUTH === "token") {
    // Land on the app once so the origin exists, seed the token, then it's authenticated.
    await page.goto(BASE + "/login", { waitUntil: "domcontentloaded" }).catch(() => {})
    await page.evaluate(([k, v]) => sessionStorage.setItem(k, v), [TOKEN_KEY, TOKEN])
    return
  }
  // stub: the click-through's fake login — fill anything, Sign In, then "Skip for now".
  await page.goto(BASE + "/login", { waitUntil: "networkidle" }).catch(() => {})
  await page.locator('input[type="email"]').first().fill(EMAIL).catch(() => {})
  await page.locator('input[type="password"]').first().fill(PASS).catch(() => {})
  await page.getByRole("button", { name: /sign in/i }).first().click().catch(() => {})
  await page.getByRole("button", { name: /skip/i }).first().waitFor({ timeout: 6000 }).catch(() => {})
  await page.getByRole("button", { name: /skip/i }).first().click().catch(() => {})
  await page.waitForTimeout(600)
}

// Structural + text fingerprint of the current page's main content.
async function fingerprint() {
  return page.evaluate(() => {
    const root = document.querySelector("main") || document.body
    const txt = (el) => (el?.textContent || "").replace(/\s+/g, " ").trim()
    const vis = (el) => {
      const r = el.getBoundingClientRect()
      const s = getComputedStyle(el)
      return r.width > 0 && r.height > 0 && s.visibility !== "hidden" && s.display !== "none"
    }
    const many = (sel) => [...root.querySelectorAll(sel)].filter(vis)

    const controlType = (el) => {
      const role = el.getAttribute("role")
      if (role === "switch") return "switch"
      if (role === "checkbox") return "checkbox"
      if (role === "tab") return "tab"
      const tag = el.tagName.toLowerCase()
      if (tag === "select") return "select"
      if (tag === "textarea") return "textarea"
      if (tag === "input") return `input:${el.getAttribute("type") || "text"}`
      return tag
    }
    const labelFor = (el) => {
      if (el.id) {
        const l = document.querySelector(`label[for="${CSS.escape(el.id)}"]`)
        if (l) return txt(l)
      }
      const al = el.getAttribute("aria-label")
      if (al) return al
      const wrap = el.closest("label")
      if (wrap) return txt(wrap).slice(0, 60)
      return ""
    }

    const controls = many(
      'input, textarea, select, [role="switch"], [role="checkbox"], [role="tab"]',
    ).map((el) => ({ type: controlType(el), label: labelFor(el) }))

    return {
      title: txt(root.querySelector("h1")),
      headings: many("h1, h2, h3, h4").map(txt).filter(Boolean),
      labels: many("label").map(txt).filter(Boolean),
      placeholders: many("input[placeholder], textarea[placeholder]").map((el) => ({
        placeholder: el.getAttribute("placeholder"),
        label: labelFor(el),
      })),
      controls,
      buttons: many("button, a[role=button], [role=tab]")
        .map((el) => txt(el) || el.getAttribute("aria-label") || "")
        .filter(Boolean),
      tableHeaders: many("th").map(txt).filter(Boolean),
      // Full readable text of the content — the primary surface for text/placeholder diffing.
      visibleText: (root.innerText || "").replace(/\s+\n/g, "\n").trim(),
    }
  })
}

// Client-side navigation preserves in-memory auth (the click-through logs out on a full page load)
// and works with token/cookie auth too, so use it for every route once we're inside the app.
async function gotoRoute(route) {
  await page.evaluate((r) => {
    window.history.pushState({}, "", r)
    window.dispatchEvent(new PopStateEvent("popstate"))
  }, route)
}

await authenticate()
// Land inside the app once. Stub auth already left us on an in-app page; token/none need one full
// navigation so the AuthGuard reads the seeded session. (A full nav here would reset the
// click-through's in-memory auth, so skip it for stub.)
if (AUTH !== "stub") await page.goto(BASE + "/", { waitUntil: "networkidle", timeout: 20000 }).catch(() => {})

const results = []
for (const route of ROUTES) {
  const s = slug(route)
  await gotoRoute(route)
  await page.waitForLoadState("networkidle").catch(() => {})
  await forceDir(DIR)
  await page.waitForTimeout(500)

  for (const theme of THEMES) {
    await forceTheme(theme)
    await page.waitForTimeout(300)
    await page.screenshot({ path: join(OUT, `${s}.${theme}.png`), fullPage: true })
  }
  await forceTheme(THEMES[0])
  const fp = await fingerprint()
  writeFileSync(join(OUT, `${s}.json`), JSON.stringify({ route, dir: DIR, ...fp }, null, 2))
  results.push({ route, slug: s, headings: fp.headings.length, controls: fp.controls.length })
  console.log(`captured ${route} -> ${s} (${fp.headings.length} headings, ${fp.controls.length} controls)`)
}

if (errors.length) console.log("PAGE ERRORS:", errors.slice(0, 5))
await browser.close()
console.log(`\ndone: ${results.length} route(s) -> ${OUT}`)
