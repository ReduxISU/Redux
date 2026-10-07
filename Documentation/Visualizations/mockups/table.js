/* ---- Table view: Edit Distance (DP grid), 0-1 Integer Programming (constraint matrix),
   and a shared trace table for DFA, NFA, SSSP and SPSP. Pages supply --av-* tokens.
   Solvers emit frames in the problem's own terms; this module only draws them. ---- */
const TableView = (() => {
  const NS = "http://www.w3.org/2000/svg";
  const mk = (tag, attrs = {}, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; };
  const esc = s => String(s).replace(/[&<>"]/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));
  const SHOW = 150, SEP = "\u0000";

  const css = `
.tb-wrap { display: grid; gap: 12px; min-width: 0; }
.tb-scroll { overflow: auto; max-height: 460px; border: 1px solid var(--av-line); border-radius: 8px; background: var(--av-surface); }
.tb-table { border-collapse: separate; border-spacing: 0; font-family: var(--av-mono); font-size: 13px; color: var(--av-ink); min-width: 100%; }
.tb-table th { position: sticky; top: 0; z-index: 1; background: var(--av-surface); text-align: left; font-weight: 600; font-size: 11px; letter-spacing: .05em; text-transform: uppercase; color: var(--av-muted); padding: 8px 12px; border-bottom: 1.5px solid var(--av-line); white-space: nowrap; }
.tb-table td { padding: 7px 12px; border-bottom: 1px solid var(--av-line); white-space: nowrap; font-variant-numeric: tabular-nums; transition: background .2s, color .2s; }
.tb-table tr:last-child td { border-bottom: 0; }
.tb-table td.num { text-align: right; }
.tb-table td.mut { color: var(--av-muted); }
.tb-table td.tent { color: var(--av-muted); font-style: italic; }
.tb-table tr.cur td { background: var(--av-hl-fill); }
.tb-table tr.cur td:first-child { box-shadow: inset 3px 0 0 var(--av-hl); }
.tb-table tr.ok td { background: var(--av-sol-fill); }
.tb-table tr.ok td:first-child { box-shadow: inset 3px 0 0 var(--av-sol); }
.tb-table tr.bad td { background: var(--av-rej-fill); color: var(--av-rej); }
.tb-table tr.bad td:first-child { box-shadow: inset 3px 0 0 var(--av-rej); }
.tb-table tr.unr td { color: var(--av-muted); }
.tb-table td.chg { font-weight: 700; box-shadow: inset 0 -2.5px 0 var(--av-hl); }
.tb-table td.on { background: var(--av-sol-fill); font-weight: 700; }
.tb-table td.zero { color: var(--av-muted); opacity: .6; }
.tb-pill { display: inline-block; padding: 1px 9px; border-radius: 999px; border: 1.2px solid var(--av-line); font-size: 12px; color: var(--av-muted); }
.tb-pill.yes { border-color: var(--av-sol); color: var(--av-sol); font-weight: 600; }
.tb-pill.no { border-color: var(--av-rej); color: var(--av-rej); font-weight: 600; }
.tb-pill.open { border-style: dashed; }
.tb-x { display: inline-grid; place-items: center; min-width: 26px; height: 26px; padding: 0 4px; border-radius: 6px; border: 1.5px solid var(--av-line); font-weight: 700; }
.tb-x.one { background: var(--av-sol); border-color: var(--av-sol); color: #fff; }
.tb-x.unset { border-style: dashed; color: var(--av-muted); font-weight: 400; }
.tb-x.cur { box-shadow: 0 0 0 2.5px var(--av-hl); }
.tb-x.trial.one { background: var(--av-hl); border-color: var(--av-hl); color: #1b1300; }
.tb-note { font-size: 12.5px; color: var(--av-muted); }
.tb-svg { width: 100%; height: auto; display: block; max-height: 520px; }
.tb-svg text { font-family: var(--av-mono); }
.tb-cell rect { fill: var(--av-surface); stroke: var(--av-line); stroke-width: 1; transition: fill .2s, stroke .2s; }
.tb-cell text { font-size: 15px; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); transition: fill .2s; }
.tb-cell.empty text { fill: transparent; }
.tb-cell.base rect { fill: var(--av-bg); }
.tb-cell.base text { fill: var(--av-muted); }
.tb-cell.src rect { stroke: var(--av-hl); stroke-width: 1.6; stroke-dasharray: 4 3; }
.tb-cell.best rect { stroke: var(--av-hl); stroke-width: 2.6; stroke-dasharray: none; }
.tb-cell.Active rect { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 2.6; }
.tb-cell.Active text { font-weight: 700; }
.tb-cell.Solution rect { fill: var(--av-sol); stroke: var(--av-sol); }
.tb-cell.Solution text { fill: #fff; font-weight: 700; }
.tb-axis { font-size: 15px; font-weight: 600; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); }
.tb-axis.eps { fill: var(--av-muted); font-weight: 400; }
.tb-axis.hl { fill: var(--av-hl); }
.tb-axis-name { font-size: 11px; fill: var(--av-muted); letter-spacing: .06em; }
.tb-arrow { stroke: var(--av-hl); stroke-width: 2.2; fill: none; }
.tb-arrow.alt { stroke-dasharray: 4 3; opacity: .55; }
.tb-arrow.path { stroke: var(--av-sol); stroke-width: 2.8; }
.tb-m-hl { fill: var(--av-hl); } .tb-m-sol { fill: var(--av-sol); }
.tb-align { display: grid; gap: 3px; font-family: var(--av-mono); font-size: 15px; overflow-x: auto; padding-bottom: 2px; }
.tb-align .tb-ln { display: flex; gap: 2px; align-items: center; }
.tb-align .tb-lab { width: 64px; flex: none; font-size: 11px; color: var(--av-muted); letter-spacing: .05em; text-transform: uppercase; }
.tb-align .tb-c { width: 1.9em; height: 1.9em; flex: none; display: grid; place-items: center; border-radius: 5px; }
.tb-align .tb-op .tb-c { font-size: 11px; font-weight: 700; }
.tb-align .tb-c.tb-k { color: var(--av-muted); }
.tb-align .tb-c.tb-s { background: var(--av-hl-fill); color: var(--av-ink); }
.tb-align .tb-c.tb-d, .tb-align .tb-c.tb-i { background: var(--av-rej-fill); color: var(--av-rej); }
.tb-align .tb-c.tb-gap { color: var(--av-muted); }
@media (prefers-reduced-motion: reduce) { .tb-table td, .tb-cell rect, .tb-cell text { transition: none; } }`;

  /* ======================= parsing ======================= */
  const EPS = new Set(["ε", "eps", "epsilon"]);
  function parseEditDistance(str) {
    // Redux: trim, strip the outer parentheses, split on the comma, trim each side.
    const s = str.trim();
    if (!/^\(.*\)$/s.test(s)) throw new Error("Expected (x, y), two strings in parentheses.");
    const parts = s.slice(1, -1).split(",");
    if (parts.length !== 2) throw new Error("Expected exactly one comma: (x, y). Strings can't contain commas.");
    const x = parts[0].trim(), y = parts[1].trim();
    if (x.length > 14 || y.length > 14) throw new Error("This mockup draws strings up to 14 characters.");
    return { x, y };
  }
  function parseIP(str) {
    // Redux: (row1),(row2),...<=(d1 d2 ...); each row is space-separated integers.
    const s = str.replace(/\s+/g, " ").trim();
    const halves = s.split("<=");
    if (halves.length !== 2) throw new Error("Expected (row1),(row2),…<=(d1 … dM).");
    const rows = halves[0].trim().split(/\)\s*,\s*\(/).map(r => r.replace(/[()]/g, "").trim());
    const C = rows.map((r, i) => {
      if (!r) throw new Error(`Row ${i + 1} is empty.`);
      return r.split(" ").map(t => { if (!/^-?\d+$/.test(t)) throw new Error(`"${t}" in row ${i + 1} isn't a whole number.`); return Number(t); });
    });
    const dStr = halves[1].replace(/[()]/g, "").trim();
    const d = dStr ? dStr.split(" ").map(t => { if (!/^-?\d+$/.test(t)) throw new Error(`"${t}" in d isn't a whole number.`); return Number(t); }) : [];
    const n = C[0].length;
    if (C.some(r => r.length !== n)) throw new Error("Every row needs the same number of coefficients.");
    if (d.length !== C.length) throw new Error(`d has ${d.length} ${d.length === 1 ? "entry" : "entries"} but there ${C.length === 1 ? "is 1 row" : `are ${C.length} rows`}. They must match.`);
    if (n > 12) throw new Error("This mockup handles up to 12 variables.");
    return { C, d, n, m: C.length };
  }
  function parseAutomaton(str, kind) {
    const s = str.replace(/\s+/g, "");
    const m = /^\(\(\{([^{}]*)\},\{([^{}]*)\},\{(.*)\},([^,{}()]+),\{([^{}]*)\}\),([^,{}()]*)\)$/.exec(s);
    if (!m) throw new Error("Expected ((States, Alphabet, Transitions, Start, Accept), input).");
    const list = x => (x ? x.split(",") : []);
    const states = list(m[1]), alphabet = list(m[2]), start = m[4], accept = new Set(list(m[5]));
    const re = /\(([^,()]+),([^,()]+),([^,()]+)\)/g;
    if (m[3].replace(re, "").replace(/,/g, "")) throw new Error("Each transition must look like (from,symbol,to).");
    const trans = []; let t;
    while ((t = re.exec(m[3]))) trans.push({ from: t[1], sym: EPS.has(t[2]) ? "ε" : t[2], to: t[3] });
    const S = new Set(states);
    if (!states.length || S.size !== states.length) throw new Error("States must be a non-empty set with no repeats.");
    if (!S.has(start)) throw new Error(`Start state ${start} is not in the state set.`);
    for (const a of accept) if (!S.has(a)) throw new Error(`Accept state ${a} is not in the state set.`);
    for (const e of trans) {
      if (!S.has(e.from) || !S.has(e.to)) throw new Error(`Transition (${e.from},${e.sym},${e.to}) uses an unknown state.`);
      if (e.sym !== "ε" && !alphabet.includes(e.sym)) throw new Error(`Symbol ${e.sym} is not in the alphabet.`);
    }
    const input = m[6] === "ε" ? [] : [...m[6]];
    for (const ch of input) if (!alphabet.includes(ch)) throw new Error(`Input symbol ${ch} is not in the alphabet.`);
    if (kind === "dfa") {
      if (trans.some(e => e.sym === "ε")) throw new Error("A DFA can't have ε-transitions.");
      const seen = new Set();
      for (const e of trans) { const k = e.from + SEP + e.sym; if (seen.has(k)) throw new Error(`State ${e.from} has two transitions on ${e.sym}.`); seen.add(k); }
    }
    return { states, alphabet, trans, start, accept, input };
  }
  function tokenize(s) {
    const out = []; let buf = "";
    for (const ch of s) { if ("{}(),".includes(ch)) { if (buf.trim()) out.push(buf.trim()); buf = ""; out.push(ch); } else buf += ch; }
    if (buf.trim()) out.push(buf.trim());
    return out;
  }
  function tree(tokens) {
    let i = 0;
    function val() {
      const t = tokens[i];
      if (t === "{" || t === "(") {
        i++; const close = t === "{" ? "}" : ")", node = { t: t === "{" ? "set" : "tup", items: [] };
        if (tokens[i] === close) { i++; return node; }
        for (;;) { node.items.push(val()); if (tokens[i] === ",") { i++; continue; } if (tokens[i] === close) { i++; return node; } throw new Error("Brackets don't match up."); }
      }
      if (t === undefined || "}),".includes(t)) throw new Error(t === undefined ? "The instance ends too early." : `Unexpected "${t}".`);
      i++; return t;
    }
    const v = val(); if (i !== tokens.length) throw new Error("Extra text after the instance."); return v;
  }
  function parsePaths(str, needT) {
    // Redux: (N, E, s) or (N, E, s, t); edges ((u,v),w) directed, ({u,v},w) both ways, (u,v)/{u,v} weight 1.
    const top = tree(tokenize(str));
    if (top.t !== "tup" || top.items.length !== (needT ? 4 : 3)) throw new Error(needT ? "Expected (N, E, s, t)." : "Expected (N, E, s).");
    const [Nn, En, s, t] = top.items;
    if (Nn.t !== "set" || En.t !== "set") throw new Error("Expected a node set and an edge set.");
    const nodes = Nn.items; const S = new Set(nodes);
    if (nodes.some(x => typeof x !== "string") || !nodes.length || S.size !== nodes.length) throw new Error("N must be a non-empty set of names with no repeats.");
    if (nodes.length > 16) throw new Error("This mockup handles up to 16 nodes.");
    const adj = new Map(nodes.map(n => [n, []]));
    const add = (a, b, w) => { if (!S.has(a) || !S.has(b)) throw new Error(`Edge ${a}–${b} uses a node that isn't in N.`); if (w < 0) throw new Error("Dijkstra needs non-negative weights."); adj.get(a).push({ to: b, w }); };
    for (const e of En.items) {
      let pair = e, w = 1;
      if (e.t === "tup" && e.items.length === 2 && typeof e.items[1] === "string" && typeof e.items[0] === "object") { pair = e.items[0]; w = Number(e.items[1]); if (!Number.isFinite(w)) throw new Error("Edge weights must be numbers."); }
      if (!pair || typeof pair !== "object" || pair.items.length !== 2 || pair.items.some(x => typeof x !== "string")) throw new Error("Each edge looks like ((u,v),w), ({u,v},w), (u,v) or {u,v}.");
      const [a, b] = pair.items;
      add(a, b, w); if (pair.t === "set") add(b, a, w);
    }
    if (typeof s !== "string" || !S.has(s)) throw new Error(`Source ${s} isn't in N.`);
    if (needT && (typeof t !== "string" || !S.has(t))) throw new Error(`Target ${t} isn't in N.`);
    return { nodes, adj, s, t: needT ? t : null };
  }

  /* ======================= solvers (frames in the problem's own terms) ======================= */
  function solveEditDistance(I) {
    const { x, y } = I, m = x.length, n = y.length;
    const dp = Array.from({ length: m + 1 }, (_, i) => Array.from({ length: n + 1 }, (_, j) => (i === 0 ? j : j === 0 ? i : 0)));
    const how = Array.from({ length: m + 1 }, () => Array(n + 1).fill(null));
    const frames = [{ filled: 0, caption: "Fill row 0 and column 0 first. Turning a prefix into the empty string costs one deletion or insertion per letter." }];
    let k = 0;
    for (let i = 1; i <= m; i++) for (let j = 1; j <= n; j++) {
      k++;
      const a = x[i - 1], b = y[j - 1];
      let caption, op;
      if (a === b) { dp[i][j] = dp[i - 1][j - 1]; op = "keep"; caption = `"${a}" = "${b}", so copy the diagonal: ${dp[i][j]}.`; }
      else {
        const sub = dp[i - 1][j - 1], del = dp[i - 1][j], ins = dp[i][j - 1], best = Math.min(sub, del, ins);
        dp[i][j] = 1 + best;
        op = sub === best ? "sub" : del === best ? "del" : "ins";
        const name = { sub: "substitute", del: "delete", ins: "insert" }[op];
        caption = `"${a}" ≠ "${b}": 1 + min(substitute ${sub}, delete ${del}, insert ${ins}) = ${dp[i][j]}, by ${name}.`;
      }
      how[i][j] = op;
      frames.push({ filled: k, cur: [i, j], op, caption });
    }
    // backtrace from (m, n): prefer the diagonal, then delete, then insert
    const path = [[m, n]], ops = [];
    let i = m, j = n;
    while (i > 0 || j > 0) {
      if (i > 0 && j > 0 && x[i - 1] === y[j - 1] && dp[i][j] === dp[i - 1][j - 1]) { ops.push({ op: "keep", a: x[i - 1], b: y[j - 1] }); i--; j--; }
      else if (i > 0 && j > 0 && dp[i][j] === dp[i - 1][j - 1] + 1) { ops.push({ op: "sub", a: x[i - 1], b: y[j - 1] }); i--; j--; }
      else if (i > 0 && dp[i][j] === dp[i - 1][j] + 1) { ops.push({ op: "del", a: x[i - 1], b: null }); i--; }
      else { ops.push({ op: "ins", a: null, b: y[j - 1] }); j--; }
      path.push([i, j]);
    }
    path.reverse(); ops.reverse();
    const cnt = t => ops.filter(o => o.op === t).length;
    const parts = [[cnt("sub"), "substitution"], [cnt("del"), "deletion"], [cnt("ins"), "insertion"]].filter(([c]) => c).map(([c, w]) => `${c} ${w}${c > 1 ? "s" : ""}`);
    frames.push({ filled: m * n, done: true, ok: true, path, ops,
      caption: `Edit distance ${dp[m][n]}. Tracing back from the corner gives one cheapest way: ${parts.length ? parts.join(", ") : "no edits at all"}.` });
    return { frames, dp, ok: true, value: dp[m][n] };
  }

  const ipRowState = (I, x) => I.C.map((row, r) => {
    let lhs = 0, lo = 0, hi = 0;
    row.forEach((c, j) => { if (x[j] === null) { lo += Math.min(0, c); hi += Math.max(0, c); } else lhs += c * x[j]; });
    const min = lhs + lo, max = lhs + hi;
    return { lhs, min, max, st: max <= I.d[r] ? "ok" : min > I.d[r] ? "bad" : "open" };
  });
  function solveIPBrute(I) {
    // Redux's IntegerProgrammingBruteForce: start at all zeros, count in binary with x1 flipping fastest,
    // return the first assignment the verifier accepts.
    const frames = [{ x: Array(I.n).fill(null), caption: "Step 0: nothing assigned yet. Brute force tries every 0-1 assignment in order." }];
    const x = Array(I.n).fill(0);
    let tried = 0, found = null;
    for (let t = 0; t < 2 ** I.n; t++) {
      tried++;
      const rs = ipRowState(I, x), bad = rs.map((r, i) => (r.st === "bad" ? i : -1)).filter(i => i >= 0);
      const ok = !bad.length;
      if (tried <= SHOW) frames.push({ x: x.slice(), trial: true, bad: !ok, caption: ok ? `Try (${x.join(" ")}). Every row holds.` : `Try (${x.join(" ")}): row${bad.length > 1 ? "s" : ""} ${bad.map(b => b + 1).join(", ")} ${bad.length > 1 ? "go" : "goes"} over the bound.` });
      if (ok) { found = x.slice(); break; }
      for (let j = 0; j < I.n; j++) { if (x[j] === 0) { x[j] = 1; break; } x[j] = 0; }
    }
    const hidden = tried > SHOW ? ` (${tried - SHOW} tries not shown)` : "";
    frames.push(found ? { x: found, done: true, ok: true, caption: `(${found.join(" ")}) satisfies every row, found on try ${tried}${hidden}.` }
      : { x: Array(I.n).fill(null), done: true, ok: false, caption: `All ${tried} assignments break some row${hidden}. No 0-1 solution exists.` });
    return { frames, ok: !!found, solution: found };
  }
  function solveIPBacktrack(I) {
    // Assign x1, x2, … in order, trying 0 before 1. Prune as soon as some row can't be met
    // even with the best choices for the variables still open.
    const frames = [{ x: Array(I.n).fill(null), caption: "Step 0: nothing assigned yet. Set one variable at a time and undo as soon as a row can't be met." }];
    const x = Array(I.n).fill(null);
    let count = 0, found = null;
    function rec(j) {
      if (j === I.n) return true;
      for (const v of [0, 1]) {
        x[j] = v;
        const rs = ipRowState(I, x), bad = rs.findIndex(r => r.st === "bad");
        if (++count <= SHOW) frames.push({ x: x.slice(), cur: j, badRow: bad, bad: bad >= 0,
          caption: bad >= 0 ? `Set x${j + 1} = ${v}: row ${bad + 1} can't be met any more (its smallest possible total is ${rs[bad].min}, above ${I.d[bad]}). Undo.` : `Set x${j + 1} = ${v}. Every row can still be met.` });
        if (bad < 0 && rec(j + 1)) return true;
      }
      x[j] = null;
      return false;
    }
    if (rec(0)) found = x.slice();
    const hidden = count > SHOW ? ` (${count - SHOW} steps not shown)` : "";
    frames.push(found ? { x: found, done: true, ok: true, caption: `(${found.join(" ")}) satisfies every row after ${count} assignments${hidden}. Brute force would check up to ${2 ** I.n}.` }
      : { x: Array(I.n).fill(null), done: true, ok: false, caption: `Every branch was cut off${hidden}. No 0-1 solution exists.` });
    return { frames, ok: !!found, solution: found };
  }

  function solveDFA(A) {
    // Redux's DFA Simulation, shown one row at a time. Unlike Redux, a missing transition drops into Garbage
    // (the decided convention) instead of silently ending the table.
    const rows = [{ step: 0, read: "—", from: "—", to: A.start, acc: A.accept.has(A.start) }];
    let cur = A.start, garbage = false;
    A.input.forEach((ch, i) => {
      const e = garbage ? null : A.trans.find(t => t.from === cur && t.sym === ch);
      const from = garbage ? "Garbage" : cur;
      if (!e) garbage = true; else cur = e.to;
      rows.push({ step: i + 1, read: ch, from, to: garbage ? "Garbage" : cur, acc: !garbage && A.accept.has(cur), garbage, fell: !e && from !== "Garbage" });
    });
    const frames = rows.map((r, k) => ({ rows: k + 1, cur: k, bad: !!r.garbage,
      caption: k === 0 ? `Start in ${A.start}${r.acc ? ", an accept state" : ""}.`
        : r.fell ? `Read "${r.read}": ${r.from} has no "${r.read}" transition, so the machine falls into Garbage.`
        : r.garbage ? `Read "${r.read}": Garbage has no way out.`
        : `Read "${r.read}": ${r.from} → ${r.to}${r.acc ? ", an accept state" : ""}.` }));
    const last = rows[rows.length - 1], ok = !last.garbage && last.acc;
    frames.push({ rows: rows.length, done: true, ok,
      caption: ok ? `Input finished in ${last.to}, an accept state. Accepted.` : last.garbage ? "Input finished in Garbage. Rejected." : `Input finished in ${last.to}, which is not an accept state. Rejected.` });
    return { frames, rows, ok };
  }
  function nfaClosure(A, set) {
    const res = new Set(set), used = [], stack = [...set];
    while (stack.length) { const s = stack.pop(); for (const e of A.trans) if (e.from === s && e.sym === "ε") { used.push(e); if (!res.has(e.to)) { res.add(e.to); stack.push(e.to); } } }
    return { res, used };
  }
  const fmtSet = s => "{" + [...s].join(", ") + "}";
  function solveNFASubset(A) {
    const c0 = nfaClosure(A, [A.start]);
    const rows = [{ step: 0, read: "—", before: "—", moves: c0.used.map(e => `${e.from} ⇢ ${e.to}`).join(", ") || "—", after: fmtSet(c0.res), acc: [...c0.res].some(s => A.accept.has(s)), dead: false }];
    let cur = c0.res;
    for (let i = 0; i < A.input.length && cur.size; i++) {
      const ch = A.input[i], next = new Set(), moves = [];
      cur.forEach(s => A.trans.forEach(e => { if (e.from === s && e.sym === ch) { next.add(e.to); moves.push(`${e.from} –${ch}→ ${e.to}`); } }));
      const cl = nfaClosure(A, next);
      cl.used.forEach(e => moves.push(`${e.from} ⇢ ${e.to}`));
      rows.push({ step: i + 1, read: ch, before: fmtSet(cur), moves: moves.join(", ") || "none", after: cl.res.size ? fmtSet(cl.res) : "∅", acc: [...cl.res].some(s => A.accept.has(s)), dead: !cl.res.size });
      cur = cl.res;
    }
    const frames = rows.map((r, k) => ({ rows: k + 1, cur: k, bad: r.dead,
      caption: k === 0 ? `Start in ${A.start}${c0.used.length ? " and follow ε moves" : ""}: active ${r.after}.`
        : r.dead ? `Read "${r.read}": no active state has a "${r.read}" move, so every branch dies.`
        : `Read "${r.read}": active states are now ${r.after}.` }));
    const finished = rows.length === A.input.length + 1 && !rows[rows.length - 1].dead, ok = finished && rows[rows.length - 1].acc;
    frames.push({ rows: rows.length, done: true, ok,
      caption: !finished ? "Every branch died before the end of the input. Rejected." : ok ? `Input finished with ${rows[rows.length - 1].after} active, and at least one of them accepts. Accepted.` : `Input finished with ${rows[rows.length - 1].after} active. None of them accept. Rejected.` });
    return { frames, rows, ok };
  }
  function nfaRuns(A) {
    // Same depth-first search as Redux's NFA Backtracking GetPathRuns: ε moves first, then symbol moves,
    // in instance order; accepting runs listed first, then dead ends.
    const runs = [], n = A.input.length;
    function dfs(state, pos, path, trs, seen) {
      const accHere = pos >= n && A.accept.has(state);
      if (accHere) runs.push({ states: path.slice(), trs: trs.slice(), accepted: true });
      let expanded = false;
      for (const e of A.trans.filter(t => t.from === state && t.sym === "ε")) {
        const k = e.to + SEP + pos; if (seen.has(k)) continue;
        seen.add(k); path.push(e.to); trs.push(e); expanded = true;
        dfs(e.to, pos, path, trs, seen);
        trs.pop(); path.pop(); seen.delete(k);
      }
      if (pos < n) for (const e of A.trans.filter(t => t.from === state && t.sym === A.input[pos])) {
        const k = e.to + SEP + (pos + 1); if (seen.has(k)) continue;
        seen.add(k); path.push(e.to); trs.push(e); expanded = true;
        dfs(e.to, pos + 1, path, trs, seen);
        trs.pop(); path.pop(); seen.delete(k);
      }
      if (!expanded && !accHere) runs.push({ states: path.slice(), trs: trs.slice(), accepted: false });
    }
    dfs(A.start, 0, [A.start], [], new Set([A.start + SEP + 0]));
    return runs.filter(r => r.accepted).concat(runs.filter(r => !r.accepted)).slice(0, 40);
  }
  function solveNFARuns(A) {
    const runs = nfaRuns(A);
    const frames = runs.map((r, i) => ({ run: i, bad: !r.accepted, caption: `Run ${i + 1} of ${runs.length}: ${[r.states[0], ...r.trs.map(t => (t.sym === "ε" ? "⇢ " : "→ ") + t.to)].join(" ")}. ${r.accepted ? "Accepted" : "Rejected"}.` }));
    const ok = runs.some(r => r.accepted);
    frames.push({ summary: true, done: true, ok, caption: `${runs.length} runs explored, ${runs.filter(r => r.accepted).length} accepting. ${ok ? "Accepted" : "Rejected"}.` });
    return { frames, runs, ok };
  }

  function solveDijkstra(G) {
    const dist = new Map(G.nodes.map(n => [n, Infinity])), prev = new Map(), order = new Map();
    dist.set(G.s, 0);
    const snap = () => ({ dist: new Map(dist), prev: new Map(prev), order: new Map(order) });
    const frames = [{ ...snap(), changed: [G.s], caption: `Start: ${G.s} costs 0, every other node ∞ until a route is found.` }];
    let k = 0;
    for (;;) {
      let u = null;
      for (const n of G.nodes) if (!order.has(n) && dist.get(n) < Infinity && (u === null || dist.get(n) < dist.get(u))) u = n;
      if (u === null) break;
      order.set(u, ++k);
      const changed = [];
      for (const { to, w } of G.adj.get(u)) {
        if (order.has(to)) continue;
        if (dist.get(u) + w < dist.get(to)) { dist.set(to, dist.get(u) + w); prev.set(to, u); changed.push(to); }
      }
      const stop = G.t !== null && u === G.t;
      frames.push({ ...snap(), cur: u, changed, stop,
        caption: `Settle ${u} at cost ${dist.get(u)}${changed.length ? `. Cheaper routes found: ${changed.map(c => `${c} → ${dist.get(c)} via ${u}`).join(", ")}` : ". No neighbor gets cheaper"}.${stop ? ` ${u} is the target, so Dijkstra stops here.` : ""}` });
      if (stop) break;
    }
    const pathTo = (n, P) => { if (P.dist.get(n) === Infinity) return null; const p = [n]; while (p[0] !== G.s) p.unshift(P.prev.get(p[0])); return p; };
    const last = frames[frames.length - 1];
    let ok, caption;
    if (G.t !== null) {
      const p = pathTo(G.t, last);
      ok = !!p && last.order.has(G.t);
      caption = ok ? `Shortest path ${p.join(" → ")}, cost ${last.dist.get(G.t)}.` : `${G.t} can't be reached from ${G.s}.`;
    } else {
      const unr = G.nodes.filter(n => last.dist.get(n) === Infinity);
      ok = true;
      caption = `Every reachable node is settled.${unr.length ? ` ${unr.join(", ")} can't be reached from ${G.s}.` : ""}`;
    }
    frames.push({ ...snap(), done: true, ok, caption });
    return { frames, ok, pathTo };
  }

  /* ======================= catalog ======================= */
  const AUTO_DFA = "(({1,2,3},{a,b},{(1,a,2),(1,b,1),(2,a,2),(2,b,3),(3,a,2),(3,b,1)},1,{3}),abaab)";
  const PROBLEMS = {
    EDITDISTANCE: { label: "Edit Distance", kind: "ed", cls: "P", standalone: true,
      def: "Find the minimum number of operations (insertion, deletion, substitution) required to transform one string into another.",
      input: "(x, y)", vizDef: "A dynamic-programming grid with x down the side and y across the top. Each cell is filled from its three neighbors, then the cheapest path back from the corner spells out the edits.",
      solvers: [["dp", "Edit Distance Dynamic Programming"]],
      examples: [["Redux default", "(horse, ros)"], ["kitten to sitting", "(kitten, sitting)"], ["One string empty", "(abc, )"]] },
    INTPROGRAMMING01: { label: "0-1 Integer Programming", kind: "ip", cls: "NP-Complete", standalone: true,
      def: "0-1 Integer Programming is a system of inequalities, where each variable can be either a 0 or a 1. It is represented by a matrix, where each column is a variable, and each row is an inequality. In this implementation the inequality is always <=. A problem is 0-1 integer programmable if each variable has an assignment of 0 or 1 such that each inequality is satisfiable.",
      input: "(row1),…,(rowM)<=(d1 … dM)", vizDef: "The constraint matrix: one row per inequality, one column per variable. The assignment sits on top, the coefficients it switches on are shaded, and each row's total is compared with its bound.",
      solvers: [["brute", "Integer Programming Brute Force"], ["bt", "Backtracking with bound pruning (not in Redux yet)"]],
      examples: [["Redux default", "(-1 1 -1),(0 0 -1),(-1 -1 1)<=(0 0 0)"],
                 ["Needs several tries", "(-1 -1 -1 -1),(1 1 0 0),(0 0 1 1),(1 0 1 0)<=(-2 1 1 1)"],
                 ["No 0-1 solution", "(-1 -1 0),(1 1 0),(0 0 1)<=(-2 1 1)"]] },
    DFA: { label: "DFA Acceptance", kind: "dfa", cls: "P",
      def: "Acceptance Problem of a DFA is a problem that aims to see if a string input will be accepted by a particular Deterministic Finite Automata model.",
      input: "((States, Alphabet, Transitions, Start, Accept), input)", vizDef: "A trace table: one row per symbol read, revealed as the machine reads it. The current row is highlighted and the last row shows accept or reject.",
      solvers: [["sim", "DFA Simulation"]],
      examples: [["Ends in ab, input abaab", AUTO_DFA], ["Missing transition, input abba", "(({1,2},{a,b},{(1,a,2),(2,b,2)},1,{2}),abba)"],
                 ["Redux default", "(({1,2,3},{a,b},{(1,a,2),(1,b,3),(2,a,2),(2,b,2),(3,a,2),(3,b,3)},1,{2}),a)"]] },
    NFA: { label: "NFA Acceptance", kind: "nfa", cls: "P",
      def: "Acceptance Problem of a NFA is a problem that aims to see if a string input will be accepted by a particular Non-deterministic Finite Automata model.",
      input: "((States, Alphabet, Transitions, Start, Accept), input)", vizDef: "A trace table. By default each row is one symbol read and lists every active state at once. The other option shows one complete run per table, as Redux does today.",
      solvers: [["subset", "All branches at once (not in Redux yet)"], ["runs", "NFA Backtracking, one run per table"]],
      examples: [["Contains ab, with ε", "(({1,2,3,4},{a,b},{(1,a,1),(1,b,1),(1,a,2),(2,b,3),(3,ε,4),(4,a,4),(4,b,4)},1,{4}),babb)"],
                 ["Redux default", "(({1,2,3},{a,b},{(1,a,2),(1,ε,2),(1,b,3),(2,a,2),(2,ε,3),(2,b,2),(3,a,2),(3,b,3)},1,{2}),a)"]] },
    SSSP: { label: "Single Source Shortest Path Problem", kind: "sssp", cls: "P",
      def: "Single Source Shortest Path (SSSP) in a weighted graph is the problem of determining the shortest path from a source vertex to all other reachable vertices in the graph such that the sum of edge weights along each path is minimized.",
      input: "(N, E, s)", vizDef: "Dijkstra's table: one row per node with its cost, the node it's reached through, and its path. Each step settles one node; the costs it lowers are marked.",
      solvers: [["dijkstra", "Dijkstra's Algorithm"]],
      examples: [["Redux default", "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1)"],
                 ["A node that can't be reached", "({1,2,3,4,5,6},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9),((6,1),1)},1)"]] },
    SPSP: { label: "Single Pair Shortest Path Problem", kind: "spsp", cls: "P",
      def: "Single Pair Shortest Path (SPSP) in a weighted graph is the problem of finding the shortest path from a given source vertex s and target vertex t in the graph, such that the sum of edge weights along the path is minimized.",
      input: "(N, E, s, t)", vizDef: "Dijkstra's table, stopping as soon as the target is settled. The target's row shows the answer.",
      solvers: [["dijkstra", "Dijkstra's Algorithm"]],
      examples: [["Redux default", "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1,5)"],
                 ["Stops before settling everything", "({s,a,b,c,t},{((s,a),1),((a,t),2),((s,b),2),((b,c),5),((c,t),9)},s,t)"]] },
  };

  function parse(str, key) {
    const k = PROBLEMS[key].kind;
    if (k === "ed") return parseEditDistance(str);
    if (k === "ip") return parseIP(str);
    if (k === "dfa" || k === "nfa") return parseAutomaton(str, k);
    return parsePaths(str, k === "spsp");
  }

  /* ======================= view ======================= */
  function create({ host }) {
    if (!document.getElementById("tb-style")) {
      const st = document.createElement("style"); st.id = "tb-style"; st.textContent = css; document.head.appendChild(st);
    }
    host.classList.add("tb-wrap");
    let key, kind, I, run, frame = null;

    function table(cols, rowsHtml) {
      return `<div class="tb-scroll"><table class="tb-table"><thead><tr>${cols.map(c => `<th>${c}</th>`).join("")}</tr></thead><tbody>${rowsHtml}</tbody></table></div>`;
    }
    const pill = (txt, cls) => `<span class="tb-pill ${cls || ""}">${esc(txt)}</span>`;

    function drawED(f) {
      const { x, y } = I, m = x.length, n = y.length, C = 44, ox = 74, oy = 74;
      const W = Math.max(ox + (n + 1) * C + 24, 360), H = oy + (m + 1) * C + 20;
      host.innerHTML = "";
      const svg = mk("svg", { class: "tb-svg", viewBox: `0 0 ${W} ${H}`, role: "img", "aria-label": `Edit distance grid for ${x || "an empty string"} and ${y || "an empty string"}` }, host);
      const defs = mk("defs", {}, svg);
      for (const k of ["hl", "sol"]) { const mm = mk("marker", { id: `tb-arr-${k}`, viewBox: "0 0 10 10", refX: 9, refY: 5, markerWidth: 7, markerHeight: 7, orient: "auto" }, defs); mk("path", { d: "M0,0 L10,5 L0,10 z", class: `tb-m-${k}` }, mm); }
      const cx = j => ox + j * C + C / 2, cy = i => oy + i * C + C / 2;
      mk("text", { x: ox + ((n + 1) * C) / 2, y: 18, class: "tb-axis-name", "text-anchor": "middle" }, svg).textContent = "TARGET  y";
      const side = mk("text", { x: 16, y: oy + ((m + 1) * C) / 2, class: "tb-axis-name", "text-anchor": "middle", transform: `rotate(-90 16 ${oy + ((m + 1) * C) / 2})` }, svg); side.textContent = "SOURCE  x";
      const cur = f && !f.done ? f.cur : null;
      mk("text", { x: cx(0), y: oy - 22, class: "tb-axis eps" }, svg).textContent = "ε";
      [...y].forEach((ch, j) => mk("text", { x: cx(j + 1), y: oy - 22, class: "tb-axis" + (cur && cur[1] === j + 1 ? " hl" : "") }, svg).textContent = ch);
      mk("text", { x: ox - 24, y: cy(0), class: "tb-axis eps" }, svg).textContent = "ε";
      [...x].forEach((ch, i) => mk("text", { x: ox - 24, y: cy(i + 1), class: "tb-axis" + (cur && cur[0] === i + 1 ? " hl" : "") }, svg).textContent = ch);
      const onPath = new Set(f && f.done ? f.path.map(([i, j]) => i + "," + j) : []);
      let src = new Set(), best = null;
      if (cur) {
        const [i, j] = cur;
        if (f.op === "keep") { best = [i - 1, j - 1]; src.add((i - 1) + "," + (j - 1)); }
        else { src = new Set([(i - 1) + "," + (j - 1), (i - 1) + "," + j, i + "," + (j - 1)]); best = f.op === "sub" ? [i - 1, j - 1] : f.op === "del" ? [i - 1, j] : [i, j - 1]; }
      }
      const filled = f ? f.filled : -1;
      for (let i = 0; i <= m; i++) for (let j = 0; j <= n; j++) {
        const base = i === 0 || j === 0, k = (i - 1) * n + (j - 1) + 1;
        const show = f && (base || k <= filled);
        let cls = "tb-cell" + (base ? " base" : "") + (show ? "" : " empty");
        if (cur && cur[0] === i && cur[1] === j) cls += " Active";
        else if (onPath.has(i + "," + j)) cls += " Solution";
        else if (src.has(i + "," + j)) cls += best && best[0] === i && best[1] === j ? " src best" : " src";
        const g = mk("g", { class: cls }, svg);
        mk("rect", { x: ox + j * C + 1, y: oy + i * C + 1, width: C - 2, height: C - 2, rx: 6 }, g);
        mk("text", { x: cx(j), y: cy(i) }, g).textContent = run.dp[i][j];
      }
      const arrow = (a, b, cls, mark) => {
        const [i1, j1] = a, [i2, j2] = b, x1 = cx(j1), y1 = cy(i1), x2 = cx(j2), y2 = cy(i2);
        const dx = x2 - x1, dy = y2 - y1, L = Math.hypot(dx, dy), sh = 15;
        mk("line", { x1: x1 + dx / L * sh, y1: y1 + dy / L * sh, x2: x2 - dx / L * (sh + 2), y2: y2 - dy / L * (sh + 2), class: "tb-arrow " + cls, "marker-end": `url(#tb-arr-${mark})` }, svg);
      };
      if (cur) [...src].forEach(s => { const [i, j] = s.split(",").map(Number); arrow([i, j], cur, best[0] === i && best[1] === j ? "" : "alt", "hl"); });
      if (f && f.done) {
        const box = document.createElement("div"); box.className = "tb-align";
        const line = (lab, cells, extra) => `<div class="tb-ln ${extra || ""}"><span class="tb-lab">${lab}</span>${cells.join("")}</div>`;
        const ops = f.ops, cell = (txt, cls) => `<span class="tb-c ${cls}">${txt}</span>`;
        box.innerHTML = line("x", ops.map(o => cell(o.a === null ? "–" : esc(o.a), o.a === null ? "tb-gap" : o.op === "keep" ? "tb-k" : "")))
          + line("edit", ops.map(o => cell({ keep: "·", sub: "S", del: "D", ins: "I" }[o.op], o.op === "keep" ? "tb-k" : "tb-" + o.op[0])), "tb-op")
          + line("y", ops.map(o => cell(o.b === null ? "–" : esc(o.b), o.b === null ? "tb-gap" : o.op === "keep" ? "tb-k" : "")));
        host.appendChild(box);
        const note = document.createElement("div"); note.className = "tb-note";
        note.textContent = "S substitute · D delete from x · I insert from y · dots are letters kept as they are";
        host.appendChild(note);
      }
    }

    function drawIP(f) {
      const x = f ? f.x : Array(I.n).fill(null);
      const rs = ipRowState(I, x);
      const cur = f && !f.done ? f.cur : undefined, trial = f && f.trial;
      const cols = ["", ...Array.from({ length: I.n }, (_, j) => `x<sub>${j + 1}</sub>`), "Total", "", "Bound", "Row holds?"];
      const xr = `<tr><td class="mut">x =</td>${x.map((v, j) => `<td><span class="tb-x ${v === null ? "unset" : v ? "one" : "zero"}${cur === j ? " cur" : ""}${trial ? " trial" : ""}">${v === null ? "?" : v}</span></td>`).join("")}<td></td><td></td><td></td><td></td></tr>`;
      const body = I.C.map((row, r) => {
        const s = rs[r], st = f ? s.st : "open";
        const rowCls = !f ? "" : st === "bad" ? "bad" : f.done && f.ok ? "ok" : "";
        const total = !f || x.every(v => v === null) ? "—" : x.some(v => v === null) ? `${s.lhs}${s.min !== s.max ? ` <span class="tb-note">(${s.min}…${s.max})</span>` : ""}` : String(s.lhs);
        const verdict = !f ? pill("—") : st === "ok" ? pill("yes", "yes") : st === "bad" ? pill(x.some(v => v === null) ? "can't" : "no", "no") : pill("open", "open");
        return `<tr class="${rowCls}"><td class="mut">row ${r + 1}</td>${row.map((c, j) => `<td class="num ${c === 0 ? "zero" : x[j] === 1 ? "on" : ""}">${c}</td>`).join("")}<td class="num">${total}</td><td class="mut">≤</td><td class="num">${I.d[r]}</td><td>${verdict}</td></tr>`;
      }).join("");
      host.innerHTML = table(cols, xr + body) + `<div class="tb-note">Shaded coefficients belong to variables set to 1; they're what each row adds up. A range in Total is the smallest and largest it can still reach.</div>`;
    }

    function drawDFA(f) {
      const rows = run.rows, upto = f ? f.rows : 1;
      const body = rows.slice(0, upto).map((r, k) => {
        const last = k === upto - 1;
        let cls = "";
        if (f && f.done && last) cls = f.ok ? "ok" : "bad";
        else if (r.garbage) cls = "bad";
        else if (f && !f.done && k === f.cur) cls = "cur";
        return `<tr class="${cls}"><td class="num">${r.step}</td><td>${esc(r.read)}</td><td>${esc(r.from)}</td><td class="${f && !f.done && k === f.cur ? "chg" : ""}">${esc(r.to)}</td><td>${r.garbage ? pill("no", "no") : r.acc ? pill("yes", "yes") : pill("no")}</td></tr>`;
      }).join("");
      host.innerHTML = table(["Step", "Read", "From", "To", "Accepting?"], body)
        + `<div class="tb-note">Input: ${I.input.length ? I.input.map((c, i) => i < upto - 1 ? `<s>${esc(c)}</s>` : `<b>${esc(c)}</b>`).join(" ") : "empty"}</div>`;
    }
    function drawNFASubset(f) {
      const rows = run.rows, upto = f ? f.rows : 1;
      const body = rows.slice(0, upto).map((r, k) => {
        const last = k === upto - 1, cls = f && f.done && last ? (f.ok ? "ok" : "bad") : r.dead ? "bad" : f && !f.done && k === f.cur ? "cur" : "";
        const now = f && !f.done && k === f.cur;
        return `<tr class="${cls}"><td class="num">${r.step}</td><td>${esc(r.read)}</td><td>${esc(r.before)}</td><td class="${now ? "chg" : ""}">${esc(r.moves)}</td><td class="${now ? "chg" : ""}">${esc(r.after)}</td><td>${r.dead ? pill("no", "no") : r.acc ? pill("yes", "yes") : pill("no")}</td></tr>`;
      }).join("");
      host.innerHTML = table(["Step", "Read", "Active before", "Moves taken", "Active after", "Any accept?"], body)
        + `<div class="tb-note">→ reads a symbol, ⇢ is an ε move. Every branch runs at once, so each row is one symbol for all of them.</div>`;
    }
    function drawNFARuns(f) {
      if (!f || f.summary) {
        const body = run.runs.map((r, i) => `<tr class="${f ? (r.accepted ? "ok" : "") : ""}"><td class="num">${i + 1}</td><td>${esc([r.states[0], ...r.trs.map(t => (t.sym === "ε" ? "⇢ " : "→ ") + t.to)].join(" "))}</td><td>${r.accepted ? pill("accepted", "yes") : pill("rejected", "no")}</td></tr>`).join("");
        host.innerHTML = table(["Run", "Path", "Result"], body) + `<div class="tb-note">${run.runs.length} runs from Redux's depth-first search: accepting runs first, then dead ends.</div>`;
        return;
      }
      const r = run.runs[f.run];
      const rows = [{ step: 0, read: "—", from: "—", to: r.states[0] }, ...r.trs.map((t, i) => ({ step: i + 1, read: t.sym, from: t.from, to: t.to }))];
      const body = rows.map((row, k) => {
        const last = k === rows.length - 1;
        return `<tr class="${last ? (r.accepted ? "ok" : "bad") : ""}"><td class="num">${row.step}</td><td>${esc(row.read)}</td><td>${esc(row.from)}</td><td>${esc(row.to)}</td><td>${I.accept.has(row.to) ? pill("yes", "yes") : pill("no")}</td></tr>`;
      }).join("");
      host.innerHTML = `<div class="tb-note"><b>Run ${f.run + 1} of ${run.runs.length}</b> · ${r.accepted ? "Accepted" : "Rejected"}</div>` + table(["Step", "Read", "From", "To", "Accepting?"], body);
    }
    function drawPaths(f) {
      const F = f || run.frames[0];
      const changed = new Set(f && !f.done ? f.changed : []);
      const body = I.nodes.map(n => {
        const d = F.dist.get(n), settled = F.order.has(n), path = d === Infinity ? null : run.pathTo(n, F);
        let cls = "";
        if (!f) cls = "";
        else if (f.done && kind === "spsp" && n === I.t) cls = f.ok ? "ok" : "bad";
        else if (f.done && d === Infinity) cls = "unr";
        else if (!f.done && n === f.cur) cls = "cur";
        const tag = (n === I.s ? " <span class=\"tb-note\">source</span>" : "") + (n === I.t ? " <span class=\"tb-note\">target</span>" : "");
        const show = !!f;
        return `<tr class="${cls}"><td><b>${esc(n)}</b>${tag}</td><td>${show && settled ? pill("#" + F.order.get(n), "yes") : show ? pill(f && f.done && d === Infinity ? "unreachable" : "not yet") : ""}</td><td class="num ${changed.has(n) ? "chg" : ""} ${show && !settled ? "tent" : ""}">${show ? (d === Infinity ? "∞" : d) : n === I.s ? 0 : "∞"}</td><td class="${changed.has(n) ? "chg" : ""} ${show && !settled ? "tent" : ""}">${show && F.prev.has(n) ? esc(F.prev.get(n)) : "—"}</td><td class="${show && !settled ? "tent" : ""}">${show && path ? esc(path.join(" → ")) : "—"}</td></tr>`;
      }).join("");
      host.innerHTML = table(["Node", "Settled", "Cost", "Via", "Path"], body)
        + `<div class="tb-note">Italic costs are tentative until the node is settled. Underlined cells changed this step.</div>`;
    }

    function draw() {
      const f = frame;
      if (kind === "ed") drawED(f);
      else if (kind === "ip") drawIP(f);
      else if (kind === "dfa") drawDFA(f);
      else if (kind === "nfa") (run.mode === "runs" ? drawNFARuns : drawNFASubset)(f);
      else drawPaths(f);
    }

    function checks(f) {
      if (kind === "ed") {
        const total = I.x.length * I.y.length;
        const out = [{ label: `Cells filled: ${f ? Math.min(f.filled, total) : 0} / ${total}`, ok: f ? f.filled >= total : null }];
        if (f && f.done) out.push({ label: `Edit distance: ${run.value}`, ok: true });
        return out;
      }
      if (kind === "ip") {
        if (f && f.done && !f.ok) return [{ label: `No 0-1 assignment satisfies all ${I.m} rows`, ok: false }];
        const x = f ? f.x : Array(I.n).fill(null), rs = ipRowState(I, x);
        const set = x.filter(v => v !== null).length, holds = rs.filter(r => r.st === "ok").length;
        return [
          { label: `Variables set: ${set} / ${I.n}`, ok: f ? set === I.n : null },
          { label: `Rows that hold: ${set ? holds : 0} / ${I.m}`, ok: f && set ? holds === I.m : null },
        ];
      }
      if (kind === "dfa") {
        const read = f ? f.rows - 1 : 0;
        const out = [{ label: `Symbols read: ${read} / ${I.input.length}`, ok: f ? read === I.input.length : null }];
        if (f && f.done) out.push({ label: f.ok ? "Ends in an accept state" : "Doesn't end in an accept state", ok: f.ok });
        return out;
      }
      if (kind === "nfa") {
        if (run.mode === "runs") {
          const acc = run.runs.filter(r => r.accepted).length;
          return [{ label: `Runs explored: ${run.runs.length}`, ok: null }, { label: `Accepting runs: ${acc}`, ok: f ? acc > 0 : null }];
        }
        const read = f ? f.rows - 1 : 0;
        const out = [{ label: `Symbols read: ${read} / ${I.input.length}`, ok: f ? read === I.input.length : null }];
        if (f && f.done) out.push({ label: f.ok ? "Some branch accepts" : "No branch accepts", ok: f.ok });
        return out;
      }
      const F = f || run.frames[0], settled = F.order.size;
      const out = [{ label: `Nodes settled: ${f ? settled : 0} / ${I.nodes.length}`, ok: null }];
      if (f && f.done && kind === "spsp") out.push({ label: f.ok ? `Shortest cost to ${I.t}: ${F.dist.get(I.t)}` : `${I.t} unreachable`, ok: f.ok });
      if (f && f.done && kind === "sssp") { const u = I.nodes.filter(n => F.dist.get(n) === Infinity).length; out.push({ label: `Unreachable nodes: ${u}`, ok: null }); }
      return out;
    }
    function chosen(f) {
      if (!f) return [];
      if (kind === "ed") {
        if (!f.done) return [];
        const c = t => f.ops.filter(o => o.op === t);
        return [{ text: `Distance ${run.value}` }, ...c("sub").map(o => ({ text: `${o.a}→${o.b}` })), ...c("del").map(o => ({ text: `delete ${o.a}` })), ...c("ins").map(o => ({ text: `insert ${o.b}` }))];
      }
      if (kind === "ip") return f.x.some(v => v !== null) ? [{ text: `(${f.x.map(v => (v === null ? "?" : v)).join(" ")})` }] : [];
      if (kind === "dfa") return [{ text: run.rows.slice(0, f.rows).map(r => r.to).join(" → ") }];
      if (kind === "nfa") {
        if (run.mode === "runs") { if (f.summary) return run.runs.filter(r => r.accepted).slice(0, 3).map(r => ({ text: r.states.join(" → ") })); const r = run.runs[f.run]; return [{ text: r.states.join(" → ") }]; }
        return [{ text: run.rows[f.rows - 1].after }];
      }
      if (kind === "spsp") { const p = f.done ? run.pathTo(I.t, f) : null; return p ? [{ text: `${p.join(" → ")} (${f.dist.get(I.t)})` }] : []; }
      return I.nodes.filter(n => f.order.has(n)).map(n => ({ text: `${n}: ${f.dist.get(n)}` }));
    }

    return {
      load(str, k, solver) {
        const P = PROBLEMS[k];
        key = k; kind = P.kind; I = parse(str, k);
        const sv = solver && P.solvers.some(s => s[0] === solver) ? solver : P.solvers[0][0];
        if (kind === "ed") run = solveEditDistance(I);
        else if (kind === "ip") run = sv === "bt" ? solveIPBacktrack(I) : solveIPBrute(I);
        else if (kind === "dfa") run = solveDFA(I);
        else if (kind === "nfa") { run = sv === "runs" ? solveNFARuns(I) : solveNFASubset(I); run.mode = sv; }
        else run = solveDijkstra({ ...I });
        frame = null; draw();
        return { frames: run.frames, ok: run.ok };
      },
      show(i) { frame = i === null ? null : run.frames[i]; draw(); return frame; },
      checks(i) { return checks(i === null ? null : run.frames[i]); },
      chosen(i) { return chosen(i === null ? null : run.frames[i]); },
      resetHover() { draw(); },
    };
  }

  return { create, parse, PROBLEMS };
})();
