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
console.log(fails ? `${fails} FAILURES` : "ALL PASS");
process.exit(fails ? 1 : 0);
