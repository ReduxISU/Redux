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
// Every example of both mockups (the design tab's and the GUI's), every solver and every frame: no two set labels
// intersect, no label covers an element dot, and every outside dot is at least 4 px clear of each region.
{
  const EX = {
    SETCOVER: ["({1,2,3,4,5},{{1,2,3},{2,4},{3,4},{4,5}},3)", "({1,2,3,4,5,6},{{1,2,3},{4,5,6},{1,2,4,5}},2)", "({1,2,3,4,7},{{1,2},{3,4},{2,3}},3)", "({1,2,3,4,5,6,7,8},{{1,2},{2,3},{3,4},{4,5},{5,6},{6,7},{7,8},{8,1},{1,5},{3,7}},4)"],
    EXACTCOVER: ["({1,2,3,4},{{1,2,3},{2,3},{4,1}})", "({1,2,3,4,5,6},{{1,2},{1,3},{2,4},{3,5,6},{4,5,6},{4,6},{5,6}})", "({1,2,3},{{1,2},{2,3},{1,3}})", "({1,2,3,4,5,6,7,8},{{1,2},{3,4},{5,6},{7,8},{2,3},{4,5},{6,7},{8,1},{1,5}})"],
    HITTINGSET: ["({1,2,3,4},{{1,3},{2,3,4},{1,4}})", "({1,2,3,4,5},{{1,2},{2,3},{3,4},{4,5}})", "({1,2,3},{{1,2},{2,3},{1,3}})", "({1,2,3,4,5,6,7,8},{{1,2},{2,3},{3,4},{4,5},{5,6},{6,7},{7,8},{8,1},{1,5}})"],
  };
  const SOLV = { SETCOVER: ["greedy", "brute"], EXACTCOVER: ["x", "brute"], HITTINGSET: ["brute", "x"] };
  const inter = (a, b) => a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h;
  const rectOf = r => ({ x: +r.getAttribute("x"), y: +r.getAttribute("y"), w: +r.getAttribute("width"), h: +r.getAttribute("height") });
  let frames = 0, regionsDrawn = 0;
  for (const [kind, list] of Object.entries(EX)) for (const str of list) for (const solver of SOLV[kind]) {
    const s = svg(), v = NV.create({ svg: s }), r = v.load(str, kind, solver);
    if (r.layout === "regions") {
      regionsDrawn++;
      const R = NV.regions(r.payload);
      r.payload.sets.forEach(set => {
        const reg = R.regs.find(x => x.id === set.id), pts = set.members.map(m => R.pos.get(m));
        r.payload.elements.forEach(e => { if (!set.members.includes(e.id)) ok(NV._distToHull(R.pos.get(e.id), pts) - reg.pad - 16 >= 4, `${e.id} is 4 px clear of ${set.id}'s region in ${str}`); });
      });
    }
    for (let i = 0; i < r.frames.length; i++) {
      v.show(i); frames++;
      const tags = s.querySelectorAll("g.ns-tag rect").map(rectOf);
      const dots = s.querySelectorAll("g.ns-dot circle.body").map(c => ({ x: +c.getAttribute("cx") - 16, y: +c.getAttribute("cy") - 16, w: 32, h: 32, cx: +c.getAttribute("cx"), cy: +c.getAttribute("cy") }));
      tags.forEach((a, p) => tags.forEach((b, q) => { if (p < q) ok(!inter(a, b), `labels ${p} and ${q} intersect in ${kind} ${str} frame ${i}`); }));
      tags.forEach(a => dots.forEach(d => ok(Math.hypot(Math.max(a.x - d.cx, 0, d.cx - a.x - a.w), Math.max(a.y - d.cy, 0, d.cy - a.y - a.h)) >= 16, `a label covers a dot in ${kind} ${str} frame ${i}`)));
    }
  }
  ok(regionsDrawn >= 14, "most examples draw as regions: " + regionsDrawn);
  console.log(`  checked ${frames} frames, ${regionsDrawn} example runs drawn as regions`);
}
console.log(fails ? `${fails} FAILURES` : "ALL PASS");
process.exit(fails ? 1 : 0);
