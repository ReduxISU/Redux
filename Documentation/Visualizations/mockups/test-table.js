const fs = require("fs");
const src = fs.readFileSync("table.js", "utf8").replace("return { create, parse, PROBLEMS };", "return { create, parse, PROBLEMS, solveEditDistance, solveIPBrute, solveIPBacktrack, solveDFA, solveNFASubset, solveNFARuns, solveDijkstra, nfaRuns };");
const T = eval(src + ";TableView");
let fails = 0;
const expect = (c, msg) => { if (!c) { fails++; console.log("  FAIL:", msg); } };
for (const [k, P] of Object.entries(T.PROBLEMS)) for (const [nm, s] of P.examples) for (const [sv, svName] of P.solvers) {
  const I = T.parse(s, k);
  let r;
  if (P.kind === "ed") r = T.solveEditDistance(I);
  else if (P.kind === "ip") r = sv === "bt" ? T.solveIPBacktrack(I) : T.solveIPBrute(I);
  else if (P.kind === "dfa") r = T.solveDFA(I);
  else if (P.kind === "nfa") r = sv === "runs" ? T.solveNFARuns(I) : T.solveNFASubset(I);
  else r = T.solveDijkstra(I);
  const last = r.frames[r.frames.length - 1];
  console.log(`${k} | ${nm} | ${svName} | frames ${r.frames.length} | ok ${r.ok}\n   ${last.caption}`);
  expect(last.done, k + nm + " last frame done");
}
// specific value checks
const ed = (a, b) => T.solveEditDistance(T.parse(`(${a}, ${b})`, "EDITDISTANCE")).value;
expect(ed("horse", "ros") === 3, "horse/ros = 3"); expect(ed("kitten", "sitting") === 3, "kitten = 3"); expect(ed("intention", "execution") === 5, "intention = 5"); expect(ed("abc", "") === 3, "abc/'' = 3"); expect(ed("", "") === 0, "empty = 0");
// alignment cost equals distance for random strings
let seed = 3; const rnd = () => (seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648;
for (let t = 0; t < 400; t++) {
  const w = () => Array.from({ length: Math.floor(rnd() * 7) }, () => "abc"[Math.floor(rnd() * 3)]).join("");
  const a = w(), b = w(); const r = T.solveEditDistance({ x: a, y: b }); const f = r.frames.at(-1);
  const cost = f.ops.filter(o => o.op !== "keep").length;
  const xa = f.ops.filter(o => o.a !== null).map(o => o.a).join(""), yb = f.ops.filter(o => o.b !== null).map(o => o.b).join("");
  if (cost !== r.value || xa !== a || yb !== b) { fails++; console.log("  FAIL align", a, b, cost, r.value); break; }
}
// IP: brute and backtracking agree with exhaustive check
for (let t = 0; t < 300; t++) {
  const n = 1 + Math.floor(rnd() * 5), m = 1 + Math.floor(rnd() * 4);
  const C = Array.from({ length: m }, () => Array.from({ length: n }, () => Math.floor(rnd() * 5) - 2));
  const d = Array.from({ length: m }, () => Math.floor(rnd() * 5) - 2);
  const I = { C, d, n, m };
  let exists = false; for (let mask = 0; mask < 1 << n; mask++) { const x = Array.from({ length: n }, (_, j) => (mask >> j) & 1); if (C.every((row, r) => row.reduce((s, c, j) => s + c * x[j], 0) <= d[r])) exists = true; }
  const a = T.solveIPBrute(I), b = T.solveIPBacktrack(I);
  if (a.ok !== exists || b.ok !== exists) { fails++; console.log("  FAIL ip", JSON.stringify(I), exists, a.ok, b.ok); break; }
  for (const s of [a.solution, b.solution]) if (s && !C.every((row, r) => row.reduce((q, c, j) => q + c * s[j], 0) <= d[r])) { fails++; console.log("  FAIL ip sol"); }
}
// Redux brute order: first solution = smallest little-endian number
{ const I = T.parse("(-1 -1 -1 -1),(1 1 0 0),(0 0 1 1),(1 0 1 0)<=(-2 1 1 1)", "INTPROGRAMMING01"); const r = T.solveIPBrute(I); expect(r.solution.join("") === "0110" && r.frames.length === 9, "brute finds 0110 on try 7 (" + r.solution + "," + r.frames.length + ")"); }
// Dijkstra correctness vs Bellman-Ford on random graphs
for (let t = 0; t < 300; t++) {
  const nodes = Array.from({ length: 2 + Math.floor(rnd() * 6) }, (_, i) => "n" + i);
  const edges = []; nodes.forEach(a => nodes.forEach(b => { if (a !== b && rnd() < 0.3) edges.push(`((${a},${b}),${Math.floor(rnd() * 9)})`); }));
  const G = T.parse(`({${nodes}},{${edges}},${nodes[0]})`, "SSSP"); const r = T.solveDijkstra(G); const fin = r.frames.at(-1);
  const bf = new Map(nodes.map(n => [n, Infinity])); bf.set(nodes[0], 0);
  for (let k = 0; k < nodes.length; k++) for (const [a, l] of G.adj) for (const { to, w } of l) if (bf.get(a) + w < bf.get(to)) bf.set(to, bf.get(a) + w);
  if (nodes.some(n => bf.get(n) !== fin.dist.get(n))) { fails++; console.log("  FAIL dijkstra", nodes, edges); break; }
}
// NFA: subset simulation and runs agree on acceptance
for (const s of T.PROBLEMS.NFA.examples.map(e => e[1])) { const I = T.parse(s, "NFA"); expect(T.solveNFASubset(I).ok === T.solveNFARuns(I).ok, "nfa agree " + s); }
// errors
for (const [k, s] of [["EDITDISTANCE", "horse, ros"], ["INTPROGRAMMING01", "(1 2),(3)<=(1 1)"], ["INTPROGRAMMING01", "(1 2)<=(1 1)"], ["DFA", "(({1},{a},{(1,a,1),(1,a,1)},1,{1}),a)"], ["SSSP", "({1,2},{((1,3),1)},1)"], ["SPSP", "({1,2},{((1,2),1)},1)"]]) {
  try { T.parse(s, k); fails++; console.log("  FAIL no error for", k, s); } catch (e) { console.log("  error ok:", k, "→", e.message); }
}
console.log(fails ? `${fails} FAILURES` : "all checks passed");
