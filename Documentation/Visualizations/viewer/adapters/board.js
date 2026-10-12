/* ---- Board (#670): payload {kind:"board", game: sudoku|queens, size, box?, labelsFrom, clues[{id,r,c,value}]}
   frames {event, caption, focus, values{cellId:"digit"}, pieces[{id,r,c,kind}], cells{cellId:{state}},
   links[{from,to,state}], ok?}   (cell ids are "r<row>c<col>", 0-based)
   Drawn with BoardView (../mockups/board.js) through its loadModel + afterPaint hooks: the mockup draws the grid, the
   digits (clues bold, placed digits green) and the queens from a neutral frame; the adapter then applies the
   backend's cell states, its links and its piece states, after every paint (so hover still works). */
(() => {
  const CELL_CLASS = { ElementHighlight: "Active", Rejected: "Rejected", Solution: "Solution" };

  Viewer.register("Board", {
    mount(ctx) {
      const { payload, note } = ctx;
      const n = payload.size, sudoku = payload.game === "sudoku";
      if (!sudoku && payload.game !== "queens") throw new Error(`Unknown Board game "${payload.game}".`);
      const cid = (r, c) => `r${r}c${c}`;
      const clueGrid = Array.from({ length: n }, () => Array(n).fill(0));
      (payload.clues || []).forEach(k => { clueGrid[k.r][k.c] = Number(k.value) || 0; });
      const G = sudoku ? { kind: "sudoku", n, b: payload.box ?? Math.round(Math.sqrt(n)), grid: clueGrid, clashes: [] } : { kind: "queens", n };
      if (sudoku && n > 16) note(`${n} × ${n}: board.js draws Sudoku grids up to 16 × 16.`, false);
      if (!sudoku && n > 12) note(`${n} × ${n}: the mockup parser caps N-Queens at 12 × 12 (the viewer builds the board from the payload, so it still draws).`, false);
      const wantLabels = sudoku ? 1 : 0;
      if (payload.labelsFrom != null && payload.labelsFrom !== wantLabels) note(`labelsFrom ${payload.labelsFrom}, but board.js numbers ${payload.game} axes from ${wantLabels}.`);
      const known = new Set(["event", "caption", "focus", "values", "pieces", "cells", "links", "ok", "phase"]);
      const extra = [...new Set(ctx.frames.flatMap(f => Object.keys(f)))].filter(k => !known.has(k));
      if (extra.length) note(`Frame fields the Board adapter ignores: ${extra.join(", ")}`, false);

      if (sudoku && ctx.frames.some(f => f.event !== "Done" && Object.values(f.cells || {}).some(v => (v.state || v) === "Solution")))
        note("Sudoku marks every digit placed so far as Solution on each step; board.js keeps placed digits plain (green digit, no fill) until the solved frame.", false);
      // Neutral mockup frames: sudoku digits as a grid, queens as row → column. kind "show" makes board.js add no states of its own.
      const frames = ctx.frames.map((f, k) => {
        if (sudoku) {
          const grid = clueGrid.map(r => r.slice());
          for (const [id, v] of Object.entries(f.values || {})) { const m = /^r(\d+)c(\d+)$/.exec(id); if (m) grid[+m[1]][+m[2]] = Number(v) || 0; else note(`Cell id "${id}" is not r<row>c<col>.`); }
          return { kind: "show", grid, caption: f.caption };
        }
        const queens = [];
        (f.pieces || []).forEach(p => {
          if (p.kind && p.kind !== "queen") note(`Piece kind "${p.kind}": board.js only draws queens.`);
          if (queens[p.r] !== undefined) note(`Frame ${k + 1}: two pieces in row ${p.r}; board.js keeps one queen per row.`);
          queens[p.r] = p.c;
        });
        return { kind: "show", queens, caption: f.caption };
      });

      let cur = 0;
      const afterPaint = (fr, svg) => {
        const f = ctx.frames[cur];
        if (!f || fr !== frames[cur]) return;
        const cells = svg.querySelectorAll("rect.bd-cell"), rings = svg.querySelectorAll("rect.bd-ring"), digs = svg.querySelectorAll("text.bd-dig");
        for (const id of Object.keys(f.cells || {})) {
          const st = Viewer.stateOf(f.cells, id), m = /^r(\d+)c(\d+)$/.exec(id);
          if (!m || st === "Background") continue;
          const i = +m[1] * n + +m[2], cls = CELL_CLASS[st];
          if (!cls) { note(`Cell state "${st}" has no style in board.js (drawn as Background).`); continue; }
          cells[i].setAttribute("class", "bd-cell " + cls);
          if (cls !== "Solution" && !rings[i].classList.contains("trace")) rings[i].setAttribute("class", "bd-ring " + cls);
          if (sudoku && digs[i] && clueGrid[+m[1]][+m[2]] === 0) digs[i].setAttribute("class", "bd-dig " + (cls === "Active" ? "try" : cls === "Rejected" ? "bad" : "placed"));
          if (sudoku && digs[i] && clueGrid[+m[1]][+m[2]] !== 0 && cls === "Rejected") digs[i].setAttribute("class", "bd-dig bad given");
        }
        // Queens: board.js appends them in row order; colour each by its square's state.
        const qs = [...svg.querySelectorAll("g.bd-queen")], qRows = frames[cur].queens ? Object.keys(frames[cur].queens).map(Number).sort((a, b) => a - b) : [];
        qs.forEach((q, j) => {
          const r = qRows[j], c = frames[cur].queens[r], id = cid(r, c);
          const cls = CELL_CLASS[Viewer.stateOf(f.cells, id)] || "";
          q.setAttribute("class", "bd-queen " + cls);
          Viewer.tag(q, id);
        });
        // Links (a clash or an attack): from cell to cell, drawn like board.js's own clash links.
        const gLinks = svg.querySelectorAll(":scope > g")[2];
        gLinks.querySelectorAll(".vv-bd-link").forEach(x => x.remove());
        (f.links || []).forEach(l => {
          const a = /^r(\d+)c(\d+)$/.exec(l.from), b = /^r(\d+)c(\d+)$/.exec(l.to);
          if (!a || !b) { note(`Link ${l.from} → ${l.to} does not name two cells.`); return; }
          if (l.state && l.state !== "Rejected") note(`Link with state "${l.state}"; board.js draws every link as a red clash.`);
          const ca = cells[+a[1] * n + +a[2]], cb = cells[+b[1] * n + +b[2]];
          const cx = el => +el.getAttribute("x") + +el.getAttribute("width") / 2, cy = el => +el.getAttribute("y") + +el.getAttribute("height") / 2;
          Viewer.mk("line", { x1: cx(ca), y1: cy(ca), x2: cx(cb), y2: cy(cb), class: "bd-link vv-bd-link" }, sudoku ? gLinks : svg.querySelectorAll(":scope > g")[5]);
        });
      };
      const svg = Viewer.svgIn(ctx.host, `${ctx.problem} board`);
      const view = BoardView.create({ svg, afterPaint });
      view.loadModel(G, { kind: G.kind }, frames);
      svg.querySelectorAll("rect.bd-cell").forEach((c, i) => Viewer.tag(c, cid(Math.floor(i / n), i % n)));
      return { show(i) { cur = i; view.show(i); } };
    },
  });
})();
