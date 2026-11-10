/* ---- FlowNetwork (#668): payload {kind:"flowNetwork", layout:"sourceToSink", rule, source, sink,
   nodes[{id,label,layer,row}], edges[{id,a,b,capacity}], layers[[ids]]}
   frames {event, caption, focus, nodes{id:{state}}, edges{id:{state}}, flow{edgeId:units}, value,
   path[{edge,forward,units}], groups?{node:0|1} (Done: 0 = S side), cut?[edgeIds], ok?}
   Drawn with FlowView (../mockups/flow.js) through its loadModel hook. flow.js paints from flow numbers, the augmenting
   path and the final S/T split (not from state names), so the adapter converts each frame into those terms, then
   checks the backend's state names against what flow.js derives and notes any disagreement. Positions come from the
   backend's layer/row. */
(() => {
  const NR = 21;

  function model(payload) {
    const nodes = payload.nodes.map(n => n.id);
    const edges = payload.edges.map((e, i) => ({ i, u: e.a, v: e.b, cap: e.capacity ?? e.cap ?? 0, id: e.id }));
    const G = { nodes, edges, s: payload.source, t: payload.sink ?? payload.target };
    const L = Math.max(0, ...payload.nodes.map(n => n.layer ?? 0));
    const cols = Array.from({ length: L + 1 }, () => []);
    payload.nodes.forEach(n => cols[n.layer ?? 0].push(n));
    cols.forEach(c => c.sort((a, b) => (a.row ?? 0) - (b.row ?? 0)));
    // Same spacing as flow.js layout(): columns spread across W, rows 110 apart, centred.
    G.fixedLayout = W => {
      const rows = Math.max(1, ...cols.map(c => c.length)), H = Math.max(300, rows * 110 + 140), P = new Map(), last = Math.max(1, L);
      cols.forEach((c, li) => c.forEach((n, i) => P.set(n.id, { x: 70 + (li * (W - 140)) / last, y: H / 2 + 6 + (i - (c.length - 1) / 2) * 110, col: li })));
      return { P, H };
    };
    return G;
  }

  /** Backend frame → flow.js frame ({flow[], value, path[arcs], done, S, cut[], cap}). */
  function convert(f, G, byId, note) {
    const flow = G.edges.map(e => (f.flow && f.flow[e.id] != null ? +f.flow[e.id] : 0));
    if (!f.flow) note("A frame has no flow map; drawn with zero flow.");
    const out = { flow, value: f.value ?? 0, caption: f.caption };
    if (Array.isArray(f.path) && f.path.length) {
      out.path = f.path.map(p => {
        const e = byId.get(p.edge);
        if (!e) { note(`Path names edge "${p.edge}", which is not in the payload.`); return null; }
        return p.forward === false ? { e, fwd: false, from: e.v, to: e.u, room: p.units } : { e, fwd: true, from: e.u, to: e.v, room: p.units };
      }).filter(Boolean);
    }
    if (f.groups && Object.keys(f.groups).length) {
      out.done = true; out.ok = f.ok;
      out.S = G.nodes.filter(n => +f.groups[n] === 0);
      const cut = (f.cut || []).map(id => byId.get(id)).filter(Boolean);
      out.cut = cut.map(e => e.i); out.cap = cut.reduce((a, e) => a + e.cap, 0);
    }
    return out;
  }

  /** What flow.js will draw for a converted frame, in state names, to compare with the backend's. */
  function derived(cf, G) {
    const nodes = {}, edges = {};
    if (cf.done) {
      const S = new Set(cf.S), cut = new Set(cf.cut);
      G.edges.forEach(e => { if (cut.has(e.i)) edges[e.id] = "Solution"; });
    } else {
      const onPath = new Map((cf.path || []).map(a => [a.e.i, a.fwd]));
      if (cf.path) [G.s, ...cf.path.map(a => a.to)].forEach(n => { nodes[n] = "ElementHighlight"; });
      G.edges.forEach(e => {
        if (onPath.has(e.i)) edges[e.id] = "ElementHighlight";
        else if (e.cap > 0 && cf.flow[e.i] === e.cap) edges[e.id] = "Covered";
      });
    }
    return { nodes, edges };
  }

  Viewer.register("FlowNetwork", {
    mount(ctx) {
      const { payload, note } = ctx;
      const svg = Viewer.svgIn(ctx.host, `${ctx.problem} flow network`);
      const view = FlowView.create({ svg });
      const G = model(payload);
      const byId = new Map(G.edges.map(e => [e.id, e]));
      const known = new Set(["event", "caption", "focus", "nodes", "edges", "flow", "value", "path", "groups", "cut", "ok", "phase", "builtNodes", "builtEdges"]);
      const extra = [...new Set(ctx.frames.flatMap(f => Object.keys(f)))].filter(k => !known.has(k));
      if (extra.length) note(`Frame fields the FlowNetwork adapter ignores: ${extra.join(", ")}`, false);
      note("flow.js derives its colors from flow numbers, the augmenting path and the S/T split, not from state names; the adapter feeds it those and cross-checks the backend's states.", false);
      const frames = ctx.frames.map(f => convert(f, G, byId, note));
      // Cross-check: every non-Background state the backend sends vs what flow.js shows.
      ctx.frames.forEach((f, i) => {
        const d = derived(frames[i], G);
        for (const kind of ["nodes", "edges"]) {
          const ids = new Set([...Object.keys(f[kind] || {}), ...Object.keys(d[kind])]);
          for (const id of ids) {
            const b = Viewer.stateOf(f[kind], id), m = d[kind][id] || "Background";
            if (b !== m) note(`Frame ${i + 1}: ${kind === "nodes" ? "node" : "edge"} ${id} is ${b} in the JSON, flow.js draws it as ${m}.`);
          }
        }
      });
      view.loadModel(G, frames);
      const paths = [...svg.querySelectorAll("path.fl-edge")], groups = [...svg.querySelectorAll("g.fl-node")];
      paths.forEach((p, i) => Viewer.tag(p, G.edges[i].id));
      groups.forEach((g, i) => {
        Viewer.tag(g, G.nodes[i]);
        const lbl = payload.nodes[i].label;
        if (lbl != null && lbl !== G.nodes[i]) { const t = g.querySelector("text"); if (t) t.textContent = lbl; }
      });
      const nodeEls = new Map(groups.map((g, i) => [G.nodes[i], g])), edgeEls = new Map(paths.map((p, i) => [G.edges[i].id, p]));
      return {
        show(i) {
          view.show(i);
          const f = ctx.frames[i];
          const bo = ctx.buildOrder || { nodes: G.nodes, edges: G.edges.map(e => e.id) };
          const bn = f.builtNodes != null ? new Set(bo.nodes.slice(0, f.builtNodes)) : null;
          const be = f.builtEdges != null ? new Set(bo.edges.slice(0, f.builtEdges)) : null;
          G.nodes.forEach(n => { nodeEls.get(n).style.display = bn && !bn.has(n) ? "none" : ""; });
          G.edges.forEach(e => { edgeEls.get(e.id).style.display = be && !be.has(e.id) ? "none" : ""; });
        },
      };
    },
  });
})();
