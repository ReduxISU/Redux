/* ---- Packing (#669): payload {kind:"packing", rule: partition|subsetSum|knapsack|binPacking,
   items[{id,label,size,value?}], containers[{id,label,target?|limit?,atLeast?}], total, maxBins?}
   frames {event, caption, focus, items{id:{state}}, containers{id:{state}}, assign[containerIndex|-1], totals[{size,value?}], ok?}
   Drawn with PackingView (../mockups/packing.js) through its loadModel hook: the instance is built from the payload,
   `assign` is the mockup's own frame format, each item's backend state is passed through as `itemStates`, and each
   container's state as `rowStates` (per drawn row: Knapsack's one container is both its Weight and Value rows). */
(() => {
  const RULES = { partition: "partition", subsetSum: "sum", subsetProduct: "product", knapsack: "knapsack", binPacking: "binpacking" };
  const ITEM_CLASS = { ElementHighlight: "Active", Solution: "Solution", Rejected: "Rejected", Untraveled: "Unused" };
  const ROW_CLASS = { ElementHighlight: "Active", Solution: "Solution", Rejected: "Rejected", Covered: "Covered" };

  function model(payload, rule, note) {
    const items = payload.items.map((t, i) => ({ i, w: t.size, v: t.value ?? null, label: String(t.size) }));
    const I = { rule, items, n: items.length };
    const c = payload.containers || [];
    if (rule === "sum" || rule === "product") I.T = c[0]?.target;
    if (rule === "knapsack") { I.pairs = payload.items.map(t => [t.size, t.value]); I.W = c[0]?.limit; I.V = c[0]?.atLeast; }
    if (rule === "binpacking") { I.C = c[0]?.limit; I.K = payload.maxBins ?? c.length; if (I.K !== c.length) note(`maxBins ${payload.maxBins} but ${c.length} containers.`); }
    if (rule === "partition" && c.some(x => x.target * 2 !== payload.total)) note("Partition container target is not half the total.");
    if (items.length > 16) note(`${items.length} items: the mockup parser caps Packing at 16 items (the viewer builds the instance from the payload, so it still draws).`, false);
    if (rule === "binpacking" && I.K > 6) note(`${I.K} bins: packing.js has six group colors, so bins repeat colors past six.`, false);
    return I;
  }

  Viewer.register("Packing", {
    mount(ctx) {
      const { payload, note } = ctx;
      const rule = RULES[payload.rule];
      if (!rule) throw new Error(`Unknown Packing rule "${payload.rule}".`);
      const svg = Viewer.svgIn(ctx.host, `${ctx.problem} packing`);
      const view = PackingView.create({ svg });
      const I = model(payload, rule, note);
      const ids = payload.items.map(t => t.id);
      const known = new Set(["event", "caption", "focus", "items", "containers", "assign", "totals", "ok", "phase", "builtNodes", "builtEdges"]);
      const extra = [...new Set(ctx.frames.flatMap(f => Object.keys(f)))].filter(k => !known.has(k));
      if (extra.length) note(`Frame fields the Packing adapter ignores: ${extra.join(", ")}`, false);
      const frames = ctx.frames.map((f, k) => {
        const assign = Array.isArray(f.assign) ? f.assign.slice() : ids.map(() => -1);
        if (!Array.isArray(f.assign)) note("A frame has no assign array; drawn with nothing placed.");
        if (assign.length !== ids.length) note(`Frame ${k + 1}: assign has ${assign.length} entries for ${ids.length} items.`);
        const itemStates = ids.map(id => {
          const st = Viewer.stateOf(f.items, id);
          if (st === "Background") return "";
          if (ITEM_CLASS[st]) return ITEM_CLASS[st];
          note(`Item state "${st}" has no style in packing.js (drawn as Background).`);
          return "";
        });
        // Cross-check the totals the backend sends against what the mockup will add up.
        (f.totals || []).forEach((t, r) => {
          const src = rule === "knapsack" ? 0 : r;
          const sum = I.items.filter((_, j) => assign[j] === src).reduce((a, it) => a + it.w, 0);
          if (t.size != null && t.size !== sum && rule !== "product") note(`Frame ${k + 1}: totals[${r}].size is ${t.size}, the items assigned there add up to ${sum}.`);
        });
        // Rows drawn by packing.js: one per container, except Knapsack (Weight and Value rows, one container).
        const conts = payload.containers || [];
        const rowConts = rule === "knapsack" ? [conts[0], conts[0]] : conts;
        const rowStates = rowConts.map(c => {
          const st = c ? Viewer.stateOf(f.containers, c.id) : "Background";
          if (st === "Background") return "";
          if (ROW_CLASS[st]) return ROW_CLASS[st];
          note(`Container state "${st}" has no style in packing.js (drawn as Background).`);
          return "";
        });
        return { assign, itemStates, rowStates, caption: f.caption, done: f.event === "Done", ok: f.ok };
      });
      view.loadModel(I, { rule }, frames);

      // Tag item tiles; wrap each container row into one <g data-vid=container id> (rows stay where they are).
      const tiles = [...svg.querySelectorAll("g.pk-tile")];
      tiles.forEach((g, i) => Viewer.tag(g, ids[i]));
      const rowLabels = [...svg.querySelectorAll("text.pk-row-label")];
      const conts = payload.containers || [];
      const rowEls = rowLabels.map((lab, r) => {
        const g = Viewer.mk("g", { class: "vv-pk-row" });
        lab.parentNode.insertBefore(g, lab);
        let el = lab;
        while (el && !(el !== lab && el.classList?.contains("pk-row-label")) && !el.classList?.contains("pk-fulcrum") && !el.classList?.contains("pk-note") && !(el.tagName === "g" && el.getAttribute("style"))) {
          const next = el.nextSibling; g.appendChild(el); el = next;
        }
        const cid = rule === "knapsack" ? conts[0]?.id : conts[r]?.id;
        Viewer.tag(g, cid);
        return g;
      });
      const srcOf = r => (rule === "knapsack" ? 0 : r);
      const tagSegs = i => {
        const f = frames[i];
        rowEls.forEach((g, r) => {
          const mine = I.items.filter((_, j) => f.assign[j] === srcOf(r)).map(t => ids[t.i]);
          [...g.querySelectorAll("g.pk-seg")].forEach((s, k) => Viewer.tag(s, mine[k]));
        });
      };
      // Reduction build phase: only the first builtNodes pieces of buildOrder (items, then containers) exist yet.
      const bo = ctx.buildOrder || { nodes: [...ids, ...conts.map(c => c.id)] };
      return {
        show(i) {
          view.show(i);
          tagSegs(i);
          const f = ctx.frames[i];
          const built = f.builtNodes != null ? new Set(bo.nodes.slice(0, f.builtNodes)) : null;
          tiles.forEach((g, j) => { g.style.display = built && !built.has(ids[j]) ? "none" : ""; });
          rowEls.forEach(g => { g.style.display = built && !built.has(g.dataset.vid) ? "none" : ""; });
        },
      };
    },
  });
})();
