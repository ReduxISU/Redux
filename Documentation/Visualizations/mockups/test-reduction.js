const fs = require("fs");
const RV = eval(fs.readFileSync("reduction.js", "utf8") + ";ReductionView");
let seed = 11; const rnd = () => (seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648;
const satOf = clauses => { const vars = [...new Set(clauses.flat().map(l => l.replace(/^!/, "")))];
  for (let m = 0; m < 1 << vars.length; m++) { const a = {}; vars.forEach((v, i) => a[v] = !!(m & (1 << i)));
    if (clauses.every(c => c.some(l => l.startsWith("!") ? !a[l.slice(1)] : a[l]))) return true; } return false; };
let fails = 0, n = 0;
for (let t = 0; t < 300; t++) {
  const m = 1 + Math.floor(rnd() * 4), nv = 1 + Math.floor(rnd() * 3);
  const clauses = Array.from({ length: m }, () => Array.from({ length: 1 + Math.floor(rnd() * 3) }, () => (rnd() < .5 ? "!" : "") + "x" + (1 + Math.floor(rnd() * nv))));
  const str = clauses.map(c => "(" + c.join(" | ") + ")").join(" & ");
  const truth = satOf(clauses);
  for (const k of ["SIPSER_CLIQUE", "KARP_COLORING"]) {
    n++;
    const R = RV._run(k)(str), last = R.frames.at(-1);
    if (!!last.ok !== truth) { fails++; if (fails < 6) console.log("MISMATCH", k, str, "sat", truth, "got", last.ok); }
  }
}
for (let t = 0; t < 300; t++) {
  const nn = 2 + Math.floor(rnd() * 5), nodes = Array.from({ length: nn }, (_, i) => String(i + 1)), E = [];
  for (let i = 0; i < nn; i++) for (let j = i + 1; j < nn; j++) if (rnd() < .55) E.push(`{${nodes[i]},${nodes[j]}}`);
  const K = 1 + Math.floor(rnd() * nn);
  const str = `(({${nodes}},{${E}}),${K})`;
  const adj = new Set(E.map(e => e.slice(1, -1)).flatMap(e => [e, e.split(",").reverse().join(",")]));
  let has = false;
  for (let m = 0; m < 1 << nn && !has; m++) { const S = nodes.filter((_, i) => m & (1 << i)); if (S.length === K && S.every((a, i) => S.slice(i + 1).every(b => adj.has(a + "," + b)))) has = true; }
  n++;
  const R = RV._run("SIPSER_VC")(str), last = R.frames.at(-1);
  if (!!last.ok !== has) { fails++; if (fails < 10) console.log("MISMATCH VC", str, has, last.ok); }
}
for (const bad of [["SIPSER_CLIQUE", "(x1 | x2"], ["SIPSER_CLIQUE", "(a|b|c|d)"], ["SIPSER_VC", "(({1,2},{{1,9}}),1)"], ["SIPSER_VC", "nonsense"]]) {
  try { RV._run(bad[0])(bad[1]); console.log("no error for", bad); } catch (e) { console.log("err ok:", e.message); }
}
console.log(`checked ${n} instances, ${fails} mismatches`);
