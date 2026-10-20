// Browser check of formula.js's labels with real fonts (Playwright; set PLAYWRIGHT like the contrast audits): on the Formula
// tab (every problem and example) and the SAT ↦ 3SAT reduction, every literal label's text box is at least 11 px tall on
// screen, every subscript is at least 75% of its label's size, and a False literal's strike line never crosses a subscript
// glyph. Usage (from mockups/): node test-formula-browser.js [page.html]
const path = require("path");
let chromium;
try { ({ chromium } = require(process.env.PLAYWRIGHT || "playwright")); } catch { console.log("SKIPPED: Playwright not found (set PLAYWRIGHT)"); process.exit(0); }
const file = process.argv[2] || path.join(__dirname, "design.html");

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
  await page.goto("file:///" + path.resolve(file).replace(/\\/g, "/"));
  await page.evaluate(() => document.fonts && document.fonts.ready);
  const fails = [];
  let checked = 0;
  const audit = async (root, where) => {
    const r = await page.evaluate(sel => {
      const out = [], svg = document.querySelector(sel);
      const scale = svg.getBoundingClientRect().width / svg.viewBox.baseVal.width;
      let n = 0;
      for (const g of svg.querySelectorAll("g.fm-lit")) {
        if (getComputedStyle(g).visibility === "hidden") continue;
        n++;
        const t = g.querySelector("text"), name = g.getAttribute("aria-label");
        const h = t.getBoundingClientRect().height;
        if (h < 11) out.push(`${name}: label ${h.toFixed(1)} px tall on screen`);
        const sub = t.querySelector("tspan.sub");
        if (!sub) continue;
        const base = parseFloat(getComputedStyle(t).fontSize), size = parseFloat(getComputedStyle(sub).fontSize);
        if (size < 0.75 * base) out.push(`${name}: subscript ${size}px under 75% of ${base}px`);
        if (size * scale < 9) out.push(`${name}: subscript ${(size * scale).toFixed(1)} px on screen`);
        // every subscript glyph's box, in the svg's user units
        const nBase = t.querySelector("tspan.base").textContent.length;
        const line = g.querySelector("line.strike");
        g.classList.add("False"); // the strike as False draws it
        const lb = line.getBBox();
        for (let i = 0; i < sub.textContent.length; i++) {
          const e = t.getExtentOfChar(nBase + i);
          const crosses = lb.x + lb.width > e.x && lb.x < e.x + e.width && lb.y + lb.height >= e.y && lb.y <= e.y + e.height;
          if (crosses) out.push(`${name}: the strike (x ${lb.x.toFixed(1)}–${(lb.x + lb.width).toFixed(1)}) crosses subscript glyph ${i} (x ${e.x.toFixed(1)}–${(e.x + e.width).toFixed(1)})`);
        }
        g.classList.remove("False");
      }
      return { out, n };
    }, root);
    checked += r.n;
    fails.push(...r.out.map(x => `${where}: ${x}`));
  };
  await page.click("#t-formula");
  for (const prob of ["SAT", "SAT3"]) {
    await page.selectOption("#fm-prob", prob);
    const exs = await page.$$eval("#fm-ex option", os => os.map(o => o.value));
    for (const ex of exs) { await page.selectOption("#fm-ex", ex); await page.waitForTimeout(50); await audit("#fm-g", `${prob} example ${ex}`); }
  }
  await page.click("#t-reduction");
  await page.selectOption("#rd-red", "KARP_SAT3");
  const exs = await page.$$eval("#rd-ex option", os => os.map(o => o.value));
  for (const ex of exs) { await page.selectOption("#rd-ex", ex); await page.waitForTimeout(50); await page.click("#rd-scrub"); await page.evaluate(() => { const s = document.querySelector("#rd-scrub"); s.value = s.max; s.dispatchEvent(new Event("input", { bubbles: true })); }); await audit("#rdf-g", `SAT ↦ 3SAT example ${ex}`); }
  await browser.close();
  fails.slice(0, 20).forEach(f => console.log("  FAIL:", f));
  console.log(fails.length ? `${fails.length} FAILURES (${checked} labels)` : `ALL PASS (${checked} labels)`);
  process.exit(fails.length ? 1 : 0);
})().catch(e => { console.error(e); process.exit(1); });
