// Node harness for schedule.js: runs every example x solver, checks frames against an independent simulation.
const fs = require("fs");
const src = fs.readFileSync(__dirname + "/schedule.js", "utf8")
  .replace("return { create, parse:", "return { jobBrute, jobGreedy, pumpFrames, simulate, parseJob, parsePump, jobTimeline, penaltyOf, create, parse:");
const SV = eval(src + ";ScheduleView");
let fails = 0;
const ok = (c, msg) => { if (!c) { fails++; console.log("  FAIL:", msg); } };

for (const [key, P] of Object.entries(SV.PROBLEMS)) {
  for (const [name, inst] of P.examples) {
    for (const [sv, svName] of P.solvers) {
      const t0 = Date.now();
      let frames, I;
      if (key === "JOBSEQ") { I = SV.parseJob(inst); frames = sv === "greedy" ? SV.jobGreedy(I) : SV.jobBrute(I); }
      else { I = SV.parsePump(inst, key === "PUMPEM"); frames = SV.pumpFrames(I).frames; }
      const last = frames[frames.length - 1];
      console.log(`${key} | ${name} | ${svName} | frames ${frames.length} | ok ${last.ok} | ${Date.now() - t0} ms`);
      console.log("   ", last.caption);
      if (key === "JOBSEQ") {
        // independent check: penalty of the final order, and existence by exhaustive search
        const perm = a => a.length <= 1 ? [a] : a.flatMap((x, i) => perm([...a.slice(0, i), ...a.slice(i + 1)]).map(p => [x, ...p]));
        const pen = o => { let t = 0, s = 0; o.forEach(j => { t += I.T[j]; if (t > I.D[j]) s += I.P[j]; }); return s; };
        const all = perm(I.T.map((_, i) => i)), best = Math.min(...all.map(pen));
        ok(last.order && new Set(last.order).size === I.n, "final order is a permutation");
        ok(last.ok === (pen(last.order) <= I.K), "ok matches penalty");
        if (sv === "brute") ok(last.ok === (best <= I.K), "brute ok iff a valid order exists");
        if (sv === "brute" && last.ok) {
          const first = all.find(o => pen(o) <= I.K); // lexicographic first
          ok(first.join() === last.order.join(), `brute returns lexicographic first (${first})`);
        }
        console.log(`    order ${last.order ? "(" + last.order + ")" : "-"}, penalty ${last.order ? pen(last.order) : "-"}, best possible ${best}`);
      } else {
        if (last.sim) {
          const s = SV.simulate(I, last.masks);
          ok(Math.abs(s.total - last.sim.total) < 1e-9, "sim total stable");
          console.log(`    total $${s.total.toFixed(2)}, tank ${s.low}..${s.high}, stored ${s.stored}, within limits ${s.ok}`);
          // replay frames: 24, each cursor in order
          const rep = frames.filter(f => f.phase === "replay");
          ok(rep.length === 24 && rep.every((f, i) => f.cursor === i), "24 replay frames in order");
          // brute-force optimality check on CM for small pump counts: compare to random schedules
          if (key === "PUMPCM") {
            let better = 0;
            for (let r = 0; r < 4000; r++) {
              const m = Array.from({ length: 24 }, () => Math.floor(Math.random() * (1 << I.pumps.length)));
              const x = SV.simulate(I, m);
              if (x.ok && x.total < s.total - 1e-6) better++;
            }
            ok(better === 0, "no random feasible schedule beats the DP");
          }
        }
        const fw = frames.filter(f => f.phase === "forward");
        console.log(`    forward frames ${fw.length}, states at last hour ${fw.length ? fw[fw.length - 1].stats.at(-1).count : 0}`);
      }
    }
  }
}
// parse errors read clearly
for (const [k, bad] of [["JOBSEQ", "((1,2),(3),(1,1),2)"], ["JOBSEQ", "((1,2),(3,4),(1,1))"], ["PUMPCM", "((100,50),((1,2),(1),(0.1,0.2)),((A,1,1,1)))"], ["PUMPEM", "((100,50,60),((" + Array(24).fill(1) + "),(),(0.1,0.1)),((A,1,1,1)),0)"]]) {
  try { SV.parse(bad, k); console.log("NO ERROR for", bad); fails++; } catch (e) { console.log("err ok:", k, "-", e.message); }
}
console.log(fails ? `${fails} FAILURES` : "all checks passed");
