/* ---- Quantum circuit view: one picture for every quantum-algorithm problem ----
   One circuit description (qubits + columns of gates + stages) drives the drawing and an in-browser
   statevector simulation. Each step applies one column; the bars underneath show the probability of
   each outcome on the register that gets measured, colored by the sign of its amplitude, so phase
   kickback is visible. Pages supply --av-* tokens. */
const QuantumView = (() => {
  const NS = "http://www.w3.org/2000/svg";
  const mk = (tag, attrs = {}, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; };
  const COLW = 74, WIREH = 44, LEFT = 96, TOP = 70, BARH = 150, EPS = 1e-12;

  const css = `
.qc-svg { width: 100%; height: auto; display: block; }
.qc-svg text { font-family: var(--av-mono); }
.qc-wire { stroke: var(--av-edge); stroke-width: 1.4; }
.qc-wname { font-size: 13px; fill: var(--av-ink); text-anchor: end; dominant-baseline: central; }
.qc-wrole { font-size: 10.5px; fill: var(--av-muted); text-anchor: end; dominant-baseline: central; }
.qc-stage rect { fill: none; stroke: var(--av-stroke); stroke-dasharray: 4 4; }
.qc-stage text { font-size: 11px; fill: var(--av-muted); text-anchor: middle; }
.qc-col { transition: opacity .2s; }
.qc-col.future { filter: grayscale(1); } .qc-col.future .qc-box, .qc-col.future .qc-meter { stroke-dasharray: 3 2; }
.qc-col.now .qc-box, .qc-col.now .qc-meter { stroke: var(--av-hl); stroke-width: 2.6; fill: var(--av-hl-fill); }
.qc-col.now .qc-dot { fill: var(--av-hl); }
.qc-col.now .qc-line, .qc-col.now .qc-plus { stroke: var(--av-hl); }
.qc-box, .qc-meter { fill: var(--av-surface); stroke: var(--av-stroke); stroke-width: 1.6; transition: fill .2s, stroke .2s; }
.qc-box.oracle { fill: var(--av-hl-fill); }
.qc-col:not(.now) .qc-box.oracle { fill: color-mix(in srgb, var(--av-hot) 10%, var(--av-surface)); stroke: var(--av-hot); }
.qc-glyph { font-size: 14px; font-weight: 600; fill: var(--av-ink); text-anchor: middle; dominant-baseline: central; pointer-events: none; }
.qc-glyph.small { font-size: 11px; font-weight: 500; }
.qc-dot { fill: var(--av-ink); }
.qc-line { stroke: var(--av-ink); stroke-width: 1.6; }
.qc-plus { fill: var(--av-surface); stroke: var(--av-ink); stroke-width: 1.6; }
.qc-arc { fill: none; stroke: var(--av-ink); stroke-width: 1.4; }
.qc-out { font-size: 13px; font-weight: 700; dominant-baseline: central; fill: var(--av-sol); }
.qc-axis { stroke: var(--av-stroke); stroke-width: 1; }
.qc-grid { stroke: var(--av-line); stroke-width: 1; stroke-dasharray: 2 4; }
.qc-tick { font-size: 10.5px; fill: var(--av-muted); text-anchor: end; dominant-baseline: central; }
.qc-blabel { font-size: 11px; fill: var(--av-muted); text-anchor: middle; }
.qc-blabel.win { fill: var(--av-sol); font-weight: 700; }
.qc-bar { transition: height .25s, y .25s, fill .2s; }
.qc-bar.pos { fill: var(--av-g0s); }
.qc-bar.neg { fill: var(--av-g1s); }
.qc-bar.plain { fill: var(--av-stroke); }
.qc-bar.win { fill: var(--av-sol); }
.qc-btitle { font-size: 12px; fill: var(--av-ink); font-weight: 600; }
.qc-note { font-size: 12px; fill: var(--av-ink); }
.qc-note.muted { fill: var(--av-muted); }
.qc-empty { font-size: 13px; fill: var(--av-muted); }
.qc-wire-g.faint { filter: grayscale(1); }
@media (prefers-reduced-motion: reduce) { .qc-col, .qc-bar, .qc-box { transition: none; } }`;

  /* ---------- statevector simulator: qubit k = bit (Q-1-k) of the basis index (q0 is the leftmost bit) ---------- */
  function State(Q) {
    const n = 1 << Q;
    return { Q, n, re: new Float64Array(n).fill(0).map((_, i) => (i === 0 ? 1 : 0)), im: new Float64Array(n) };
  }
  const bit = (st, k) => 1 << (st.Q - 1 - k);
  function opH(k) {
    return st => {
      const b = bit(st, k), s = Math.SQRT1_2;
      for (let i = 0; i < st.n; i++) if (!(i & b)) {
        const j = i | b, ar = st.re[i], ai = st.im[i], br = st.re[j], bi = st.im[j];
        st.re[i] = (ar + br) * s; st.im[i] = (ai + bi) * s; st.re[j] = (ar - br) * s; st.im[j] = (ai - bi) * s;
      }
    };
  }
  function opX(k) {
    return st => {
      const b = bit(st, k);
      for (let i = 0; i < st.n; i++) if (!(i & b)) {
        const j = i | b; let t = st.re[i]; st.re[i] = st.re[j]; st.re[j] = t; t = st.im[i]; st.im[i] = st.im[j]; st.im[j] = t;
      }
    };
  }
  function opCX(c, k) {
    return st => {
      const bc = bit(st, c), b = bit(st, k);
      for (let i = 0; i < st.n; i++) if ((i & bc) && !(i & b)) {
        const j = i | b; let t = st.re[i]; st.re[i] = st.re[j]; st.re[j] = t; t = st.im[i]; st.im[i] = st.im[j]; st.im[j] = t;
      }
    };
  }
  // A permutation with optional sign: basis i → f(i) = {j, s}
  function opPerm(f) {
    return st => {
      const re = new Float64Array(st.n), im = new Float64Array(st.n);
      for (let i = 0; i < st.n; i++) {
        if (Math.abs(st.re[i]) < EPS && Math.abs(st.im[i]) < EPS) continue;
        const { j, s } = f(i);
        re[j] += st.re[i] * s; im[j] += st.im[i] * s;
      }
      st.re = re; st.im = im;
    };
  }
  // Grover diffusion on qubits [0, r): amplitude → 2·mean − amplitude, per value of the remaining qubits
  function opDiffuse(r) {
    return st => {
      const shift = st.Q - r, R = 1 << r, rest = 1 << shift;
      for (let low = 0; low < rest; low++) {
        let mr = 0, mi = 0;
        for (let x = 0; x < R; x++) { mr += st.re[(x << shift) | low]; mi += st.im[(x << shift) | low]; }
        mr /= R; mi /= R;
        for (let x = 0; x < R; x++) { const i = (x << shift) | low; st.re[i] = 2 * mr - st.re[i]; st.im[i] = 2 * mi - st.im[i]; }
      }
    };
  }
  // Inverse QFT on qubits [0, t), done as a direct inverse DFT (small registers only)
  function opIQFT(t) {
    return st => {
      const shift = st.Q - t, T = 1 << t, rest = 1 << shift, norm = 1 / Math.sqrt(T);
      const cos = new Float64Array(T), sin = new Float64Array(T);
      for (let m = 0; m < T; m++) { cos[m] = Math.cos((2 * Math.PI * m) / T); sin[m] = Math.sin((2 * Math.PI * m) / T); }
      const vr = new Float64Array(T), vi = new Float64Array(T);
      for (let low = 0; low < rest; low++) {
        let any = false;
        for (let x = 0; x < T; x++) { const i = (x << shift) | low; vr[x] = st.re[i]; vi[x] = st.im[i]; if (vr[x] || vi[x]) any = true; }
        if (!any) continue;
        for (let k = 0; k < T; k++) {
          let sr = 0, si = 0;
          for (let x = 0; x < T; x++) {
            if (!vr[x] && !vi[x]) continue;
            const m = (x * k) % T, c = cos[m], s = -sin[m];
            sr += vr[x] * c - vi[x] * s; si += vr[x] * s + vi[x] * c;
          }
          const i = (k << shift) | low; st.re[i] = sr * norm; st.im[i] = si * norm;
        }
      }
    };
  }
  const copy = st => ({ Q: st.Q, n: st.n, re: Float64Array.from(st.re), im: Float64Array.from(st.im) });

  // Probability of each value of register R (qubit indices, first = most significant), plus amplitude signs.
  function distribution(st, R) {
    const r = R.length, size = 1 << r, probs = new Float64Array(size), others = [];
    for (let q = 0; q < st.Q; q++) if (!R.includes(q)) others.push(q);
    const regOf = i => R.reduce((v, q) => (v << 1) | ((i >> (st.Q - 1 - q)) & 1), 0);
    const restOf = i => others.reduce((v, q) => (v << 1) | ((i >> (st.Q - 1 - q)) & 1), 0);
    const restW = new Map();
    let real = true;
    for (let i = 0; i < st.n; i++) {
      const p = st.re[i] * st.re[i] + st.im[i] * st.im[i];
      if (p < EPS) continue;
      if (Math.abs(st.im[i]) > 1e-9) real = false;
      probs[regOf(i)] += p;
      const rk = restOf(i); restW.set(rk, (restW.get(rk) || 0) + p);
    }
    let best = 0, bw = -1;
    for (const [k, w] of restW) if (w > bw) { bw = w; best = k; }
    const signs = real ? new Int8Array(size) : null;
    if (real) for (let i = 0; i < st.n; i++) {
      const p = st.re[i] * st.re[i];
      if (p < EPS || restOf(i) !== best) continue;
      signs[regOf(i)] = st.re[i] >= 0 ? 1 : -1;
    }
    return { probs: Array.from(probs), signs: signs ? Array.from(signs) : null, r };
  }

  /* ---------- parsing ---------- */
  function parseList(str, what) {
    const s = str.replace(/\s+/g, "");
    const m = /^\(([^()]*)\)$/.exec(s);
    if (!m || !m[1]) throw new Error(`Expected a list like (0,1,0,1) for ${what}.`);
    const vals = m[1].split(",").map(Number);
    if (vals.some(v => !Number.isInteger(v) || v < 0)) throw new Error("Every entry must be a whole number, 0 or more.");
    return vals;
  }
  const log2 = n => { let k = 0; while ((1 << k) < n) k++; return (1 << k) === n ? k : -1; };
  function tableBits(vals, min, max) {
    const n = log2(vals.length);
    if (n < 0) throw new Error(`The table's length must be a power of two (2, 4, 8, …); got ${vals.length}.`);
    if (n < min) throw new Error(`The table needs at least ${1 << min} entries; got ${vals.length}.`);
    if (n > max) throw new Error(`This mockup simulates up to ${1 << max} table entries in the browser; got ${vals.length}.`);
    return n;
  }
  const bin = (v, n) => v.toString(2).padStart(n, "0");
  const dot = (a, b) => { let x = a & b, c = 0; while (x) { c ^= x & 1; x >>= 1; } return c; };
  const gcd = (a, b) => { while (b) [a, b] = [b, a % b]; return a; };
  const modpow = (a, e, m) => { let r = 1 % m; a %= m; while (e > 0) { if (e & 1) r = (r * a) % m; a = (a * a) % m; e >>= 1; } return r; };

  /* ---------- circuits. A circuit: {wires:[{name, role}], columns:[{gates, caption, stage?}], stages:[{label, from, to}], reg:[qubit idx], regName} ---------- */
  // |x>|y> → |x>|y ⊕ f(x)>, data on qubits [0,n), output on [n, n+m)
  const oracleXor = (n, m, f) => opPerm(i => { const x = i >> m, y = i & ((1 << m) - 1); return { j: (x << m) | (y ^ f(x)), s: 1 }; });

  function phaseKickback(n, f, oracleLabel) {
    const Q = n + 1, anc = n, all = [...Array(Q).keys()], data = [...Array(n).keys()];
    const wires = [...data.map(k => ({ name: `x${k}`, role: "query" })), { name: "y", role: "helper" }];
    const cols = [
      { gates: [{ type: "x", q: [anc] }], ops: [opX(anc)], caption: "Flip the helper qubit y to 1." },
      { gates: all.map(k => ({ type: "h", q: [k] })), ops: all.map(opH), caption: `Hadamard every qubit: the query qubits now hold all ${1 << n} inputs at once, and y becomes |−⟩.` },
      { gates: [{ type: "box", q: all, label: oracleLabel, oracle: true }], ops: [oracleXor(n, 1, f)], caption: "One call to the oracle. Because y is |−⟩, every input x with f(x) = 1 gets its sign flipped (phase kickback). Watch the bar colors." },
      { gates: data.map(k => ({ type: "h", q: [k] })), ops: data.map(opH), caption: "Hadamard the query qubits again. The signs interfere, and the answer piles up on one outcome." },
      { gates: data.map(k => ({ type: "measure", q: [k] })), ops: [], caption: "Measure the query qubits.", measure: true },
    ];
    return { Q, wires, columns: cols, reg: data, regName: data.map(k => `x${k}`).join(""),
      stages: [{ label: "prepare", from: 0, to: 1 }, { label: "query once", from: 2, to: 2 }, { label: "interfere", from: 3, to: 3 }, { label: "read", from: 4, to: 4 }] };
  }

  function simulate(circ) {
    let st = State(circ.Q);
    const snaps = [distribution(st, circ.reg)];
    const states = [copy(st)];
    circ.columns.forEach(c => { c.ops.forEach(op => op(st)); snaps.push(distribution(st, circ.reg)); states.push(copy(st)); });
    return { snaps, states };
  }
  // Continued-fraction convergents of k/T; returns the first denominator d ≤ N with a^d ≡ 1 (mod N), else null
  function periodFrom(k, T, a, N) {
    if (!k) return null;
    let [h1, h2, k1, k2] = [1, 0, 0, 1], num = k, den = T;
    while (den) {
      const q = Math.floor(num / den);
      [h1, h2] = [q * h1 + h2, h1]; [k1, k2] = [q * k1 + k2, k1];
      [num, den] = [den, num - q * den];
      if (k1 > N) break;
      if (k1 > 0 && modpow(a, k1, N) === 1) return k1;
    }
    return null;
  }
  const argmax = a => a.reduce((b, v, i) => (v > a[b] ? i : b), 0);

  /* ---------- the six problems ---------- */
  const PROBLEMS = {
    BERNSTEINVAZIRANI: {
      label: "Bernstein Vazirani", cls: "EQP",
      def: "The Bernstein-Vazirani problem asks for the identification of an unknown bit string s that defines a linear Boolean function f(x)= s*x (mod 2). The task is to determine the hidden string s using as few queries as possible",
      input: "(f(0), f(1), …, f(2ⁿ−1))", solvers: [["quantum", "Bernstein-Vazirani Algorithm"]], classical: "Bernstein-Vazirani Classical",
      examples: [["Redux default (s = 101)", "(0,1,0,1,1,0,1,0)"], ["Hidden string 111", "(0,1,1,0,1,0,0,1)"], ["Table that isn't linear", "(0,1,1,1,0,0,0,0)"]],
      build(I) {
        const vals = parseList(I, "f"); if (vals.some(v => v > 1)) throw new Error("Bernstein-Vazirani's table holds bits: every entry must be 0 or 1.");
        const n = tableBits(vals, 1, 4), f = x => vals[x];
        let s = 0; for (let k = 0; k < n; k++) if (f(1 << (n - 1 - k))) s |= 1 << (n - 1 - k);
        const linear = vals.every((v, x) => v === dot(s, x));
        const circ = phaseKickback(n, f, "U_f");
        return { circ, n, s, linear, answerOf: o => bin(o, n) };
      },
      verdict(B, out) {
        const got = bin(out, B.n), want = bin(B.s, B.n);
        if (!B.linear) return { ok: false, answer: got, caption: `Measured ${got}. This table isn't s·x for any s, so the promise is broken and the outcome is spread out. The answer means nothing here.` };
        return { ok: got === want, answer: got, caption: `Measured ${got}, which is the hidden string s, after a single oracle query.` };
      },
      checks(B, f, last) {
        const out = last.outcome;
        return [
          { label: `Measured string: ${f.done ? bin(out, B.n) : "not yet"}`, ok: null },
          { label: B.linear ? `Matches the hidden string ${bin(B.s, B.n)}` : "Table is linear (s·x mod 2): no", ok: f.done ? (B.linear && out === B.s) : B.linear ? null : false },
          { label: `Oracle queries: 1 (Bernstein-Vazirani Classical: ${B.n})`, ok: null },
        ];
      },
    },
    DEUTSCH: {
      label: "Deutsch", cls: "EQP",
      def: "Deutsch's algorithm determines whether a given function f: {0,1} -> {0,1} is constant or balanced. The problem has four possible input functions and is represented to the ordered list of outputs, i.e. (f(0), f(1)).",
      input: "(f(0), f(1))", solvers: [["quantum", "Deutsch's Algorithm"]], classical: "Deutsch Classical",
      examples: [["Redux default (balanced)", "(0,1)"], ["Constant", "(1,1)"]],
      build(I) {
        const vals = parseList(I, "f");
        if (vals.length !== 2 || vals.some(v => v > 1)) throw new Error("Deutsch takes exactly two bits: (f(0), f(1)).");
        return { circ: phaseKickback(1, x => vals[x], "U_f"), n: 1, balanced: vals[0] !== vals[1], answerOf: o => (o ? "balanced" : "constant") };
      },
      verdict(B, out) {
        const ans = out ? "balanced" : "constant";
        return { ok: ans === (B.balanced ? "balanced" : "constant"), answer: ans, caption: `Measured ${out}, so f is ${ans}. One oracle query instead of two.` };
      },
      checks(B, f, last) {
        return [
          { label: `Measured x0: ${f.done ? last.outcome : "not yet"}`, ok: null },
          { label: `Answer: ${f.done ? (last.outcome ? "balanced" : "constant") : "not yet"} (f is ${B.balanced ? "balanced" : "constant"})`, ok: f.done ? (!!last.outcome === B.balanced) : null },
          { label: "Oracle queries: 1 (Deutsch Classical: 2)", ok: null },
        ];
      },
    },
    DEUTSCHJOZSA: {
      label: "Deutsch Jozsa", cls: "EQP",
      def: "Deutsch-Jozsa's algorithm solves the general case of the parity problem and therefore determines whether a function f: {0,1}^n -> {0,1} is constant or balanced. It is represented by an ordered list of values, which show the functions output for the 2^n possible inputs.",
      input: "(f(0), …, f(2ⁿ−1))", solvers: [["quantum", "Deutsch-Jozsa Algorithm"]], classical: "Deutsch-Jozsa Classical",
      examples: [["Redux default (constant)", "(1,1,1,1)"], ["Balanced, 3 bits", "(0,1,1,0,1,0,0,1)"], ["Neither (promise broken)", "(1,0,0,0)"]],
      build(I) {
        const vals = parseList(I, "f"); if (vals.some(v => v > 1)) throw new Error("Every entry must be 0 or 1.");
        const n = tableBits(vals, 1, 4), ones = vals.filter(v => v).length;
        const kind = ones === 0 || ones === vals.length ? "constant" : ones * 2 === vals.length ? "balanced" : "neither";
        return { circ: phaseKickback(n, x => vals[x], "U_f"), n, kind, answerOf: o => (o === 0 ? "constant" : "balanced") };
      },
      verdict(B, out) {
        const ans = out === 0 ? "constant" : "balanced";
        if (B.kind === "neither") return { ok: false, answer: ans, caption: `Measured ${bin(out, B.n)}. But f is neither constant nor balanced, so the promise is broken and the reading means nothing.` };
        return { ok: ans === B.kind, answer: ans, caption: out === 0 ? `Measured all zeros (${bin(0, B.n)}), so f is constant.` : `Measured ${bin(out, B.n)}, not all zeros, so f is balanced. The all-zero outcome has probability 0.` };
      },
      checks(B, f, last) {
        return [
          { label: `All zeros measured: ${f.done ? (last.outcome === 0 ? "yes" : "no") : "not yet"}`, ok: null },
          { label: `Answer matches f (${B.kind})`, ok: f.done ? (B.kind !== "neither" && (last.outcome === 0) === (B.kind === "constant")) : B.kind === "neither" ? false : null },
          { label: `Oracle queries: 1 (classical worst case: ${(1 << (B.n - 1)) + 1})`, ok: null },
        ];
      },
    },
    SIMON: {
      label: "Simon's Problem", cls: "BQP",
      def: "Simon's problem is defined by a black-box function f: {0,1}^n -> {0,1}^m. For this function the following is promised: f(x) = f(y) if and only if x = y or x = y ⊕ s for some secret string s ∈ {0,1}^n. The goal is to find the string s",
      input: "(f(0), …, f(2ⁿ−1))", solvers: [["quantum", "Simon's algorithm (not in Redux yet)"]], classical: "Simon Classical",
      examples: [["Redux default (s = 010)", "(5,6,5,6,3,2,3,2)"], ["One-to-one (s = 00)", "(0,1,2,3)"]],
      build(I) {
        const vals = parseList(I, "f"), n = tableBits(vals, 1, 4);
        const maxv = Math.max(...vals), m = Math.max(1, Math.ceil(Math.log2(maxv + 1)));
        if (n + m > 9) throw new Error("This mockup simulates up to 9 qubits; the outputs need too many bits.");
        // the promise: f is 1-to-1, or exactly 2-to-1 with a single XOR mask
        let s = 0;
        const seen = new Map(); let ok = true, masks = new Set();
        vals.forEach((v, x) => { if (seen.has(v)) masks.add(seen.get(v) ^ x); else seen.set(v, x); });
        if (masks.size > 1 || (masks.size === 1 && [...seen.keys()].length * 2 !== vals.length)) ok = false;
        if (masks.size === 1) s = [...masks][0];
        const Q = n + m, data = [...Array(n).keys()], out = [...Array(m).keys()].map(k => n + k);
        const wires = [...data.map(k => ({ name: `x${k}`, role: "query" })), ...out.map((q, k) => ({ name: `f${k}`, role: "output" }))];
        const cols = [
          { gates: data.map(k => ({ type: "h", q: [k] })), ops: data.map(opH), caption: `Hadamard the query qubits: all ${1 << n} inputs at once.` },
          { gates: [{ type: "box", q: [...data, ...out], label: "U_f", oracle: true }], ops: [oracleXor(n, m, x => vals[x])], caption: "One oracle call writes f(x) into the output qubits. Inputs that share an output are now tied together." },
          { gates: out.map(k => ({ type: "measure", q: [k] })), ops: [], caption: "Measuring the output (optional) leaves the query qubits holding one pair {x, x ⊕ s}." },
          { gates: data.map(k => ({ type: "h", q: [k] })), ops: data.map(opH), caption: "Hadamard the query qubits again. Only outcomes y with y · s = 0 survive." },
          { gates: data.map(k => ({ type: "measure", q: [k] })), ops: [], caption: "Measure: every possible y satisfies y · s = 0 (mod 2).", measure: true },
        ];
        const circ = { Q, wires, columns: cols, reg: data, regName: data.map(k => `x${k}`).join(""),
          stages: [{ label: "prepare", from: 0, to: 0 }, { label: "query once", from: 1, to: 1 }, { label: "interfere", from: 2, to: 3 }, { label: "read", from: 4, to: 4 }] };
        return { circ, n, s, promise: ok, answerOf: o => bin(o, n) };
      },
      tail(B, snaps) {
        // Classical post-processing: collect independent outcomes, then solve y · s = 0.
        const probs = snaps[snaps.length - 1].probs, n = B.n, frames = [];
        const outcomes = probs.map((p, y) => [p, y]).filter(([p]) => p > 1e-9).map(([, y]) => y);
        const basis = [], picked = [];
        const reduce = v => { for (const b of basis) v = Math.min(v, v ^ b); return v; };
        for (const y of outcomes) { if (y === 0) continue; const r = reduce(y); if (r) { basis.push(r); basis.sort((a, b) => b - a); picked.push(y); frames.push({ outcomes: [...picked], caption: `Run the circuit again and get y = ${bin(y, n)}. It's independent of the earlier outcomes, so keep it.` }); } if (basis.length === n - (B.s ? 1 : 0)) break; }
        const sols = []; for (let c = 1; c < 1 << n; c++) if (picked.every(y => dot(y, c) === 0)) sols.push(c);
        const found = sols.length ? sols[0] : 0;
        return { frames, found, picked };
      },
      verdict(B, out, tail) {
        if (!B.promise) return { ok: false, answer: "?", caption: "This table breaks Simon's promise (not 1-to-1, and not 2-to-1 with one mask), so the outcomes don't determine any s." };
        const ans = bin(tail.found, B.n);
        return { ok: tail.found === B.s, answer: ans, caption: (tail.found ? `Solve y · s = 0 for every kept y (Gaussian elimination over bits). The only nonzero answer is s = ${ans}.` : `No nonzero s satisfies every kept y, so f is one-to-one and s = ${ans}.`) + ` That took ${tail.picked.length} circuit run${tail.picked.length === 1 ? "" : "s"}; a real run gets its outcomes at random and may need a few more.` };
      },
      checks(B, f, last) {
        return [
          { label: `Outcomes kept: ${(f.outcomes || []).map(y => bin(y, B.n)).join(", ") || "none yet"}`, ok: null },
          { label: `Secret string: ${f.done ? last.answer : "not yet"}${B.promise ? ` (true s = ${bin(B.s, B.n)})` : ""}`, ok: f.done ? last.ok : B.promise ? null : false },
          { label: `Simon Classical in Redux checks all ${1 << B.n} inputs`, ok: null },
        ];
      },
    },
    UNSTRUCTUREDSEARCH: {
      label: "Unstructured Search", cls: "BQP",
      def: "Input: a function f: Σⁿ → Σ. Output: a string x ∈ Σⁿ satisfying f(x) = 1, or \"no solution\" if no such string x exists.",
      input: "(f(0), …, f(2ⁿ−1))", solvers: [["quantum", "Grover's Algorithm"]], classical: "Unstructured Search Brute Force",
      examples: [["Redux default (4 items)", "(0,1,0,0)"], ["8 items, item 5 marked", "(0,0,0,0,0,1,0,0)"], ["16 items, item 11 marked", "(0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,0)"], ["Nothing marked", "(0,0,0,0)"]],
      build(I) {
        const vals = parseList(I, "f"), n = tableBits(vals, 2, 5), N = 1 << n;
        const marked = vals.map((v, i) => (v ? i : -1)).filter(i => i >= 0), M = marked.length;
        const iters = M === 0 || M === N ? 0 : Math.max(1, Math.floor((Math.PI / 4) * Math.sqrt(N / M)));
        const data = [...Array(n).keys()];
        const wires = data.map(k => ({ name: `x${k}`, role: "index" }));
        const cols = [{ gates: data.map(k => ({ type: "h", q: [k] })), ops: data.map(opH), caption: `Hadamard every qubit: all ${N} items are equally likely (${(100 / N).toFixed(1)}% each).` }];
        const stages = [{ label: "spread", from: 0, to: 0 }];
        const oracle = opPerm(i => ({ j: i, s: vals[i] ? -1 : 1 }));
        for (let t = 0; t < iters; t++) {
          cols.push({ gates: [{ type: "box", q: data, label: "Oracle", oracle: true }], ops: [oracle], caption: `Round ${t + 1}: the oracle flips the sign of the marked item${M > 1 ? "s" : ""}. Probabilities don't change yet; only the sign does.` });
          cols.push({ gates: [{ type: "box", q: data, label: "Diffuse" }], ops: [opDiffuse(n)], caption: `Round ${t + 1}: reflect every amplitude about the average. The marked item${M > 1 ? "s grow" : " grows"} and the rest shrink.` });
          stages.push({ label: `round ${t + 1}`, from: cols.length - 2, to: cols.length - 1 });
        }
        cols.push({ gates: data.map(k => ({ type: "measure", q: [k] })), ops: [], caption: "Measure.", measure: true });
        stages.push({ label: "read", from: cols.length - 1, to: cols.length - 1 });
        return { circ: { Q: n, wires, columns: cols, reg: data, regName: data.map(k => `x${k}`).join(""), stages }, n, N, marked, iters, answerOf: o => String(o) };
      },
      verdict(B, out, tail, probs) {
        if (!B.marked.length) return { ok: true, answer: "no solution", caption: "Nothing is marked, so there's nothing to amplify. Every outcome stays equally likely, and checking the measured item shows f = 0: no solution." };
        return { ok: B.marked.includes(out), answer: String(out), caption: `Measure item ${out} (${bin(out, B.n)}) with probability ${(probs[out] * 100).toFixed(1)}%, after ${B.iters} oracle quer${B.iters === 1 ? "y" : "ies"}.` };
      },
      checks(B, f, last, probs) {
        const pm = B.marked.reduce((a, i) => a + (probs[i] || 0), 0);
        return [
          B.marked.length ? { label: `Chance of a marked item now: ${(pm * 100).toFixed(1)}%`, ok: f.done ? pm > 0.5 : null } : { label: "Nothing is marked: the right answer is no solution", ok: f.done ? true : null },
          { label: `Rounds: ${B.iters} (about π/4 · √(N/M))`, ok: null },
          { label: `Oracle queries: ${B.iters} (Brute Force worst case: ${B.N})`, ok: null },
        ];
      },
    },
    PRIMEFACTOR: {
      label: "Prime Factorization", cls: "NP",
      def: "The prime factorization algorithm solves the decomposition of a positive integer into a product of prime integers.",
      input: "N, a positive integer", solvers: [["quantum", "Shor's Algorithm"]], classical: "Prime Factorization Trial Division",
      examples: [["Redux default (12)", "12"], ["15", "15"], ["21", "21"], ["A prime (13)", "13"]],
      build(I) {
        const s = I.trim();
        if (!/^\d+$/.test(s)) throw new Error("Enter one whole number, like 15.");
        const N = Number(s);
        if (N < 2) throw new Error("Enter a whole number of at least 2.");
        if (N > 1000000) throw new Error("This mockup factors numbers up to 1,000,000.");
        // Classical outer loop; the first odd composite that isn't a prime power gets the quantum period-finding run.
        const pre = [], parts = [N], factors = [];
        let quantum = null;
        const isPrime = x => { if (x < 2) return false; for (let d = 2; d * d <= x; d++) if (x % d === 0) return false; return true; };
        while (parts.length) {
          const x = parts.shift();
          if (isPrime(x)) { factors.push(x); pre.push(`${x} is prime.`); continue; }
          if (x % 2 === 0) { pre.push(`${x} is even: split off 2, leaving ${x / 2}. No quantum step needed.`); factors.push(2); parts.unshift(x / 2); continue; }
          let pp = null;
          for (let k = 2; (1 << k) <= x; k++) { const b = Math.round(Math.pow(x, 1 / k)); for (const c of [b - 1, b, b + 1]) if (c > 1 && Math.pow(c, k) === x) pp = [c, k]; }
          if (pp) { pre.push(`${x} = ${pp[0]}^${pp[1]}, a prime power: checked classically.`); for (let i = 0; i < pp[1]; i++) parts.unshift(pp[0]); continue; }
          if (quantum) { pre.push(`${x} is left over; the mockup splits it by trial division (Redux would run Shor's circuit again).`); let d = 3; while (x % d) d += 2; parts.unshift(d, x / d); continue; }
          // pick a: smallest base with an even order r and a^(r/2) ≢ −1 (real runs pick a at random)
          let a = null, r = null, tried = [];
          for (let c = 2; c < x; c++) {
            if (gcd(c, x) > 1) { tried.push(`a = ${c} shares a factor with ${x} (lucky; a real run picks a at random)`); continue; }
            let k = 1; while (modpow(c, k, x) !== 1) k++;
            if (k % 2 === 1 || modpow(c, k / 2, x) === x - 1) { tried.push(`a = ${c} has period ${k}, which doesn't help`); continue; }
            a = c; r = k; break;
          }
          const nb = Math.ceil(Math.log2(x + 1)), t = 2 * nb;
          quantum = { N: x, a, r, nb, t, tried, fits: t + nb <= 15 };
          const p = gcd(modpow(a, r / 2, x) - 1, x), q = x / p;
          quantum.p = p; quantum.q = q;
          parts.unshift(p, q);
        }
        factors.sort((u, v) => u - v);
        const B = { N, pre, factors, quantum, answerOf: () => "(" + factors.join(",") + ")" };
        if (quantum && quantum.fits) {
          const { N: X, a, nb, t } = quantum, Q = t + nb;
          const count = [...Array(t).keys()], work = [...Array(nb).keys()].map(k => t + k);
          const wires = [...count.map(k => ({ name: `c${k}`, role: "count" })), ...work.map((q, k) => ({ name: `w${k}`, role: "work" }))];
          const cols = [
            { gates: [{ type: "x", q: [work[work.length - 1]] }], ops: [opX(work[work.length - 1])], caption: "Set the work register to 1." },
            { gates: count.map(k => ({ type: "h", q: [k] })), ops: count.map(opH), caption: `Hadamard the ${t} counting qubits: every exponent 0 to ${(1 << t) - 1} at once.` },
          ];
          const mask = (1 << nb) - 1;
          for (let j = 0; j < t; j++) {
            const ctrl = t - 1 - j, mult = modpow(a, 1 << j, X);
            cols.push({ gates: [{ type: "box", q: work, label: `×${mult}`, ctrl }], ops: [opPerm(i => {
              const cbit = (i >> (Q - 1 - ctrl)) & 1, y = i & mask;
              if (!cbit || y >= X) return { j: i, s: 1 };
              return { j: (i & ~mask) | ((y * mult) % X), s: 1 };
            })], caption: `If c${ctrl} is 1, multiply the work register by ${a}^${1 << j} mod ${X} = ${mult}.${mult === 1 ? " That's ×1, so nothing happens." : ""}` });
          }
          cols.push({ gates: [{ type: "box", q: count, label: "QFT†" }], ops: [opIQFT(t)], caption: `Inverse quantum Fourier transform on the counting qubits: the period ${quantum.r} shows up as evenly spaced peaks.` });
          cols.push({ gates: count.map(k => ({ type: "measure", q: [k] })), ops: [], caption: "Measure the counting qubits.", measure: true });
          B.circ = { Q, wires, columns: cols, reg: count, regName: "counting register", decimal: true,
            stages: [{ label: "prepare", from: 0, to: 1 }, { label: `a^x mod ${X}`, from: 2, to: 1 + t }, { label: "find period", from: 2 + t, to: 2 + t }, { label: "read", from: 3 + t, to: 3 + t }] };
        }
        return B;
      },
      verdict(B, out) {
        return { ok: B.factors.reduce((a, b) => a * b, 1) === B.N, answer: "(" + B.factors.join(",") + ")", caption: `${B.N} = ${B.factors.join(" × ")}.` };
      },
      checks(B, f, last) {
        const q = B.quantum;
        return [
          { label: q ? `Period of ${q.a}^x mod ${q.N}: ${f.done || f.periodKnown ? q.r : "not found yet"}` : "Quantum step needed: no", ok: null },
          { label: `Factors: ${f.done ? B.factors.join(" × ") : "not yet"}`, ok: f.done ? last.ok : null },
          { label: !q ? "No circuit needed: classical checks did it all" : q.fits ? "Circuit simulated in your browser" : "Circuit too large to simulate here", ok: null },
        ];
      },
    },
  };

  /* ---------- solving: frames come from running the circuit one column at a time ---------- */
  function solve(key, str) {
    const P = PROBLEMS[key], B = P.build(str), frames = [];
    if (key === "PRIMEFACTOR") return shorFrames(B);
    const { snaps } = simulate(B.circ);
    frames.push({ col: 0, dist: snaps[0], caption: "Step 0: every qubit starts at |0⟩." });
    B.circ.columns.forEach((c, i) => frames.push({ col: i + 1, dist: snaps[i + 1], caption: c.caption }));
    const finalProbs = snaps[snaps.length - 1].probs, out = argmax(finalProbs);
    let tail = null;
    if (P.tail) { tail = P.tail(B, snaps); tail.frames.forEach(fr => frames.push({ col: B.circ.columns.length, dist: snaps[snaps.length - 1], classical: true, ...fr })); }
    const v = P.verdict(B, out, tail, finalProbs);
    frames.push({ col: B.circ.columns.length, dist: snaps[snaps.length - 1], done: true, ok: v.ok, outcome: key === "SIMON" ? null : out, answer: v.answer, caption: v.caption,
      outcomes: tail ? tail.picked : undefined });
    const last = frames[frames.length - 1];
    if (key !== "SIMON") frames[frames.length - 2].outcome = out;
    return { B, frames, last };
  }
  function shorFrames(B) {
    const frames = [], q = B.quantum;
    frames.push({ col: 0, caption: `Factor ${B.N}. Shor's algorithm only needs the quantum computer to find a period; the rest is classical.` });
    B.pre.filter(line => !q || !line.startsWith(`${q.N} =`)).forEach(line => frames.push({ col: 0, classical: true, caption: line }));
    if (q) {
      q.tried.forEach(t => frames.push({ col: 0, classical: true, caption: `Try ${t}.`, rejected: true }));
      frames.push({ col: 0, classical: true, caption: `Pick a = ${q.a}. Its period r (the smallest r with ${q.a}^r ≡ 1 mod ${q.N}) is what the circuit finds.` });
      if (B.circ) {
        const { snaps } = simulate(B.circ);
        B.circ.columns.forEach((c, i) => frames.push({ col: i + 1, dist: snaps[i + 1], caption: c.caption }));
        const T = 1 << q.t, probs = snaps[snaps.length - 1].probs, r = q.r;
        const peaks = probs.map((p, k) => [p, k]).filter(([p]) => p > 0.004).sort((u, v) => v[0] - u[0] || u[1] - v[1]).map(([, k]) => k);
        let m = null;
        for (const k of peaks) {
          const d = periodFrom(k, T, q.a, q.N), cand = k ? T / gcd(k, T) : null;
          if (d === r) { m = k; frames.push({ col: B.circ.columns.length, dist: snaps[snaps.length - 1], outcome: k, periodKnown: true, classical: true,
            caption: `Reading ${k}: ${k}/${T} ≈ ${(k / T).toFixed(4)}. Continued fractions give a fraction with denominator ${d}, and ${q.a}^${d} ≡ 1 mod ${q.N}, so the period is r = ${d}.` }); break; }
          frames.push({ col: B.circ.columns.length, dist: snaps[snaps.length - 1], outcome: k, classical: true, rejected: true,
            caption: k === 0 ? "Reading 0 says nothing about the period. Run the circuit again." : `Reading ${k}: ${k}/${T} ≈ ${(k / T).toFixed(4)} reduces to a fraction with denominator ${cand}, and ${q.a}^${cand} ≢ 1 mod ${q.N}. This run only gives part of r; run again.` });
        }
        frames.push({ col: B.circ.columns.length, dist: snaps[snaps.length - 1], outcome: m, periodKnown: true, classical: true,
          caption: `r = ${r} is even, so gcd(${q.a}^${r / 2} − 1, ${q.N}) = ${q.p} and gcd(${q.a}^${r / 2} + 1, ${q.N}) = ${q.q} are factors.` });
      } else {
        frames.push({ col: 0, classical: true, periodKnown: true, caption: `${q.N} needs ${q.t + q.nb} qubits, too many to simulate in the browser. Redux sends this to the quantum server. The period is r = ${q.r}, giving factors ${q.p} and ${q.q}.` });
      }
    }
    const v = PROBLEMS.PRIMEFACTOR.verdict(B);
    const lastDist = frames[frames.length - 1].dist;
    frames.push({ col: B.circ ? B.circ.columns.length : 0, dist: lastDist, outcome: frames[frames.length - 1].outcome, done: true, ok: v.ok, answer: v.answer, periodKnown: true, caption: v.caption });
    return { B, frames, last: frames[frames.length - 1] };
  }

  /* ---------- view ---------- */
  function create({ svg }) {
    if (!document.getElementById("qc-style")) {
      const st = document.createElement("style"); st.id = "qc-style"; st.textContent = css; document.head.appendChild(st);
    }
    svg.classList.add("qc-svg");
    let key, run, frame = null, hover = null, els;

    function draw() {
      svg.innerHTML = "";
      const circ = run.B.circ;
      if (!circ) {
        svg.setAttribute("viewBox", "0 0 680 120");
        const t = mk("text", { x: 24, y: 64, class: "qc-empty" }, svg);
        t.textContent = run.B.quantum ? "This number needs more qubits than the browser can simulate." : `No quantum circuit is needed for ${run.B.N}: the classical checks factor it completely.`;
        els = null;
        return;
      }
      const C = circ.columns.length, W = Math.max(680, LEFT + C * COLW + 90);
      const wy = k => TOP + k * WIREH, wiresBottom = wy(circ.wires.length - 1);
      const barTop = wiresBottom + 70, H = barTop + BARH + 64;
      svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
      const cx = c => LEFT + 24 + c * COLW + COLW / 2;
      els = { cols: [], wires: [], bars: [], labels: [], barTop, W, H };
      // stages
      const gS = mk("g", {}, svg);
      circ.stages.forEach(s => {
        const g = mk("g", { class: "qc-stage" }, gS), x0 = cx(s.from) - COLW / 2 + 3, x1 = cx(s.to) + COLW / 2 - 3;
        mk("rect", { x: x0, y: TOP - 34, width: x1 - x0, height: wiresBottom - TOP + 56, rx: 8 }, g);
        mk("text", { x: (x0 + x1) / 2, y: TOP - 40 }, g).textContent = s.label;
      });
      // wires
      circ.wires.forEach((w, k) => {
        const g = mk("g", { class: "qc-wire-g" }, svg);
        mk("line", { x1: LEFT, y1: wy(k), x2: cx(C - 1) + COLW / 2 + 40, y2: wy(k), class: "qc-wire" }, g);
        mk("text", { x: LEFT - 30, y: wy(k) - 6, class: "qc-wname" }, g).textContent = w.name;
        mk("text", { x: LEFT - 30, y: wy(k) + 9, class: "qc-wrole" }, g).textContent = w.role;
        mk("text", { x: LEFT - 6, y: wy(k), class: "qc-wname" }, g).textContent = "|0⟩";
        els.wires.push(g);
      });
      // gates, one group per column
      circ.columns.forEach((col, c) => {
        const g = mk("g", { class: "qc-col" }, svg), x = cx(c);
        col.gates.forEach(gt => {
          if (gt.type === "h" || gt.type === "x" || gt.type === "z") {
            const y = wy(gt.q[0]);
            mk("rect", { x: x - 14, y: y - 14, width: 28, height: 28, rx: 5, class: "qc-box" }, g);
            mk("text", { x, y, class: "qc-glyph" }, g).textContent = gt.type.toUpperCase();
          } else if (gt.type === "box") {
            const ys = gt.q.map(wy), y0 = Math.min(...ys) - 16, y1 = Math.max(...ys) + 16;
            if (gt.ctrl !== undefined) {
              const yc = wy(gt.ctrl);
              mk("line", { x1: x, y1: yc, x2: x, y2: yc < y0 ? y0 : y1, class: "qc-line" }, g);
              mk("circle", { cx: x, cy: yc, r: 5, class: "qc-dot" }, g);
            }
            const w = Math.max(40, gt.label.length * 8 + 14);
            mk("rect", { x: x - w / 2, y: y0, width: w, height: y1 - y0, rx: 6, class: "qc-box" + (gt.oracle ? " oracle" : "") }, g);
            mk("text", { x, y: (y0 + y1) / 2, class: "qc-glyph" + (gt.label.length > 4 ? " small" : "") }, g).textContent = gt.label;
          } else if (gt.type === "measure") {
            const y = wy(gt.q[0]);
            mk("rect", { x: x - 15, y: y - 14, width: 30, height: 28, rx: 5, class: "qc-meter" }, g);
            mk("path", { d: `M${x - 9} ${y + 6} A 10 10 0 0 1 ${x + 9} ${y + 6}`, class: "qc-arc" }, g);
            mk("line", { x1: x, y1: y + 6, x2: x + 7, y2: y - 8, class: "qc-line" }, g);
          }
        });
        els.cols.push(g);
      });
      // measured bits, written after the last column
      els.outs = circ.wires.map((w, k) => mk("text", { x: cx(C - 1) + COLW / 2 + 12, y: wy(k), class: "qc-out" }, svg));
      // probability chart
      const gB = mk("g", {}, svg);
      els.gB = gB;
      const left = LEFT, right = W - 30;
      els.chart = { left, right, top: barTop, h: BARH };
      els.btitle = mk("text", { x: left - 70, y: barTop - 26, class: "qc-btitle" }, gB);
      [0, 0.5, 1].forEach(v => {
        const y = barTop + BARH - v * BARH;
        mk("line", { x1: left, y1: y, x2: right, y2: y, class: v === 0 ? "qc-axis" : "qc-grid" }, gB);
        mk("text", { x: left - 8, y, class: "qc-tick" }, gB).textContent = v === 0 ? "0" : v === 1 ? "100%" : "50%";
      });
      els.barsG = mk("g", {}, gB);
      els.note = mk("text", { x: left, y: barTop + BARH + 46, class: "qc-note muted" }, gB);
    }

    function paintBars(f) {
      const circ = run.B.circ, { left, right, top, h } = els.chart;
      els.barsG.innerHTML = "";
      els.note.textContent = "";
      const d = f ? f.dist : null;
      if (!d) { els.btitle.textContent = `Outcome probabilities on the ${circ.regName} appear once you step through.`; return; }
      els.btitle.textContent = circ.decimal ? `Probability of each reading of the ${circ.regName}` : `Probability of each outcome on ${circ.regName}`;
      let idx = d.probs.map((p, i) => i);
      const sparse = idx.length > 32;
      if (sparse) idx = idx.filter(i => d.probs[i] > 0.004);
      if (sparse && !idx.length) { els.note.textContent = `Spread evenly over all ${d.probs.length} readings (each ${(100 / d.probs.length).toFixed(2)}%), too thin to draw.`; return; }
      const nBars = idx.length, slot = (right - left) / Math.max(nBars, 1), bw = Math.min(42, slot * 0.7);
      const win = f.done || f.outcome !== undefined && f.outcome !== null ? f.outcome : null;
      idx.forEach((i, k) => {
        const p = d.probs[i], x = left + slot * k + (slot - bw) / 2, bh = Math.max(p * h, p > 1e-9 ? 1.5 : 0);
        let cls = "qc-bar " + (d.signs ? (d.signs[i] < 0 ? "neg" : "pos") : "plain");
        if (win === i && (f.done || f.col === circ.columns.length)) cls = "qc-bar win";
        mk("rect", { x, y: top + h - bh, width: bw, height: bh, rx: 2, class: cls }, els.barsG);
        const lab = circ.decimal ? String(i) : bin(i, circ.reg.length);
        if (nBars <= 16 || k % Math.ceil(nBars / 16) === 0) {
          const t = mk("text", { x: x + bw / 2, y: top + h + 16, class: "qc-blabel" + (cls.includes("win") ? " win" : "") }, els.barsG);
          t.textContent = lab;
        }
        if (p > 0.02 && nBars <= 16) mk("text", { x: x + bw / 2, y: top + h - bh - 6, class: "qc-blabel" }, els.barsG).textContent = Math.round(p * 100) + "%";
      });
      if (d.signs) els.note.textContent = "Blue: positive amplitude. Orange: negative. Height: probability.";
      else if (circ.decimal) els.note.textContent = `Only readings above 0.4% are drawn. Peaks sit near multiples of ${1 << run.B.quantum.t}/r.`;
    }

    function paint() {
      if (!els) return;
      const circ = run.B.circ, f = frame, C = circ.columns.length;
      els.cols.forEach((g, c) => {
        let cls = "qc-col";
        if (f) { if (c >= f.col) cls += " future"; else if (c === f.col - 1 && !f.classical && !f.done) cls += " now"; }
        g.setAttribute("class", cls);
      });
      // measured values on the wires
      const measured = f && f.col === C && f.outcome !== undefined && f.outcome !== null;
      els.outs.forEach((t, k) => {
        const pos = circ.reg.indexOf(k);
        t.textContent = measured && pos >= 0 ? String((f.outcome >> (circ.reg.length - 1 - pos)) & 1) : "";
      });
      els.wires.forEach((g, k) => g.setAttribute("class", "qc-wire-g" + (hover !== null && hover !== k ? " faint" : "")));
      paintBars(f);
    }

    return {
      load(str, k) {
        key = k; run = solve(k, str); frame = null; hover = null;
        draw(); paint();
        return { frames: run.frames, ok: run.last.ok };
      },
      show(i) { frame = i === null ? null : run.frames[i]; paint(); return frame; },
      checks(i) {
        const f = i === null ? null : run.frames[i];
        const probs = f && f.dist ? f.dist.probs : run.frames[run.frames.length - 1].dist ? run.frames[run.frames.length - 1].dist.probs : [];
        return PROBLEMS[key].checks(run.B, f || {}, run.last, probs).map(c => (f ? c : { ...c, ok: null }));
      },
      chosen(i) {
        const f = i === null ? null : run.frames[i];
        if (!f || !f.done) return [];
        return [{ text: f.answer }];
      },
      resetHover() { hover = null; paint(); },
    };
  }

  return { create, parse: (str, key) => PROBLEMS[key].build(str), PROBLEMS, _solve: solve };
})();
