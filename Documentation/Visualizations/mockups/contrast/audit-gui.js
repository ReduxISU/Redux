// Drives redux-gui.html through every problem and visualization choice in light and dark, collecting contrast failures.
// Usage (from mockups/): node contrast/audit-gui.js [page.html] [failures.json]   Needs Playwright: npm i -D playwright, or set PLAYWRIGHT to its path.
// Exits 1 if anything fails: text under 4.5:1 (3:1 when large), strokes, small marks and control borders under 3:1.
const path = require("path"), fs = require("fs");
const { chromium } = require(process.env.PLAYWRIGHT || "playwright");
const [file = path.join(__dirname, "..", "redux-gui.html"), outFile] = process.argv.slice(2);

(async () => {
  const browser = await chromium.launch();
  const all = [];
  for (const theme of ["light", "dark"]) {
    const page = await browser.newPage({ viewport: { width: 1280, height: 900 }, colorScheme: theme });
    page.on("pageerror", e => console.error("pageerror", e.message));
    await page.goto("file:///" + path.resolve(file).replace(/\\/g, "/"));
    await page.evaluate(t => document.documentElement.setAttribute("data-theme", t), theme);
    await page.addScriptTag({ path: path.join(__dirname, "audit-lib.js") });
    await page.addStyleTag({ content: "*,*::before,*::after{transition:none!important;animation:none!important}" });
    const audit = async ctx => { await page.waitForTimeout(60); all.push(...await page.evaluate(([s, c]) => window.__audit(s, c), ["body", { theme, tab: "gui", ...ctx }])); };
    const keys = await page.$$eval("#probMenu li[role=option]", ls => ls.map(l => l.dataset.key));
    for (const key of keys) {
      await page.evaluate(k => setProblem(k), key); await page.waitForTimeout(80);
      const vizCount = await page.$$eval("#vizMenu li[role=option]", ls => ls.length);
      for (let v = 0; v < Math.max(1, vizCount); v++) {
        if (vizCount) { await page.click("#vizPick"); await page.locator("#vizMenu li[role=option]").nth(v).click(); await page.waitForTimeout(80); }
        const viz = await page.textContent("#vizVal");
        const ctx = { problem: key, view: viz };
        await audit({ ...ctx, step: "first" });
        for (let i = 0; i < 3; i++) if (await page.locator("#next").isEnabled()) await page.click("#next");
        await audit({ ...ctx, step: "3" });
        if (await page.locator("#last").isEnabled()) await page.click("#last");
        await audit({ ...ctx, step: "last" });
        // each visible switch, flipped and flipped back
        const sw = page.locator(".sw input:not(:disabled)");
        for (let i = 0; i < await sw.count(); i++) {
          if (!(await sw.nth(i).isVisible())) continue;
          await sw.nth(i).click({ force: true }); await audit({ ...ctx, step: "last", toggle: "switch" + i }); await sw.nth(i).click({ force: true });
        }
        // hover the first node-like element, to catch hover-trace fading
        const node = page.locator(".viz:not([hidden]) svg g[tabindex]").first();
        if (await node.count() && await node.isVisible()) { await node.hover(); await audit({ ...ctx, step: "last", toggle: "hover" }); await page.mouse.move(0, 0); }
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
