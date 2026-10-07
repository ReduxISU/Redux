/* ---- Layered view: directed graphs drawn in ranks, left to right ----
   Topological Sort, Feedback Arc Set, Feedback Node Set, Directed Hamiltonian Cycle,
   Strongly Connected Components, SSSP and SPSP. A problem gives its graph and declares a rule;
   solvers emit frames in the problem's own terms and the base derives every color. Pages supply --av-* tokens. */
const LayeredView = (() => {
  const NS = "http://www.w3.org/2000/svg";
  const mk = (tag, attrs = {}, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; };
  const SEP = "\u0000", NR = 19, SHOW = 150, DX = 150, DY = 84;
  const dkey = (u, v) => u + SEP + v;
  const fmt = a => "{" + a.join(",") + "}";

  const css = `
.ly-svg { width: 100%; height: auto; display: block; }
.ly-svg text { font-family: var(--av-mono); }
.ly-node { cursor: pointer; transition: opacity .2s; }
.ly-node:focus { outline: none; }
.ly-node .body { fill: var(--av-bg); stroke: var(--av-stroke); stroke-width: 1.8; transition: fill .2s, stroke .2s; }
.ly-node text.nm { font-size: 13px; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); pointer-events: none; }
.ly-node.Active .body { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; }
.ly-node.Solution .body { fill: var(--av-sol); stroke: var(--av-sol); } .ly-node.Solution text.nm { fill: #fff; font-weight: 600; }
.ly-node.Covered .body { fill: var(--av-sol-fill); stroke: var(--av-sol); stroke-width: 2.2; }
.ly-node.Rejected .body { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 3; } .ly-node.Rejected text.nm { fill: var(--av-rej); }
.ly-node.Blocked .body { fill: transparent; stroke: var(--av-line); stroke-dasharray: 4 3; } .ly-node.Blocked text.nm { fill: var(--av-muted); }
.ly-node.Untraveled .body { fill: transparent; stroke-dasharray: 4 4; } .ly-node.Untraveled text.nm { fill: var(--av-muted); }
.ly-node.c0 .body { fill: #56B4E9; stroke: #2b7fb0; } .ly-node.c1 .body { fill: #E69F00; stroke: #a87200; }
.ly-node.c2 .body { fill: #b59ce0; stroke: #7a5cb8; } .ly-node.c3 .body { fill: #F0E442; stroke: #b5a90f; }
.ly-node.c4 .body { fill: #5fc4a8; stroke: #2a8a70; } .ly-node.c5 .body { fill: #CC79A7; stroke: #9a4c78; }
.ly-node[class*=" c"] text.nm { fill: #10131a; font-weight: 600; }
.ly-node.ring .body { stroke: var(--av-hl); stroke-width: 3.5; }
.ly-node.trace .body, .ly-node:focus-visible .body { stroke: var(--av-hot); stroke-width: 3; }
.ly-node.faint { opacity: .28; }
.ly-node .tag { font-size: 10.5px; font-weight: 700; text-anchor: middle; dominant-baseline: central; fill: var(--av-muted); }
.ly-dist { font-size: 11.5px; text-anchor: middle; dominant-baseline: central; fill: var(--av-muted); }
.ly-dist.set { fill: var(--av-ink); font-weight: 600; }
.ly-order circle { fill: var(--av-ink); } .ly-order text { fill: var(--av-surface); font-size: 10.5px; font-weight: 700; text-anchor: middle; dominant-baseline: central; }
.ly-edge { fill: none; stroke: var(--av-edge); stroke-width: 1.6; transition: opacity .2s, stroke .2s; }
.ly-edge.back { stroke-dasharray: 2 0; }
.ly-edge.sol { stroke: var(--av-sol); stroke-width: 3; }
.ly-edge.cov { stroke: var(--av-sol); stroke-width: 1.8; opacity: .5; }
.ly-edge.rej { stroke: var(--av-rej); stroke-width: 3; }
.ly-edge.hot { stroke: var(--av-hl); stroke-width: 3; }
.ly-edge.cut { stroke: var(--av-sol); stroke-width: 2.4; stroke-dasharray: 6 5; }
.ly-edge.dim { opacity: .22; }
.ly-edge.trace { stroke: var(--av-hot); stroke-width: 2.6; opacity: 1; }
.ly-edge.faint { opacity: .07; }
.ly-phantom { stroke: var(--av-rej); stroke-width: 2.4; stroke-dasharray: 6 5; fill: none; }
.ly-m-def { fill: var(--av-edge); } .ly-m-sol { fill: var(--av-sol); } .ly-m-rej { fill: var(--av-rej); }
.ly-m-hot { fill: var(--av-hl); } .ly-m-trace { fill: var(--av-hot); }
.ly-w rect { fill: var(--av-surface); stroke: var(--av-line); } .ly-w text { font-size: 11.5px; text-anchor: middle; dominant-baseline: central; fill: var(--av-muted); }
.ly-w.sol rect { stroke: var(--av-sol); } .ly-w.sol text { fill: var(--av-sol); font-weight: 700; }
.ly-w.hot rect { stroke: var(--av-hl); } .ly-w.hot text { fill: var(--av-ink); font-weight: 700; }
.ly-w.faint { opacity: .15; }
@media (prefers-reduced-motion: reduce) { .ly-node, .ly-node .body, .ly-edge { transition: none; } }`;

  /* ---------- parsing: (N,E), ((N,E),K), (N,E,s), (N,E,s,t); edges (u,v) or ((u,v),w); {u,v} = both ways ---------- */
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
    let g = top, K = null, s = null, t = null;
    if (top.items[0] && top.items[0].t === "tup") {
      g = top.items[0];
      if (top.items.length > 1) { K = Number(top.items[1]); if (!Number.isInteger(K) || K < 0) throw new Error("K must be a whole number."); }
    } else {
      s = top.items[2] === undefined ? null : top.items[2];
      t = top.items[3] === undefined ? null : top.items[3];
    }
    if (P.needK && K === null) throw new Error("This problem needs K at the end: ((N,E),K).");
    if (P.needS && (s === null || typeof s !== "string")) throw new Error("This problem needs a source node at the end: (N,E,s).");
    if (P.needT && (t === null || typeof t !== "string")) throw new Error("This problem needs a source and a target: (N,E,s,t).");
    const [Nn, En] = g.items;
    if (!Nn || Nn.t !== "set" || !En || En.t !== "set") throw new Error("Expected a node set and an edge set: (N,E).");
    const nodes = Nn.items.map(x => { if (typeof x !== "string") throw new Error("Node names must be plain names."); return x; });
    if (!nodes.length || new Set(nodes).size !== nodes.length) throw new Error("N must be a non-empty set with no repeats.");
    if (nodes.length > 14) throw new Error("This mockup draws up to 14 nodes.");
    const S = new Set(nodes);
    if (s !== null && P.needS && !S.has(s)) throw new Error(`Source ${s} is not in N.`);
    if (t !== null && P.needT && !S.has(t)) throw new Error(`Target ${t} is not in N.`);
    const edges = [], seen = new Set();
    const add = (u, v, w, undirected) => {
      if (!S.has(u) || !S.has(v)) throw new Error(`Edge (${u},${v}) uses a node that isn't in N.`);
      const k = dkey(u, v);
      if (seen.has(k)) return;
      seen.add(k); edges.push({ u, v, w, k, undirected: !!undirected });
    };
    for (const e of En.items) {
      let pair = e, w = null;
      if (e.t === "tup" && e.items.length === 2 && typeof e.items[1] === "string" && typeof e.items[0] !== "string") { pair = e.items[0]; w = Number(e.items[1]); if (!Number.isFinite(w) || w < 0) throw new Error("Edge weights must be non-negative numbers."); }
      if (!pair || !pair.items || pair.items.length !== 2 || pair.items.some(x => typeof x !== "string")) throw new Error("Each edge must look like (u,v)" + (P.weighted ? " or ((u,v),w)." : "."));
      if (P.weighted && w === null) throw new Error("This problem needs a weight on every edge: ((u,v),w).");
      const [u, v] = pair.items;
      if (pair.t === "set") {
        if (!P.weighted) throw new Error("Edges here are directed: write (u,v), not {u,v}.");
        add(u, v, w, true); add(v, u, w, true);
      } else add(u, v, w, false);
    }
    const out = new Map(nodes.map(n => [n, []])), inn = new Map(nodes.map(n => [n, []]));
    edges.forEach(e => { out.get(e.u).push(e); inn.get(e.v).push(e); });
    return { nodes, edges, K, s, t, out, inn, has: (u, v) => seen.has(dkey(u, v)), byKey: new Map(edges.map(e => [e.k, e])) };
  }

  /* ---------- graph helpers ---------- */
  // A cycle in the graph left after removing some edges and nodes; returns its edge keys, or null.
  function findCycle(G, goneEdges, goneNodes) {
    const state = new Map(), stack = [];
    let found = null;
    function dfs(n) {
      state.set(n, 1); stack.push(n);
      for (const e of G.out.get(n)) {
        if (found || goneEdges.has(e.k) || goneNodes.has(e.v) || e.undirected) continue;
        if (state.get(e.v) === 1) {
          const at = stack.indexOf(e.v), ring = stack.slice(at);
          found = ring.map((x, i) => dkey(x, ring.at((i + 1) % ring.length)));
          return;
        }
        if (!state.has(e.v)) dfs(e.v);
        if (found) return;
      }
      stack.pop(); state.set(n, 2);
    }
    for (const n of G.nodes) if (!found && !goneNodes.has(n) && !state.has(n)) dfs(n);
    return found;
  }
  function* combos(arr, k, start = 0, acc = []) {
    if (acc.length === k) { yield acc.slice(); return; }
    for (let i = start; i <= arr.length - (k - acc.length); i++) { acc.push(arr[i]); yield* combos(arr, k, i + 1, acc); acc.pop(); }
  }
  const cycleText = (G, keys) => { const e = keys.map(k => G.byKey.get(k)); return e.map(x => x.u).concat(e.at(0).u).join(" → "); };

  /* ---------- solvers: frames in the problem's own terms ---------- */
  function kahn(G) {
    const frames = [], indeg = new Map(G.nodes.map(n => [n, G.inn.get(n).length])), order = [];
    let ready = G.nodes.filter(n => indeg.get(n) === 0);
    frames.push({ order: [], ready: [...ready], partial: true, caption: ready.length ? `Ready to place (no incoming edges): ${ready.join(", ")}.` : "No node is free of incoming edges, so the graph has a cycle." });
    while (ready.length) {
      const n = ready.shift();
      order.push(n);
      const freed = [];
      for (const e of G.out.get(n)) { indeg.set(e.v, indeg.get(e.v) - 1); if (indeg.get(e.v) === 0) { ready.push(e.v); freed.push(e.v); } }
      frames.push({ order: [...order], ready: [...ready], trying: n, partial: true,
        caption: `Place ${n} at position ${order.length}.${freed.length ? ` That frees ${freed.join(", ")}.` : ""}${ready.length ? ` Ready: ${ready.join(", ")}.` : ""}` });
    }
    const ok = order.length === G.nodes.length;
    const stuck = G.nodes.filter(n => !order.includes(n));
    frames.push({ order: [...order], ready: [], done: true, ok, stuck,
      caption: ok ? `Every node is placed and every edge points forward: ${order.join(", ")}.` : `${stuck.join(", ")} never ${stuck.length > 1 ? "lose" : "loses"} ${stuck.length > 1 ? "their" : "its"} last incoming edge, because of the cycle shown in red. No topological order exists.` });
    return frames;
  }
  function removeBrute(G, rule) {
    const frames = [], pool = rule === "arcset" ? G.edges.map(e => e.k) : G.nodes.slice();
    const name = x => (rule === "arcset" ? `(${G.byKey.get(x).u},${G.byKey.get(x).v})` : x);
    let tried = 0, found = null;
    outer: for (let k = 0; k <= Math.min(G.K, pool.length); k++) {
      for (const pick of combos(pool, k)) {
        tried++;
        const cyc = rule === "arcset" ? findCycle(G, new Set(pick), new Set()) : findCycle(G, new Set(), new Set(pick));
        const label = pick.length ? `removing ${fmt(pick.map(name))}` : "removing nothing";
        if (tried <= SHOW) frames.push({ cert: pick, trial: !!cyc,
          caption: cyc ? `Try ${label}: the cycle ${cycleText(G, cyc)} is still there.` : `Try ${label}. No cycle is left.` });
        if (!cyc) { found = pick; break outer; }
      }
    }
    const hidden = tried > SHOW ? ` (${tried - SHOW} tries not shown)` : "";
    frames.push(found
      ? { cert: found, done: true, ok: true, caption: `${found.length ? `Removing ${fmt(found.map(name))}` : "Removing nothing"} leaves no cycle, within K = ${G.K}. Found after ${tried} tries${hidden}.` }
      : { cert: [], done: true, ok: false, caption: `Checked every way to remove up to K = ${G.K} ${rule === "arcset" ? "edges" : "nodes"}${hidden}. A cycle always survives.` });
    return frames;
  }
  function dirHam(G) {
    const frames = [], path = [G.nodes[0]], seen = new Set(path);
    let count = 0;
    function rec() {
      const last = path.at(-1);
      if (path.length === G.nodes.length) {
        if (G.has(last, path[0])) return true;
        if (++count <= SHOW) frames.push({ cert: [...path], partial: true, badNode: last, caption: `Every node is visited, but there is no edge from ${last} back to ${path[0]}. Back up.` });
        return false;
      }
      let moved = false;
      for (const e of G.out.get(last)) {
        if (seen.has(e.v)) continue;
        moved = true;
        path.push(e.v); seen.add(e.v);
        if (++count <= SHOW) frames.push({ cert: [...path], trying: e.v, partial: true, caption: `Follow the edge ${last} → ${e.v}.` });
        if (rec()) return true;
        path.pop(); seen.delete(e.v);
      }
      if (!moved && ++count <= SHOW) frames.push({ cert: [...path], partial: true, badNode: last, caption: `${last} has no edge to an unvisited node. Back up.` });
      return false;
    }
    const ok = rec();
    frames.push(ok ? { cert: [...path], done: true, ok, caption: `${path.join(" → ")} → ${path[0]} follows the edge directions, visits every node once and returns home.` }
      : { cert: [], done: true, ok, caption: "Every path dead-ends. No directed Hamiltonian cycle." });
    return frames;
  }
  function kosaraju(G) {
    const frames = [], visited = new Set(), finish = [];
    function dfs1(n) {
      visited.add(n);
      frames.push({ phase: 1, visited: [...visited], finish: [...finish], trying: n, caption: `Pass 1: visit ${n}.` });
      for (const e of G.out.get(n)) if (!visited.has(e.v)) dfs1(e.v);
      finish.push(n);
      frames.push({ phase: 1, visited: [...visited], finish: [...finish], trying: n, caption: `Pass 1: ${n} is finished (number ${finish.length}).` });
    }
    for (const n of G.nodes) if (!visited.has(n)) dfs1(n);
    const groups = [], where = new Map();
    function dfs2(n, g) {
      where.set(n, g); groups[g].push(n);
      for (const e of G.inn.get(n)) if (!where.has(e.u)) {
        frames.push({ phase: 2, groups: groups.map(x => [...x]), finish: [...finish], trying: e.u, caption: `Pass 2: follow the edge ${e.u} → ${n} backward, so ${e.u} joins component ${g + 1}.` });
        dfs2(e.u, g);
      }
    }
    for (const n of [...finish].reverse()) {
      if (where.has(n)) continue;
      groups.push([]);
      frames.push({ phase: 2, groups: groups.map(x => [...x]), finish: [...finish], trying: n, caption: `Pass 2: ${n} has the highest finish number left, so it starts component ${groups.length}.` });
      dfs2(n, groups.length - 1);
    }
    frames.push({ phase: 2, groups: groups.map(x => [...x]), done: true, ok: true, caption: `${groups.length} strongly connected component${groups.length > 1 ? "s" : ""}: ${groups.map(fmt).join(", ")}.` });
    return frames;
  }
  function dijkstra(G, single) {
    const frames = [], dist = new Map(G.nodes.map(n => [n, Infinity])), pred = new Map(), settled = new Set(), s = G.s, t = G.t;
    dist.set(s, 0);
    const snap = extra => ({ dist: Object.fromEntries(dist), pred: Object.fromEntries(pred), settled: [...settled], ...extra });
    frames.push(snap({ caption: `Start at ${s} with distance 0. Every other node starts at ∞.` }));
    for (;;) {
      let cur = null;
      for (const n of G.nodes) if (!settled.has(n) && dist.get(n) < Infinity && (cur === null || dist.get(n) < dist.get(cur))) cur = n;
      if (cur === null) break;
      settled.add(cur);
      const ups = [], relax = [];
      for (const e of G.out.get(cur)) {
        if (settled.has(e.v)) continue;
        const nd = dist.get(cur) + e.w;
        relax.push(e.k);
        if (nd < dist.get(e.v)) { ups.push(`${e.v}: ${dist.get(e.v) === Infinity ? "∞" : dist.get(e.v)} → ${nd}`); dist.set(e.v, nd); pred.set(e.v, cur); }
      }
      frames.push(snap({ current: cur, relax, caption: `Settle ${cur} at distance ${dist.get(cur)}.${ups.length ? ` Shorter routes found: ${ups.join(", ")}.` : " No shorter routes from here."}` }));
      if (single && cur === t) break;
    }
    if (single) {
      const ok = dist.get(t) < Infinity, path = [];
      if (ok) for (let x = t; x !== undefined; x = pred.get(x)) path.unshift(x);
      frames.push(snap({ done: true, ok, path, caption: ok ? `Shortest path ${path.join(" → ")}, total ${dist.get(t)}. Dijkstra stops as soon as ${t} is settled.` : `${t} can't be reached from ${s}.` }));
    } else {
      const lost = G.nodes.filter(n => dist.get(n) === Infinity);
      frames.push(snap({ done: true, ok: true, lost, caption: `Every reachable node is settled. The green edges form the shortest-path tree from ${s}.${lost.length ? ` ${lost.join(", ")} can't be reached.` : ""}` }));
    }
    return frames;
  }
  const SOLVE = {
    kahn: G => kahn(G),
    brute: (G, rule) => (rule === "dirham" ? dirHam(G) : removeBrute(G, rule)),
    kosaraju: G => kosaraju(G),
    dijkstra: (G, rule) => dijkstra(G, rule === "spsp"),
  };

  /* ---------- rules: frame → states ---------- */
  function evaluate(G, rule, f) {
    const out = { nodes: new Map(), edges: new Map(), phantom: [], dist: null, checks: [] };
    const setN = (n, v) => out.nodes.set(n, Object.assign(out.nodes.get(n) || {}, v));
    if (!f) return out;
    if (rule === "topo") {
      const pos = new Map(f.order.map((n, i) => [n, i]));
      f.order.forEach((n, i) => setN(n, { st: n === f.trying && !f.done ? "Active" : "Solution", order: i + 1 }));
      (f.ready || []).forEach(n => setN(n, { st: "Covered" }));
      let backward = 0;
      G.edges.forEach(e => {
        if (pos.has(e.u) && pos.has(e.v)) { if (pos.get(e.u) < pos.get(e.v)) out.edges.set(e.k, "sol"); else { out.edges.set(e.k, "rej"); backward++; } }
        else if (pos.has(e.u)) out.edges.set(e.k, "cov");
      });
      if (f.done && !f.ok) {
        f.stuck.forEach(n => setN(n, { st: "Rejected" }));
        const cyc = findCycle(G, new Set(), new Set(f.order));
        if (cyc) cyc.forEach(k => out.edges.set(k, "rej"));
      }
      out.checks.push({ label: `Nodes placed: ${f.order.length} / ${G.nodes.length}`, ok: f.order.length === G.nodes.length });
      out.checks.push({ label: `Edges pointing backward: ${backward}`, ok: backward === 0 });
      if (f.done && !f.ok) out.checks.push({ label: "The graph has a cycle", ok: false });
    } else if (rule === "arcset" || rule === "nodeset") {
      const pick = new Set(f.cert || []);
      if (rule === "arcset") pick.forEach(k => out.edges.set(k, "cut"));
      else {
        pick.forEach(n => setN(n, { st: f.trial ? "Active" : "Solution" }));
        G.edges.forEach(e => { if (pick.has(e.u) || pick.has(e.v)) out.edges.set(e.k, "dim"); });
      }
      const cyc = rule === "arcset" ? findCycle(G, pick, new Set()) : findCycle(G, new Set(), pick);
      if (cyc) cyc.forEach(k => out.edges.set(k, "rej"));
      out.checks.push({ label: `${rule === "arcset" ? "Edges" : "Nodes"} removed: ${pick.size} (K = ${G.K})`, ok: pick.size <= G.K });
      out.checks.push({ label: cyc ? `Cycle left: ${cycleText(G, cyc)}` : "No cycle left", ok: !cyc });
    } else if (rule === "dirham") {
      const order = f.cert || [];
      order.forEach((n, i) => setN(n, { st: f.badNode === n ? "Rejected" : n === f.trying ? "Active" : "Solution", order: i + 1 }));
      const steps = [];
      for (let i = 0; i + 1 < order.length; i++) steps.push([order[i], order[i + 1]]);
      if (!f.partial && order.length === G.nodes.length && order.length > 1) steps.push([order.at(-1), order[0]]);
      let missing = 0;
      const used = new Set();
      steps.forEach(([a, b], i) => {
        if (G.has(a, b)) { const k = dkey(a, b); used.add(k); out.edges.set(k, b === f.trying && i === steps.length - 1 ? "hot" : "sol"); }
        else { missing++; out.phantom.push([a, b]); }
      });
      if (order.length) G.edges.forEach(e => { if (!used.has(e.k)) out.edges.set(e.k, "dim"); });
      out.checks.push({ label: `Visits every node once: ${new Set(order).size} / ${G.nodes.length}`, ok: order.length === G.nodes.length });
      out.checks.push({ label: `Steps against an edge's direction or missing: ${missing}`, ok: missing === 0 });
    } else if (rule === "scc") {
      if (f.phase === 1) {
        f.visited.forEach(n => setN(n, { st: n === f.trying ? "Active" : "Covered" }));
        f.finish.forEach((n, i) => setN(n, { tag: "#" + (i + 1) }));
        out.checks.push({ label: `Pass 1, nodes finished: ${f.finish.length} / ${G.nodes.length}`, ok: f.finish.length === G.nodes.length });
      } else {
        const where = new Map();
        f.groups.forEach((g, i) => g.forEach(n => where.set(n, i)));
        (f.finish || []).forEach((n, i) => setN(n, { tag: "#" + (i + 1) }));
        where.forEach((g, n) => setN(n, { color: g }));
        if (f.trying && !f.done) setN(f.trying, { ring: true });
        G.edges.forEach(e => {
          if (where.has(e.u) && where.get(e.u) === where.get(e.v)) out.edges.set(e.k, "sol");
          else if (where.size) out.edges.set(e.k, "dim");
        });
        out.checks.push({ label: `Components found: ${f.groups.length}`, ok: f.done ? true : null });
        out.checks.push({ label: `Nodes assigned: ${where.size} / ${G.nodes.length}`, ok: where.size === G.nodes.length });
      }
    } else if (rule === "sssp" || rule === "spsp") {
      out.dist = f.dist;
      const settled = new Set(f.settled);
      G.nodes.forEach(n => {
        if (n === f.current && !f.done) setN(n, { st: "Active" });
        else if (settled.has(n)) setN(n, { st: "Covered" });
      });
      (f.relax || []).forEach(k => out.edges.set(k, "hot"));
      const treeKeys = new Set(Object.entries(f.pred).map(([v, u]) => dkey(u, v)));
      if (f.done && rule === "sssp") {
        G.edges.forEach(e => out.edges.set(e.k, treeKeys.has(e.k) ? "sol" : "dim"));
        f.lost.forEach(n => setN(n, { st: "Untraveled" }));
        G.nodes.forEach(n => { if (settled.has(n)) setN(n, { st: "Solution" }); });
      }
      if (f.done && rule === "spsp") {
        const pk = new Set();
        for (let i = 0; i + 1 < f.path.length; i++) pk.add(dkey(f.path[i], f.path[i + 1]));
        G.edges.forEach(e => out.edges.set(e.k, pk.has(e.k) ? "sol" : "dim"));
        f.path.forEach(n => setN(n, { st: "Solution" }));
        if (!f.ok) setN(G.t, { st: "Rejected" });
      }
      setN(G.s, { ring: !f.done });
      const reached = Object.values(f.dist).filter(d => d !== Infinity && d !== null).length;
      if (rule === "sssp") {
        out.checks.push({ label: `Nodes settled: ${settled.size} / ${G.nodes.length}`, ok: f.done ? true : null });
        if (f.done) out.checks.push({ label: `Reachable from ${G.s}: ${reached} / ${G.nodes.length}`, ok: null });
      } else {
        const d = f.dist[G.t];
        out.checks.push({ label: `Distance to ${G.t}: ${d === Infinity || d === null ? "∞" : d}`, ok: f.done ? f.ok : null });
        if (f.done && f.ok) out.checks.push({ label: `Path: ${f.path.join(" → ")}`, ok: true });
      }
    }
    // a failed search has no answer to grade: keep the failures, drop the passes
    if (f.done && f.ok === false && (rule === "arcset" || rule === "nodeset" || rule === "dirham")) {
      out.checks = out.checks.filter(c => c.ok === false);
      out.checks.unshift({ label: "No valid answer exists", ok: false });
    }
    return out;
  }

  /* ---------- layout: ranks left to right (Sugiyama-lite) ---------- */
  function layout(G) {
    // 1. break cycles: edges into a node still on the DFS stack are back edges
    const back = new Set(), state = new Map();
    function dfs(n) {
      state.set(n, 1);
      for (const e of G.out.get(n)) {
        if (e.undirected && back.has(dkey(e.v, e.u))) continue;
        if (state.get(e.v) === 1) back.add(e.k);
        else if (!state.has(e.v)) dfs(e.v);
      }
      state.set(n, 2);
    }
    const starts = G.s ? [G.s, ...G.nodes.filter(n => n !== G.s)] : G.nodes;
    for (const n of starts) if (!state.has(n)) dfs(n);
    // the second direction of an undirected edge never sets a rank
    G.edges.forEach(e => { if (e.undirected && !back.has(e.k) && G.byKey.has(dkey(e.v, e.u)) && !back.has(dkey(e.v, e.u))) { const a = G.nodes.indexOf(e.u), b = G.nodes.indexOf(e.v); if (a > b) back.add(e.k); } });
    // 2. rank by longest path over forward edges
    const fwd = G.edges.filter(e => !back.has(e.k) && e.u !== e.v);
    const rank = new Map(G.nodes.map(n => [n, 0]));
    for (let pass = 0; pass < G.nodes.length; pass++) fwd.forEach(e => { if (rank.get(e.v) < rank.get(e.u) + 1) rank.set(e.v, rank.get(e.u) + 1); });
    const R = Math.max(...rank.values());
    const cols = Array.from({ length: R + 1 }, () => []);
    G.nodes.forEach(n => cols.at(rank.get(n)).push(n));
    // 3. order within ranks by barycenter, two down-and-up sweeps
    const idx = new Map();
    const reindex = () => cols.forEach(c => c.forEach((n, i) => idx.set(n, i)));
    reindex();
    const bary = (n, dir) => {
      const nb = dir > 0 ? G.inn.get(n).map(e => e.u) : G.out.get(n).map(e => e.v);
      const near = nb.filter(m => rank.get(m) === rank.get(n) - dir);
      return near.length ? near.reduce((a, m) => a + idx.get(m), 0) / near.length : idx.get(n);
    };
    for (let it = 0; it < 2; it++) {
      for (let r = 1; r <= R; r++) { const c = cols.at(r); const b = new Map(c.map(n => [n, bary(n, 1)])); c.sort((a, z) => b.get(a) - b.get(z)); reindex(); }
      for (let r = R - 1; r >= 0; r--) { const c = cols.at(r); const b = new Map(c.map(n => [n, bary(n, -1)])); c.sort((a, z) => b.get(a) - b.get(z)); reindex(); }
    }
    const rows = Math.max(...cols.map(c => c.length));
    const W = Math.max(640, 100 + R * DX + 100), top0 = 70, Hrows = rows * DY;
    const backCount = G.edges.filter(e => back.has(e.k)).length;
    const H = top0 + Hrows + 40 + (backCount ? 30 + 14 * Math.min(backCount, 4) : 0);
    const ox = (W - R * DX) / 2;
    const P = new Map();
    cols.forEach((c, r) => c.forEach((n, i) => P.set(n, { x: ox + r * DX, y: top0 + Hrows / 2 + (i - (c.length - 1) / 2) * DY })));
    return { P, back, rank, W, H, bottom: top0 + Hrows };
  }

  function create({ svg }) {
    if (!document.getElementById("ly-style")) {
      const st = document.createElement("style"); st.id = "ly-style"; st.textContent = css; document.head.appendChild(st);
    }
    svg.classList.add("ly-svg");
    let G, rule, frames, L, frame = null, hover = null, els;

    function edgeGeom(e, backIdx) {
      const a = L.P.get(e.u), b = L.P.get(e.v);
      if (e.u === e.v) {
        const p0 = [a.x - 9, a.y - NR + 2], p1 = [a.x + 9, a.y - NR + 2];
        return { d: `M${p0} C${a.x - 34},${a.y - NR - 46} ${a.x + 34},${a.y - NR - 46} ${p1}`, lx: a.x, ly: a.y - NR - 34 };
      }
      if (L.back.has(e.k)) {
        // back edge: leave from the bottom, run under the rows, come up into the target
        const yb = L.bottom + 18 + 14 * (backIdx % 4);
        const s = [a.x, a.y + NR], t = [b.x, b.y + NR + 1];
        return { d: `M${s} C${a.x},${yb} ${b.x},${yb} ${t}`, lx: (a.x + b.x) / 2, ly: yb - 6, back: true };
      }
      const pair = !e.undirected && G.has(e.v, e.u) && !L.back.has(dkey(e.v, e.u));
      const span = Math.abs(L.rank.get(e.v) - L.rank.get(e.u));
      const dx = b.x - a.x, dy = b.y - a.y, len = Math.hypot(dx, dy) || 1, nx = dy / len, ny = -dx / len;
      let off = pair ? 18 : 0;
      if (span > 1) off = Math.max(off, 22 + 10 * span);
      const cx = (a.x + b.x) / 2 + nx * off * 2, cy = (a.y + b.y) / 2 + ny * off * 2;
      const sl = Math.hypot(cx - a.x, cy - a.y) || 1, tl = Math.hypot(cx - b.x, cy - b.y) || 1;
      const s = [a.x + (cx - a.x) / sl * NR, a.y + (cy - a.y) / sl * NR];
      const t = [b.x + (cx - b.x) / tl * (NR + 1), b.y + (cy - b.y) / tl * (NR + 1)];
      return { d: off ? `M${s} Q${cx},${cy} ${t}` : `M${s} L${t}`, lx: 0.25 * s[0] + 0.5 * cx + 0.25 * t[0], ly: 0.25 * s[1] + 0.5 * cy + 0.25 * t[1] };
    }

    function draw() {
      svg.innerHTML = "";
      L = layout(G);
      svg.setAttribute("viewBox", `0 0 ${L.W} ${L.H}`);
      const defs = mk("defs", {}, svg);
      for (const k of ["def", "sol", "rej", "hot", "trace"]) {
        const m = mk("marker", { id: `ly-arrow-${k}`, viewBox: "0 0 10 10", refX: 9, refY: 5, markerWidth: 9, markerHeight: 9, markerUnits: "userSpaceOnUse", orient: "auto" }, defs);
        mk("path", { d: "M0,0 L10,5 L0,10 z", class: `ly-m-${k}` }, m);
      }
      const gE = mk("g", {}, svg), gPh = mk("g", {}, svg), gW = mk("g", {}, svg), gN = mk("g", {}, svg);
      els = { gPh, nodes: new Map(), edges: new Map() };
      let bi = 0;
      const drawn = new Set();
      G.edges.forEach(e => {
        // an undirected edge is drawn once, with no arrowhead
        if (e.undirected && drawn.has(dkey(e.v, e.u))) { els.edges.set(e.k, { e, twin: dkey(e.v, e.u) }); return; }
        drawn.add(e.k);
        const geo = edgeGeom(e, L.back.has(e.k) ? bi++ : 0);
        const path = mk("path", { d: geo.d, class: "ly-edge" + (geo.back ? " back" : "") }, gE);
        let w = null;
        if (e.w !== null) {
          w = mk("g", { class: "ly-w" }, gW);
          const text = String(e.w), wd = text.length * 7.4 + 12;
          mk("rect", { x: geo.lx - wd / 2, y: geo.ly - 9, width: wd, height: 18, rx: 9 }, w);
          mk("text", { x: geo.lx, y: geo.ly }, w).textContent = text;
        }
        els.edges.set(e.k, { e, path, w, back: !!geo.back });
      });
      G.nodes.forEach(n => {
        const p = L.P.get(n);
        const g = mk("g", { class: "ly-node", tabindex: "0", role: "button", "aria-label": `Node ${n}` }, gN);
        mk("circle", { cx: p.x, cy: p.y, r: NR, class: "body" }, g);
        mk("text", { x: p.x, y: p.y, class: "nm" }, g).textContent = n;
        const tag = mk("text", { x: p.x, y: p.y - NR - 9, class: "tag" }, g);
        const dist = mk("text", { x: p.x, y: p.y + NR + 13, class: "ly-dist" }, g);
        const ob = mk("g", { class: "ly-order", visibility: "hidden" }, g);
        mk("circle", { cx: p.x + NR - 2, cy: p.y - NR + 2, r: 9 }, ob);
        const ot = mk("text", { x: p.x + NR - 2, y: p.y - NR + 2 }, ob);
        const on = () => { hover = n; paint(); }, off = () => { hover = null; paint(); };
        g.addEventListener("pointerenter", on); g.addEventListener("pointerleave", off);
        g.addEventListener("focus", on); g.addEventListener("blur", off);
        g.addEventListener("click", () => { hover = hover === n ? null : n; paint(); });
        els.nodes.set(n, { g, ob, ot, tag, dist });
      });
      // source and target labels for the shortest-path problems
      if (G.s && (rule === "sssp" || rule === "spsp")) els.nodes.get(G.s).tag.dataset.fixed = "source";
      if (G.t && rule === "spsp") els.nodes.get(G.t).tag.dataset.fixed = "target";
    }

    function paint() {
      const ev = evaluate(G, rule, frame);
      const touches = new Set();
      if (hover) G.edges.forEach(e => { if (e.u === hover || e.v === hover) { touches.add(e.u); touches.add(e.v); } });
      for (const [n, o] of els.nodes) {
        const s = ev.nodes.get(n) || {};
        let cls = "ly-node " + (s.st || "Background");
        if (s.color !== undefined) cls += " c" + (s.color % 6);
        if (s.ring) cls += " ring";
        if (hover) cls += n === hover ? " trace" : touches.has(n) ? "" : " faint";
        o.g.setAttribute("class", cls);
        o.ob.setAttribute("visibility", s.order ? "visible" : "hidden");
        if (s.order) o.ot.textContent = s.order;
        o.tag.textContent = s.tag || o.tag.dataset.fixed || "";
        if (ev.dist) {
          const d = ev.dist[n];
          o.dist.textContent = d === Infinity || d === null || d === undefined ? "∞" : "d = " + d;
          o.dist.setAttribute("class", "ly-dist" + (d === Infinity || d === null ? "" : " set"));
        } else o.dist.textContent = "";
      }
      for (const [k, o] of els.edges) {
        if (o.twin) continue;
        let st = ev.edges.get(k) || "";
        if (o.e.undirected && !st) st = ev.edges.get(dkey(o.e.v, o.e.u)) || "";
        if (hover) st = o.e.u === hover || o.e.v === hover ? "trace" : "faint";
        o.path.setAttribute("class", "ly-edge" + (o.back ? " back" : "") + (st ? " " + st : ""));
        const mark = { sol: "sol", rej: "rej", hot: "hot", trace: "trace", cut: "sol" }[st] || "def";
        if (o.e.undirected) o.path.removeAttribute("marker-end");
        else o.path.setAttribute("marker-end", `url(#ly-arrow-${mark})`);
        if (o.w) o.w.setAttribute("class", "ly-w" + (st === "sol" ? " sol" : st === "hot" ? " hot" : st === "dim" || st === "faint" ? " faint" : ""));
      }
      els.gPh.innerHTML = "";
      if (!hover) ev.phantom.forEach(([a, b]) => {
        const p = L.P.get(a), q = L.P.get(b);
        mk("line", { x1: p.x, y1: p.y, x2: q.x, y2: q.y, class: "ly-phantom" }, els.gPh);
      });
      return ev;
    }

    return {
      load(str, P, solver) {
        G = parse(str, P); rule = P.rule;
        frames = SOLVE[solver || P.solvers[0][0]](G, rule);
        if (rule === "arcset" || rule === "nodeset" || rule === "dirham")
          frames.unshift({ cert: [], partial: true, caption: "Step 0: nothing chosen yet. Step forward to watch the solver." });
        frame = null; hover = null;
        draw(); paint();
        const last = frames.at(-1);
        return { frames, ok: last.ok };
      },
      show(i) { frame = i === null ? null : frames[i]; paint(); return frame; },
      checks(i) { return evaluate(G, rule, i === null ? null : frames[i]).checks; },
      chosen(i) {
        const f = i === null ? null : frames[i];
        if (!f) return [];
        if (rule === "topo") return f.order.length ? [{ text: f.order.join(" → ") }] : [];
        if (rule === "arcset") return (f.cert || []).map(k => ({ text: `(${G.byKey.get(k).u},${G.byKey.get(k).v})` }));
        if (rule === "nodeset") return (f.cert || []).map(n => ({ text: n }));
        if (rule === "dirham") return (f.cert || []).length ? [{ text: f.cert.join(" → ") + (f.partial ? "" : " → " + f.cert[0]) }] : [];
        if (rule === "scc") return f.groups ? f.groups.map((g, j) => ({ text: fmt(g), color: j })) : [];
        if (rule === "spsp") return f.done && f.ok ? [{ text: f.path.join(" → ") }] : [];
        if (rule === "sssp") return f.done ? G.nodes.filter(n => f.dist[n] !== Infinity).map(n => {
          const path = []; for (let x = n; x !== undefined; x = f.pred[x]) path.unshift(x);
          return { text: `${n}: ${path.join("→")} (${f.dist[n]})` };
        }) : [];
        return [];
      },
      resetHover() { hover = null; paint(); },
    };
  }

  /* Catalog shared by both mockups. Names, definitions, defaults and solver names come from Redux. */
  const PROBLEMS = {
    TOPOSORT: { label: "Topological Sort", rule: "topo", cls: "P",
      def: "Topological Sort is the problem of arranging the vertices of a directed acyclic graph (DAG) into a linear sequence such that all directed edges point forward in the sequence. If the graph contains a cycle, no valid topological ordering exists.",
      solvers: [["kahn", "Kahn's Algorithm"]],
      examples: [["Redux default", "({1,2,3,4,5,6},{(1,2),(1,3),(2,4),(3,4),(4,5),(3,6)})"],
                 ["Has a cycle", "({1,2,3,4,5},{(1,2),(2,3),(3,4),(4,2),(3,5)})"]] },
    ARCSET: { label: "Feedback Arc Set", rule: "arcset", needK: true, cls: "NP-Complete",
      def: "ARCSET, or the Feedback Arc Set satisfiability problem, is an NP-complete problem that can be described like the following. Given a directed graph, does removing a given set of edges render the graph acyclical? That is, does removing the edges break every cycle in the graph?",
      solvers: [["brute", "Arc Set Brute Force"]],
      examples: [["Redux default", "(({1,2,3,4,5},{(1,2),(2,3),(3,1),(4,5),(5,2),(3,4)}),1)"],
                 ["Two separate cycles, K = 1", "(({1,2,3,4,5,6},{(1,2),(2,3),(3,1),(4,5),(5,6),(6,4),(3,4)}),1)"]] },
    NODESET: { label: "Feedback Node Set", rule: "nodeset", needK: true, cls: "NP-Complete",
      def: "Feedback Node Set is solved by removing at most k nodes so that no cycles remain.",
      solvers: [["brute", "Node Set Brute Force"]],
      examples: [["Redux default", "(({1,2,3,4,5},{(1,2),(2,3),(3,1),(4,5),(5,2),(3,4)}),1)"],
                 ["Two separate cycles, K = 1", "(({1,2,3,4,5,6},{(1,2),(2,3),(3,1),(4,5),(5,6),(6,4),(3,4)}),1)"]] },
    DIRHAM: { label: "Directed Hamiltonian Cycle", rule: "dirham", cls: "NP-Complete",
      def: "Directed Hamiltonian Cycle is the problem of determining whether a directed graph has a Hamiltonian cycle: a cycle that follows the edge directions, visits every vertex exactly once, and returns to the vertex it started from.",
      solvers: [["brute", "Directed Hamiltonian Brute Force"]],
      examples: [["Redux default", "({1,2,3,4,5},{(2,1),(1,3),(2,3),(3,5),(4,2),(5,4)})"],
                 ["Edges point the wrong way", "({1,2,3,4},{(1,2),(2,3),(3,4),(1,4)})"]] },
    SCC: { label: "Strongly Connected Components", rule: "scc", cls: "P",
      def: "A strongly connected component is a maximal group of vertices in a directed graph where every vertex can reach every other vertex in the same group. The goal is to return all such components.",
      solvers: [["kosaraju", "Kosaraju's Algorithm"]],
      examples: [["Redux default", "({1,2,3,4,5},{(1,2),(2,3),(3,1),(3,4),(4,5),(5,4)})"],
                 ["Three components", "({a,b,c,d,e,f,g},{(a,b),(b,c),(c,a),(c,d),(d,e),(e,d),(e,f),(f,g),(g,f)})"]] },
    SSSP: { label: "Single Source Shortest Path", rule: "sssp", weighted: true, needS: true, cls: "P",
      def: "Single Source Shortest Path (SSSP) in a weighted graph is the problem of determining the shortest path from a source vertex to all other reachable vertices in the graph such that the sum of edge weights along each path is minimized.",
      solvers: [["dijkstra", "Dijkstra's Algorithm"]],
      examples: [["Redux default", "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1)"],
                 ["An unreachable node", "({1,2,3,4,5,6},{((1,2),4),((1,3),2),((3,2),1),((2,4),5),((3,4),8),((6,5),1)},1)"]] },
    SPSP: { label: "Single Pair Shortest Path", rule: "spsp", weighted: true, needS: true, needT: true, cls: "P",
      def: "Single Pair Shortest Path (SPSP) in a weighted graph is the problem of finding the shortest path from a given source vertex s and target vertex t in the graph, such that the sum of edge weights along the path is minimized.",
      solvers: [["dijkstra", "Dijkstra's Algorithm"]],
      examples: [["Redux default", "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1,5)"],
                 ["The direct road isn't shortest", "({s,a,b,t},{((s,t),10),((s,a),2),((a,b),3),((b,t),1)},s,t)"]] },
  };

  return { create, parse, PROBLEMS };
})();
