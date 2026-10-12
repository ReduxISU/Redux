// Drawing tests for misc.js's viewer hooks: the Convex Hull chain-walk merge (walk, popped).
const { load, svg } = require("./dom-shim.js");
const MV = load("misc.js", "MiscView");
let fails = 0;
const ok = (cond, msg) => { if (!cond) { fails++; console.log("  FAIL:", msg); } };
const cls = el => el.getAttribute("class") || "";

// Convex Hull: left hull {0,1}, right hull {2,3,4}; the walk is at [0,2] and point 3 was just popped.
{
  const s = svg(), v = MV.create({ svg: s });
  const pts = [{ x: 0, y: 0 }, { x: 1, y: 2 }, { x: 3, y: -1 }, { x: 4, y: 1 }, { x: 5, y: 0 }];
  v.loadModel("CONVEXHULL", { pts }, [
    { hulls: [[0, 1], [2, 3, 4]], focus: [[0, 1], [2, 3, 4]], walk: [0, 2], popped: 3 },
    { hulls: [[0, 1], [2, 3, 4]], focus: [[0, 1], [2, 3, 4]], walk: [0, 2, 4] },
  ]);
  v.show(0);
  ok(s.querySelectorAll("polyline.ms-chain").length === 1, "the walk is drawn as a chain");
  ok(s.querySelectorAll("line.ms-pop").length === 1, "the popped turn is drawn as a dashed red edge");
  const ptCls = () => s.querySelectorAll("g.ms-pt").map(cls);
  ok(ptCls()[3].includes("Rejected"), "the popped point is Rejected (" + ptCls()[3] + ")");
  v.show(1);
  ok(!s.querySelectorAll("line.ms-pop").length, "no popped edge on a plain step");
  ok(ptCls()[4].includes("ring"), "the newest chain point is ringed");
  ok(ptCls()[0].includes("c0") && ptCls()[2].includes("c1"), "left and right hulls keep their group colours");
}
console.log(fails ? `${fails} FAILURES` : "ALL PASS");
process.exit(fails ? 1 : 0);
