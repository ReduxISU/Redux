/* ---- Search Tree (#771): payload {kind:"searchTree", problem, instance, maxLeaves}
   frames {event, caption, focus, nodes:[{id, parent, label, kind, state, bound?, order?, hidden?}], total, cut, ok?}
   Each frame carries the whole tree so far (not patches). Also used for "search tree alongside" and for steps that carry
   SolverStep.tree patches (app.js accumulates those into the same shape).
   Drawn with SearchTreeView (../mockups/searchtree.js) through its loadModel hook: a frame's `tree` replaces the mockup's
   patch replay. The node's mockup class (st) is derived from the backend's state first, its kind second. */
(() => {
  const KINDS = new Set(["explored", "pruned", "failed", "queued", "skipped", "solution", "cut", "more"]);

  function st(n, note) {
    if (n.kind && !KINDS.has(n.kind)) note(`Search-tree node kind "${n.kind}" is unknown to searchtree.js.`);
    if (n.kind === "more") return "fold"; // backend-folded finished subtree ("+k more"): searchtree.js's own fold style
    switch (n.state || "Background") {
      case "Solution": return "solution";
      case "Rejected": return n.kind === "failed" || n.kind === "explored" || n.kind === "solution" ? "failed" : "pruned";
      case "Untraveled": return n.kind === "queued" ? "queued" : "skipped";
      case "Covered": return "explored";
      case "Blocked": return "skipped";
      case "ElementHighlight": return n.kind === "queued" ? "queued" : "explored";
      case "Background": return n.kind === "cut" ? "pruned" : n.kind === "solution" ? "explored" : n.kind || "explored";
      default: note(`Search-tree node state "${n.state}" has no style in searchtree.js.`); return n.kind || "explored";
    }
  }

  Viewer.register("SearchTree", {
    mount(ctx) {
      const { note } = ctx;
      const svg = Viewer.svgIn(ctx.host, `${ctx.problem} search tree`);
      const view = SearchTreeView.create({ svg });
      // The backend folds the tree itself (kind "cut" / "more" nodes, payload.maxLeaves): searchtree.js then draws it as given.
      // A tree built here from steps' tree patches (app.js, payload.clientFolds) is folded by searchtree.js instead.
      const serverFolds = ctx.payload && ctx.payload.maxLeaves != null && !ctx.payload.clientFolds;
      if (ctx.frames.some(f => (f.nodes || []).some(n => n.bound !== undefined)))
        note("Node field names differ: backend {kind, state, bound} vs mockup patches {st, note}; mapped by the adapter (state wins over kind).", false);
      const frames = ctx.frames.map(f => {
        const list = (f.nodes || []).map(n => ({ id: n.id, parent: n.parent ?? null, label: n.label ?? n.id, st: st(n, note), note: n.bound ?? n.note, order: n.order, val: n.val }));
        const done = f.event === "Done";
        let cur = null;
        if (!done) {
          const hl = (f.nodes || []).filter(n => n.state === "ElementHighlight");
          if (hl.length) cur = hl[hl.length - 1].id;
          if (hl.length > 1) note(`A frame highlights ${hl.length} search-tree nodes; searchtree.js marks one current node (the last).`, false);
        } else if (f.ok) {
          const sol = (f.nodes || []).filter(n => n.kind === "solution" && n.state === "Solution");
          const end = sol.length ? sol[sol.length - 1] : [...(f.nodes || [])].reverse().find(n => n.state === "Solution");
          cur = end ? end.id : null;
        }
        return { tree: list, cur, done, ok: f.ok, caption: f.caption };
      });
      view.loadModel({ kind: "viewer", game: false, serverFolds }, frames);
      return { show(i) { view.show(i); } };
    },
  });
})();
