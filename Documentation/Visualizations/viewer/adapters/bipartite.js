/* ---- Bipartite graph (#665): payload {kind:"bipartiteGraph", layout:"columns", pickSide:"sets"|"elements"|"assignment",
   rule:"atLeastOnce"|"exactlyOnce"|"satisfiability", elements[{id,label}], sets[{id,label,members[ids]}], edges[{id,a,b,positive?}], k?}
   frames {event, caption, focus, nodes{id:{state}}, edges{id:{state}}, hits{id:n}, assignment?{var:bool}, ok?}
   Set problems → BipartiteView.create (loadModel hook); the SAT factor graph (rule "satisfiability") → BipartiteView.createSat.
   Each frame carries the backend's states as `ev`, so the colors are the backend's, not the mockup's derivation. */
(() => {
  const NODE_CLASS = { ElementHighlight: "Active", Solution: "Solution", Covered: "Covered", Rejected: "Rejected", Blocked: "Blocked", False: "False" };
  const EDGE_CLASS = { ElementHighlight: "hot", Solution: "sol", False: "dim" };

  function nodeEv(f, map, note, hasFalse) {
    const out = new Map();
    for (const id of Object.keys(f.nodes || {})) {
      const st = Viewer.stateOf(f.nodes, id);
      if (st === "Background") continue;
      if (NODE_CLASS[st] && (st !== "False" || hasFalse)) out.set(map ? map(id) : id, NODE_CLASS[st]);
      else note(`Node state "${st}" has no style in bipartite.js (drawn as Background).`);
    }
    return out;
  }
  function edgeClass(f, id, note) {
    const st = Viewer.stateOf(f.edges, id);
    if (st === "Background") return "";
    if (EDGE_CLASS[st]) return EDGE_CLASS[st];
    note(`Edge state "${st}" has no style in bipartite.js (drawn as Background).`);
    return "";
  }

  /** Hide pieces a Reduction build frame has not added yet (first builtNodes / builtEdges of ctx.buildOrder). */
  function buildHider(ctx, nodeEls, edgeEls) {
    return f => {
      const bo = ctx.buildOrder, bn = f.builtNodes, be = f.builtEdges;
      const shownN = bo && bn != null ? new Set(bo.nodes.slice(0, bn)) : null, shownE = bo && be != null ? new Set(bo.edges.slice(0, be)) : null;
      for (const [id, el] of nodeEls) el.style.display = shownN && !shownN.has(id) ? "none" : "";
      for (const [id, el] of edgeEls) el.style.display = shownE && !shownE.has(id) ? "none" : "";
    };
  }

  function mountSets(ctx) {
    const { payload, note } = ctx;
    const svg = Viewer.svgIn(ctx.host, `${ctx.problem} bipartite graph`);
    const view = BipartiteView.create({ svg });
    const pickSets = payload.pickSide !== "elements";
    const exact = payload.rule === "exactlyOnce";
    // BipartiteView keys its rules by problem: SETCOVER = at least once, pick sets; EXACTCOVER = exactly once, pick sets;
    // HITTINGSET = exactly once, pick elements.
    const kind = pickSets ? (exact ? "EXACTCOVER" : "SETCOVER") : "HITTINGSET";
    if (!pickSets && !exact) note(`rule "${payload.rule}" with pickSide "elements" has no mockup counterpart; drawn as Hitting Set.`);
    // The mockup labels a set's items by element *name* and looks elements up as "e:" + name.
    const elName = new Map(payload.elements.map(e => [e.id, e.id.startsWith("e:") ? e.id.slice(2) : e.id]));
    if (payload.elements.some(e => !e.id.startsWith("e:"))) note(`Element ids without the "e:" prefix the mockup assumes (e.g. ${payload.elements.find(e => !e.id.startsWith("e:")).id}).`);
    const sets = payload.sets.map(s => ({ id: s.id, label: s.label, items: s.members.map(m => elName.get(m) ?? m) }));
    const els = payload.elements.map(e => ({ id: "e:" + elName.get(e.id), label: e.label }));
    const toMock = id => (elName.has(id) ? "e:" + elName.get(id) : id);
    const edges = payload.edges.map(e => {
      const aIsEl = elName.has(e.a);
      return { el: toMock(aIsEl ? e.a : e.b), set: aIsEl ? e.b : e.a, id: e.id };
    });
    const cands = pickSets ? sets.map(s => s.id) : els.map(e => e.id);
    const cons = pickSets ? els.map(e => e.id) : sets.map(s => s.id);
    const hits = new Map(cands.map(c => [c, new Set()]));
    edges.forEach(e => (pickSets ? hits.get(e.set).add(e.el) : hits.get(e.el).add(e.set)));
    const M = { sets, els, edges, cands, cons, hits, pickSets,
      name: id => (id.startsWith("e:") ? id.slice(2) : id), nice: id => id };
    const frames = ctx.frames.map(f => ({
      chosen: [], caption: f.caption, done: f.event === "Done",
      ev: {
        nodes: nodeEv(f, toMock, note, false),
        hits: new Map(Object.entries(f.hits || {}).map(([id, n]) => [toMock(id), +n])),
        edges: new Map(payload.edges.map(e => [e.id, edgeClass(f, e.id, note)])),
      },
    }));
    view.loadModel(M, { kind, K: payload.k ?? null, U: els.map(e => e.label), sets: sets.map(s => s.items) }, frames);
    // bipartite.js caps the set box at 260 px but not the member list in it: long lists (long backend ids) run past the box
    // and onto the background. Clip them with an ellipsis and keep the full text in a <title>.
    let clipped = 0;
    ctx.host.querySelectorAll("g.bp-node.set").forEach(g => {
      const body = g.querySelector("rect.body"), sub = g.querySelector("text.sub");
      if (!body || !sub || !sub.getComputedTextLength) return;
      const room = +body.getAttribute("x") + +body.getAttribute("width") - +sub.getAttribute("x") - 8, full = sub.textContent;
      if (sub.getComputedTextLength() <= room) return;
      let t = full;
      while (t.length > 1 && sub.getComputedTextLength() > room) { t = t.slice(0, -1); sub.textContent = t + "…"; }
      Viewer.mk("title", {}, g).textContent = full;
      clipped++;
    });
    if (clipped) note(`${clipped} set member lists are wider than the 260 px set box in bipartite.js; clipped with … (full list on hover).`);
    const known = new Set(["event", "caption", "focus", "nodes", "edges", "hits", "ok", "phase", "builtNodes", "builtEdges", "skipped"]);
    const extra = [...new Set(ctx.frames.flatMap(f => Object.keys(f)))].filter(k => !known.has(k));
    if (extra.length) note(`Frame fields the Bipartite adapter ignores: ${extra.join(", ")}`, false);

    // Tag: draw() appends lines in M.edges order, then element nodes, then set nodes.
    const lines = [...svg.querySelectorAll("line.bp-edge")], nodeGs = [...svg.querySelectorAll("g.bp-node")];
    const edgeEls = new Map(), nodeEls = new Map();
    lines.forEach((l, i) => { Viewer.tag(l, payload.edges[i].id); edgeEls.set(payload.edges[i].id, l); });
    nodeGs.forEach((g, i) => {
      const id = i < payload.elements.length ? payload.elements[i].id : payload.sets[i - payload.elements.length].id;
      Viewer.tag(g, id); nodeEls.set(id, g);
    });
    const hide = buildHider(ctx, nodeEls, edgeEls);
    return { show(i) { view.show(i); hide(ctx.frames[i]); } };
  }

  function mountSat(ctx) {
    const { payload, note } = ctx;
    const svg = Viewer.svgIn(ctx.host, `${ctx.problem} factor graph`);
    const view = BipartiteView.createSat({ svg });
    const vIdx = new Map(payload.elements.map((e, i) => [e.id, i + 1]));
    const names = payload.elements.map(e => e.label);
    // One literal per edge, in clause order: createSat draws one line per literal in that same order.
    const edgeOrder = [];
    const C = payload.sets.map(s => payload.edges.filter(e => e.b === s.id || e.a === s.id).map(e => {
      edgeOrder.push(e.id);
      const v = vIdx.get(vIdx.has(e.a) ? e.a : e.b);
      if (e.positive === undefined) note("Factor-graph edge without \"positive\"; drawn as a positive literal.");
      return e.positive === false ? -v : v;
    }));
    const dup = payload.sets.filter(s => new Set(s.members).size !== payload.edges.filter(e => e.b === s.id).length);
    if (dup.length) note(`Clause ${dup[0].label} uses one variable twice (two literals): its "members" list has the variable once but there are two edges, drawn on top of each other.`, false);
    view.load(C, names);
    const nodeKey = id => (vIdx.has(id) ? "v" + vIdx.get(id) : "c" + payload.sets.findIndex(s => s.id === id));
    const frames = ctx.frames.map(f => {
      const asg = {};
      for (const [k, v] of Object.entries(f.assignment || {})) if (vIdx.has(k)) asg[vIdx.get(k)] = v;
      return { asg, caption: f.caption, done: f.event === "Done",
        ev: { nodes: nodeEv(f, nodeKey, note, true), edges: edgeOrder.map(id => edgeClass(f, id, note)) } };
    });
    if (ctx.frames.some(f => Object.values(f.hits || {}).some(n => n > 1))) note("Factor graph: frames send hits counts (true literals per clause); the SAT factor view has no badge for them.", false);
    // Tag: createSat draws edges (literal order), then per variable a node + its T/F label, then clause nodes.
    const lines = [...svg.querySelectorAll("line.bp-edge")], vars = [...svg.querySelectorAll("g.bp-node.el")], cls = [...svg.querySelectorAll("g.bp-node.set")];
    const edgeEls = new Map(), nodeEls = new Map();
    lines.forEach((l, i) => { Viewer.tag(l, edgeOrder[i]); edgeEls.set(edgeOrder[i], l); });
    vars.forEach((g, i) => { Viewer.tag(g, payload.elements[i].id); nodeEls.set(payload.elements[i].id, g); });
    cls.forEach((g, i) => { Viewer.tag(g, payload.sets[i].id); nodeEls.set(payload.sets[i].id, g); });
    const hide = buildHider(ctx, nodeEls, edgeEls);
    return { show(i) { view.show(frames[i]); hide(ctx.frames[i]); } };
  }

  Viewer.register("BipartiteGraph", {
    mount(ctx) {
      return ctx.payload.rule === "satisfiability" || ctx.payload.pickSide === "assignment" ? mountSat(ctx) : mountSets(ctx);
    },
  });
})();
