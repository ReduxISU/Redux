/* ---- Board view (Sudoku, N-Queens), shared by both mockups ----
   A grid of cells. Solvers emit frames in the problem's own terms (a grid of digits, a list of queens);
   the view derives every color from the frame. Pages supply --av-* tokens. */
const BoardView = (() => {
  const NS = "http://www.w3.org/2000/svg";
  const mk = (tag, attrs = {}, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; };
  const SHOW = 250, BUDGET = 200000;

  const css = `
.bd-svg { width: 100%; height: auto; display: block; max-height: 640px; }
.bd-svg text { font-family: inherit; }
.bd-axis { font-size: 11px; fill: var(--av-muted); text-anchor: middle; dominant-baseline: central; font-family: var(--av-mono); }
.bd-cell { fill: var(--av-surface); stroke: none; transition: fill .2s; cursor: pointer; }
.bd-cell.given { fill: color-mix(in srgb, var(--av-line) 45%, var(--av-surface)); }
.bd-cell.dark { fill: color-mix(in srgb, var(--av-line) 75%, var(--av-surface)); }
.bd-cell.unit { fill: color-mix(in srgb, var(--av-hot) 13%, var(--av-surface)); }
.bd-cell.attacked { fill: color-mix(in srgb, var(--av-rej) 7%, var(--av-surface)); }
.bd-cell.Active { fill: var(--av-hl-fill); }
.bd-cell.Rejected { fill: var(--av-rej-fill); }
.bd-cell.Solution { fill: var(--av-sol-fill); }
.bd-cell:focus { outline: none; }
.bd-ring { fill: none; stroke: transparent; stroke-width: 3; pointer-events: none; transition: stroke .2s; }
.bd-ring.Active { stroke: var(--av-hl); }
.bd-ring.Rejected { stroke: var(--av-rej); }
.bd-ring.trace { stroke: var(--av-hot); }
.bd-thin { stroke: var(--av-line); stroke-width: 1; }
.bd-thick { stroke: var(--av-stroke); stroke-width: 2.2; fill: none; }
.bd-dig { text-anchor: middle; dominant-baseline: central; pointer-events: none; fill: var(--av-ink); }
.bd-dig.given { font-weight: 700; }
.bd-dig.placed { fill: var(--av-sol); font-weight: 500; }
.bd-dig.try { fill: var(--av-ink); font-weight: 700; }
.bd-dig.bad { fill: var(--av-rej); font-weight: 700; }
.bd-mark { text-anchor: middle; dominant-baseline: central; fill: var(--av-muted); pointer-events: none; font-family: var(--av-mono); }
.bd-link { stroke: var(--av-rej); stroke-width: 2.2; stroke-dasharray: 5 4; fill: none; pointer-events: none; }
.bd-x { stroke: var(--av-rej); stroke-width: 2.4; stroke-linecap: round; pointer-events: none; }
.bd-ray { stroke: var(--av-hot); stroke-width: 1.6; stroke-dasharray: 3 4; pointer-events: none; }
.bd-queen path, .bd-queen circle { fill: var(--av-ink); stroke: var(--av-surface); stroke-width: 1; transition: fill .2s; }
.bd-queen.Active path, .bd-queen.Active circle { fill: var(--av-hl); stroke: var(--av-ink); }
.bd-queen.Solution path, .bd-queen.Solution circle { fill: var(--av-sol); stroke: var(--av-surface); }
.bd-queen.Rejected path, .bd-queen.Rejected circle { fill: var(--av-rej); }
@media (prefers-reduced-motion: reduce) { .bd-cell, .bd-ring, .bd-queen path, .bd-queen circle { transition: none; } }`;

  /* ---------- catalog: names, definitions, defaults and solver names copied from Redux ---------- */
  const SUDOKU_DEFAULT = "0,0,0,1,0,0,2,0,3;\n0,2,0,0,4,0,5,0,6;\n0,7,0,0,0,6,4,0,0;\n5,0,0,6,0,0,8,0,0;\n0,6,0,4,0,2,0,5,0;\n0,0,4,0,0,9,0,0,7;\n0,0,9,5,0,0,0,4,0;\n7,0,6,0,8,0,0,1,0;\n4,0,3,0,0,7,0,0,0";
  const PROBLEMS = {
    SUDOKU: {
      label: "Sudoku", kind: "sudoku", cls: "NP-Complete",
      def: "Sudoku is a logic-based, combinatorial number-placement puzzle where the goal is to fill a 9x9 grid with digits so that each column, row, and 3x3 box contains all of the digits from 1 to 9.",
      format: "Rows of comma-separated digits (0 for empty, 1-9 for clues), rows separated by semicolons",
      solvers: [["bt", "Sudoku Backtracking"], ["naive", "Try every digit (not in Redux yet)"]],
      examples: [
        ["Redux default", SUDOKU_DEFAULT],
        ["4 × 4, easy to follow", "1,0,0,0;\n0,0,3,0;\n0,4,0,0;\n0,0,0,2"],
        ["4 × 4, no solution", "0,0,0,1;\n0,0,0,0;\n0,0,2,3;\n1,0,0,0"],
        ["4 × 4, clashing clues", "1,0,0,1;\n0,0,0,0;\n0,0,0,0;\n0,0,0,0"],
      ] },
    NQUEENS: {
      label: "N-Queens", kind: "queens", cls: "P",
      def: "Given an integer n, determine whether n queens can be placed on an n x n chessboard so that no two queens share a row, column, or diagonal. Although finding a placement is often taught alongside NP-complete search problems, the decision version is trivial (a solution exists for every n except 2 and 3) and an explicit placement can be constructed in O(n) time, so N-Queens lies in P.",
      format: "A single non-negative integer n giving the board size / number of queens",
      solvers: [["bt", "N-Queens Backtracking"], ["construct", "N-Queens Constructive"]],
      examples: [["Redux default", "4"], ["Eight queens", "8"], ["n = 3, impossible", "3"], ["n = 9 (formula adjusts)", "9"]] },
  };

  /* ---------- parsing ---------- */
  function parseSudoku(str) {
    const rows = str.replace(/\r?\n/g, "").split(";").map(r => r.trim()).filter(r => r.length);
    if (!rows.length) throw new Error("The grid is empty.");
    const grid = rows.map((r, i) => r.split(",").map(x => x.trim()).filter(x => x.length).map(x => {
      if (!/^\d+$/.test(x)) throw new Error(`Row ${i + 1}: "${x}" isn't a digit.`);
      return Number(x);
    }));
    const n = grid.length, b = Math.round(Math.sqrt(n));
    if (b * b !== n) throw new Error(`The grid has ${n} rows. Sudoku needs a square number of rows: 4, 9 or 16.`);
    if (n > 16) throw new Error("This mockup draws grids up to 16 × 16.");
    grid.forEach((r, i) => { if (r.length !== n) throw new Error(`Row ${i + 1} has ${r.length} digits; every row needs ${n}.`); });
    grid.forEach((r, i) => r.forEach((v, j) => { if (v > n) throw new Error(`R${i + 1}C${j + 1} holds ${v}; digits run from 0 (empty) to ${n}.`); }));
    return { kind: "sudoku", n, b, grid, clashes: findClashes(grid, b) };
  }
  function parseQueens(str) {
    const s = str.trim();
    if (!/^\d+$/.test(s)) throw new Error("Enter a single whole number n, the board size.");
    const n = Number(s);
    if (n < 1 || n > 12) throw new Error("This mockup draws boards from 1 × 1 up to 12 × 12.");
    return { kind: "queens", n };
  }
  function parse(str, P) { return P.kind === "sudoku" ? parseSudoku(str) : parseQueens(str); }

  /* ---------- Sudoku helpers ---------- */
  const unitOf = (b, r1, c1, r2, c2) => r1 === r2 ? "row" : c1 === c2 ? "column" : (Math.floor(r1 / b) === Math.floor(r2 / b) && Math.floor(c1 / b) === Math.floor(c2 / b)) ? "box" : null;
  // every pair of filled cells that share a row, column or box and hold the same digit
  function findClashes(grid, b) {
    const n = grid.length, out = [];
    for (let p = 0; p < n * n; p++) for (let q = p + 1; q < n * n; q++) {
      const r1 = Math.floor(p / n), c1 = p % n, r2 = Math.floor(q / n), c2 = q % n;
      const v = grid[r1][c1];
      if (v && v === grid[r2][c2] && unitOf(b, r1, c1, r2, c2)) out.push({ a: [r1, c1], b: [r2, c2], v, unit: unitOf(b, r1, c1, r2, c2) });
    }
    return out;
  }
  const rc = ([r, c]) => `R${r + 1}C${c + 1}`;
  const listText = a => a.length <= 2 ? a.join(" or ") : a.slice(0, -1).join(", ") + " or " + a.at(-1);
  // digits not yet in the cell's row, column or box: exactly the solver's Intersect(rows, cols, blocks)
  function allowed(grid, b, r, c) {
    const n = grid.length, used = new Set();
    for (let i = 0; i < n; i++) { used.add(grid[r][i]); used.add(grid[i][c]); }
    const br = Math.floor(r / b) * b, bc = Math.floor(c / b) * b;
    for (let i = 0; i < b; i++) for (let j = 0; j < b; j++) used.add(grid[br + i][bc + j]);
    const out = [];
    for (let v = 1; v <= n; v++) if (!used.has(v)) out.push(v);
    return out;
  }
  // first filled cell in the same row, then column, then box that already holds v (for the "try every digit" solver)
  function clashWith(grid, b, r, c, v) {
    const n = grid.length;
    for (let j = 0; j < n; j++) if (j !== c && grid[r][j] === v) return { at: [r, j], unit: "row" };
    for (let i = 0; i < n; i++) if (i !== r && grid[i][c] === v) return { at: [i, c], unit: "column" };
    const br = Math.floor(r / b) * b, bc = Math.floor(c / b) * b;
    for (let i = 0; i < b; i++) for (let j = 0; j < b; j++) { const rr = br + i, cc = bc + j; if ((rr !== r || cc !== c) && grid[rr][cc] === v) return { at: [rr, cc], unit: "box" }; }
    return null;
  }

  /* ---------- Sudoku solvers: frames carry the grid as the solver sees it ---------- */
  function sudokuSolve(G, naive) {
    const n = G.n, b = G.b, grid = G.grid.map(r => r.slice()), frames = [];
    let tries = 0, shown = 0, events = 0, gaveUp = false;
    const snap = () => grid.map(r => r.slice());
    const push = make => { events++; if (shown < SHOW) { frames.push(make()); shown++; } };
    function rec(pos) {
      if (pos === n * n) return true;
      if (tries > BUDGET) { gaveUp = true; return false; }
      const r = Math.floor(pos / n), c = pos % n;
      if (grid[r][c] !== 0) return rec(pos + 1);
      if (naive) {
        for (let v = 1; v <= n; v++) {
          tries++;
          const hit = clashWith(grid, b, r, c, v);
          if (hit) { push(() => ({ kind: "clash", grid: snap(), cell: [r, c], value: v, with: hit.at, unit: hit.unit, caption: `Try ${v} in ${rc([r, c])}: ${rc(hit.at)} in the same ${hit.unit} already has ${v}. Reject it.` })); continue; }
          grid[r][c] = v;
          push(() => ({ kind: "try", grid: snap(), cell: [r, c], value: v, caption: `Try ${v} in ${rc([r, c])}. Nothing in its row, column or box has ${v}, so it stays for now.` }));
          if (rec(pos + 1)) return true;
          grid[r][c] = 0;
          if (gaveUp) return false;
        }
        push(() => ({ kind: "dead", grid: snap(), cell: [r, c], caption: `${rc([r, c])} has no digit left that fits. Undo the previous choice.` }));
        return false;
      }
      const cands = allowed(grid, b, r, c);
      if (!cands.length) {
        push(() => ({ kind: "dead", grid: snap(), cell: [r, c], caption: `${rc([r, c])} has no digit left: every digit from 1 to ${n} is already in its row, column or box. Undo the last choice.` }));
        return false;
      }
      for (let i = 0; i < cands.length; i++) {
        const v = cands[i];
        tries++;
        grid[r][c] = v;
        push(() => ({ kind: "try", grid: snap(), cell: [r, c], value: v, cands,
          caption: i === 0 ? `${rc([r, c])} can still hold ${listText(cands)}. Try ${v}.` : `${cands[i - 1]} in ${rc([r, c])} led to a dead end. Try ${v} instead.` }));
        if (rec(pos + 1)) return true;
        grid[r][c] = 0;
        if (gaveUp) return false;
      }
      return false;
    }
    const solved = rec(0);
    const hidden = events > shown ? `; the first ${shown} steps are shown` : "";
    let done;
    if (gaveUp) done = { kind: "done", grid: G.grid.map(r => r.slice()), ok: false, caption: `Stopped after ${BUDGET.toLocaleString("en-US")} tries${hidden}. Redux's solver stops on its timer the same way.` };
    else if (solved) done = { kind: "done", grid: snap(), ok: true, caption: `Every cell is filled and no row, column or box repeats a digit. Solved after ${tries.toLocaleString("en-US")} tries${hidden}.` };
    else {
      // clashing clues can never be completed (every row, column and box must use each digit once), but the solver
      // never checks its clues, so it only finds out by exhausting the search
      const k = G.clashes[0];
      done = { kind: "done", grid: G.grid.map(r => r.slice()), ok: false, caption: `Every choice ran into a dead end after ${tries.toLocaleString("en-US")} tries${hidden}. This grid has no solution.` +
        (k ? ` The clues already clash (${rc(k.a)} and ${rc(k.b)} both hold ${k.v}); checking the clues first would have said so before any search.` : "") };
    }
    frames.push(done);
    return frames;
  }

  /* ---------- N-Queens solvers: frames carry the queens placed so far (row → column) ---------- */
  const attacker = (queens, row, col) => {
    for (let i = 0; i < queens.length; i++) {
      if (queens[i] === col) return { r: i, c: queens[i], how: "column" };
      if (Math.abs(queens[i] - col) === Math.abs(i - row)) return { r: i, c: queens[i], how: "diagonal" };
    }
    return null;
  };
  const listCols = cs => cs.length === 1 ? `column ${cs[0]} is` : `columns ${cs.slice(0, -1).join(", ")} and ${cs.at(-1)} are`;
  const why = x => `${x.c} ${x.by.how === "column" ? "along its column" : "diagonally"} by the queen at (${x.by.r},${x.by.c})`;
  function placeCaption(row, col, rej, again) {
    const reason = rej.length ? `${listCols(rej.map(x => x.c))} attacked (${why(rej[0])})` : "";
    if (again) return `Back in row ${row}: ${reason ? reason + ", so move" : "move"} the queen on to column ${col}.`;
    return reason ? `Row ${row}: ${reason}. Place the queen at column ${col}.` : `Row ${row}: column ${col} is safe. Place the queen there.`;
  }
  const certOf = qs => "{" + qs.map((c, r) => `(${r},${c})`).join(",") + "}";
  function queensBT(n) {
    const board = [], frames = [];
    let placed = 0, shown = 0, events = 0;
    const push = make => { events++; if (shown < SHOW) { frames.push(make()); shown++; } };
    function rec(row) {
      if (row === n) return true;
      let rejected = [], resumed = false;
      for (let col = 0; col < n; col++) {
        const by = attacker(board.slice(0, row), row, col);
        if (by) { rejected.push({ c: col, by }); continue; }
        board[row] = col; placed++;
        const rej = rejected.slice(), again = resumed;
        push(() => ({ kind: "place", queens: board.slice(0, row + 1), row, col, rejected: rej, caption: placeCaption(row, col, rej, again) }));
        rejected = []; resumed = true;
        if (rec(row + 1)) return true;
      }
      const rej = rejected.slice();
      push(() => ({ kind: "dead", queens: board.slice(0, row), row, rejected: rej,
        caption: row === 0 ? `Row 0 has no column left to try.` : rej.length === 0 ? `Row ${row}: its queen was already in the last column, so nothing is left to try. Take back the queen in row ${row - 1}.` : (resumed ? `Row ${row}: ` : `Row ${row}: every column is attacked, so no queen fits. `) + (resumed ? `the rest of the row is attacked too. ` : "") + `Take back the queen in row ${row - 1}.` }));
      return false;
    }
    const ok = rec(0);
    frames.push(ok
      ? { kind: "done", queens: board.slice(0, n), ok: true, caption: `All ${n} queens are placed and none attacks another: ${certOf(board.slice(0, n))}. Found after ${placed} placements${events > shown ? `; the first ${shown} steps are shown` : ""}.` }
      : { kind: "done", queens: [], ok: false, caption: `Every arrangement runs into an attack. No placement exists for n = ${n}, so the solver returns {}.` });
    return frames;
  }
  // N-Queens Constructive, step for step: even columns first, then odd, with the mod-12 adjustments
  function queensConstruct(n) {
    const frames = [];
    if (n === 2 || n === 3) {
      frames.push({ kind: "done", queens: [], ok: false, caption: `The formula has no answer for n = ${n}, and none exists, so the solver returns {} without placing anything.` });
      return frames;
    }
    const rem = n % 12, evens = [], odds = [];
    for (let c = 2; c <= n; c += 2) evens.push(c);
    for (let c = 1; c <= n; c += 2) odds.push(c);
    const moveToEnd = (a, v) => { const i = a.indexOf(v); if (i >= 0) { a.splice(i, 1); a.push(v); } };
    const notes = [];
    if (rem === 3 || rem === 9) { moveToEnd(evens, 2); notes.push("move 2 to the end of the evens"); }
    if (rem === 8) { for (let i = 0; i + 1 < odds.length; i += 2) [odds[i], odds[i + 1]] = [odds[i + 1], odds[i]]; notes.push("swap the odds in pairs"); }
    else if (rem === 2) { const ia = odds.indexOf(1), ib = odds.indexOf(3); if (ia >= 0 && ib >= 0) [odds[ia], odds[ib]] = [odds[ib], odds[ia]]; moveToEnd(odds, 5); notes.push("swap 1 and 3, then move 5 to the end of the odds"); }
    else if (rem === 3 || rem === 9) { moveToEnd(odds, 1); moveToEnd(odds, 3); notes.push("move 1 and 3 to the end of the odds"); }
    const order = evens.concat(odds), cols = order.map(c => c - 1);
    frames.push({ kind: "plan", queens: [], order, caption: `n = ${n}, so n mod 12 = ${rem}. Write the even columns, then the odd ones${notes.length ? ", and " + notes.join("; ") : ""}: ${order.join(", ")}. Row i gets the i-th number, minus 1.` });
    for (let row = 0; row < n; row++) frames.push({ kind: "place", queens: cols.slice(0, row + 1), row, col: cols[row], rejected: [], caption: `Row ${row}: column ${cols[row]}, straight from the list. No checking needed.` });
    let bad = 0;
    for (let i = 0; i < n; i++) for (let j = i + 1; j < n; j++) if (cols[i] === cols[j] || Math.abs(cols[i] - cols[j]) === j - i) bad++;
    frames.push({ kind: "done", queens: cols, ok: bad === 0, caption: bad === 0 ? `All ${n} queens placed in ${n} steps, with no search: ${certOf(cols)}.` : `${bad} pairs attack each other. The formula failed for n = ${n}.` });
    return frames;
  }

  const SOLVE = {
    sudoku: { bt: G => sudokuSolve(G, false), naive: G => sudokuSolve(G, true) },
    queens: { bt: G => queensBT(G.n), construct: G => queensConstruct(G.n) },
  };

  /* ---------- the view ---------- */
  function create({ svg }) {
    if (!document.getElementById("bd-style")) {
      const st = document.createElement("style"); st.id = "bd-style"; st.textContent = css; document.head.appendChild(st);
    }
    svg.classList.add("bd-svg");
    let G, P, frames, frame = null, hover = null, hints = false, els;

    function geometry() {
      const n = G.n, cs = G.kind === "sudoku" ? (n <= 4 ? 84 : n <= 9 ? 54 : 36) : Math.max(40, Math.min(76, Math.floor(520 / n)));
      const L = 34, T = 30;
      return { n, cs, L, T, W: L + n * cs + 16, H: T + n * cs + 16 };
    }
    const center = (g, r, c) => [g.L + c * g.cs + g.cs / 2, g.T + r * g.cs + g.cs / 2];

    function draw() {
      svg.innerHTML = "";
      const g = geometry(), n = g.n;
      svg.setAttribute("viewBox", `0 0 ${g.W} ${g.H}`);
      svg.setAttribute("aria-label", G.kind === "sudoku" ? `${n} by ${n} Sudoku grid` : `${n} by ${n} chessboard`);
      const gCells = mk("g", {}, svg), gLines = mk("g", {}, svg), gLinks = mk("g", {}, svg), gMarks = mk("g", {}, svg), gDig = mk("g", {}, svg), gOver = mk("g", {}, svg);
      els = { g, cells: [], rings: [], digs: [], gLinks, gMarks, gOver, queens: [] };
      for (let i = 0; i < n; i++) {
        const lab = G.kind === "sudoku" ? String(i + 1) : String(i);
        mk("text", { x: g.L - 14, y: g.T + i * g.cs + g.cs / 2, class: "bd-axis" }, svg).textContent = lab;
        mk("text", { x: g.L + i * g.cs + g.cs / 2, y: g.T - 13, class: "bd-axis" }, svg).textContent = lab;
      }
      for (let r = 0; r < n; r++) {
        els.cells.push([]); els.rings.push([]); els.digs.push([]);
        for (let c = 0; c < n; c++) {
          const x = g.L + c * g.cs, y = g.T + r * g.cs;
          const cell = mk("rect", { x, y, width: g.cs, height: g.cs, class: "bd-cell", tabindex: "0", role: "button",
            "aria-label": G.kind === "sudoku" ? `Row ${r + 1}, column ${c + 1}` : `Row ${r}, column ${c}` }, gCells);
          const on = () => { hover = [r, c]; paint(); }, off = () => { hover = null; paint(); };
          cell.addEventListener("pointerenter", on); cell.addEventListener("pointerleave", off);
          cell.addEventListener("focus", on); cell.addEventListener("blur", off);
          cell.addEventListener("click", () => { hover = hover && hover[0] === r && hover[1] === c ? null : [r, c]; paint(); });
          els.cells[r].push(cell);
          els.rings[r].push(mk("rect", { x: x + 2, y: y + 2, width: g.cs - 4, height: g.cs - 4, rx: 3, class: "bd-ring" }, gOver));
          if (G.kind === "sudoku") els.digs[r].push(mk("text", { x: x + g.cs / 2, y: y + g.cs / 2 + 1, class: "bd-dig", "font-size": Math.round(g.cs * 0.5) }, gDig));
        }
      }
      for (let i = 0; i <= n; i++) {
        mk("line", { x1: g.L, y1: g.T + i * g.cs, x2: g.L + n * g.cs, y2: g.T + i * g.cs, class: "bd-thin" }, gLines);
        mk("line", { x1: g.L + i * g.cs, y1: g.T, x2: g.L + i * g.cs, y2: g.T + n * g.cs, class: "bd-thin" }, gLines);
      }
      if (G.kind === "sudoku") {
        for (let i = 0; i <= n; i += G.b) {
          mk("line", { x1: g.L, y1: g.T + i * g.cs, x2: g.L + n * g.cs, y2: g.T + i * g.cs, class: "bd-thick" }, gLines);
          mk("line", { x1: g.L + i * g.cs, y1: g.T, x2: g.L + i * g.cs, y2: g.T + n * g.cs, class: "bd-thick" }, gLines);
        }
      } else mk("rect", { x: g.L, y: g.T, width: n * g.cs, height: n * g.cs, class: "bd-thick" }, gLines);
    }

    // a crown, centered on the square
    function queen(parent, cx, cy, s, cls) {
      const k = s / 40, gq = mk("g", { class: "bd-queen " + cls, transform: `translate(${cx - 20 * k},${cy - 20 * k}) scale(${k})` }, parent);
      mk("path", { d: "M7 31 L5 14 L13 22 L20 9 L27 22 L35 14 L33 31 Z M7 33 H33 V36 H7 Z" }, gq);
      mk("circle", { cx: 5, cy: 12, r: 2.6 }, gq); mk("circle", { cx: 20, cy: 7, r: 2.6 }, gq); mk("circle", { cx: 35, cy: 12, r: 2.6 }, gq);
      return gq;
    }

    function paintSudoku() {
      const g = els.g, n = G.n, b = G.b, f = frame;
      const grid = f ? f.grid : G.grid, cell = f && f.cell, done = f && f.kind === "done";
      const units = new Set();
      const addUnits = (r, c, which) => {
        for (let i = 0; i < n; i++) {
          if (!which || which === "row") units.add(r * n + i);
          if (!which || which === "column") units.add(i * n + c);
        }
        if (!which || which === "box") { const br = Math.floor(r / b) * b, bc = Math.floor(c / b) * b; for (let i = 0; i < b; i++) for (let j = 0; j < b; j++) units.add((br + i) * n + bc + j); }
      };
      if (hover) addUnits(hover[0], hover[1]);
      else if (f && f.kind === "dead") addUnits(cell[0], cell[1]);
      else if (f && f.kind === "clash") addUnits(cell[0], cell[1], f.unit);
      const clashCells = new Set();
      const links = [];
      if (!f || f.kind === "start" || (done && !f.ok)) G.clashes.forEach(k => { clashCells.add(k.a[0] * n + k.a[1]); clashCells.add(k.b[0] * n + k.b[1]); links.push([k.a, k.b]); });
      if (f && f.kind === "clash") { clashCells.add(f.with[0] * n + f.with[1]); links.push([f.cell, f.with]); }
      els.gLinks.innerHTML = "";
      els.gMarks.innerHTML = "";
      for (let r = 0; r < n; r++) for (let c = 0; c < n; c++) {
        const given = G.grid[r][c] !== 0, v = grid[r][c], id = r * n + c, here = cell && cell[0] === r && cell[1] === c;
        let st = given ? "given" : "";
        if (units.has(id)) st = "unit";
        if (done && f.ok) st = given ? "given" : "Solution";
        let ring = "";
        if (here && f.kind === "try") { st = "Active"; ring = "Active"; }
        if (here && (f.kind === "dead" || f.kind === "clash")) { st = "Rejected"; ring = "Rejected"; }
        if (clashCells.has(id)) { st = "Rejected"; ring = "Rejected"; }
        if (hover && hover[0] === r && hover[1] === c) ring = "trace";
        els.cells[r][c].setAttribute("class", "bd-cell " + st);
        els.rings[r][c].setAttribute("class", "bd-ring " + ring);
        const d = els.digs[r][c];
        let text = v ? String(v) : "", dcls = given ? "given" : "placed";
        if (here && f.kind === "clash") { text = String(f.value); dcls = "bad"; }
        else if (here && f.kind === "try") dcls = "try";
        else if (clashCells.has(id) && given) dcls = "bad given";
        d.textContent = text;
        d.setAttribute("class", "bd-dig " + dcls);
        if (hints && !v && !(here && f.kind === "clash")) {
          const cand = allowed(grid, b, r, c), x0 = g.L + c * g.cs, y0 = g.T + r * g.cs, step = g.cs / b;
          cand.forEach(val => {
            const i = val - 1;
            mk("text", { x: x0 + (i % b) * step + step / 2, y: y0 + Math.floor(i / b) * step + step / 2 + 1, class: "bd-mark", "font-size": Math.max(7, Math.round(step * 0.55)) }, els.gMarks).textContent = val;
          });
        }
      }
      links.forEach(([a, z]) => {
        const [x1, y1] = center(g, a[0], a[1]), [x2, y2] = center(g, z[0], z[1]);
        mk("line", { x1, y1, x2, y2, class: "bd-link" }, els.gLinks);
      });
    }

    function paintQueens() {
      const g = els.g, n = G.n, f = frame;
      const qs = f && f.queens ? f.queens : [], done = f && f.kind === "done";
      els.gOver.querySelectorAll(".bd-queen,.bd-link,.bd-x,.bd-ray").forEach(x => x.remove());
      const attacked = new Set();
      if (hints) qs.forEach((c, r) => {
        for (let i = 0; i < n; i++) for (let j = 0; j < n; j++) if (i !== r && (j === c || Math.abs(j - c) === Math.abs(i - r))) attacked.add(i * n + j);
        for (let j = 0; j < n; j++) if (j !== c) attacked.add(r * n + j);
      });
      const rejected = new Map((f && f.rejected || []).map(x => [x.c, x.by]));
      for (let r = 0; r < n; r++) for (let c = 0; c < n; c++) {
        let st = (r + c) % 2 ? "dark" : "";
        if (attacked.has(r * n + c)) st = "attacked";
        if (f && f.kind === "dead" && r === f.row) st = "Rejected";
        if (f && f.row === r && rejected.has(c)) st = "Rejected";
        if (f && f.kind === "place" && f.row === r && f.col === c) st = "Active";
        if (done && f.ok && qs[r] === c) st = "Solution";
        els.cells[r][c].setAttribute("class", "bd-cell " + st);
        els.rings[r][c].setAttribute("class", "bd-ring " + (hover && hover[0] === r && hover[1] === c ? "trace" : ""));
      }
      if (f) (f.rejected || []).forEach(({ c, by }) => {
        const [x, y] = center(g, f.row, c), [qx, qy] = center(g, by.r, by.c), d = g.cs * 0.22;
        mk("line", { x1: qx, y1: qy, x2: x, y2: y, class: "bd-link" }, els.gOver);
        mk("line", { x1: x - d, y1: y - d, x2: x + d, y2: y + d, class: "bd-x" }, els.gOver);
        mk("line", { x1: x - d, y1: y + d, x2: x + d, y2: y - d, class: "bd-x" }, els.gOver);
      });
      qs.forEach((c, r) => {
        const [x, y] = center(g, r, c);
        const cls = done ? (f.ok ? "Solution" : "Rejected") : f && f.kind === "place" && f.row === r ? "Active" : f && f.kind === "dead" && r === f.row - 1 ? "Rejected" : "";
        queen(els.gOver, x, y, g.cs * 0.8, cls);
      });
      if (hover) {
        const [hr, hc] = hover, [x0, y0] = center(g, hr, hc);
        for (const [dr, dc] of [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [1, -1], [-1, 1], [-1, -1]]) {
          let k = 1; while (hr + dr * k >= 0 && hr + dr * k < n && hc + dc * k >= 0 && hc + dc * k < n) k++;
          k--; if (!k) continue;
          const [x1, y1] = center(g, hr + dr * k, hc + dc * k);
          mk("line", { x1: x0, y1: y0, x2: x1, y2: y1, class: "bd-ray" }, els.gOver);
        }
      }
    }

    function paint() { if (G.kind === "sudoku") paintSudoku(); else paintQueens(); }

    function checksFor(f) {
      if (G.kind === "sudoku") {
        const n = G.n, total = n * n, grid = f ? f.grid : G.grid;
        const filled = grid.flat().filter(Boolean).length, givens = G.grid.flat().filter(Boolean).length;
        const clashes = findClashes(grid, G.b).length;
        if (!f) return [{ label: `Clues: ${givens} of ${total} cells`, ok: null }, { label: `Clues that clash: ${G.clashes.length}`, ok: G.clashes.length === 0 }];
        const out = [{ label: `Cells filled: ${filled} / ${total}`, ok: filled === total }, { label: `Repeated digits in a row, column or box: ${clashes}`, ok: clashes === 0 }];
        if (f.kind === "dead") out.push({ label: `${rc(f.cell)}: digits left that fit: 0`, ok: false });
        return out;
      }
      const n = G.n, qs = f && f.queens ? f.queens : [];
      if (!f) return [{ label: `Board: ${n} × ${n}`, ok: null }, { label: `Queens to place: ${n}`, ok: null }];
      let bad = 0;
      for (let i = 0; i < qs.length; i++) for (let j = i + 1; j < qs.length; j++) if (qs[i] === qs[j] || Math.abs(qs[i] - qs[j]) === j - i) bad++;
      return [{ label: `Queens placed: ${qs.length} / ${n}`, ok: qs.length === n }, { label: `Pairs that attack each other: ${bad}`, ok: bad === 0 }];
    }

    return {
      load(str, Pk, solverKey) {
        P = Pk; G = parse(str, P);
        frames = SOLVE[G.kind][solverKey || P.solvers[0][0]](G);
        frames.unshift(G.kind === "sudoku"
          ? { kind: "start", grid: G.grid.map(r => r.slice()), caption: G.clashes.length ? `Step 0: the clues. ${rc(G.clashes[0].a)} and ${rc(G.clashes[0].b)} already clash. Step forward to see what the solver does with them.` : "Step 0: the clues. Step forward to watch the solver fill the grid." }
          : { kind: "start", queens: [], caption: "Step 0: an empty board. Step forward to watch the solver place queens." });
        frame = null; hover = null;
        draw(); paint();
        const last = frames.at(-1);
        return { frames, ok: !!last.ok };
      },
      show(i) { frame = i === null ? null : frames[i]; paint(); return frame; },
      setHints(on) { hints = !!on; if (G) paint(); },
      checks(i) { return checksFor(i === null ? null : frames[i]); },
      chosen(i) {
        const f = i === null ? null : frames[i];
        if (!f) return [];
        if (G.kind === "queens") return (f.queens || []).map((c, r) => ({ text: `(${r},${c})` }));
        const filled = f.grid.flat().filter(Boolean).length, givens = G.grid.flat().filter(Boolean).length;
        return [{ text: `${givens} clues` }, { text: `${filled - givens} filled` }, { text: `${G.n * G.n - filled} empty` }];
      },
      // the certificate in Redux's format, or "" when there is none
      certificate() {
        const last = frames.at(-1);
        if (!last.ok) return "";
        return G.kind === "queens" ? certOf(last.queens) : last.grid.map(r => r.join(",")).join(";");
      },
      resetHover() { hover = null; if (G) paint(); },
    };
  }

  return { create, parse, PROBLEMS };
})();
