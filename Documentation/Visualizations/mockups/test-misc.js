const fs = require("fs");
const MV = eval(fs.readFileSync("misc.js", "utf8") + ";MiscView");
const I = MV._impl;
let fails = 0;
const assert = (c, msg) => { if (!c) { fails++; console.log("  FAIL:", msg); } };

// Convex Hull: port must reproduce Redux's certificate example (same start, same order)
const hullEx = "((0.2723211656942368,-0.8053758131859647), (0.7674622377407927,-0.21537444528240846), (0.6077591838324792,0.5288040272918157), (-0.32705115386597394,0.6744065707101621), (-0.6984449872706371,0.3857380723376367), (-0.9308276577586132,-0.1423800479224624), (-0.27394905790800017,-0.7488048223660126))";
const expect = [...hullEx.matchAll(/\((-?[\d.]+),(-?[\d.]+)\)/g)].map(m => m[1] + "," + m[2]);
for (const [k, P] of Object.entries(MV.PROBLEMS)) {
  for (const [ename, inst] of P.examples) {
    for (const [sv, sname] of P.solvers) {
      const D = I[k].parse(inst);
      const frames = I[k].solve(D, sv);
      const last = frames[frames.length - 1];
      const ck = I[k].checks(D, last);
      console.log(`${k} | ${ename} | ${sv}: frames ${frames.length}, ok ${last.ok}`);
      console.log("   ", last.caption);
      console.log("    checks:", ck.map(c => c.label + (c.ok === true ? " ✓" : c.ok === false ? " ✗" : "")).join(" ; "));
      console.log("    chips:", I[k].chosen(D, last).map(c => c.text).join(" | ").slice(0, 200));
      // every frame must evaluate
      frames.forEach((f, i) => { try { I[k].checks(D, f); I[k].chosen(D, f); } catch (e) { assert(false, `${k} ${sv} frame ${i}: ${e.message}`); } });
      if (k === "CONVEXHULL") {
        if (!ename.includes("Redux bug") || sv === "chain") assert(ck.every(c => c.ok !== false), "hull checks pass " + ename + " " + sv); else assert(last.ok === false, "bug example flagged");
        if (ename === "Redux default" && sv === "dc") {
          const got = last.hull.map(i => D.pts[i].x + "," + D.pts[i].y);
          assert(JSON.stringify(got) === JSON.stringify(expect), "D&C port matches Redux certificate example: got " + got.join(" "));
        }
      }
    }
  }
}
// Errors
const bad = [["CONVEXHULL", "{(0,0),(1,1)}"], ["CONVEXHULL", "{(0,0),(1,x),(2,2)}"], ["CONVEXHULL", "{(0,0),(1,1),(0,0)}"], ["DM3", "{a,b}{c}"], ["DM3", "{a,b}{c,d}{e,f}{a,c,q}"], ["DM3", "{a,b}{a,d}{e,f}{a,a,e}"], ["LDC", ""]];
for (const [k, s] of bad) { try { I[k].parse(s); assert(false, "should fail: " + s); } catch (e) { console.log(`error ok (${k} ${JSON.stringify(s)}): ${e.message}`); } }

// DM3 cross-check: brute and backtracking must agree on existence for random instances
let agree = 0;
for (let t = 0; t < 400; t++) {
  const n = 2 + (t % 3), names = p => Array.from({ length: n }, (_, i) => p + i);
  const X = names("x"), Y = names("y"), Z = names("z"), m = n + (t % 5);
  const M = Array.from({ length: m }, (_, i) => `{${X[(i * 7 + t) % n]},${Y[(i * 3 + t * 5) % n]},${Z[(i * 5 + t * 3) % n]}}`);
  const s = `{${X}}{${Y}}{${Z}}${M.join("")}`;
  const D = I.DM3.parse(s);
  const a = I.DM3.solve(D, "brute").pop().ok, b = I.DM3.solve(I.DM3.parse(s), "bt").pop().ok;
  if (a === b) agree++; else assert(false, "brute/backtrack disagree on " + s);
}
console.log(`DM3 brute vs backtracking agree on ${agree}/400 random instances`);

// Hull cross-check: D&C vs monotone chain give the same corner set on random point sets
let same = 0;
let seed = 11; const rnd = () => (seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648;
for (let t = 0; t < 300; t++) {
  const n = 3 + (t % 25), pts = [];
  const seen = new Set();
  while (pts.length < n) { const x = Math.round(rnd() * 40) / 4, y = Math.round(rnd() * 40) / 4; if (!seen.has(x + "," + y)) { seen.add(x + "," + y); pts.push(`(${x},${y})`); } }
  const s = `{${pts.join(",")}}`;
  const D1 = I.CONVEXHULL.parse(s), D2 = I.CONVEXHULL.parse(s);
  const h1 = I.CONVEXHULL.solve(D1, "dc").pop().hull, h2 = I.CONVEXHULL.solve(D2, "chain").pop().hull;
  const k1 = h1.slice().sort((a, b) => a - b).join(), k2 = h2.slice().sort((a, b) => a - b).join();
  const c1 = I.CONVEXHULL.checks(D1, { done: true, hull: h1 });
  if (k1 === k2 && c1.every(c => c.ok !== false)) same++;
  else if (t < 400) { console.log("  hull mismatch", s, "dc:", h1.join(), "chain:", h2.join(), c1.map(c => c.label + c.ok).join(";")); }
}
console.log(`Convex hull: divide-and-conquer and monotone chain agree on ${same}/300 random point sets (with collinear points)`);

// LDC: decode check and Huffman optimality vs brute-force-free bound (entropy)
for (const s of ["a", "aaaa", "ab", "hello world", "the quick brown fox jumps over the lazy dog"]) {
  const D = I.LDC.parse(s); const fr = I.LDC.solve(D); const ck = I.LDC.checks(D, fr[fr.length - 1]);
  assert(ck.every(c => c.ok !== false), "ldc checks " + s);
  console.log(`LDC "${s}": ${D.bits} bits, frames ${fr.length}`);
}
console.log(fails ? `${fails} FAILURES` : "all good");
