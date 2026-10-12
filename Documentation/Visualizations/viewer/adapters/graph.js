/* ---- Graph (#663): payload {kind:"graph", subtype, layout, nodes[{id,label,terminal?}], edges[{id,a,b,weight?}], shape, rule, k?}
   frames {event, caption, focus, nodes{id:{state}}, edges{id:{state}}, phantoms[{a,b,state}], groups?{node:color}, order?[ids], ok?}
   Drawn with GraphView (../mockups/graphbase.js) through its loadModel hook: the graph is built from the payload (not
   re-parsed from the instance) and each frame carries its states already evaluated, so the colors are the backend's. */
(() => {
  const NODE_CLASS = { ElementHighlight: "Active", Solution: "Solution", Covered: "Covered", Rejected: "Rejected", Blocked: "Blocked" };
  const EDGE_CLASS = { ElementHighlight: "hot", Solution: "sol", Covered: "cov", Rejected: "rej" };

  function model(payload) {
    const nodes = payload.nodes.map(n => n.id);
    const edges = payload.edges.map(e => ({ a: e.a, b: e.b, w: e.weight ?? e.w ?? null, k: e.id }));
    const adj = new Map(nodes.map(n => [n, new Set()]));
    edges.forEach(e => { adj.get(e.a)?.add(e.b); adj.get(e.b)?.add(e.a); });
    const terms = payload.nodes.filter(n => n.terminal).map(n => n.id);
    return {
      nodes, edges, K: payload.k ?? null, adj, W: new Map(edges.map(e => [e.k, e.w])),
      terminals: terms.length ? new Set(terms) : null, byKey: new Map(edges.map(e => [e.k, e])), has: (a, b) => adj.get(a)?.has(b),
    };
  }

  /** Backend frame → GraphView's evaluated states ({nodes, edges, phantom, checks}). */
  function evaluate(f, note) {
    const ev = { nodes: new Map(), edges: new Map(), phantom: [], checks: [] };
    const setN = (n, v) => ev.nodes.set(n, Object.assign(ev.nodes.get(n) || {}, v));
    for (const [id, v] of Object.entries(f.nodes || {})) {
      const st = Viewer.stateOf(f.nodes, id);
      if (st === "Background") continue;
      if (NODE_CLASS[st]) setN(id, { st: NODE_CLASS[st] });
      else note(`Node state "${st}" has no style in graphbase.js (drawn as Background).`);
    }
    for (const [id, c] of Object.entries(f.groups || {})) setN(id, { color: +c });
    if (Array.isArray(f.order)) {
      const seen = new Set();
      f.order.forEach((n, i) => { if (!seen.has(n)) { seen.add(n); setN(n, { order: i + 1 }); } });
    }
    for (const id of Object.keys(f.edges || {})) {
      const st = Viewer.stateOf(f.edges, id);
      if (st === "Background") continue;
      if (EDGE_CLASS[st]) ev.edges.set(id, EDGE_CLASS[st]);
      else note(`Edge state "${st}" has no style in graphbase.js (drawn as Background).`);
    }
    for (const p of f.phantoms || []) {
      ev.phantom.push([p.a, p.b]);
      if (p.state && p.state !== "Rejected") note(`Phantom (missing edge) with state "${p.state}"; graphbase.js draws every phantom as Rejected.`);
    }
    return ev;
  }

  Viewer.register("Graph", {
    mount(ctx) {
      const { payload, note } = ctx;
      const svg = Viewer.svgIn(ctx.host, `${ctx.problem} graph`);
      const view = GraphView.create({ svg });
      const G = model(payload);
      if (payload.subtype && payload.subtype !== "undirected") note(`subtype "${payload.subtype}": graphbase.js has no arrowheads; the viewer adds them.`, false);
      if (G.nodes.length > 16) note(`${G.nodes.length} nodes: the mockup parser caps graphs at 16 nodes (the viewer builds the graph from the payload, so it still draws).`, false);
      const directed = payload.subtype === "directed";
      const frames = ctx.frames.map(f => ({ ...f, ev: evaluate(f, note) }));
      const known = new Set(["event", "caption", "focus", "nodes", "edges", "phantoms", "groups", "order", "ok", "phase", "builtNodes", "builtEdges", "skipped"]);
      const extra = [...new Set(ctx.frames.flatMap(f => Object.keys(f)))].filter(k => !known.has(k));
      if (extra.length) note(`Frame fields the Graph adapter ignores: ${extra.join(", ")}`, false);
      view.loadModel(G, { rule: payload.rule, layout: payload.layout === "circle" ? "circle" : "force" }, frames);

      // Tag pieces with backend ids (draw() appends edges, then nodes, in model order).
      const lines = [...svg.querySelectorAll("line.gb-edge")], groups = [...svg.querySelectorAll("g.gb-node")];
      lines.forEach((l, i) => Viewer.tag(l, G.edges[i].k));
      groups.forEach((g, i) => {
        Viewer.tag(g, G.nodes[i]);
        const lbl = payload.nodes[i].label;
        if (lbl != null && lbl !== G.nodes[i]) { const t = g.querySelector("text"); if (t) t.textContent = lbl; }
      });
      if (directed) {
        const defs = Viewer.mk("defs", {}, svg);
        const m = Viewer.mk("marker", { id: "vv-arrow-" + Math.random().toString(36).slice(2), viewBox: "0 0 10 10", refX: "10", refY: "5", markerWidth: "7", markerHeight: "7", orient: "auto-start-reverse", markerUnits: "userSpaceOnUse" }, defs);
        Viewer.mk("path", { d: "M0,0 L10,5 L0,10 z", style: "fill: var(--av-edge)" }, m);
        lines.forEach(l => {
          const x1 = +l.getAttribute("x1"), y1 = +l.getAttribute("y1"), x2 = +l.getAttribute("x2"), y2 = +l.getAttribute("y2");
          const d = Math.hypot(x2 - x1, y2 - y1) || 1, r = 21;
          l.setAttribute("x2", x2 - (x2 - x1) / d * r); l.setAttribute("y2", y2 - (y2 - y1) / d * r);
          l.setAttribute("marker-end", `url(#${m.id})`);
        });
      }
      const nodeEls = new Map(groups.map((g, i) => [G.nodes[i], g])), edgeEls = new Map(lines.map((l, i) => [G.edges[i].k, l]));
      const wEls = [...svg.querySelectorAll("g.gb-w")], weighted = G.edges.filter(e => e.w !== null);
      const weightEls = new Map(wEls.map((w, i) => [weighted[i].k, w]));
      const pos = id => { const c = nodeEls.get(id)?.querySelector("circle.body, rect.body"); if (!c) return null;
        return c.tagName === "circle" ? { x: +c.getAttribute("cx"), y: +c.getAttribute("cy") } : { x: +c.getAttribute("x") + +c.getAttribute("width") / 2, y: +c.getAttribute("y") + +c.getAttribute("height") / 2 }; };
      const skipLayer = Viewer.mk("g", { class: "vv-skipped" });
      svg.insertBefore(skipLayer, svg.children[1] || null);
      return {
        show(i) {
          const f = frames[i];
          view.show(i);
          // Reduction View build phase: only the first builtNodes / builtEdges of buildOrder are on screen yet.
          const bn = f.builtNodes, be = f.builtEdges, bo = ctx.buildOrder;
          const shownN = bn != null && bo ? new Set(bo.nodes.slice(0, bn)) : null, shownE = be != null && bo ? new Set(bo.edges.slice(0, be)) : null;
          if (bn != null && !bo) note("Build frame has builtNodes but the response has no target.buildOrder.");
          for (const [n, el] of nodeEls) el.style.display = shownN && !shownN.has(n) ? "none" : "";
          for (const [k, el] of edgeEls) el.style.display = shownE && !shownE.has(k) ? "none" : "";
          for (const [k, el] of weightEls) el.style.display = shownE && !shownE.has(k) ? "none" : "";
          // Pairs the construction rule considered and left without an edge: drawn like the mockup's reduction view, as phantoms.
          skipLayer.innerHTML = "";
          for (const p of f.skipped || []) {
            const a = pos(p.a), b = pos(p.b);
            if (a && b) Viewer.mk("line", { x1: a.x, y1: a.y, x2: b.x, y2: b.y, class: "gb-phantom" }, skipLayer);
          }
        },
      };
    },
  });
})();
