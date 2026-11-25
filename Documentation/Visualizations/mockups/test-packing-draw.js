// Drawing tests for packing.js's viewer hooks: container (row) states on the track and the row label.
const { load, svg } = require("./dom-shim.js");
const PV = load("packing.js", "PackingView");
let fails = 0;
const ok = (cond, msg) => { if (!cond) { fails++; console.log("  FAIL:", msg); } };
const cls = el => el.getAttribute("class") || "";

// Bin Packing, 3 bins: bin 1 Covered, bin 2 being filled, bin 3 overfull.
{
  const s = svg(), v = PV.create({ svg: s });
  const I = PV.parse("((4,7,3,6,2,8),10,3)", PV.PROBLEMS.BINPACKING);
  v.loadModel(I, { rule: "binpacking" }, [
    { assign: [0, 1, 0, 1, 2, 2], rowStates: ["Covered", "Active", "Rejected"] },
    { assign: [0, 1, 0, 1, 2, 2] },
  ]);
  v.show(0);
  const tracks = s.querySelectorAll("rect.pk-track"), labels = s.querySelectorAll("text.pk-row-label");
  ok(tracks.length === 3 && labels.length === 3, "one track and label per bin");
  ok(cls(tracks[0]) === "pk-track Covered" && cls(tracks[1]) === "pk-track Active" && cls(tracks[2]) === "pk-track Rejected", "track classes follow rowStates: " + tracks.map(cls).join(" | "));
  ok(cls(labels[0]).includes("Covered") && cls(labels[2]).includes("Rejected"), "row labels carry the state too");
  v.show(1);
  ok(s.querySelectorAll("rect.pk-track").every(t => cls(t) === "pk-track"), "a frame without rowStates draws plain tracks");
}
// Knapsack: one container, two rows; the adapter passes its state for both.
{
  const s = svg(), v = PV.create({ svg: s });
  const I = PV.parse("({(10,60),(20,100),(30,120)},50,220)", PV.PROBLEMS.KNAPSACK);
  v.loadModel(I, { rule: "knapsack" }, [{ assign: [-1, 0, 0], rowStates: ["Covered", "Covered"] }]);
  v.show(0);
  ok(s.querySelectorAll("rect.pk-track.Covered").length === 2, "knapsack: both rows Covered");
}
// Maximum-Value Knapsack: a weight limit line, no value line; the best so far is a Committed bar behind the value row.
{
  const s = svg(), v = PV.create({ svg: s }), P = PV.PROBLEMS.MAXIMUMVALUEKNAPSACK;
  const r = v.load("(((10,60),(20,100),(30,120)),50)", P, "brute");
  const lines = s.querySelectorAll("line.pk-line"), labels = s.querySelectorAll("text.pk-line-label");
  ok(lines.length === 1 && labels.length === 1 && labels[0].textContent === "W = 50", "one line, the weight limit W = 50; no value target");
  ok(s.querySelectorAll("text.sub").map(t => t.textContent).join() === "#1,#2,#3", "each item tile shows its 1-based index");
  v.show(null);
  ok(s.querySelectorAll("rect.pk-best").length === 0 && s.querySelectorAll("text.pk-best-label").length === 0, "instance: no best-so-far bar");
  // frame 6 = try {1,3} (value 180, a new best) ; frame 5 = try {3} while the best is {1,2}
  v.show(5);
  const tiles = s.querySelectorAll("g.pk-tile");
  ok(cls(tiles[0]) === "pk-tile Committed" && cls(tiles[1]) === "pk-tile Committed" && cls(tiles[2]) === "pk-tile Active", "try {3} while the best is {1,2}: 1, 2 Committed, 3 ElementHighlight: " + tiles.map(cls).join(" | "));
  ok(s.querySelectorAll("rect.pk-best").length === 1 && s.querySelector("text.pk-best-label").textContent === "best so far 160", "the best-so-far bar reads 160");
  v.show(7);
  ok(cls(s.querySelectorAll("g.pk-tile")[1]) === "pk-tile Committed", "try {2,3} beats the best: accepted at once, so its items turn Committed");
  v.show(8);
  ok(s.querySelectorAll("g.pk-seg.over").length > 0, "try {1,2,3}: the weight row runs past W in red");
  ok(s.querySelectorAll("g.pk-tile")[1].getAttribute("class").includes("Active kept"), "an item in both the try and the best keeps the ring on the kept fill");
  v.show(r.frames.length - 1);
  ok(s.querySelectorAll("g.pk-tile").map(cls).join(" | ") === "pk-tile Unused | pk-tile Solution | pk-tile Solution", "solved: items 2 and 3 green, item 1 left out");
  ok(s.querySelectorAll("rect.pk-best").length === 0, "solved: the best-so-far bar is gone, the answer is the green bar");
  const reads = s.querySelectorAll("text.pk-read").map(t => t.textContent);
  ok(reads[0] === "50 ✓" && reads[1] === "220", "solved: weight 50 within W, value 220 with no target mark: " + reads.join(" / "));
  ok(v.checks(r.frames.length - 1).length === 3 && v.checks(r.frames.length - 1)[2].label.startsWith("Optimal"), "solved: optimality is its own check");
  ok(v.chosen(r.frames.length - 1)[0].text.startsWith("({2,3},220)"), "certificate in Redux's format");
  // Branch and Bound draws the bound marker
  v.load("(((10,60),(20,100),(30,120)),50)", P, "bnb");
  v.show(4);
  ok(s.querySelectorAll("line.pk-bound").length === 1 && s.querySelector("text.pk-bound-label").textContent === "at most 240", "branch and bound: the bound marker reads 'at most 240'");
  // Greedy: the comparison frame shows the single item highlighted against the kept greedy set
  const g = v.load("(((10,60),(20,100),(30,120)),50)", P, "greedy");
  v.show(g.frames.length - 2);
  ok(s.querySelectorAll("g.pk-tile").map(cls).join(" | ") === "pk-tile Committed | pk-tile Committed | pk-tile Active", "greedy: single item 3 against the kept greedy set {1,2}");
  v.show(g.frames.length - 1);
  ok(v.checks(g.frames.length - 1)[2].ok === false, "greedy: the optimality check fails on the default (160 < 220)");
}
// Change-Making: one tile per coin with a count badge, two rows (amount, coins), and the DP table strip.
{
  const s = svg(), v = PV.create({ svg: s }), P = PV.PROBLEMS.CHANGEMAKING;
  const r = v.load("({1,7,10,25},39,3)", P, "dp"), last = r.frames.length - 1;
  ok(s.querySelectorAll("g.pk-tile").length === 4, "one tile per coin value");
  ok(s.querySelectorAll("g.pk-cell").length === 40, "one table cell per amount 0..39");
  ok(s.querySelectorAll("text.pk-line-label").map(t => t.textContent).join() === "X = 39,K = 3", "lines: exactly X, at most K");
  v.show(null);
  ok(s.querySelectorAll("g.pk-badge rect").length === 0 && s.querySelectorAll("text.val").every(t => t.textContent === ""), "instance: no badges, an empty table");
  v.show(15); // amount 14
  const cells = s.querySelectorAll("g.pk-cell");
  ok(cls(cells[14]) === "pk-cell Active" && cls(cells[7]) === "pk-cell pick" && cls(cells[13]) === "pk-cell read" && cls(cells[4]) === "pk-cell read", "amount 14: the cell is highlighted, the amounts it reads are ringed, 7 (the best) solidly: " + [14, 7, 13, 4].map(k => cls(cells[k])).join(" | "));
  ok(cells[13].querySelector("text.val").textContent === "4" && cells[15].querySelector("text.val").textContent === "", "cells fill left to right");
  ok(cls(s.querySelectorAll("g.pk-tile")[1]) === "pk-tile Active", "the coin the step settles on (7) is highlighted");
  v.show(last - 1); // the last trace step: at 25, take a 25
  ok(s.querySelectorAll("g.pk-badge text").map(t => t.textContent).join() === "×2,×1", "trace back: badges ×2 on 7 and ×1 on 25");
  ok(cls(s.querySelectorAll("g.pk-cell")[39]) === "pk-cell Committed" && cls(s.querySelectorAll("g.pk-cell")[25]) === "pk-cell Active", "trace back: visited cells kept, the current one highlighted");
  ok(s.querySelectorAll("g.pk-seg.Active").length === 2, "trace back: the coin just added is highlighted in both rows");
  v.show(last);
  ok([0, 25, 32, 39].every(k => cls(s.querySelectorAll("g.pk-cell")[k]) === "pk-cell Solution"), "solved: the path 39, 32, 25, 0 is green");
  ok(s.querySelectorAll("g.pk-seg.Solution").length === 6, "solved: three coins in each row");
  ok(s.querySelectorAll("text.pk-read").map(t => t.textContent).join() === "39 ✓,3 ✓", "solved: 39 exactly, 3 coins within K");
  ok(cls(s.querySelectorAll("g.pk-tile")[0]) === "pk-tile Unused" && cls(s.querySelectorAll("g.pk-tile")[3]) === "pk-tile Solution", "solved: unused coins left out, used ones green");
  ok(v.chosen(last)[0].text.startsWith("{(7,2),(25,1)}"), "certificate in Redux's format and order");
  // K too small: the X cell fails, nothing green
  const n = v.load("({1,7,10,25},39,2)", P, "dp");
  v.show(n.frames.length - 1);
  ok(cls(s.querySelectorAll("g.pk-cell")[39]) === "pk-cell Rejected" && s.querySelectorAll(".Solution").length === 0, "K too small: the X cell is Rejected and nothing is green");
  // Memoization leaves amounts it never needed blank
  const m = v.load("({10,25},65,5)", P, "memo");
  v.show(m.frames.length - 1);
  ok(s.querySelectorAll("g.pk-cell")[1].querySelector("text.val").textContent === "" && s.querySelectorAll("g.pk-cell")[40].querySelector("text.val").textContent === "4", "memo: unneeded amounts stay blank");
}
console.log(fails ? `${fails} FAILURES` : "ALL PASS");
process.exit(fails ? 1 : 0);
