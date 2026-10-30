// Drawing tests for quantum.js with hand-made frames (the shape the viewer's adapter passes): qubit states on the wires,
// the measured outcome (bits on the wires, the outcome bar), shot counts, and the "simulated in this browser" label.
const { load, svg } = require("./dom-shim.js");
const QV = load("quantum.js", "QuantumView");
let fails = 0;
const ok = (cond, msg) => { if (!cond) { fails++; console.log("  FAIL:", msg); } };
const cls = el => el.getAttribute("class") || "";

function view(key, inst) {
  const s = svg(), v = QV.create({ svg: s });
  const res = v.load(inst, key);
  return { s, v, res };
}
const last = (key, inst) => { const { res } = view(key, inst); return res.frames[res.frames.length - 1]; };

// Deutsch (0,1): 2 wires (x0 measured, y helper), 5 columns.
{
  const { s, v, res } = view("DEUTSCH", "(0,1)");
  const sim = res.frames[res.frames.length - 1].dist;
  // An ok Done: the measured qubit is Solution, outcome 1.
  v.useFrames([{ col: 5, dist: sim, done: true, ok: true, outcome: 1, qubitStates: ["Solution", ""], measured: "Solution" }]);
  v.show(0);
  const wires = s.querySelectorAll("g.qc-wire-g"), outs = s.querySelectorAll("text.qc-out");
  ok(cls(wires[0]).includes("Solution") && !cls(wires[1]).includes("Solution"), "Deutsch ok: x0's wire is Solution, y's is not");
  ok(outs[0].textContent === "1" && cls(outs[0]).includes("Solution"), "Deutsch ok: bit 1 written on x0 in Solution");
  ok(s.querySelectorAll("rect.qc-bar.win").length === 1 && s.querySelectorAll("rect.qc-bar.miss").length === 0, "Deutsch ok: one green outcome bar");

  // An ok:false Done: the measured qubit is Rejected; the outcome bar turns red, not green.
  v.useFrames([{ col: 5, dist: sim, done: true, ok: false, outcome: 1, qubitStates: ["Rejected", ""], measured: "Rejected" }]);
  v.show(0);
  ok(cls(s.querySelectorAll("g.qc-wire-g")[0]).includes("Rejected"), "Deutsch not ok: x0's wire is Rejected");
  ok(cls(s.querySelectorAll("text.qc-out")[0]).includes("Rejected"), "Deutsch not ok: the bit is drawn Rejected");
  ok(s.querySelectorAll("rect.qc-bar.miss").length === 1 && s.querySelectorAll("rect.qc-bar.win").length === 0, "Deutsch not ok: the outcome bar is red");

  // Measured but not yet confirmed (measured ""): the bit is written in plain ink and no bar is coloured.
  v.useFrames([{ col: 5, dist: sim, outcome: 1, qubitStates: ["", ""], measured: "" }]);
  v.show(0);
  ok(s.querySelectorAll("text.qc-out")[0].textContent === "1" && cls(s.querySelectorAll("text.qc-out")[0]) === "qc-out", "Deutsch unconfirmed: the bit is plain");
  ok(!s.querySelectorAll("rect.qc-bar.win").length && !s.querySelectorAll("rect.qc-bar.miss").length, "Deutsch unconfirmed: no green or red bar");
}

// Shot counts: drawn as ×n under each outcome, and as the bars when no probabilities came with them.
{
  const { s, v } = view("BERNSTEINVAZIRANI", "(0,1,0,1,1,0,1,0)");
  v.useFrames([{ col: 5, dist: null, done: true, ok: true, outcome: 5, counts: { "101": 900, "001": 100 }, qubitStates: ["Solution", "Solution", "Solution", ""], measured: "Solution" }]);
  v.show(0);
  const counts = s.querySelectorAll("text.qc-count").map(t => t.textContent);
  ok(counts.length === 8 && counts.includes("×900") && counts.includes("×100"), "BV counts: a ×n label per outcome (" + counts.join(" ") + ")");
  const bars = s.querySelectorAll("rect.qc-bar");
  ok(bars.length === 8, "BV counts: bars synthesized from the counts");
  ok(/1000 shots/.test(s.querySelector("text.qc-note").textContent), "BV counts: the note gives the shot total");
  // Counts as a list of {outcome, count} with numeric outcomes.
  v.useFrames([{ col: 5, dist: null, outcome: 5, counts: [{ outcome: 5, count: 7 }] }]);
  v.show(0);
  ok(s.querySelectorAll("text.qc-count").some(t => t.textContent === "×7"), "BV counts as a list");
}

// Where the bars come from: a dist marked browser:true gets the label, a backend one does not.
{
  const { s, v, res } = view("PRIMEFACTOR", "15");
  const sim = res.frames.find(f => f.dist).dist;
  v.useFrames([{ col: 0, dist: { ...sim, browser: true }, classical: true }, { col: 0, dist: sim, classical: true }]);
  v.show(0);
  ok(/Simulated in this browser/.test(s.querySelector("text.qc-src").textContent), "Shor: browser bars are labelled");
  v.show(1);
  ok(s.querySelector("text.qc-src").textContent === "", "Shor: no label on the backend's own bars");
}

// The mockup's own frames still colour the outcome only at the end.
{
  const f = last("DEUTSCHJOZSA", "(1,0,0,0)"); // promise broken: ok false
  ok(f.done && f.ok === false, "DJ neither: the mockup's last frame is not ok");
}

console.log(fails ? `${fails} FAILURES` : "ALL PASS");
process.exit(fails ? 1 : 0);
