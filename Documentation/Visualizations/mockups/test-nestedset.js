// Tests for nestedset.js: the solvers against brute force, the frame rules (the Bipartite Graph's), and the regions layout
// (every member inside its region, every other dot clear of it, labels apart, deterministic, the set-list fallback).
const { load } = require("./dom-shim.js");
const NV = load("nestedset.js", "NestedSetView");
let fails = 0, n = 0;
const ok = (cond, msg) => { n++; if (!cond) { fails++; if (fails < 25) console.log("  FAIL:", msg); } };
let seed = 5; const rnd = () => (seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648;

function truth(I) {
  const M = NV.model(NV.payload(I)), picks = M.picks, k = picks.length;
  for (let m = 1; m < 1 << k; m++) {
    const pick = picks.filter((_, i) => m & (1 << i));
    if (I.K !== null && pick.length > I.K) continue;
    const cnt = new Map(M.cons.map(c => [c, 0]));
    pick.forEach(p => M.hits.get(p).forEach(c => cnt.set(c, cnt.get(c) + 1)));
    if ([...cnt.values()].every(v => (M.exact ? v === 1 : v >= 1))) return true;
  }
  return false;
}
const randomInstance = kind => {
  const u = 2 + Math.floor(rnd() * 5), U = Array.from({ length: u }, (_, i) => String(i + 1));
  const sets = Array.from({ length: 1 + Math.floor(rnd() * 5) }, () => U.filter(() => rnd() < 0.4));
  sets.forEach(s => { if (!s.length) s.push(U[Math.floor(rnd() * u)]); });
  return `({${U}},{${sets.map(s => `{${s}}`)}}${kind === "SETCOVER" ? "," + (1 + Math.floor(rnd() * 3)) : ""})`;
};

// solvers: brute force and Algorithm X agree with the truth; greedy never claims a cover it doesn't have
for (let t = 0; t < 300; t++) for (const kind of ["SETCOVER", "EXACTCOVER", "HITTINGSET"]) {
  const str = randomInstance(kind), I = NV.parse(str, kind), M = NV.model(NV.payload(I)), want = truth(I);
  for (const s of kind === "SETCOVER" ? ["greedy", "brute"] : ["x", "brute"]) {
    const run = NV.SOLVERS[s].run(M, I), last = run.steps.at(-1);
    if (s !== "greedy") ok(last.ok === want, `${s} ${kind} ${str}: ok ${last.ok}, want ${want}`);
    else if (last.ok) ok(want, `greedy claims a cover for ${str}`);
    const frames = NV.build(M, run);
    frames.forEach((f, i) => {
      const states = Object.values(f.nodes);
      const ends = f.event === "Done" || (f.event === "Accept" && frames[i + 1] && frames[i + 1].event === "Done" && frames[i + 1].ok);
      if (!ends) ok(!states.includes("Solution"), `${s} frame ${i} of ${str} shows Solution before the answer`);
      if (!run.incremental) ok(!states.includes("Committed"), `${s} frame ${i} of ${str}: Committed in a brute force`);
      if (f.event === "Done" && !f.ok) ok(!states.includes("Solution") && !states.includes("Committed"), `${s} failed Done of ${str} shows green`);
    });
  }
}
// frame rules, Exact Cover ({1,2,3},{{1,2},{2,3},{3}})
{
  const I = NV.parse("({1,2,3},{{1,2},{2,3},{3}})", "EXACTCOVER"), M = NV.model(NV.payload(I));
  const P = NV.payload(I);
  ok(P.elements[0].id === "e:1" && P.sets[1].id === "S2" && P.sets[1].members.join() === "e:2,e:3" && !("edges" in P), "Bipartite ids, no edges");
  const a = NV.frame(M, { event: "Try", picks: ["S1"], focus: ["S1"] }, { incremental: true });
  ok(a.nodes.S1 === "ElementHighlight" && a.nodes["e:1"] === "Covered" && a.nodes.S2 === "Blocked" && !a.nodes.S3, "tried set highlighted, its elements Covered, an overlapping set Blocked: " + JSON.stringify(a.nodes));
  const b = NV.frame(M, { event: "Try", picks: ["S1", "S2"], focus: [] }, { incremental: false });
  ok(b.nodes.S1 === "ElementHighlight" && b.nodes["e:2"] === "Rejected" && b.hits["e:2"] === 2, "a brute-force candidate is highlighted; an element covered twice is Rejected with hits 2");
  const c = NV.frame(M, { event: "Accept", picks: ["S1", "S3"], focus: [] }, { incremental: true });
  ok(c.nodes.S1 === "Committed" && c.nodes.S3 === "Committed" && c.nodes.S2 === "Blocked", "kept picks Committed mid-run");
  const d = NV.frame(M, { event: "Done", picks: ["S1", "S3"], focus: [], ok: true });
  ok(d.nodes.S1 === "Solution" && d.nodes["e:3"] === "Covered", "Done ok: Solution");
  const e = NV.frame(M, { event: "Done", picks: ["S2"], focus: [], ok: false });
  ok(e.nodes.S2 === "Rejected" && e.nodes["e:1"] === "Rejected", "Done ok=false: picks Rejected, unhit elements Rejected");
  const g = NV.frame(M, { event: "Done", picks: [], focus: [], ok: false });
  ok(!Object.keys(g.nodes).length, "Done ok=false with nothing picked draws nothing (as the backend does)");
  const r = NV.frame(M, { event: "Reject", picks: ["S2"], focus: ["e:1"] }, { incremental: true });
  ok(r.nodes["e:1"] === "Rejected", "an incremental Reject's focus is Rejected");
}
// Hitting Set picks elements; a set hit twice is Rejected
{
  const I = NV.parse("({1,2,3},{{1,2},{2,3}})", "HITTINGSET"), M = NV.model(NV.payload(I));
  const f = NV.frame(M, { event: "Done", picks: ["e:2"], focus: [], ok: true });
  ok(f.nodes["e:2"] === "Solution" && f.nodes.S1 === "Covered" && f.nodes.S2 === "Covered" && f.nodes["e:1"] === "Blocked", "Hitting Set: element picked, sets Covered, an element that would double up Blocked");
  const g = NV.frame(M, { event: "Try", picks: ["e:1", "e:3"], focus: [] });
  ok(!g.nodes.S1 || g.nodes.S1 === "Covered", "Hitting Set: sets hit once are Covered");
}
// regions layout
const checkRegions = (str, kind) => {
  const P = NV.payload(NV.parse(str, kind)), R = NV.regions(P);
  if (!R.ok) return R;
  const R2 = NV.regions(P);
  ok(JSON.stringify([...R.pos]) === JSON.stringify([...R2.pos]) && R.regs.map(r => r.d).join() === R2.regs.map(r => r.d).join(), "layout is deterministic: " + str);
  P.sets.forEach(s => {
    const r = R.regs.find(x => x.id === s.id), pts = s.members.map(m => R.pos.get(m));
    P.elements.forEach(e => {
      const d = NV._distToHull(R.pos.get(e.id), pts);
      if (s.members.includes(e.id)) ok(d < 1e-6, `${e.id} sits inside ${s.id}`);
      else ok(d >= r.pad + 16 + NV.LIMITS.clearance, `${e.id} is clear of ${s.id} (${d.toFixed(1)} vs ${r.pad + 16})`);
    });
  });
  const hit = (a, b) => a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h;
  ok(R.labels.every((a, i) => R.labels.every((b, j) => i >= j || !hit(a.box, b.box))), "set labels never overlap: " + str);
  const clearOf = (p, b) => Math.hypot(Math.max(b.x - p.x, 0, p.x - b.x - b.w), Math.max(b.y - p.y, 0, p.y - b.y - b.h));
  ok(R.labels.every(l => [...R.pos.values()].every(p => clearOf(p, l.box) >= 16)), "set labels never cover a dot: " + str);
  ok(R.labels.every(l => l.box.x >= 0 && l.box.y >= 0 && l.box.x + l.w <= R.W && l.box.y + l.h <= R.H), "labels stay inside the picture: " + str);
  return R;
};
{
  for (const [s, k] of [["({1,2,3,4,5},{{1,2,3},{2,4},{3,4},{4,5}},3)", "SETCOVER"], ["({1,2,3,4},{{1,2,3},{2,3},{4,1}})", "EXACTCOVER"], ["({1,2,3,4},{{1,3},{2,3,4},{1,4}})", "HITTINGSET"],
    ["({1,2,3,4,5,6},{{1,2},{1,3},{2,4},{3,5,6},{4,5,6},{4,6},{5,6}})", "EXACTCOVER"], ["({1,2,3,4,5},{{1,2},{2,3},{3,4},{4,5}})", "HITTINGSET"]]) {
    const R = checkRegions(s, k);
    ok(R.ok, `the mockup example ${s} draws as regions (${R.why || ""})`);
  }
  let drawn = 0, fell = 0;
  for (let t = 0; t < 200; t++) { const R = checkRegions(randomInstance("EXACTCOVER"), "EXACTCOVER"); R.ok ? drawn++ : fell++; }
  ok(drawn > 100, `most small random instances draw as regions (${drawn} drawn, ${fell} fell back)`);
  const big = "({1,2,3,4,5,6,7,8},{{1,2},{2,3},{3,4},{4,5},{5,6},{6,7},{7,8},{8,1},{1,5}})";
  const B = NV.regions(NV.payload(NV.parse(big, "EXACTCOVER")));
  ok(!B.ok && /more than 8 sets/.test(B.why), "nine sets fall back to the set list: " + B.why);
  const deep = NV.regions(NV.payload(NV.parse("({1,2,3,4,5,6},{{1},{1,2},{1,3},{1,4},{1,5}})", "EXACTCOVER")));
  ok(!deep.ok && /more than 4/.test(deep.why), "an element in five sets falls back: " + deep.why);
  const L = NV.list(NV.payload(NV.parse(big, "EXACTCOVER")));
  ok(L.rows.length === 10 && L.rows[0].id === "U" && L.rows[1].chips.map(c => c.id).join() === "e:1,e:2", "the set list: U, then one row per set with its members");
  ok(L.rows.every((r, i) => !i || r.y >= L.rows[i - 1].y + L.rows[i - 1].h), "set-list rows do not overlap");
}
console.log(fails ? `${fails} FAILURES (${n} checks)` : `ALL PASS (${n} checks)`);
process.exit(fails ? 1 : 0);
