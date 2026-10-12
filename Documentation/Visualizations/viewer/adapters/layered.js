/* ---- LayeredGraph (#667): payload {kind:"layeredGraph", layout:"ranked", shape, rule, directed, weighted,
   nodes[{id,label,layer,row}], edges[{id,a,b,back,weight?}], layers[[ids]], k?, source?, target?}
   frames {event, caption, focus, nodes{id:{state}}, edges{id:{state}}, order?[ids], groups?{node:color}, finish?[ids],
   dist?{node:number|null}, pred?{node:node}, ok?}
   Drawn with LayeredView (../mockups/layered.js) through its loadModel hook. Positions come from the backend's
   layer/row/back (the backend ranks the graph); the mockup's arrows, curves and labels draw them. */
(() => {
  const SEP = "\u0000", DX = 150, DY = 84;
  const dkey = (u, v) => u + SEP + v;
  const RULES = { topologicalSort: "topo", feedbackArcSet: "arcset", feedbackNodeSet: "nodeset", stronglyConnectedComponents: "scc",
    singleSourceShortestPath: "sssp", singlePairShortestPath: "spsp", directedHamiltonianCycle: "dirham", directedHamiltonian: "dirham" };
  const NODE_CLASS = { ElementHighlight: "Active", Solution: "Solution", Covered: "Covered", Rejected: "Rejected", Blocked: "Blocked", Untraveled: "Untraveled" };
  const EDGE_CLASS = { ElementHighlight: "hot", Solution: "sol", Covered: "cov", Rejected: "rej", Untraveled: "dim" };

  function model(payload) {
    const nodes = payload.nodes.map(n => n.id);
    const idToKey = new Map();
    const edges = payload.edges.map(e => { const k = dkey(e.a, e.b); idToKey.set(e.id, k); return { u: e.a, v: e.b, w: e.weight ?? null, k, undirected: payload.directed === false, id: e.id }; });
    const out = new Map(nodes.map(n => [n, []])), inn = new Map(nodes.map(n => [n, []]));
    edges.forEach(e => { out.get(e.u)?.push(e); inn.get(e.v)?.push(e); });
    const seen = new Set(edges.map(e => e.k));
    const G = { nodes, edges, K: payload.k ?? null, s: payload.source ?? null, t: payload.target ?? null, out, inn,
      has: (u, v) => seen.has(dkey(u, v)), byKey: new Map(edges.map(e => [e.k, e])) };
    // The backend's ranking, in the shape layout() returns (same spacing as layered.js).
    const rank = new Map(payload.nodes.map(n => [n.id, n.layer ?? 0]));
    const R = Math.max(0, ...rank.values());
    const cols = Array.from({ length: R + 1 }, () => []);
    payload.nodes.forEach(n => cols[n.layer ?? 0].push(n));
    cols.forEach(c => c.sort((a, b) => (a.row ?? 0) - (b.row ?? 0)));
    const back = new Set(payload.edges.filter(e => e.back).map(e => idToKey.get(e.id)));
    const rows = Math.max(1, ...cols.map(c => c.length));
    const W = Math.max(640, 100 + R * DX + 100), top0 = 70, Hrows = rows * DY;
    const H = top0 + Hrows + 40 + (back.size ? 30 + 14 * Math.min(back.size, 4) : 0);
    const ox = (W - R * DX) / 2, P = new Map();
    cols.forEach((c, r) => c.forEach((n, i) => P.set(n.id, { x: ox + r * DX, y: top0 + Hrows / 2 + (i - (c.length - 1) / 2) * DY })));
    G.fixedLayout = { P, back, rank, W, H, bottom: top0 + Hrows };
    return { G, idToKey };
  }

  function evaluate(f, rule, idToKey, note) {
    const ev = { nodes: new Map(), edges: new Map(), phantom: [], checks: [] };
    const setN = (n, v) => ev.nodes.set(n, Object.assign(ev.nodes.get(n) || {}, v));
    for (const id of Object.keys(f.nodes || {})) {
      const st = Viewer.stateOf(f.nodes, id);
      if (st === "Background") continue;
      if (NODE_CLASS[st]) setN(id, { st: NODE_CLASS[st] }); else note(`Node state "${st}" has no style in layered.js (drawn as Background).`);
    }
    for (const [id, c] of Object.entries(f.groups || {})) setN(id, { color: +c });
    if (Array.isArray(f.order)) f.order.forEach((n, i) => setN(n, { order: i + 1 }));
    if (Array.isArray(f.finish)) f.finish.forEach((n, i) => setN(n, { tag: "#" + (i + 1) }));
    for (const id of Object.keys(f.edges || {})) {
      const st = Viewer.stateOf(f.edges, id), k = idToKey.get(id);
      if (st === "Background") continue;
      if (!k) { note(`Frame names edge "${id}", which is not in the payload.`); continue; }
      // Feedback Arc Set: the removed edges are the answer; layered.js draws removed edges as "cut" (dashed green).
      if (rule === "arcset" && st === "Solution") ev.edges.set(k, "cut");
      else if (EDGE_CLASS[st]) ev.edges.set(k, EDGE_CLASS[st]);
      else note(`Edge state "${st}" has no style in layered.js (drawn as Background).`);
    }
    for (const p of f.phantoms || []) ev.phantom.push([p.a, p.b]);
    if (f.dist) ev.dist = Object.fromEntries(Object.entries(f.dist).map(([n, d]) => [n, d === null ? Infinity : d]));
    return ev;
  }

  Viewer.register("LayeredGraph", {
    mount(ctx) {
      const { payload, note } = ctx;
      const rule = RULES[payload.rule];
      if (!rule) note(`Unknown LayeredGraph rule "${payload.rule}" (no mockup rule); drawn without rule-specific labels.`);
      if (payload.directed === false) note("directed:false: layered.js draws every edge with an arrow unless it is a {u,v} pair.", false);
      const svg = Viewer.svgIn(ctx.host, `${ctx.problem} layered graph`);
      const view = LayeredView.create({ svg });
      const { G, idToKey } = model(payload);
      if (G.nodes.length > 14) note(`${G.nodes.length} nodes: the mockup parser caps layered graphs at 14 nodes (the viewer builds the graph from the payload, so it still draws).`, false);
      if (rule === "arcset") note("Feedback Arc Set marks removed edges as Solution; layered.js has a separate \"cut\" style (dashed green) for removed edges, which the adapter uses.", false);
      if (ctx.frames.some(f => Object.values(f.edges || {}).some(v => (v.state || v) === "Untraveled"))) note("Edges in state Untraveled (removed with a node): layered.js has no Untraveled edge style; drawn as dim.", false);
      const known = new Set(["event", "caption", "focus", "nodes", "edges", "order", "groups", "finish", "dist", "pred", "phantoms", "ok", "phase", "builtNodes", "builtEdges"]);
      const extra = [...new Set(ctx.frames.flatMap(f => Object.keys(f)))].filter(k => !known.has(k));
      if (extra.length) note(`Frame fields the LayeredGraph adapter ignores: ${extra.join(", ")}`, false);
      const frames = ctx.frames.map(f => ({ ...f, ev: evaluate(f, rule, idToKey, note) }));
      view.loadModel(G, { rule: rule || payload.rule }, frames);

      // Tag pieces: draw() appends one path per edge (directed graphs), then one g per node, in model order.
      const paths = [...svg.querySelectorAll("path.ly-edge")], groups = [...svg.querySelectorAll("g.ly-node")];
      const drawnEdges = G.edges.filter((e, i) => !(e.undirected && G.edges.slice(0, i).some(x => x.u === e.v && x.v === e.u)));
      paths.forEach((p, i) => drawnEdges[i] && Viewer.tag(p, drawnEdges[i].id));
      groups.forEach((g, i) => {
        Viewer.tag(g, G.nodes[i]);
        const lbl = payload.nodes[i].label;
        if (lbl != null && lbl !== G.nodes[i]) { const t = g.querySelector("text.nm"); if (t) t.textContent = lbl; }
      });
      const nodeEls = new Map(groups.map((g, i) => [G.nodes[i], g]));
      const edgeEls = new Map(paths.map((p, i) => [drawnEdges[i]?.id, p]));
      return {
        show(i) {
          const f = frames[i];
          view.show(i);
          // Reduction build phase: only the first builtNodes / builtEdges of the construction order exist yet.
          const bo = ctx.buildOrder || { nodes: G.nodes, edges: payload.edges.map(e => e.id) };
          const bn = f.builtNodes != null ? new Set(bo.nodes.slice(0, f.builtNodes)) : null;
          const be = f.builtEdges != null ? new Set(bo.edges.slice(0, f.builtEdges)) : null;
          G.nodes.forEach(n => { nodeEls.get(n).style.display = bn && !bn.has(n) ? "none" : ""; });
          payload.edges.forEach(e => { const el = edgeEls.get(e.id); if (el) el.style.display = be && !be.has(e.id) ? "none" : ""; });
        },
      };
    },
  });
})();
