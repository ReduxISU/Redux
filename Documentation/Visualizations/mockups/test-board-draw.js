// Drawing test for board.js's Sudoku digits: clues bold, solver-placed digits plain (not green) mid-run, green only on a
// solved board.
const { load, svg } = require("./dom-shim.js");
const BV = load("board.js", "BoardView");
let fails = 0;
const ok = (cond, msg) => { if (!cond) { fails++; console.log("  FAIL:", msg); } };
const cls = el => el.getAttribute("class") || "";

const clues = [[1, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0]];
const solved = [[1, 2, 3, 4], [3, 4, 1, 2], [2, 1, 4, 3], [4, 3, 2, 1]];
const G = { kind: "sudoku", n: 4, b: 2, grid: clues, clashes: [] };
const s = svg(), v = BV.create({ svg: s });
v.loadModel(G, { kind: "sudoku" }, [
  { kind: "show", grid: [[1, 2, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0]] },
  { kind: "done", grid: solved, ok: true },
]);
const digs = () => s.querySelectorAll("text.bd-dig");
v.show(0);
ok(cls(digs()[0]).split(" ").includes("given"), "the clue is drawn as given");
ok(cls(digs()[1]) === "bd-dig placed", "a solver digit mid-run is plain placed, not green (" + cls(digs()[1]) + ")");
v.show(1);
ok(cls(digs()[1]) === "bd-dig placed sol", "on the solved board placed digits are green");
ok(cls(digs()[0]).split(" ").includes("given") && !cls(digs()[0]).includes("sol"), "clues stay clues on the solved board");
console.log(fails ? `${fails} FAILURES` : "ALL PASS");
process.exit(fails ? 1 : 0);
