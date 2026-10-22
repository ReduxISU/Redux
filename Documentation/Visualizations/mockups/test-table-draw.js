// Drawing tests for table.js's viewer hooks: the 0-1 ILP table drawn from the backend's values (no "≤" column, undecided
// Total / Holds left blank / open), and the Edit Distance grid's axis names and backtrace steps.
const { load, div } = require("./dom-shim.js");
const TV = load("table.js", "TableView");
let fails = 0;
const ok = (cond, msg) => { if (!cond) { fails++; console.log("  FAIL:", msg); } };
const cls = el => el.getAttribute("class") || "";

// 0-1 ILP, 3 variables, 3 rows. x unset: the backend sends null Total / Holds.
{
  const h = div(), v = TV.create({ host: h });
  const inst = "(-1 1 -1),(0 0 -1),(1 -1 1)<=(0 0 1)";
  const unset = [["?", "?", "?", null, null, null], ["-1", "1", "-1", null, "0", null], ["0", "0", "-1", null, "0", null], ["1", "-1", "1", null, "1", null]];
  const set = [["0", "0", "0", null, null, null], ["-1", "1", "-1", "0", "0", "yes"], ["0", "0", "-1", "0", "0", "yes"], ["1", "-1", "1", "0", "1", "yes"]];
  v.loadFrames(inst, "INTPROGRAMMING01", [{ x: [null, null, null], values: unset }, { x: [0, 0, 0], values: set, done: true, ok: true }]);
  v.show(0);
  let html = h.innerHTML;
  ok(!html.includes("<th>≤</th>") && !html.includes('<td class="mut">≤</td>'), "no ≤ column");
  ok((html.match(/<th>/g) || []).length === 7, "columns: '', x1..x3, Total, Bound, Row holds? (" + (html.match(/<th>/g) || []).length + ")");
  ok(!/pill yes|pill no|can't/.test(html), "x unset: no derived verdict");
  ok((html.match(/tb-pill open">open</g) || []).length === 3, "x unset: every row's Holds is open");
  ok(!/\(\d+…\d+\)/.test(html), "x unset: no derived Total range");
  v.show(1);
  html = h.innerHTML;
  ok((html.match(/tb-pill yes">yes</g) || []).length === 3, "x set: the backend's yes in every row");
}
// Edit Distance: axis names from the backend, and a backtrace step.
{
  const h = div(), v = TV.create({ host: h });
  const vals = [["0", "1", "2", "3"], ["1", "1", "2", "3"], ["2", "2", "1", "2"], ["3", "2", "2", "2"], ["4", "3", "3", "2"], ["5", "4", "4", "3"]];
  v.loadFrames("(horse,ros)", "EDITDISTANCE", [
    { values: vals, filled: 15, back: { cur: [3, 2], path: [[4, 3], [5, 3]] } },
    { values: vals, filled: 15, done: true, ok: true, path: [[0, 0], [1, 1], [2, 2], [3, 2], [4, 3], [5, 3]], ops: [] },
  ], { axes: { row: "source x", col: "target y" } });
  v.show(0);
  const svgEl = h.querySelector("svg");
  const names = svgEl.querySelectorAll("text.tb-axis-name").map(t => t.textContent);
  ok(names.includes("TARGET  y") && names.includes("SOURCE  x"), "axis names from the frames: " + names.join(", "));
  const cells = svgEl.querySelectorAll("g.tb-cell"), at = (i, j) => cells[i * 4 + j];
  ok(cls(at(3, 2)).includes("Active"), "backtrace: the traced cell is Active");
  ok(cls(at(4, 3)).includes("Solution") && cls(at(5, 3)).includes("Solution"), "backtrace: the path so far is Solution");
  ok(!cls(at(1, 1)).includes("Solution") && !cls(at(0, 0)).includes("Solution"), "backtrace: the rest of the path is not drawn yet");
  const arrows = svgEl.querySelectorAll("line.tb-arrow");
  ok(arrows.filter(a => cls(a).includes("path")).length === 1 && arrows.length === 2, "backtrace: one path arrow and one step arrow (" + arrows.map(cls).join(" | ") + ")");
  ok(!h.querySelector("div.tb-align"), "backtrace: no alignment until the end");
  v.loadFrames("(horse,ros)", "EDITDISTANCE", [{ values: vals, filled: 15 }], { axes: { row: "word one", col: "word two" } });
  v.show(0);
  ok(h.querySelector("svg").querySelectorAll("text.tb-axis-name").map(t => t.textContent).includes("WORD TWO"), "other axis names pass through");
}
console.log(fails ? `${fails} FAILURES` : "ALL PASS");
process.exit(fails ? 1 : 0);
