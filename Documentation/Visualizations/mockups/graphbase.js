/* ---- Base graph view: one renderer for every plain graph problem ----
   A problem supplies the graph and declares its certificate (subset, partition or tour) plus a rule.
   The base derives every color from the certificate, so problems never write highlight code. */
const GraphView = (() => {
  const NS = "http://www.w3.org/2000/svg";
  const mk = (tag, attrs = {}, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; };
  const SEP = "\u0000", NR = 19, SHOW = 150;
  const key = (a, b) => (a < b ? a + SEP + b : b + SEP + a);

  const css = `
.gb-svg { width: 100%; height: auto; display: block; }
.gb-svg text { font-family: var(--av-mono); }
.gb-node { cursor: pointer; transition: opacity .2s; }
.gb-node:focus { outline: none; }
.gb-node .body { fill: var(--av-bg); stroke: var(--av-stroke); stroke-width: 1.8; transition: fill .2s, stroke .2s; }
.gb-node text { font-size: 13px; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); pointer-events: none; }
.gb-node.Active .body { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; }
.gb-node.Solution .body { fill: var(--av-sol); stroke: var(--av-sol); } .gb-node.Solution text { fill: var(--av-on-sol); font-weight: 600; }
.gb-node.Covered .body { fill: var(--av-sol-fill); stroke: var(--av-sol); stroke-width: 2.2; }
.gb-node.Rejected .body { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 3; } .gb-node.Rejected text { fill: var(--av-rej); }
.gb-node.Blocked .body { fill: transparent; stroke: var(--av-stroke); stroke-dasharray: 4 3; } .gb-node.Blocked text { fill: var(--av-muted); }
.gb-node.c0 .body { fill: var(--av-g0); stroke: var(--av-g0s); } .gb-node.c1 .body { fill: var(--av-g1); stroke: var(--av-g1s); }
.gb-node.c2 .body { fill: var(--av-g2); stroke: var(--av-g2s); } .gb-node.c3 .body { fill: var(--av-g3); stroke: var(--av-g3s); }
.gb-node.c4 .body { fill: var(--av-g4); stroke: var(--av-g4s); } .gb-node.c5 .body { fill: var(--av-g5); stroke: var(--av-g5s); }
.gb-node[class*=" c"] text { fill: #10131a; font-weight: 600; }
.gb-node.ring .body { stroke: var(--av-hl); stroke-width: 3.5; }
.gb-node.badnode .body { stroke: var(--av-rej); stroke-width: 3.5; }
.gb-node.trace .body, .gb-node:focus-visible .body { stroke: var(--av-hot); stroke-width: 3; }
.gb-node.faint { filter: grayscale(1); } .gb-node.faint .body { stroke-dasharray: 3 3; }
.gb-term { fill: none; stroke: var(--av-ink); stroke-width: 1.6; pointer-events: none; }
.gb-order circle { fill: var(--av-ink); stroke: var(--av-surface); stroke-width: 1.5; } .gb-order text { fill: var(--av-surface); font-size: 10.5px; font-weight: 700; text-anchor: middle; dominant-baseline: central; }
.gb-edge { stroke: var(--av-edge); stroke-width: 1.6; transition: opacity .2s, stroke .2s; }
.gb-edge.sol { stroke: var(--av-sol); stroke-width: 3.2; }
.gb-edge.cov { stroke: var(--av-cov); stroke-width: 2; }
.gb-edge.rej { stroke: var(--av-rej); stroke-width: 3.2; }
.gb-edge.hot { stroke: var(--av-hl); stroke-width: 3.2; }
.gb-edge.dim { stroke: var(--av-edge-dim); }
.gb-edge.trace { stroke: var(--av-hot); stroke-width: 2.6; opacity: 1; }
.gb-edge.faint { stroke: var(--av-edge-dim); stroke-dasharray: 3 3; }
.gb-phantom { stroke: var(--av-rej); stroke-width: 2.4; stroke-dasharray: 6 5; fill: none; }
.gb-w rect { fill: var(--av-surface); stroke: var(--av-line); } .gb-w text { font-size: 11.5px; text-anchor: middle; dominant-baseline: central; fill: var(--av-muted); }
.gb-w.sol rect { stroke: var(--av-sol); } .gb-w.sol text { fill: var(--av-sol); font-weight: 700; }
.gb-w.faint { filter: grayscale(1); }
@media (prefers-reduced-motion: reduce) { .gb-node, .gb-node .body, .gb-edge { transition: none; } }`;

  /* ---------- parsing: Redux's ((N,E),K) and (N,E) shapes, weighted edges as ({a,b},w) ---------- */
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
  function parse(str, P) {
    const top = tree(tokenize(str));
    if (top.t !== "tup") throw new Error("The instance must start with (.");
    let g = top, K = null, R = null;
    if (top.items[0] && top.items[0].t === "tup") {
      g = top.items[0];
      const rest = top.items.slice(1);
      if (P.terminals) {
        const r = rest.shift();
        if (!r || r.t !== "set") throw new Error("Steiner Tree needs a set of terminal nodes: ((N,E),R,K).");
        R = r.items;
      }
      if (rest.length) { K = Number(rest[0]); if (!Number.isInteger(K) || K < 0) throw new Error("K must be a whole number."); }
    }
    if (P.terminals && !R) throw new Error("Steiner Tree needs a set of terminal nodes: ((N,E),R,K).");
    if (P.needK && K === null) throw new Error("This problem needs K at the end: ((N,E),K).");
    const [Nn, En] = g.items;
    if (!Nn || Nn.t !== "set" || !En || En.t !== "set") throw new Error("Expected a node set and an edge set: (N,E).");
    const nodes = Nn.items.map(x => { if (typeof x !== "string") throw new Error("Node names must be plain names."); return x; });
    if (!nodes.length || new Set(nodes).size !== nodes.length) throw new Error("N must be a non-empty set with no repeats.");
    if (nodes.length > 16) throw new Error("This mockup draws up to 16 nodes.");
    const S = new Set(nodes), edges = [], seen = new Set();
    for (const e of En.items) {
      let pair, w = null;
      if (e.t === "set") pair = e.items;
      else if (e.t === "tup" && e.items[0] && e.items[0].t === "set") { pair = e.items[0].items; w = Number(e.items[1]); if (!Number.isFinite(w)) throw new Error("Edge weights must be numbers."); }
      else throw new Error("Each edge must look like {a,b}" + (P.weighted ? " or ({a,b},w)." : "."));
      if (pair.length !== 2 || pair.some(x => typeof x !== "string")) throw new Error("Each edge joins exactly two nodes.");
      const [a, b] = pair;
      if (!S.has(a) || !S.has(b)) throw new Error(`Edge {${a},${b}} uses a node that isn't in N.`);
      if (a === b) throw new Error(`Edge {${a},${b}} is a loop.`);
      if (P.weighted && w === null) throw new Error("This problem needs a weight on every edge: ({a,b},w).");
      const k = key(a, b);
      if (seen.has(k)) continue;
      seen.add(k); edges.push({ a, b, w, k });
    }
    const adj = new Map(nodes.map(n => [n, new Set()]));
    edges.forEach(e => { adj.get(e.a).add(e.b); adj.get(e.b).add(e.a); });
    const W = new Map(edges.map(e => [e.k, e.w]));
    let terminals = null;
    if (R) {
      for (const r of R) if (typeof r !== "string" || !S.has(r)) throw new Error(`Terminal ${r} isn't in N.`);
      terminals = new Set(R);
    }
    const byKey = new Map(edges.map(e => [e.k, e]));
    return { nodes, edges, K, adj, W, terminals, byKey, has: (a, b) => adj.get(a).has(b) };
  }

  /* ---------- rules: certificate → states. This is the only problem-specific drawing logic. ---------- */
  const fmt = a => "{" + a.join(",") + "}";
  function evaluate(G, rule, f) {
    const out = { nodes: new Map(), edges: new Map(), phantom: [], checks: [] };
    const setN = (n, v) => out.nodes.set(n, Object.assign(out.nodes.get(n) || {}, v));
    if (!f) return out;
    const trial = !!f.trial, done = !!f.done, K = G.K;
    if (rule === "clique" || rule === "vc" || rule === "mvc" || rule === "is" || rule === "ds") {
      const S = new Set(f.cert || []);
      S.forEach(n => setN(n, { st: trial || n === f.trying ? "Active" : "Solution" }));
      if (rule === "clique") {
        let missing = 0;
        const arr = [...S];
        G.edges.forEach(e => { if (S.has(e.a) && S.has(e.b)) out.edges.set(e.k, trial ? "hot" : "sol"); else if (S.size) out.edges.set(e.k, "dim"); });
        for (let i = 0; i < arr.length; i++) for (let j = i + 1; j < arr.length; j++) if (!G.has(arr[i], arr[j])) { missing++; out.phantom.push([arr[i], arr[j]]); }
        out.checks.push({ label: `Pairs not joined by an edge: ${missing}`, ok: missing === 0 });
        out.checks.push({ label: `Size: ${S.size} (K = ${K})`, ok: S.size === K });
      } else if (rule === "vc" || rule === "mvc") {
        let unc = 0;
        G.edges.forEach(e => {
          if (S.has(e.a) || S.has(e.b)) out.edges.set(e.k, "cov");
          else if (!f.partial) { out.edges.set(e.k, "rej"); unc++; } else unc++;
        });
        out.checks.push({ label: `Edges covered: ${G.edges.length - unc} / ${G.edges.length}`, ok: unc === 0 });
        if (rule === "vc") out.checks.push({ label: `Size: ${S.size} (K = ${K})`, ok: S.size <= K });
        else { const m = smallestVC(G); out.checks.push({ label: `Size: ${S.size} (smallest possible: ${m})`, ok: S.size === m && unc === 0 }); }
      } else if (rule === "is") {
        let bad = 0;
        G.edges.forEach(e => { if (S.has(e.a) && S.has(e.b)) { out.edges.set(e.k, "rej"); bad++; } else if (S.size) out.edges.set(e.k, "dim"); });
        G.nodes.forEach(n => { if (!S.has(n) && [...G.adj.get(n)].some(m => S.has(m))) setN(n, { st: "Blocked" }); });
        out.checks.push({ label: `Edges inside the set: ${bad}`, ok: bad === 0 });
        out.checks.push({ label: `Size: ${S.size} (K = ${K})`, ok: S.size === K });
      } else {
        let undom = 0;
        G.nodes.forEach(n => {
          if (S.has(n)) return;
          if ([...G.adj.get(n)].some(m => S.has(m))) setN(n, { st: "Covered" });
          else { undom++; if (!f.partial && S.size) setN(n, { st: "Rejected" }); }
        });
        G.edges.forEach(e => { if (S.has(e.a) !== S.has(e.b)) out.edges.set(e.k, "cov"); else if (S.size) out.edges.set(e.k, "dim"); });
        out.checks.push({ label: `Nodes dominated: ${G.nodes.length - undom} / ${G.nodes.length}`, ok: undom === 0 });
        out.checks.push({ label: `Size: ${S.size} (K = ${K})`, ok: S.size <= K });
      }
    } else if (rule === "color") {
      const color = f.color || {};
      let conflicts = 0, used = new Set();
      G.nodes.forEach(n => { if (color[n] !== undefined) { used.add(color[n]); setN(n, { color: color[n] }); } });
      G.edges.forEach(e => {
        if (color[e.a] !== undefined && color[e.a] === color[e.b]) { out.edges.set(e.k, "rej"); conflicts++; }
      });
      if (f.trying) setN(f.trying, f.badNode ? { ring: false, bad: true } : { ring: true });
      const colored = Object.keys(color).length;
      out.checks.push({ label: `Nodes colored: ${colored} / ${G.nodes.length}`, ok: colored === G.nodes.length });
      out.checks.push({ label: `Edges with both ends the same color: ${conflicts}`, ok: conflicts === 0 });
      out.checks.push({ label: `Colors used: ${used.size} (K = ${K})`, ok: used.size <= K });
    } else if (rule === "cut" || rule === "kcut" || rule === "wkcut" || rule === "mincut") {
      const A = new Set(f.cert || []);
      let wsum = 0, count = 0;
      if (f.cert) G.nodes.forEach(n => setN(n, { color: A.has(n) ? 0 : 1 }));
      G.edges.forEach(e => {
        if (!f.cert) return;
        if (A.has(e.a) !== A.has(e.b)) { out.edges.set(e.k, trial ? "hot" : "sol"); wsum += e.w === null ? 1 : e.w; count++; } else out.edges.set(e.k, "dim");
      });
      const twoSides = f.cert ? A.size > 0 && A.size < G.nodes.length : false;
      if (rule === "cut") {
        out.checks.push({ label: `Cut weight: ${wsum}`, ok: f.best === undefined ? null : wsum === f.best });
        if (f.best !== undefined) out.checks.push({ label: `Largest possible: ${f.best}`, ok: true });
      } else if (rule === "kcut") {
        out.checks.push({ label: `Edges crossing the split: ${count} (K = ${K})`, ok: count === K && twoSides });
      } else if (rule === "wkcut") {
        out.checks.push({ label: `Weight crossing the split: ${wsum} (K = ${K})`, ok: wsum === K && twoSides });
      } else {
        const m = smallestCut(G);
        out.checks.push({ label: `Both sides non-empty`, ok: twoSides });
        out.checks.push({ label: `Weight crossing the split: ${wsum} (smallest possible: ${m})`, ok: twoSides && wsum === m });
      }
    } else if (rule === "ccover") {
      const color = f.color || {};
      const groups = {};
      Object.entries(color).forEach(([n, c]) => { setN(n, { color: c }); (groups[c] = groups[c] || []).push(n); });
      let missing = 0;
      Object.values(groups).forEach(g => {
        for (let i = 0; i < g.length; i++) for (let j = i + 1; j < g.length; j++) {
          if (G.has(g[i], g[j])) out.edges.set(key(g[i], g[j]), "sol");
          else { missing++; out.phantom.push([g[i], g[j]]); }
        }
      });
      if (Object.keys(color).length) G.edges.forEach(e => { if (!out.edges.has(e.k)) out.edges.set(e.k, "dim"); });
      if (f.trying) setN(f.trying, f.badNode ? { bad: true } : { ring: true });
      const placed = Object.keys(color).length, used = Object.keys(groups).length;
      out.checks.push({ label: `Nodes placed in a group: ${placed} / ${G.nodes.length}`, ok: placed === G.nodes.length });
      out.checks.push({ label: `Pairs in a group with no edge: ${missing}`, ok: missing === 0 });
      out.checks.push({ label: `Groups used: ${used} (K = ${K})`, ok: used <= K && used > 0 });
    } else if (rule === "mst" || rule === "steiner") {
      const C = new Set(f.edges || []), frontier = new Set(f.frontier || []);
      // union-find over the chosen edges, in the order they were chosen, to spot edges that close a cycle
      const parent = new Map(G.nodes.map(n => [n, n]));
      const find = x => { while (parent.get(x) !== x) { parent.set(x, parent.get(parent.get(x))); x = parent.get(x); } return x; };
      const cycle = new Set();
      let total = 0;
      (f.edges || []).forEach(k => {
        const e = G.byKey.get(k);
        total += e.w === null ? 1 : e.w;
        const ra = find(e.a), rb = find(e.b);
        if (ra === rb) cycle.add(k); else parent.set(ra, rb);
      });
      const touched = new Set();
      C.forEach(k => { const e = G.byKey.get(k); touched.add(e.a); touched.add(e.b); });
      G.edges.forEach(e => {
        if (C.has(e.k)) out.edges.set(e.k, cycle.has(e.k) && (rule === "mst" || !f.partial) ? "rej" : trial ? "hot" : "sol");
        else if (e.k === f.rejectEdge) out.edges.set(e.k, "rej");
        else if (frontier.has(e.k)) out.edges.set(e.k, "hot");
        else if (C.size || frontier.size || f.rejectEdge) out.edges.set(e.k, "dim");
      });
      G.nodes.forEach(n => { if (touched.has(n) || (f.inTree && f.inTree.includes(n))) setN(n, { st: "Covered" }); });
      const roots = new Set(G.nodes.map(find));
      if (rule === "mst") {
        const m = mstWeight(G);
        out.checks.push({ label: `Edges chosen: ${C.size} of ${G.nodes.length - 1} needed`, ok: C.size === G.nodes.length - 1 });
        out.checks.push({ label: `Pieces still separate: ${roots.size}`, ok: roots.size === 1 });
        out.checks.push({ label: `Edges that close a cycle: ${cycle.size}`, ok: cycle.size === 0 });
        out.checks.push({ label: `Total weight: ${total} (smallest possible: ${m})`, ok: total === m && roots.size === 1 && cycle.size === 0 });
      } else {
        const T = [...G.terminals];
        const tRoot = T.length ? find(T[0]) : null;
        const reached = T.filter(t => (touched.has(t) || T.length === 1) && find(t) === tRoot).length;
        if (!f.partial && C.size) T.forEach(t => { if (!touched.has(t) || find(t) !== tRoot) setN(t, { st: "Rejected" }); });
        const pieces = new Set([...touched].map(find)).size;
        out.checks.push({ label: `Terminals joined together: ${reached} / ${T.length}`, ok: reached === T.length });
        out.checks.push({ label: `Chosen edges form one piece`, ok: C.size ? pieces === 1 : null });
        out.checks.push({ label: `Edges used: ${C.size} (K = ${K})`, ok: C.size <= K });
        out.checks.push({ label: `Edges that close a cycle: ${cycle.size}`, ok: cycle.size === 0 ? true : null });
      }
    } else if (rule === "ham" || rule === "tsp") {
      const order = f.cert || [];
      order.forEach((n, i) => setN(n, { st: f.badNode === n ? "Rejected" : n === f.trying ? "Active" : trial ? "Active" : "Solution", order: i + 1 }));
      let missing = 0, total = 0;
      const steps = [];
      for (let i = 0; i + 1 < order.length; i++) steps.push([order[i], order[i + 1]]);
      if (!f.partial && order.length === G.nodes.length && order.length > 2) steps.push([order[order.length - 1], order[0]]);
      const used = new Set();
      steps.forEach(([a, b], i) => {
        if (G.has(a, b)) {
          const k = key(a, b); used.add(k);
          out.edges.set(k, (b === f.trying && i === steps.length - 1 && f.partial) || trial ? "hot" : "sol");
          if (rule === "tsp") total += G.W.get(k);
        } else { missing++; out.phantom.push([a, b]); }
      });
      if (order.length) G.edges.forEach(e => { if (!used.has(e.k)) out.edges.set(e.k, "dim"); });
      const all = new Set(order).size === G.nodes.length && order.length === G.nodes.length;
      out.checks.push({ label: `Visits every ${rule === "tsp" ? "city" : "node"} once: ${new Set(order).size} / ${G.nodes.length}`, ok: all });
      out.checks.push({ label: `Steps that aren't edges: ${missing}`, ok: missing === 0 });
      if (rule === "tsp") out.checks.push({ label: `Total weight: ${total} (K = ${K})`, ok: total <= K && all });
    }
    const empty = !(f.cert && f.cert.length) && !(f.color && Object.keys(f.color).length) && !(f.edges && f.edges.length);
    if (done && !f.ok && empty) out.checks = out.checks.map(c => ({ ...c, ok: false }));
    return out;
  }

  /* ---------- solvers: each emits frames of certificates; the base draws them ---------- */
  function* combos(arr, k, start = 0, acc = []) {
    if (acc.length === k) { yield acc.slice(); return; }
    for (let i = start; i <= arr.length - (k - acc.length); i++) { acc.push(arr[i]); yield* combos(arr, k, i + 1, acc); acc.pop(); }
  }
  const valid = {
    clique: (G, S) => S.every((a, i) => S.slice(i + 1).every(b => G.has(a, b))),
    is: (G, S) => S.every((a, i) => S.slice(i + 1).every(b => !G.has(a, b))),
    vc: (G, S) => { const s = new Set(S); return G.edges.every(e => s.has(e.a) || s.has(e.b)); },
    ds: (G, S) => { const s = new Set(S); return G.nodes.every(n => s.has(n) || [...G.adj.get(n)].some(m => s.has(m))); },
  };
  function why(G, rule, S) {
    const s = new Set(S);
    if (rule === "clique") { for (const a of S) for (const b of S) if (a < b && !G.has(a, b)) return `${a} and ${b} aren't joined`; }
    if (rule === "is") { for (const e of G.edges) if (s.has(e.a) && s.has(e.b)) return `${e.a} and ${e.b} are joined`; }
    if (rule === "vc") { const e = G.edges.find(e => !s.has(e.a) && !s.has(e.b)); if (e) return `edge {${e.a},${e.b}} isn't covered`; }
    if (rule === "ds") { const n = G.nodes.find(n => !s.has(n) && ![...G.adj.get(n)].some(m => s.has(m))); if (n !== undefined) return `${n} isn't dominated`; }
    return "";
  }
  function subsetBrute(G, rule) {
    const frames = [];
    const sizes = rule === "clique" || rule === "is" ? [G.K] : Array.from({ length: Math.min(G.K, G.nodes.length) }, (_, i) => i + 1);
    let tried = 0, found = null;
    outer: for (const k of sizes) {
      if (k > G.nodes.length) continue;
      for (const S of combos(G.nodes, k)) {
        tried++;
        const ok = valid[rule](G, S);
        if (tried <= SHOW) frames.push({ cert: S, trial: !ok, caption: ok ? `Try ${fmt(S)}. It works.` : `Try ${fmt(S)}: ${why(G, rule, S)}.` });
        if (ok) { found = S; break outer; }
      }
    }
    const hidden = tried > SHOW ? ` (${tried - SHOW} tries not shown)` : "";
    frames.push(found
      ? { cert: found, done: true, ok: true, caption: `${fmt(found)} works, found after ${tried} tries${hidden}.` }
      : { cert: [], done: true, ok: false, caption: `Checked all ${tried} candidate sets${hidden}. None works.` });
    return frames;
  }
  // Smallest-answer helpers used by checks (cached per graph; graphs here have at most 16 nodes)
  function smallestVC(G) {
    if (G._mvc !== undefined) return G._mvc;
    for (let k = 0; k <= G.nodes.length; k++) for (const S of combos(G.nodes, k)) if (valid.vc(G, S)) return (G._mvc = k);
    return (G._mvc = G.nodes.length);
  }
  function smallestCut(G) {
    if (G._mincut !== undefined) return G._mincut;
    const n = G.nodes.length;
    let best = Infinity;
    for (let mask = 0; mask < 1 << (n - 1); mask++) {
      const s = new Set(G.nodes.filter((_, i) => i === 0 || mask & (1 << (i - 1))));
      if (s.size === n) continue;
      best = Math.min(best, G.edges.reduce((t, e) => t + (s.has(e.a) !== s.has(e.b) ? (e.w === null ? 1 : e.w) : 0), 0));
    }
    return (G._mincut = best);
  }
  function mstWeight(G) {
    if (G._mst !== undefined) return G._mst;
    const parent = new Map(G.nodes.map(n => [n, n]));
    const find = x => { while (parent.get(x) !== x) x = parent.get(x); return x; };
    let t = 0;
    G.edges.slice().sort((a, b) => a.w - b.w).forEach(e => { const ra = find(e.a), rb = find(e.b); if (ra !== rb) { parent.set(ra, rb); t += e.w; } });
    return (G._mst = t);
  }
  function pathInForest(G, chosenKeys, a, b) {
    const adj = new Map(G.nodes.map(n => [n, []]));
    chosenKeys.forEach(k => { const e = G.byKey.get(k); adj.get(e.a).push(e.b); adj.get(e.b).push(e.a); });
    const prev = new Map([[a, null]]), q = [a];
    while (q.length) { const x = q.shift(); if (x === b) break; for (const y of adj.get(x)) if (!prev.has(y)) { prev.set(y, x); q.push(y); } }
    const path = [];
    for (let x = b; x !== null && x !== undefined; x = prev.get(x)) path.unshift(x);
    return path;
  }
  function kruskal(G) {
    const frames = [], chosen = [];
    const parent = new Map(G.nodes.map(n => [n, n]));
    const find = x => { while (parent.get(x) !== x) x = parent.get(x); return x; };
    const sorted = G.edges.slice().sort((a, b) => a.w - b.w);
    for (const e of sorted) {
      if (chosen.length === G.nodes.length - 1) break;
      const ra = find(e.a), rb = find(e.b);
      if (ra === rb) {
        const path = pathInForest(G, chosen, e.a, e.b);
        frames.push({ edges: [...chosen], rejectEdge: e.k, partial: true,
          caption: `Skip {${e.a},${e.b}} (${e.w}): ${path.join(" – ")} already joins them, so it would close a cycle.` });
      } else {
        parent.set(ra, rb); chosen.push(e.k);
        frames.push({ edges: [...chosen], partial: true, caption: `Take {${e.a},${e.b}} (${e.w}), the cheapest edge left that joins two separate pieces.` });
      }
    }
    const ok = chosen.length === G.nodes.length - 1;
    const total = chosen.reduce((t, k) => t + G.W.get(k), 0);
    frames.push({ edges: chosen, done: true, ok, caption: ok ? `${chosen.length} edges connect all ${G.nodes.length} nodes, total weight ${total}.` : "The graph is disconnected, so no spanning tree exists." });
    return frames;
  }
  function prim(G) {
    const frames = [], chosen = [], inTree = [G.nodes[0]];
    while (inTree.length < G.nodes.length) {
      const t = new Set(inTree);
      const frontier = G.edges.filter(e => t.has(e.a) !== t.has(e.b));
      if (!frontier.length) break;
      const best = frontier.reduce((m, e) => (e.w < m.w ? e : m));
      const next = t.has(best.a) ? best.b : best.a;
      frames.push({ edges: [...chosen], frontier: frontier.map(e => e.k), inTree: [...inTree], partial: true,
        caption: `${frontier.length} edges leave the tree. The cheapest is {${best.a},${best.b}} (${best.w}).` });
      chosen.push(best.k); inTree.push(next);
      frames.push({ edges: [...chosen], inTree: [...inTree], partial: true, caption: `Add {${best.a},${best.b}}, bringing ${next} into the tree.` });
    }
    const ok = inTree.length === G.nodes.length;
    const total = chosen.reduce((t, k) => t + G.W.get(k), 0);
    frames.push({ edges: chosen, inTree, done: true, ok, caption: ok ? `The tree reaches all ${G.nodes.length} nodes, total weight ${total}.` : "Some nodes can't be reached, so no spanning tree exists." });
    return frames;
  }
  function steinerBrute(G) {
    const frames = [], T = [...G.terminals];
    const keys = G.edges.map(e => e.k);
    let tried = 0, found = null;
    const check = S => {
      const touched = new Set(), adj = new Map();
      S.forEach(k => { const e = G.byKey.get(k); [e.a, e.b].forEach(x => { touched.add(x); if (!adj.has(x)) adj.set(x, []); }); adj.get(e.a).push(e.b); adj.get(e.b).push(e.a); });
      const missing = T.filter(t => !touched.has(t));
      if (missing.length) return `terminal ${missing.join(", ")} not reached`;
      const start = [...touched][0], seen = new Set([start]), q = [start];
      while (q.length) { const x = q.shift(); for (const y of adj.get(x)) if (!seen.has(y)) { seen.add(y); q.push(y); } }
      return seen.size === touched.size ? "" : "the edges split into separate pieces";
    };
    outer: for (let size = Math.max(1, T.length - 1); size <= Math.min(G.K, keys.length); size++) {
      for (const S of combos(keys, size)) {
        tried++;
        const why = check(S);
        if (tried <= SHOW) frames.push({ edges: S, trial: !!why, caption: why ? `Try ${S.length} edges: ${why}.` : `Try ${S.length} edges. They join every terminal.` });
        if (!why) { found = S; break outer; }
      }
    }
    const hidden = tried > SHOW ? ` (${tried - SHOW} tries not shown)` : "";
    frames.push(found
      ? { edges: found, done: true, ok: true, caption: `${found.length} edges join all ${T.length} terminals, within K = ${G.K}, found after ${tried} tries${hidden}. Smaller sets were tried first.` }
      : { edges: [], done: true, ok: false, caption: `Checked all ${tried} edge sets of up to K = ${G.K} edges${hidden}. None joins every terminal.` });
    return frames;
  }
  function ccoverBacktrack(G) {
    const frames = [], color = {}, order = G.nodes.slice();
    let count = 0;
    function rec(i) {
      if (i === order.length) return true;
      const n = order[i];
      for (let c = 0; c < G.K; c++) {
        const members = Object.keys(color).filter(m => color[m] === c);
        color[n] = c;
        const stranger = members.find(m => !G.has(m, n));
        if (++count <= SHOW) frames.push({ color: { ...color }, trying: n, badNode: stranger !== undefined,
          caption: stranger !== undefined ? `Try ${n} in group ${c + 1}: it has no edge to ${stranger}. Reject.`
            : members.length ? `Put ${n} in group ${c + 1}. It's joined to everyone already there.` : `Start group ${c + 1} with ${n}.` });
        if (stranger === undefined && rec(i + 1)) return true;
        delete color[n];
      }
      return false;
    }
    const ok = rec(0);
    frames.push(ok ? { color: { ...color }, done: true, ok, caption: `Every node sits in a group where all pairs are joined, using at most K = ${G.K} groups.` }
      : { color: {}, done: true, ok, caption: `No way to split the nodes into ${G.K} or fewer cliques.` });
    return frames;
  }
  function splitCutBrute(G, rule) {
    // Mirrors CutBruteForce / WeightedCutBruteForce: try node sets S, smallest first, and measure the edges leaving S.
    const frames = [], n = G.nodes.length, maxSize = rule === "kcut" ? Math.min(G.K, n - 1) : n - 1;
    let tried = 0, found = null;
    outer: for (let size = 1; size <= maxSize; size++) {
      for (const S of combos(G.nodes, size)) {
        tried++;
        const s = new Set(S);
        const v = G.edges.reduce((t, e) => t + (s.has(e.a) !== s.has(e.b) ? (rule === "kcut" ? 1 : e.w) : 0), 0);
        const ok = v === G.K;
        const what = rule === "kcut" ? `${v} edge${v === 1 ? "" : "s"} cross` : `weight ${v} crosses`;
        if (tried <= SHOW) frames.push({ cert: S, trial: !ok, caption: ok ? `Split off ${fmt(S)}: ${what}. That's K.` : `Split off ${fmt(S)}: ${what}, not K = ${G.K}.` });
        if (ok) { found = S; break outer; }
      }
    }
    const hidden = tried > SHOW ? ` (${tried - SHOW} tries not shown)` : "";
    frames.push(found
      ? { cert: found, done: true, ok: true, caption: `${fmt(found)} versus the rest is a cut of exactly K = ${G.K}, found after ${tried} tries${hidden}.` }
      : { cert: null, done: true, ok: false, caption: `Checked ${tried} splits${hidden}. None has exactly K = ${G.K}.` });
    return frames;
  }
  function minCutBrute(G) {
    const frames = [], n = G.nodes.length;
    let best = Infinity, bestSet = [];
    for (let mask = 0; mask < 1 << (n - 1); mask++) {
      const A = G.nodes.filter((_, i) => i === 0 || mask & (1 << (i - 1)));
      if (A.length === n) continue;
      const s = new Set(A);
      const w = G.edges.reduce((t, e) => t + (s.has(e.a) !== s.has(e.b) ? e.w : 0), 0);
      if (w < best) { best = w; bestSet = A; frames.push({ cert: A, caption: `Split ${fmt(A)} from the rest: weight ${w} crosses. New smallest.` }); }
    }
    frames.push({ cert: bestSet, done: true, ok: true, caption: `Checked all ${2 ** (n - 1) - 1} splits. The smallest cut is ${best}: ${fmt(bestSet)} versus the rest.` });
    return frames;
  }
  function mvcBrute(G) {
    const frames = [];
    let tried = 0, found = null;
    outer: for (let k = 1; k <= G.nodes.length; k++) {
      for (const S of combos(G.nodes, k)) {
        tried++;
        const ok = valid.vc(G, S);
        if (tried <= SHOW) frames.push({ cert: S, trial: !ok, caption: ok ? `Try ${fmt(S)}. It covers every edge.` : `Try ${fmt(S)}: ${why(G, "vc", S)}.` });
        if (ok) { found = S; break outer; }
      }
    }
    const hidden = tried > SHOW ? ` (${tried - SHOW} tries not shown)` : "";
    frames.push({ cert: found, done: true, ok: true, caption: `${fmt(found)} covers every edge. Smaller sets were all tried first, so ${found.length} is the smallest possible${hidden}.` });
    return frames;
  }
  function mvcApprox(G) {
    // Take any uncovered edge and add BOTH ends; never more than twice the smallest cover.
    const frames = [], S = [];
    for (const e of G.edges) {
      if (S.includes(e.a) || S.includes(e.b)) continue;
      S.push(e.a, e.b);
      frames.push({ cert: [...S], trying: e.b, partial: true, caption: `Edge {${e.a},${e.b}} isn't covered yet. Take both ends.` });
    }
    const m = smallestVC(G);
    frames.push({ cert: S, done: true, ok: S.length === m, caption: `Every edge is covered with ${S.length} nodes. The smallest possible is ${m}; this method never uses more than twice that.` });
    return frames;
  }
  function vcGreedy(G) {
    const frames = [], S = [], covered = new Set();
    for (;;) {
      let best = null, gain = 0;
      for (const n of G.nodes) {
        if (S.includes(n)) continue;
        const g = G.edges.filter(e => !covered.has(e.k) && (e.a === n || e.b === n)).length;
        if (g > gain) { best = n; gain = g; }
      }
      if (!best) break;
      S.push(best);
      G.edges.forEach(e => { if (e.a === best || e.b === best) covered.add(e.k); });
      frames.push({ cert: [...S], trying: best, partial: true, caption: `Pick ${best}. It covers ${gain} more edge${gain === 1 ? "" : "s"}, the most of any node left.` });
    }
    if (G.K === null) {
      const m = smallestVC(G);
      frames.push({ cert: [...S], done: true, ok: S.length === m, caption: S.length === m ? `Every edge is covered with ${S.length} nodes, which is the smallest possible.`
        : `Every edge is covered with ${S.length} nodes, but the smallest possible is ${m}. Greedy isn't guaranteed to find it.` });
      return frames;
    }
    const ok = S.length <= G.K;
    frames.push({ cert: [...S], done: true, ok, caption: ok ? `Every edge is covered with ${S.length} nodes, within K = ${G.K}.`
      : `Greedy's cover uses ${S.length} nodes, more than K = ${G.K}. Greedy doesn't always find the smallest cover, so try brute force.` });
    return frames;
  }
  function colorGreedy(G) {
    const frames = [], color = {};
    const order = G.nodes.slice().sort((a, b) => G.adj.get(b).size - G.adj.get(a).size);
    for (const n of order) {
      const taken = new Set([...G.adj.get(n)].map(m => color[m]).filter(c => c !== undefined));
      let c = 0; while (taken.has(c)) c++;
      color[n] = c;
      frames.push({ color: { ...color }, trying: n, caption: `Color ${n} (degree ${G.adj.get(n).size}) with color ${c + 1}, the lowest one none of its neighbors use.` });
    }
    const used = new Set(Object.values(color)).size, ok = used <= G.K;
    frames.push({ color: { ...color }, done: true, ok, caption: ok ? `All nodes colored with ${used} colors, within K = ${G.K}.` : `Greedy used ${used} colors, more than K = ${G.K}. Try brute force.` });
    return frames;
  }
  function colorBacktrack(G) {
    const frames = [], color = {}, order = G.nodes.slice();
    let count = 0;
    function rec(i) {
      if (i === order.length) return true;
      const n = order[i];
      for (let c = 0; c < G.K; c++) {
        color[n] = c;
        const clash = [...G.adj.get(n)].find(m => color[m] === c);
        if (++count <= SHOW) frames.push({ color: { ...color }, trying: n, badNode: clash !== undefined,
          caption: clash !== undefined ? `Try color ${c + 1} on ${n}: ${clash} already has it. Reject.` : `Try color ${c + 1} on ${n}. No neighbor has it.` });
        if (clash === undefined && rec(i + 1)) return true;
        delete color[n];
      }
      return false;
    }
    const ok = rec(0);
    frames.push(ok ? { color: { ...color }, done: true, ok, caption: `Every node is colored with no clashes, using at most K = ${G.K} colors.` }
      : { color: {}, done: true, ok, caption: `Every way to use ${G.K} colors clashes somewhere. Not colorable with K = ${G.K}.` });
    return frames;
  }
  function hamBacktrack(G, rule) {
    const frames = [], path = [G.nodes[0]], seen = new Set(path);
    let count = 0;
    function rec() {
      if (path.length === G.nodes.length) {
        if (G.has(path[path.length - 1], path[0])) return true;
        if (++count <= SHOW) frames.push({ cert: [...path], partial: true, badNode: path[path.length - 1],
          caption: `Every node is visited, but ${path[path.length - 1]} has no edge back to ${path[0]}. Back up.` });
        return false;
      }
      const last = path[path.length - 1];
      for (const n of G.nodes) {
        if (seen.has(n) || !G.has(last, n)) continue;
        path.push(n); seen.add(n);
        if (++count <= SHOW) frames.push({ cert: [...path], trying: n, partial: true, caption: `Extend the path from ${last} to ${n}.` });
        if (rec()) return true;
        path.pop(); seen.delete(n);
      }
      if (path.length < G.nodes.length && ++count <= SHOW) frames.push({ cert: [...path], partial: true, badNode: last, caption: `${last} has no unvisited neighbor. Back up.` });
      return false;
    }
    const ok = rec();
    frames.push(ok ? { cert: [...path], done: true, ok, caption: `${path.join(" → ")} → ${path[0]} visits every node once and returns home. Hamiltonian.` }
      : { cert: [], done: true, ok, caption: "Every path dead-ends. No Hamiltonian cycle." });
    return frames;
  }
  const tourCost = (G, t) => { let s = 0; for (let i = 0; i < t.length; i++) { const w = G.W.get(key(t[i], t[(i + 1) % t.length])); if (w === undefined) return Infinity; s += w; } return s; };
  function tspGreedy(G) {
    const frames = [], tour = [G.nodes[0]], seen = new Set(tour);
    while (tour.length < G.nodes.length) {
      const last = tour[tour.length - 1];
      let best = null, bw = Infinity;
      for (const n of G.nodes) if (!seen.has(n) && G.has(last, n) && G.W.get(key(last, n)) < bw) { best = n; bw = G.W.get(key(last, n)); }
      if (!best) break;
      tour.push(best); seen.add(best);
      frames.push({ cert: [...tour], trying: best, partial: true, caption: `From ${last}, the nearest unvisited city is ${best} (${bw}).` });
    }
    const cost = tourCost(G, tour), ok = tour.length === G.nodes.length && cost <= G.K;
    frames.push({ cert: tour, done: true, ok, caption: tour.length < G.nodes.length ? "Greedy got stuck with cities left over." :
      ok ? `Tour total is ${cost}, within K = ${G.K}.` : `Tour total is ${cost}, more than K = ${G.K}. Nearest neighbor isn't optimal, so try brute force.` });
    return frames;
  }
  function tspBrute(G) {
    const frames = [], [first, ...rest] = G.nodes;
    let best = null, bestCost = Infinity, tried = 0;
    function* perms(a) { if (a.length <= 1) { yield a; return; } for (let i = 0; i < a.length; i++) for (const p of perms([...a.slice(0, i), ...a.slice(i + 1)])) yield [a[i], ...p]; }
    for (const p of perms(rest)) {
      if (p.length > 1 && p[0] > p[p.length - 1]) continue; // each cycle once, not once per direction
      const t = [first, ...p], c = tourCost(G, t);
      tried++;
      const better = c < bestCost;
      if (better) { best = t; bestCost = c; }
      if (tried <= SHOW) frames.push({ cert: t, trial: !better, caption: c === Infinity ? `Try ${t.join(" → ")}: uses a missing road.` : better ? `Try ${t.join(" → ")}: ${c}. New best.` : `Try ${t.join(" → ")}: ${c}. Not better than ${bestCost}.` });
    }
    const ok = bestCost <= G.K;
    frames.push(best ? { cert: best, done: true, ok, caption: `Checked ${tried} tours. The best is ${bestCost}${ok ? `, within K = ${G.K}` : `, more than K = ${G.K}`}.` }
      : { cert: [], done: true, ok: false, caption: "No tour exists." });
    return frames;
  }
  function cutBrute(G) {
    const frames = [], n = G.nodes.length;
    let best = -1, bestSet = [];
    for (let mask = 0; mask < 1 << (n - 1); mask++) {
      const A = G.nodes.filter((_, i) => i === 0 || mask & (1 << (i - 1)));
      if (A.length === n) continue;
      const s = new Set(A);
      const w = G.edges.reduce((t, e) => t + (s.has(e.a) !== s.has(e.b) ? e.w : 0), 0);
      if (w > best) { best = w; bestSet = A; frames.push({ cert: A, caption: `Split ${fmt(A)} from the rest: cut weight ${w}. New best.` }); }
    }
    frames.forEach(f => (f.best = undefined));
    frames.push({ cert: bestSet, done: true, ok: true, best, caption: `Checked all ${2 ** (n - 1) - 1} splits. The largest cut is ${best}: ${fmt(bestSet)} versus the rest.` });
    return frames;
  }
  // rule → solver key → frames. Keys match the solver lists in PROBLEMS.
  const SOLVERS = {
    clique: { brute: G => subsetBrute(G, "clique") },
    vc: { greedy: vcGreedy, brute: G => subsetBrute(G, "vc") },
    mvc: { brute: mvcBrute, greedy: vcGreedy, approx: mvcApprox },
    is: { brute: G => subsetBrute(G, "is") },
    ds: { brute: G => subsetBrute(G, "ds") },
    color: { greedy: colorGreedy, brute: colorBacktrack },
    ccover: { brute: ccoverBacktrack },
    ham: { brute: hamBacktrack },
    tsp: { greedy: tspGreedy, brute: tspBrute },
    cut: { brute: cutBrute },
    kcut: { brute: G => splitCutBrute(G, "kcut") },
    wkcut: { brute: G => splitCutBrute(G, "wkcut") },
    mincut: { brute: minCutBrute },
    mst: { kruskal, prim },
    steiner: { brute: steinerBrute },
  };
  const SOLVE = { brute: (G, rule) => SOLVERS[rule].brute(G), greedy: (G, rule) => SOLVERS[rule].greedy(G) };

  /* ---------- layout: deterministic force (Fruchterman-Reingold) or circle ---------- */
  function layout(G, mode, W, H, sizes) {
    const n = G.nodes.length, idx = new Map(G.nodes.map((x, i) => [x, i]));
    let pos = G.nodes.map((_, i) => ({ x: Math.cos((2 * Math.PI * i) / n - Math.PI / 2), y: Math.sin((2 * Math.PI * i) / n - Math.PI / 2) }));
    if (mode === "force" && n > 2) {
      pos = pos.map(p => ({ x: p.x * 100, y: p.y * 100 }));
      const k = Math.sqrt((W * H) / n) * 0.42;
      let t = 60;
      for (let it = 0; it < 450; it++) {
        const d = pos.map(() => ({ x: 0, y: 0 }));
        for (let i = 0; i < n; i++) for (let j = i + 1; j < n; j++) {
          let dx = pos[i].x - pos[j].x, dy = pos[i].y - pos[j].y, dist = Math.max(0.5, Math.hypot(dx, dy));
          const f = (k * k) / dist;
          d[i].x += (dx / dist) * f; d[i].y += (dy / dist) * f; d[j].x -= (dx / dist) * f; d[j].y -= (dy / dist) * f;
        }
        for (const e of G.edges) {
          const i = idx.get(e.a), j = idx.get(e.b);
          let dx = pos[i].x - pos[j].x, dy = pos[i].y - pos[j].y, dist = Math.max(0.5, Math.hypot(dx, dy));
          const f = (dist * dist) / k;
          d[i].x -= (dx / dist) * f; d[i].y -= (dy / dist) * f; d[j].x += (dx / dist) * f; d[j].y += (dy / dist) * f;
        }
        for (let i = 0; i < n; i++) {
          const len = Math.max(0.01, Math.hypot(d[i].x, d[i].y));
          pos[i].x += (d[i].x / len) * Math.min(len, t) - pos[i].x * 0.01;
          pos[i].y += (d[i].y / len) * Math.min(len, t) - pos[i].y * 0.01;
        }
        t = Math.max(0.5, t * 0.985);
      }
    }
    const xs = pos.map(p => p.x), ys = pos.map(p => p.y);
    const minX = Math.min(...xs), maxX = Math.max(...xs), minY = Math.min(...ys), maxY = Math.max(...ys);
    const padX = 40 + Math.max(...sizes) / 2, padY = 50;
    const sx = (W - 2 * padX) / Math.max(1e-6, maxX - minX), sy = (H - 2 * padY) / Math.max(1e-6, maxY - minY);
    const s = Math.min(sx, sy, mode === "circle" ? Infinity : 3.2);
    const ox = (W - (maxX - minX) * s) / 2, oy = (H - (maxY - minY) * s) / 2;
    return new Map(G.nodes.map((x, i) => [x, { x: ox + (pos[i].x - minX) * s, y: oy + (pos[i].y - minY) * s }]));
  }

  function create({ svg }) {
    if (!document.getElementById("gb-style")) {
      const st = document.createElement("style"); st.id = "gb-style"; st.textContent = css; document.head.appendChild(st);
    }
    svg.classList.add("gb-svg");
    let G, rule, frames, mode = "force", frame = null, hover = null, els;

    function draw() {
      svg.innerHTML = "";
      const W = 680, H = G.nodes.length > 9 ? 560 : 480;
      svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
      const width = n => (n.length > 3 ? n.length * 8 + 22 : NR * 2);
      const P = layout(G, mode, W, H, G.nodes.map(width));
      const gE = mk("g", {}, svg), gPh = mk("g", {}, svg), gW = mk("g", {}, svg), gN = mk("g", {}, svg);
      els = { P, gPh, nodes: new Map(), edges: new Map(), weights: new Map() };
      G.edges.forEach(e => {
        const a = P.get(e.a), b = P.get(e.b);
        els.edges.set(e.k, { e, el: mk("line", { x1: a.x, y1: a.y, x2: b.x, y2: b.y, class: "gb-edge" }, gE) });
        if (e.w !== null) {
          const g = mk("g", { class: "gb-w" }, gW), text = String(e.w), w = text.length * 7.4 + 12;
          const mx = (a.x + b.x) / 2, my = (a.y + b.y) / 2;
          mk("rect", { x: mx - w / 2, y: my - 9, width: w, height: 18, rx: 9 }, g);
          mk("text", { x: mx, y: my }, g).textContent = text;
          els.weights.set(e.k, g);
        }
      });
      G.nodes.forEach(n => {
        const p = P.get(n), w = width(n);
        const g = mk("g", { class: "gb-node", tabindex: "0", role: "button", "aria-label": `Node ${n}` }, gN);
        if (w > NR * 2) mk("rect", { x: p.x - w / 2, y: p.y - NR, width: w, height: NR * 2, rx: NR, class: "body" }, g);
        else mk("circle", { cx: p.x, cy: p.y, r: NR, class: "body" }, g);
        if (G.terminals && G.terminals.has(n)) mk("circle", { cx: p.x, cy: p.y, r: NR + 5, class: "gb-term" }, g);
        mk("text", { x: p.x, y: p.y }, g).textContent = n;
        const ob = mk("g", { class: "gb-order", visibility: "hidden" }, g);
        mk("circle", { cx: p.x + w / 2 - 2, cy: p.y - NR + 2, r: 9 }, ob);
        const ot = mk("text", { x: p.x + w / 2 - 2, y: p.y - NR + 2 }, ob);
        const on = () => { hover = n; paint(); }, off = () => { hover = null; paint(); };
        g.addEventListener("pointerenter", on); g.addEventListener("pointerleave", off);
        g.addEventListener("focus", on); g.addEventListener("blur", off);
        g.addEventListener("click", () => { hover = hover === n ? null : n; paint(); });
        els.nodes.set(n, { g, ob, ot });
      });
    }

    function paint() {
      const ev = evaluate(G, rule, frame);
      const nb = hover ? G.adj.get(hover) : null;
      for (const [n, o] of els.nodes) {
        const s = ev.nodes.get(n) || {};
        let cls = "gb-node " + (s.st || "Background");
        if (s.color !== undefined) cls += " c" + (s.color % 6);
        if (s.ring) cls += " ring";
        if (s.bad) cls += " badnode";
        if (hover) cls += n === hover ? " trace" : nb.has(n) ? "" : " faint";
        o.g.setAttribute("class", cls);
        o.ob.setAttribute("visibility", s.order ? "visible" : "hidden");
        if (s.order) o.ot.textContent = s.order;
      }
      for (const [k, o] of els.edges) {
        const st = ev.edges.get(k) || "";
        let cls = "gb-edge" + (st ? " " + st : "");
        if (hover) cls = "gb-edge " + (o.e.a === hover || o.e.b === hover ? "trace" : "faint");
        o.el.setAttribute("class", cls);
        const w = els.weights.get(k);
        if (w) w.setAttribute("class", "gb-w" + (hover ? (o.e.a === hover || o.e.b === hover ? "" : " faint") : st === "sol" ? " sol" : st === "dim" ? " faint" : ""));
      }
      els.gPh.innerHTML = "";
      if (!hover) ev.phantom.forEach(([a, b]) => {
        const p = els.P.get(a), q = els.P.get(b);
        mk("line", { x1: p.x, y1: p.y, x2: q.x, y2: q.y, class: "gb-phantom" }, els.gPh);
      });
      return ev;
    }

    return {
      load(str, P, solver) {
        G = parse(str, P); rule = P.rule;
        const key0 = solver || P.solvers[0][0];
        if (!SOLVERS[rule] || !SOLVERS[rule][key0]) throw new Error(`No "${key0}" solver for this problem in the mockup.`);
        frames = SOLVERS[rule][key0](G);
        const colorRule = rule === "color" || rule === "ccover", edgeRule = rule === "mst" || rule === "steiner";
        frames.unshift({ cert: colorRule || edgeRule ? undefined : [], color: colorRule ? {} : undefined, edges: edgeRule ? [] : undefined, partial: true,
          caption: "Step 0: nothing chosen yet. Step forward to watch the solver." });
        if (rule === "cut" || rule === "kcut" || rule === "wkcut" || rule === "mincut") frames[0].cert = null;
        frame = null; hover = null;
        draw(); paint();
        const last = frames[frames.length - 1];
        return { frames, ok: last.ok, K: G.K };
      },
      setLayout(m) { mode = m; if (G) { draw(); paint(); } },
      show(i) { frame = i === null ? null : frames[i]; paint(); return frame; },
      checks(i) { return evaluate(G, rule, i === null ? null : frames[i]).checks; },
      chosen(i) {
        const f = i === null ? null : frames[i];
        if (!f) return [];
        if (rule === "color" || rule === "ccover") {
          const cls = {};
          Object.entries(f.color || {}).forEach(([n, c]) => (cls[c] = cls[c] || []).push(n));
          return Object.keys(cls).sort().map(c => ({ text: `${+c + 1}: ${fmt(cls[c])}`, color: +c }));
        }
        if (rule === "mst" || rule === "steiner") {
          const ks = f.edges || [];
          if (!ks.length) return [];
          const weighted = ks.some(k => G.W.get(k) !== null);
          const chips = ks.map(k => { const e = G.byKey.get(k); return { text: `{${e.a},${e.b}}` + (weighted ? ` ${e.w}` : "") }; });
          chips.push({ text: weighted ? `Total: ${ks.reduce((t, k) => t + G.W.get(k), 0)}` : `${ks.length} edges` });
          return chips;
        }
        if (rule === "cut" || rule === "kcut" || rule === "wkcut" || rule === "mincut") return f.cert ? [{ text: fmt(f.cert), color: 0 }, { text: fmt(G.nodes.filter(n => !f.cert.includes(n))), color: 1 }] : [];
        if (rule === "ham" || rule === "tsp") return (f.cert || []).length ? [{ text: f.cert.join(" → ") + (f.partial ? "" : " → " + f.cert[0]) }] : [];
        return (f.cert || []).map(n => ({ text: n }));
      },
      resetHover() { hover = null; paint(); },
    };
  }

  /* Catalog shared by both mockups. Names, definitions, defaults and solver names are copied from Redux. */
  const TSP_DEFAULT = "(({New York,Chicago,Denver,Los Angeles,Miami},{({New York,Chicago},790),({New York,Denver},1770),({New York,Los Angeles},2450),({New York,Miami},1280),({Chicago,Denver},1000),({Chicago,Los Angeles},2015),({Chicago,Miami},1370),({Denver,Los Angeles},1015),({Denver,Miami},2060),({Los Angeles,Miami},2745)}),8000)";
  const PROBLEMS = {
    CLIQUE: { label: "Clique", rule: "clique", cert: "subset", needK: true, layout: "force",
      def: "A clique is the problem of uncovering a subset of vertices in an undirected graph G = (V, E) such that every two distinct vertices are adjacent",
      solvers: [["brute", "Clique Brute Force"]],
      examples: [["Redux default", "(({1,2,3,4,5,6},{{4,1},{1,2},{4,3},{3,2},{2,4},{5,2},{3,5},{5,4},{3,6},{6,4},{1,6}}),4)"],
                 ["No clique of size K", "(({1,2,3,4,5},{{1,2},{2,3},{3,4},{4,5},{5,1},{1,3}}),4)"]] },
    VERTEXCOVER: { label: "Vertex Cover", rule: "vc", cert: "subset", needK: true, layout: "force",
      def: "A vertex cover is a subset of nodes S, such that every edge in the graph, G, touches a node in S.",
      solvers: [["greedy", "Vertex Cover Max-Degree Greedy"], ["brute", "Vertex Cover Brute Force"]],
      examples: [["Redux default", "(({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}}),3)"],
                 ["Greedy overshoots K", "(({a,b,c,d,e,f,g},{{a,b},{a,e},{a,g},{b,d},{b,e},{d,e},{d,g},{f,g}}),3)"]] },
    INDEPENDENTSET: { label: "Independent Set", rule: "is", cert: "subset", needK: true, layout: "force",
      def: "An Independent Set is a set of nodes in a graph G, where no node is connected to another node in the set",
      solvers: [["brute", "Independent Set Brute Force"]],
      examples: [["Redux default", "(({a,b,c,d,e,f,g},{{a,b},{b,c},{c,a},{a,d},{d,e},{e,a},{f,e},{f,d},{g,b},{g,a}}),3)"]] },
    DOMINATINGSET: { label: "Dominating Set", rule: "ds", cert: "subset", needK: true, layout: "force",
      def: "A dominating set is a set of nodes D such that every node not in D has a neighbor in D, with |D| at most K.",
      solvers: [["brute", "Brute force (not in Redux yet)"]],
      examples: [["Redux default", "(({0,1,2,3,4},{{1,0},{0,3},{1,2},{2,4},{1,3},{3,4},{4,1}}),2)"],
                 ["A path of seven", "(({1,2,3,4,5,6,7},{{1,2},{2,3},{3,4},{4,5},{5,6},{6,7}}),3)"]] },
    GRAPHCOLORING: { label: "Graph Coloring", rule: "color", cert: "partition", needK: true, layout: "force",
      def: "An assignment of labels (e.g., colors) to the vertices of a graph such that no two adjacent vertices are of the same label. This is called a vertex coloring.",
      solvers: [["greedy", "Graph Coloring Greedy"], ["brute", "Graph Coloring Brute Force"]],
      examples: [["Redux default", "(({a,b,c,d,e,f,g,h,i},{{a,b},{b,c},{a,c},{d,a},{d,e},{a,e},{a,f},{f,g},{g,a},{a,h},{h,i},{i,a}}),3)"],
                 ["Needs backtracking", "(({a,b,c,d,e,f},{{a,b},{a,c},{b,c},{b,d},{c,e},{d,e},{d,f},{e,f}}),3)"],
                 ["Not 3-colorable (K4)", "(({a,b,c,d},{{a,b},{a,c},{a,d},{b,c},{b,d},{c,d}}),3)"]] },
    HAMILTONIAN: { label: "Hamiltonian Cycle", rule: "ham", cert: "tour", layout: "circle",
      def: "Hamiltonian Cycle is the problem of determining whether an undirected graph has a Hamiltonian cycle: a cycle that visits every vertex exactly once and returns to the vertex it started from.",
      solvers: [["brute", "Hamiltonian Brute Force"]],
      examples: [["Redux default", "({1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}})"],
                 ["No cycle (a tree)", "({1,2,3,4,5},{{1,2},{1,3},{3,4},{3,5}})"]] },
    TSP: { label: "Traveling Salesperson", rule: "tsp", cert: "tour", needK: true, weighted: true, layout: "circle",
      def: "Find a cycle that visits every city exactly once with total edge weight at most K.",
      solvers: [["greedy", "Traveling Salesperson Greedy"], ["brute", "Traveling Salesperson Brute Force"]],
      examples: [["Redux default", TSP_DEFAULT], ["Budget below the best tour", TSP_DEFAULT.replace(/,8000\)$/, ",6800)")]] },
    MAXCUT: { label: "Max Cut", rule: "cut", cert: "partition", weighted: true, layout: "force",
      def: "Given a weighted undirected graph, find a partition of the vertices into two non-empty sets S and T such that the total weight of edges crossing the partition is maximized.",
      solvers: [["brute", "Max Cut Brute Force"]],
      examples: [["Redux default", "({1,2,3,4,5},{({2,1},5),({1,3},4),({2,3},2),({3,5},1),({2,4},4),({4,5},2)})"]] },
  };

  Object.assign(PROBLEMS, {
    MINIMUMSPANNINGTREE: { label: "Minimum Spanning Tree", rule: "mst", cert: "edges", weighted: true, layout: "force",
      def: "Given a weighted, undirected graph, find a set of edges that connects every vertex without creating cycles and has the smallest possible total weight.",
      solvers: [["kruskal", "Kruskal's Algorithm"], ["prim", "Prim's Algorithm"]],
      examples: [["Redux default", "({1,2,3,4},{({1,2},1),({2,3},2),({3,4},1),({1,4},2),({1,3},3)})"],
                 ["Six nodes, some skipped edges", "({a,b,c,d,e,f},{({a,b},4),({a,c},1),({b,c},2),({b,d},5),({c,d},8),({c,e},10),({d,e},2),({d,f},6),({e,f},3)})"]] },
    STEINERTREE: { label: "Steiner Tree", rule: "steiner", cert: "edges", needK: true, terminals: true, layout: "force",
      def: "Steiner tree problem in graphs requires a tree of minimum weight that contains all terminals. Here the graph is unweighted, so K bounds the number of edges.",
      solvers: [["brute", "Steiner Tree Brute Force"]],
      examples: [["Redux default", "(({1,2,3,4,5,6,7,8},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5},{6,7},{7,8},{6,8},{6,1}}),{5,2,8},6)"],
                 ["Budget too small", "(({1,2,3,4,5,6,7,8},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5},{6,7},{7,8},{6,8},{6,1}}),{5,2,8},4)"]] },
    CLIQUECOVER: { label: "Clique Cover", rule: "ccover", cert: "partition", needK: true, layout: "force",
      def: "A clique cover is a partition of the vertices into cliques, subsets of vertices within which every two vertices are adjacent",
      solvers: [["brute", "Clique Cover Brute Force"]],
      examples: [["Redux default", "(({1,2,3,4,5,6,7,8},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5},{6,7},{7,8},{6,8}}),3)"],
                 ["Too few groups", "(({1,2,3,4,5,6,7,8},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5},{6,7},{7,8},{6,8}}),2)"]] },
    CUT: { label: "Cut", rule: "kcut", cert: "partition", needK: true, layout: "force",
      def: "A cut in an undirected graph is a partition of the graph's vertices into two complementary sets S and T, and the size of the cut is the number of edges between S and T.",
      solvers: [["brute", "Cut Brute Force"]],
      examples: [["Redux default", "(({1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}}),5)"]] },
    WEIGHTEDCUT: { label: "Weighted Cut", rule: "wkcut", cert: "partition", needK: true, weighted: true, layout: "force",
      def: "A weighted cut in an undirected graph is a partition of the graph's vertices into two complementary sets S and T, and the size of the cut is the sum of edge weights between S and T.",
      solvers: [["brute", "Weighted Cut Brute Force"]],
      examples: [["Redux default", "(({1,2,3,4,5},{({2,1},5),({1,3},4),({2,3},2),({3,5},1),({2,4},4),({4,5},2)}),5)"]] },
    MINCUT: { label: "Minimum Cut", cls: "P", rule: "mincut", cert: "partition", weighted: true, layout: "force",
      def: "Given a weighted undirected graph, find a partition of the vertices into two non-empty sets S and T such that the total weight of edges crossing the partition is minimized.",
      solvers: [["brute", "Brute force (not in Redux yet; Redux uses Stoer-Wagner)"]],
      examples: [["Redux default", "({1,2,3,4,5},{({2,1},5),({1,3},4),({2,3},2),({3,5},1),({2,4},4),({4,5},2)})"]] },
    MINIMUMVERTEXCOVER: { label: "Minimum Vertex Cover", cls: "NP-Hard", rule: "mvc", cert: "subset", layout: "force",
      def: "A vertex cover is a subset of nodes C, such that every edge in the graph, G, touches a node in C. A minimal vertex cover is the smallest possible subset C.",
      solvers: [["brute", "Minimum Vertex Cover Brute Force"], ["greedy", "Minimum Vertex Cover Max-Degree Greedy"], ["approx", "Minimum Vertex Cover Approximation"]],
      examples: [["Redux default", "({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}})"],
                 ["Greedy misses the smallest", "({a,b,c,d,e,f,g},{{a,b},{a,e},{a,g},{b,d},{b,e},{d,e},{d,g},{f,g}})"]] },
  });

  return { create, parse, PROBLEMS };
})();
