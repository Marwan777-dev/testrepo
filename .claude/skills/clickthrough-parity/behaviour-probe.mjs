// Behaviour probe for SCR-04, run identically against both sides.
import { chromium } from "playwright"

const [, , base, mode, token] = process.argv

// The click-through's auth is in-memory: a page.goto of an in-app route bounces to /login, so
// navigate CLIENT-SIDE (pushState + popstate), the same way capture.mjs does.
async function gotoRoute(page, route) {
  await page.evaluate((r) => {
    window.history.pushState({}, "", r)
    window.dispatchEvent(new PopStateEvent("popstate"))
  }, route)
  await page.waitForTimeout(1200)
}

const browser = await chromium.launch()
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } })
const out = {}

// Auth mirrors capture.mjs exactly. Stub: the Sign In click is what actually advances the
// click-through's fake login — the field fills silently no-op on its #login-email/#login-password
// ids, and it is "Skip for now" that authenticates. Token: seed sessionStorage, then one full nav.
if (mode === "stub") {
  await page.goto(`${base}/login`, { waitUntil: "networkidle" }).catch(() => {})
  await page.locator("#login-email").fill("demo@nabadat.local").catch(() => {})
  await page.locator("#login-password").fill("Demo1234!").catch(() => {})
  await page.getByRole("button", { name: /sign in/i }).first().click().catch(() => {})
  await page.getByRole("button", { name: /skip/i }).first().waitFor({ timeout: 6000 }).catch(() => {})
  await page.getByRole("button", { name: /skip/i }).first().click().catch(() => {})
  await page.waitForTimeout(900)
} else {
  await page.goto(`${base}/login`, { waitUntil: "domcontentloaded" }).catch(() => {})
  await page.evaluate((t) => window.sessionStorage.setItem("session_token", t), token)
  await page.goto(`${base}/`, { waitUntil: "networkidle", timeout: 20000 }).catch(() => {})
}

await gotoRoute(page, "/integration-hub/service-channels/new")
await page.waitForSelector("#channel-id", { timeout: 15000 })

// B1 — channel ID sanitises live and caps at 19 (AC-S4-01).
await page.locator("#channel-id").fill("Kiosk Front!! Desk_2026 North Wing Branch")
out.b1_sanitizedId = await page.locator("#channel-id").inputValue()

// B2/B3 — Supported gates Required, and the contract summary follows (AC-S4-03 / FR-S4-03).
const sw = page.locator("[data-testid^='supported-'][role='switch']").first()
const cb = page.locator("[data-testid^='required-'][role='checkbox']").first()
const summary = () => page.locator("[data-testid=contract-summary]").innerText()

out.b2_requiredDisabled_beforeSupported = await cb.isDisabled()
out.b0_summary_initial = (await summary()).replace(/\s+/g, " ").trim()

await sw.click(); await page.waitForTimeout(250)
out.b2_requiredDisabled_afterSupported = await cb.isDisabled()
out.b3_summary_afterSupported = (await summary()).replace(/\s+/g, " ").trim()

await cb.click(); await page.waitForTimeout(250)
out.b3_summary_afterRequired = (await summary()).replace(/\s+/g, " ").trim()

// FR-S4-04 — clearing Supported force-clears Required in the same update.
await sw.click(); await page.waitForTimeout(250)
out.b4_requiredDisabled_afterUnsupported = await cb.isDisabled()
out.b4_requiredChecked_afterUnsupported = await cb.isChecked()
out.b4_summary_afterUnsupported = (await summary()).replace(/\s+/g, " ").trim()

// B5 — submit with the form empty: does client validation surface inline errors?
await gotoRoute(page, "/integration-hub/service-channels/new")
await page.waitForSelector("#channel-id", { timeout: 15000 })
await page.getByRole("button", { name: /create channel/i }).click()
await page.waitForTimeout(700)
out.b5_alertCount = await page.getByRole("alert").count()
out.b5_alertTexts = (await page.getByRole("alert").allInnerTexts()).map((s) => s.trim())
out.b5_stillOnForm = page.url().includes("/service-channels/new")

// B6 — the contract filter narrows the parameter table.
const rowsBefore = await page.locator("[data-testid^='supported-'][role='switch']").count()
await page.locator("#contract-filter").fill("zzzzz-no-such-parameter")
await page.waitForTimeout(400)
const rowsAfter = await page.locator("[data-testid^='supported-'][role='switch']").count()
out.b6_filter = { rowsBefore, rowsAfter, emptyText: await page.locator("table").last().innerText().then((t) => t.split("\n").pop().trim()) }

console.log(JSON.stringify(out, null, 2))
await browser.close()
