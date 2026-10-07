// Node tests for packing.js: every example x every solver, plus independent cross-checks.
const fs = require("fs");
const PV = eval(fs.readFileSync(__dirname + "/packing.js", "utf8") + ";PackingView");
let fails = 0;
const ok = (cond, msg) => { if (!cond) { fails++; console.log("  FAIL:", msg); } };
const sum = a => a.reduce((s, x) => s + x, 0), prod = a => a.reduce((s, x) => s * x, 1);

// independent answers by exhaustive search
function truth(P, I) {
  const n = I.n, w = I.items.map(t => t.w);
  if (P.rule === "partition") { for (let m = 0; m < 2 ** n; m++) { const a = sum(w.filter((_, i) => m & (1 << i))); if (2 * a === sum(w)) return true; } return false; }
  if (P.rule === "sum") { for (let m = 1; m < 2 ** n; m++) if (sum(w.filter((_, i) => m & (1 << i))) === I.T) return true; return false; }
  if (P.rule === "product") { for (let m = 1; m < 2 ** n; m++) if (prod(w.filter((_, i) => m & (1 << i))) === I.T) return true; return false; }
  if (P.rule === "knapsack") { for (let m = 0; m < 2 ** n; m++) { const s = I.items.filter((_, i) => m & (1 << i)); if (sum(s.map(t => t.w)) <= I.W && sum(s.map(t => t.v)) >= I.V) return true; } return false; }
  // bin packing: try every assignment
  const K = I.K, C = I.C;
  const rec = (i, loads) => i === n ? true : loads.some((l, b) => l + w[i] <= C && (loads[b] += w[i], rec(i + 1, loads) || (loads[b] -= w[i], false)));
  return rec(0, Array(K).fill(0));
}
// does a final frame really satisfy the rule?
function valid(P, I, assign) {
  const pick = r => I.items.filter((_, k) => assign[k] === r);
  if (P.rule === "partition") return assign.every(a => a === 0 || a === 1) && sum(pick(0).map(t => t.w)) === sum(pick(1).map(t => t.w));
  if (P.rule === "sum") return pick(0).length > 0 && sum(pick(0).map(t => t.w)) === I.T;
  if (P.rule === "product") return pick(0).length > 0 && prod(pick(0).map(t => t.w)) === I.T;
  if (P.rule === "knapsack") return sum(pick(0).map(t => t.w)) <= I.W && sum(pick(0).map(t => t.v)) >= I.V;
  return assign.every(a => a >= 0 && a < I.K) && Array.from({ length: I.K }, (_, b) => sum(pick(b).map(t => t.w))).every(s => s <= I.C);
}

for (const [key, P] of Object.entries(PV.PROBLEMS)) {
  for (const [name, inst] of P.examples) {
    const I = PV.parse(inst, P), t = truth(P, I);
    for (const [sk, sname] of P.solvers) {
      const frames = PV.SOLVE[P.rule][sk](I), last = frames[frames.length - 1];
      console.log(`${key} | ${name} | ${sname}: ${frames.length} frames, ok=${last.ok} (truth ${t}) | ${last.caption}`);
      frames.forEach((f, i) => { ok(Array.isArray(f.assign) && f.assign.length === I.n, `${key}/${name}/${sk} frame ${i} assign`); ok(typeof f.caption === "string" && f.caption.length > 0, `${key}/${name}/${sk} frame ${i} caption`); });
      ok(last.done, `${key}/${name}/${sk} last frame not done`);
      if (last.ok) ok(valid(P, I, last.assign), `${key}/${name}/${sk} claims ok but answer invalid`);
      const exact = !(sk === "greedy" || sk === "approx" || sk === "ffd");
      if (exact) ok(last.ok === t, `${key}/${name}/${sk} exact solver disagrees with truth`);
      else if (last.ok) ok(t, `${key}/${name}/${sk} heuristic says yes but truth is no`);
    }
  }
}
// the examples that are meant to show a heuristic missing
const miss = (key, ex, sk) => { const P = PV.PROBLEMS[key], I = PV.parse(P.examples.find(e => e[0] === ex)[1], P); const last = PV.SOLVE[P.rule][sk](I).at(-1); ok(!last.ok && truth(P, I), `${key} "${ex}" should show ${sk} missing a real answer`); };
miss("PARTITION", "Greedy misses it", "greedy");
miss("SUBSETSUM", "Approximation misses", "approx");
miss("BINPACKING", "First Fit Decreasing falls short", "ffd");

// random cross-checks for the exact solvers
let seed = 11; const rnd = k => (seed = (seed * 1103515245 + 12345) % 2147483648) % k;
for (let t = 0; t < 600; t++) {
  const n = 2 + rnd(7), xs = Array.from({ length: n }, () => 1 + rnd(12));
  const cases = [
    ["PARTITION", `{${xs}}`],
    ["SUBSETSUM", `({${xs}},${1 + rnd(40)})`],
    ["SUBSETPRODUCT", `({${xs}},${[6, 12, 24, 30, 36, 60, 7, 11][rnd(8)]})`],
    ["KNAPSACK", `({${xs.map(x => `(${x},${1 + rnd(20)})`)}},${5 + rnd(30)},${5 + rnd(40)})`],
    ["BINPACKING", `((${xs}),${12 + rnd(6)},${1 + rnd(4)})`],
  ];
  for (const [key, inst] of cases) {
    const P = PV.PROBLEMS[key], I = PV.parse(inst, P), tr = truth(P, I);
    for (const [sk] of P.solvers) {
      const last = PV.SOLVE[P.rule][sk](I).at(-1);
      if (last.ok) ok(valid(P, I, last.assign), `random ${key} ${inst} ${sk} invalid yes`);
      if (!(sk === "greedy" || sk === "approx" || sk === "ffd")) ok(last.ok === tr, `random ${key} ${inst} ${sk}: got ${last.ok}, truth ${tr}`);
      else if (last.ok) ok(tr, `random ${key} ${inst} ${sk} heuristic yes vs truth no`);
    }
  }
}
// bad input
for (const [key, bad] of [["PARTITION", "{1,2,x}"], ["SUBSETSUM", "({1,2},)"], ["KNAPSACK", "({(1,2,3)},5,5)"], ["BINPACKING", "((1,2),3)"], ["SUBSETPRODUCT", "({0,2},4)"]]) {
  try { PV.parse(bad, PV.PROBLEMS[key]); fails++; console.log("  FAIL: accepted", key, bad); } catch (e) { console.log(`  rejects ${key} ${bad}: ${e.message}`); }
}
console.log(fails ? `${fails} FAILURES` : "ALL PASS");
