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
  // Maximum-Value Knapsack: the best value (a number, not yes/no)
  if (P.rule === "maxknapsack") { let b = 0; for (let m = 0; m < 2 ** n; m++) { const s = I.items.filter((_, i) => m & (1 << i)); if (sum(s.map(t => t.w)) <= I.W) b = Math.max(b, sum(s.map(t => t.v))); } return b; }
  // Change-Making: fewest coins by breadth-first search over amounts, then compare with K
  if (P.rule === "changemaking") { return fewestCoins(w, I.X) <= I.K; }
  // bin packing: try every assignment
  const K = I.K, C = I.C;
  const rec = (i, loads) => i === n ? true : loads.some((l, b) => l + w[i] <= C && (loads[b] += w[i], rec(i + 1, loads) || (loads[b] -= w[i], false)));
  return rec(0, Array(K).fill(0));
}
function fewestCoins(coins, X) {
  const dist = new Map([[0, 0]]), q = [0];
  while (q.length) { const a = q.shift(); for (const c of coins) if (a + c <= X && !dist.has(a + c)) { dist.set(a + c, dist.get(a) + 1); q.push(a + c); } }
  return dist.has(X) ? dist.get(X) : Infinity;
}
// does a final frame really satisfy the rule?
function valid(P, I, assign, f) {
  const pick = r => I.items.filter((_, k) => assign[k] === r);
  if (P.rule === "maxknapsack") return sum(pick(0).map(t => t.w)) <= I.W;
  if (P.rule === "changemaking") return f.counts.every(c => c >= 0) && sum(I.items.map((t, k) => t.w * f.counts[k])) === I.X && sum(f.counts) <= I.K;
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
      if (last.ok) ok(valid(P, I, last.assign, last), `${key}/${name}/${sk} claims ok but answer invalid`);
      const exact = !(sk === "greedy" || sk === "approx" || sk === "ffd");
      if (P.rule === "maxknapsack") agreeMax(I, last, t, exact, `${key}/${name}/${sk}`);
      else if (exact) ok(last.ok === t, `${key}/${name}/${sk} exact solver disagrees with truth`);
      else if (last.ok) ok(t, `${key}/${name}/${sk} heuristic says yes but truth is no`);
    }
  }
}
// Maximum-Value Knapsack always has an answer: exact solvers reach the best value, Modified Greedy at least half of it.
function agreeMax(I, last, best, exact, what) {
  const v = sum(I.items.filter((_, k) => last.assign[k] === 0).map(t => t.v));
  ok(last.ok === true, `${what} an optimization run always ends ok`);
  if (exact) ok(v === best, `${what} exact solver got ${v}, best is ${best}`);
  else ok(2 * v >= best, `${what} greedy got ${v}, less than half of ${best}`);
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
    ["MAXIMUMVALUEKNAPSACK", `((${xs.map(x => `(${rnd(5) ? x : 0},${rnd(6) ? 1 + rnd(20) : 0})`)}),${rnd(35)})`],
    ["CHANGEMAKING", `({${xs.map(x => x + 2)}},${1 + rnd(60)},${rnd(9)})`],
  ];
  for (const [key, inst] of cases) {
    const P = PV.PROBLEMS[key], I = PV.parse(inst, P), tr = truth(P, I);
    for (const [sk] of P.solvers) {
      const last = PV.SOLVE[P.rule][sk](I).at(-1);
      if (last.ok) ok(valid(P, I, last.assign, last), `random ${key} ${inst} ${sk} invalid yes`);
      if (P.rule === "maxknapsack") agreeMax(I, last, tr, sk !== "greedy", `random ${key} ${inst} ${sk}`);
      else if (!(sk === "greedy" || sk === "approx" || sk === "ffd")) ok(last.ok === tr, `random ${key} ${inst} ${sk}: got ${last.ok}, truth ${tr}`);
      else if (last.ok) ok(tr, `random ${key} ${inst} ${sk} heuristic yes vs truth no`);
    }
  }
}
// bad input
for (const [key, bad] of [["PARTITION", "{1,2,x}"], ["SUBSETSUM", "({1,2},)"], ["KNAPSACK", "({(1,2,3)},5,5)"], ["BINPACKING", "((1,2),3)"], ["SUBSETPRODUCT", "({0,2},4)"],
  ["MAXIMUMVALUEKNAPSACK", "({(10,60)},50)"], ["MAXIMUMVALUEKNAPSACK", "(((10,60,1)),50)"], ["MAXIMUMVALUEKNAPSACK", "(((10,60)),50,220)"], ["MAXIMUMVALUEKNAPSACK", "(((10,-1)),50)"],
  ["CHANGEMAKING", "({1,7},0,3)"], ["CHANGEMAKING", "({0,7},14,3)"], ["CHANGEMAKING", "({1,7},14,-1)"], ["CHANGEMAKING", "((1,7),14,3)"], ["CHANGEMAKING", "({1},100,100)"]]) {
  try { PV.parse(bad, PV.PROBLEMS[key]); fails++; console.log("  FAIL: accepted", key, bad); } catch (e) { console.log(`  rejects ${key} ${bad}: ${e.message}`); }
}

// ---- Maximum-Value 0-1 Knapsack: the frames follow Redux's three solvers ----
{
  const P = PV.PROBLEMS.MAXIMUMVALUEKNAPSACK, I = PV.parse("(((10,60),(20,100),(30,120)),50)", P);
  const idx = f => f.assign.map((a, k) => (a === 0 ? k + 1 : 0)).filter(Boolean).join(",");
  const brute = PV.SOLVE.maxknapsack.brute(I), bnb = PV.SOLVE.maxknapsack.bnb(I), greedy = PV.SOLVE.maxknapsack.greedy(I);
  ok(idx(brute.at(-1)) === "2,3" && idx(bnb.at(-1)) === "2,3", "default: both exact solvers answer ({2,3},220), Redux's certificate");
  ok(idx(greedy.at(-1)) === "1,2", "default: Modified Greedy answers ({1,2},160), short of 220");
  ok(brute.length === 2 ** 3 + 1, "brute force: one frame per subset, including the empty one, then the answer");
  let prev = -1, mono = true;
  for (const f of brute.slice(0, -1)) { if (f.bestValue < prev) mono = false; prev = f.bestValue; }
  ok(mono, "brute force: the best so far never goes down");
  ok(brute.slice(0, -1).every(f => Array.isArray(f.best) && f.best.length === I.n), "brute force: every try carries the best set so far");
  ok(brute.filter(f => f.trial).length === 1 && brute.find(f => f.trial).caption.includes("over 50"), "brute force: only {1,2,3} is too heavy, and its caption says so");
  ok(bnb.some(f => f.bound !== undefined && f.trial), "branch and bound: at least one branch is cut by its bound");
  ok(bnb.filter(f => f.bound !== undefined).every(f => f.bound >= f.bestValue || f.trial || /decided/.test(f.caption)), "branch and bound: a branch is kept only while its bound beats the best so far");
  ok(greedy.some(f => f.bad === 2), "greedy: item 3 is the first that doesn't fit");
  ok(/most valuable single item/.test(greedy.at(-2).caption), "greedy: the single-item comparison comes last before the answer");
  const one = PV.SOLVE.maxknapsack.greedy(PV.parse("(((1,2),(10,10)),10)", P)).at(-1);
  ok(one.assign.join() === "-1,0", "greedy: one big item beats the greedy set when it is worth more");
  // Redux's pre-pass: zero-weight items are free, zero-value items are never taken
  const z = PV.parse("(((0,5),(4,7),(3,0),(9,9)),5)", P);
  for (const sk of ["brute", "bnb", "greedy"]) { const l = PV.SOLVE.maxknapsack[sk](z).at(-1); ok(l.assign[0] === 0 && l.assign[2] === -1 && idx(l) === "1,2", `zero weight / zero value items: ${sk} answers ({1,2},12), got {${idx(l)}}`); }
}
// ---- Change-Making: the DP table and the trace back ----
{
  const P = PV.PROBLEMS.CHANGEMAKING, I = PV.parse("({1,7,10,25},39,3)", P);
  for (const sk of ["dp", "memo"]) {
    const fr = PV.SOLVE.changemaking[sk](I), last = fr.at(-1);
    ok(last.ok && last.counts.join() === "0,2,0,1", `${sk}: 39 = 7 + 7 + 25, Redux's certificate {(7,2),(25,1)}`);
    const tb = last.table.best;
    let same = true;
    for (let v = 0; v <= I.X; v++) if (tb[v] !== fewestCoins([1, 7, 10, 25], v)) same = false;
    ok(same, `${sk}: every table cell holds the fewest coins for its amount`);
    ok(last.table.path.join() === "39,32,25,0", `${sk}: the trace back walks 39, 32, 25, 0`);
    const trace = fr.filter(f => f.table && f.table.path && !f.done);
    ok(trace.length === 4 && trace.every((f, i) => sum(f.counts) === i), `${sk}: the trace back adds one coin per step`);
  }
  const dp = PV.SOLVE.changemaking.dp(I);
  ok(dp.filter(f => f.table && f.table.reads).map(f => f.table.cur).join() === Array.from({ length: 39 }, (_, i) => i + 1).join(), "dp: fills amounts 1 to 39 left to right");
  const big = PV.parse("({10,25},65,5)", P), memo = PV.SOLVE.changemaking.memo(big).at(-1), full = PV.SOLVE.changemaking.dp(big).at(-1);
  ok(memo.table.best.filter(b => b !== undefined).length < full.table.best.filter(b => b !== undefined).length, "memo: works out only the amounts reachable from X");
  ok(memo.counts.join() === full.counts.join() && sum(memo.counts) === 5, "memo and dp agree: 65 = 25 + 10 + 10 + 10 + 10");
  const no = PV.SOLVE.changemaking.dp(PV.parse("({1,7,10,25},39,2)", P)).at(-1);
  ok(!no.ok && no.table.bad === 39 && sum(no.counts) === 0, "K too small: no coins, the X cell is the one that fails");
  const never = PV.SOLVE.changemaking.memo(PV.parse("({4,6},23,10)", P)).at(-1);
  ok(!never.ok && never.table.best[23] === Infinity, "an odd amount from even coins can't be made");
  ok(PV.parse("({7,1,7},8,2)", P).items.map(t => t.w).join() === "7,1", "repeated coins count once, in input order");
}
console.log(fails ? `${fails} FAILURES` : "ALL PASS");
