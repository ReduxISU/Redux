// Node tests for circuitgrammar.js: every example × solver, plus random cross-checks.
const fs = require("fs");
const V = eval(fs.readFileSync(__dirname + "/circuitgrammar.js", "utf8") + ";CircuitGrammarView");
const { IMPL, cyk, evaluate, parseCircuit, parseGrammar } = V._impl;
let fails = 0;
const ok = (c, msg) => { if (!c) { fails++; console.log("FAIL:", msg); } };

for (const [k, P] of Object.entries(V.PROBLEMS)) for (const [name, s] of P.examples) for (const [sv] of P.solvers) {
  const m = IMPL[k].parse(s);
  const frames = k === "CFGMEMBER" ? cyk(m).frames : IMPL[k].solve(m, sv);
  const last = frames[frames.length - 1];
  ok(last.done, `${k}/${name}/${sv} ends done`);
  frames.forEach((f, i) => ok(typeof f.caption === "string" && f.caption.length > 5, `${k}/${name}/${sv} frame ${i} caption`));
  console.log(`${k} | ${name} | ${sv} | frames ${frames.length} | ok ${last.ok}\n    ${last.caption}`);
  if (k === "CIRCUITSAT" && last.ok) ok(evaluate(m, last.assign)[m.out] === 1, `${name} assignment satisfies`);
}

// brute force vs backtracking vs exhaustive on random circuits
let seed = 11; const rnd = () => (seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648;
const OPS = ["AND", "OR", "XOR", "NAND", "NOR", "NOT"];
for (let t = 0; t < 400; t++) {
  const n = 2 + Math.floor(rnd() * 4), X = Array.from({ length: n }, (_, i) => "x" + (i + 1)), names = X.slice(), gates = [];
  const gn = 2 + Math.floor(rnd() * 6);
  for (let g = 0; g < gn; g++) {
    const op = OPS[Math.floor(rnd() * OPS.length)], k = op === "NOT" ? 1 : 2 + (rnd() < 0.2 ? 1 : 0);
    const ins = []; for (let j = 0; j < k; j++) ins.push(names[Math.floor(rnd() * names.length)]);
    gates.push(`(g${g},${op},${ins.join(",")})`); names.push("g" + g);
  }
  const s = `({${X}},{${gates}},g${gn - 1})`;
  const C = parseCircuit(s, false);
  let truth = false;
  for (let mask = 0; mask < 1 << n; mask++) { const a = {}; X.forEach((x, i) => (a[x] = (mask >> i) & 1)); if (evaluate(C, a)[C.out] === 1) { truth = true; break; } }
  const b = IMPL.CIRCUITSAT.solve(C, "brute").at(-1), bt = IMPL.CIRCUITSAT.solve(C, "bt").at(-1);
  ok(b.ok === truth && bt.ok === truth, `random circuit ${t}: brute ${b.ok} bt ${bt.ok} truth ${truth} :: ${s}`);
  if (bt.ok) ok(evaluate(C, bt.assign)[C.out] === 1, `random ${t} bt assignment valid`);
}

// CYK vs brute-force derivation on random small CNF grammars
const derives = (G, w) => { // memoized recursive membership, independent of the table
  const memo = new Map();
  const can = (A, i, j) => {
    const key = A + "|" + i + "|" + j; if (memo.has(key)) return memo.get(key);
    let r = false;
    if (j - i === 1) r = G.unary.some(u => u.A === A && u.a === w[i]);
    else for (const b of G.binary) if (b.A === A) for (let k = i + 1; k < j && !r; k++) if (can(b.B, i, k) && can(b.C, k, j)) r = true;
    memo.set(key, r); return r;
  };
  return w.length === 0 ? G.eps : can(G.start, 0, w.length);
};
for (let t = 0; t < 300; t++) {
  const Vs = ["S", "A", "B", "C"].slice(0, 2 + Math.floor(rnd() * 3)), rules = [];
  for (const A of Vs) {
    rules.push(`(${A},${rnd() < 0.5 ? "a" : "b"})`);
    for (let r = 0; r < 2; r++) rules.push(`(${A},${Vs[Math.floor(rnd() * Vs.length)]}${Vs[Math.floor(rnd() * Vs.length)]})`);
  }
  const len = 1 + Math.floor(rnd() * 6), w = Array.from({ length: len }, () => (rnd() < 0.5 ? "a" : "b")).join("");
  const s = `(({${Vs}},{a,b},{${rules}},S),${w})`;
  const G = parseGrammar(s), R = cyk(G), last = R.frames.at(-1);
  ok(last.ok === derives(G, G.w), `random grammar ${t}: cyk ${last.ok} :: ${s}`);
  if (last.ok) { // the parse tree's leaves spell w
    const nodes = R.frames.find(f => f.nodes).nodes;
    ok(nodes.filter(x => x.leaf).sort((a, b) => a.i - b.i).map(x => x.sym).join("") === w, `tree leaves spell w (${t})`);
  }
}

// bad input
for (const [k, s] of [["CVP", "(({x1},{(g1,AND,x1)},g1),{(x1,1)})"], ["CVP", "(({x1,x2},{(g1,AND,x1,x2)},g1),{(x1,1)})"], ["CIRCUITSAT", "({x1},{(g1,AND,x1,g2),(g2,OR,x1,g1)},g1)"],
  ["CFGMEMBER", "(({S,A},{a},{(S,aA),(A,a)},S),aa)"], ["CFGMEMBER", "(({S},{a},{(S,a)},S),b)"], ["CFGMEMBER", "(({S,A},{a},{(A,ε),(S,a)},S),a)"]]) {
  try { IMPL[k].parse(s); fails++; console.log("FAIL: accepted bad input", s); } catch (e) { console.log("rejects:", e.message); }
}
console.log(fails ? `${fails} FAILURES` : "all tests pass");
