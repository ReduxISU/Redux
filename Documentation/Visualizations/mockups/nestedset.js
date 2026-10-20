/* ---- Nested Set view (Set Cover, Exact Cover, Exact Hitting Set), shared by both mockups ----
   Every element of U is a labelled dot on a circle; every set is a padded, rounded region around its members (an Euler-style
   hull), with its name on its edge. When the regions can't be drawn cleanly, the picture falls back to the set list: one
   row per set, its members written out as chips (the old Redux_GUI nested-set picture).
   Ids and frames are the Bipartite Graph's (element e:{x}, set S{i+1}, hits for ×N badges), so both pictures color together.
   Pages supply --av-* tokens. */
const NestedSetView = (() => {
  const NS = "http://www.w3.org/2000/svg";
  const mk = (tag, attrs = {}, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; };
  const DOT = 16;

  const css = `
.ns-svg { width: 100%; height: auto; display: block; }
.ns-svg text { font-family: var(--av-mono); }
.ns-head { font-size: 11px; font-weight: 600; letter-spacing: .08em; fill: var(--av-muted); }
.ns-head .pick { fill: var(--av-hot); }
.ns-region { cursor: pointer; }
.ns-region .fill { fill: transparent; stroke: none; }
.ns-region .edge { fill: none; stroke: var(--av-edge); stroke-width: 1.8; transition: stroke .2s; }
.ns-region.Solution .fill, .ns-region.Covered .fill { fill: var(--av-sol-fill); }
.ns-region.Solution .edge { stroke: var(--av-sol); stroke-width: 3.2; }
.ns-region.Covered .edge { stroke: var(--av-sol); stroke-width: 2.4; }
.ns-region.Committed .fill { fill: var(--av-com-fill); }
.ns-region.Committed .edge { stroke: var(--av-com); stroke-width: 2.8; stroke-dasharray: 7 4; }
.ns-region.ElementHighlight .fill { fill: var(--av-hl-fill); }
.ns-region.ElementHighlight .edge { stroke: var(--av-hl); stroke-width: 3.2; }
.ns-region.Rejected .edge { stroke: var(--av-rej); stroke-width: 3.2; }
.ns-region.Blocked .edge { stroke: var(--av-edge); stroke-dasharray: 4 4; }
.ns-region.trace .edge { stroke: var(--av-hot); stroke-width: 3; stroke-dasharray: none; }
.ns-region.faint { filter: grayscale(1); } .ns-region.faint .edge { stroke: var(--av-edge-dim); stroke-dasharray: 3 3; }
.ns-tag { cursor: pointer; }
.ns-tag:focus { outline: none; }
.ns-tag rect { fill: var(--av-surface); stroke: var(--av-edge); stroke-width: 1.4; }
.ns-tag text { font-size: 12.5px; font-weight: 600; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); pointer-events: none; }
.ns-tag text.sub { font-size: 11.5px; font-weight: 400; fill: var(--av-muted); text-anchor: start; }
.ns-tag.Solution rect { fill: var(--av-sol); stroke: var(--av-sol); } .ns-tag.Solution text { fill: var(--av-on-sol); }
.ns-tag.Committed rect { fill: var(--av-com-fill); stroke: var(--av-com); stroke-width: 2.2; stroke-dasharray: 5 3; } .ns-tag.Committed text { fill: var(--av-com-ink); }
.ns-tag.Covered rect { fill: var(--av-sol-fill); stroke: var(--av-sol); } .ns-tag.Covered text { fill: var(--av-sol-ink); }
.ns-tag.ElementHighlight rect { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; }
.ns-tag.Rejected rect { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 2.6; } .ns-tag.Rejected text { fill: var(--av-rej-ink); }
.ns-tag.Blocked rect { fill: var(--av-surface); stroke: var(--av-edge); stroke-dasharray: 4 3; } .ns-tag.Blocked text { fill: var(--av-muted); }
.ns-tag.trace rect, .ns-tag:focus-visible rect { stroke: var(--av-hot); stroke-width: 3; stroke-dasharray: none; }
.ns-tag.faint { filter: grayscale(1); } .ns-tag.faint rect { stroke-dasharray: 3 3; }
.ns-dot { cursor: pointer; }
.ns-dot:focus { outline: none; }
.ns-dot .body { fill: var(--av-bg); stroke: var(--av-edge); stroke-width: 1.8; transition: fill .2s, stroke .2s; }
.ns-dot text { font-size: 13.5px; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); pointer-events: none; }
.ns-dot.Covered .body { fill: var(--av-sol-fill); stroke: var(--av-sol); stroke-width: 2.4; } .ns-dot.Covered text { fill: var(--av-sol-ink); font-weight: 600; }
.ns-dot.Solution .body { fill: var(--av-sol); stroke: var(--av-sol); } .ns-dot.Solution text { fill: var(--av-on-sol); font-weight: 600; }
.ns-dot.Committed .body { fill: var(--av-com-fill); stroke: var(--av-com); stroke-width: 2.4; stroke-dasharray: 5 3; } .ns-dot.Committed text { fill: var(--av-com-ink); font-weight: 600; }
.ns-dot.ElementHighlight .body { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; }
.ns-dot.Rejected .body { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 3; } .ns-dot.Rejected text:not(.ns-count-t) { fill: var(--av-rej-ink); font-weight: 600; }
.ns-dot.Blocked .body { fill: var(--av-surface); stroke: var(--av-edge); stroke-dasharray: 4 3; } .ns-dot.Blocked text { fill: var(--av-muted); }
.ns-dot.trace .body, .ns-dot:focus-visible .body { stroke: var(--av-hot); stroke-width: 3; stroke-dasharray: none; }
.ns-dot.faint { filter: grayscale(1); } .ns-dot.faint .body { stroke-dasharray: 3 3; }
.ns-count-bg { fill: var(--av-rej); }
.ns-svg text.ns-count-t { font-size: 11px; font-weight: 700; text-anchor: middle; dominant-baseline: central; fill: var(--av-on-rej); }
.ns-row .box { fill: var(--av-bg); stroke: var(--av-edge); stroke-width: 1.6; }
.ns-row.Solution .box { fill: var(--av-sol-fill); stroke: var(--av-sol); stroke-width: 3; }
.ns-row.Covered .box { fill: var(--av-sol-fill); stroke: var(--av-sol); stroke-width: 2.4; }
.ns-row.Committed .box { fill: var(--av-com-fill); stroke: var(--av-com); stroke-width: 2.6; stroke-dasharray: 7 4; }
.ns-row.ElementHighlight .box { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; }
.ns-row.Rejected .box { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 3; }
.ns-row.Blocked .box { fill: var(--av-surface); stroke: var(--av-edge); stroke-dasharray: 4 4; }
.ns-row.trace .box { stroke: var(--av-hot); stroke-width: 3; stroke-dasharray: none; }
.ns-row.faint { filter: grayscale(1); } .ns-row.faint .box { stroke-dasharray: 3 3; }
.ns-brace { font-size: 20px; fill: var(--av-ink); dominant-baseline: central; pointer-events: none; }
@media (prefers-reduced-motion: reduce) { .ns-dot .body, .ns-region .edge { transition: none; } }`;

  const KINDS = {
    SETCOVER:   { cand: "sets", rule: "atLeastOnce", needK: true },
    EXACTCOVER: { cand: "sets", rule: "exactlyOnce" },
    HITTINGSET: { cand: "elements", rule: "exactlyOnce" },
  };

  /* ---------- the instance (the same format and ids as bipartite.js) ---------- */

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
  const escapeName = n => n.replace(/[\\~:]/g, c => "\\" + c);
  const elId = x => "e:" + escapeName(x);
  const setId = i => "S" + (i + 1);

  /** The payload: the Bipartite Graph's elements and sets, without its edges. */
  function payload(I) {
    const K = KINDS[I.kind];
    return { kind: "nestedSet", layout: "regions", pickSide: K.cand, rule: K.rule, ...(I.K !== null ? { k: I.K } : {}),
      elements: I.U.map(x => ({ id: elId(x), label: x })),
      sets: I.sets.map((items, i) => ({ id: setId(i), label: setId(i), members: items.map(elId) })) };
  }
  function model(P) {
    const pickSets = P.pickSide === "sets";
    const hits = new Map([...P.elements.map(e => [e.id, []]), ...P.sets.map(s => [s.id, []])]);
    P.sets.forEach(s => s.members.forEach(e => { hits.get(s.id).push(e); hits.get(e).push(s.id); }));
    return { P, pickSets, picks: pickSets ? P.sets.map(s => s.id) : P.elements.map(e => e.id), cons: pickSets ? P.elements.map(e => e.id) : P.sets.map(s => s.id), hits, exact: P.rule === "exactlyOnce" };
  }

  /* ---------- frames: BipartiteFrames.Frame without the edges ---------- */

  const RANK = ["Background", "False", "Blocked", "Covered", "Committed", "Solution", "ElementHighlight", "Rejected"];
  const raise = (map, id, st) => { if (RANK.indexOf(st) > RANK.indexOf(map[id] || "Background")) map[id] = st; };
  const pickedState = (ev, incremental, endsRun, ok) =>
    ev === "Done" ? (ok ? "Solution" : "Rejected") : ev === "Accept" && endsRun ? "Solution" : (ev === "Try" || ev === "Reject") && !incremental ? "ElementHighlight" : "Committed";

  /** One step ({ event, picks: [ids], focus, caption, ok? }) as the frame the backend would send. */
  function frame(M, step, { incremental = false, endsRun = false, index = null } = {}) {
    const chosen = new Set((step.picks || []).filter(p => M.picks.includes(p)));
    const count = new Map(M.cons.map(c => [c, 0]));
    chosen.forEach(p => M.hits.get(p).forEach(c => count.set(c, count.get(c) + 1)));
    const nodes = {}, pick = pickedState(step.event, incremental, endsRun, step.ok === true);
    chosen.forEach(p => raise(nodes, p, pick));
    const judged = chosen.size > 0 && (step.event === "Done" || (!incremental && (step.event === "Try" || step.event === "Reject")));
    M.cons.forEach(c => {
      const k = count.get(c);
      if (M.exact && k > 1) raise(nodes, c, "Rejected");
      else if (k > 0) raise(nodes, c, "Covered");
      else if (judged) raise(nodes, c, "Rejected");
    });
    if (M.exact && pick !== "ElementHighlight")
      M.picks.forEach(p => { if (!chosen.has(p) && M.hits.get(p).some(c => count.get(c) > 0)) raise(nodes, p, "Blocked"); });
    const mark = incremental && step.event === "Reject" ? "Rejected" : "ElementHighlight";
    (step.focus || []).forEach(id => { if (M.hits.has(id)) raise(nodes, id, mark); });
    const hits = {};
    count.forEach((k, c) => { if (k > 0) hits[c] = k; });
    const out = { event: step.event, caption: step.caption, focus: step.focus || [], step: index, nodes, hits, picks: [...chosen] };
    if (step.ok !== undefined) out.ok = step.ok;
    return out;
  }
  const build = (M, run) => run.steps.map((s, k) => {
    if (run.stopped && s.event === "Done") return { event: "Done", caption: s.caption, focus: [], step: k, nodes: {}, hits: {}, picks: [], ok: false };
    const next = run.steps[k + 1];
    return frame(M, s, { incremental: run.incremental, endsRun: s.event === "Accept" && next && next.event === "Done" && next.ok === true, index: k });
  });

  /* ---------- reference solvers (test oracles; the backend records the real steps) ---------- */

  const name = id => (id.startsWith("e:") ? id.slice(2) : id);
  const fmt = ids => "{" + ids.map(name).join(",") + "}";
  const nice = (M, id) => (id.startsWith("e:") ? `element ${name(id)}` : `${id} ${fmt(M.hits.get(id))}`);

  // Set Cover: greedy, incremental (each pick is kept; the next is tried).
  function solveGreedy(M, K) {
    const steps = [], chosen = [], covered = new Set();
    for (;;) {
      let best = null, gain = 0;
      M.picks.forEach(p => { if (chosen.includes(p)) return; const g = M.hits.get(p).filter(c => !covered.has(c)).length; if (g > gain) { best = p; gain = g; } });
      if (!best) break;
      steps.push({ event: "Try", picks: [...chosen, best], focus: [best], caption: `Try ${nice(M, best)}: it covers ${gain} new element${gain === 1 ? "" : "s"}, the most of any set left.` });
      chosen.push(best); M.hits.get(best).forEach(c => covered.add(c));
      steps.push({ event: "Accept", picks: [...chosen], focus: [], caption: `Keep ${best}. Covered so far: ${covered.size} of ${M.cons.length}.` });
    }
    const missing = M.cons.filter(c => !covered.has(c));
    let ok, caption;
    if (missing.length) { ok = false; caption = `${missing.map(name).join(", ")} ${missing.length > 1 ? "are" : "is"} in no set, so no cover exists. Greedy's picks are drawn red.`; }
    else if (chosen.length <= K) { ok = true; caption = `All of U is covered with ${chosen.length} set${chosen.length > 1 ? "s" : ""}, within K = ${K}.`; }
    else { ok = false; caption = `Greedy's cover uses ${chosen.length} sets, more than K = ${K}. Greedy doesn't always find the smallest cover; try the brute-force solver.`; }
    steps.push({ event: "Done", picks: [...chosen], focus: [], ok, caption });
    return { steps, incremental: true, ok, solution: ok ? chosen : null };
  }

  // Exact Cover / Exact Hitting Set: Algorithm X, incremental (pick the constraint with the fewest options, branch, undo).
  function solveX(M) {
    const steps = [], chosen = [], covered = new Set(), noun = M.pickSets ? "set" : "element";
    const fits = p => M.hits.get(p).every(c => !covered.has(c));
    function rec() {
      const open = M.cons.filter(c => !covered.has(c));
      if (!open.length) return true;
      let best = null, opts = null;
      for (const c of open) { const o = M.picks.filter(p => !chosen.includes(p) && M.hits.get(p).includes(c) && fits(p)); if (!opts || o.length < opts.length) { best = c; opts = o; } }
      if (!opts.length) { steps.push({ event: "Reject", picks: [...chosen], focus: [best], caption: `No ${noun} left can take ${nice(M, best)} without doubling up somewhere, so undo.` }); return false; }
      for (const p of opts) {
        chosen.push(p); M.hits.get(p).forEach(c => covered.add(c));
        steps.push({ event: "Try", picks: [...chosen], focus: [p], caption: `${nice(M, best).replace(/^./, ch => ch.toUpperCase())} has the fewest options (${opts.length}). Try ${nice(M, p)}.` });
        if (rec()) return true;
        chosen.pop(); M.hits.get(p).forEach(c => covered.delete(c));
        steps.push({ event: "Backtrack", picks: [...chosen], focus: [p], caption: `Take ${name(p)} back out.` });
      }
      return false;
    }
    const ok = rec();
    const what = M.pickSets ? "Every element is covered by exactly one chosen set." : "Every set contains exactly one chosen element.";
    steps.push({ event: "Done", picks: ok ? [...chosen] : [], focus: [], ok, caption: ok ? `${what} Solution: ${fmt(chosen)}.` : `Every branch was undone. No ${M.pickSets ? "exact cover" : "hitting set"} exists.` });
    return { steps, incremental: true, ok, solution: ok ? [...chosen] : null };
  }

  // Brute force: every combination, smallest first. Not incremental, so each candidate is only highlighted.
  const SHOW_TRIES = 120;
  function* combos(n, k, start = 0, acc = []) { if (acc.length === k) { yield acc.slice(); return; } for (let i = start; i <= n - (k - acc.length); i++) { acc.push(i); yield* combos(n, k, i + 1, acc); acc.pop(); } }
  function solveBrute(M, K) {
    const steps = [], n = M.picks.length;
    if (n > 16) return { steps: [{ event: "Done", picks: [], focus: [], ok: false, caption: "Too many candidates for the mockup to brute-force. Use another solver." }], incremental: false, ok: false, solution: null };
    let tried = 0, found = null;
    outer: for (let size = 1; size <= (K === null ? n : Math.min(K, n)); size++) for (const c of combos(n, size)) {
      const pick = c.map(i => M.picks[i]), cnt = new Map(M.cons.map(x => [x, 0]));
      pick.forEach(p => M.hits.get(p).forEach(x => cnt.set(x, cnt.get(x) + 1)));
      const missing = M.cons.filter(x => cnt.get(x) === 0), doubled = M.exact ? M.cons.filter(x => cnt.get(x) > 1) : [];
      const good = !missing.length && !doubled.length;
      tried++;
      if (tried <= SHOW_TRIES || good) {
        const why = [];
        if (missing.length) why.push(`${missing.map(name).join(", ")} ${M.pickSets ? "not covered" : "not hit"}`);
        if (doubled.length) why.push(`${doubled.map(name).join(", ")} ${M.pickSets ? "covered" : "hit"} twice`);
        steps.push(good ? { event: "Accept", picks: pick, focus: [], caption: `Try ${fmt(pick)}. It works.` } : { event: "Reject", picks: pick, focus: [], caption: `Try ${fmt(pick)}: ${why.join("; ")}.` });
      }
      if (good) { found = pick; break outer; }
    }
    const hidden = tried > SHOW_TRIES ? ` (${tried - SHOW_TRIES} tries not shown)` : "";
    steps.push(found ? { event: "Done", picks: found, focus: [], ok: true, caption: `${fmt(found)} is the first combination that works, after ${tried} tries${hidden}.` }
      : { event: "Done", picks: [], focus: [], ok: false, caption: `Checked all ${tried} combinations${K !== null ? ` of up to K = ${K} sets` : ""}${hidden}. None works.` });
    return { steps, incremental: false, ok: !!found, solution: found };
  }
  const SOLVERS = {
    greedy: { label: "Greedy Set Cover", run: (M, I) => solveGreedy(M, I.K) },
    x: { label: "Algorithm X", run: M => solveX(M) },
    brute: { label: "Brute Force", run: (M, I) => solveBrute(M, I.K) },
  };

  /* ---------- layout: regions on a circle, or the set list ---------- */

  const LIMITS = { elements: 12, sets: 8, perElement: 4, clearance: 4 };
  const PAD0 = DOT + 9, PADSTEP = 9;

  // Order the elements round the circle so each set's members sit together: every order is tried for up to 9 elements
  // (the first stays put), a barycentre sweep beyond that. Ties keep the first order found, so the layout is deterministic.
  function order(P) {
    const n = P.elements.length, idx = new Map(P.elements.map((e, i) => [e.id, i]));
    const sets = P.sets.map(s => s.members.map(m => idx.get(m)));
    const cost = perm => {
      const at = new Array(n); perm.forEach((e, p) => (at[e] = p));
      let c = 0;
      for (const s of sets) {
        if (s.length < 2) continue;
        const on = new Array(n).fill(false); s.forEach(e => (on[at[e]] = true));
        let runs = 0; for (let p = 0; p < n; p++) if (on[p] && !on[(p + n - 1) % n]) runs++;
        c += (Math.max(1, runs) - 1) * 1000;
        const ps = s.map(e => at[e]).sort((a, b) => a - b);
        let gap = 0; for (let k = 0; k < ps.length; k++) gap = Math.max(gap, ((ps[(k + 1) % ps.length] - ps[k]) + n) % n || n);
        c += n - gap;
      }
      return c;
    };
    if (n <= 9) {
      let best = null, bestC = Infinity;
      const rest = P.elements.map((_, i) => i).slice(1), perm = [0], used = new Array(n).fill(false);
      const rec = () => {
        if (perm.length === n) { const c = cost(perm); if (c < bestC) { bestC = c; best = perm.slice(); } return; }
        for (const e of rest) if (!used[e]) { used[e] = true; perm.push(e); rec(); perm.pop(); used[e] = false; }
      };
      rec();
      return best;
    }
    let perm = P.elements.map((_, i) => i);
    for (let it = 0; it < 4; it++) {
      const at = new Map(perm.map((e, p) => [e, p]));
      const bc = e => { const ss = sets.filter(s => s.includes(e)); return ss.length ? ss.reduce((a, s) => a + s.reduce((b, x) => b + at.get(x), 0) / s.length, 0) / ss.length : at.get(e); };
      perm = perm.slice().sort((a, b) => bc(a) - bc(b) || a - b);
    }
    return perm;
  }

  // Padded hull: the convex hull of the member dots grown by `pad`, as straight edges joined by circular arcs.
  function hull(points) {
    const pts = points.slice().sort((a, b) => a.x - b.x || a.y - b.y);
    if (pts.length < 3) return pts;
    const cross = (o, a, b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
    const lo = [], up = [];
    for (const p of pts) { while (lo.length >= 2 && cross(lo.at(-2), lo.at(-1), p) <= 0) lo.pop(); lo.push(p); }
    for (const p of pts.slice().reverse()) { while (up.length >= 2 && cross(up.at(-2), up.at(-1), p) <= 0) up.pop(); up.push(p); }
    return lo.slice(0, -1).concat(up.slice(0, -1));
  }
  function regionPath(points, pad) {
    if (points.length === 1) { const p = points[0]; return `M${p.x - pad},${p.y} a${pad},${pad} 0 1,0 ${2 * pad},0 a${pad},${pad} 0 1,0 ${-2 * pad},0Z`; }
    let h = hull(points);
    // clockwise on screen (y down), so every corner arc turns the same way (sweep 1) and normals point out
    const area = h.reduce((a, p, i) => { const q = h[(i + 1) % h.length]; return a + p.x * q.y - q.x * p.y; }, 0);
    if (h.length > 2 && area < 0) h = h.slice().reverse();
    const m = h.length, seg = [];
    for (let i = 0; i < m; i++) {
      const a = h[i], b = h[(i + 1) % m], dx = b.x - a.x, dy = b.y - a.y, L = Math.hypot(dx, dy) || 1, nx = dy / L, ny = -dx / L;
      seg.push({ a: { x: a.x + pad * nx, y: a.y + pad * ny }, b: { x: b.x + pad * nx, y: b.y + pad * ny } });
    }
    const f = v => v.toFixed(1);
    let d = `M${f(seg[0].a.x)},${f(seg[0].a.y)}`;
    for (let i = 0; i < m; i++) { const s = seg[i], nx = seg[(i + 1) % m].a; d += ` L${f(s.b.x)},${f(s.b.y)} A${pad},${pad} 0 0,1 ${f(nx.x)},${f(nx.y)}`; }
    return d + "Z";
  }
  // Distance from point p to the convex hull of `points` (0 inside).
  function distToHull(p, points) {
    const h = hull(points);
    const dSeg = (a, b) => { const dx = b.x - a.x, dy = b.y - a.y, L2 = dx * dx + dy * dy || 1, t = Math.max(0, Math.min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / L2)); return Math.hypot(p.x - a.x - t * dx, p.y - a.y - t * dy); };
    if (h.length === 1) return Math.hypot(p.x - h[0].x, p.y - h[0].y);
    if (h.length === 2) return dSeg(h[0], h[1]);
    const inside = h.every((a, i) => { const b = h[(i + 1) % h.length]; return (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x) >= 0; }) || h.every((a, i) => { const b = h[(i + 1) % h.length]; return (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x) <= 0; });
    return inside ? 0 : Math.min(...h.map((a, i) => dSeg(a, h[(i + 1) % h.length])));
  }

  /** Regions layout, or { ok: false, why } when the regions can't be drawn cleanly (then the set list is used). */
  function regions(P) {
    const n = P.elements.length;
    if (n > LIMITS.elements) return { ok: false, why: `more than ${LIMITS.elements} elements` };
    if (P.sets.length > LIMITS.sets) return { ok: false, why: `more than ${LIMITS.sets} sets` };
    // nesting level: small sets inside, and two sets that share an element never share a level
    const lv = new Map();
    P.sets.map((s, i) => i).sort((a, b) => P.sets[a].members.length - P.sets[b].members.length || a - b).forEach(i => {
      const taken = new Set(P.sets.filter((t, j) => lv.has(j) && t.members.some(m => P.sets[i].members.includes(m))).map(t => lv.get(P.sets.indexOf(t))));
      let l = 1; while (taken.has(l)) l++;
      lv.set(i, l);
    });
    const deepest = Math.max(1, ...lv.values());
    if (deepest > LIMITS.perElement) return { ok: false, why: `an element sits in more than ${LIMITS.perElement} overlapping sets` };
    const perm = order(P), R = Math.max(118, 40 * n / Math.PI + 30 + 6 * deepest);
    const padMax = PAD0 + PADSTEP * (deepest - 1), M = padMax + 44;
    const W = Math.max(640, 2 * (R + M)), H = 2 * (R + M) + 20, cx = W / 2, cy = H / 2 + 10;
    const pos = new Map();
    perm.forEach((e, p) => { const a = -Math.PI / 2 + (2 * Math.PI * p) / n; pos.set(P.elements[e].id, { x: cx + (n === 1 ? 0 : R * Math.cos(a)), y: cy + (n === 1 ? 0 : R * Math.sin(a)) }); });
    const regs = [], labels = [];
    for (let i = 0; i < P.sets.length; i++) {
      const s = P.sets[i], pad = PAD0 + PADSTEP * (lv.get(i) - 1);
      if (!s.members.length) return { ok: false, why: `${s.id} is empty` };
      const pts = s.members.map(m => pos.get(m));
      for (const e of P.elements) if (!s.members.includes(e.id) && distToHull(pos.get(e.id), pts) < pad + DOT + LIMITS.clearance) return { ok: false, why: `${s.id}'s region would run into ${e.label}` };
      regs.push({ id: s.id, d: regionPath(pts, pad), area: s.members.length * 1000 + pad, pad, pts });
    }
    // labels: on each region's edge, as far out from the centre as possible, clear of dots and of each other
    const dots = [...pos.values()];
    const clearOf = (p, b) => Math.hypot(Math.max(b.x - p.x, 0, p.x - b.x - b.w), Math.max(b.y - p.y, 0, p.y - b.y - b.h));
    const hit = (a, b) => a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h;
    for (const r of regs) {
      const c = r.pts.reduce((a, p) => ({ x: a.x + p.x / r.pts.length, y: a.y + p.y / r.pts.length }), { x: 0, y: 0 });
      let base = Math.atan2(c.y - cy, c.x - cx);
      if (Math.hypot(c.x - cx, c.y - cy) < 8) base = -Math.PI / 2;
      const w = 12 + r.id.length * 8.5, h = 22;
      let placed = null;
      for (const off of [0, 0.3, -0.3, 0.6, -0.6, 0.9, -0.9, 1.2, -1.2, 1.6, -1.6, 2.0, -2.0, 2.5, -2.5, Math.PI]) {
        const a = base + off, ux = Math.cos(a), uy = Math.sin(a);
        const sp = r.pts.reduce((b, p) => (p.x * ux + p.y * uy > b.x * ux + b.y * uy ? p : b));
        // slide out from the edge just far enough to clear the dots; the label still straddles the edge
        for (let t = r.pad; t <= r.pad + 12 && !placed; t += 3) {
          const x = sp.x + t * ux, y = sp.y + t * uy, box = { x: x - w / 2, y: y - h / 2, w, h };
          if (box.x < 2 || box.y < 34 || box.x + w > W - 2 || box.y + h > H - 2) continue;
          if (dots.some(p => clearOf(p, box) < DOT + 3) || labels.some(l => hit(l.box, box))) continue;
          placed = { id: r.id, x, y, w, h, box };
        }
        if (placed) break;
      }
      if (!placed) return { ok: false, why: `no room for ${r.id}'s label` };
      labels.push(placed);
    }
    return { ok: true, W, H, pos, regs: regs.sort((a, b) => b.area - a.area), labels };
  }

  // The set list: U on top, then one row per set with its members as chips (wrapped).
  function list(P, W = 760) {
    const chipW = e => Math.max(2 * DOT, String(e.label).length * 8.4 + 16), lab = P.elements.reduce((m, e) => m.set(e.id, e), new Map());
    const rows = [], tagW = 64;
    let y = 46;
    const place = (id, label, members) => {
      let x = tagW + 30, line = 0;
      const chips = members.map(m => {
        const w = chipW(lab.get(m));
        if (x + w > W - 30) { x = tagW + 30; line++; }
        const c = { id: m, x, y: y + 8 + line * 40, w, h: 2 * DOT };
        x += w + 10; return c;
      });
      const h = 16 + (line + 1) * 40 - 8;
      rows.push({ id, label, x: 10, y, w: W - 20, h, chips });
      y += h + 12;
    };
    place("U", "U", P.elements.map(e => e.id));
    y += 10;
    P.sets.forEach(s => place(s.id, s.label, s.members));
    return { W, H: y + 6, rows };
  }

  /* ---------- drawing ---------- */

  function create({ svg }) {
    if (!document.getElementById("ns-style")) { const st = document.createElement("style"); st.id = "ns-style"; st.textContent = css; document.head.appendChild(st); }
    svg.classList.add("ns-svg");
    let I, P, M, run, frames, f = null, hover = null, mode = "auto", L, els, used = "regions";

    function hookup(g, id) {
      const on = () => { hover = id; paint(); }, off = () => { hover = null; paint(); };
      g.addEventListener("pointerenter", on); g.addEventListener("pointerleave", off);
      g.addEventListener("focus", on); g.addEventListener("blur", off);
      g.addEventListener("click", () => { hover = hover === id ? null : id; paint(); });
    }
    function head(W) {
      const t = mk("text", { x: 12, y: 22, class: "ns-head" }, svg);
      t.textContent = used === "regions" ? "SETS AS REGIONS ROUND THEIR ELEMENTS" : "SET LIST";
      const p = mk("tspan", { class: "pick" }, t); p.textContent = `  ● PICKED: ${M.pickSets ? "SETS" : "ELEMENTS"}`;
      if (used === "list" && L.why) { const w = mk("text", { x: W - 12, y: 22, class: "ns-head", "text-anchor": "end" }, svg); w.textContent = mode === "list" ? "CHOSEN BY HAND" : `REGIONS DON'T FIT: ${L.why.toUpperCase()}`; }
    }
    function badge(g, x, y) {
      const b = mk("g", { visibility: "hidden" }, g);
      mk("circle", { cx: x, cy: y, r: 9, class: "ns-count-bg" }, b);
      const t = mk("text", { x, y, class: "ns-count-t" }, b);
      return { b, t };
    }
    function draw() {
      svg.innerHTML = "";
      const R = mode === "list" ? { ok: false, why: "set list chosen" } : regions(P);
      used = R.ok ? "regions" : "list";
      els = { regions: new Map(), tags: new Map(), dots: new Map(), rows: new Map(), chips: [] };
      if (R.ok) {
        L = R;
        svg.setAttribute("viewBox", `0 0 ${R.W} ${R.H}`);
        head(R.W);
        const gF = mk("g", {}, svg), gE = mk("g", {}, svg), gD = mk("g", {}, svg), gT = mk("g", {}, svg);
        // two layers, every fill under every edge; each path sits in a <g class="ns-region STATE"> of its own
        R.regs.forEach(r => {
          const gf = mk("g", { class: "ns-region" }, gF), ge = mk("g", { class: "ns-region" }, gE);
          mk("path", { d: r.d, class: "fill" }, gf);
          const edge = mk("path", { d: r.d, class: "edge" }, ge);
          els.regions.set(r.id, { gf, ge, edge });
        });
        P.elements.forEach(e => {
          const p = R.pos.get(e.id);
          const g = mk("g", { class: "ns-dot", tabindex: "0", role: "button", "aria-label": `Element ${e.label}, in ${P.sets.filter(s => s.members.includes(e.id)).map(s => s.id).join(", ") || "no set"}` }, gD);
          mk("circle", { cx: p.x, cy: p.y, r: DOT, class: "body" }, g);
          const t = mk("text", { x: p.x, y: p.y }, g); t.textContent = e.label; if (String(e.label).length > 2) t.style.fontSize = "10.5px";
          els.dots.set(e.id, [{ g, ...badge(g, p.x + DOT - 2, p.y - DOT + 2) }]);
          hookup(g, e.id);
        });
        R.labels.forEach(l => {
          const s = P.sets.find(x => x.id === l.id);
          const g = mk("g", { class: "ns-tag", tabindex: "0", role: "button", "aria-label": `Set ${s.label} {${s.members.map(m => P.elements.find(e => e.id === m).label).join(",")}}` }, gT);
          mk("rect", { x: l.box.x, y: l.box.y, width: l.w, height: l.h, rx: 11 }, g);
          mk("text", { x: l.x, y: l.y }, g).textContent = s.label;
          els.tags.set(l.id, { g, ...badge(g, l.box.x + l.w, l.box.y + 1) });
          hookup(g, l.id);
          // the region itself answers hover too
          const r = els.regions.get(l.id); hookup(r.edge, l.id); r.edge.style.pointerEvents = "stroke";
        });
      } else {
        L = { ...list(P), why: R.why };
        svg.setAttribute("viewBox", `0 0 ${L.W} ${L.H}`);
        head(L.W);
        L.rows.forEach((r, k) => {
          const isU = r.id === "U";
          const g = mk("g", { class: "ns-row" + (isU ? " U" : ""), ...(isU ? {} : { tabindex: "0", role: "button", "aria-label": `Set ${r.label}` }) }, svg);
          if (!isU) mk("rect", { x: r.x, y: r.y, width: r.w, height: r.h, rx: 10, class: "box" }, g);
          const tag = mk("g", { class: "ns-tag" }, g);
          mk("rect", { x: r.x + 10, y: r.y + r.h / 2 - 11, width: 52, height: 22, rx: 11 }, tag);
          mk("text", { x: r.x + 36, y: r.y + r.h / 2 }, tag).textContent = r.label;
          mk("text", { x: r.x + 70, y: r.y + r.h / 2, class: "ns-brace" }, g).textContent = "{";
          mk("text", { x: r.x + r.w - 16, y: r.y + r.h / 2, class: "ns-brace" }, g).textContent = "}";
          if (!isU) { els.rows.set(r.id, { g, tag, ...badge(tag, r.x + 62, r.y + r.h / 2 - 10) }); hookup(g, r.id); }
          r.chips.forEach(c => {
            const e = P.elements.find(x => x.id === c.id);
            const cg = mk("g", { class: "ns-dot", tabindex: "0", role: "button", "aria-label": `Element ${e.label}${isU ? "" : " in " + r.label}` }, svg);
            mk("rect", { x: c.x, y: c.y, width: c.w, height: c.h, rx: DOT, class: "body" }, cg);
            mk("text", { x: c.x + c.w / 2, y: c.y + c.h / 2 }, cg).textContent = e.label;
            if (!els.dots.has(c.id)) els.dots.set(c.id, []);
            els.dots.get(c.id).push({ g: cg, ...badge(cg, c.x + c.w - 2, c.y + 2), inU: isU });
            hookup(cg, c.id);
          });
        });
      }
    }
    function paint() {
      const st = id => { const v = f && f.nodes[id]; return (v && v.state) || v || "Background"; }; // a name, or the backend's { state } ref
      const near = new Set();
      if (hover) { near.add(hover); M.hits.get(hover).forEach(x => near.add(x)); }
      const deco = id => (hover ? (id === hover ? " trace" : near.has(id) ? "" : " faint") : "");
      const showBadge = (o, id) => { const k = f && f.hits ? f.hits[id] || 0 : 0; const on = k > 1 && M.cons.includes(id); o.b.setAttribute("visibility", on ? "visible" : "hidden"); if (on) o.t.textContent = "×" + k; };
      els.regions.forEach((r, id) => { const cls = "ns-region " + st(id) + deco(id); r.gf.setAttribute("class", cls); r.ge.setAttribute("class", cls); });
      els.tags.forEach((o, id) => { o.g.setAttribute("class", "ns-tag " + st(id) + deco(id)); showBadge(o, id); });
      els.rows.forEach((o, id) => { o.g.setAttribute("class", "ns-row " + st(id) + deco(id)); o.tag.setAttribute("class", "ns-tag " + st(id)); showBadge(o, id); });
      els.dots.forEach((list, id) => list.forEach(o => { o.g.setAttribute("class", "ns-dot " + st(id) + deco(id)); showBadge(o, id); }));
    }
    function checks(fr) {
      const hits = fr ? fr.hits : {}, chosen = fr ? fr.picks : [];
      const once = M.cons.filter(c => hits[c] === 1).length, any = M.cons.filter(c => hits[c] > 0).length, total = M.cons.length;
      if (I.kind === "SETCOVER") return [{ label: `Elements covered: ${any} / ${total}`, ok: fr ? any === total : null }, { label: `Sets used: ${chosen.length} (K = ${I.K})`, ok: fr ? chosen.length <= I.K : null }];
      if (I.kind === "EXACTCOVER") return [{ label: `Elements covered exactly once: ${once} / ${total}`, ok: fr ? once === total : null }];
      return [{ label: `Sets hit exactly once: ${once} / ${total}`, ok: fr ? once === total : null }];
    }

    return {
      load(str, kind, solver) {
        I = parse(str, kind); P = payload(I); M = model(P);
        const s = solver || (kind === "SETCOVER" ? "greedy" : kind === "EXACTCOVER" ? "x" : "brute");
        run = SOLVERS[s].run(M, I);
        frames = [{ event: null, caption: "Step 0: nothing chosen yet. Step forward to watch the solver.", focus: [], step: null, nodes: {}, hits: {}, picks: [], base: true }, ...build(M, run)];
        f = null; hover = null; draw(); paint();
        return { frames, ok: run.ok, solution: run.solution ? run.solution.map(name) : null, K: I.K, layout: used, why: L.why || null, payload: P };
      },
      // Viewer hook: draw a payload and frames built elsewhere (the backend's Bipartite frames work as they are).
      loadFrames(p, fr, kind) { P = p; M = model(P); I = { kind: kind || (P.pickSide === "elements" ? "HITTINGSET" : P.k !== undefined ? "SETCOVER" : "EXACTCOVER"), K: P.k ?? null }; // backend frames carry no picks list: a pick drawn in a picked state is one
        const sv = v => (v && v.state) || v;
        frames = fr.map(x => x.picks ? x : { ...x, picks: M.picks.filter(p => ["Solution", "Committed", "ElementHighlight", "Rejected"].includes(sv(x.nodes[p]))) });
        f = null; hover = null; draw(); paint(); return { frames, layout: used }; },
      show(i) { f = i === null ? null : frames[i]; paint(); return f; },
      checks(i) { return checks(i === null ? null : frames[i]); },
      chosenLabels(i) { return i === null ? [] : frames[i].picks.map(name); },
      setLayout(m) { mode = m === "list" ? "list" : "auto"; if (P) { draw(); paint(); } return used; },
      layoutUsed() { return { layout: used, why: L ? L.why || null : null }; },
      resetHover() { hover = null; paint(); },
    };
  }

  return { create, parse, payload, model, frame, build, SOLVERS, LIMITS, regions, list, KINDS, _hull: hull, _distToHull: distToHull };
})();
