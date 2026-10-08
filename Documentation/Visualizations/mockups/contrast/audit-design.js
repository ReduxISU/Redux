// Drives design.html through every tab, problem, view and toggle in light and dark, collecting contrast failures.
// Usage (from mockups/): node contrast/audit-design.js [page.html] [failures.json]   Needs Playwright: npm i -D playwright, or set PLAYWRIGHT to its path.
// Exits 1 if anything fails: text under 4.5:1 (3:1 when large), strokes, small marks and control borders under 3:1.
const path = require("path"), fs = require("fs");
const { chromium } = require(process.env.PLAYWRIGHT || "playwright");
const [file = path.join(__dirname, "..", "design.html"), outFile] = process.argv.slice(2);
const TABS = [["clause", ""], ["auto", "a-"], ["bip", "b-"], ["graph", "g-"], ["flow", "fl-"], ["layered", "l-"], ["packing", "pk-"], ["schedule", "sc-"],
  ["quantum", "qc-"], ["board", "bd-"], ["table", "tb-"], ["misc", "ms-"], ["cgram", "cg-"], ["reduction", "rd-"], ["machine", "mc-"], ["dmap", "dm-"], ["search", "st-"]];
const only = process.env.TABS ? process.env.TABS.split(",") : null;

(async () => {
  const browser = await chromium.launch();
  const all = [];
  for (const theme of ["light", "dark"]) {
    const page = await browser.newPage({ viewport: { width: 1280, height: 900 }, colorScheme: theme });
    await page.goto("file:///" + path.resolve(file).replace(/\\/g, "/"));
    await page.evaluate(t => document.documentElement.setAttribute("data-theme", t), theme);
    await page.addScriptTag({ path: path.join(__dirname, "audit-lib.js") });
    await page.addStyleTag({ content: "*,*::before,*::after{transition:none!important;animation:none!important}" });
    const audit = async ctx => { await page.waitForTimeout(60); all.push(...await page.evaluate(([s, c]) => window.__audit(s, c), [`#tab-${ctx.tab}`, { theme, ...ctx }])); };
    const opts = sel => page.$$eval(`${sel} option`, os => os.map(o => o.value));
    const exists = sel => page.$(sel).then(Boolean);
    for (const [tab, p] of TABS) {
      if (only && !only.includes(tab)) continue;
      await page.click(`#t-${tab}`); await page.waitForTimeout(100);
      const probSel = `#${p}prob`, exSel = tab === "reduction" ? "#rd-ex" : tab === "clause" ? "#inst" : `#${p}ex`;
      const groups = tab === "reduction" ? await opts("#rd-red") : (await exists(probSel)) ? await opts(probSel) : [null];
      for (const g of groups) {
        if (g !== null) await page.selectOption(tab === "reduction" ? "#rd-red" : probSel, g);
        const exs = (await exists(exSel)) ? (await opts(exSel)).slice(0, 3) : [null];
        for (const ex of exs) {
          if (ex !== null) await page.selectOption(exSel, ex).catch(() => {});
          const base = { tab, problem: g, example: ex };
          if (tab === "reduction") {
            for (const ph of ["build", "solve", "map"]) {
              await page.click(`#rd-phases [data-phase="${ph}"]`);
              for (let i = 0; i < 3; i++) if (!(await page.locator("#rd-next").isDisabled())) await page.click("#rd-next");
              await audit({ ...base, view: ph });
            }
            continue;
          }
          for (const v of ["instance", "solution"]) { await page.click(`#${p}m-${v}`); await audit({ ...base, view: v }); }
          await page.click(`#${p}m-steps`);
          for (let i = 0; i < 4; i++) if (!(await page.locator(`#${p}next`).isDisabled())) await page.click(`#${p}next`);
          await audit({ ...base, view: "steps-4" });
          const slider = page.locator(`#tab-${tab} input[type=range]`).first();
          if (await slider.count()) { await slider.evaluate(s => { s.value = s.max; s.dispatchEvent(new Event("input", { bubbles: true })); s.dispatchEvent(new Event("change", { bubbles: true })); }); await audit({ ...base, view: "steps-last" }); }
        }
        // every non-view toggle, once per problem, in Solved and mid-Steps
        const toggles = await page.$$eval(`#tab-${tab} .seg button`, bs => bs.filter(b => !/-m-|^m-/.test(b.id) && !b.closest("[hidden]")).map(b => b.id || b.textContent.trim()));
        for (const t of toggles) {
          const loc = t.match(/^[\w-]+$/) && await exists(`#${t}`) ? page.locator(`#${t}`) : page.locator(`#tab-${tab} .seg button`, { hasText: t }).first();
          if (!(await loc.isVisible()) || await loc.isDisabled()) continue;
          await loc.click();
          if (tab !== "reduction") { await page.click(`#${p}m-solution`); await audit({ tab, problem: g, view: "solution", toggle: t }); await page.click(`#${p}m-steps`); for (let i = 0; i < 3; i++) if (!(await page.locator(`#${p}next`).isDisabled())) await page.click(`#${p}next`); }
          await audit({ tab, problem: g, view: "steps", toggle: t });
        }
      }
      // the clause tab also has an edges select, shown only for the clause view
      if (tab === "clause") {
        await page.click("#v-clause");
        for (const e of await opts("#edges")) { await page.selectOption("#edges", e); await page.click("#m-solution"); await audit({ tab, view: "solution", toggle: "edges=" + e }); }
      }
    }
    await page.close();
  }
  await browser.close();
  // one line per element kind, with the worst contrast seen
  const g = new Map();
  for (const f of all) { const k = `${f.theme} | ${f.tab} | ${f.kind} | ${f.what}`; const e = g.get(k) || { n: 0, min: 99, ex: "" }; e.n++; if (f.ratio < e.min) { e.min = f.ratio; e.ex = `${f.fg} on ${f.bg} = ${f.ratio}:1, needs ${f.need}:1${f.text ? ` ("${f.text}")` : ""}`; } g.set(k, e); }
  for (const [k, e] of [...g].sort()) console.log(`${k} | ${e.ex} | x${e.n}`);
  console.log(g.size ? `${g.size} kinds of failure (${all.length} elements)` : "no contrast failures");
  if (outFile) fs.writeFileSync(outFile, JSON.stringify(all, null, 1));
  process.exitCode = g.size ? 1 : 0;
})().catch(e => { console.error(e); process.exit(1); });
