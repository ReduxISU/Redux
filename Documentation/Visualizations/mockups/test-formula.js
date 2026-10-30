// Tests for formula.js: the solvers against brute force, the frame rules (the Grouped Graph's, plus the variables map),
// Karp's 3SAT Reduction (answers map back and agree with brute force), and the layout (no overlap, wraps inside the width).
// The proposed generalization: CNF lays out as before, DNF swaps the operators, the operator table and parentheses (an
// unknown operator is name(a, b)), the equation layout and its wrapping, integer variables, Prime Factorization / 0-1 ILP /
// Subset Sum frames following their Redux solvers, and the legend worded from the layout.
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
  const L = FV.layout(FV.payload(spec), 800);
  ok(L.groups.every(c => c.x >= 0 && c.x + c.w <= 800), "every clause box fits the width");
  ok(new Set(L.groups.map(c => c.y)).size > 1, "a long formula wraps onto more lines");
  const over = (a, b) => a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h;
  ok(L.groups.every((a, i) => L.groups.every((b, j) => i >= j || !over(a, b))), "clause boxes do not overlap");
  L.groups.forEach(c => c.members.forEach(id => { const m = L.pieces.get(id); ok(m.x >= c.x && m.x + m.w <= c.x + c.w && m.y >= c.y && m.y + m.h <= c.y + c.h, `literal ${id} sits inside its clause box`); }));
  ok(L.joins.length === spec.clauses.length - 1 && L.joins.every(a => a.text === "∧"), "one ∧ between each two clauses");
  ok(L.ops.length === spec.clauses.reduce((a, c) => a + c.length - 1, 0) && L.ops.every(o => o.text === "∨"), "one ∨ between each two literals of a clause");
  const wideStr = "(" + Array.from({ length: 30 }, (_, i) => "x" + (i % 16 + 1)).join(" | ") + ")";
  for (const W of [800, 500, 320]) {
    const wide = FV.layout(FV.payload(FV.parse(wideStr)), W);
    ok(wide.groups[0].x + wide.groups[0].w <= W - 12 && wide.groups[0].h > 48, `a clause wider than the line wraps at its ∨s inside a taller box (${W})`);
    // after another clause (so it starts behind an ∧ on a fresh line) it still fits: the old greedy left the leading ∨ out
    const after = FV.layout(FV.payload(FV.parse("(x1 | x2) & " + wideStr)), W);
    ok(after.groups.every(c => c.x + c.w <= W - 12), `a wrapped wide clause after another stays inside the width (${W})`);
  }
}
// CNF lays out as before the generalization: the same boxes, pills, ∧s, ∨s and chips (a fingerprint of the old layout at
// top = 34 and widths 800 / 500 / 320). The free-floating heading above it is gone, so the default top is now 10.
{
  const r = v => Math.round(v * 100) / 100, out = [];
  for (const s of ["(x1 | !x2 | x3 | !x4) & (!x1 | x2) & (!x3 | x4 | x5) & (!x2 | !x5) & (x4 | !x1)",
    "(x1 | x2 | !x3) & (!x1 | x4) & (x2 | !x4 | x5 | x6) & (!x2 | x3) & (!x5 | !x6) & (x3 | x6 | !x7) & (x7 | !x8) & (!x3 | x8 | x1) & (x5 | x7) & (!x6 | !x7 | x8) & (x4 | !x8) & (!x4 | x6 | x2)",
    "(a | !foo_2 | x10) & (Bar7)"]) for (const W of [800, 500, 320]) {
    const L = FV.layout(FV.payload(FV.parse(s)), W, 34);
    out.push(L.groups.map(c => [c.id, r(c.x), r(c.y), r(c.w), r(c.h)].join()).join(";"));
    out.push([...L.pieces.values()].map(m => [m.id, r(m.x), r(m.y), r(m.w), r(m.h)].join()).join(";"));
    out.push(L.joins.map(a => [r(a.x), r(a.y)].join()).join(";"), L.ops.map(a => [r(a.x), r(a.y)].join()).join(";"));
    out.push(L.vars.map(v => [v.id, r(v.x), r(v.y), r(v.w), r(v.h)].join()).join(";"), r(L.stripY), r(L.height));
  }
  const str = out.join("|");
  let h = 2166136261; for (let i = 0; i < str.length; i++) { h ^= str.charCodeAt(i); h = Math.imul(h, 16777619) >>> 0; }
  ok(h.toString(16) === "fd1ead7a" && str.length === 6964, `CNF layout matches the pre-generalization one: ${h.toString(16)} / ${str.length}`);
  ok(FV.layout(FV.payload(FV.parse("(x1 | x2)")), 800).groups[0].y === 10 + 18, "the default top leaves no room for a heading");
}
// DNF: the same picture with the operators swapped, and its frame rules
{
  const spec = FV.parse("(x1 & !x2) | (!x1 & x3) | (x2 & x3)", "DNF"), P = FV.payload(spec);
  ok(P.layout === "dnf" && P.groups[1].label === "D2", "a DNF payload: layout dnf, terms D1, D2, …");
  const L = FV.layout(P, 800);
  ok(L.joins.length === 2 && L.joins.every(a => a.text === "∨"), "terms are joined by ∨");
  ok(L.ops.length === 3 && L.ops.every(o => o.text === "∧"), "literals inside a term are joined by ∧");
  ok(/joined by ∨; the literals inside are joined by ∧ \(DNF\)/.test(FV.legend(P)) && /One term that holds is enough/.test(FV.legend(P)), "DNF legend: " + FV.legend(P));
  const f = FV.frame(spec, { event: "Try", partial: { x1: true, x2: false, x3: false }, focus: [], caption: "" });
  ok(f.groups.c0 === "Covered" && f.groups.c1 === "False" && f.groups.c2 === "False", "a term with every literal true is Covered, one with a false literal False: " + JSON.stringify(f.groups));
  const g = FV.frame(spec, { event: "Reject", partial: { x1: true, x2: true, x3: false }, focus: [], caption: "" });
  ok(Object.values(g.groups).every(s => s === "Rejected"), "when every term is false they are all Rejected: " + JSON.stringify(g.groups));
  const run = FV.SOLVERS.brute.run(spec), last = run.steps.at(-1);
  ok(last.ok === true && spec.clauses.some(c => c.every(l => (l > 0 ? last.partial[spec.names[l - 1]] : !last.partial[spec.names[-l - 1]]))), "the DNF brute force ends on an assignment with a true term");
  ok(FV.SOLVERS.brute.run(FV.parse("(x1 & !x1) | (x2 & !x2)", "DNF")).steps.at(-1).ok === false, "contradictory terms: no assignment");
  ok(FV.textOf(spec) === "(x1 & !x2) | (!x1 & x3) | (x2 & x3)", "DNF round trip: " + FV.textOf(spec));
}
// the CNF legend, derived from the layout
{
  const L = FV.legend(FV.payload(FV.parse("(x1 | !x2) & (x2)")));
  ok(/^Boxes are clauses, joined by ∧; the literals inside are joined by ∨ \(CNF\)\. Every clause must hold: a clause holds when one of its literals is true\./.test(L) && /\(T or F; – while it is not set\)/.test(L), "CNF legend: " + L);
}
// operators: one table, parentheses by precedence, unknown names written name(a, b)
{
  const T = e => FV._exprTokens(e, new Map(), "g").map(t => t.text).join(" ");
  const x = id => ({ id, var: "v:" + id }), n = (id, v) => ({ id, num: v });
  ok(T({ op: "xor", args: [x("a"), x("b")] }) === "xor( v:a , v:b )", "an unknown operator is written by name: " + T({ op: "xor", args: [x("a"), x("b")] }));
  ok(T({ op: "and", args: [{ op: "or", args: [x("a"), x("b")] }, x("c")] }) === "( v:a ∨ v:b ) ∧ v:c", "a looser child gets parentheses");
  ok(T({ op: "or", args: [{ op: "and", args: [x("a"), x("b")] }, x("c")] }) === "v:a ∧ v:b ∨ v:c", "a tighter child does not");
  ok(T({ op: "minus", args: [x("a"), { op: "minus", args: [x("b"), x("c")] }] }) === "v:a − ( v:b − v:c )", "minus on the right keeps its parentheses");
  ok(T({ op: "plus", args: [x("a"), { op: "plus", args: [x("b"), x("c")] }] }) === "v:a + v:b + v:c", "plus is associative");
  ok(T({ op: "not", args: [x("a")] }) === "¬v:a", "not over a variable folds into one ¬ pill");
  ok(T({ op: "not", args: [{ op: "or", args: [x("a"), x("b")] }] }) === "¬ ( v:a ∨ v:b )", "not over an expression is a prefix with parentheses");
  ok(T({ op: "sum", args: [n("p", 1), n("q", 7)] }) === "Σ( 1 , 7 )", "sum is Σ(…)");
  ok(T({ op: "le", args: [{ op: "mul", args: [n("c", 2), x("x1")] }, n("d", -5)] }) === "2 v:x1 ≤ −5", "juxtaposition has no symbol; a negative number has a real minus");
  for (const [name, o] of Object.entries(FV.OPS)) ok(typeof o.sym === "string" && ["infix", "prefix", "juxt", "call"].includes(o.fix) && typeof o.prec === "number", "operator table entry " + name);
  // a rows payload with an unknown operator lays out and the legend says what it is
  const P = { kind: "formula", layout: "rows", outer: "and", groups: [{ id: "g0", label: "G1", expr: { id: "o", op: "xor", args: [x("a"), x("b")] } }], variables: [{ id: "v:a", label: "a" }, { id: "v:b", label: "b" }] };
  const L = FV.layout(P, 800);
  ok(L.ops.map(o => o.text).join(" ") === "xor( , )" && [...L.pieces.values()].map(p => p.text).join() === "a,b", "an unknown operator is laid out as xor(a, b)");
  ok(/xor\(…\) is the operator "xor", written by name/.test(FV.legend(P)), "the legend explains the unknown operator: " + FV.legend(P));
}
// the equation layout, and its wrapping at top-level operators first
{
  const pp = FV.PROBLEMS.PRIMEFACTOR, spec = pp.parse("60"), P = pp.payload(spec);
  ok(P.layout === "equation" && FV.legend(P).startsWith("One equation, wrapped at its top-level operators; it holds when both sides are equal."), "equation legend: " + FV.legend(P));
  const last = pp.run(spec).frames.at(-1);
  const L = FV.layout({ ...P, groups: [{ ...P.groups[0], expr: last.exprs.e0 }] }, 800);
  ok(L.ops.map(o => o.text).join(" ") === "= × × ×" && [...L.pieces.values()].map(p => p.text).join(" ") === "60 2 2 3 5" && L.groups[0].lines === 1, "60 = 2 × 2 × 3 × 5 on one line");
  // a long product wraps at its ×s, each new line led by its operator, and every piece stays inside the width
  const long = { kind: "formula", layout: "equation", groups: [{ id: "e0", label: "", expr: { id: "eq", op: "eq", args: [{ id: "n", num: 123456789 }, { id: "p", op: "times", args: Array.from({ length: 14 }, (_, k) => ({ id: "f" + k, num: 1000 + k })) }] } }], variables: [] };
  for (const W of [800, 500, 320]) {
    const LL = FV.layout(long, W), g = LL.groups[0];
    ok([...LL.pieces.values()].every(p => p.x + p.w <= W - 12) && g.x + g.w <= W - 12, `long equation fits ${W}`);
    ok(g.lines > 1, `long equation wraps at ${W}`);
    const lineStarts = [...Array(g.lines).keys()].slice(1).map(k => g.y + 9 + k * 38 + 15);
    ok(lineStarts.every(y => LL.ops.some(o => o.y === y && o.x === g.x + 9 + 12 && o.text === "×")), `${W}: every new line starts with its ×`);
  }
  // Σ(…) = T too wide for one line: it breaks at "=", not inside Σ(…)
  const sumEq = { kind: "formula", layout: "equation", groups: [{ id: "e0", expr: { op: "eq", args: [{ op: "sum", args: Array.from({ length: 8 }, (_, k) => ({ id: "s" + k, num: 100 + k })) }, { id: "T", num: 1234 }] } }], variables: [] };
  const SE = FV.layout(sumEq, 500);
  ok(SE.groups[0].lines === 2 && SE.pieces.get("T").y > SE.pieces.get("s7").y && SE.pieces.get("s0").y === SE.pieces.get("s7").y, "Σ(…) = T breaks at = first");
  // a sum of products: + is shallower than ×, so a×b + c×d + … breaks at +, never inside a product
  const terms = Array.from({ length: 10 }, (_, k) => ({ op: "times", args: [{ id: "a" + k, num: 11 + k }, { id: "b" + k, num: 21 + k }] }));
  const SL = FV.layout({ kind: "formula", layout: "equation", groups: [{ id: "e0", expr: { op: "plus", args: terms } }], variables: [] }, 320);
  ok(SL.groups[0].lines > 1 && terms.every((_, k) => SL.pieces.get("a" + k).y === SL.pieces.get("b" + k).y), "breaks at +, never inside a product");
}
// Prime Factorization: frames follow Trial Division; integers in the strip
{
  const pp = FV.PROBLEMS.PRIMEFACTOR;
  const evs = s => pp.run(pp.parse(s)).frames.slice(1).map(f => f.event[0]).join("");
  ok(evs("60") === "AAAAD", "60: ÷2, ÷2, ÷3, then 5 is the last prime, Done: " + evs("60"));
  ok(evs("1001") === "RRRRRARRRAAD", "1001: 2–6 fail, ÷7, 8–10 fail, ÷11, 13 is prime: " + evs("1001"));
  ok(evs("97") === "RRRRRRRRAD", "97: 2–9 fail, so 97 is prime: " + evs("97"));
  ok(evs("8") === "AAAD", "8: ÷2 three times and nothing is left: " + evs("8"));
  const f = pp.run(pp.parse("60")).frames;
  ok(f[1].caption === "60 ÷ 2 = 30, so 2 is a prime factor." && f[4].caption === "No number from 2 to √5 divides 5, so 5 is prime: the last factor." && f.at(-1).caption === "60 = 2 × 2 × 3 × 5.", "captions follow PrimeFactorSolver");
  ok(f[2].nodes.f0 === "Committed" && f[2].nodes.f1 === "ElementHighlight" && f[2].assignment["v:rest"] === 15 && f[2].assignment["v:i"] === 2, "a kept factor is Committed, the new one highlighted, rest = 15 in the strip");
  ok(f.at(-1).ok === true && f.at(-1).groups.e0 === "Covered" && ["f0", "f1", "f2", "f3"].every(k => f.at(-1).nodes[k] === "Solution") && !JSON.stringify(f.at(-1).exprs).includes('"rest"'), "Done: every factor Solution, the equation Covered, nothing left over");
  const r = pp.run(pp.parse("1001")).frames[1];
  ok(r.event === "Reject" && r.variables["v:i"] === "Rejected" && r.caption === "2 does not divide 1,001.", "a divisor that fails: its chip Rejected, the caption in Redux's N0 format");
  const L = FV.layout(pp.payload(pp.parse("60")), 800);
  ok(L.vars.every(v => v.type === "int") && L.stripLabel === "VALUES", "integer chips, the strip labelled VALUES");
  for (const s of ["60", "1001", "97", "8", "360"]) pp.run(pp.parse(s)).frames.slice(1).forEach((fr, i) => ok(pp.checks(pp.parse(s), fr)[1].ok === true, `${s} frame ${i + 1}: factors × what is left = n`));
}
// 0-1 ILP: rows of linear constraints, stacked, joined by ∧; a violated row is Rejected
{
  const ip = FV.PROBLEMS.INTPROGRAMMING01, spec = ip.parse("(2 3 -1),(-1 -1 0),(1 0 4)<=(5 -1 4)"), P = ip.payload(spec);
  const L = FV.layout(P, 800);
  ok(L.ops.filter(o => o.group === "r0").map(o => o.text).join(" ") === "+ − ≤" && [...L.pieces.values()].filter(p => p.group === "r0").map(p => p.text).join(" ") === "2 x1 3 x2 x3 5", "row 1 reads 2x₁ + 3x₂ − x₃ ≤ 5");
  ok(L.ops.filter(o => o.group === "r1").map(o => o.text).join(" ") === "− − ≤" && L.pieces.get("r1-d").text === "−1", "row 2 reads −x₁ − x₂ ≤ −1");
  ok(L.joins.length === 2 && L.joins.every(j => j.text === "∧" && j.x === 12 + 17) && new Set(L.groups.map(g => g.x)).size === 1 && new Set(L.groups.map(g => g.y)).size === 3, "stacked: a row per line, ∧ leading rows 2 and 3, boxes aligned");
  ok(/Every constraint must hold/.test(FV.legend(P)), "ILP legend: " + FV.legend(P));
  const run = ip.run(spec), fr = run.frames, t1 = fr[1];
  ok(t1.event === "Reject" && t1.caption === "Try (0 0 0): row 2 adds up to 0, over its bound -1." && t1.groups.r1.state === "Rejected" && t1.groups.r0.state === "Covered", "the first try (0 0 0) breaks row 2, which is Rejected");
  ok(t1.groups.r1.note === "total 0 > −1" && t1.focus.join() === "r1", "the rejected row's note and focus: " + t1.groups.r1.note);
  ok(fr[2].assignment["v:x1"] === 1 && fr[2].assignment["v:x2"] === 0, "x₁ flips fastest, as the counter does");
  ok(run.ok === true && fr.at(-1).event === "Done" && Object.values(fr.at(-1).groups).every(g => g.state === "Covered"), "an answer exists and every row holds at the end");
  const none = ip.run(ip.parse("(-1 -1 0),(1 1 0),(0 0 1)<=(-2 1 1)"));
  ok(none.ok === false && none.frames.at(-1).event === "Done" && none.frames.at(-1).assignment["v:x1"] === 1 && Object.values(none.frames.at(-1).variables).includes("Rejected"), "no answer: the Done keeps the last try, picks Rejected (#905)");
  ok(FV.layout(P, 320).groups.every(g => g.x + g.w <= 320 - 12), "ILP rows fit a phone width");
  const L2 = FV.layout(ip.payload(ip.parse("(1 1 1 1 1 1 1 1)<=(4)")), 320);
  ok(L2.groups[0].lines > 1 && [...L2.pieces.values()].every(p => p.x + p.w <= 320 - 12), "a long row wraps inside its box at a phone width");
}
// Subset Sum: Σ(chosen) = T
{
  const ss = FV.PROBLEMS.SUBSETSUM, run = ss.run(ss.parse("({1,7,12,15},28)"));
  ok(run.frames.length === 1 + 13 + 1 && run.frames.at(-2).caption === "Try {1,12,15}: 1 + 12 + 15 = 28. That's T." && run.frames.at(-1).ok === true, "13 subsets in mask order, ending on {1,12,15}");
  const f = run.frames[3];
  ok(f.nodes.s0 === "ElementHighlight" && f.nodes.s1 === "ElementHighlight" && f.nodes.s2 === "False" && f.groups.e0.state === "Rejected" && f.groups.e0.note === "sum 8 ≠ 28", "{1,7}: chosen highlighted, the rest False, the equation Rejected");
  const none = ss.run(ss.parse("({3,5,9},7)"));
  ok(none.ok === false && !Object.keys(none.frames.at(-1).nodes).length, "no subset: the Done is blank");
}
// no frame shows green before the answer, in the new problems either
for (const [k, s] of [["PRIMEFACTOR", "360"], ["INTPROGRAMMING01", "(-1 -1 -1 -1),(1 1 0 0),(0 0 1 1),(1 0 1 0)<=(-2 1 1 1)"], ["SUBSETSUM", "({1,7,12,15},28)"], ["DNF", "(x1 & !x2) | (!x1 & x3 & x2)"]]) {
  const fr = FV.PROBLEMS[k].run(FV.PROBLEMS[k].parse(s), FV.PROBLEMS[k].solver).frames;
  fr.forEach((f, i) => {
    const states = [...Object.values(f.nodes), ...Object.values(f.variables)].map(v => (v && v.state) || v);
    const endsRun = f.event === "Accept" && fr[i + 1] && fr[i + 1].event === "Done" && fr[i + 1].ok;
    if (f.event !== "Done" && !endsRun) ok(!states.includes("Solution"), `${k} frame ${i} shows Solution before the answer`);
  });
}
console.log(fails ? `${fails} FAILURES (${n} checks)` : `ALL PASS (${n} checks)`);
process.exit(fails ? 1 : 0);
