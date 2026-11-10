const fs = require("fs");
const src = fs.readFileSync("board.js", "utf8").replace("return { create, parse, PROBLEMS };", "return { create, parse, PROBLEMS, SOLVE, findClashes };");
const B = eval(src + ";BoardView");
let fail = 0;
const ok = (cond, msg) => { if (!cond) { fail++; console.log("FAIL:", msg); } };
// port of SudokuVerifier.VerifyHelper
function verifySudoku(inst, cert) {
  const n = inst.length, b = Math.sqrt(n);
  for (let i = 0; i < n; i++) for (let j = 0; j < n; j++) {
    const v = cert[i][j];
    if (v < 1 || v > n) return false;
    if (inst[i][j] && v !== inst[i][j]) return false;
  }
  return B.findClashes(cert, b).length === 0;
}
const verifyQueens = (n, qs) => qs.length === n && qs.every((c, i) => qs.every((d, j) => i === j || (c !== d && Math.abs(c - d) !== Math.abs(i - j))));
for (const [key, P] of Object.entries(B.PROBLEMS)) for (const [name, inst] of P.examples) for (const [sk, sname] of P.solvers) {
  const G = B.parse(inst, P), t0 = Date.now(), frames = B.SOLVE[G.kind][sk](G), last = frames.at(-1), ms = Date.now() - t0;
  let extra = "";
  if (G.kind === "sudoku") {
    if (last.ok) ok(verifySudoku(G.grid, last.grid), `${key}/${name}/${sk} answer fails the verifier`);
    if (/no solution|clashing/.test(name)) ok(!last.ok, `${key}/${name}/${sk} should have no solution`);
    const kinds = {}; frames.forEach(f => kinds[f.kind] = (kinds[f.kind] || 0) + 1);
    extra += " kinds " + JSON.stringify(kinds);
  } else {
    if (last.ok) ok(verifyQueens(G.n, last.queens), `${key}/${name}/${sk} placement attacks`);
    ok(last.ok === !(G.n === 2 || G.n === 3), `${key}/${name}/${sk} ok flag wrong`);
  }
  frames.forEach((f, i) => ok(typeof f.caption === "string" && f.caption.length > 0, `${key}/${name}/${sk} frame ${i} has no caption`));
  console.log(`${key} | ${name} | ${sname} | frames ${frames.length} | ok ${last.ok} | ${ms}ms${extra}\n   ${last.caption}`);
}
// every n for both N-Queens solvers
for (let n = 1; n <= 12; n++) for (const sk of ["bt", "construct"]) {
  const last = B.SOLVE.queens[sk]({ kind: "queens", n }).at(-1);
  ok(last.ok === !(n === 2 || n === 3), `queens ${sk} n=${n} ok flag`);
  if (last.ok) ok(verifyQueens(n, last.queens), `queens ${sk} n=${n} invalid`);
}
// constructive for large n too (formula correctness, no drawing)
for (let n = 4; n <= 60; n++) { const last = B.SOLVE.queens.construct({ kind: "queens", n }).at(-1); ok(last.ok && verifyQueens(n, last.queens), `construct n=${n}`); }
// parse errors read well
for (const bad of ["1,2;3", "1,2,3;4,5,6;7,8,9", "5,0,0,0;0,0,0,0;0,0,0,0;0,0,0,0", "a,0,0,0;0,0,0,0;0,0,0,0;0,0,0,0", ""]) {
  try { B.parse(bad, B.PROBLEMS.SUDOKU); fail++; console.log("FAIL: accepted", JSON.stringify(bad)); } catch (e) { console.log("sudoku error:", e.message); }
}
for (const bad of ["0", "13", "-1", "4x", ""]) {
  try { B.parse(bad, B.PROBLEMS.NQUEENS); fail++; console.log("FAIL: accepted", JSON.stringify(bad)); } catch (e) { console.log("queens error:", e.message); }
}
console.log(fail ? `${fail} FAILURES` : "all passed");
