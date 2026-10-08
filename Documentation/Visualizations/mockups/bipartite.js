/* ---- Bipartite view (Set Cover, Hitting Set, Exact Cover), shared by both mockups ----
   Elements on the left, sets on the right, one edge per membership. Pages supply --av-* tokens. */
const BipartiteView = (() => {
  const NS = "http://www.w3.org/2000/svg";
  const mk = (tag, attrs = {}, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; };
  const ROW = 50, ER = 17;

  const css = `
.bp-svg { width: 100%; height: auto; display: block; }
.bp-svg text { font-family: var(--av-mono); }
.bp-head { font-size: 11px; font-weight: 600; letter-spacing: .08em; fill: var(--av-muted); }
.bp-head .pick { fill: var(--av-hot); }
.bp-node { cursor: pointer; transition: opacity .2s; }
.bp-node:focus { outline: none; }
.bp-node .body { fill: var(--av-bg); stroke: var(--av-stroke); stroke-width: 1.8; transition: fill .2s, stroke .2s; }
.bp-node text { dominant-baseline: central; fill: var(--av-ink); pointer-events: none; }
.bp-node text.lbl { font-size: 13px; font-weight: 600; }
.bp-node text.sub { font-size: 12px; fill: var(--av-muted); }
.bp-node.el text { text-anchor: middle; font-size: 14px; }
.bp-node.Active .body { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; }
.bp-node.Solution .body { fill: var(--av-sol); stroke: var(--av-sol); }
.bp-node.Solution text, .bp-node.Solution text.sub { fill: var(--av-on-sol); }
.bp-node.Covered .body { fill: var(--av-sol-fill); stroke: var(--av-sol); stroke-width: 2.4; }
.bp-node.Rejected .body { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 3; }
.bp-node.Rejected text:not(.bp-count-t) { fill: var(--av-rej); }
.bp-node.Blocked .body { fill: transparent; stroke: var(--av-stroke); stroke-dasharray: 4 3; }
.bp-node.Blocked text { fill: var(--av-muted); }
.bp-node.focus .body { stroke: var(--av-hl); stroke-width: 3; }
.bp-node.trace .body, .bp-node:focus-visible .body { stroke: var(--av-hot); stroke-width: 3; }
.bp-node.faint { filter: grayscale(1); } .bp-node.faint .body { stroke-dasharray: 3 3; }
.bp-count { font-size: 11px; font-weight: 600; text-anchor: middle; dominant-baseline: central; }
.bp-count-bg { fill: var(--av-rej); }
.bp-node text.bp-count-t { fill: var(--av-on-rej); }
.bp-edge { stroke: var(--av-edge); stroke-width: 1.4; transition: opacity .2s, stroke .2s; }
.bp-edge.sol { stroke: var(--av-sol); stroke-width: 2.6; }
.bp-edge.hot { stroke: var(--av-hl); stroke-width: 3; }
.bp-edge.trace { stroke: var(--av-hot); stroke-width: 2.4; }
.bp-edge.dim { stroke: var(--av-edge-dim); }
.bp-edge.faint { stroke: var(--av-edge-dim); stroke-dasharray: 3 3; }
@media (prefers-reduced-motion: reduce) { .bp-node, .bp-node .body, .bp-edge { transition: none; } }`;

  const KINDS = {
    SETCOVER:   { cand: "sets", rule: "atLeast", needK: true },
    EXACTCOVER: { cand: "sets", rule: "exact" },
    HITTINGSET: { cand: "elements", rule: "exact" },
  };

  function parse(str, kind) {
    const s = str.replace(/\s+/g, "");
    const m = /^\(\{([^{}]*)\},\{(.*)\}(?:,(\d+))?\)$/.exec(s);
    if (!m) throw new Error(KINDS[kind].needK ? "Expected ({U},{{S1},{S2},…},K)." : "Expected ({U},{{S1},{S2},…}).");
    if (KINDS[kind].needK && m[3] === undefined) throw new Error("Set Cover needs a budget K at the end, like ({1,2},{{1},{2}},2).");
    if (!KINDS[kind].needK && m[3] !== undefined) throw new Error("This problem doesn't take a K. Remove the trailing number.");
    const U = m[1] ? m[1].split(",") : [];
    if (!U.length || new Set(U).size !== U.length) throw new Error("U must be a non-empty set with no repeats.");
    const re = /\{([^{}]*)\}/g;
    if (m[2].replace(re, "").replace(/,/g, "")) throw new Error("S must be a list of sets, like {{1,2},{3}}.");
    const sets = [];
    let t;
    while ((t = re.exec(m[2]))) {
      const items = t[1] ? t[1].split(",") : [];
      for (const x of items) if (!U.includes(x)) throw new Error(`Element ${x} in S${sets.length + 1} is not in U.`);
      sets.push([...new Set(items)]);
    }
    if (!sets.length) throw new Error("S needs at least one set.");
    if (sets.length > 24 || U.length > 30) throw new Error("This mockup draws up to 30 elements and 24 sets.");
    return { U, sets, K: m[3] === undefined ? null : Number(m[3]), kind };
  }

  const fmtSet = items => "{" + items.join(",") + "}";

  /* Candidates are what the solver picks; constraints are what must be hit. */
  function model(I) {
    const setIds = I.sets.map((_, i) => "S" + (i + 1));
    const sets = I.sets.map((items, i) => ({ id: setIds[i], label: setIds[i], items }));
    const els = I.U.map(x => ({ id: "e:" + x, label: x }));
    const edges = [];
    sets.forEach(st => st.items.forEach(x => edges.push({ el: "e:" + x, set: st.id })));
    const pickSets = KINDS[I.kind].cand === "sets";
    const cands = pickSets ? sets.map(s => s.id) : els.map(e => e.id);
    const cons = pickSets ? els.map(e => e.id) : sets.map(s => s.id);
    const hits = new Map(cands.map(c => [c, new Set()]));
    edges.forEach(e => (pickSets ? hits.get(e.set).add(e.el) : hits.get(e.el).add(e.set)));
    const name = id => (id.startsWith("e:") ? id.slice(2) : id);
    const nice = id => (id.startsWith("e:") ? `element ${id.slice(2)}` : `${id} ${fmtSet(sets.find(s => s.id === id).items)}`);
    return { sets, els, edges, cands, cons, hits, name, nice, pickSets };
  }

  // Exact Cover / exact Hitting Set: Algorithm X (pick the constraint with the fewest options, branch, undo).
  function solveExact(I, M) {
    const frames = [], chosen = [], covered = new Set();
    const noun = M.pickSets ? "set" : "element";
    const fits = p => [...M.hits.get(p)].every(c => !covered.has(c));
    function rec() {
      const open = M.cons.filter(c => !covered.has(c));
      if (!open.length) return true;
      let best = null, bestOpts = null;
      for (const c of open) {
        const opts = M.cands.filter(p => !chosen.includes(p) && M.hits.get(p).has(c) && fits(p));
        if (!bestOpts || opts.length < bestOpts.length) { best = c; bestOpts = opts; }
      }
      if (!bestOpts.length) {
        frames.push({ chosen: [...chosen], failed: best,
          caption: `No ${noun} left can cover ${M.nice(best)} without doubling up somewhere, so undo.` });
        return false;
      }
      for (const p of bestOpts) {
        chosen.push(p); M.hits.get(p).forEach(c => covered.add(c));
        frames.push({ chosen: [...chosen], trying: p, focus: best,
          caption: `${M.nice(best)[0].toUpperCase() + M.nice(best).slice(1)} has the fewest options (${bestOpts.length}). Try ${M.nice(p)}.` });
        if (rec()) return true;
        chosen.pop(); M.hits.get(p).forEach(c => covered.delete(c));
      }
      return false;
    }
    const ok = rec();
    const what = M.pickSets ? "Every element is covered by exactly one chosen set." : "Every set contains exactly one chosen element.";
    frames.push({ chosen: ok ? [...chosen] : [], done: true, ok,
      caption: ok ? `${what} Solution: ${fmtSet(chosen.map(M.name))}.` : `Every branch was undone. No ${M.pickSets ? "exact cover" : "hitting set"} exists.` });
    return { frames, ok, solution: ok ? [...chosen] : null };
  }

  // Set Cover: the greedy solver, then a brute-force check against K when greedy overshoots.
  function solveGreedy(I, M) {
    const frames = [], chosen = [], covered = new Set();
    for (;;) {
      let best = null, gain = 0;
      for (const p of M.cands) {
        if (chosen.includes(p)) continue;
        const g = [...M.hits.get(p)].filter(c => !covered.has(c)).length;
        if (g > gain) { best = p; gain = g; }
      }
      if (!best) break;
      chosen.push(best); M.hits.get(best).forEach(c => covered.add(c));
      frames.push({ chosen: [...chosen], trying: best,
        caption: `Pick ${M.nice(best)}. It covers ${gain} new element${gain === 1 ? "" : "s"}, the most of any set left.` });
    }
    const missing = M.cons.filter(c => !covered.has(c));
    let ok, caption, final = [...chosen];
    if (missing.length) {
      ok = false;
      caption = `${missing.map(M.name).join(", ")} ${missing.length > 1 ? "are" : "is"} in no set, so no cover exists.`;
    } else if (chosen.length <= I.K) {
      ok = true;
      caption = `All of U is covered with ${chosen.length} set${chosen.length > 1 ? "s" : ""}, within K = ${I.K}.`;
    } else {
      ok = false;
      caption = `Greedy's cover uses ${chosen.length} sets, more than K = ${I.K}. Greedy doesn't always find the smallest cover, so switch to the brute-force solver to check.`;
    }
    frames.push({ chosen: final, done: true, ok, missing, caption });
    return { frames, ok, solution: ok ? final : null };
  }

  // Brute force: try every combination, smallest first, and stop at the first one that works.
  const SHOW_TRIES = 120;
  function* combos(n, k, start = 0, acc = []) {
    if (acc.length === k) { yield acc.slice(); return; }
    for (let i = start; i <= n - (k - acc.length); i++) { acc.push(i); yield* combos(n, k, i + 1, acc); acc.pop(); }
  }
  function solveBrute(I, M) {
    const frames = [], n = M.cands.length, exact = KINDS[I.kind].rule === "exact";
    const fmtPick = pick => fmtSet(pick.map(M.name));
    if (n > 16) {
      frames.push({ chosen: [], done: true, ok: false, caption: "This instance has too many candidates for the mockup to brute-force. Use another solver." });
      return { frames, ok: false, solution: null };
    }
    const maxSize = I.kind === "SETCOVER" ? Math.min(I.K, n) : n;
    let tried = 0, found = null;
    outer: for (let size = 1; size <= maxSize; size++) {
      for (const combo of combos(n, size)) {
        const pick = combo.map(i => M.cands[i]);
        const cnt = new Map(M.cons.map(c => [c, 0]));
        pick.forEach(p => M.hits.get(p).forEach(c => cnt.set(c, cnt.get(c) + 1)));
        const missing = M.cons.filter(c => cnt.get(c) === 0);
        const doubled = exact ? M.cons.filter(c => cnt.get(c) > 1) : [];
        const good = !missing.length && !doubled.length;
        tried++;
        if (tried <= SHOW_TRIES) {
          const why = [];
          if (missing.length) why.push(`${missing.map(M.name).join(", ")} ${M.pickSets ? "not covered" : "not hit"}`);
          if (doubled.length) why.push(`${doubled.map(M.name).join(", ")} ${M.pickSets ? "covered" : "hit"} twice`);
          frames.push({ chosen: pick, trial: !good, missing,
            caption: good ? `Try ${fmtPick(pick)}. It works.` : `Try ${fmtPick(pick)}: ${why.join("; ")}.` });
        }
        if (good) { found = pick; break outer; }
      }
    }
    const hidden = tried > SHOW_TRIES ? ` (${tried - SHOW_TRIES} tries not shown)` : "";
    if (found) frames.push({ chosen: found, done: true, ok: true,
      caption: `${fmtPick(found)} is the first combination that works, after ${tried} tries${hidden}. Smaller ones were checked first, so it's also a smallest one.` });
    else frames.push({ chosen: [], done: true, ok: false, missing: M.cons.slice(),
      caption: `Checked all ${tried} combinations${I.kind === "SETCOVER" ? ` of up to K = ${I.K} sets` : ""}${hidden}. None works.` });
    return { frames, ok: !!found, solution: found };
  }
  function create({ svg }) {
    if (!document.getElementById("bp-style")) {
      const st = document.createElement("style"); st.id = "bp-style"; st.textContent = css; document.head.appendChild(st);
    }
    svg.classList.add("bp-svg");
    let I, M, run, nodes, edgeEls, frame = null, hover = null;

    function draw() {
      svg.innerHTML = "";
      // order sets by the average position of their elements, which cuts down crossings
      const pos = new Map(M.els.map((e, i) => [e.id, i]));
      const bary = s => (s.items.length ? s.items.reduce((a, x) => a + pos.get("e:" + x), 0) / s.items.length : 1e9);
      const setOrder = M.sets.slice().sort((a, b) => bary(a) - bary(b));
      const setW = Math.min(260, Math.max(...M.sets.map(s => (s.label.length + fmtSet(s.items).length) * 7.6 + 34), 96));
      const rows = Math.max(M.els.length, M.sets.length);
      const W = Math.max(640, 200 + setW + 300), H = rows * ROW + 80;
      svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
      const ex = 90, sx = W - 50 - setW;
      const top = (count) => 70 + ((rows - count) * ROW) / 2 + ROW / 2;
      const P = new Map();
      M.els.forEach((e, i) => P.set(e.id, { x: ex, y: top(M.els.length) + i * ROW }));
      setOrder.forEach((s, i) => P.set(s.id, { x: sx, y: top(M.sets.length) + i * ROW }));

      const head = (x, text, pick, anchor) => {
        const t = mk("text", { x, y: 28, class: "bp-head", "text-anchor": anchor }, svg);
        t.textContent = text;
        if (pick) { const ts = mk("tspan", { class: "pick" }, t); ts.textContent = "  ● PICKED"; }
      };
      head(ex - ER, "ELEMENTS · U", !M.pickSets, "start");
      head(sx, "SETS · S", M.pickSets, "start");

      const gE = mk("g", {}, svg), gN = mk("g", {}, svg);
      edgeEls = M.edges.map(e => {
        const a = P.get(e.el), b = P.get(e.set);
        return { ...e, el2: mk("line", { x1: a.x + ER, y1: a.y, x2: b.x, y2: b.y, class: "bp-edge" }, gE) };
      });
      nodes = new Map();
      const hookup = (g, id) => {
        const on = () => { hover = id; paint(); }, off = () => { hover = null; paint(); };
        g.addEventListener("pointerenter", on); g.addEventListener("pointerleave", off);
        g.addEventListener("focus", on); g.addEventListener("blur", off);
        g.addEventListener("click", () => { hover = hover === id ? null : id; paint(); });
      };
      M.els.forEach(e => {
        const p = P.get(e.id);
        const g = mk("g", { class: "bp-node el", tabindex: "0", role: "button", "aria-label": `Element ${e.label}` }, gN);
        mk("circle", { cx: p.x, cy: p.y, r: ER, class: "body" }, g);
        mk("text", { x: p.x, y: p.y }, g).textContent = e.label;
        const badge = mk("g", { visibility: "hidden" }, g);
        mk("circle", { cx: p.x - ER - 4, cy: p.y - ER + 4, r: 8, class: "bp-count-bg" }, badge);
        const bt = mk("text", { x: p.x - ER - 4, y: p.y - ER + 4, class: "bp-count bp-count-t" }, badge);
        nodes.set(e.id, { g, badge, bt }); hookup(g, e.id);
      });
      M.sets.forEach(s => {
        const p = P.get(s.id);
        const g = mk("g", { class: "bp-node set", tabindex: "0", role: "button", "aria-label": `Set ${s.label} ${fmtSet(s.items)}` }, gN);
        mk("rect", { x: p.x, y: p.y - 17, width: setW, height: 34, rx: 8, class: "body" }, g);
        mk("text", { x: p.x + 12, y: p.y, class: "lbl" }, g).textContent = s.label;
        mk("text", { x: p.x + 14 + s.label.length * 8.5, y: p.y, class: "sub" }, g).textContent = fmtSet(s.items);
        const badge = mk("g", { visibility: "hidden" }, g);
        mk("circle", { cx: p.x + setW + 2, cy: p.y - 15, r: 8, class: "bp-count-bg" }, badge);
        const bt = mk("text", { x: p.x + setW + 2, y: p.y - 15, class: "bp-count bp-count-t" }, badge);
        nodes.set(s.id, { g, badge, bt }); hookup(g, s.id);
      });
    }

    function paint() {
      const f = frame, exact = KINDS[I.kind].rule === "exact";
      const chosen = new Set(f ? f.chosen : []);
      const hitCount = new Map(M.cons.map(c => [c, 0]));
      chosen.forEach(p => M.hits.get(p).forEach(c => hitCount.set(c, hitCount.get(c) + 1)));
      const covered = new Set([...hitCount].filter(([, n]) => n > 0).map(([c]) => c));
      const neighbors = new Set();
      if (hover) M.edges.forEach(e => { if (e.el === hover) neighbors.add(e.set); if (e.set === hover) neighbors.add(e.el); });

      for (const [id, n] of nodes) {
        let st = "Background";
        const isCand = M.hits.has(id);
        if (f) {
          if (isCand) {
            if ((id === f.trying || (f.trial && chosen.has(id))) && !f.done) st = "Active";
            else if (chosen.has(id)) st = "Solution";
            else if (exact && !f.trial && [...M.hits.get(id)].some(c => covered.has(c))) st = "Blocked";
          } else {
            const k = hitCount.get(id);
            if (id === f.failed || (exact && k > 1) || (f.missing && f.missing.includes(id) && (f.trial || f.done))) st = "Rejected";
            else if (k > 0) st = "Covered";
          }
        }
        let cls = `bp-node ${M.els.some(e => e.id === id) ? "el" : "set"} ${st}`;
        if (f && id === f.focus && !f.done) cls += " focus";
        if (hover) cls += id === hover ? " trace" : neighbors.has(id) ? "" : " faint";
        n.g.setAttribute("class", cls);
        const k = hitCount.get(id);
        const showBadge = f && !isCand && k > 1;
        n.badge.setAttribute("visibility", showBadge ? "visible" : "hidden");
        if (showBadge) n.bt.textContent = "×" + k;
      }
      edgeEls.forEach(e => {
        const p = M.pickSets ? e.set : e.el;
        let cls = "bp-edge";
        if (hover) cls += e.el === hover || e.set === hover ? " trace" : " faint";
        else if (f && !f.done && (p === f.trying || (f.trial && chosen.has(p)))) cls += " hot";
        else if (f && chosen.has(p)) cls += " sol";
        else if (f) cls += " dim";
        e.el2.setAttribute("class", cls);
      });
    }

    // Plain-language checks for the side panel.
    function checks(f) {
      const chosen = new Set(f ? f.chosen : []);
      const hitCount = new Map(M.cons.map(c => [c, 0]));
      chosen.forEach(p => M.hits.get(p).forEach(c => hitCount.set(c, hitCount.get(c) + 1)));
      const once = [...hitCount.values()].filter(n => n === 1).length, any = [...hitCount.values()].filter(n => n > 0).length;
      const total = M.cons.length;
      if (I.kind === "SETCOVER") return [
        { label: `Elements covered: ${any} / ${total}`, ok: any === total },
        { label: `Sets used: ${chosen.size} (K = ${I.K})`, ok: chosen.size <= I.K },
      ];
      if (I.kind === "EXACTCOVER") return [{ label: `Elements covered exactly once: ${once} / ${total}`, ok: once === total }];
      return [{ label: `Sets hit exactly once: ${once} / ${total}`, ok: once === total }];
    }

    return {
      // solver: "greedy" (Set Cover), "x" (Algorithm X, exact rules), or "brute"
      load(str, kind, solver) {
        I = parse(str, kind); M = model(I);
        const s = solver || (KINDS[kind].rule === "exact" ? "x" : "greedy");
        if (s === "brute") run = solveBrute(I, M);
        else if (s === "x" && KINDS[kind].rule === "exact") run = solveExact(I, M);
        else run = solveGreedy(I, M);
        run.frames.unshift({ chosen: [], caption: KINDS[kind].rule === "exact"
          ? "Step 0: nothing chosen yet. Step forward to watch the solver."
          : "Step 0: nothing chosen yet. Step forward to watch the solver." });
        frame = null; hover = null;
        draw(); paint();
        return { frames: run.frames, ok: run.ok, solution: run.solution ? run.solution.map(M.name) : null, K: I.K };
      },
      show(i) { frame = i === null ? null : run.frames[i]; paint(); return frame; },
      checks(i) { return checks(i === null ? null : run.frames[i]); },
      chosenLabels(i) { return i === null ? [] : run.frames[i].chosen.map(M.name); },
      resetHover() { hover = null; paint(); },
    };
  }

  /* ---- SAT factor graph: variables left, clauses right, solid edge = x, dashed edge = ¬x ----
     Frames are the same {asg, tried, failed, done, ok} frames the clause graph uses. */
  const satCss = `
.bp-edge.neg { stroke-dasharray: 5 4; }
.bp-node.False .body { fill: var(--av-surface); stroke: var(--av-stroke); }
.bp-node.False text { fill: var(--av-muted); }
.bp-val { font-size: 12px; font-weight: 600; text-anchor: end; dominant-baseline: central; fill: var(--av-muted); }
.bp-val.T { fill: var(--av-sol); }`;
  const SUBS = "₀₁₂₃₄₅₆₇₈₉";
  const pretty = nm => { const m = /^([A-Za-z_]+)(\d+)$/.exec(nm); return m ? m[1] + m[2].split("").map(d => SUBS[d]).join("") : nm; };

  function createSat({ svg }) {
    if (!document.getElementById("bp-style")) {
      const st = document.createElement("style"); st.id = "bp-style"; st.textContent = css; document.head.appendChild(st);
    }
    if (!document.getElementById("bp-sat-style")) {
      const st = document.createElement("style"); st.id = "bp-sat-style"; st.textContent = satCss; document.head.appendChild(st);
    }
    svg.classList.add("bp-svg");
    let C, names, varEls, clauseEls, edgeEls, frame = null, hover = null;

    function draw() {
      svg.innerHTML = "";
      const n = names.length, m = C.length;
      const litText = c => c.map(l => (l < 0 ? "¬" : "") + pretty(names[Math.abs(l) - 1])).join(" ∨ ");
      const bary = c => c.reduce((a, l) => a + Math.abs(l), 0) / c.length;
      const order = C.map((c, j) => j).sort((a, b) => bary(C[a]) - bary(C[b]));
      const setW = Math.min(300, Math.max(...C.map((c, j) => (("C" + (j + 1)).length + litText(c).length) * 7.6 + 40), 120));
      const rows = Math.max(n, m), W = Math.max(640, 220 + setW + 280), H = rows * ROW + 80;
      svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
      const vx = 110, cx = W - 50 - setW;
      const top = count => 70 + ((rows - count) * ROW) / 2 + ROW / 2;
      const vy = i => top(n) + i * ROW;
      const cy = new Map(order.map((j, r) => [j, top(m) + r * ROW]));
      const head = (x, text, pick) => {
        const t = mk("text", { x, y: 28, class: "bp-head" }, svg); t.textContent = text;
        if (pick) { const ts = mk("tspan", { class: "pick" }, t); ts.textContent = "  ● ASSIGNED"; }
      };
      head(vx - ER - 22, "VARIABLES", true);
      head(cx, "CLAUSES", false);
      const gE = mk("g", {}, svg), gN = mk("g", {}, svg);
      edgeEls = [];
      C.forEach((c, j) => c.forEach(l => {
        const i = Math.abs(l) - 1;
        edgeEls.push({ v: i + 1, j, lit: l, el: mk("line", { x1: vx + ER, y1: vy(i), x2: cx, y2: cy.get(j), class: "bp-edge" + (l < 0 ? " neg" : "") }, gE) });
      }));
      const hookup = (g, key) => {
        const on = () => { hover = key; paint(); }, off = () => { hover = null; paint(); };
        g.addEventListener("pointerenter", on); g.addEventListener("pointerleave", off);
        g.addEventListener("focus", on); g.addEventListener("blur", off);
        g.addEventListener("click", () => { hover = hover === key ? null : key; paint(); });
      };
      varEls = names.map((nm, i) => {
        const g = mk("g", { class: "bp-node el", tabindex: "0", role: "button", "aria-label": `Variable ${nm}` }, gN);
        mk("circle", { cx: vx, cy: vy(i), r: ER, class: "body" }, g);
        mk("text", { x: vx, y: vy(i) }, g).textContent = pretty(nm);
        const val = mk("text", { x: vx - ER - 8, y: vy(i), class: "bp-val" }, gN);
        hookup(g, "v" + (i + 1));
        return { g, val };
      });
      clauseEls = C.map((c, j) => {
        const y = cy.get(j);
        const g = mk("g", { class: "bp-node set", tabindex: "0", role: "button", "aria-label": `Clause ${j + 1}: ${litText(c)}` }, gN);
        mk("rect", { x: cx, y: y - 17, width: setW, height: 34, rx: 8, class: "body" }, g);
        mk("text", { x: cx + 12, y, class: "lbl" }, g).textContent = "C" + (j + 1);
        mk("text", { x: cx + 18 + ("C" + (j + 1)).length * 8.5, y, class: "sub" }, g).textContent = litText(c);
        hookup(g, "c" + j);
        return g;
      });
    }

    function paint() {
      const f = frame, asg = f ? f.asg : {};
      const val = l => { const a = asg[Math.abs(l)]; return a === undefined ? undefined : (l > 0 ? a : !a); };
      const hv = hover && hover[0] === "v" ? +hover.slice(1) : null, hc = hover && hover[0] === "c" ? +hover.slice(1) : null;
      const nearV = new Set(), nearC = new Set();
      if (hv) C.forEach((c, j) => { if (c.some(l => Math.abs(l) === hv)) nearC.add(j); });
      if (hc !== null) C[hc].forEach(l => nearV.add(Math.abs(l)));
      varEls.forEach((o, i) => {
        const v = i + 1, a = asg[v];
        let st = a === undefined ? "Background" : a ? "Solution" : "False";
        if (f && f.tried === v && !f.done) st = "Active";
        let cls = "bp-node el " + st;
        if (hover) cls += hv === v ? " trace" : nearV.has(v) ? "" : " faint";
        o.g.setAttribute("class", cls);
        o.val.textContent = a === undefined ? "" : a ? "T" : "F";
        o.val.setAttribute("class", "bp-val" + (a ? " T" : ""));
      });
      clauseEls.forEach((g, j) => {
        let st = "Background";
        if (f && j === f.failed) st = "Rejected";
        else if (C[j].some(l => val(l) === true)) st = "Covered";
        let cls = "bp-node set " + st;
        if (hover) cls += hc === j ? " trace" : nearC.has(j) ? "" : " faint";
        g.setAttribute("class", cls);
      });
      edgeEls.forEach(e => {
        let cls = "bp-edge" + (e.lit < 0 ? " neg" : "");
        if (hover) cls += hv === e.v || hc === e.j ? " trace" : " faint";
        else if (f && f.tried === e.v && !f.done) cls += " hot";
        else if (val(e.lit) === true) cls += " sol";
        else if (val(e.lit) === false) cls += " dim";
        e.el.setAttribute("class", cls);
      });
    }

    return {
      load(clauses, varNames) { C = clauses; names = varNames; frame = null; hover = null; draw(); paint(); },
      show(f) { frame = f || null; paint(); },
      resetHover() { hover = null; paint(); },
    };
  }

  return { create, createSat, parse };
})();
