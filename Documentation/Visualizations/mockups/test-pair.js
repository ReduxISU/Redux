const fs = require("fs");
const V = eval(fs.readFileSync("pairview.js", "utf8") + ";PairView");
let fail = 0;
const check = (c, m) => { if (!c) { fail++; console.log("FAIL:", m); } };
for (const [k, P] of Object.entries(V.PROBLEMS)) for (const [nm, s] of P.examples) for (const [sv] of P.solvers) {
  const I = V.parse(s, k), fr = V._solve(k, I, sv), last = fr[fr.length - 1];
  check(last.done, k + nm + sv + " no done");
  fr.forEach((f, i) => check(typeof f.caption === "string" && f.caption.length > 5, `${k} ${nm} ${sv} frame ${i} caption`));
  let extra = "";
  if (k === "PCP" && last.ok) { const t = last.seq.map(i => I[i].t).join(""), b = last.seq.map(i => I[i].b).join(""); check(t === b, "pcp match mismatch"); extra = t; }
  if (k !== "PCP" && last.ok) {
    const m = last.map, used = new Set(Object.values(m));
    check(Object.keys(m).length === I.L.nodes.length && used.size === Object.keys(m).length, "gi map not injective/complete");
    I.L.edges.forEach(e => check(I.R.has(m[e.a], m[e.b]), "edge not kept " + e.k));
    if (!I.sub) { const inv = {}; for (const [u, v] of Object.entries(m)) inv[v] = u; I.R.edges.forEach(e => check(I.L.has(inv[e.a], inv[e.b]), "extra edge")); }
    extra = JSON.stringify(m);
  }
  console.log(k.padEnd(12), nm.padEnd(30), sv.padEnd(6), "frames", String(fr.length).padStart(3), "ok", last.ok, last.verdict || "", extra);
}
// brute vs backtracking agree on random small graphs
let seed = 3; const rnd = () => (seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648;
for (let t = 0; t < 300; t++) {
  const n = 3 + Math.floor(rnd() * 4), m = 3 + Math.floor(rnd() * 3), sub = rnd() < 0.5;
  const g = (nn, pre, p) => { const N = Array.from({ length: nn }, (_, i) => pre + i), E = []; for (let i = 0; i < nn; i++) for (let j = i + 1; j < nn; j++) if (rnd() < p) E.push(`{${N[i]},${N[j]}}`); return `({${N}},{${E}})`; };
  const s = sub ? `(${g(Math.min(n, m), "p", 0.5)},${g(Math.max(n, m), "h", 0.5)})` : `(${g(n, "a", 0.45)},${g(n, "b", 0.45)})`;
  const k = sub ? "SUBGRAPHISO" : "GRAPHISO", I = V.parse(s, k);
  const a = V._solve(k, I, "deg").at(-1).ok, b = V._solve(k, I, "brute").at(-1).ok;
  check(a === b, "disagree " + k + " " + s);
}
// PCP: bfs vs ids agree on verdict where both settle; matches verified
for (let t = 0; t < 300; t++) {
  const w = () => Array.from({ length: 1 + Math.floor(rnd() * 3) }, () => "ab"[Math.floor(rnd() * 2)]).join("");
  const s = "{" + Array.from({ length: 2 + Math.floor(rnd() * 2) }, () => `(${w()},${w()})`).join(",") + "}";
  const I = V.parse(s, "PCP"), a = V._solve("PCP", I, "bfs").at(-1), b = V._solve("PCP", I, "ids").at(-1);
  if (a.ok) { const tt = a.seq.map(i => I[i].t).join(""), bb = a.seq.map(i => I[i].b).join(""); check(tt === bb, "bfs bad match " + s); }
  if (b.ok) { const tt = b.seq.map(i => I[i].t).join(""), bb = b.seq.map(i => I[i].b).join(""); check(tt === bb, "ids bad match " + s); }
  if (a.ok && b.ok) check(a.seq.length === b.seq.length, "shortest lengths differ " + s);
  if (a.verdict === "noMatch") check(!b.ok, "bfs says none but ids found " + s);
  if (b.verdict === "noMatch") check(!a.ok, "ids says none but bfs found " + s);
}
for (const bad of ["{(a,b),(c)}", "{(a,b)", "(({1,2},{{1,3}}),({a,b},{}))"]) { try { V.parse(bad, bad.startsWith("{") ? "PCP" : "GRAPHISO"); fail++; console.log("FAIL no error", bad); } catch (e) { console.log("err ok:", e.message); } }
console.log("failures:", fail);
