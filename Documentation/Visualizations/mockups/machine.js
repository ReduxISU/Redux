/* ---- Tape & stack machines: Turing machine, linear bounded automaton, pushdown automaton (not in Redux yet) ----
   The state diagram follows automaton.js's look (layers from the start state, start arrow, double-ring accept,
   merged transition labels). Under it: the tape (head, written cell) and, for a PDA, one stack column per branch.
   Frames are in the machine's own terms: state, tape and head, or configurations (state, input read, stack).
   Pages supply --av-* tokens; state names follow the shared vocabulary. */
const MachineView = (() => {
  const NS = "http://www.w3.org/2000/svg";
  const mk = (tag, attrs = {}, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; };
  const R = 26, DX = 170, DY = 120, SEP = "\u0000", BLANK = "_", LM = "<", RM = ">";
  const EPS = new Set(["ε", "eps", "epsilon"]);
  const CW = 34, CHT = 36, SCELL = 24, SW = 54, MAX_BRANCHES = 6, MAX_STACK = 8, RUN_LIMIT = 40;

  const css = `
.mc-svg { width: 100%; height: auto; display: block; }
.mc-svg text { font-family: var(--av-mono); }
.mc-node { cursor: pointer; transition: opacity .2s; }
.mc-node:focus { outline: none; }
.mc-node .body { fill: var(--av-bg); stroke: var(--av-stroke); stroke-width: 2; transition: fill .2s, stroke .2s; }
.mc-node .inner { fill: none; stroke: var(--av-stroke); stroke-width: 1.6; }
.mc-node text { font-size: 14px; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); pointer-events: none; }
.mc-node.Active .body { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; }
.mc-node.Active .inner { stroke: var(--av-hl); }
.mc-node.Solution .body { fill: var(--av-sol); stroke: var(--av-sol); }
.mc-node.Solution .inner { stroke: #fff; } .mc-node.Solution text { fill: #fff; font-weight: 600; }
.mc-node.Rejected .body { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 3; }
.mc-node.Rejected .inner { stroke: var(--av-rej); } .mc-node.Rejected text { fill: var(--av-rej); }
.mc-node.Untraveled .body { fill: transparent; stroke-dasharray: 4 4; }
.mc-node.Untraveled .inner { stroke-dasharray: 4 4; } .mc-node.Untraveled text { fill: var(--av-muted); }
.mc-node.reject .body { stroke-dasharray: 2 3; }
.mc-node.trace .body, .mc-node:focus-visible .body { stroke: var(--av-hot); stroke-width: 3; }
.mc-node.faint { opacity: .3; }
.mc-edge { fill: none; stroke: var(--av-edge); stroke-width: 1.6; transition: opacity .2s, stroke .2s; }
.mc-edge.eps { stroke-dasharray: 5 4; }
.mc-edge.hot { stroke: var(--av-hl); stroke-width: 3; }
.mc-edge.trace { stroke: var(--av-hot); stroke-width: 2.4; }
.mc-edge.faint, .mc-elabel.faint { opacity: .15; }
.mc-elabel rect { fill: var(--av-surface); stroke: var(--av-line); }
.mc-elabel text { font-size: 12px; text-anchor: middle; dominant-baseline: central; fill: var(--av-muted); }
.mc-elabel text.on { fill: #1b1300; font-weight: 700; }
.mc-elabel.hot rect { fill: var(--av-hl-fill); stroke: var(--av-hl); }
.mc-elabel.trace rect { stroke: var(--av-hot); } .mc-elabel.trace text { fill: var(--av-hot); }
.mc-elabel rect.lineon { fill: var(--av-hl); stroke: none; }
.mc-start { stroke: var(--av-ink); stroke-width: 1.8; }
.mc-m-def { fill: var(--av-edge); } .mc-m-hot { fill: var(--av-hl); } .mc-m-trace { fill: var(--av-hot); } .mc-m-ink { fill: var(--av-ink); }
.mc-sect { font-size: 11px; font-weight: 600; letter-spacing: .08em; fill: var(--av-muted); }
.mc-cell rect { fill: var(--av-surface); stroke: var(--av-line); stroke-width: 1.5; transition: fill .2s, stroke .2s; }
.mc-cell text { font-size: 15px; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); }
.mc-cell.blank text { fill: var(--av-muted); }
.mc-cell.marker rect { fill: transparent; stroke-dasharray: 3 3; } .mc-cell.marker text { fill: var(--av-muted); }
.mc-cell.read rect { fill: transparent; } .mc-cell.read text { fill: var(--av-muted); }
.mc-cell.written rect { fill: var(--av-hl-fill); }
.mc-cell.written text { font-weight: 700; }
.mc-cell.head rect { stroke: var(--av-hl); stroke-width: 3; }
.mc-cell.end rect { stroke-dasharray: 4 3; fill: transparent; } .mc-cell.end text { font-size: 11px; fill: var(--av-muted); }
.mc-cell.end.acc rect { stroke: var(--av-sol); stroke-dasharray: none; } .mc-cell.end.acc text { fill: var(--av-sol); }
.mc-cell.end.rej rect { stroke: var(--av-rej); stroke-dasharray: none; } .mc-cell.end.rej text { fill: var(--av-rej); }
.mc-headmark { fill: var(--av-hl); }
.mc-headlbl { font-size: 12px; font-weight: 600; text-anchor: middle; fill: var(--av-ink); }
.mc-gap { font-size: 12px; fill: var(--av-muted); text-anchor: middle; dominant-baseline: central; }
.mc-stack rect { fill: var(--av-surface); stroke: var(--av-line); stroke-width: 1.5; }
.mc-stack text { font-size: 13px; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); }
.mc-stack .top rect { stroke: var(--av-hl); stroke-width: 2.6; fill: var(--av-hl-fill); }
.mc-stack .top text { font-weight: 700; }
.mc-stack .floor { stroke: var(--av-ink); stroke-width: 2; }
.mc-stack .slbl { font-size: 11.5px; font-weight: 600; fill: var(--av-ink); }
.mc-stack.acc .slbl { fill: var(--av-sol); } .mc-stack.rej .slbl { fill: var(--av-rej); }
.mc-stack .more { font-size: 11px; fill: var(--av-muted); }
@media (prefers-reduced-motion: reduce) { .mc-node, .mc-node .body, .mc-edge, .mc-cell rect { transition: none; } }`;

  /* ---------- parsing: Redux-style tuples, like DFA/NFA's ((N,A,E,S,F),I) ---------- */
  function tokenize(s) {
    const out = []; let buf = "";
    for (const ch of s) {
      if ("{}(),".includes(ch)) { if (buf.trim()) out.push(buf.trim()); buf = ""; out.push(ch); }
      else buf += ch;
    }
    if (buf.trim()) out.push(buf.trim());
    return out;
  }
  function tree(tokens) {
    let i = 0;
    function val() {
      const t = tokens[i];
      if (t === "{" || t === "(") {
        i++;
        const close = t === "{" ? "}" : ")", node = { t: t === "{" ? "set" : "tup", items: [] };
        if (tokens[i] === close) { i++; return node; }
        for (;;) {
          node.items.push(val());
          if (tokens[i] === ",") { i++; continue; }
          if (tokens[i] === close) { i++; return node; }
          throw new Error("Brackets don't match up.");
        }
      }
      if (t === undefined || "}),".includes(t)) throw new Error(t === undefined ? "The instance ends too early." : `Unexpected "${t}".`);
      i++; return t;
    }
    const v = val();
    if (i !== tokens.length) throw new Error("Extra text after the instance.");
    return v;
  }
  const atoms = (node, name) => {
    if (!node || node.t !== "set") throw new Error(`${name} must be a set, like {a,b}.`);
    return node.items.map(x => { if (typeof x !== "string") throw new Error(`${name} must list plain names.`); return x; });
  };
  const atom = (node, name) => { if (typeof node !== "string") throw new Error(`${name} must be a single name.`); return node; };
  const oneChar = (list, name) => list.forEach(s => { if ([...s].length !== 1) throw new Error(`${name} symbols must be single characters ("${s}" isn't).`); });

  function parse(str, key) {
    const P = PROBLEMS[key];
    const top = tree(tokenize(str));
    if (top.t !== "tup" || top.items.length !== 2 || !top.items[0] || top.items[0].t !== "tup")
      throw new Error(`Expected ${P.input}.`);
    const M = top.items[0].items, wRaw = atom(top.items[1], "The input");
    const input = EPS.has(wRaw) ? [] : [...wRaw];
    if (M.length !== 7) throw new Error(`The machine needs 7 parts: ${P.parts}.`);
    const Q = atoms(M[0], "Q"), S = atoms(M[1], "Σ"), G = atoms(M[2], "Γ");
    if (!Q.length || new Set(Q).size !== Q.length) throw new Error("Q must be a non-empty set of states with no repeats.");
    oneChar(S, "Σ"); oneChar(G, "Γ");
    const QS = new Set(Q);
    const q0 = atom(M[4], "The start state");
    if (!QS.has(q0)) throw new Error(`Start state ${q0} isn't in Q.`);
    if (!M[3] || M[3].t !== "set") throw new Error("δ must be a set of rules, like {(q0,a,q1,X,R), …}.");
    const rules = M[3].items.map(r => {
      if (!r || r.t !== "tup" || r.items.length !== 5 || r.items.some(x => typeof x !== "string")) throw new Error(`Each rule in δ has 5 parts: ${P.rule}.`);
      return r.items;
    });
    for (const ch of input) if (!S.includes(ch)) throw new Error(`Input symbol "${ch}" isn't in Σ.`);

    if (P.kind === "tm") {
      const Gs = new Set(G);
      if (P.bounded) { Gs.add(LM); Gs.add(RM); if (S.includes(LM) || S.includes(RM)) throw new Error(`Σ can't use the end markers ${LM} and ${RM}.`); }
      else { if (!Gs.has(BLANK)) throw new Error(`Γ must include the blank symbol ${BLANK}.`); if (S.includes(BLANK)) throw new Error(`Σ can't include the blank ${BLANK}.`); }
      for (const s of S) if (!Gs.has(s)) throw new Error(`Σ must be part of Γ ("${s}" is missing from Γ).`);
      const qa = atom(M[5], "The accept state"), qr = atom(M[6], "The reject state");
      if (!QS.has(qa) || !QS.has(qr)) throw new Error("The accept and reject states must be in Q.");
      if (qa === qr) throw new Error("The accept and reject states must be different.");
      const delta = new Map(), trans = [];
      for (const [q, a, p, b, D] of rules) {
        if (!QS.has(q) || !QS.has(p)) throw new Error(`Rule (${q},${a},${p},${b},${D}) uses a state not in Q.`);
        if (!Gs.has(a) || !Gs.has(b)) throw new Error(`Rule (${q},${a},${p},${b},${D}) uses a symbol not in Γ.`);
        if (D !== "L" && D !== "R") throw new Error(`Rule (${q},${a},${p},${b},${D}) must move L or R.`);
        if (q === qa || q === qr) throw new Error(`${q} halts the machine, so it can't have rules.`);
        if (P.bounded && (a === LM || a === RM) && (b !== a || D !== (a === LM ? "R" : "L")))
          throw new Error(`Rule (${q},${a},${p},${b},${D}): an end marker must be written back and the head must move ${a === LM ? "right" : "left"}.`);
        const k = q + SEP + a;
        if (delta.has(k)) throw new Error(`Two rules for ${q} reading "${a}". A Turing machine is deterministic.`);
        const t = { q, a, p, b, D, i: trans.length };
        delta.set(k, t); trans.push(t);
      }
      return { kind: "tm", bounded: !!P.bounded, Q, S, G: [...Gs], q0, qa, qr, delta, trans, input };
    }
    // PDA: (q, a, X, p, γ): in q reading a (or ε), pop X, push the string γ (its first symbol on top)
    const Z0 = atom(M[5], "The start stack symbol"), F = atoms(M[6], "F");
    if (!G.includes(Z0)) throw new Error(`Start stack symbol ${Z0} isn't in Γ.`);
    for (const f of F) if (!QS.has(f)) throw new Error(`Accept state ${f} isn't in Q.`);
    const trans = rules.map(([q, a, X, p, g], i) => {
      const sym = EPS.has(a) ? "ε" : a, push = EPS.has(g) ? "" : g;
      if (!QS.has(q) || !QS.has(p)) throw new Error(`Rule (${q},${a},${X},${p},${g}) uses a state not in Q.`);
      if (sym !== "ε" && !S.includes(sym)) throw new Error(`Rule (${q},${a},${X},${p},${g}) reads "${a}", which isn't in Σ.`);
      if (!G.includes(X)) throw new Error(`Rule (${q},${a},${X},${p},${g}) pops "${X}", which isn't in Γ.`);
      for (const c of push) if (!G.includes(c)) throw new Error(`Rule (${q},${a},${X},${p},${g}) pushes "${c}", which isn't in Γ.`);
      return { q, a: sym, X, p, g: push, i };
    });
    return { kind: "pda", Q, S, G, q0, Z0, F: new Set(F), trans, input };
  }

  /* ---------- Turing machine / LBA simulation: one rule per frame ---------- */
  function runTM(M, limit) {
    const n = M.input.length;
    let off = 0, cells, head;
    if (M.bounded) { cells = [LM, ...M.input, RM]; head = 1; }
    else { cells = M.input.length ? [...M.input] : [BLANK]; head = 0; }
    const read = () => cells[head - off] ?? BLANK;
    const snap = () => ({ off, cells: cells.slice() });
    const frames = [];
    let state = M.q0, steps = 0, lo = 0, hi = Math.max(cells.length - 1, 0);
    const seen = new Map();
    const cfg = () => state + SEP + head + SEP + cells.join("");
    frames.push({ state, tape: snap(), head, steps: 0, caption: `Start in ${M.q0} with the head on the first ${M.bounded ? "input " : ""}cell.${n ? "" : " The input is empty."}` });
    if (M.bounded) seen.set(cfg(), 0);
    for (;;) {
      if (state === M.qa || state === M.qr) {
        const ok = state === M.qa;
        frames.push({ state, tape: snap(), head, steps, done: true, ok, verdict: ok ? "accept" : "reject", lo, hi,
          caption: ok ? `Halted in ${M.qa} after ${steps} step${steps === 1 ? "" : "s"}. Accepted.` : `Halted in ${M.qr} after ${steps} step${steps === 1 ? "" : "s"}. Rejected.` });
        return frames;
      }
      if (steps >= limit) {
        frames.push({ state, tape: snap(), head, steps, done: true, ok: false, verdict: "limit", lo, hi,
          caption: `Still running after ${limit} steps, so the simulation stops here. That isn't a "no": the machine might halt later. No simulator can always tell a slow machine from one that runs forever (the halting problem).` });
        return frames;
      }
      const sym = read(), t = M.delta.get(state + SEP + sym);
      if (!t) {
        frames.push({ state, tape: snap(), head, steps, done: true, ok: false, verdict: "stuck", stuck: state, lo, hi,
          caption: `No rule for ${state} reading "${sym}". By convention a missing rule sends the machine to ${M.qr}, so it halts and rejects.` });
        return frames;
      }
      const prev = head;
      cells[head - off] = t.b;
      let note = "";
      if (t.D === "R") {
        head++;
        if (head - off >= cells.length) cells.push(BLANK);
      } else {
        // the tape is infinite in both directions (as in JFLAP), so moving left past the first cell adds a blank
        head--;
        if (head < off) { cells.unshift(BLANK); off--; }
      }
      lo = Math.min(lo, head); hi = Math.max(hi, head);
      state = t.p; steps++;
      frames.push({ state, tape: snap(), head, prev, written: t.b, trans: [t], steps,
        caption: `${t.q} reads "${t.a}": write "${t.b}", move ${t.D === "L" ? "left" : "right"}, go to ${t.p}.${note}` });
      if (M.bounded && state !== M.qa && state !== M.qr) {
        const k = cfg();
        if (seen.has(k)) {
          frames.push({ state, tape: snap(), head, steps, done: true, ok: false, verdict: "loop", lo, hi,
            caption: `This exact configuration (state, head position and tape) already happened at step ${seen.get(k)}, so the machine will loop forever and never accept. An LBA has only finitely many configurations, so a simulator can always catch this. A general Turing machine can't be checked this way.` });
          return frames;
        }
        seen.set(k, steps);
      }
    }
  }

  /* ---------- PDA: configurations (state, stack); all branches per symbol, or one run per move ---------- */
  const ruleText = t => `${t.a}, ${t.X} → ${t.g || "ε"}`;
  function pdaMoves(M, c, pos) {
    const top = c.stack[0], out = [];
    if (top === undefined) return out;
    for (const t of M.trans) {
      if (t.q !== c.state || t.X !== top) continue;
      if (t.a === "ε") out.push({ t, read: false });
      else if (pos < M.input.length && t.a === M.input[pos]) out.push({ t, read: true });
    }
    return out;
  }
  const apply = (c, t) => ({ state: t.p, stack: t.g + c.stack.slice(1) });
  function pdaAll(M) {
    const n = M.input.length, cap = 2 * n + 8, frames = [];
    let pruned = false;
    const closure = list => {
      const res = new Map(), used = [], q = [];
      for (const c of list) { const k = c.state + SEP + c.stack; if (!res.has(k)) { res.set(k, c); q.push(c); } }
      while (q.length) {
        const c = q.shift();
        for (const m of pdaMoves(M, c, Infinity)) {
          if (m.read) continue;
          const d = apply(c, m.t);
          if (d.stack.length > cap) { pruned = true; continue; }
          used.push(m.t);
          const k = d.state + SEP + d.stack;
          if (!res.has(k)) { if (res.size >= 300) { pruned = true; continue; } res.set(k, d); q.push(d); }
        }
      }
      return { configs: [...res.values()], used };
    };
    let cur = closure([{ state: M.q0, stack: M.Z0 }]);
    frames.push({ pos: 0, configs: cur.configs, taken: cur.used,
      caption: `Start in ${M.q0} with ${M.Z0} on the stack.${cur.configs.length > 1 ? ` ε-moves already open ${cur.configs.length} branches.` : ""}` });
    let last = cur.configs;
    for (let i = 0; i < n; i++) {
      const sym = M.input[i], next = [], taken = [];
      for (const c of last) for (const m of pdaMoves(M, c, i)) if (m.read) { taken.push(m.t); next.push(apply(c, m.t)); }
      const cl = closure(next);
      cl.used.forEach(t => taken.push(t));
      const k = cl.configs.length;
      frames.push({ pos: i + 1, sym, configs: cl.configs, taken,
        caption: k ? `Read "${sym}". ${k} branch${k > 1 ? "es are" : " is"} still alive${cl.used.length ? " (including ε-moves)" : ""}.` : `Read "${sym}". No branch has a move for it with its stack top, so every branch dies.` });
      last = cl.configs;
      if (!k) break;
    }
    const finished = frames[frames.length - 1].pos === n && last.length > 0;
    const winners = finished ? last.filter(c => M.F.has(c.state)) : [];
    const ok = winners.length > 0;
    frames.push({ pos: frames[frames.length - 1].pos, configs: last, taken: [], done: true, ok, winners, pruned,
      visited: new Set(frames.flatMap(f => f.configs.map(c => c.state))),
      caption: !finished ? "Rejected. Every branch got stuck before the end of the input."
        : ok ? `Input finished. A branch is in accept state ${winners[0].state}, so the machine accepts (acceptance by final state).`
        : `Input finished, but no branch is in an accept state. Rejected.` });
    return frames;
  }
  function pdaRuns(M) {
    const n = M.input.length, cap = 2 * n + 8, found = [];
    function dfs(c, pos, moves, seen) {
      if (found.length >= RUN_LIMIT || moves.length > 4 * n + 20) return;
      const opts = pdaMoves(M, c, pos).filter(m => {
        const d = apply(c, m.t);
        return d.stack.length <= cap && !seen.has(d.state + SEP + (m.read ? pos + 1 : pos) + SEP + d.stack);
      });
      if (pos === n || !opts.some(m => m.read)) found.push({ moves: moves.slice(), end: c, pos });
      for (const m of opts) {
        const d = apply(c, m.t), np = m.read ? pos + 1 : pos, k = d.state + SEP + np + SEP + d.stack;
        seen.add(k); moves.push({ t: m.t, read: m.read, c: d, pos: np });
        dfs(d, np, moves, seen);
        moves.pop(); seen.delete(k);
      }
    }
    const c0 = { state: M.q0, stack: M.Z0 };
    dfs(c0, 0, [], new Set([M.q0 + SEP + 0 + SEP + M.Z0]));
    return found.map((r, ri) => {
      const frames = [{ pos: 0, configs: [c0], taken: [], caption: `Start in ${M.q0} with ${M.Z0} on the stack.` }];
      let prev = c0;
      for (const m of r.moves) {
        frames.push({ pos: m.pos, sym: m.read ? M.input[m.pos - 1] : undefined, configs: [m.c], taken: [m.t],
          caption: m.read ? `In ${prev.state}, read "${m.t.a}", pop ${m.t.X}, push ${m.t.g ? `"${m.t.g}"` : "nothing"}: now in ${m.t.p}.`
            : `ε-move from ${prev.state}: read nothing, pop ${m.t.X}, push ${m.t.g ? `"${m.t.g}"` : "nothing"}: now in ${m.t.p}.` });
        prev = m.c;
      }
      const finished = r.pos === n, ok = finished && M.F.has(r.end.state);
      frames.push({ pos: r.pos, configs: [r.end], taken: [], done: true, ok, winners: ok ? [r.end] : [],
        visited: new Set([M.q0, ...r.moves.map(m => m.t.p)]),
        caption: ok ? `This run reads the whole input and ends in ${r.end.state}, an accept state. Accepted.`
          : finished ? `This run reads the whole input but ends in ${r.end.state}, not an accept state. Rejected.`
          : `This run gets stuck in ${r.end.state}: no move with "${M.input[r.pos]}" next and ${r.end.stack[0] ?? "nothing"} on top. Rejected.` });
      const chain = [M.q0, ...r.moves.map(m => (m.read ? "→ " : "⇢ ") + m.t.p)].join(" ");
      return { label: `Run ${ri + 1}: ${chain}, ${ok ? "accepts" : finished ? "rejects" : `stuck at symbol ${r.pos + 1}`}`, ok, frames };
    });
  }

  /* ---------- view ---------- */
  function create({ svg }) {
    if (!document.getElementById("mc-style")) {
      const st = document.createElement("style"); st.id = "mc-style"; st.textContent = css; document.head.appendChild(st);
    }
    svg.classList.add("mc-svg");
    let M, Pk, all, runs, cur, lim = 0, frame = null, hover = null, els;

    const label = t => (M.kind === "tm" ? `${t.a} → ${t.b}, ${t.D}` : ruleText(t));

    function draw() {
      svg.innerHTML = "";
      const L = { [M.q0]: 0 }, q = [M.q0];
      while (q.length) { const s = q.shift(); for (const t of M.trans) if (t.q === s && !(t.p in L)) { L[t.p] = L[s] + 1; q.push(t.p); } }
      // a Turing machine's halting states get the last column, the usual way TMs are drawn
      const halt = new Set(M.kind === "tm" ? [M.qa, M.qr] : []);
      const others = M.Q.filter(s => !halt.has(s));
      const reach = Math.max(0, ...others.filter(s => s in L).map(s => L[s]));
      others.forEach(s => { if (!(s in L)) L[s] = reach + 1; });
      const lastCol = Math.max(0, ...others.map(s => L[s]));
      halt.forEach(s => { L[s] = lastCol + 1; });
      const cols = [];
      M.Q.forEach(s => (cols[L[s]] ||= []).push(s));
      const rows = Math.max(...cols.filter(Boolean).map(c => c.length));

      const groups = new Map();
      M.trans.forEach(t => {
        const k = t.q + SEP + t.p;
        if (!groups.has(k)) groups.set(k, { key: k, from: t.q, to: t.p, lines: [], ts: [] });
        const g = groups.get(k); g.lines.push(label(t)); g.ts.push(t);
      });
      const hasLoops = [...groups.values()].some(g => g.from === g.to);
      const loopLines = Math.max(1, ...[...groups.values()].filter(g => g.from === g.to).map(g => g.lines.length));
      const CWd = 110 + (cols.length - 1) * DX + 80, W = Math.max(CWd, 680), ox = (W - CWd) / 2;
      const P = {};
      cols.forEach((c, li) => c.forEach((s, i) => { P[s] = { x: ox + 110 + li * DX, y: (i - (c.length - 1) / 2) * DY }; }));
      const ys = Object.values(P).map(p => p.y), rowTop = Math.min(...ys), rowBot = Math.max(...ys);
      const loopTop = hasLoops ? rowTop - R - 60 - (loopLines - 1) * 15 : rowTop - R;

      // route every edge first, then shift the whole drawing so nothing is cut off.
      // Long forward edges arc over the loops; back edges (they close a cycle) arc under the rows.
      for (const g of groups.values()) {
        const a = P[g.from], b = P[g.to];
        g.h = g.lines.length * 15 + 6;
        if (g.from === g.to) {
          const a1 = -Math.PI / 2 - 0.45, a2 = -Math.PI / 2 + 0.45;
          const p0 = [a.x + R * Math.cos(a1), a.y + R * Math.sin(a1)], p1 = [a.x + R * Math.cos(a2), a.y + R * Math.sin(a2)];
          g.d = `M${p0} C${a.x - 42},${a.y - R - 60} ${a.x + 42},${a.y - R - 60} ${p1}`;
          g.lx = a.x; g.ly = a.y - R - 46 - (g.lines.length - 1) * 7.5;
          continue;
        }
        const span = L[g.to] - L[g.from], rev = groups.has(g.to + SEP + g.from);
        let cx, cy, curved = true;
        if (span >= 2) {
          const apex = loopTop - 16 - 16 * (span - 2);
          cx = (a.x + b.x) / 2; cy = 2 * apex - (a.y + b.y) / 2;
        } else if (span < 0 && !(rev && span === -1)) {
          const apex = rowBot + R + 30 + 16 * (-span - 1);
          cx = (a.x + b.x) / 2; cy = 2 * apex - (a.y + b.y) / 2;
        } else {
          const dx = b.x - a.x, dy = b.y - a.y, len = Math.hypot(dx, dy), nx = dy / len, ny = -dx / len;
          let off = rev ? 26 : 0;
          if (span === 0 && Math.abs(dy) > DY * 1.5) off = Math.max(off, 44);
          cx = (a.x + b.x) / 2 + nx * off * 2; cy = (a.y + b.y) / 2 + ny * off * 2; curved = off > 0;
        }
        const sl = Math.hypot(cx - a.x, cy - a.y), tl = Math.hypot(cx - b.x, cy - b.y);
        const s0 = [a.x + (cx - a.x) / sl * R, a.y + (cy - a.y) / sl * R];
        const t0 = [b.x + (cx - b.x) / tl * (R + 1), b.y + (cy - b.y) / tl * (R + 1)];
        g.d = curved ? `M${s0} Q${cx},${cy} ${t0}` : `M${s0} L${t0}`;
        g.lx = 0.25 * s0[0] + 0.5 * cx + 0.25 * t0[0]; g.ly = 0.25 * s0[1] + 0.5 * cy + 0.25 * t0[1];
      }
      const gs = [...groups.values()];
      const minTop = Math.min(rowTop - R, ...gs.map(g => g.ly - g.h / 2)), maxBot = Math.max(rowBot + R, ...gs.map(g => g.ly + g.h / 2));
      const shift = 34 - minTop, Hd = Math.max(250, maxBot - minTop + 60);

      // what sits under the diagram
      const tapeY = Hd + 34;
      let stackRows = 0;
      if (M.kind === "pda") {
        const allFrames = [...all, ...runs.flatMap(r => r.frames)];
        stackRows = Math.min(MAX_STACK, Math.max(1, ...allFrames.flatMap(f => f.configs.map(c => c.stack.length))));
      }
      const stackY = tapeY + CHT + 50;
      const H = M.kind === "pda" ? stackY + 26 + stackRows * SCELL + 30 : tapeY + CHT + 24;
      svg.setAttribute("viewBox", `0 0 ${W} ${H}`);

      const defs = mk("defs", {}, svg);
      for (const k of ["def", "hot", "trace", "ink"]) {
        const m = mk("marker", { id: `mc-arrow-${k}`, viewBox: "0 0 10 10", refX: 9, refY: 5, markerWidth: 10, markerHeight: 10, markerUnits: "userSpaceOnUse", orient: "auto" }, defs);
        mk("path", { d: "M0,0 L10,5 L0,10 z", class: `mc-m-${k}` }, m);
      }
      const gD = mk("g", { transform: `translate(0,${shift + (Hd - (maxBot - minTop) - 34) / 2 - 10})` }, svg);
      const gE = mk("g", {}, gD), gL = mk("g", {}, gD), gN = mk("g", {}, gD);
      for (const g of groups.values()) {
        const { d, lx, ly } = g;
        g.path = mk("path", { d, class: "mc-edge" + (g.ts.every(t => t.a === "ε") ? " eps" : "") }, gE);
        const w = Math.max(...g.lines.map(l => [...l].length)) * 7.3 + 14, h = g.lines.length * 15 + 6;
        g.label = mk("g", { class: "mc-elabel" }, gL);
        mk("rect", { x: lx - w / 2, y: ly - h / 2, width: w, height: h, rx: 8 }, g.label);
        g.lineRects = []; g.lineTexts = [];
        g.lines.forEach((l, i) => {
          const y = ly - h / 2 + 3 + i * 15 + 7.5;
          g.lineRects.push(mk("rect", { x: lx - w / 2 + 2, y: y - 7, width: w - 4, height: 14, rx: 5, fill: "none" }, g.label));
          const tx = mk("text", { x: lx, y }, g.label); tx.textContent = l; g.lineTexts.push(tx);
        });
      }
      const s0 = P[M.q0];
      mk("path", { d: `M${s0.x - R - 42},${s0.y} L${s0.x - R - 1},${s0.y}`, class: "mc-start", "marker-end": "url(#mc-arrow-ink)" }, gN);
      const nodes = {};
      const accepting = s => (M.kind === "tm" ? s === M.qa : M.F.has(s));
      M.Q.forEach(s => {
        const p = P[s];
        const g = mk("g", { class: "mc-node", tabindex: "0", role: "button", "aria-label": `State ${s}${s === M.q0 ? ", start" : ""}${accepting(s) ? ", accepting" : ""}${M.kind === "tm" && s === M.qr ? ", rejecting" : ""}` }, gN);
        mk("circle", { cx: p.x, cy: p.y, r: R, class: "body" }, g);
        if (accepting(s)) mk("circle", { cx: p.x, cy: p.y, r: R - 5, class: "inner" }, g);
        const tx = mk("text", { x: p.x, y: p.y }, g); tx.textContent = s;
        if (s.length > 3) tx.style.fontSize = "11px";
        const on = () => { hover = s; paint(); }, off = () => { hover = null; paint(); };
        g.addEventListener("pointerenter", on); g.addEventListener("pointerleave", off);
        g.addEventListener("focus", on); g.addEventListener("blur", off);
        g.addEventListener("click", () => { hover = hover === s ? null : s; paint(); });
        nodes[s] = g;
      });
      const sect = (y, text) => { const t = mk("text", { x: 24, y, class: "mc-sect" }, svg); t.textContent = text; };
      sect(tapeY - 26, M.kind === "pda" ? "INPUT" : M.bounded ? "TAPE · BOUNDED BY END MARKERS" : "TAPE");
      if (M.kind === "pda") sect(stackY - 8, "STACK · TOP FIRST");
      els = { W, P, groups, nodes, tapeY, stackY, stackRows, gTape: mk("g", {}, svg), gStack: mk("g", {}, svg) };
    }

    function paintTape(f) {
      const g = els.gTape; g.innerHTML = "";
      const y = els.tapeY, W = els.W;
      const fit = Math.floor((W - 60) / (CW + 4));
      const cellAt = (x, sym, cls) => {
        const c = mk("g", { class: "mc-cell " + cls }, g);
        mk("rect", { x, y, width: CW, height: CHT, rx: 6 }, c);
        mk("text", { x: x + CW / 2, y: y + CHT / 2 }, c).textContent = sym;
        return c;
      };
      if (M.kind === "pda") {
        const pos = f ? f.pos : -1, n = M.input.length;
        const total = n + 1, x0 = Math.max(24, (W - total * (CW + 4)) / 2);
        M.input.forEach((ch, i) => {
          let cls = "";
          if (f) { if (i < pos) cls += " read"; if (!f.done && f.sym !== undefined && i === pos - 1) cls += " written"; if (!f.done && i === pos) cls += " head"; }
          cellAt(x0 + i * (CW + 4), ch, cls);
        });
        const endCls = "end" + (f && f.done ? (f.ok ? " acc" : " rej") : "") + (f && !f.done && pos === n ? " head" : "");
        const e = mk("g", { class: "mc-cell " + endCls }, g);
        const ex = x0 + n * (CW + 4), ew = 64;
        mk("rect", { x: ex, y, width: ew, height: CHT, rx: 6 }, e);
        mk("text", { x: ex + ew / 2, y: y + CHT / 2 }, e).textContent = f && f.done ? (f.ok ? "accept" : "reject") : n ? "end" : "empty";
        if (f && !f.done && pos < n) {
          const hx = x0 + pos * (CW + 4) + CW / 2;
          mk("path", { d: `M${hx - 7},${y - 12} L${hx + 7},${y - 12} L${hx},${y - 3} z`, class: "mc-headmark" }, g);
        }
        return;
      }
      // Turing machine tape: a window that holds what was ever used, following the head when too wide
      const t = f ? f.tape : (M.bounded ? { off: 0, cells: [LM, ...M.input, RM] } : { off: 0, cells: M.input.length ? [...M.input] : [BLANK] });
      const head = f ? f.head : (M.bounded ? 1 : 0);
      const used0 = t.off, used1 = t.off + t.cells.length - 1;
      let lo = Math.min(used0, head), hi = Math.max(used1, head);
      if (!M.bounded) { lo -= 1; hi += 1; }
      if (hi - lo + 1 > fit) { lo = Math.max(lo, head - Math.floor(fit / 2)); hi = lo + fit - 1; if (hi < head) { hi = head + 1; lo = hi - fit + 1; } }
      const count = hi - lo + 1, x0 = Math.max(24, (W - count * (CW + 4)) / 2);
      for (let i = lo; i <= hi; i++) {
        const sym = t.cells[i - t.off] ?? BLANK;
        let cls = sym === BLANK ? "blank" : (sym === LM || sym === RM) ? "marker" : "";
        if (f && !f.done && f.prev === i) cls += " written";
        if (i === head) cls += " head";
        cellAt(x0 + (i - lo) * (CW + 4), sym, cls);
      }
      if (lo > Math.min(used0, head) || (!M.bounded && lo > used0 - 1)) mk("text", { x: x0 - 14, y: y + CHT / 2, class: "mc-gap" }, g).textContent = "…";
      if (hi < used1) mk("text", { x: x0 + count * (CW + 4) + 6, y: y + CHT / 2, class: "mc-gap" }, g).textContent = "…";
      const hx = x0 + (head - lo) * (CW + 4) + CW / 2;
      mk("path", { d: `M${hx - 7},${y - 12} L${hx + 7},${y - 12} L${hx},${y - 3} z`, class: "mc-headmark" }, g);
      if (f) mk("text", { x: hx, y: y - 16, class: "mc-headlbl" }, g).textContent = f.state;
    }

    function paintStacks(f) {
      const g = els.gStack; g.innerHTML = "";
      if (M.kind !== "pda") return;
      const configs = f ? f.configs : [{ state: M.q0, stack: M.Z0 }];
      const show = configs.slice(0, MAX_BRANCHES), y0 = els.stackY + 22;
      const x0 = Math.max(24, (els.W - show.length * (SW + 22)) / 2);
      show.forEach((c, i) => {
        const win = f && f.done && f.winners && f.winners.includes(c);
        const sg = mk("g", { class: "mc-stack" + (f && f.done ? (win ? " acc" : f.ok ? "" : " rej") : "") }, g);
        const x = x0 + i * (SW + 22);
        // the stack rests on its floor line; its top is the highest cell. Too tall: the top cells, then "+k below".
        const floorY = y0 + els.stackRows * SCELL + 2;
        const syms = [...c.stack], over = syms.length > els.stackRows;
        const shown = over ? syms.slice(0, els.stackRows - 1) : syms, slots = shown.length + (over ? 1 : 0);
        mk("text", { x: x + SW / 2, y: y0 - 12, "text-anchor": "middle", class: "slbl" }, sg).textContent = c.state;
        shown.forEach((s, j) => {
          const cg = mk("g", { class: j === 0 ? "top" : "" }, sg), cy = floorY - 2 - (slots - j) * SCELL;
          mk("rect", { x, y: cy, width: SW, height: SCELL - 2, rx: 4 }, cg);
          mk("text", { x: x + SW / 2, y: cy + (SCELL - 2) / 2 }, cg).textContent = s;
        });
        if (over) mk("text", { x: x + SW / 2, y: floorY - 2 - SCELL / 2, "text-anchor": "middle", class: "more" }, sg).textContent = `+${syms.length - shown.length} below`;
        mk("line", { x1: x - 4, x2: x + SW + 4, y1: floorY, y2: floorY, class: "floor" }, sg);
        if (!syms.length) mk("text", { x: x + SW / 2, y: floorY - 12, "text-anchor": "middle", class: "more" }, sg).textContent = "empty";
      });
      if (configs.length > MAX_BRANCHES) {
        mk("text", { x: x0 + show.length * (SW + 22) + 4, y: y0 + 12, class: "mc-gap" }, g).textContent = `+${configs.length - MAX_BRANCHES} more branches`;
      }
    }

    function paint() {
      const f = frame, done = f && f.done;
      const active = new Set(f ? (M.kind === "tm" ? [f.state] : f.configs.map(c => c.state)) : []);
      const takenIdx = new Set((f && !done ? (f.trans || f.taken || []) : []).map(t => t.i));
      const targets = new Set(hover ? M.trans.filter(t => t.q === hover).map(t => t.p) : []);
      const visited = new Set();
      if (done) {
        if (M.kind === "tm") cur.forEach(fr => visited.add(fr.state));
        else (f.visited || new Set()).forEach(s => visited.add(s));
      }
      M.Q.forEach(s => {
        let st = "Background";
        if (f && done) {
          if (M.kind === "tm") {
            if (s === f.state) st = f.verdict === "accept" ? "Solution" : f.verdict === "limit" ? "Active" : "Rejected";
            else if (f.verdict === "stuck" && s === M.qr) st = "Rejected";
            else if (!visited.has(s)) st = "Untraveled";
          } else {
            if (active.has(s)) st = f.ok ? (f.winners.some(c => c.state === s) ? "Solution" : "Background") : "Rejected";
            else if (!visited.has(s)) st = "Untraveled";
          }
        } else if (f && active.has(s)) st = "Active";
        let cls = "mc-node " + st + (M.kind === "tm" && s === M.qr ? " reject" : "");
        if (hover) cls += s === hover ? " trace" : targets.has(s) ? "" : " faint";
        els.nodes[s].setAttribute("class", cls);
      });
      for (const g of els.groups.values()) {
        const on = g.ts.map(t => takenIdx.has(t.i));
        let state = on.some(Boolean) ? "hot" : "";
        if (hover) state = g.from === hover ? "trace" : "faint";
        g.path.setAttribute("class", "mc-edge" + (g.ts.every(t => t.a === "ε") ? " eps" : "") + (state ? " " + state : ""));
        g.path.setAttribute("marker-end", `url(#mc-arrow-${state === "hot" ? "hot" : state === "trace" ? "trace" : "def"})`);
        g.label.setAttribute("class", "mc-elabel" + (state ? " " + state : ""));
        g.lineRects.forEach((r, i) => r.setAttribute("class", state === "hot" && on.at(i) ? "lineon" : ""));
        g.lineTexts.forEach((t, i) => t.setAttribute("class", state === "hot" && on.at(i) ? "on" : ""));
      }
      paintTape(f);
      paintStacks(f);
    }

    return {
      load(str, key, solver) {
        Pk = key; M = parse(str, key);
        if (M.kind === "tm") { lim = M.bounded ? 5000 : (solver === "s2000" ? 2000 : 200); all = runTM(M, lim); runs = []; }
        else { all = pdaAll(M); runs = pdaRuns(M); }
        cur = all; frame = null; hover = null;
        draw(); paint();
        const last = all[all.length - 1];
        return { frames: all, ok: last.ok, runs: runs.map(r => ({ label: r.label, ok: r.ok })), runLimitHit: runs.length >= RUN_LIMIT };
      },
      // null = every branch at once; otherwise follow runs[i] alone (PDA only)
      usePath(i) {
        const r = i === null ? null : runs[i];
        cur = r ? r.frames : all; frame = null; paint();
        return { frames: cur, ok: r ? r.ok : all[all.length - 1].ok };
      },
      show(i) { frame = i === null ? null : cur[i]; paint(); return frame; },
      checks(i) {
        const f = i === null ? null : cur[i];
        if (!f) return [];
        if (M.kind === "tm") {
          const out = [{ label: M.bounded ? `Steps taken: ${f.steps}` : `Steps taken: ${f.steps} (limit ${lim})`, ok: null }];
          const used = f.tape.cells.filter(c => c !== BLANK && c !== LM && c !== RM).length;
          out.push({ label: `Tape cells holding a symbol: ${used}`, ok: null });
          if (f.done) {
            out.push({ label: f.verdict === "limit" ? "Halted: not within the step limit" : f.verdict === "loop" ? "Halted: no, it loops forever" : "Halted: yes", ok: f.verdict === "accept" || f.verdict === "reject" || f.verdict === "stuck" ? true : false });
            out.push({ label: `Accepted: ${f.verdict === "accept" ? "yes" : f.verdict === "limit" ? "unknown" : "no"}`, ok: f.verdict === "accept" });
          }
          return out;
        }
        const out = [{ label: `Input read: ${f.pos} / ${M.input.length}`, ok: f.done ? f.pos === M.input.length : null }];
        out.push({ label: `Branches alive: ${f.configs.length}`, ok: null });
        if (f.done) {
          out.push({ label: `A branch ends in an accept state: ${f.ok ? "yes" : "no"}`, ok: f.ok });
          if (f.pruned) out.push({ label: "Some branches that kept growing their stack with ε-moves were cut off", ok: null });
        }
        return out;
      },
      chosen(i) {
        const f = i === null ? null : cur[i];
        if (!f) return [];
        if (M.kind === "tm") {
          const t = f.tape.cells.join("").replace(/^_+|_+$/g, "") || "(blank)";
          const out = [{ text: `Tape: ${t}` }];
          if (f.done) out.push({ text: f.verdict === "accept" ? "Accepted" : f.verdict === "limit" ? `No answer after ${f.steps} steps` : f.verdict === "loop" ? "Loops forever" : "Rejected" });
          return out;
        }
        const out = f.configs.slice(0, MAX_BRANCHES).map(c => ({ text: `${c.state}: ${c.stack || "empty"}` }));
        if (f.done) out.unshift({ text: f.ok ? "Accepted" : "Rejected" });
        return out;
      },
      resetHover() { hover = null; paint(); },
    };
  }

  /* ---------- catalog ---------- */
  const TM_ANBN = "(({q0,q1,q2,q3,qa,qr},{a,b},{a,b,X,Y,_},{(q0,a,q1,X,R),(q0,Y,q3,Y,R),(q0,_,qa,_,R),(q1,a,q1,a,R),(q1,Y,q1,Y,R),(q1,b,q2,Y,L),(q2,a,q2,a,L),(q2,Y,q2,Y,L),(q2,X,q0,X,R),(q3,Y,q3,Y,R),(q3,_,qa,_,R)},q0,qa,qr),";
  const LBA_ABC = "(({q0,q1,q2,q3,q4,q5,qa,qr},{a,b,c},{a,b,c,X,Y,Z,<,>},{(q0,a,q1,X,R),(q0,Y,q4,Y,R),(q0,>,qa,>,L),(q1,a,q1,a,R),(q1,Y,q1,Y,R),(q1,b,q2,Y,R),(q2,b,q2,b,R),(q2,Z,q2,Z,R),(q2,c,q3,Z,L),(q3,a,q3,a,L),(q3,b,q3,b,L),(q3,Y,q3,Y,L),(q3,Z,q3,Z,L),(q3,X,q0,X,R),(q4,Y,q4,Y,R),(q4,Z,q5,Z,R),(q5,Z,q5,Z,R),(q5,>,qa,>,L)},q0,qa,qr),";
  const PDA_ANBN = "(({q0,q1,qf},{a,b},{A,Z},{(q0,a,Z,q0,AZ),(q0,a,A,q0,AA),(q0,b,A,q1,ε),(q1,b,A,q1,ε),(q1,ε,Z,qf,Z),(q0,ε,Z,qf,Z)},q0,Z,{qf}),";
  const PROBLEMS = {
    TMACCEPT: {
      label: "Turing Machine Acceptance", kind: "tm", cls: "Undecidable", type: "Automata", vizType: "Tape Machine",
      def: "Given a Turing machine M and an input w, does M accept w? This is A_TM, the classic undecidable problem: simulating M confirms a yes, but a machine that never halts can only be given up on.",
      input: "((Q, Σ, Γ, δ, q0, q_accept, q_reject), w)", parts: "Q, Σ, Γ, δ, q0, q_accept, q_reject", rule: "(state, read, next state, write, L or R)",
      inputLong: "States, input and tape alphabets (blank is _), rules, start, accept and reject states, and the input",
      solvers: [["s200", "Step-by-step simulation, up to 200 steps (not in Redux yet)"], ["s2000", "Step-by-step simulation, up to 2,000 steps (not in Redux yet)"]],
      examples: [["Recognizes aⁿbⁿ, input aabb", TM_ANBN + "aabb)"], ["Recognizes aⁿbⁿ, input aab", TM_ANBN + "aab)"],
                 ["Binary increment, 1011 + 1", "(({q0,q1,qa,qr},{0,1},{0,1,_},{(q0,0,q0,0,R),(q0,1,q0,1,R),(q0,_,q1,_,L),(q1,1,q1,0,L),(q1,0,qa,1,R),(q1,_,qa,1,R)},q0,qa,qr),1011)"],
                 ["Never halts (runs right forever)", "(({q0,qa,qr},{a},{a,_},{(q0,a,q0,a,R),(q0,_,q0,_,R)},q0,qa,qr),aa)"]],
    },
    LBAACCEPT: {
      label: "Linear Bounded Automaton Acceptance", kind: "tm", bounded: true, cls: "PSPACE-Complete", type: "Automata", vizType: "Tape Machine",
      def: "Given a linear bounded automaton M (a Turing machine that never leaves the cells holding its input) and an input w, does M accept w? Unlike A_TM this is decidable: M has finitely many configurations, so a repeat proves it loops.",
      input: "((Q, Σ, Γ, δ, q0, q_accept, q_reject), w)", parts: "Q, Σ, Γ, δ, q0, q_accept, q_reject", rule: "(state, read, next state, write, L or R)",
      inputLong: "Same as a Turing machine; the end markers < and > are added around the input",
      solvers: [["detect", "Simulation with loop detection (not in Redux yet)"]],
      examples: [["Decides aⁿbⁿcⁿ, input aabbcc", LBA_ABC + "aabbcc)"], ["Decides aⁿbⁿcⁿ, input aabbc", LBA_ABC + "aabbc)"],
                 ["Bounces between the markers forever", "(({q0,q1,qa,qr},{a},{a,<,>},{(q0,a,q0,a,R),(q0,>,q1,>,L),(q1,a,q1,a,L),(q1,<,q0,<,R)},q0,qa,qr),aa)"]],
    },
    PDAACCEPT: {
      label: "Pushdown Automaton Acceptance", kind: "pda", cls: "P", type: "Automata", vizType: "Pushdown Automaton",
      def: "Given a pushdown automaton P and an input w, does P accept w by final state? Decidable in polynomial time (it's the same question as context-free grammar membership). Steps search P's configurations directly.",
      input: "((Q, Σ, Γ, δ, q0, Z0, F), w)", parts: "Q, Σ, Γ, δ, q0, Z0, F", rule: "(state, read or ε, pop, next state, push or ε)",
      inputLong: "States, input and stack alphabets, rules, start state, start stack symbol, accept states, and the input",
      solvers: [["search", "Configuration search (not in Redux yet)"]],
      examples: [["Balanced brackets, input [[]][]", "(({q,f},{[,]},{A,Z},{(q,[,Z,q,AZ),(q,[,A,q,AA),(q,],A,q,ε),(q,ε,Z,f,Z)},q,Z,{f}),[[]][])"],
                 ["aⁿbⁿ, input aabb", PDA_ANBN + "aabb)"], ["aⁿbⁿ, input aab", PDA_ANBN + "aab)"],
                 ["Even palindromes (nondeterministic), input abba", "(({p,q,f},{a,b},{A,B,Z},{(p,a,Z,p,AZ),(p,a,A,p,AA),(p,a,B,p,AB),(p,b,Z,p,BZ),(p,b,A,p,BA),(p,b,B,p,BB),(p,ε,Z,q,Z),(p,ε,A,q,A),(p,ε,B,q,B),(q,a,A,q,ε),(q,b,B,q,ε),(q,ε,Z,f,Z)},p,Z,{f}),abba)"]],
    },
  };

  return { create, parse, PROBLEMS, _runTM: runTM, _pdaAll: pdaAll, _pdaRuns: pdaRuns };
})();
