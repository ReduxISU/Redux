// Contrast audit of the viewer: runs ../mockups/contrast/audit-lib.js (the same rules as the design mockup's audit: text 4.5:1,
// large text 3:1, strokes, marks and control borders 3:1, composited through every ancestor opacity) over the drawn panes and the
// side panel, for every case in cases.mjs, at a few frames each, in light and dark mode. Also hovers one piece per pane
// (renderer hover tracing, and gadget linking in reductions). Exits 1 on any failure; prints one line per failing element kind.
//
// Usage (backend on 27000, `node serve.mjs` on 27080): PLAYWRIGHT=<path to playwright> node contrast.mjs [--only Graph,Red-] [--out failures.json]
import { createRequire } from "node:module";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { CASES } from "./cases.mjs";
const require = createRequire(import.meta.url);
const { chromium } = require(process.env.PLAYWRIGHT || "playwright");
const here = path.dirname(fileURLToPath(import.meta.url));
const arg = (k, d) => { const i = process.argv.indexOf("--" + k); return i > 0 ? process.argv[i + 1] : d; };
const VIEWER = arg("viewer", "http://localhost:27080/viewer/"), BACKEND = arg("backend", "http://localhost:27000");
const ONLY = arg("only", null)?.split(","), OUT = arg("out", null);

(async () => {
  const browser = await chromium.launch();
  const all = [];
  for (const theme of ["light", "dark"]) {
    for (const c of CASES) {
      if (c.stopped) continue;
      if (ONLY && !ONLY.some(o => c.name.startsWith(o) || c.type === o)) continue;
      const page = await browser.newPage({ viewport: { width: 1440, height: 1000 }, colorScheme: theme });
      await page.goto(VIEWER + "?" + new URLSearchParams({ backend: BACKEND, theme, run: "1", ...c.q }));
      await page.waitForFunction(() => ["ready", "error"].includes(document.body.dataset.state), null, { timeout: 60000 }).catch(() => {});
      await page.addScriptTag({ path: path.join(here, "..", "mockups", "contrast", "audit-lib.js") });
      await page.addStyleTag({ content: "*,*::before,*::after{transition:none!important;animation:none!important}" });
      const n = await page.evaluate(() => +document.getElementById("scrub").max + 1);
      const audit = async view => { await page.waitForTimeout(40); all.push(...await page.evaluate(([s, ctx]) => window.__audit(s, ctx), [".stage .host, .side", { theme, case: c.name, view }])); };
      for (const i of [...new Set([0, Math.floor((n - 1) / 3), Math.floor(2 * (n - 1) / 3), n - 1])]) {
        await page.evaluate(i => { const s = document.getElementById("scrub"); s.value = i; s.dispatchEvent(new Event("input")); }, i);
        await audit("frame " + i);
      }
      for (const pane of await page.$$(".stage .host")) {
        const el = await pane.$("[data-vid]");
        if (!el) continue;
        await el.hover({ force: true }).catch(() => {});
        await audit("hover");
        await page.mouse.move(2, 2);
      }
      await page.close();
    }
  }
  await browser.close();
  const kinds = new Map();
  for (const f of all) { const k = `${f.theme} ${f.kind} ${f.what} fg ${f.fg} on ${f.bg} = ${f.ratio} (need ${f.need})`; if (!kinds.has(k)) kinds.set(k, new Set()); kinds.get(k).add(f.case); }
  for (const [k, cs] of kinds) console.log(k + "  [" + [...cs].slice(0, 4).join(", ") + (cs.size > 4 ? ", …" : "") + "]");
  if (OUT) fs.writeFileSync(OUT, JSON.stringify(all, null, 1));
  console.log(`${all.length} failing checks, ${kinds.size} kinds`);
  process.exit(all.length ? 1 : 0);
})();
