// Smoke test: loads every picture type (and a set of reductions) in the viewer against a running backend, steps through
// every frame (sampled when there are many), and fails on any console error, page error, failed run or empty drawing.
//
// Usage (backend on 27000, viewer server on 27080):
//   node smoke.mjs [--shots <dir>] [--only Graph,Packing] [--themes light,dark] [--viewer http://localhost:27080/viewer/] [--backend http://localhost:27000]
//                  [--stop-backend http://localhost:27001]   (stopped-run cases; skipped when that backend is not up)
// Needs Playwright: set PLAYWRIGHT to an installed copy (e.g. ../../../../Redux_GUI/node_modules/playwright) or `npm i playwright` somewhere on the path.
import { createRequire } from "node:module";
import fs from "node:fs";
import path from "node:path";
const require = createRequire(import.meta.url);
const { chromium } = require(process.env.PLAYWRIGHT || "playwright");

const arg = (k, d) => { const i = process.argv.indexOf("--" + k); return i > 0 ? process.argv[i + 1] : d; };
const VIEWER = arg("viewer", "http://localhost:27080/viewer/");
const BACKEND = arg("backend", "http://localhost:27000");
const STOP_BACKEND = arg("stop-backend", process.env.STOP_BACKEND || "http://localhost:27001");
const SHOTS = arg("shots", null);
const ONLY = arg("only", null)?.split(",");
const THEMES = arg("themes", SHOTS ? "light,dark" : "light").split(",");

import { CASES } from "./cases.mjs";

/**
 * Per-case expectations (cases.mjs `expect`), checked by stepping through every frame. Returns failure messages.
 *   applied: true    frame.applied never falls, rises from its first value to the circuit's last column, and the drawing
 *                    changes with it (a new picture for every new applied value, and bars on every frame past 0).
 *   ok: true|false|"any"   the last frame's ok.
 *   qubits: true     the last frame draws the measured qubits Solution (ok) or Rejected (ok:false), as the backend sent them.
 *   result: "..."    the last frame's result.
 *   browserBars: true   the chart says its bars are the browser's own simulation.
 */
async function checkExpect(page, ex, n) {
  const out = [];
  const go = i => page.evaluate(i => { const s = document.getElementById("scrub"); s.value = i; s.dispatchEvent(new Event("input")); }, i);
  const info = await page.evaluate(() => { const r = window.__viewer.run; return { frames: r.frames.map(f => ({ applied: f.applied, ok: f.ok, result: f.result, qubits: f.qubits })), columns: (r.resp.payload.columns || []).length }; });
  const last = info.frames[n - 1];
  if (ex.applied) {
    const ap = info.frames.map(f => f.applied ?? 0);
    if (ap.some((a, i) => i && a < ap[i - 1])) out.push(`expect applied: falls somewhere (${ap.join(",")})`);
    if (Math.max(...ap) !== info.columns || Math.max(...ap) <= Math.min(...ap)) out.push(`expect applied: does not rise to the ${info.columns} columns (${ap.join(",")})`);
    const sigs = new Map();
    for (let i = 0; i < n; i++) {
      await go(i);
      const sig = await page.evaluate(() => {
        const h = document.querySelector(".stage .host");
        return { done: [...h.querySelectorAll("g.qc-col")].filter(g => !g.classList.contains("future")).length,
          bars: [...h.querySelectorAll("rect.qc-bar")].map(b => (+b.getAttribute("height")).toFixed(1)).join(",") };
      });
      if (sig.done !== ap[i]) out.push(`expect applied: frame ${i + 1} has applied ${ap[i]} but draws ${sig.done} columns as applied`);
      if (ap[i] > 0 && !sig.bars) out.push(`expect applied: frame ${i + 1} (applied ${ap[i]}) draws no bars`);
      if (!sigs.has(ap[i])) sigs.set(ap[i], sig.done + "#" + sig.bars);
    }
    const distinct = new Set(sigs.values());
    if (distinct.size < sigs.size) out.push(`expect applied: ${sigs.size} applied values but only ${distinct.size} different drawings`);
  }
  if (ex.ok !== undefined && ex.ok !== "any" && last.ok !== ex.ok) out.push(`expect ok: ${ex.ok}, the last frame has ${last.ok}`);
  if (ex.ok === "any" && typeof last.ok !== "boolean") out.push(`expect ok: a verdict, the last frame has ${last.ok}`);
  if (ex.result !== undefined && last.result !== ex.result) out.push(`expect result: ${ex.result}, got ${last.result}`);
  await go(n - 1);
  if (ex.qubits) {
    const want = last.ok === false ? "Rejected" : "Solution";
    const sent = Object.values(last.qubits || {}).map(v => v.state || v);
    if (!sent.includes(want)) out.push(`expect qubits: the last frame sends no ${want} qubit (${JSON.stringify(last.qubits)})`);
    const drawn = await page.evaluate(w => ({ wires: document.querySelectorAll(`.stage .host g.qc-wire-g.${w}`).length, bar: document.querySelectorAll(`.stage .host rect.qc-bar.${w === "Solution" ? "win" : "miss"}`).length,
      other: document.querySelectorAll(`.stage .host g.qc-wire-g.${w === "Solution" ? "Rejected" : "Solution"}`).length }), want);
    if (drawn.wires !== sent.filter(s => s === want).length) out.push(`expect qubits: ${sent.filter(s => s === want).length} ${want} qubits sent, ${drawn.wires} drawn`);
    if (!drawn.bar) out.push(`expect qubits: the measured outcome's bar is not drawn ${want === "Solution" ? "green" : "red"}`);
    if (drawn.other) out.push("expect qubits: the opposite verdict is drawn too");
  }
  if (ex.browserBars) {
    const t = await page.evaluate(() => document.querySelector(".stage .host text.qc-src")?.textContent || "");
    if (!/Simulated in this browser/.test(t)) out.push("expect browserBars: the chart does not say the bars are the browser's simulation");
  }
  return out;
}

const sample = (n, k = 12) => n <= k ? [...Array(n).keys()] : [...new Set([...Array(k).keys()].map(i => Math.round(i * (n - 1) / (k - 1))))];

(async () => {
  const browser = await chromium.launch();
  const failures = [];
  let ran = 0;
  for (const theme of THEMES) {
    for (const c of CASES) {
      if (ONLY && !ONLY.some(o => c.name.startsWith(o) || c.type === o)) continue;
      const page = await browser.newPage({ viewport: { width: 1440, height: 1000 }, colorScheme: theme });
      const errs = [];
      page.on("console", m => { if (m.type() === "error") errs.push("console: " + m.text()); });
      page.on("pageerror", e => errs.push("pageerror: " + e.message));
      if (c.stopped && !(await fetch(STOP_BACKEND + "/Navigation/ALL_ProblemsRefactor").then(r => r.ok, () => false))) { console.log(`skip  ${theme.padEnd(5)} ${c.name} (no stopped-run backend at ${STOP_BACKEND})`); await page.close(); continue; }
      const q = new URLSearchParams({ backend: c.stopped ? STOP_BACKEND : BACKEND, theme, run: "1", ...c.q });
      await page.goto(VIEWER + "?" + q);
      await page.waitForFunction(() => ["ready", "error"].includes(document.body.dataset.state), null, { timeout: 60000 }).catch(() => errs.push("timed out waiting for the run"));
      const state = await page.evaluate(() => document.body.dataset.state);
      if (state !== "ready") errs.push("run failed: " + await page.evaluate(() => [...document.querySelectorAll(".err")].map(e => e.textContent).join(" ").slice(0, 400)));
      const n = await page.evaluate(() => +document.getElementById("scrub").max + 1);
      const drawn = await page.evaluate(() => [...document.querySelectorAll(".stage .host")].map(h => h.querySelectorAll("svg *, table td").length));
      // A legacy visualization has no frames picture: the viewer must say so instead of drawing.
      if (c.legacy) {
        const told = await page.evaluate(() => /legacy visualization/.test(document.querySelector(".stage")?.textContent || ""));
        if (state === "ready" && !told) errs.push("legacy visualization: the viewer doesn't show the legacy notice");
      } else if (state === "ready" && drawn.some(x => x < 3)) errs.push("a pane drew (almost) nothing: " + drawn.join(","));
      for (const i of sample(n)) {
        await page.evaluate(i => { const s = document.getElementById("scrub"); s.value = i; s.dispatchEvent(new Event("input")); }, i);
      }
      // A few hovers over drawn pieces (exercises renderer hover and, in reductions, gadget linking).
      const vids = await page.$$(".stage [data-vid]");
      for (const el of vids.slice(0, 4)) await el.hover({ force: true }).catch(() => {});
      await page.mouse.move(2, 2);
      if (SHOTS) {
        fs.mkdirSync(SHOTS, { recursive: true });
        await page.addStyleTag({ content: ".top, .side { position: static !important; max-height: none !important; }" }); // full-page shots
        // Middle frame and last frame.
        for (const [label, i] of [["mid", Math.floor((n - 1) / 2)], ["last", n - 1]]) {
          await page.evaluate(i => { const s = document.getElementById("scrub"); s.value = i; s.dispatchEvent(new Event("input")); }, i);
          await page.waitForTimeout(250);
          await page.screenshot({ path: path.join(SHOTS, `${c.name}-${theme}-${label}.png`), fullPage: true });
        }
      }
      if (c.stopped && state === "ready") {
        const k = await page.evaluate(() => window.__viewer.run.frames.findIndex(f => f.event === "Done" && f.ok === false));
        if (k < 0) errs.push("stopped run: no Done frame with ok:false");
        else {
          await page.evaluate(i => { const s = document.getElementById("scrub"); s.value = i; s.dispatchEvent(new Event("input")); }, k);
          const badges = await page.evaluate(() => document.getElementById("badges").textContent);
          if (!/stopped/.test(badges)) errs.push("stopped run: the ok:false Done frame is not shown as stopped: " + badges);
          if (SHOTS) await page.screenshot({ path: path.join(SHOTS, `${c.name}-${theme}-stopped.png`), fullPage: true });
        }
      }
      if (c.expect && state === "ready") errs.push(...await checkExpect(page, c.expect, n));
      const notes = await page.evaluate(() => [...document.querySelectorAll("#notes li")].map(l => l.textContent));
      ran++;
      const line = `${errs.length ? "FAIL" : "ok  "} ${theme.padEnd(5)} ${c.name.padEnd(28)} frames=${n} drawn=${drawn.join("/")}${notes.length ? "  notes=" + notes.length : ""}`;
      console.log(line);
      if (process.env.NOTES) notes.forEach(t => console.log("      · " + t));
      if (errs.length) { errs.forEach(e => console.log("      ! " + e)); failures.push(c.name + " " + theme); }
      await page.close();
    }
  }
  await browser.close();
  console.log(`${ran - failures.length}/${ran} passed`);
  process.exit(failures.length ? 1 : 0);
})();
