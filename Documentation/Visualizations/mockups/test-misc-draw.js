// Drawing tests for misc.js's viewer hooks: the Convex Hull chain-walk merge (walk, popped) and the 3-Dimensional
// Matching layout for long triple lists (the list wraps into columns under the picture instead of running off).
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
// 3DM with many triples: the list wraps into columns below the elements, wide enough for each label.
{
  const s = svg(), v = MV.create({ svg: s });
  const names = k => Array.from({ length: 18 }, (_, i) => `${k}_garb_${i}`);
  const X = names("x"), Y = names("y"), Z = names("z");
  const M = Array.from({ length: 135 }, (_, i) => ({ id: "T" + (i + 1), x: X[i % 18], y: Y[(i * 7) % 18], z: Z[(i * 5) % 18] }));
  v.loadModel("DM3", { X, Y, Z, M }, [{ chosen: [], src: {} }]);
  v.show(0);
  const rows = s.querySelectorAll("g.ms-trow"), rects = rows.map(r => r.querySelector("rect"));
  const longest = Math.max(...M.map(t => `${t.x} · ${t.y} · ${t.z}`.length));
  ok(rects.every(r => +r.getAttribute("width") >= longest * 7 + 30), "triple rows are wide enough for their text");
  const xs = new Set(rects.map(r => r.getAttribute("x")));
  ok(xs.size > 1, "the long list wraps into several columns (" + xs.size + ")");
  const vb = s.getAttribute("viewBox").split(" ").map(Number);
  ok(rects.every(r => +r.getAttribute("x") + +r.getAttribute("width") <= vb[2] + 0.5), "every row fits inside the picture");
  const lastY = Math.max(...rects.map(r => +r.getAttribute("y")));
  ok(lastY < 135 * 22, "the picture is shorter than one 135-row column");
  // A short list keeps the design layout: one column beside the elements.
  const s2 = svg(), v2 = MV.create({ svg: s2 });
  v2.loadModel("DM3", { X: ["a", "b"], Y: ["c", "d"], Z: ["e", "f"], M: [{ id: "T1", x: "a", y: "c", z: "e" }, { id: "T2", x: "b", y: "d", z: "f" }] }, [{ chosen: [], src: {} }]);
  ok(new Set(s2.querySelectorAll("g.ms-trow").map(r => r.querySelector("rect").getAttribute("x"))).size === 1, "a short list stays in one column");
}
console.log(fails ? `${fails} FAILURES` : "ALL PASS");
process.exit(fails ? 1 : 0);
