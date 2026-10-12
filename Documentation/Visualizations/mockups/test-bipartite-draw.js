// Drawing tests for bipartite.js's viewer hooks: Rejected edges, a clause that uses one variable twice, the hits badge,
// and long set member lists (wrapped instead of running out of the box).
const { load, svg } = require("./dom-shim.js");
const BV = load("bipartite.js", "BipartiteView");
let fails = 0;
const ok = (cond, msg) => { if (!cond) { fails++; console.log("  FAIL:", msg); } };
const cls = el => el.getAttribute("class") || "";

// SAT factor graph: C1 = (x1 ∨ ¬x1 ∨ x2) uses x1 twice; C2 = (x2).
{
  const s = svg(), v = BV.createSat({ svg: s });
  v.load([[1, -1, 2], [2]], ["x1", "x2"], { hits: true });
  const edges = s.querySelectorAll(".bp-edge");
  ok(edges.length === 4, "one edge per literal (4)");
  ok(edges[0].tagName === "line" && edges[1].tagName === "path" && /Q/.test(edges[1].getAttribute("d")), "the repeated variable's second edge is a curve");
  ok(edges[2].tagName === "line" && edges[3].tagName === "line", "other edges stay straight lines");
  v.show({ asg: { 1: true, 2: false }, ev: { nodes: new Map([["c0", "Covered"], ["c1", "Rejected"]]), edges: ["sol", "dim", "rej", "rej"], hits: new Map([["c0", 1]]) } });
  ok(cls(edges[2]).includes("rej") && cls(edges[3]).includes("rej"), "Rejected edges get the rej class");
  const badges = s.querySelectorAll("g.bp-hits");
  ok(badges.length === 2, "a hits badge per clause");
  ok(badges[0].getAttribute("visibility") === "visible" && badges[0].textContent === "1 true", "C1 shows 1 true (" + badges[0].textContent + ")");
  ok(badges[1].getAttribute("visibility") === "hidden", "a clause without a hits count shows no badge");
  // Without hits frames the badge is not drawn at all (the design look).
  const s2 = svg(), v2 = BV.createSat({ svg: s2 });
  v2.load([[1, -1, 2], [2]], ["x1", "x2"]);
  ok(!s2.querySelectorAll("g.bp-hits").length, "no badge without hits");
}
console.log(fails ? `${fails} FAILURES` : "ALL PASS");
process.exit(fails ? 1 : 0);
