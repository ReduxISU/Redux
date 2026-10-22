// Drawing test for automaton.js's loadModel path runs (the NFA one-path picker in the viewer): usePath(i) steps that
// run's frames, usePath(null) goes back to all branches.
const { load, svg, div } = require("./dom-shim.js");
const AV = load("automaton.js", "AutomatonView");
let fails = 0;
const ok = (cond, msg) => { if (!cond) { fails++; console.log("  FAIL:", msg); } };
const cls = el => el.getAttribute("class") || "";

const A = { states: ["1", "2"], alphabet: ["a"], trans: [{ from: "1", sym: "a", to: "2" }], start: "1", accept: new Set(["2"]), input: ["a"], kind: "NFA", garbage: null };
const fr = (nodes, done) => ({ active: [], taken: [], visited: new Set(), pos: 0, done: !!done, accepted: !!done, caption: "", ev: { nodes: new Map(nodes), edges: new Map() } });
const all = [fr([["1", "Active"]]), fr([["2", "Active"]]), fr([["2", "Solution"]], true)];
const path = [fr([["1", "Active"]]), fr([["2", "Rejected"]], true)];
const s = svg(), v = AV.create({ svg: s, tape: div() });
const r = v.loadModel(A, all, [{ label: "Path 1: 1 → 2, rejects", accepted: false, frames: path }]);
ok(r.paths.length === 1 && r.paths[0].label.startsWith("Path 1"), "loadModel reports the path runs");
const nodeCls = () => s.querySelectorAll("g.au-node").map(cls);
v.show(2);
ok(nodeCls()[1].includes("Solution"), "all branches: frame 3 has state 2 Solution");
const u = v.usePath(0);
ok(u.frames === path, "usePath(0) switches to the path's frames");
v.show(1);
ok(nodeCls()[1].includes("Rejected"), "path 1: frame 2 has state 2 Rejected (" + nodeCls().join(" | ") + ")");
v.usePath(null); v.show(2);
ok(nodeCls()[1].includes("Solution"), "back on all branches");
console.log(fails ? `${fails} FAILURES` : "ALL PASS");
process.exit(fails ? 1 : 0);
