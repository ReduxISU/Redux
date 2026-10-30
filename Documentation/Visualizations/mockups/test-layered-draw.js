// Drawing test for layered.js's Untraveled edge style (an edge removed with its node), through the loadModel hook.
const { load, svg } = require("./dom-shim.js");
const LV = load("layered.js", "LayeredView");
let fails = 0;
const ok = (cond, msg) => { if (!cond) { fails++; console.log("  FAIL:", msg); } };
const cls = el => el.getAttribute("class") || "";
const k = (u, v) => u + "\u0000" + v;

const G = LV.parse("({1,2,3},{(1,2),(2,3),(1,3)})", LV.PROBLEMS.TOPOSORT);
const s = svg(), v = LV.create({ svg: s });
v.loadModel(G, { rule: "topo" }, [
  { ev: { nodes: new Map([["2", { st: "Untraveled" }]]), edges: new Map([[k("1", "2"), "untr"], [k("2", "3"), "untr"], [k("1", "3"), "sol"]]), phantom: [], checks: [] } },
]);
v.show(0);
const edges = s.querySelectorAll("path.ly-edge");
ok(edges.length === 3, "three edges drawn");
const untr = edges.filter(e => cls(e).split(" ").includes("untr"));
ok(untr.length === 2, "two edges drawn Untraveled (" + edges.map(cls).join(" | ") + ")");
ok(untr.every(e => e.getAttribute("marker-end") === "url(#ly-arrow-untr)"), "Untraveled edges use the dim arrowhead");
ok(s.querySelectorAll("marker").some(m => m.getAttribute("id") === "ly-arrow-untr"), "the dim arrowhead marker exists");
console.log(fails ? `${fails} FAILURES` : "ALL PASS");
process.exit(fails ? 1 : 0);
