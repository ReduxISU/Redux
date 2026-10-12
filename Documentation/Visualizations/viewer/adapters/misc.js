/* ---- Geometry (#674), Tripartite Matching (#675), Code Tree (#676): all drawn with MiscView (../mockups/misc.js) ----
   Through two viewer hooks in misc.js: loadModel(key, model, frames) draws a model built here from the payload (no
   mockup parsing or solving), and afterPaint(els, frame, hover) runs after each paint. Each backend frame is turned
   into the mockup's own frame shape (so polygons, chains, badges and the encoded strip still draw), then afterPaint
   restyles every piece with the backend's own state, so the colors shown are the backend's. */
(() => {
  // Backend state → misc.js state class (points, elements, tree nodes share these names).
  const CLS = { ElementHighlight: "Active", Solution: "Solution", Covered: "Covered", Rejected: "Rejected" };
  const keepTokens = (el, keep) => (el.getAttribute("class") || "").split(/\s+/).filter(t => keep.includes(t));

  /* ------------------------------------------------ Geometry ------------------------------------------------ */
  Viewer.register("Geometry", {
    mount(ctx) {
      const { payload, note } = ctx;
      const pts = payload.points || [];
      const idx = new Map(pts.map((p, i) => [p.id, i]));
      pts.forEach((p, i) => { if (p.id !== "P" + (i + 1)) note(`Point ${i + 1} has id "${p.id}"; misc.js labels points P1, P2, … by position.`); });
      const toIdx = list => (list || []).map(id => idx.get(id)).filter(i => i !== undefined);
      const roles = new Set(ctx.frames.flatMap(f => (f.pieces || []).map(p => p.role)));
      if (roles.size) note(`Geometry pieces carry roles (${[...roles].join(", ")}); the merge is a chain walk over the left and right hulls, while misc.js's divide-and-conquer frames show upper/lower bridge candidates (cand, bridge). The viewer draws pieces as hulls (left/right in the two group colors) plus the chain.`, false);
      const frames = ctx.frames.map(f => {
        const done = f.event === "Done";
        if (done && f.hull && f.hull.length) return { done: true, hull: toIdx(f.hull), ok: f.ok, src: f };
        if (done && !(f.hull || []).length && f.ok === false) note("A Done frame with an empty hull (stopped run): drawn without a polygon.", false);
        const hulls = (f.pieces || []).map(p => toIdx(p.points));
        const lr = (f.pieces || []).map((p, k) => [p, k]).filter(([p]) => p.role === "left" || p.role === "right").map(([, k]) => hulls[k]);
        const focusSet = new Set(f.focus || []);
        const one = (f.pieces || []).map((p, k) => [p, k]).filter(([p]) => p.points.length && p.points.every(id => focusSet.has(id))).map(([, k]) => hulls[k]);
        return { hulls, focus: lr.length ? lr : one.slice(0, 1), src: f };
      });
      const svg = Viewer.svgIn(ctx.host, `${ctx.problem} points`);
      let E = null;
      const view = MiscView.create({
        svg,
        afterPaint(els, fr) {
          if (!fr) return;
          const f = fr.src;
          if (!fr.done && (f.chain || []).length) {
            const poly = toIdx(f.chain).map(i => `${els.X(els.P[i].x)},${els.Y(els.P[i].y)}`).join(" ");
            Viewer.mk("polyline", { points: poly, class: "ms-chain" }, els.gL);
          }
          els.pts.forEach((o, i) => {
            const st = Viewer.stateOf(f.points, pts[i].id);
            if (st !== "Background" && !CLS[st]) note(`Point state "${st}" has no style in misc.js.`);
            o.g.setAttribute("class", ["ms-pt", CLS[st] || "", ...keepTokens(o.g, ["ring", "trace"])].filter(Boolean).join(" "));
          });
        },
      });
      E = view.loadModel("CONVEXHULL", { pts: pts.map(p => ({ x: p.x, y: p.y })) }, frames).els;
      E.pts.forEach((o, i) => Viewer.tag(o.g, pts[i].id));
      return { show(i) { view.show(i); } };
    },
  });

  /* ------------------------------------------- Tripartite Matching ------------------------------------------- */
  Viewer.register("TripartiteMatching", {
    mount(ctx) {
      const { payload, note } = ctx;
      const cols = [["x", payload.x || []], ["y", payload.y || []], ["z", payload.z || []]];
      const nameOf = new Map(), keyOf = new Map();
      cols.forEach(([c, list]) => list.forEach(e => { nameOf.set(e.id, e.name ?? e.id); keyOf.set(e.id, c + ":" + (e.name ?? e.id)); }));
      const names = cols.map(([, list]) => list.map(e => e.name ?? e.id));
      const triples = payload.triples || [];
      const D = { X: names[0], Y: names[1], Z: names[2], M: triples.map(t => ({ id: t.id, x: nameOf.get(t.x), y: nameOf.get(t.y), z: nameOf.get(t.z) })) };
      if (triples.length > 30 || names.some(n => n.length > 10)) note(`${triples.length} triples and ${names.map(n => n.length).join("/")} elements: misc.js's parser caps the picture at 10 names per set and 30 triples (the viewer builds the model from the payload, so it still draws, crowded).`, false);
      const tIdx = new Map(triples.map((t, i) => [t.id, i]));
      const frames = ctx.frames.map(f => {
        const chosen = (f.chosen || []).map(id => tIdx.get(id)).filter(i => i !== undefined);
        const done = f.event === "Done";
        const hl = Object.keys(f.triples || {}).filter(id => Viewer.stateOf(f.triples, id) === "ElementHighlight");
        return { chosen, done, ok: f.ok, trying: hl.length ? tIdx.get(hl[hl.length - 1]) : undefined, src: f };
      });
      const TRI = { Solution: "sol", Rejected: "rej", ElementHighlight: "hot", Blocked: "blk", Covered: "sol" };
      const order = ctx.buildOrder && ctx.buildOrder.nodes;
      const svg = Viewer.svgIn(ctx.host, `${ctx.problem} tripartite matching`);
      let elKeys = null;
      const view = MiscView.create({
        svg,
        afterPaint(els, fr) {
          if (!fr) return;
          const f = fr.src;
          for (const [k, o] of els.els) {
            const id = elKeys.get(k), st = Viewer.stateOf(f.elements, id);
            if (st !== "Background" && !CLS[st]) note(`Element state "${st}" has no style in misc.js.`);
            o.g.setAttribute("class", ["ms-el", CLS[st] || "Background", ...keepTokens(o.g, ["trace", "faint"])].join(" "));
            const n = f.repeats ? f.repeats[id] || 0 : 0;
            o.badge.setAttribute("visibility", n > 1 ? "visible" : "hidden");
            if (n > 1) o.bt.textContent = "×" + n;
          }
          els.tris.forEach((o, i) => {
            const st = Viewer.stateOf(f.triples, triples[i].id);
            if (st !== "Background" && !TRI[st]) note(`Triple state "${st}" has no style in misc.js.`);
            const extra = keepTokens(o.g, ["trace", "faint"]);
            const c = TRI[st] || "";
            const pTok = keepTokens(o.path, ["trace", "faint"]);
            o.path.setAttribute("class", ["ms-tri", ...(pTok.length ? pTok : [c])].filter(Boolean).join(" "));
            o.g.setAttribute("class", ["ms-trow", c, ...extra].filter(Boolean).join(" "));
          });
          // Reduction View build phase: only the first builtNodes ids of buildOrder exist yet.
          const shown = f.builtNodes != null && order ? new Set(order.slice(0, f.builtNodes)) : null;
          for (const [k, o] of els.els) o.g.style.display = shown && !shown.has(elKeys.get(k)) ? "none" : "";
          els.tris.forEach((o, i) => { const h = shown && !shown.has(triples[i].id); o.path.style.display = h ? "none" : ""; o.g.style.display = h ? "none" : ""; });
        },
      });
      elKeys = new Map([...keyOf].map(([id, k]) => [k, id]));
      if (elKeys.size !== keyOf.size) note("Two elements share a name across X, Y and Z; misc.js keys elements by name, so they collide.");
      const els = view.loadModel("DM3", D, frames).els;
      for (const [k, o] of els.els) Viewer.tag(o.g, elKeys.get(k));
      els.tris.forEach((o, i) => { Viewer.tag(o.path, triples[i].id); Viewer.tag(o.g, triples[i].id); });
      return { show(i) { view.show(i); } };
    },
  });

  /* ------------------------------------------------ Code Tree ------------------------------------------------ */
  Viewer.register("CodeTree", {
    mount(ctx) {
      const { payload, note } = ctx;
      const syms = payload.symbols || [];
      const text = payload.text ?? ctx.instance ?? "";
      const last = ctx.frames[ctx.frames.length - 1] || {};
      // The full merge list is in the last frame that has the most merges (a stopped run may not finish the tree).
      const merged = ctx.frames.reduce((a, f) => ((f.merged || []).length > a.length ? f.merged : a), []);
      const N = [], index = new Map();
      syms.forEach(s => { const ch = s.code != null ? String.fromCharCode(s.code) : s.label; index.set(s.id, N.length); N.push({ id: N.length, bid: s.id, ch, w: s.count, level: 0 }); });
      merged.forEach((m, k) => {
        const L = N[index.get(m.left)], R = N[index.get(m.right)];
        index.set(m.id, N.length);
        N.push({ id: N.length, bid: m.id, ch: null, w: m.weight, left: L.id, right: R.id, level: 1 + Math.max(L.level, R.level), order: k + 1 });
      });
      // Roots left after the last merge: one for a finished tree; more (a stopped run) get a hidden virtual root.
      const hasParent = new Set(N.filter(n => n.ch === null).flatMap(n => [n.left, n.right]));
      let roots = N.filter(n => !hasParent.has(n.id));
      while (roots.length > 1) {
        note("The tree never finished (stopped run): the remaining trees hang under a hidden virtual root.", false);
        const [a, b] = roots.splice(0, 2);
        const v = { id: N.length, bid: null, ch: null, w: a.w + b.w, left: a.id, right: b.id, level: 1 + Math.max(a.level, b.level), order: 1e9 };
        N.push(v); roots.push(v);
      }
      const root = roots[0];
      const codes = new Map(), freq = new Map();
      (function walk(id, code) { const n = N[id]; if (n.ch !== null) { n.code = code || "0"; codes.set(n.ch, n.code); return; } walk(n.left, code + "0"); walk(n.right, code + "1"); })(root.id, "");
      const backendCodes = last.codes || {};
      for (const n of N) if (n.ch !== null && backendCodes[n.bid] && backendCodes[n.bid] !== n.code) note(`Code for ${n.bid} differs: backend ${backendCodes[n.bid]}, tree walk ${n.code}.`);
      for (const n of N) if (n.ch !== null) freq.set(n.ch, n.w);
      const missing = [...new Set(text.split(""))].filter(c => !codes.has(c));
      if (missing.length) note(`Characters in payload.text with no symbol: ${missing.join(" ")}`);
      const D = { text, nodes: N, root: root.id, codes, freq, bits: text.split("").reduce((s, c) => s + (codes.get(c) || "").length, 0), encoded: text.split("").map(c => codes.get(c) || "").join("") };
      if (syms.some(s => s.label && s.code != null && s.label !== String.fromCharCode(s.code) && s.code !== 32))
        note("Symbol labels differ from their characters; misc.js shows the character itself (with ␣ ↵ ⇥ for whitespace).", false);
      const frames = ctx.frames.map(f => {
        const built = (f.merged || []).length;
        const codesOn = Object.keys(f.codes || {}).length > 0;
        const fr = { built, codes: codesOn, done: f.event === "Done", ok: f.ok, src: f };
        const lastM = (f.merged || [])[built - 1];
        if (lastM && (f.focus || []).includes(lastM.id)) { fr.merging = [index.get(lastM.left), index.get(lastM.right)]; fr.parent = index.get(lastM.id); }
        return fr;
      });
      const EDGE = { ElementHighlight: "hot", Solution: "sol" };
      const svg = Viewer.svgIn(ctx.host, `${ctx.problem} code tree`);
      const view = MiscView.create({
        svg,
        afterPaint(els, fr) {
          if (!fr) return;
          const f = fr.src;
          els.nodeEls.forEach((g, id) => {
            const n = N[id];
            if (n.bid === null) return;
            const st = Viewer.stateOf(f.nodes, n.bid);
            if (st !== "Background" && !CLS[st]) note(`Tree node state "${st}" has no style in misc.js.`);
            g.setAttribute("class", ["ms-tn", CLS[st] || "Background", ...keepTokens(g, ["hidden", "trace", "faint"])].join(" "));
          });
          els.edges.forEach(e => {
            const p = N[e.parent];
            const eid = p.bid === null ? null : `${p.bid}.${e.child === p.left ? 0 : 1}`;
            const hidden = keepTokens(e.line, ["hidden"]), trace = keepTokens(e.line, ["trace"]);
            const st = eid ? Viewer.stateOf(f.edges, eid) : "Background";
            if (st !== "Background" && !EDGE[st]) note(`Tree edge state "${st}" has no style in misc.js.`);
            e.line.setAttribute("class", ["ms-te", ...hidden, ...(trace.length ? trace : [EDGE[st] || ""])].filter(Boolean).join(" "));
          });
        },
      });
      const els = view.loadModel("LDC", D, frames).els;
      els.nodeEls.forEach((g, id) => { if (N[id].bid) Viewer.tag(g, N[id].bid); });
      els.edges.forEach(e => { const p = N[e.parent]; if (p.bid) Viewer.tag(e.line, `${p.bid}.${e.child === p.left ? 0 : 1}`); });
      if (ctx.frames.some(f => f.bits !== undefined)) note("Frames carry \"bits\"; misc.js computes the encoded strip itself from the codes.", false);
      return { show(i) { view.show(i); } };
    },
  });
})();
