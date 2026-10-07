const fs = require("fs");
const src = fs.readFileSync("graphbase.js", "utf8").replace("return { create, parse, PROBLEMS };", "return { create, parse, PROBLEMS, SOLVERS, evaluate };");
const GV = eval(src + ";GraphView");
let fails = 0;
for (const [k, P] of Object.entries(GV.PROBLEMS)) for (const [nm, s] of P.examples) for (const [sv, svName] of P.solvers) {
  try {
    const G = GV.parse(s, P);
    const fr = GV.SOLVERS[P.rule][sv](G);
    // every frame must evaluate without throwing
    fr.forEach(f => GV.evaluate(G, P.rule, f));
    const last = fr.at(-1), ev = GV.evaluate(G, P.rule, last);
    const checks = ev.checks.map(c => (c.ok === true ? "✓ " : c.ok === false ? "✗ " : "· ") + c.label).join(" ; ");
    console.log(`${k} | ${nm} | ${sv} | frames ${fr.length} | ok ${last.ok}\n    ${last.caption}\n    ${checks}`);
  } catch (e) { fails++; console.log(`FAIL ${k} ${nm} ${sv}: ${e.stack.split("\n").slice(0, 2).join(" ")}`); }
}
// bad input
for (const [k, s] of [["STEINERTREE", "(({1,2},{{1,2}}),{9},1)"], ["STEINERTREE", "(({1,2},{{1,2}}),1)"], ["MINIMUMSPANNINGTREE", "({1,2},{{1,2}})"]]) {
  try { GV.parse(s, GV.PROBLEMS[k]); console.log("NO ERROR for", k, s); fails++; } catch (e) { console.log("err ok:", e.message); }
}
console.log("fails:", fails);
