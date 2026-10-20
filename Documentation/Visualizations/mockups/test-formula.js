// Tests for formula.js: the solvers against brute force, the frame rules (the Grouped Graph's, plus the variables map),
// Karp's 3SAT Reduction (answers map back and agree with brute force), and the layout (no overlap, wraps inside the width).
const { load } = require("./dom-shim.js");
const FV = load("formula.js", "FormulaView");
let fails = 0, n = 0;
const ok = (cond, msg) => { n++; if (!cond) { fails++; if (fails < 25) console.log("  FAIL:", msg); } };
let seed = 7; const rnd = () => (seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648;
const truth = spec => { const k = spec.names.length; for (let m = 0; m < 1 << k; m++) { const a = i => !!(m & (1 << (i - 1))); if (spec.clauses.every(c => c.some(l => (l > 0 ? a(l) : !a(-l))))) return true; } return false; };
const randomFormula = (maxW, nv) => Array.from({ length: 1 + Math.floor(rnd() * 6) }, () => "(" + Array.from({ length: 1 + Math.floor(rnd() * maxW) }, () => (rnd() < 0.5 ? "!" : "") + "x" + (1 + Math.floor(rnd() * nv))).join(" | ") + ")").join(" & ");

// parse: natural order, signed indices, 3SAT width check
{
  const s = FV.parse("(x10 | !x2) & (x2 | x1)");
  ok(s.names.join() === "x1,x2,x10", "names in natural order: " + s.names);
  ok(JSON.stringify(s.clauses) === "[[3,-2],[2,1]]", "signed 1-based indices: " + JSON.stringify(s.clauses));
  let threw = false; try { FV.parse("(a | b | c | d)", "SAT3"); } catch { threw = true; }
  ok(threw, "3SAT refuses a clause of four literals");
  const P = FV.payload(s);
  ok(P.groups[0].id === "c0" && P.groups[1].label === "C2" && P.nodes[1].id === "c0-1" && P.nodes[1].label === "¬x2" && P.nodes[1].variable === "v:x2" && P.nodes[1].positive === false, "Grouped Graph ids and labels");
  ok(FV.payload({ clauses: [[1, -2]], names: ["a-b", "c:d"] }).variables.map(v => v.id).join() === "v:a\\-b,v:c\\:d", "separators in names are escaped");
}
// solvers agree with brute-force truth
for (let t = 0; t < 400; t++) {
  const str = randomFormula(4, 1 + Math.floor(rnd() * 4)), spec = FV.parse(str), want = truth(spec);
  for (const k of ["dpll", "brute"]) {
    const run = FV.SOLVERS[k].run(spec), last = run.steps.at(-1);
    ok(last.event === "Done" && last.ok === want, `${k} on ${str}: ok ${last.ok}, want ${want}`);
    if (want) ok(spec.clauses.every(c => c.some(l => (l > 0 ? last.partial[spec.names[l - 1]] : !last.partial[spec.names[-l - 1]]))), `${k}'s answer satisfies ${str}`);
    // green only on the confirmed answer; Committed only in incremental runs; Done ok=false has no Solution anywhere
    const frames = FV.build(spec, run);
    frames.forEach((f, i) => {
      const states = [...Object.values(f.nodes), ...Object.values(f.variables)];
      if (f.event !== "Done" && !(f.event === "Accept" && frames[i + 1] && frames[i + 1].event === "Done" && frames[i + 1].ok)) ok(!states.includes("Solution"), `${k} frame ${i} of ${str} shows Solution before the answer`);
      if (!run.incremental) ok(!states.includes("Committed"), `${k} frame ${i} has Committed in a brute force`);
      if (f.event === "Done" && !f.ok) ok(!states.includes("Solution") && !states.includes("Committed"), `${k} failed Done of ${str} shows green`);
    });
  }
}
// the frame rules on one formula: (x1 ∨ ¬x2) ∧ (x2) ∧ (¬x1 ∨ ¬x2)
{
  const spec = FV.parse("(x1 | !x2) & (x2) & (!x1 | !x2)");
  const f = FV.frame(spec, { event: "Try", partial: { x1: true, x2: true }, focus: ["v:x2"], caption: "" }, { incremental: true });
  ok(f.nodes["c0-0"] === "Committed", "a kept true literal is Committed in an incremental run: " + f.nodes["c0-0"]);
  ok(f.nodes["c1-0"] === "ElementHighlight", "the tried variable's true literal is highlighted");
  ok(f.nodes["c0-1"] === "False", "the tried variable's false literal stays False: " + f.nodes["c0-1"]);
  ok(f.groups.c2 === "Rejected" && f.nodes["c2-0"] === "Rejected" && f.nodes["c2-1"] === "Rejected", "an all-false clause and its literals are Rejected");
  ok(f.groups.c0 === "Covered" && f.groups.c1 === "Covered", "clauses with a true literal are Covered");
  ok(f.variables["v:x1"] === "Committed" && f.variables["v:x2"] === "ElementHighlight", "variables map: kept and tried");
  ok(f.assignment["v:x1"] === true && f.assignment["v:x2"] === true, "assignment keyed by variable id");
  const r = FV.frame(spec, { event: "Reject", partial: { x1: true, x2: true }, focus: ["c2"], caption: "" }, { incremental: true });
  ok(r.groups.c2 === "Rejected", "a Reject step's focus clause is Rejected");
  const b = FV.frame(spec, { event: "Try", partial: { x1: true }, focus: [], caption: "" });
  ok(b.nodes["c0-0"] === "ElementHighlight" && !b.nodes["c1-0"], "a brute-force candidate is highlighted, an unassigned literal stays Background");
  const d = FV.frame(spec, { event: "Done", partial: { x1: true, x2: false }, focus: [], caption: "", ok: true });
  ok(d.nodes["c0-0"] === "Solution" && d.variables["v:x1"] === "Solution" && d.variables["v:x2"] === "False", "Done ok: true literals and variables Solution, false ones False");
  const e = FV.frame(spec, { event: "Done", partial: { x1: true }, focus: [], caption: "", ok: false });
  ok(e.nodes["c0-0"] === "Rejected" && e.variables["v:x1"] === "Rejected", "Done ok=false: picks Rejected");
  const run = FV.SOLVERS.stopped.run(FV.parse("(x1 | x2) & (!x1 | x3) & (!x3 | x4) & (!x4 | !x2) & (x5 | x6)"));
  const last = FV.build(FV.parse("(x1 | x2) & (!x1 | x3) & (!x3 | x4) & (!x4 | !x2) & (x5 | x6)"), run).at(-1);
  ok(last.event === "Done" && last.ok === false && !Object.keys(last.nodes).length && !Object.keys(last.variables).length && /^Stopped after/.test(last.caption), "a stopped run ends on a blank Done");
  // an Accept that ends the run is Solution
  const one = FV.SOLVERS.dpll.run(FV.parse("(x1) & (x1 | x2)")), fr = FV.build(FV.parse("(x1) & (x1 | x2)"), one);
  const endAcc = fr.findIndex((x, i) => x.event === "Accept" && fr[i + 1] && fr[i + 1].event === "Done" && fr[i + 1].ok);
  if (endAcc >= 0) ok(fr[endAcc].nodes["c0-0"] === "Solution" || fr[endAcc].nodes["c0-0"] === "ElementHighlight", "an Accept that ends the run draws its picks as the answer");
}
// Karp's 3SAT Reduction: every target clause has three literals, and the mapped answer agrees with the truth
for (let t = 0; t < 250; t++) {
  const str = randomFormula(6, 1 + Math.floor(rnd() * 4)), want = truth(FV.parse(str));
  let R; try { R = FV._karp(str); } catch (e) { ok(false, "karp threw on " + str + ": " + e.message); continue; }
  ok(R.tgt.clauses.every(c => c.length === 3), "every 3SAT clause has three literals: " + R.targetInstance);
  ok(truth(R.tgt) === want, `3SAT is satisfiable exactly when SAT is: ${str} ↦ ${R.targetInstance}`);
  const last = R.frames.at(-1);
  ok(!!last.ok === want, `the view ends ok=${last.ok}, want ${want}: ${str}`);
  ok(R.frames[0].phase === "build" && R.frames.some(f => f.phase === "solve"), "phases in order");
  // #840 and ReductionView.Create: build ends on a plain Accept; only the last frame is a Done and carries ok
  const lastBuild = R.frames.filter(f => f.phase === "build").at(-1);
  ok(lastBuild.event === "Accept" && lastBuild.ok === undefined, "the build ends on a plain 'target built' frame: " + str);
  ok(R.frames.slice(0, -1).every(f => f.event !== "Done" && f.ok === undefined && !f.done), "no frame before the last is a Done or has ok: " + str);
  ok(last.event === "Done" && last.phase === "map", "the last frame is the map's Done: " + str);
  ok(R.frames.filter(f => f.phase === "solve").every(f => f.event === "Accept" || f.event === "Reject"), "the solve ends on an Accept or a Reject, not a Done: " + str);
  if (want) {
    // the final frame: both sides agree on every original variable, the SAT side is the answer
    const sa = last.src.assignment, ta = last.tgt.assignment;
    ok(R.src.names.every(n => sa["v:" + n] !== undefined && sa["v:" + n] === ta["v:" + n]), "both sides agree on every original variable: " + str);
    ok(Object.keys(sa).length === R.src.names.length, "the SAT side has only its own variables: " + Object.keys(sa));
    ok(Object.values(last.src.groups).length === R.src.clauses.length && Object.values(last.src.groups).every(x => x === "Covered"), "every SAT clause ✓ in the final frame: " + str);
    ok(Object.values(last.src.nodes).every(x => x === "Solution" || x === "False") && Object.values(last.src.nodes).includes("Solution"), "the SAT side's true literals are Solution: " + str);
    ok(Object.values(last.src.variables).every(x => x === "Solution" || x === "False"), "the SAT side's chips are the answer: " + str);
  }
  const ids = new Set([...R.Pt.groups.map(g => "t:" + g.id), ...R.Pt.nodes.map(x => "t:" + x.id), ...R.Pt.variables.map(v => "t:" + v.id), ...R.Ps.groups.map(g => "s:" + g.id), ...R.Ps.nodes.map(x => "s:" + x.id), ...R.Ps.variables.map(v => "s:" + v.id)]);
  ok(R.gadgets.every(g => [...g.from, ...g.to].every(id => ids.has(id))), "every gadget id is a piece of one of the two pictures: " + str);
  ok(R.Ps.nodes.every(x => R.gadgets.some(g => g.kind === "element" && g.from.includes("s:" + x.id) && g.to.length)), "every source literal maps to a target literal: " + str);
}
// Karp's split names and order follow KarpSATToSAT3
{
  const R = FV._karp("(x1 | x2 | x3 | x4 | x5)");
  ok(R.targetInstance === "(x1 | x2 | !x7) & (x4 | x5 | x6) & (x3 | !x6 | x7)", "splits take the last two literals and new names from x6: " + R.targetInstance);
  const P = FV._karp("(a) & (a | !b)");
  ok(P.targetInstance === "(a | a | a) & (a | !b | a)", "short clauses are padded by repeating: " + P.targetInstance);
}
// layout: inside the width, no two boxes overlap, clauses wrap at boundaries
{
  const spec = FV.parse(Array.from({ length: 14 }, (_, i) => `(x${i + 1} | !x${(i + 3) % 14 + 1} | x${(i + 5) % 14 + 1})`).join(" & "));
  const L = FV.layout(FV.payload(spec), 960);
  ok(L.clauses.every(c => c.x >= 0 && c.x + c.w <= 960), "every clause box fits the width");
  ok(new Set(L.clauses.map(c => c.y)).size > 1, "a long formula wraps onto more lines");
  const over = (a, b) => a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h;
  ok(L.clauses.every((a, i) => L.clauses.every((b, j) => i >= j || !over(a, b))), "clause boxes do not overlap");
  L.clauses.forEach(c => c.members.forEach(id => { const m = L.lits.get(id); ok(m.x >= c.x && m.x + m.w <= c.x + c.w && m.y >= c.y && m.y + m.h <= c.y + c.h, `literal ${id} sits inside its clause box`); }));
  ok(L.ands.length === spec.clauses.length - 1, "one ∧ between each two clauses");
  ok(L.ors.length === spec.clauses.reduce((a, c) => a + c.length - 1, 0), "one ∨ between each two literals of a clause");
  const wide = FV.layout(FV.payload(FV.parse("(" + Array.from({ length: 30 }, (_, i) => "x" + (i % 16 + 1)).join(" | ") + ")")), 960);
  ok(wide.clauses[0].x + wide.clauses[0].w <= 960 && wide.clauses[0].h > 48, "a clause wider than the line wraps at its ∨s inside a taller box");
}
console.log(fails ? `${fails} FAILURES (${n} checks)` : `ALL PASS (${n} checks)`);
process.exit(fails ? 1 : 0);
