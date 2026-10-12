// Drawing tests for bipartite.js's viewer hooks: Rejected edges, a clause that uses one variable twice, the hits badge,
// and long set member lists (wrapped instead of running out of the box) and long element names (pills).
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
// Set view with long member lists and long element names (Graph Coloring → Exact Cover): lists wrap inside a taller box,
// and long element names get a pill instead of overflowing the circle.
{
  const names = Array.from({ length: 12 }, (_, i) => `a_b_${i}`);
  const sets = [{ id: "S1", label: "S1", items: names }, { id: "S2", label: "S2", items: ["a_b_0"] }];
  const els = names.map(n => ({ id: "e:" + n, label: n }));
  const edges = sets.flatMap(st => st.items.map(n => ({ el: "e:" + n, set: st.id, id: st.id + n })));
  const hits = new Map(sets.map(st => [st.id, new Set(st.items.map(n => "e:" + n))]));
  const M = { sets, els, edges, cands: sets.map(x => x.id), cons: els.map(e => e.id), hits, pickSets: true, name: id => id.replace(/^e:/, ""), nice: id => id };
  const s = svg(), v = BV.create({ svg: s });
  v.loadModel(M, { kind: "EXACTCOVER", K: null, U: names, sets: sets.map(x => x.items) }, [{ chosen: [] }]);
  const setGs = s.querySelectorAll("g.bp-node.set");
  const g1 = setGs.find(g => g.querySelector("text.lbl").textContent === "S1"), g2 = setGs.find(g => g.querySelector("text.lbl").textContent === "S2");
  const subs = g1.querySelectorAll("text.sub");
  ok(subs.length > 1, "S1's long list wraps onto " + subs.length + " lines");
  ok(subs.map(t => t.textContent).join("") === "{" + names.join(",") + "}", "the wrapped lines hold the whole list");
  const box = g1.querySelector("rect.body"), room = +box.getAttribute("x") + +box.getAttribute("width") - +subs[0].getAttribute("x");
  ok(subs.every(t => t.textContent.length * 7.4 <= room), "every line fits the box width (by the 7.4 px estimate)");
  ok(+box.getAttribute("height") > 34, "the box grows to hold the lines");
  ok(g2.querySelectorAll("text.sub").length === 1 && +g2.querySelector("rect.body").getAttribute("height") === 34, "a short list keeps the one-line box");
  const spans = setGs.map(g => { const r = g.querySelector("rect.body"); return [+r.getAttribute("y"), +r.getAttribute("y") + +r.getAttribute("height")]; }).sort((a, b) => a[0] - b[0]);
  ok(spans.every((sp, k) => !k || sp[0] >= spans[k - 1][1]), "set boxes do not overlap: " + JSON.stringify(spans));
  const el0 = s.querySelectorAll("g.bp-node.el")[0];
  ok(el0.querySelector("rect.body") && !el0.querySelector("circle.body"), "a long element name gets a pill");
  ok(+el0.querySelector("rect.body").getAttribute("width") >= "a_b_0".length * 8.6, "the pill is as wide as its name");
}
console.log(fails ? `${fails} FAILURES` : "ALL PASS");
process.exit(fails ? 1 : 0);
