// Drawing tests for formula.js: one pill per literal occurrence, ∧ between clauses and ∨ inside them (not the other way
// round), state classes read from the frame, the ✓/✗ clause marks, the strip, backend frames through loadFrames, and the
// SAT ↦ 3SAT pair (hidden clauses during the build, gadget colors).
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
console.log(fails ? `${fails} FAILURES` : "ALL PASS");
process.exit(fails ? 1 : 0);
