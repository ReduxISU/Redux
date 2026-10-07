const fs = require("fs");
const QV = eval(fs.readFileSync(__dirname + "/quantum.js", "utf8") + ";QuantumView");
let fails = 0;
const check = (cond, msg) => { if (!cond) { fails++; console.log("  FAIL:", msg); } };
for (const [key, P] of Object.entries(QV.PROBLEMS)) {
  for (const [name, inst] of P.examples) {
    const t0 = Date.now();
    const { B, frames, last } = QV._solve(key, inst);
    const ms = Date.now() - t0;
    frames.forEach((f, i) => {
      if (f.dist) { const s = f.dist.probs.reduce((a, b) => a + b, 0); check(Math.abs(s - 1) < 1e-9, `${key} ${name} frame ${i} probs sum ${s}`); }
      check(typeof f.caption === "string" && f.caption.length > 0, `${key} ${name} frame ${i} caption`);
    });
    console.log(`${key} | ${name} | frames ${frames.length} | ok ${last.ok} | answer ${last.answer} | ${ms}ms`);
    console.log("    " + last.caption);
  }
}
// specific expectations
const ans = (k, s) => QV._solve(k, s).last;
check(ans("BERNSTEINVAZIRANI", "(0,1,0,1,1,0,1,0)").answer === "101" && ans("BERNSTEINVAZIRANI", "(0,1,0,1,1,0,1,0)").ok, "BV default recovers 101");
check(ans("BERNSTEINVAZIRANI", "(0,1,1,0,1,0,0,1)").answer === "111", "BV recovers 111");
check(ans("BERNSTEINVAZIRANI", "(0,1,1,1,0,0,0,0)").ok === false, "BV non-linear flagged");
check(ans("DEUTSCH", "(0,1)").answer === "balanced" && ans("DEUTSCH", "(1,1)").answer === "constant", "Deutsch");
check(ans("DEUTSCH", "(1,0)").answer === "balanced" && ans("DEUTSCH", "(0,0)").answer === "constant", "Deutsch other two functions");
check(ans("DEUTSCHJOZSA", "(1,1,1,1)").answer === "constant" && ans("DEUTSCHJOZSA", "(0,1,1,0,1,0,0,1)").answer === "balanced", "DJ");
{ const r = QV._solve("DEUTSCHJOZSA", "(0,1,1,0,1,0,0,1)"); const d = r.frames[r.frames.length - 1].dist; check(d.probs[0] < 1e-12, "DJ balanced: all-zero has probability 0"); }
check(ans("DEUTSCHJOZSA", "(1,0,0,0)").ok === false, "DJ neither flagged");
check(ans("SIMON", "(5,6,5,6,3,2,3,2)").answer === "010" && ans("SIMON", "(5,6,5,6,3,2,3,2)").ok, "Simon 010");
check(ans("SIMON", "(0,1,2,3)").answer === "00" && ans("SIMON", "(0,1,2,3)").ok, "Simon 00");
{ const r = QV._solve("SIMON", "(5,6,5,6,3,2,3,2)"); const d = r.frames.find(f => f.col === 5).dist; d.probs.forEach((p, y) => { if (p > 1e-9) { let x = y & 2, c = 0; while (x) { c ^= x & 1; x >>= 1; } check(c === 0, `Simon outcome ${y} orthogonal to s`); } }); }
for (const s of ["(0,1,0,0)", "(0,0,0,0,0,1,0,0)", "(0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,0)"]) {
  const r = QV._solve("UNSTRUCTUREDSEARCH", s), d = r.frames[r.frames.length - 1].dist, marked = s.slice(1, -1).split(",").indexOf("1");
  console.log(`  Grover ${s}: P(marked) = ${d.probs[marked].toFixed(4)}`);
  check(r.last.answer === String(marked) && d.probs[marked] > 0.9, `Grover peaks on ${marked}`);
}
check(ans("UNSTRUCTUREDSEARCH", "(0,0,0,0)").answer === "no solution", "Grover none");
check(ans("PRIMEFACTOR", "12").answer === "(2,2,3)" && !QV._solve("PRIMEFACTOR", "12").B.circ, "Shor 12 classical only");
check(ans("PRIMEFACTOR", "15").answer === "(3,5)", "Shor 15");
check(ans("PRIMEFACTOR", "21").answer === "(3,7)", "Shor 21");
check(ans("PRIMEFACTOR", "13").answer === "(13)", "prime");
check(ans("PRIMEFACTOR", "9").answer === "(3,3)", "prime power");
check(ans("PRIMEFACTOR", "33").answer === "(3,11)", "33 too big to simulate but factored");
check(ans("PRIMEFACTOR", "105").answer === "(3,5,7)", "105");
{ const r = QV._solve("PRIMEFACTOR", "21"); console.log("  21 readout frames:"); r.frames.filter(f => /Reading/.test(f.caption)).forEach(f => console.log("    " + f.caption)); }
{ const r = QV._solve("PRIMEFACTOR", "15"); const d = r.frames.find(f => f.periodKnown && f.dist).dist; const peaks = d.probs.map((p, k) => [p, k]).filter(([p]) => p > 0.01).map(([p, k]) => `${k}:${p.toFixed(3)}`); console.log("  15 peaks:", peaks.join(" ")); check(peaks.length === 4, "15 has 4 peaks"); }
// BV phase kickback: after the oracle, sign of x = (-1)^(s·x)
{ const r = QV._solve("BERNSTEINVAZIRANI", "(0,1,0,1,1,0,1,0)"); const d = r.frames[3].dist; const s = 5; d.probs.forEach((p, x) => { let v = x & s, c = 0; while (v) { c ^= v & 1; v >>= 1; } check(d.signs[x] === (c ? -1 : 1), `BV sign at ${x}`); }); }
// bad inputs
for (const [k, s] of [["BERNSTEINVAZIRANI", "(0,1,1)"], ["DEUTSCH", "(0,1,1)"], ["PRIMEFACTOR", "1"], ["PRIMEFACTOR", "abc"], ["SIMON", "(1,2,3)"], ["UNSTRUCTUREDSEARCH", "(1,0)"]]) {
  try { QV._solve(k, s); check(false, `${k} ${s} should fail`); } catch (e) { console.log(`  ${k} ${s} -> ${e.message}`); }
}
console.log(fails ? `${fails} FAILURES` : "ALL PASS");
