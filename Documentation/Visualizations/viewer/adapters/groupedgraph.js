/* ---- Grouped graph / clause graph (#664): payload {kind:"groupedGraph", layout:"ring", shape:"assignment", rule:"satisfiability",
   groups[{id,label,members[ids]}], nodes[{id,label,variable,positive,group}], variables[{id,label}], edges[{id,a,b}] (complement edges
   between clauses), clauseEdges[{id,a,b}] (inside a clause)}
   frames {event, caption, focus, nodes{id:{state}}, groups{id:{state}}, assignment{var:bool}, ok?}
   The design mockup has no reusable module for this picture: its build/draw/paint is inline page code in
   ../mockups/design.src.html. This file ports that layout (groups on a ring, literals on a small circle in each group,
   complement edges curved through the centre) and its CSS (scoped as .gg-*), and draws the backend's states directly.
   An assignment strip under the ring shows each variable (tagged with its variable id for gadget linking). */
(() => {
  const css = `
.gg-svg { width: 100%; height: auto; display: block; }
.gg-svg text { font-family: var(--av-mono); }
.gg-hull { fill: var(--av-surface); stroke: var(--av-stroke); stroke-width: 1.5; transition: stroke .2s, fill .2s; }
.gg-hull.Covered, .gg-hull.Solution { stroke: var(--av-sol); fill: var(--av-sol-fill); }
.gg-hull.Rejected { stroke: var(--av-rej); fill: var(--av-rej-fill); stroke-width: 2.5; }
.gg-hull.ElementHighlight { stroke: var(--av-hl); fill: var(--av-hl-fill); stroke-width: 2.5; }
.gg-hull.hot { stroke: var(--av-hot); stroke-width: 2.5; }
.gg-glabel { font-size: 13px; font-weight: 500; fill: var(--av-muted); }
.gg-glabel.Covered, .gg-glabel.Solution { fill: var(--av-sol-ink); } .gg-glabel.Rejected { fill: var(--av-rej-ink); } .gg-glabel.ElementHighlight { fill: var(--av-hl-ink); }
.gg-edge { fill: none; stroke: var(--av-edge); stroke-width: 1.2; transition: stroke .2s; }
.gg-edge.intra { stroke-dasharray: 3 3; }
.gg-edge.dim { stroke: var(--av-edge-dim); }
.gg-edge.faint { stroke: var(--av-edge-dim); stroke-dasharray: 3 3; }
.gg-edge.hot { stroke: var(--av-hot); stroke-width: 2; }
.gg-node circle { stroke-width: 1.6; transition: fill .2s, stroke .2s; }
.gg-node text { font-size: 12.5px; text-anchor: middle; dominant-baseline: central; pointer-events: none; }
.gg-node { cursor: pointer; }
.gg-node:focus { outline: none; }
.gg-node:focus-visible .ring { stroke: var(--av-hot); stroke-width: 2; opacity: 1; }
.gg-node.Background circle.body { fill: var(--av-bg); stroke: var(--av-stroke); } .gg-node.Background text { fill: var(--av-ink); }
.gg-node.False circle.body { fill: var(--s-false); stroke: var(--av-stroke); } .gg-node.False text { fill: var(--s-false-ink); }
.gg-node.Solution circle.body { fill: var(--av-sol); stroke: var(--av-sol); } .gg-node.Solution text { fill: var(--av-on-sol); font-weight: 500; }
.gg-node.Covered circle.body { fill: var(--av-sol-fill); stroke: var(--av-sol); } .gg-node.Covered text { fill: var(--av-ink); }
.gg-node.Rejected circle.body { fill: var(--av-rej-fill); stroke: var(--av-rej); } .gg-node.Rejected text { fill: var(--av-rej-ink); }
.gg-node.ElementHighlight circle.body { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 2.4; } .gg-node.ElementHighlight text { fill: var(--av-ink); }
.gg-node.Blocked circle.body, .gg-node.Untraveled circle.body { fill: transparent; stroke: var(--av-stroke); stroke-dasharray: 4 3; } .gg-node.Blocked text, .gg-node.Untraveled text { fill: var(--av-muted); }
.gg-node .ring { fill: none; stroke: transparent; stroke-width: 3; opacity: 0; }
.gg-node.hl .ring { stroke: var(--av-hl); opacity: 1; }
.gg-node.faint { filter: grayscale(1); } .gg-node.faint circle.body { stroke-dasharray: 3 3; }
.gg-var rect { fill: var(--av-surface); stroke: var(--av-stroke); stroke-width: 1.4; }
.gg-var text { font-size: 12.5px; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); }
.gg-var.T rect { fill: var(--av-sol); stroke: var(--av-sol); } .gg-var.T text { fill: var(--av-on-sol); font-weight: 600; }
.gg-var.F rect { fill: var(--s-false); } .gg-var.F text { fill: var(--s-false-ink); }
.gg-var.tried rect { stroke: var(--av-hl); stroke-width: 3; }
.gg-var.faint { filter: grayscale(1); }
@media (prefers-reduced-motion: reduce) { .gg-node circle, .gg-hull, .gg-edge { transition: none; } }`;
  const NS = Viewer.NS, mk = Viewer.mk;
  const KNOWN = ["Background", "ElementHighlight", "Solution", "Rejected", "Covered", "False", "Blocked", "Untraveled"];

  Viewer.register("GroupedGraph", {
    mount(ctx) {
      const { payload, note } = ctx;
      if (!document.getElementById("gg-style")) { const st = document.createElement("style"); st.id = "gg-style"; st.textContent = css; document.head.appendChild(st); }
      const svg = Viewer.svgIn(ctx.host, `${ctx.problem} clause graph`);
      svg.classList.add("gg-svg");
      const groups = payload.groups.map((g, ci) => ({ ...g, ci }));
      const byNode = new Map(payload.nodes.map(n => [n.id, n]));
      // Layout from design.src.html build(): groups on a ring, literals on a small circle inside each group.
      const n = groups.length, nodeR = 17;
      const R = n === 1 ? 0 : Math.min(228 * Math.max(1, n / 12), Math.max(150, n * 30));
      const maxK = Math.max(1, ...groups.map(g => g.members.length));
      const hullMax = (maxK <= 1 ? 0 : (nodeR + 7) / Math.sin(Math.PI / maxK)) + nodeR + 10;
      // The design uses a fixed 640-wide canvas centred at (320, 320); grow it only when the ring needs more room.
      const half = Math.max(320, R + hullMax + 30), cx = half, cy = half;
      const vars = payload.variables || [];
      const stripCols = Math.max(1, Math.floor((2 * cx - 20) / 74)), stripRows = Math.ceil(vars.length / stripCols);
      const W = 2 * cx, H = 2 * cy + (vars.length ? 30 + stripRows * 38 : 0);
      svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
      const pos = new Map();
      groups.forEach(g => {
        const th = -Math.PI / 2 + (2 * Math.PI * g.ci) / n;
        g.x = cx + R * Math.cos(th); g.y = cy + R * Math.sin(th); g.th = th;
        const k = g.members.length; g.rin = k <= 1 ? 0 : (nodeR + 7) / Math.sin(Math.PI / k); g.hull = g.rin + nodeR + 10;
        g.members.forEach((id, li) => {
          const a = th + Math.PI + (2 * Math.PI * li) / k;
          pos.set(id, { x: g.x + g.rin * Math.cos(a), y: g.y + g.rin * Math.sin(a), group: g });
        });
      });
      const missing = payload.nodes.filter(nd => !pos.has(nd.id));
      if (missing.length) note(`Nodes not listed in any group's members: ${missing.map(m => m.id).join(", ")}`);
      const gHull = mk("g", {}, svg), gEdges = mk("g", {}, svg), gNodes = mk("g", {}, svg), gVars = mk("g", {}, svg);
      const hullEls = new Map(), labEls = new Map(), nodeEls = new Map(), edgeEls = new Map(), varEls = new Map();
      let hoverVar = null;
      groups.forEach(g => {
        const el = mk("circle", { cx: g.x, cy: g.y, r: g.hull, class: "gg-hull" }, gHull);
        Viewer.tag(el, g.id);
        const lr = g.hull + 16;
        const lab = mk("text", { x: g.x + lr * Math.cos(g.th), y: g.y + lr * Math.sin(g.th), class: "gg-glabel", "text-anchor": "middle", "dominant-baseline": "central" }, gHull);
        lab.textContent = g.label;
        hullEls.set(g.id, el); labEls.set(g.id, lab);
      });
      const edge = (e, kind) => {
        const a = pos.get(e.a), b = pos.get(e.b);
        if (!a || !b) { note(`Edge ${e.id} joins an unplaced node.`); return; }
        let d;
        if (kind === "conflict") {
          const mx = (a.x + b.x) / 2, my = (a.y + b.y) / 2, qx = mx + (cx - mx) * 0.5, qy = my + (cy - my) * 0.5;
          d = `M${a.x},${a.y} Q${qx},${qy} ${b.x},${b.y}`;
        } else d = `M${a.x},${a.y} L${b.x},${b.y}`;
        const el = mk("path", { d, class: "gg-edge " + kind }, gEdges);
        Viewer.tag(el, e.id);
        edgeEls.set(e.id, { el, kind, e });
      };
      (payload.edges || []).forEach(e => edge(e, "conflict"));
      // The design mockup hides the within-clause edges by default (a toggle); keep that default.
      if ((payload.clauseEdges || []).length) note(`clauseEdges (${payload.clauseEdges.length}, within a clause) are sent; the design draws them only behind a toggle, so the viewer leaves them out.`, false);
      payload.nodes.forEach(nd => {
        const p = pos.get(nd.id); if (!p) return;
        const g = mk("g", { class: "gg-node Background", tabindex: "0", role: "button", "aria-label": `${nd.label} in ${p.group.label}` }, gNodes);
        mk("circle", { cx: p.x, cy: p.y, r: nodeR + 4, class: "ring" }, g);
        mk("circle", { cx: p.x, cy: p.y, r: nodeR, class: "body" }, g);
        mk("text", { x: p.x, y: p.y }, g).textContent = nd.label;
        Viewer.tag(g, nd.id);
        const on = () => { hoverVar = nd.variable; paint(); }, off = () => { hoverVar = null; paint(); };
        g.addEventListener("pointerenter", on); g.addEventListener("pointerleave", off);
        g.addEventListener("focus", on); g.addEventListener("blur", off);
        nodeEls.set(nd.id, g);
      });
      vars.forEach((v, i) => {
        const col = i % stripCols, row = Math.floor(i / stripCols);
        const x = 10 + col * 74, y = 2 * cy + 20 + row * 38;
        const g = mk("g", { class: "gg-var" }, gVars);
        mk("rect", { x, y, width: 66, height: 28, rx: 7 }, g);
        const t = mk("text", { x: x + 33, y: y + 14 }, g);
        Viewer.tag(g, v.id);
        g.addEventListener("pointerenter", () => { hoverVar = v.id; paint(); });
        g.addEventListener("pointerleave", () => { hoverVar = null; paint(); });
        varEls.set(v.id, { g, t, label: v.label });
      });

      let f = null;
      function paint() {
        const fr = f || {};
        for (const g of groups) {
          const st = Viewer.stateOf(fr.groups, g.id);
          if (!KNOWN.includes(st)) note(`Group state "${st}" not drawn.`);
          hullEls.get(g.id).setAttribute("class", "gg-hull " + st);
          labEls.get(g.id).setAttribute("class", "gg-glabel " + st);
        }
        const focus = new Set(fr.focus || []);
        for (const [id, g] of nodeEls) {
          const st = Viewer.stateOf(fr.nodes, id), nd = byNode.get(id);
          let cls = "gg-node " + st;
          if (focus.has(nd.variable)) cls += " hl";
          if (hoverVar !== null) cls += nd.variable === hoverVar ? " hl" : " faint";
          g.setAttribute("class", cls);
        }
        const assigned = Object.keys(fr.assignment || {}).length > 0;
        for (const { el, kind, e } of edgeEls.values()) {
          let cls = "gg-edge " + kind;
          const va = byNode.get(e.a)?.variable;
          if (hoverVar !== null) cls += va === hoverVar ? " hot" : " faint";
          else if (assigned) cls += " dim";
          el.setAttribute("class", cls);
        }
        for (const [id, o] of varEls) {
          const a = (fr.assignment || {})[id];
          o.t.textContent = `${o.label} ${a === undefined ? "–" : a ? "T" : "F"}`;
          o.g.setAttribute("class", "gg-var" + (a === undefined ? "" : a ? " T" : " F") + (focus.has(id) ? " tried" : "") + (hoverVar !== null && hoverVar !== id ? " faint" : ""));
        }
        // Reduction target build frames: only the first builtNodes / builtEdges of buildOrder exist yet.
        const bo = ctx.buildOrder;
        const shownN = bo && fr.builtNodes != null ? new Set(bo.nodes.slice(0, fr.builtNodes)) : null;
        const shownE = bo && fr.builtEdges != null ? new Set(bo.edges.slice(0, fr.builtEdges)) : null;
        for (const [id, g] of nodeEls) g.style.display = shownN && !shownN.has(id) ? "none" : "";
        for (const [id, o] of edgeEls) o.el.style.display = shownE && !shownE.has(id) ? "none" : "";
        for (const [id, o] of varEls) o.g.style.display = shownN && bo.nodes.includes(id) && !shownN.has(id) ? "none" : "";
        for (const g of groups) {
          const any = !shownN || g.members.some(m => shownN.has(m)) || (bo && bo.nodes.includes(g.id) && shownN.has(g.id));
          hullEls.get(g.id).style.display = labEls.get(g.id).style.display = any ? "" : "none";
        }
      }
      const known = new Set(["event", "caption", "focus", "nodes", "groups", "assignment", "ok", "phase", "builtNodes", "builtEdges", "skipped"]);
      const extra = [...new Set(ctx.frames.flatMap(x => Object.keys(x)))].filter(k => !known.has(k));
      if (extra.length) note(`Frame fields the GroupedGraph adapter ignores: ${extra.join(", ")}`, false);
      paint();
      return { show(i) { f = ctx.frames[i]; paint(); } };
    },
  });
})();
