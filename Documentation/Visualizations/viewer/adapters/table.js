/* ---- Table (#671): payload {kind:"table", shape:"grid"|"matrix"|"trace", columns[{id,label,key?}], rows[{id,label?}], rowAxis?, columnAxis?, title?}
   frames {event, caption, focus, values[row][col] (string|null), rows{id:{state}}, cells{"r<i>c<j>":{state}}, changed[cell ids], arrows?[{from,to,best}], ok?}
   Drawn with TableView (../mockups/table.js):
     grid   (Edit Distance)  → the mockup's DP grid (drawED) through its loadFrames hook; values, axis names (rowAxis /
                               columnAxis) and the backtrace steps (`back`: the traced cell and the path so far) come from
                               the backend frame.
     matrix (0-1 ILP)        → the mockup's constraint table (drawIP) through loadFrames, given the backend's values, so it
                               draws the backend's columns (x…, Total, Bound, Holds) and leaves undecided cells blank / open;
                               the backend's row/cell states are then applied on top.
     trace  (DFA/NFA/SSSP/SPSP trace tables) → drawn here with table.js's own CSS classes (tb-table, tr.cur/ok/bad, td.chg,
                               tb-pill). The mockup's trace drawers read its own solvers' run data (rows, runs, Dijkstra maps),
                               which the backend's generic columns/values trace does not map onto (NFA especially). */
(() => {
  const ROW_CLASS = { ElementHighlight: "cur", Solution: "ok", Covered: "ok", Rejected: "bad", Untraveled: "unr" };
  const CELL_CLASS = { ElementHighlight: "chg", Solution: "on", Covered: "on", Rejected: "vv-rej" };
  const esc = s => String(s).replace(/[&<>"]/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));
  const cellId = (i, j) => `r${i}c${j}`;
  const parseCell = id => { const m = /^r(\d+)c(\d+)$/.exec(id || ""); return m ? [+m[1], +m[2]] : null; };

  function ensureCss() {
    if (document.getElementById("vv-table-style")) return;
    const st = document.createElement("style"); st.id = "vv-table-style";
    st.textContent = `.tb-table td.vv-rej { background: var(--av-rej-fill); color: var(--av-rej); font-weight: 700; }
.tb-table th.vv-chg { color: var(--av-hl-ink); box-shadow: inset 0 -2.5px 0 var(--av-hl); }`;
    document.head.appendChild(st);
  }

  /** Hide what the Reduction View's construction has not built yet (first builtNodes of buildOrder.nodes are on screen). */
  function hiddenIds(ctx, f) {
    if (f.builtNodes == null || !ctx.buildOrder || !ctx.buildOrder.nodes) return new Set();
    return new Set(ctx.buildOrder.nodes.slice(f.builtNodes));
  }

  function checkStates(f, note) {
    for (const [id, v] of Object.entries(f.rows || {})) { const s = v && v.state; if (s && !ROW_CLASS[s]) note(`Row state "${s}" (${id}) has no style in table.js.`); }
    for (const [id, v] of Object.entries(f.cells || {})) { const s = v && v.state; if (s && !CELL_CLASS[s]) note(`Cell state "${s}" (${id}) has no style in table.js.`); }
  }

  /* ---------- generic (trace, and fallback for any shape) ---------- */
  function drawGeneric(ctx, host, f) {
    const { payload } = ctx, hide = hiddenIds(ctx, f);
    const cols = payload.columns.filter(c => !hide.has(c.id));
    const changed = new Set(f.changed || []);
    const keyOf = c => (c.key || c.label || "").toLowerCase();
    const settledCol = payload.columns.findIndex(c => keyOf(c) === "settled");
    const rowsShown = (f.values || []).map((vals, i) => ({ i, vals, row: payload.rows[i] || { id: "r" + i } })).filter(r => !hide.has(r.row.id));
    const pill = (t, cls) => `<span class="tb-pill ${cls || ""}">${esc(t)}</span>`;
    const head = (payload.rows.some(r => r.label) ? "<th></th>" : "") + cols.map(c => `<th data-vid="${esc(c.id)}">${esc(c.label ?? c.id)}</th>`).join("");
    const body = rowsShown.map(({ i, vals, row }) => {
      const rs = Viewer.stateOf(f.rows, row.id);
      const notSettled = settledCol >= 0 && /^not|^unreach/i.test(String(vals[settledCol] ?? ""));
      const tds = cols.map(c => {
        const j = payload.columns.indexOf(c), v = vals[j], id = cellId(i, j), k = keyOf(c);
        const cls = [];
        if (v != null && /^-?[\d.]+$|^∞$/.test(v)) cls.push("num");
        if (changed.has(id)) cls.push("chg");
        const cs = Viewer.stateOf(f.cells, id);
        if (CELL_CLASS[cs]) cls.push(CELL_CLASS[cs]);
        if (notSettled && ["cost", "via", "path"].includes(k)) cls.push("tent");
        let html = v == null ? "" : esc(v);
        if (v != null && ["accepting", "holds", "accept"].includes(k)) html = pill(v, v === "yes" ? "yes" : v === "no" ? "no" : "");
        if (v != null && k === "settled") html = pill(v, /^#/.test(v) ? "yes" : "");
        return `<td class="${cls.join(" ")}" data-vid="${id}">${html}</td>`;
      }).join("");
      const lab = payload.rows.some(r => r.label) ? `<td class="mut">${esc(row.label ?? "")}</td>` : "";
      return `<tr class="${ROW_CLASS[rs] || ""}" data-vid="${esc(row.id)}">${lab}${tds}</tr>`;
    }).join("");
    host.innerHTML = (payload.title ? `<div class="tb-note"><b>${esc(payload.title)}</b></div>` : "")
      + `<div class="tb-scroll"><table class="tb-table"><thead><tr>${head}</tr></thead><tbody>${body}</tbody></table></div>`
      + (rowsShown.length < payload.rows.length ? `<div class="tb-note">${rowsShown.length} of ${payload.rows.length} rows so far.</div>` : "");
  }

  /* ---------- grid: Edit Distance through drawED ---------- */
  function gridFrame(f, I, note) {
    const vals = f.values || [];
    const m = I.x.length, n = I.y.length;
    let filled = 0, gap = false;
    for (let i = 1; i <= m; i++) for (let j = 1; j <= n; j++) {
      const v = (vals[i] || [])[j];
      if (v != null) { if (gap) note("Edit Distance values are not filled in row-major order; the mockup's grid assumes they are."); filled++; } else gap = true;
    }
    const hl = Object.keys(f.cells || {}).filter(id => Viewer.stateOf(f.cells, id) === "ElementHighlight").map(parseCell).filter(Boolean);
    const sol = Object.keys(f.cells || {}).filter(id => Viewer.stateOf(f.cells, id) === "Solution").map(parseCell).filter(Boolean)
      .sort((a, b) => a[0] - b[0] || a[1] - b[1]);
    const out = { values: vals, filled, caption: f.caption };
    if (hl.length > 1) note("Edit Distance frame highlights more than one cell; the mockup's grid has one current cell.");
    // A backtrace step: one highlighted cell (the one being traced) and no arrows; the Solution cells are the path so far.
    const backtrace = hl.length && !(f.arrows || []).length && f.event !== "Done";
    if (backtrace) out.back = { cur: hl[0], path: sol };
    if (hl.length && f.event !== "Done" && !backtrace) {
      const [i, j] = hl[0];
      out.cur = [i, j];
      const arrows = f.arrows || [], best = arrows.find(a => a.best);
      if (best) {
        const [bi, bj] = parseCell(best.from);
        out.op = bi === i - 1 && bj === j - 1 ? (I.x[i - 1] === I.y[j - 1] ? "keep" : "sub") : bi === i - 1 ? "del" : "ins";
        const want = out.op === "keep" ? 1 : 3;
        if (arrows.length !== want) note(`Edit Distance cell ${cellId(i, j)}: backend sends ${arrows.length} arrows, the mockup draws ${want} (one for a kept letter, three otherwise).`);
      } else if (i > 0 && j > 0) note("Edit Distance step with a current cell but no best arrow.");
    }
    if (f.event === "Done") {
      out.done = true; out.ok = f.ok;
      out.path = sol;
      // Alignment from the backtrace path (the mockup prints it under the grid).
      const ops = [];
      for (let k = 1; k < sol.length; k++) {
        const [i0, j0] = sol[k - 1], [i1, j1] = sol[k];
        if (i1 === i0 + 1 && j1 === j0 + 1) ops.push({ a: I.x[i0], b: I.y[j0], op: I.x[i0] === I.y[j0] ? "keep" : "sub" });
        else if (i1 === i0 + 1 && j1 === j0) ops.push({ a: I.x[i0], b: null, op: "del" });
        else if (i1 === i0 && j1 === j0 + 1) ops.push({ a: null, b: I.y[j0], op: "ins" });
        else note(`Edit Distance backtrace jumps from ${cellId(i0, j0)} to ${cellId(i1, j1)}; not one edit apart.`);
      }
      out.ops = ops;
      if (sol.length && (sol[0][0] !== 0 || sol[0][1] !== 0 || sol.at(-1)[0] !== m || sol.at(-1)[1] !== n)) note("Edit Distance Solution cells do not run from (0,0) to (m,n).");
    }
    return out;
  }

  /* ---------- matrix: 0-1 ILP through drawIP ---------- */
  function matrixFrame(f, n) {
    const row0 = (f.values && f.values[0]) || [];
    const x = Array.from({ length: n }, (_, j) => { const v = row0[j]; return v == null || v === "?" ? null : Number(v); });
    const cur = Object.keys(f.cells || {}).map(id => [id, parseCell(id)]).find(([id, p]) => p && p[0] === 0 && Viewer.stateOf(f.cells, id) === "ElementHighlight");
    return { x, cur: cur ? cur[1][1] : undefined, done: f.event === "Done", ok: f.ok, caption: f.caption, values: f.values || [] };
  }

  function decorateMatrix(ctx, host, f, n, note) {
    const { payload } = ctx, hide = hiddenIds(ctx, f);
    // drawIP's columns with the backend's values: "", x1..xn, Total, Bound, Row holds?  →  backend c0..c(n+2), one to one.
    const tdIndex = j => 1 + j;
    const ths = [...host.querySelectorAll("thead th")], trs = [...host.querySelectorAll("tbody tr")];
    payload.columns.forEach((c, j) => { const th = ths[tdIndex(j)]; if (th) Viewer.tag(th, c.id); });
    trs.forEach((tr, i) => {
      const row = payload.rows[i];
      if (!row) return;
      Viewer.tag(tr, row.id);
      const rs = Viewer.stateOf(f.rows, row.id);
      tr.className = ROW_CLASS[rs] || "";
      tr.style.display = hide.has(row.id) ? "none" : "";
      const tds = [...tr.children];
      payload.columns.forEach((c, j) => {
        const td = tds[tdIndex(j)], id = cellId(i, j);
        if (!td) return;
        Viewer.tag(td, id);
        const cs = Viewer.stateOf(f.cells, id);
        const span = td.querySelector(".tb-x");
        if (span) span.classList.toggle("cur", cs === "ElementHighlight");
        else if (CELL_CLASS[cs]) td.classList.add(CELL_CLASS[cs]);
        // drawIP writes the backend's own Total / Bound / Holds; check it did (null → blank Total, "open" Holds).
        const bv = (f.values[i] || [])[j];
        if (i > 0 && j >= n) {
          const drawn = td.textContent.trim(), want = bv == null ? (j === n + 2 ? "open" : "") : String(bv);
          if (drawn !== want) note(`0-1 ILP ${id}: backend says "${bv}", the table shows "${drawn}".`);
        }
      });
    });
    // Columns not built yet.
    payload.columns.forEach((c, j) => {
      if (!hide.has(c.id)) return;
      const k = tdIndex(j);
      [ths[k], ...trs.map(tr => tr.children[k])].forEach(el => { if (el) el.style.display = "none"; });
    });
  }

  Viewer.register("Table", {
    mount(ctx) {
      ensureCss();
      const { payload, note } = ctx;
      const host = document.createElement("div");
      ctx.host.appendChild(host);
      const view = TableView.create({ host });
      ctx.frames.forEach(f => checkStates(f, note));
      const known = new Set(["event", "caption", "focus", "values", "rows", "cells", "changed", "arrows", "ok", "phase", "builtNodes", "builtEdges", "skipped"]);
      const extra = [...new Set(ctx.frames.flatMap(f => Object.keys(f)))].filter(k => !known.has(k));
      if (extra.length) note(`Frame fields the Table adapter ignores: ${extra.join(", ")}`, false);
      let mode = payload.shape;

      if (mode === "grid") {
        try {
          const { I } = view.loadFrames(ctx.instance, "EDITDISTANCE", []);
          const labels = payload.rows.slice(1).map(r => r.label).join("") + "|" + payload.columns.slice(1).map(c => c.label).join("");
          if (labels !== I.x + "|" + I.y) note(`Edit Distance axis labels (${labels}) differ from the instance's strings (${I.x}|${I.y}).`);
          const frames = ctx.frames.map(f => gridFrame(f, I, note));
          view.loadFrames(ctx.instance, "EDITDISTANCE", frames, { axes: { row: payload.rowAxis, col: payload.columnAxis } });
          return {
            show(i) {
              view.show(i);
              const cells = [...host.querySelectorAll("g.tb-cell")], cols = payload.columns.length;
              cells.forEach((g, k) => Viewer.tag(g, cellId(Math.floor(k / cols), k % cols)));
              // drawED draws the current cell (Active) and the Solution path from the frame; check it matches the backend.
              for (const id of Object.keys(ctx.frames[i].cells || {})) {
                const s = Viewer.stateOf(ctx.frames[i].cells, id), g = cells.find(c => c.dataset.vid === id);
                if (s !== "ElementHighlight" && s !== "Solution") { note(`Edit Distance cell state "${s}" is not drawn by the mockup grid.`); continue; }
                const want = s === "Solution" ? "Solution" : "Active";
                if (g && !g.classList.contains(want)) note(`Edit Distance ${id}: the backend says ${s}, the grid does not draw it ${want}.`);
              }
            },
          };
        } catch (e) {
          note(`Edit Distance grid could not use the mockup's drawing (${e.message}); drawn as a plain table.`);
          mode = "generic";
        }
      }

      if (mode === "matrix") {
        try {
          const n = payload.columns.filter(c => /^x/i.test(c.key || c.label)).length;
          const frames = ctx.frames.map(f => matrixFrame(f, n));
          const { I } = view.loadFrames(ctx.instance, "INTPROGRAMMING01", frames);
          if (I.n !== n) note(`0-1 ILP: the payload has ${n} variable columns, the instance ${I.n}.`);
          if (payload.columns.length !== n + 3) note("0-1 ILP: payload columns are not x1..xn, Total, Bound, Holds; mapping may be off.");
          return { show(i) { view.show(i); decorateMatrix(ctx, host, ctx.frames[i], n, note); } };
        } catch (e) {
          note(`0-1 ILP matrix could not use the mockup's drawing (${e.message}); drawn as a plain table.`);
        }
      }

      if (mode === "trace") note("Trace table drawn by the viewer with table.js's CSS classes: the mockup's trace drawers read its own solvers' run data, not a generic columns/values table.", false);
      return { show(i) { drawGeneric(ctx, host, ctx.frames[i]); } };
    },
  });
})();
