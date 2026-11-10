// Drawing tests for formula.js: one pill per literal occurrence, ∧ between clauses and ∨ inside them (not the other way
// round), state classes read from the frame, the ✓/✗ clause marks, the strip, backend frames through loadFrames, and the
// SAT ↦ 3SAT pair (hidden clauses during the build, gadget colors). The proposed generalization: no free-floating text (every
// <text> inside a g[data-piece]; no FORMULA heading; ASSIGNMENT inside the strip), DNF, an unknown operator, the equation
// layout with integer chips and frames that rebuild it, 0-1 ILP rows with a Rejected row and its note, and Subset Sum.
const { load, svg } = require("./dom-shim.js");
const FV = load("formula.js", "FormulaView");
let fails = 0;
const ok = (cond, msg) => { if (!cond) { fails++; console.log("  FAIL:", msg); } };
const cls = el => el.getAttribute("class") || "";

{
  const s = svg(), v = FV.create({ svg: s });
  const r = v.load("(x1 | !x2) & (x2) & (!x1 | !x2)", "SAT", "dpll");
  const lits = s.querySelectorAll("g.fm-lit"), clauses = s.querySelectorAll("g.fm-clause");
  ok(lits.length === 5 && clauses.length === 3, "a pill per occurrence, a box per clause");
  ok(s.querySelectorAll("text.fm-and").length === 2 && s.querySelectorAll("text.fm-and").every(t => t.textContent === "∧"), "∧ between clauses");
  ok(s.querySelectorAll("text.fm-op").length === 2 && s.querySelectorAll("text.fm-op").every(t => t.textContent === "∨"), "∨ inside clauses");
  ok(lits[1].querySelector("tspan.base").textContent === "¬x" && lits[1].querySelector("tspan.sub").textContent === "2", "negation written ¬x, the digits a real subscript: " + lits[1].querySelector("text").textContent);
  // subscripts: letters+digits get one at ≥ 75% of the label size; any other name is drawn plain
  {
    const s2 = svg(), v2 = FV.create({ svg: s2 });
    v2.load("(a | !foo_2 | x10) & (Bar7)", "SAT", "dpll");
    const L2 = s2.querySelectorAll("g.fm-lit"), by = t => L2.find(l => l.getAttribute("aria-label").startsWith(t));
    ok(!by("a ").querySelector("tspan.sub") && by("a ").querySelector("tspan.base").textContent === "a", "a is drawn plain");
    ok(!by("¬foo_2").querySelector("tspan.sub") && by("¬foo_2").querySelector("tspan.base").textContent === "¬foo_2", "foo_2 is drawn plain");
    const x10 = by("x10").querySelector("tspan.sub");
    ok(x10 && x10.textContent === "10" && +x10.getAttribute("font-size") >= 0.75 * 15, "x10: subscript 10 at ≥ 75% of 15 px: " + (x10 && x10.getAttribute("font-size")));
    ok(by("Bar7").querySelector("tspan.sub").textContent === "7", "Bar7: subscript 7");
    // the False strike ends before the subscript starts (by the mono estimate) and the pill holds the whole label
    L2.forEach(l => {
      const t = l.querySelector("text"), line = l.querySelector("line.strike"), r = l.querySelector("rect");
      const base = l.querySelector("tspan.base").textContent, sub = l.querySelector("tspan.sub");
      const x0 = +t.getAttribute("x"), baseEnd = x0 + base.length * 9 * 0.9;
      ok(+line.getAttribute("x2") <= baseEnd + 0.01, "strike stays on the letters: " + l.getAttribute("aria-label"));
      const end = x0 + base.length * 9 + (sub ? sub.textContent.length * 9 * 0.78 : 0);
      ok(x0 >= +r.getAttribute("x") && end <= +r.getAttribute("x") + +r.getAttribute("width"), "label inside its pill: " + l.getAttribute("aria-label"));
    });
  }
  ok(s.querySelectorAll("g.fm-var").length === 2, "a chip per variable");
  v.show(null);
  ok(lits.every(l => cls(l) === "fm-lit Background"), "the instance view is all Background");
  const last = r.frames.length - 1, f = v.show(last);
  ok(f.event === "Done", "the last frame is the Done");
  ok(clauses.some(c => cls(c).includes("Rejected")) === (f.ok === false && Object.values(f.groups).includes("Rejected")), "clause classes follow the frame");
  // a backend frame, drawn as sent
  const P = r.payload;
  v.loadFrames(P, [{ event: "Reject", caption: "", focus: ["c2"], nodes: { "c0-0": { state: "Committed" }, "c2-0": "Rejected", "c2-1": "Rejected", "c0-1": "False" }, groups: { c0: "Covered", c2: "Rejected" }, assignment: { "v:x1": true, "v:x2": true }, variables: { "v:x1": "Committed", "v:x2": "ElementHighlight" } }]);
  v.show(0);
  const L = s.querySelectorAll("g.fm-lit"), C = s.querySelectorAll("g.fm-clause"), V = s.querySelectorAll("g.fm-var");
  ok(cls(L[0]) === "fm-lit Committed" && cls(L[1]) === "fm-lit False" && cls(L[3]) === "fm-lit Rejected", "literal classes from the frame: " + L.map(cls).join(" | "));
  ok(cls(C[0]) === "fm-clause Covered" && C[0].querySelector("text.mark").textContent === "✓", "Covered clause marked ✓");
  ok(cls(C[2]) === "fm-clause Rejected" && C[2].querySelector("text.mark").textContent === "✗", "Rejected clause marked ✗");
  ok(cls(V[0]) === "fm-var Committed" && V[0].querySelector("tspan.val").textContent === "T", "chip state and value");
  ok(cls(V[1]) === "fm-var ElementHighlight", "the tried variable's chip is highlighted");
  // hover a literal: its variable's literals and chip trace, the rest fade
  L[0].dispatch("pointerenter");
  ok(cls(L[0]).includes("trace") && cls(L[3]).includes("trace") && cls(L[1]).includes("faint") && cls(V[0]).includes("trace"), "hover traces the variable: " + L.map(cls).join(" | "));
  L[0].dispatch("pointerleave");
}
{
  const s = svg(), v = FV.createPair({ svg: s });
  const r = v.load("(x1 | x2 | x3 | x4 | x5) & (x1)", "KARP_SAT3");
  v.show(0);
  const clauses = s.querySelectorAll("g.fm-clause");
  ok(clauses.length === 2 + 4, "both formulas drawn: 2 SAT clauses, 4 3SAT clauses");
  ok(clauses.filter(c => cls(c).includes("fm-hidden")).length === 1, "during the first build step the padded clause is still hidden: " + clauses.map(cls).join(" | "));
  ok(clauses.some(c => cls(c).includes("ElementHighlight")), "the step's pieces are highlighted");
  v.setGadgetColors(true); v.show(r.frames.length - 1);
  ok(s.querySelectorAll("g.fm-clause").filter(c => / g\d/.test(cls(c))).length === 6, "gadget colors on every clause of both sides");
  ok(r.ok === true && r.frames.at(-1).phase === "map", "the view ends on the mapped answer");
}
// The hard rule: every <text> belongs to a piece (an ancestor <g data-piece>) and moves with it; no free-floating heading.
const floating = s => s.querySelectorAll("text").filter(t => { for (let p = t.parentNode; p && p.tagName !== "svg"; p = p.parentNode) if (p.tagName === "g" && p.getAttribute("data-piece")) return false; return true; });
const owner = t => { for (let p = t.parentNode; p; p = p.parentNode) if (p.getAttribute && p.getAttribute("data-piece")) return p; return null; };
{
  const s = svg(), v = FV.create({ svg: s });
  v.load("(x1 | !x2) & (x2) & (!x1 | !x2)", "SAT", "dpll");
  ok(!s.querySelectorAll("text").some(t => /FORMULA/.test(t.textContent)) && !s.querySelectorAll("text.fm-head").length, "no FORMULA · CNF heading");
  ok(floating(s).length === 0, "every text in the CNF picture belongs to a piece: " + floating(s).map(t => t.textContent).join(" | "));
  const lab = s.querySelectorAll("text.fm-slabel")[0];
  ok(lab && lab.textContent === "ASSIGNMENT" && owner(lab).getAttribute("data-piece") === "strip" && owner(lab).querySelectorAll("g.fm-var").length === 2, "ASSIGNMENT is drawn inside the strip, with its chips");
  const ands = s.querySelectorAll("text.fm-and"), ors = s.querySelectorAll("text.fm-op");
  ok(ands.every(t => owner(t).getAttribute("data-piece") === "c1" || owner(t).getAttribute("data-piece") === "c2"), "each ∧ belongs to the clause it leads");
  ok(ors.every(t => /^c\d+$/.test(owner(t).getAttribute("data-piece"))), "each ∨ belongs to its clause");
  ok(s.querySelectorAll("g.fm-clause")[0].querySelector("text.cid").textContent === "C1", "the clause label stays with its clause");
}
{
  const s = svg(), v = FV.create({ svg: s });
  const r = v.load("(x1 & !x2) | (!x1 & x3 & x2) | (x2 & !x3)", "DNF", "brute");
  ok(s.querySelectorAll("text.fm-and").map(t => t.textContent).join() === "∨,∨" && s.querySelectorAll("text.fm-op").map(t => t.textContent).join() === "∧,∧,∧,∧", "DNF draws ∨ between terms and ∧ inside them");
  ok(s.querySelectorAll("g.fm-clause")[1].querySelector("text.cid").textContent === "D2" && /joined by ∨; the literals inside are joined by ∧ \(DNF\)/.test(r.legend), "DNF labels and legend");
  v.show(r.frames.length - 1);
  const C = s.querySelectorAll("g.fm-clause");
  ok(C.some(c => cls(c) === "fm-clause Covered") && C.some(c => cls(c) === "fm-clause False"), "the true term Covered, the others False: " + C.map(cls).join(" | "));
  ok(floating(s).length === 0, "DNF: no floating text");
}
{
  // an unknown operator, drawn from a payload alone
  const s = svg(), v = FV.create({ svg: s });
  const P = { kind: "formula", layout: "rows", outer: "and", noun: "constraint", groups: [{ id: "g0", label: "G1", expr: { id: "o", op: "xor", args: [{ id: "a1", var: "v:a" }, { id: "b1", var: "v:b" }] } }], variables: [{ id: "v:a", label: "a" }, { id: "v:b", label: "b" }] };
  v.loadFrames(P, [{ event: "Try", caption: "", focus: [], nodes: { a1: "ElementHighlight" }, groups: {}, assignment: { "v:a": true }, variables: { "v:a": "ElementHighlight" } }]);
  v.show(0);
  ok(s.querySelectorAll("g.fm-clause text.fm-op").map(t => t.textContent).join(" ") === "xor( , )" && s.querySelectorAll("g.fm-lit").length === 2, "xor(a, b) is drawn generically");
  ok(cls(s.querySelectorAll("g.fm-lit")[0]) === "fm-lit ElementHighlight", "its pieces take frame states by id");
  ok(floating(s).length === 0, "unknown operator: no floating text");
}
{
  // the equation layout with integer chips, and a frame that rebuilds the expression
  const s = svg(), v = FV.create({ svg: s });
  const r = v.load("60", "PRIMEFACTOR");
  ok(s.querySelectorAll("g.fm-lit").map(g => g.querySelector("text").textContent).join(" ") === "60 ?" && s.querySelectorAll("text.fm-op").map(t => t.textContent).join() === "=", "instance: 60 = ?");
  ok(s.querySelectorAll("g.fm-clause.fm-eq").length === 1 && s.querySelectorAll("g.fm-lit.fm-num").length === 2, "an unboxed equation of numbers");
  v.show(2);
  ok(s.querySelectorAll("g.fm-lit").map(g => g.querySelector("text").textContent).join(" ") === "60 2 2 15" && s.querySelectorAll("text.fm-op").map(t => t.textContent).join(" ") === "= × ×", "step 2: 60 = 2 × 2 × 15");
  const V = s.querySelectorAll("g.fm-var");
  ok(V.map(g => g.querySelector("text").textContent).join(" | ") === "i= 2 | rest= 15", "integer chips: " + V.map(g => g.querySelector("text").textContent).join(" | "));
  ok(cls(s.querySelectorAll("g.fm-lit")[2]) === "fm-lit fm-num ElementHighlight" && cls(s.querySelectorAll("g.fm-lit")[1]) === "fm-lit fm-num Committed", "the new factor highlighted, the kept one Committed");
  v.show(r.frames.length - 1);
  ok(s.querySelectorAll("g.fm-lit").map(g => g.querySelector("text").textContent).join(" ") === "60 2 2 3 5" && s.querySelectorAll("g.fm-clause")[0].querySelector("text.mark").textContent === "✓", "Done: 60 = 2 × 2 × 3 × 5 ✓");
  ok(floating(s).length === 0, "equation: no floating text");
  v.show(null);
  ok(s.querySelectorAll("g.fm-lit").length === 2, "back to the instance: the payload's own expression");
}
{
  // 0-1 ILP: stacked rows, a Rejected row with its note, 0/1 chips
  const s = svg(), v = FV.create({ svg: s });
  const r = v.load("(2 3 -1),(-1 -1 0),(1 0 4)<=(5 -1 4)", "INTPROGRAMMING01");
  v.show(1);
  const C = s.querySelectorAll("g.fm-clause");
  ok(cls(C[1]) === "fm-clause Rejected" && C[1].querySelector("text.mark").textContent === "✗" && C[1].querySelector("text.note").textContent === "total 0 > −1", "row 2 Rejected, with ✗ and its total");
  ok(cls(C[0]) === "fm-clause Covered" && C[0].querySelector("text.note").textContent === "total 0 ≤ 5", "row 1 holds");
  ok(s.querySelectorAll("g.fm-var").every(g => g.querySelector("tspan.val").textContent === "= 0") && s.querySelectorAll("g.fm-var").every(g => cls(g) === "fm-var False"), "every chip = 0, False");
  ok(C[0].querySelector("text.fm-and") === null && C[1].querySelector("text.fm-and").textContent === "∧", "∧ leads each later row, inside it");
  v.show(r.frames.length - 1);
  ok(s.querySelectorAll("g.fm-clause").every(c => cls(c) === "fm-clause Covered"), "the answer: every row Covered");
  ok(floating(s).length === 0, "ILP: no floating text");
}
{
  // Subset Sum: Σ(…) = T
  const s = svg(), v = FV.create({ svg: s });
  v.load("({1,7,12,15},28)", "SUBSETSUM");
  ok(s.querySelectorAll("text.fm-op").map(t => t.textContent).join(" ") === "Σ( , , , ) =", "Σ(1, 7, 12, 15) = 28");
  v.show(3);
  const L = s.querySelectorAll("g.fm-lit");
  ok(cls(L[2]) === "fm-lit fm-num False on-rej" && cls(L[0]) === "fm-lit fm-num ElementHighlight", "a number left out is False (struck; on-rej in a failed equation), a chosen one highlighted: " + cls(L[2]));
  ok(floating(s).length === 0, "Subset Sum: no floating text");
}
{
  // the SAT ↦ 3SAT pair: pane titles and the arrow belong to their pane / the separator
  const s = svg(), v = FV.createPair({ svg: s });
  v.load("(x1 | x2 | x3 | x4 | x5) & (x1)", "KARP_SAT3");
  ok(floating(s).length === 0, "the pair: no floating text: " + floating(s).map(t => t.textContent).join(" | "));
  ok(!s.querySelectorAll("text").some(t => /FORMULA/.test(t.textContent)), "the pair: no FORMULA · CNF headings");
  ok(s.querySelectorAll("text.fm-pane-title").every(t => /^pane:/.test(owner(t).getAttribute("data-piece"))), "pane titles inside their panes");
}
console.log(fails ? `${fails} FAILURES` : "ALL PASS");
process.exit(fails ? 1 : 0);
