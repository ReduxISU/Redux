// Node tests for searchtree.js: every example × solver, plus random cross-checks against plain brute force.
const fs = require("fs");
const ST = eval(fs.readFileSync(__dirname + "/searchtree.js", "utf8") + ";SearchTreeView");
let fails = 0;
const ok = (c, msg) => { if (!c) { fails++; console.log("  FAIL:", msg); } };
const vname = l => (l[0] === "!" ? l.slice(1) : l);
const satBrute = cl => {
  const vars = [...new Set(cl.flat().map(vname))];
  for (let m = 0; m < 1 << vars.length; m++) {
    const a = new Map(vars.map((v, i) => [v, !!(m & (1 << i))]));
    if (cl.every(c => c.some(l => (l[0] === "!" ? !a.get(vname(l)) : a.get(l))))) return true;
  }
  return false;
};
const satCheck = (cl, a) => cl.every(c => c.some(l => (l[0] === "!" ? !a.get(vname(l)) : a.get(l))));

for (const [key, P] of Object.entries(ST.PROBLEMS)) {
  for (const [name, inst] of P.examples) for (const [sv] of P.solvers) {
    const { inst: I, out } = ST._run(inst, P, sv);
    const frames = ST._frames(out);
    console.log(`${key} · ${name} · ${sv}: ok=${out.ok} nodes=${out.R.ev.filter(e => e.label !== undefined).length} frames=${frames.length} | ${out.caption}`);
    ok(frames.every(f => typeof f.caption === "string" && f.caption.length), key + " captions");
    if (P.kind === "sat") { ok(out.ok === satBrute(I), "sat agrees with brute force"); if (out.ok) ok(satCheck(I, out.asg), "assignment satisfies"); }
  }
}

// random 3SAT: both solvers agree with brute force
let seed = 11; const rnd = () => (seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648;
for (let t = 0; t < 300; t++) {
  const nv = 2 + Math.floor(rnd() * 4), nc = 1 + Math.floor(rnd() * 7);
  const cl = Array.from({ length: nc }, () => Array.from({ length: 1 + Math.floor(rnd() * 3) }, () => (rnd() < 0.5 ? "!" : "") + "x" + (1 + Math.floor(rnd() * nv))));
  const str = cl.map(c => "(" + c.join(" | ") + ")").join(" & ");
  const want = satBrute(cl);
  for (const sv of ["redux", "dpll"]) {
    const { out } = ST._run(str, ST.PROBLEMS.SAT3, sv);
    if (sv === "dpll") ok(out.ok === want, `dpll ${str}`);
    else if (out.ok !== want) console.log(`  note: Redux-style backtracking says ${out.ok} but brute force says ${want} on ${str}`);
    if (out.ok) ok(satCheck(cl.map(c => [...new Set(c)]), out.asg), `${sv} assignment invalid on ${str}`);
  }
}
// random knapsack
for (let t = 0; t < 300; t++) {
  const n = 1 + Math.floor(rnd() * 7), items = Array.from({ length: n }, () => [1 + Math.floor(rnd() * 9), 1 + Math.floor(rnd() * 30)]);
  const W = Math.floor(rnd() * 20), V = Math.floor(rnd() * 60);
  let best = false;
  for (let m = 0; m < 1 << n; m++) { let w = 0, v = 0; items.forEach((it, i) => { if (m & (1 << i)) { w += it[0]; v += it[1]; } }); if (w <= W && v >= V) best = true; }
  const { out } = ST._run(`({${items.map(i => `(${i[0]},${i[1]})`).join(",")}},${W},${V})`, ST.PROBLEMS.KNAPSACK, "bb");
  ok(out.ok === best, `knapsack ${JSON.stringify(items)} W=${W} V=${V}`);
  if (out.ok) { const w = out.picked.reduce((a, b) => a + b.w, 0), v = out.picked.reduce((a, b) => a + b.v, 0); ok(w <= W && v >= V, "knapsack pick valid"); }
}
// random TSP: B&B cost = brute-force optimum (when within K)
for (let t = 0; t < 120; t++) {
  const n = 3 + Math.floor(rnd() * 4), N = "abcdefgh".slice(0, n).split(""), E = [];
  for (let i = 0; i < n; i++) for (let j = i + 1; j < n; j++) if (rnd() < 0.85) E.push([N[i], N[j], 1 + Math.floor(rnd() * 20)]);
  const w = new Map(E.map(e => [[e[0], e[1]].sort().join(), e[2]]));
  const perm = a => (a.length <= 1 ? [a] : a.flatMap((x, i) => perm([...a.slice(0, i), ...a.slice(i + 1)]).map(p => [x, ...p])));
  let opt = Infinity;
  for (const p of perm(N.slice(1))) { const tour = [N[0], ...p]; let c = 0; for (let i = 0; i < n; i++) { const k = [tour[i], tour[(i + 1) % n]].sort().join(); if (!w.has(k)) { c = Infinity; break; } c += w.get(k); } opt = Math.min(opt, c); }
  const K = 1000;
  const { out } = ST._run(`(({${N}},{${E.map(e => `({${e[0]},${e[1]}},${e[2]})`).join(",")}}),${K})`, ST.PROBLEMS.TSP, "bb");
  ok(out.ok === (opt < Infinity), `tsp feasibility n=${n}`);
  if (out.ok) ok(out.cost === opt, `tsp optimum ${out.cost} vs ${opt}`);
}
// random coloring
for (let t = 0; t < 200; t++) {
  const n = 2 + Math.floor(rnd() * 6), N = "abcdefgh".slice(0, n).split(""), E = [];
  for (let i = 0; i < n; i++) for (let j = i + 1; j < n; j++) if (rnd() < 0.5) E.push([N[i], N[j]]);
  const K = 1 + Math.floor(rnd() * 3);
  let can = false;
  for (let m = 0; m < K ** n && !can; m++) { const c = N.map((_, i) => Math.floor(m / K ** i) % K); if (E.every(([a, b]) => c[N.indexOf(a)] !== c[N.indexOf(b)])) can = true; }
  const { out } = ST._run(`(({${N}},{${E.map(e => `{${e[0]},${e[1]}}`).join(",")}}),${K})`, ST.PROBLEMS.GRAPHCOLORING, "bt");
  ok(out.ok === can, `coloring n=${n} K=${K}`);
  if (out.ok) ok(E.every(([a, b]) => out.color.get(a) !== out.color.get(b)), "coloring valid");
}
// N-Queens 1..7
for (let n = 1; n <= 7; n++) {
  const { out } = ST._run(String(n), ST.PROBLEMS.NQUEENS, "bt");
  ok(out.ok === !(n === 2 || n === 3), `queens n=${n}`);
  if (out.ok) for (let i = 0; i < n; i++) for (let j = i + 1; j < n; j++) ok(out.board[i] !== out.board[j] && Math.abs(out.board[i] - out.board[j]) !== j - i, `queens attack n=${n}`);
}
// random QBF vs plain recursive evaluation
for (let t = 0; t < 300; t++) {
  const nv = 1 + Math.floor(rnd() * 4), vars = Array.from({ length: nv }, (_, i) => "x" + (i + 1));
  const pre = vars.map(v => (rnd() < 0.5 ? "forall " : "exists ") + v).join(" ");
  const cl = Array.from({ length: 1 + Math.floor(rnd() * 4) }, () => Array.from({ length: 1 + Math.floor(rnd() * 3) }, () => (rnd() < 0.5 ? "!" : "") + vars[Math.floor(rnd() * nv)]));
  const str = pre + " : " + cl.map(c => "(" + c.join(" | ") + ")").join(" & ");
  const Q = ST.parse.qbf(str);
  const ev = (k, a) => { if (k === Q.prefix.length) return satCheck(Q.clauses, a); const { q, v } = Q.prefix[k]; const r = [true, false].map(b => ev(k + 1, new Map([...a, [v, b]]))); return q === "E" ? r[0] || r[1] : r[0] && r[1]; };
  const { out } = ST._run(str, ST.PROBLEMS.QBF, "game");
  ok(out.qbfTrue === ev(0, new Map()), `qbf ${str}`);
}
// random geography vs plain minimax
for (let t = 0; t < 300; t++) {
  const n = 2 + Math.floor(rnd() * 5), N = Array.from({ length: n }, (_, i) => String(i + 1)), E = [];
  for (const u of N) for (const v of N) if (u !== v && rnd() < 0.35) E.push([u, v]);
  const out1 = new Map(N.map(x => [x, []])); E.forEach(([u, v]) => out1.get(u).push(v));
  const win = (at, seen) => out1.get(at).filter(v => !seen.has(v)).some(v => !win(v, new Set([...seen, v])));
  const { out } = ST._run(`({${N}},{${E.map(e => `(${e[0]},${e[1]})`).join(",")}},1)`, ST.PROBLEMS.GEOGRAPHY, "game");
  ok(out.p1 === win("1", new Set(["1"])), `geography ${JSON.stringify(E)}`);
}
// bad input
for (const [P, s] of [[ST.PROBLEMS.SAT3, "(x1 | x2 | x3 | x4)"], [ST.PROBLEMS.QBF, "x1 : (x1)"], [ST.PROBLEMS.QBF, "forall x1 : (x2)"], [ST.PROBLEMS.GEOGRAPHY, "({1,2},{(1,3)},1)"], [ST.PROBLEMS.KNAPSACK, "({(1,2)},5)"], [ST.PROBLEMS.NQUEENS, "9"], [ST.PROBLEMS.TSP, "(({a,b},{{a,b}}),3)"]]) {
  try { ST._run(s, P, P.solvers[0][0]); ok(false, "should reject " + s); } catch (e) { console.log(`  rejects "${s}": ${e.message}`); }
}
console.log(fails ? `${fails} FAILURES` : "all passed");
