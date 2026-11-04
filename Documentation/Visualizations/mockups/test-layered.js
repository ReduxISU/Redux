const fs = require("fs");
const src = fs.readFileSync("layered.js", "utf8").replace("return { create, parse, PROBLEMS };", "return { create, parse, PROBLEMS, SOLVE, evaluate, layout };");
const LV = eval(src + ";LayeredView");
for (const [k, P] of Object.entries(LV.PROBLEMS)) for (const [nm, s] of P.examples) for (const [sv] of P.solvers) {
  const G = LV.parse(s, P);
  const fr = LV.SOLVE[sv](G, P.rule);
  const last = fr.at(-1);
  // evaluate every frame to catch crashes
  fr.forEach(f => LV.evaluate(G, P.rule, f));
  const L = LV.layout(G);
  console.log(`${k} | ${nm} | ${sv} | frames ${fr.length} | ok ${last.ok} | back ${L.back.size} | ${last.caption}`);
  console.log("   checks:", LV.evaluate(G, P.rule, last).checks.map(c => c.label + (c.ok === true ? " ✓" : c.ok === false ? " ✗" : "")).join(" ; "));
}
for (const [bad, P] of [["({1,2},{(1,3)})", LV.PROBLEMS.TOPOSORT], ["({1,2},{((1,2),4)})", LV.PROBLEMS.SSSP], ["({1,2},{(1,2)},1)", LV.PROBLEMS.SSSP], ["(({1,2},{(1,2)}))", LV.PROBLEMS.ARCSET]]) {
  try { LV.parse(bad, P); console.log("NO ERROR for", bad); } catch (e) { console.log("err:", e.message); }
}
