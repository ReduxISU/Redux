// Node tests for machine.js: verdicts on every example, plus random checks of the TM and PDA simulators.
const fs = require("fs");
const V = eval(fs.readFileSync(__dirname + "/machine.js", "utf8") + ";MachineView");
let fails = 0;
const ok = (c, m) => { if (!c) { fails++; console.log("FAIL", m); } };
const expect = {
  "TMACCEPT|0": "accept", "TMACCEPT|1": "stuck", "TMACCEPT|2": "accept", "TMACCEPT|3": "limit",
  "LBAACCEPT|0": "accept", "LBAACCEPT|1": "stuck", "LBAACCEPT|2": "loop",
  "PDAACCEPT|0": true, "PDAACCEPT|1": true, "PDAACCEPT|2": false, "PDAACCEPT|3": true,
};
for (const [k, P] of Object.entries(V.PROBLEMS)) P.examples.forEach(([nm, inst], i) => {
  const M = V.parse(inst, k);
  if (M.kind === "tm") {
    for (const lim of M.bounded ? [5000] : [200, 2000]) {
      const fr = V._runTM(M, lim), L = fr[fr.length - 1];
      ok(L.verdict === expect[k + "|" + i], `${k} ${nm}: got ${L.verdict}`);
      ok(fr.slice(0, -1).every(f => !f.done), `${k} ${nm}: only the last frame is done`);
    }
  } else {
    const a = V._pdaAll(M), runs = V._pdaRuns(M);
    ok(a[a.length - 1].ok === expect[k + "|" + i], `${k} ${nm}: all-branches verdict`);
    ok(runs.some(r => r.ok) === a[a.length - 1].ok, `${k} ${nm}: runs agree with all-branches`);
  }
});
// binary increment really adds one
const inc = V.PROBLEMS.TMACCEPT.examples[2][1].replace(/,1011\)$/, "");
for (let n = 1; n < 200; n++) {
  const M = V.parse(inc + "," + n.toString(2) + ")", "TMACCEPT"), L = V._runTM(M, 200).at(-1);
  ok(parseInt(L.tape.cells.join("").replace(/_/g, ""), 2) === n + 1, "increment " + n);
}
// aⁿbⁿ machines (TM, PDA) and the aⁿbⁿcⁿ LBA against the real language, on every string up to length 8
const anbn = w => /^(a*)(b*)$/.test(w) && w.split("a").length - 1 === w.split("b").length - 1;
const anbncn = w => { const m = /^(a*)(b*)(c*)$/.exec(w); return !!m && m[1].length === m[2].length && m[2].length === m[3].length; };
const words = (al, n) => { let out = [""]; const all = [""]; for (let i = 0; i < n; i++) { out = out.flatMap(w => al.map(c => w + c)); all.push(...out); } return all; };
const TM = V.PROBLEMS.TMACCEPT.examples[0][1].replace(/,aabb\)$/, ""), PDA = V.PROBLEMS.PDAACCEPT.examples[1][1].replace(/,aabb\)$/, ""), LBA = V.PROBLEMS.LBAACCEPT.examples[0][1].replace(/,aabbcc\)$/, "");
for (const w of words(["a", "b"], 8)) {
  const t = V._runTM(V.parse(TM + "," + (w || "ε") + ")", "TMACCEPT"), 2000).at(-1);
  ok((t.verdict === "accept") === anbn(w), "TM aⁿbⁿ on " + JSON.stringify(w));
  const p = V._pdaAll(V.parse(PDA + "," + (w || "ε") + ")", "PDAACCEPT")).at(-1);
  ok(p.ok === anbn(w), "PDA aⁿbⁿ on " + JSON.stringify(w));
}
for (const w of words(["a", "b", "c"], 6)) {
  const t = V._runTM(V.parse(LBA + "," + (w || "ε") + ")", "LBAACCEPT"), 5000).at(-1);
  ok((t.verdict === "accept") === anbncn(w), "LBA aⁿbⁿcⁿ on " + JSON.stringify(w));
}
// even palindromes PDA against the language
const PAL = V.PROBLEMS.PDAACCEPT.examples[3][1].replace(/,abba\)$/, "");
for (const w of words(["a", "b"], 7)) {
  const p = V._pdaAll(V.parse(PAL + "," + (w || "ε") + ")", "PDAACCEPT")).at(-1);
  ok(p.ok === (w.length % 2 === 0 && w === [...w].reverse().join("")), "palindrome on " + JSON.stringify(w));
}
console.log(fails ? `${fails} failures` : "all machine tests pass");
