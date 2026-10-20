// Drawing tests for nestedset.js: a region and a label per set, a dot per element, state classes from the frame (names or
// the backend's { state } refs), the ×N badge, hover tracing, and the set-list fallback (forced, and automatic).
const { load, svg } = require("./dom-shim.js");
const NV = load("nestedset.js", "NestedSetView");
let fails = 0;
const ok = (cond, msg) => { if (!cond) { fails++; console.log("  FAIL:", msg); } };
const cls = el => el.getAttribute("class") || "";

{
  const s = svg(), v = NV.create({ svg: s });
  const r = v.load("({1,2,3},{{1,2},{2,3},{3}})", "EXACTCOVER", "x");
  ok(r.layout === "regions", "a small instance draws as regions");
  ok(s.querySelectorAll("path.edge").length === 3 && s.querySelectorAll("path.fill").length === 3, "a fill and an edge per set");
  ok(s.querySelectorAll("g.ns-tag").length === 3, "a label per set");
  ok(s.querySelectorAll("g.ns-dot").length === 3, "a dot per element");
  // a backend frame: S1 and S2 picked by a brute force, element 2 covered twice
  v.loadFrames(r.payload, [{ event: "Reject", caption: "", focus: [], nodes: { S1: { state: "ElementHighlight" }, S2: { state: "ElementHighlight" }, "e:2": { state: "Rejected" }, "e:1": { state: "Covered" }, "e:3": { state: "Covered" } }, hits: { "e:1": 1, "e:2": 2, "e:3": 1 } }], "EXACTCOVER");
  v.show(0);
  const tags = s.querySelectorAll("g.ns-tag"), dots = s.querySelectorAll("g.ns-dot");
  const tag = id => tags.find(t => t.querySelector("text").textContent === id), dot = l => dots.find(d => d.querySelector("text").textContent === l);
  ok(cls(tag("S1")) === "ns-tag ElementHighlight" && cls(tag("S3")) === "ns-tag Background", "label states from the frame: " + tags.map(cls).join(" | "));
  ok(s.querySelectorAll("g.ns-region.ElementHighlight").length === 4, "both layers of S1 and S2 carry the state");
  ok(cls(dot("2")) === "ns-dot Rejected", "element 2 Rejected");
  const badge = dot("2").querySelector("g");
  ok(badge.getAttribute("visibility") === "visible" && badge.textContent === "×2", "×2 badge on the doubled element");
  ok(dot("1").querySelector("g").getAttribute("visibility") === "hidden", "no badge for a single hit");
  ok(v.chosenLabels(0).join() === "S1,S2", "picks read back from a backend frame");
  dot("1").dispatch("pointerenter");
  ok(cls(dot("1")).includes("trace") && cls(tag("S1")).includes(" ") && !cls(tag("S1")).includes("faint") && cls(tag("S2")).includes("faint"), "hover traces an element and its sets");
  dot("1").dispatch("pointerleave");
  ok(v.setLayout("list") === "list" && s.querySelectorAll("g.ns-row").length === 4, "the set list: U plus a row per set");
  ok(s.querySelectorAll("g.ns-dot").length === 3 + 2 + 2 + 1, "a chip per membership, plus U");
}
{
  const s = svg(), v = NV.create({ svg: s });
  const r = v.load("({1,2,3,4,5,6,7,8},{{1,2},{2,3},{3,4},{4,5},{5,6},{6,7},{7,8},{8,1},{1,5}})", "HITTINGSET", "brute");
  ok(r.layout === "list" && /more than 8 sets/.test(r.why), "nine sets fall back to the set list on their own: " + r.why);
  ok(s.querySelector("text.ns-head").textContent.includes("SET LIST"), "the header says so");
}
console.log(fails ? `${fails} FAILURES` : "ALL PASS");
process.exit(fails ? 1 : 0);
