/* ---- Flow network view: Minimum S-T Cut, shown as max flow one augmenting path at a time ----
   Source far left, sink far right, the rest layered by distance from the source. Edges read "flow/capacity".
   Each step is one augmenting path; the last frame splits the nodes into S and T and sums the cut. Pages supply --av-* tokens. */
const FlowView = (() => {
  const NS = "http://www.w3.org/2000/svg";
  const mk = (tag, attrs = {}, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; };
  const NR = 21;

  const css = `
.fl-svg { width: 100%; height: auto; display: block; }
.fl-svg text { font-family: var(--av-mono); }
.fl-node { cursor: pointer; transition: opacity .2s; }
.fl-node:focus { outline: none; }
.fl-node .body { fill: var(--av-bg); stroke: var(--av-stroke); stroke-width: 1.8; transition: fill .2s, stroke .2s; }
.fl-node text { font-size: 14px; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); pointer-events: none; }
.fl-node .role { font-size: 11px; fill: var(--av-muted); }
.fl-node.end .body { stroke-width: 2.6; }
.fl-node.Active .body { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; }
.fl-node.c0 .body { fill: var(--av-g0); stroke: var(--av-g0s); } .fl-node.c1 .body { fill: var(--av-g1); stroke: var(--av-g1s); }
.fl-node.c0 text:not(.role), .fl-node.c1 text:not(.role) { fill: #10131a; font-weight: 600; }
.fl-node.trace .body, .fl-node:focus-visible .body { stroke: var(--av-hot); stroke-width: 3; }
.fl-node.faint { filter: grayscale(1); } .fl-node.faint .body { stroke-dasharray: 3 3; }
.fl-edge { fill: none; stroke: var(--av-edge); transition: opacity .2s, stroke .2s; }
.fl-edge.flowing { stroke: var(--av-stroke); }
.fl-edge.sat { stroke: var(--av-ink); }
.fl-edge.hot { stroke: var(--av-hl); }
.fl-edge.sol { stroke: var(--av-sol); }
.fl-edge.dim { stroke: var(--av-edge-dim); }
.fl-edge.trace { stroke: var(--av-hot); opacity: 1; }
.fl-edge.faint { stroke: var(--av-edge-dim); stroke-dasharray: 3 3; }
.fl-back { fill: none; stroke: var(--av-muted); stroke-width: 1.4; stroke-dasharray: 5 4; }
.fl-back.hot { stroke: var(--av-hl); stroke-width: 3; }
.fl-lab rect { fill: var(--av-surface); stroke: var(--av-line); }
.fl-lab text { font-size: 11.5px; text-anchor: middle; dominant-baseline: central; fill: var(--av-muted); }
.fl-lab.sat rect { fill: var(--av-ink); stroke: var(--av-ink); } .fl-lab.sat text { fill: var(--av-surface); font-weight: 700; }
.fl-lab.hot rect { fill: var(--av-hl); stroke: var(--av-hl); } .fl-lab.hot text { fill: var(--av-on-hl); font-weight: 700; }
.fl-lab.sol rect { fill: var(--av-sol); stroke: var(--av-sol); } .fl-lab.sol text { fill: var(--av-on-sol); font-weight: 700; }
.fl-lab.dim, .fl-lab.faint { filter: grayscale(1); }
.fl-lab.back rect { stroke-dasharray: 3 2; }
.fl-m-def { fill: var(--av-edge); } .fl-m-flow { fill: var(--av-stroke); } .fl-m-sat { fill: var(--av-ink); }
.fl-m-hot { fill: var(--av-hl); } .fl-m-sol { fill: var(--av-sol); } .fl-m-trace { fill: var(--av-hot); } .fl-m-back { fill: var(--av-muted); }
@media (prefers-reduced-motion: reduce) { .fl-node, .fl-node .body, .fl-edge { transition: none; } }`;

  /* ---------- parsing: ({N},{((u,v),c), …},s,t) ---------- */
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
  function parse(str) {
    const top = tree(tokenize(str));
    if (top.t !== "tup" || top.items.length !== 4) throw new Error("Expected (N, E, s, t): nodes, capacitated edges, source, sink.");
    const [Nn, En, s, t] = top.items;
    if (Nn.t !== "set" || En.t !== "set") throw new Error("N and E must be sets.");
    if (typeof s !== "string" || typeof t !== "string") throw new Error("The source and sink must be node names.");
    const nodes = Nn.items.map(x => { if (typeof x !== "string") throw new Error("Node names must be plain names."); return x; });
    if (!nodes.length || new Set(nodes).size !== nodes.length) throw new Error("N must be a non-empty set with no repeats.");
    if (nodes.length > 14) throw new Error("This mockup draws up to 14 nodes.");
    const S = new Set(nodes);
    if (!S.has(s)) throw new Error(`The source ${s} isn't in N.`);
    if (!S.has(t)) throw new Error(`The sink ${t} isn't in N.`);
    if (s === t) throw new Error("The source and sink must be different nodes.");
    const edges = [], byPair = new Map();
    for (const e of En.items) {
      if (e.t !== "tup" || e.items.length !== 2 || !e.items[0] || e.items[0].t !== "tup" || e.items[0].items.length !== 2)
        throw new Error("Each edge must look like ((u,v),capacity).");
      const [u, v] = e.items[0].items, c = Number(e.items[1]);
      if (typeof u !== "string" || typeof v !== "string") throw new Error("Each edge joins two node names.");
      if (!S.has(u) || !S.has(v)) throw new Error(`Edge (${u},${v}) uses a node that isn't in N.`);
      if (u === v) throw new Error(`Edge (${u},${v}) is a loop.`);
      if (!Number.isInteger(c) || c < 0) throw new Error(`Edge (${u},${v}) needs a whole, non-negative capacity.`);
      const k = u + "\u0000" + v;
      if (byPair.has(k)) { byPair.get(k).cap += c; continue; } // repeated edges add up, as in Redux's solver
      const edge = { i: edges.length, u, v, cap: c };
      byPair.set(k, edge); edges.push(edge);
    }
    return { nodes, edges, s, t };
  }

  /* ---------- residual graph helpers ---------- */
  // A residual arc is either forward along edge i (room = cap - flow) or backward against it (room = flow).
  function arcsFrom(G, flow, x) {
    const out = [];
    for (const e of G.edges) {
      if (e.u === x && e.cap - flow[e.i] > 0) out.push({ e, fwd: true, from: e.u, to: e.v, room: e.cap - flow[e.i] });
      if (e.v === x && flow[e.i] > 0) out.push({ e, fwd: false, from: e.v, to: e.u, room: flow[e.i] });
    }
    return out;
  }
  function findPathBFS(G, flow) {
    const prev = new Map([[G.s, null]]), q = [G.s];
    while (q.length) {
      const x = q.shift();
      if (x === G.t) break;
      for (const a of arcsFrom(G, flow, x)) if (!prev.has(a.to)) { prev.set(a.to, a); q.push(a.to); }
    }
    if (!prev.has(G.t)) return null;
    const path = [];
    for (let x = G.t; x !== G.s; x = prev.get(x).from) path.unshift(prev.get(x));
    return path;
  }
  function findPathDFS(G, flow) {
    const seen = new Set([G.s]), path = [];
    function go(x) {
      if (x === G.t) return true;
      for (const a of arcsFrom(G, flow, x)) {
        if (seen.has(a.to)) continue;
        seen.add(a.to); path.push(a);
        if (go(a.to)) return true;
        path.pop();
      }
      return false;
    }
    return go(G.s) ? path : null;
  }
  function reachable(G, flow) {
    const R = new Set([G.s]), q = [G.s];
    while (q.length) { const x = q.shift(); for (const a of arcsFrom(G, flow, x)) if (!R.has(a.to)) { R.add(a.to); q.push(a.to); } }
    return R;
  }
  const fmt = a => "{" + a.join(",") + "}";
  const pathText = (G, path) => [G.s, ...path.map(a => (a.fwd ? "→ " : "⇠ ") + a.to)].join(" ");

  /* ---------- solver: Ford-Fulkerson with BFS (Edmonds-Karp, Redux's solver) or DFS ---------- */
  function solve(G, how) {
    const flow = G.edges.map(() => 0), frames = [];
    let value = 0, n = 0;
    const find = how === "dfs" ? findPathDFS : findPathBFS;
    for (;;) {
      const path = find(G, flow);
      if (!path) break;
      if (++n > 200) break; // capacities are integers, so this only guards against huge inputs
      const b = Math.min(...path.map(a => a.room));
      const tight = path.find(a => a.room === b);
      path.forEach(a => { flow[a.e.i] += a.fwd ? b : -b; });
      value += b;
      const undo = path.filter(a => !a.fwd);
      const undoText = undo.length ? ` It runs backward over ${undo.map(a => `${a.e.u}→${a.e.v}`).join(" and ")}, taking back flow sent there earlier.` : "";
      frames.push({ flow: flow.slice(), value, path, sym: n,
        caption: `Path ${n}: ${pathText(G, path)}. The bottleneck is ${b}, at ${tight.fwd ? `${tight.e.u}→${tight.e.v}` : `${tight.e.u}→${tight.e.v} (backward)`}, so push ${b}. Flow is now ${value}.${undoText}` });
    }
    const S = reachable(G, flow);
    const cut = G.edges.filter(e => S.has(e.u) && !S.has(e.v));
    const cap = cut.reduce((a, e) => a + e.cap, 0);
    frames.push({ flow: flow.slice(), value, done: true, ok: true, S: [...S], cut: cut.map(e => e.i), cap,
      caption: value === 0 && !frames.length
        ? `There is no path from ${G.s} to ${G.t} at all, so the maximum flow and the minimum cut are both 0.`
        : `No augmenting path is left. The nodes still reachable from ${G.s} form S = ${fmt(G.nodes.filter(x => S.has(x)))}. The ${cut.length} edge${cut.length === 1 ? "" : "s"} from S to T are full and add up to ${cap}, equal to the maximum flow.` });
    return frames;
  }

  /* ---------- layout: s left, t right, everything else by BFS distance from s ---------- */
  function layout(G, W) {
    const dist = new Map([[G.s, 0]]), q = [G.s];
    while (q.length) {
      const x = q.shift();
      for (const e of G.edges) if (e.u === x && !dist.has(e.v) && e.v !== G.t) { dist.set(e.v, dist.get(x) + 1); q.push(e.v); }
    }
    let maxL = 0;
    G.nodes.forEach(x => { if (x !== G.t && dist.has(x)) maxL = Math.max(maxL, dist.get(x)); });
    const unreached = G.nodes.filter(x => x !== G.t && !dist.has(x));
    if (unreached.length) { maxL = Math.max(maxL, 1); unreached.forEach(x => dist.set(x, maxL)); }
    dist.set(G.t, maxL + 1);
    const cols = [];
    G.nodes.forEach(x => (cols[dist.get(x)] ||= []).push(x));
    const rows = Math.max(...cols.filter(Boolean).map(c => c.length));
    const H = Math.max(300, rows * 110 + 140);
    const P = new Map(), last = maxL + 1;
    cols.forEach((c, li) => c.forEach((x, i) => P.set(x, { x: 70 + (li * (W - 140)) / last, y: H / 2 + 6 + (i - (c.length - 1) / 2) * 110, col: li })));
    return { P, H };
  }
  // distance from point p to segment ab
  const segDist = (p, a, b) => {
    const dx = b.x - a.x, dy = b.y - a.y, L = dx * dx + dy * dy || 1;
    const t = Math.max(0, Math.min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / L));
    return Math.hypot(p.x - (a.x + t * dx), p.y - (a.y + t * dy));
  };
  // a quadratic arc from a to b bowed by `off` to the left of travel; endpoints trimmed to the node rims
  function arc(a, b, off) {
    const dx = b.x - a.x, dy = b.y - a.y, len = Math.hypot(dx, dy), nx = dy / len, ny = -dx / len;
    const cx = (a.x + b.x) / 2 + nx * off * 2, cy = (a.y + b.y) / 2 + ny * off * 2;
    const sl = Math.hypot(cx - a.x, cy - a.y), tl = Math.hypot(cx - b.x, cy - b.y);
    const s = [a.x + ((cx - a.x) / sl) * NR, a.y + ((cy - a.y) / sl) * NR];
    const t = [b.x + ((cx - b.x) / tl) * (NR + 2), b.y + ((cy - b.y) / tl) * (NR + 2)];
    return { d: off ? `M${s} Q${cx},${cy} ${t}` : `M${s} L${t}`, lx: 0.49 * s[0] + 0.42 * cx + 0.09 * t[0], ly: 0.49 * s[1] + 0.42 * cy + 0.09 * t[1] };
  }

  function create({ svg }) {
    if (!document.getElementById("fl-style")) {
      const st = document.createElement("style"); st.id = "fl-style"; st.textContent = css; document.head.appendChild(st);
    }
    svg.classList.add("fl-svg");
    let G, frames, frame = null, hover = null, residual = false, els;

    function label(g, x, y, text, cls) {
      const w = text.length * 7.2 + 14;
      const lg = mk("g", { class: "fl-lab" + (cls ? " " + cls : "") }, g);
      mk("rect", { x: x - w / 2, y: y - 10, width: w, height: 20, rx: 10 }, lg);
      mk("text", { x, y }, lg).textContent = text;
      return lg;
    }

    function draw() {
      svg.innerHTML = "";
      const W = 700, { P, H } = layout(G, W);
      svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
      const defs = mk("defs", {}, svg);
      for (const k of ["def", "flow", "sat", "hot", "sol", "trace", "back"]) {
        const m = mk("marker", { id: `fl-arrow-${k}`, viewBox: "0 0 10 10", refX: 9, refY: 5, markerWidth: 9, markerHeight: 9, markerUnits: "userSpaceOnUse", orient: "auto" }, defs);
        mk("path", { d: "M0,0 L10,5 L0,10 z", class: `fl-m-${k}` }, m);
      }
      const gE = mk("g", {}, svg), gB = mk("g", {}, svg), gL = mk("g", {}, svg), gN = mk("g", {}, svg);
      const pair = new Set(G.edges.map(e => e.u + "\u0000" + e.v));
      els = { P, gB, edges: [], nodes: new Map() };
      G.edges.forEach(e => {
        const a = P.get(e.u), b = P.get(e.v);
        let off = pair.has(e.v + "\u0000" + e.u) ? 22 : 0;
        if (G.nodes.some(x => x !== e.u && x !== e.v && segDist(P.get(x), a, b) < NR + 10)) off = Math.max(off, 34);
        const g = arc(a, b, off);
        const path = mk("path", { d: g.d, class: "fl-edge" }, gE);
        els.edges.push({ e, path, off, lx: g.lx, ly: g.ly, lab: null, gL });
      });
      G.nodes.forEach(x => {
        const p = P.get(x), end = x === G.s || x === G.t;
        const g = mk("g", { class: "fl-node" + (end ? " end" : ""), tabindex: "0", role: "button", "aria-label": `Node ${x}${x === G.s ? ", source" : x === G.t ? ", sink" : ""}` }, gN);
        mk("circle", { cx: p.x, cy: p.y, r: NR, class: "body" }, g);
        mk("text", { x: p.x, y: p.y }, g).textContent = x;
        if (end) mk("text", { x: p.x, y: p.y + NR + 14, class: "role" }, g).textContent = x === G.s ? "source" : "sink";
        const on = () => { hover = x; paint(); }, off = () => { hover = null; paint(); };
        g.addEventListener("pointerenter", on); g.addEventListener("pointerleave", off);
        g.addEventListener("focus", on); g.addEventListener("blur", off);
        g.addEventListener("click", () => { hover = hover === x ? null : x; paint(); });
        els.nodes.set(x, g);
      });
    }

    function paint() {
      const f = frame, done = f && f.done;
      const flow = f ? f.flow : null;
      const onPath = new Map();
      if (f && !done && f.path) f.path.forEach(a => onPath.set(a.e.i, a.fwd));
      const S = done ? new Set(f.S) : null, cut = done ? new Set(f.cut) : null;
      const pathNodes = new Set(f && f.path && !done ? [G.s, ...f.path.map(a => a.to)] : []);
      for (const [x, g] of els.nodes) {
        let cls = "fl-node" + (x === G.s || x === G.t ? " end" : "");
        if (done) cls += S.has(x) ? " c0" : " c1";
        else if (pathNodes.has(x)) cls += " Active";
        if (hover) cls += x === hover ? " trace" : G.edges.some(e => (e.u === hover && e.v === x) || (e.v === hover && e.u === x)) ? "" : " faint";
        g.setAttribute("class", cls);
      }
      els.edges.forEach(o => {
        const e = o.e, fl = flow ? flow[e.i] : 0, sat = flow && e.cap > 0 && fl === e.cap;
        let st = "";
        if (done) st = cut.has(e.i) ? "sol" : (S.has(e.u) && S.has(e.v)) || (!S.has(e.u) && !S.has(e.v)) ? "" : "dim";
        else if (onPath.get(e.i) === true) st = "hot";
        else if (sat) st = "sat";
        else if (fl > 0) st = "flowing";
        if (hover) st = e.u === hover || e.v === hover ? "trace" : "faint";
        o.path.setAttribute("class", "fl-edge" + (st ? " " + st : ""));
        const width = st === "sol" ? 4 : st === "hot" ? 3.4 : 1.4 + (e.cap ? (3 * fl) / e.cap : 0);
        o.path.style.strokeWidth = width;
        o.path.setAttribute("marker-end", `url(#fl-arrow-${st === "sol" ? "sol" : st === "hot" ? "hot" : st === "trace" ? "trace" : st === "sat" ? "sat" : st === "flowing" ? "flow" : "def"})`);
        if (o.lab) o.lab.remove();
        const text = flow ? `${fl}/${e.cap}` : String(e.cap);
        o.lab = label(o.gL, o.lx, o.ly, text, st === "flowing" ? "" : st);
      });
      // backward residual arcs: always when the residual view is on, and whenever the current path uses one
      els.gB.innerHTML = "";
      if (flow && !hover) els.edges.forEach(o => {
        const e = o.e, fl = flow[e.i], used = onPath.get(e.i) === false;
        if (!(used || (residual && fl > 0 && !done))) return;
        const g = arc(els.P.get(e.v), els.P.get(e.u), o.off + 26);
        mk("path", { d: g.d, class: "fl-back" + (used ? " hot" : ""), "marker-end": `url(#fl-arrow-${used ? "hot" : "back"})` }, els.gB);
        label(els.gB, g.lx, g.ly, `↶${fl}`, "back" + (used ? " hot" : ""));
      });
    }

    function checks(f) {
      if (!f) return [];
      if (!f.done) return [
        { label: `Flow value: ${f.value || 0}`, ok: null },
        { label: `Augmenting paths so far: ${f.sym || 0}`, ok: null },
      ];
      return [
        { label: `Flow value: ${f.value}`, ok: true },
        { label: `Cut capacity: ${f.cap}`, ok: true },
        { label: `Max flow = min cut: ${f.value} = ${f.cap}`, ok: f.value === f.cap },
      ];
    }

    return {
      load(str, problemKey, solverKey) {
        G = parse(str);
        frames = solve(G, solverKey === "dfs" ? "dfs" : "bfs");
        frames.unshift({ flow: G.edges.map(() => 0), value: 0, sym: 0,
          caption: `Step 0: no flow yet. Each step finds a path from ${G.s} to ${G.t} with room left and pushes as much as it can.` });
        frame = null; hover = null;
        draw(); paint();
        return { frames, ok: true };
      },
      setResidual(on) { residual = !!on; if (G) paint(); },
      show(i) { frame = i === null ? null : frames[i]; paint(); return frame; },
      checks(i) { return checks(i === null ? null : frames[i]); },
      chosen(i) {
        const f = i === null ? null : frames[i];
        if (!f) return [];
        if (f.done) return [{ text: "S " + fmt(G.nodes.filter(x => f.S.includes(x))), color: 0 }, { text: "T " + fmt(G.nodes.filter(x => !f.S.includes(x))), color: 1 }];
        if (f.path) return [{ text: `${pathText(G, f.path)}  (+${Math.min(...f.path.map(a => a.room))})` }];
        return [];
      },
      resetHover() { hover = null; paint(); },
    };
  }

  /* Catalog. The name, definition, default and Edmonds-Karp come from Redux's P_MINSTCUT. */
  const PROBLEMS = {
    MINSTCUT: {
      label: "Minimum S-T Cut",
      def: "Given a weighted directed graph with non-negative edge capacities, a source node s, and a sink node t, find a partition of the vertices into S (containing s) and T (containing t) such that the total capacity of edges directed from S to T is minimized. By the Max-Flow Min-Cut theorem, this minimum cut capacity equals the maximum flow from s to t.",
      solvers: [["bfs", "Edmonds-Karp Algorithm"], ["dfs", "Ford-Fulkerson, depth-first (not in Redux yet)"]],
      examples: [
        ["Redux default", "({1,2,3,4},{((1,2),10),((1,3),2),((2,4),3),((3,4),5)},1,4)"],
        ["Cut isn't the smallest edge", "({s,a,b,c,t},{((s,a),4),((s,b),4),((a,c),3),((b,c),3),((a,b),1),((c,t),5),((b,t),2)},s,t)"],
        ["Flow gets taken back", "({s,a,b,c,d,t},{((s,a),1),((s,c),1),((a,b),1),((a,d),1),((c,b),1),((b,t),1),((d,t),1)},s,t)"],
      ],
    },
  };

  return { create, parse, PROBLEMS };
})();
