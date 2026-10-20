/* ---- Formula view (SAT, 3SAT): the CNF itself, shared by both mockups ----
   Clause boxes joined by ∧, literals inside a clause joined by ∨, negation written ¬x, and an assignment strip under it.
   Long formulas wrap at clause boundaries; a clause too long for one line wraps at its ∨s.
   Ids and frames are the Grouped Graph's (clause c{i}, literal occurrence c{i}-{j}, variable v:{name}), so the Grouped Graph,
   the Factor Graph and this picture color together and the Reduction View matches gadgets on them. A frame adds one map,
   `variables`, for the assignment strip. Also the SAT ↦ 3SAT Reduction View pair (Karp's 3SAT Reduction), both sides drawn
   as formulas. Pages supply --av-* tokens. */
const FormulaView = (() => {
  const NS = "http://www.w3.org/2000/svg";
  const mk = (tag, attrs = {}, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; };
  const SUBS = "₀₁₂₃₄₅₆₇₈₉";
  const pretty = nm => { const m = /^([A-Za-z_]+)(\d+)$/.exec(nm); return m ? m[1] + m[2].split("").map(d => SUBS[d]).join("") : nm; };

  const css = `
.fm-svg { width: 100%; height: auto; display: block; }
.fm-svg text { font-family: var(--av-mono); }
.fm-head { font-size: 11px; font-weight: 600; letter-spacing: .08em; fill: var(--av-muted); }
.fm-clause { cursor: pointer; }
.fm-clause:focus { outline: none; }
.fm-clause .box { fill: var(--av-bg); stroke: var(--av-stroke); stroke-width: 1.6; transition: fill .2s, stroke .2s; }
.fm-clause .cid { font-size: 11px; font-weight: 600; fill: var(--av-muted); dominant-baseline: central; }
.fm-clause .mark { font-size: 12px; font-weight: 700; dominant-baseline: central; }
.fm-clause.Covered .box { fill: var(--av-sol-fill); stroke: var(--av-sol); stroke-width: 2.2; }
.fm-clause.Covered .mark { fill: var(--av-sol-ink); }
.fm-clause.ElementHighlight .box { stroke: var(--av-hl); stroke-width: 3; }
.fm-clause.Rejected .box { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 3; }
.fm-clause.Rejected .cid, .fm-clause.Rejected .mark { fill: var(--av-rej-ink); }
.fm-clause.trace .box, .fm-clause:focus-visible .box { stroke: var(--av-hot); stroke-width: 3; }
.fm-clause.faint { filter: grayscale(1); } .fm-clause.faint .box { stroke-dasharray: 3 3; }
.fm-op { font-size: 14px; fill: var(--av-muted); text-anchor: middle; dominant-baseline: central; pointer-events: none; }
.fm-and { font-size: 17px; font-weight: 600; fill: var(--av-ink); text-anchor: middle; dominant-baseline: central; }
.fm-lit { cursor: pointer; }
.fm-lit:focus { outline: none; }
.fm-lit rect { fill: var(--av-bg); stroke: var(--av-stroke); stroke-width: 1.4; transition: fill .2s, stroke .2s; }
.fm-lit text { font-size: 15px; text-anchor: start; dominant-baseline: central; fill: var(--av-ink); pointer-events: none; }
.fm-lit.ElementHighlight rect { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; }
.fm-lit.Solution rect { fill: var(--av-sol); stroke: var(--av-sol); } .fm-lit.Solution text { fill: var(--av-on-sol); font-weight: 600; }
.fm-lit.Committed rect { fill: var(--av-com-fill); stroke: var(--av-com); stroke-width: 2.4; stroke-dasharray: 5 3; } .fm-lit.Committed text { fill: var(--av-com-ink); font-weight: 600; }
.fm-lit .strike { display: none; }
.fm-lit.False rect { fill: var(--av-surface); stroke: var(--av-stroke); stroke-dasharray: 2 3; } .fm-lit.False text { fill: var(--av-muted); }
.fm-lit.False .strike { display: inline; stroke: var(--av-muted); stroke-width: 1.6; }
.fm-lit.Rejected rect { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 2.6; } .fm-lit.Rejected text { fill: var(--av-rej-ink); font-weight: 600; }
.fm-lit.trace rect, .fm-lit:focus-visible rect { stroke: var(--av-hot); stroke-width: 3; stroke-dasharray: none; }
.fm-lit.faint { filter: grayscale(1); } .fm-lit.faint rect { stroke-dasharray: 3 3; }
.fm-var { cursor: pointer; }
.fm-var:focus { outline: none; }
.fm-var rect { fill: var(--av-surface); stroke: var(--av-ctrl); stroke-width: 1.4; transition: fill .2s, stroke .2s; }
.fm-var text { font-size: 14px; text-anchor: start; dominant-baseline: central; fill: var(--av-ink); pointer-events: none; }
.fm-var text .val { font-weight: 700; }
.fm-var.ElementHighlight rect { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; }
.fm-var.Committed rect { fill: var(--av-com-fill); stroke: var(--av-com); stroke-width: 2.4; stroke-dasharray: 5 3; } .fm-var.Committed text { fill: var(--av-com-ink); }
.fm-var.Solution rect { fill: var(--av-sol); stroke: var(--av-sol); } .fm-var.Solution text { fill: var(--av-on-sol); }
.fm-var.False rect { fill: var(--av-surface); stroke: var(--av-stroke); stroke-dasharray: 2 3; } .fm-var.False text { fill: var(--av-muted); }
.fm-var.Rejected rect { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 2.6; } .fm-var.Rejected text { fill: var(--av-rej-ink); }
.fm-var.trace rect, .fm-var:focus-visible rect { stroke: var(--av-hot); stroke-width: 3; stroke-dasharray: none; }
.fm-var.faint { filter: grayscale(1); } .fm-var.faint rect { stroke-dasharray: 3 3; }
.fm-hidden { visibility: hidden; }
.fm-clause.g0 .box { stroke: var(--av-g0s); stroke-width: 2.8; } .fm-clause.g1 .box { stroke: var(--av-g1s); stroke-width: 2.8; }
.fm-clause.g2 .box { stroke: var(--av-g2s); stroke-width: 2.8; } .fm-clause.g3 .box { stroke: var(--av-g3s); stroke-width: 2.8; }
.fm-clause.g4 .box { stroke: var(--av-g4s); stroke-width: 2.8; } .fm-clause.g5 .box { stroke: var(--av-g5s); stroke-width: 2.8; }
.fm-pane-title { font-size: 11.5px; font-weight: 600; letter-spacing: .07em; fill: var(--av-muted); }
.fm-arrow { font-size: 26px; fill: var(--av-muted); text-anchor: middle; dominant-baseline: central; }
.fm-link { fill: none; stroke: var(--av-hot); stroke-width: 1.4; stroke-dasharray: 2 3; pointer-events: none; }
@media (prefers-reduced-motion: reduce) { .fm-clause .box, .fm-lit rect, .fm-var rect { transition: none; } }`;

  function addStyle() {
    if (!document.getElementById("fm-style")) { const st = document.createElement("style"); st.id = "fm-style"; st.textContent = css; document.head.appendChild(st); }
  }

  /* ---------- the instance ---------- */

  // Names in natural order (x2 before x10), as GroupedGraphSpec.FromLiterals numbers them.
  function naturalCompare(a, b) {
    const ra = a.match(/\d+|\D+/g) || [], rb = b.match(/\d+|\D+/g) || [];
    for (let i = 0; i < Math.min(ra.length, rb.length); i++) {
      const x = ra[i], y = rb[i], dx = /^\d/.test(x), dy = /^\d/.test(y);
      let c;
      if (dx && dy) { const vx = x.replace(/^0+/, ""), vy = y.replace(/^0+/, ""); c = vx.length !== vy.length ? vx.length - vy.length : vx < vy ? -1 : vx > vy ? 1 : x < y ? -1 : x > y ? 1 : 0; }
      else if (dx !== dy) c = dx ? -1 : 1;
      else c = x < y ? -1 : x > y ? 1 : 0;
      if (c) return Math.sign(c);
    }
    return ra.length - rb.length;
  }
  // GroupedGraphFrames.EscapeName: a backslash before \ ~ - : inside a name.
  const escapeName = n => n.replace(/[\\~\-:]/g, m => "\\" + m);
  const varId = name => "v:" + escapeName(name);
  const clauseId = i => "c" + i;
  const litId = (i, j) => `c${i}-${j}`;

  /** Redux's SAT format, `(x1 | !x2 | x3) & (!x1 | x3)`, to { clauses (signed 1-based variable indices), names }. */
  function parse(str, kind = "SAT") {
    const s = str.trim();
    if (!s) throw new Error("Enter a formula like (x1 | !x2 | x3) & (!x1 | x3).");
    const written = s.split("&").map((part, i) => {
      const inner = part.trim().replace(/^\(/, "").replace(/\)$/, "");
      if (/[()]/.test(inner) || !inner.trim()) throw new Error(`Clause ${i + 1} is malformed.`);
      return inner.split("|").map(tok => {
        let t = tok.trim(), neg = false;
        while (/^[!~¬]/.test(t)) { neg = !neg; t = t.slice(1).trim(); }
        if (!/^[A-Za-z_]\w*$/.test(t)) throw new Error(`"${tok.trim()}" isn't a literal.`);
        return { neg, name: t };
      });
    });
    if (kind === "SAT3" && written.some(c => c.length > 3)) throw new Error("3SAT clauses have at most three literals.");
    if (written.length > 60) throw new Error("This mockup draws up to 60 clauses.");
    const names = [...new Set(written.flat().map(l => l.name))].sort(naturalCompare);
    if (names.length > 30) throw new Error("This mockup draws up to 30 variables.");
    const index = new Map(names.map((n, i) => [n, i + 1]));
    return { clauses: written.map(c => c.map(l => (l.neg ? -1 : 1) * index.get(l.name))), names };
  }
  const textOf = spec => spec.clauses.map(c => "(" + c.map(l => (l < 0 ? "!" : "") + spec.names[Math.abs(l) - 1]).join(" | ") + ")").join(" & ");

  /** The payload: the Grouped Graph's groups, nodes and variables, without its edges. */
  function payload(spec) {
    const groups = [], nodes = [];
    spec.clauses.forEach((c, i) => {
      const members = [];
      c.forEach((l, j) => {
        const name = spec.names[Math.abs(l) - 1];
        members.push(litId(i, j));
        nodes.push({ id: litId(i, j), label: (l > 0 ? "" : "¬") + name, variable: varId(name), positive: l > 0, group: clauseId(i) });
      });
      groups.push({ id: clauseId(i), label: "C" + (i + 1), members });
    });
    return { kind: "formula", layout: "cnf", shape: "assignment", rule: "satisfiability", groups, nodes, variables: spec.names.map(n => ({ id: varId(n), label: n })) };
  }

  /* ---------- frames: GroupedGraphFrames.Frame, plus the variables map ---------- */

  const RANK = ["Background", "False", "Blocked", "Covered", "Committed", "Solution", "ElementHighlight", "Rejected"];
  const raise = (map, id, st) => { if (RANK.indexOf(st) > RANK.indexOf(map[id] || "Background")) map[id] = st; };
  // GraphSubsetFrames.Picked: what a true literal (a pick) is in a step.
  const picked = (ev, incremental, endsRun, ok) =>
    ev === "Done" ? (ok ? "Solution" : "Rejected") : ev === "Accept" && endsRun ? "Solution" : (ev === "Try" || ev === "Reject") && !incremental ? "ElementHighlight" : "Committed";

  /** One step ({ event, partial: { name: bool }, focus: [ids], caption, ok? }) as the frame the backend would send. */
  function frame(spec, step, { incremental = false, endsRun = false, index = null } = {}) {
    const values = new Map(), assignment = {};
    spec.names.forEach((n, i) => { if (step.partial && n in step.partial) { values.set(i + 1, !!step.partial[n]); assignment[varId(n)] = !!step.partial[n]; } });
    const pick = picked(step.event, incremental, endsRun, step.ok === true);
    const nodes = {}, groups = {}, variables = {}, litsOf = new Map();
    spec.clauses.forEach((c, i) => {
      let trues = 0, falses = 0;
      c.forEach((l, j) => {
        const id = litId(i, j), v = Math.abs(l), vid = varId(spec.names[v - 1]);
        if (!litsOf.has(vid)) litsOf.set(vid, []);
        litsOf.get(vid).push(id);
        if (!values.has(v)) return;
        if (values.get(v) === l > 0) { trues++; raise(nodes, id, pick); } else { falses++; raise(nodes, id, "False"); }
      });
      if (trues) raise(groups, clauseId(i), "Covered");
      else if (c.length && falses === c.length) { raise(groups, clauseId(i), "Rejected"); c.forEach((_, j) => raise(nodes, litId(i, j), "Rejected")); }
    });
    // The strip: a true variable takes the pick's state, a false one is False.
    values.forEach((val, v) => raise(variables, varId(spec.names[v - 1]), val ? pick : "False"));
    const hiLit = id => { if (nodes[id] !== "False") raise(nodes, id, "ElementHighlight"); };
    const clauseMark = step.event === "Reject" ? "Rejected" : "ElementHighlight";
    (step.focus || []).forEach(id => {
      if (litsOf.has(id)) { litsOf.get(id).forEach(hiLit); raise(variables, id, "ElementHighlight"); }
      else if (/^c\d+$/.test(id) && +id.slice(1) < spec.clauses.length) raise(groups, id, clauseMark);
      else if (/^c\d+-\d+$/.test(id)) hiLit(id);
      else if (id.startsWith("v:")) raise(variables, id, "ElementHighlight");
    });
    const out = { event: step.event, caption: step.caption, focus: step.focus || [], step: index, nodes, groups, assignment, variables };
    if (step.ok !== undefined) out.ok = step.ok;
    return out;
  }
  const stoppedFrame = (caption, index = null) => ({ event: "Done", caption, focus: [], step: index, nodes: {}, groups: {}, assignment: {}, variables: {}, ok: false, stopped: true });
  const baseFrame = caption => ({ event: null, caption, focus: [], step: null, nodes: {}, groups: {}, assignment: {}, variables: {}, base: true });

  /** The whole run as frames, as GroupedGraphFrames.Build does: each step with whether it is an Accept that ends the run. */
  function build(spec, run) {
    const frames = run.steps.map((s, k) => {
      const next = run.steps[k + 1];
      if (run.stopped && s.event === "Done") return stoppedFrame(s.caption, k);
      return frame(spec, s, { incremental: run.incremental, endsRun: s.event === "Accept" && next && next.event === "Done" && next.ok === true, index: k });
    });
    return frames;
  }

  /* ---------- reference solvers (test oracles; the backend records the real steps) ---------- */

  const nameOf = (spec, v) => spec.names[v - 1];
  const show = (spec, v) => pretty(nameOf(spec, v));
  const litShow = (spec, l) => (l < 0 ? "¬" : "") + show(spec, Math.abs(l));
  const valOf = (asg, l) => { const a = asg[Math.abs(l)]; return a === undefined ? undefined : l > 0 ? a : !a; };
  const toPartial = (spec, asg) => Object.fromEntries(Object.entries(asg).map(([v, b]) => [nameOf(spec, +v), b]));

  // Backtracking with unit propagation (DPLL). Incremental: what it has decided so far is kept (Committed).
  function solveDpll(spec, { limit = Infinity } = {}) {
    const steps = [], asg = {}, n = spec.names.length;
    const push = (event, focus, caption) => steps.push({ event, partial: toPartial(spec, asg), focus, caption });
    const falsified = () => spec.clauses.findIndex(c => c.every(l => valOf(asg, l) === false));
    const satisfied = () => spec.clauses.every(c => c.some(l => valOf(asg, l) === true));
    function propagate(trail) {
      for (;;) {
        if (steps.length >= limit) return "stop";
        const bad = falsified();
        if (bad >= 0) { push("Reject", [clauseId(bad)], `C${bad + 1} has no true literal left, so this branch fails.`); return "conflict"; }
        const unit = spec.clauses.findIndex(c => !c.some(l => valOf(asg, l) === true) && c.filter(l => valOf(asg, l) === undefined).length === 1);
        if (unit < 0) return "ok";
        const l = spec.clauses[unit].find(x => valOf(asg, x) === undefined), v = Math.abs(l);
        asg[v] = l > 0; trail.push(v);
        push("Accept", [varId(nameOf(spec, v))], `C${unit + 1} has one literal left that can be true, ${litShow(spec, l)}, so ${show(spec, v)} = ${l > 0 ? "T" : "F"}.`);
      }
    }
    let stopped = false;
    function rec() {
      if (satisfied()) return true;
      let v = 1; while (v <= n && asg[v] !== undefined) v++;
      if (v > n) return false;
      for (const b of [true, false]) {
        if (steps.length >= limit) { stopped = true; return false; }
        asg[v] = b;
        push("Try", [varId(nameOf(spec, v))], `${b ? "Try" : "Now try"} ${show(spec, v)} = ${b ? "T" : "F"}.`);
        const trail = [], r = propagate(trail);
        if (r === "stop") { stopped = true; return false; }
        if (r === "ok" && rec()) return true;
        if (stopped) return false;
        trail.forEach(u => delete asg[u]);
        delete asg[v];
        if (b) push("Backtrack", [varId(nameOf(spec, v))], `${show(spec, v)} = T fails, so undo it${trail.length ? " and what it forced" : ""}.`);
      }
      push("Backtrack", [varId(nameOf(spec, v))], `Both values of ${show(spec, v)} fail, so back up.`);
      return false;
    }
    const trail0 = [], r0 = propagate(trail0);
    const ok = r0 === "ok" && rec();
    if (stopped || r0 === "stop") {
      steps.push({ event: "Done", partial: {}, focus: [], caption: "Stopped after 10 s: the time limit ran out before the solver finished.", ok: false });
      return { steps, incremental: true, stopped: true, ok: false, solution: null };
    }
    if (ok) {
      const free = spec.names.map((_, i) => i + 1).filter(v => asg[v] === undefined);
      free.forEach(v => (asg[v] = false));
      steps.push({ event: "Done", partial: toPartial(spec, asg), focus: [], ok: true,
        caption: `Every clause has a true literal: satisfiable.${free.length ? ` ${free.map(v => show(spec, v)).join(", ")} ${free.length > 1 ? "were" : "was"} never needed, so ${free.length > 1 ? "they are" : "it is"} set to F.` : ""}` });
    } else steps.push({ event: "Done", partial: {}, focus: [], ok: false, caption: "Every branch failed, so no assignment satisfies the formula: unsatisfiable." });
    return { steps, incremental: true, ok, solution: ok ? { ...asg } : null };
  }

  // Brute force: every whole assignment in turn. Not incremental, so each candidate is only highlighted.
  const SHOW_TRIES = 64;
  function solveBrute(spec) {
    const steps = [], n = spec.names.length;
    if (n > 12) return { steps: [{ event: "Done", partial: {}, focus: [], ok: false, caption: "Too many variables for the mockup to brute-force. Use the backtracking solver." }], incremental: false, ok: false, solution: null };
    let found = null, tried = 0, last = null;
    for (let m = 0; m < 1 << n; m++) {
      const asg = {}; for (let v = 1; v <= n; v++) asg[v] = !(m & (1 << (n - v)));
      last = asg; tried++;
      const bad = spec.clauses.findIndex(c => c.every(l => valOf(asg, l) === false));
      const txt = spec.names.map((_, i) => `${show(spec, i + 1)}=${asg[i + 1] ? "T" : "F"}`).join(" ");
      if (tried <= SHOW_TRIES || bad < 0) steps.push(bad < 0
        ? { event: "Accept", partial: toPartial(spec, asg), focus: [], caption: `Try ${txt}: every clause has a true literal.` }
        : { event: "Reject", partial: toPartial(spec, asg), focus: [clauseId(bad)], caption: `Try ${txt}: C${bad + 1} is false.` });
      if (bad < 0) { found = asg; break; }
    }
    const hidden = tried > SHOW_TRIES ? ` (${tried - SHOW_TRIES} tries not shown)` : "";
    // The mockup's brute force ends an unsatisfiable run on its last candidate, to show that a Done with ok = false draws its picks Rejected.
    if (found) steps.push({ event: "Done", partial: toPartial(spec, found), focus: [], ok: true, caption: `Satisfiable, found after ${tried} tries${hidden}.` });
    else steps.push({ event: "Done", partial: toPartial(spec, last), focus: [], ok: false, caption: `Checked all ${tried} assignments${hidden}; none satisfies every clause. The last one tried is drawn red.` });
    return { steps, incremental: false, ok: !!found, solution: found };
  }

  const SOLVERS = {
    dpll: { label: "Backtracking (DPLL)", run: spec => solveDpll(spec) },
    brute: { label: "Brute Force", run: spec => solveBrute(spec) },
    stopped: { label: "Backtracking, stopped by the time limit", run: spec => solveDpll(spec, { limit: 5 }) },
  };

  /* ---------- layout: pure math, (payload, width) → boxes ---------- */

  const CW = 9, PH = 30, PAD = 9, BH = PH + 2 * PAD, OR_W = 24, AND_W = 34, LABEL_H = 18, ROW_GAP = 16, VW = 66, VH = 30;
  // Labels: a name of letters then digits (x10) is drawn as its letters with the digits as a real subscript, at SUBF of the
  // label size; any other name (a, foo_2) is drawn plain. Mono, so a width is characters × CW (CW at the 15 px literal size).
  const LIT_PX = 15, VAR_PX = 14, SUBF = 0.78;
  const split = nm => { const m = /^([A-Za-z]+)(\d+)$/.exec(nm); return m ? { base: m[1], sub: m[2] } : { base: nm, sub: "" }; };
  const labelW = (neg, nm, px = LIT_PX) => { const p = split(nm); return ((neg ? 1 : 0) + p.base.length + p.sub.length * SUBF) * CW * px / LIT_PX; };
  const pillW = (neg, nm) => Math.max(34, labelW(neg, nm) + 18);
  /** Writes a label into a <text> drawn from its left edge x0: base (with ¬), then the subscript a little lower. */
  function drawLabel(t, neg, nm, px) {
    t.textContent = "";
    const p = split(nm), shift = +(0.32 * px).toFixed(1);
    mk("tspan", { class: "base" }, t).textContent = (neg ? "¬" : "") + p.base;
    if (p.sub) mk("tspan", { class: "sub", "font-size": +(SUBF * px).toFixed(1), dy: shift }, t).textContent = p.sub;
    return { p, shift: p.sub ? shift : 0 };
  }

  function layout(P, width, top = 34) {
    const left = 12, right = width - 12;
    const lits = new Map(P.nodes.map(n => { const name = n.positive ? n.label : n.label.slice(1); return [n.id, { ...n, text: n.label, neg: !n.positive, name, pw: pillW(!n.positive, name) }]; }));
    const clauses = [], ands = [], ors = [];
    let x = left, y = top, rowH = 0, rowStart = true;
    P.groups.forEach((g, i) => {
      const ms = g.members.map(id => lits.get(id));
      // pack the clause's literals into rows no wider than the line (a long SAT clause wraps at its ∨s)
      const maxInner = right - left - 2 * PAD - AND_W, rows = [[]];
      let w = 0;
      ms.forEach(m => {
        const need = (rows.at(-1).length ? OR_W : 0) + m.pw;
        if (rows.at(-1).length && w + need > maxInner) { rows.push([]); w = 0; }
        w += (rows.at(-1).length ? OR_W : 0) + m.pw; rows.at(-1).push(m);
      });
      const rowWs = rows.map((r, k) => r.reduce((a, m, q) => a + m.pw + (q || k ? OR_W : 0), 0));
      const bw = 2 * PAD + Math.max(...rowWs), bh = BH + (rows.length - 1) * (PH + 8);
      const need = (i ? AND_W : 0) + bw;
      if (!rowStart && x + need > right) { y += rowH + ROW_GAP; x = left; rowH = 0; rowStart = true; }
      if (i) { ands.push({ x: x + AND_W / 2, y: y + LABEL_H + BH / 2, after: g.id }); x += AND_W; }
      const box = { id: g.id, label: g.label, x, y: y + LABEL_H, w: bw, h: bh, members: g.members };
      rows.forEach((r, k) => {
        let px = x + PAD + (k ? OR_W : 0);
        const py = box.y + PAD + k * (PH + 8);
        if (k) ors.push({ x: x + PAD + OR_W / 2, y: py + PH / 2, clause: g.id });
        r.forEach((m, q) => {
          if (q) { ors.push({ x: px + OR_W / 2, y: py + PH / 2, clause: g.id }); px += OR_W; }
          Object.assign(m, { x: px, y: py, w: m.pw, h: PH });
          px += m.pw;
        });
      });
      clauses.push(box);
      x += bw; rowH = Math.max(rowH, LABEL_H + bh); rowStart = false;
    });
    let vy = y + rowH + 40;
    const vars = [];
    let vx = left;
    P.variables.forEach(v => {
      const w = Math.max(VW, labelW(false, v.label, VAR_PX) + 2 * CW * VAR_PX / LIT_PX + 18);
      if (vx + w > right) { vx = left; vy += VH + 10; }
      vars.push({ ...v, x: vx, y: vy, w, h: VH });
      vx += w + 8;
    });
    return { clauses, lits, ands, ors, vars, headY: top - 12, stripY: y + rowH + 22, height: vy + VH + 14 };
  }

  /* ---------- drawing one formula into a group ---------- */

  function drawFormula(root, P, L, hookup, { head = true } = {}) {
    if (head) {
      mk("text", { x: 12, y: L.headY, class: "fm-head" }, root).textContent = "FORMULA · CNF";
      mk("text", { x: 12, y: L.stripY, class: "fm-head" }, root).textContent = "ASSIGNMENT";
    }
    const els = { clauses: new Map(), lits: new Map(), vars: new Map(), ands: [], P, L };
    L.clauses.forEach(c => {
      const g = mk("g", { class: "fm-clause", tabindex: "0", role: "button", "aria-label": `Clause ${c.label}: ${c.members.map(id => L.lits.get(id).text).join(" or ")}` }, root);
      mk("rect", { x: c.x, y: c.y, width: c.w, height: c.h, rx: 9, class: "box" }, g);
      mk("text", { x: c.x + 2, y: c.y - 9, class: "cid" }, g).textContent = c.label;
      const mark = mk("text", { x: c.x + 6 + c.label.length * 7.4, y: c.y - 9, class: "mark" }, g);
      els.clauses.set(c.id, { g, mark, box: c });
      hookup(g, c.id);
    });
    L.ands.forEach(a => { const t = mk("text", { x: a.x, y: a.y, class: "fm-and" }, root); t.textContent = "∧"; els.ands.push({ el: t, after: a.after }); });
    L.ors.forEach(o => { const t = mk("text", { x: o.x, y: o.y, class: "fm-op" }, root); t.textContent = "∨"; o.el = t; });
    L.lits.forEach((m, id) => {
      const g = mk("g", { class: "fm-lit", tabindex: "0", role: "button", "aria-label": `${m.text} in clause ${+m.group.slice(1) + 1}` }, root);
      mk("rect", { x: m.x, y: m.y, width: m.w, height: m.h, rx: 7 }, g);
      // drawn from the left edge (not centred) so the False strike can end exactly where the base letters end
      const lw = labelW(m.neg, m.name), x0 = m.x + (m.w - lw) / 2, cy = m.y + m.h / 2;
      const t = mk("text", { x: x0, y: cy }, g);
      const { p } = drawLabel(t, m.neg, m.name, LIT_PX);
      // False's mark besides color: a strike through the letters only, never through the subscript
      const baseW = ((m.neg ? 1 : 0) + p.base.length) * CW * 0.9;
      mk("line", { x1: x0 - 1, y1: cy + 1, x2: x0 + baseW, y2: cy + 1, class: "strike" }, g);
      els.lits.set(id, { g, m });
      hookup(g, id);
    });
    L.vars.forEach(v => {
      const g = mk("g", { class: "fm-var", tabindex: "0", role: "button", "aria-label": `Variable ${v.label}` }, root);
      mk("rect", { x: v.x, y: v.y, width: v.w, height: v.h, rx: 7 }, g);
      const t = mk("text", { x: v.x + 9, y: v.y + v.h / 2, class: "lab" }, g);
      t.textContent = v.label + " –";
      els.vars.set(v.id, { g, t, v });
      hookup(g, v.id);
    });
    return els;
  }

  // What a hovered id lights up: a variable traces its literals; a literal its variable's literals; a clause its literals.
  function traceSet(P, id) {
    const hot = new Set([id]);
    if (!id) return hot;
    const node = P.nodes.find(n => n.id === id);
    const vid = id.startsWith("v:") ? id : node ? node.variable : null;
    if (vid) { hot.add(vid); P.nodes.forEach(n => { if (n.variable === vid) hot.add(n.id); }); }
    const grp = P.groups.find(g => g.id === id);
    if (grp) grp.members.forEach(m => { hot.add(m); hot.add(P.nodes.find(n => n.id === m).variable); });
    return hot;
  }

  function paintFormula(els, f, { hot = null, shown = null, colorOf = null } = {}) {
    // a state is a name here, or a { state } ref as the backend writes it
    const st = (map, id) => { const v = f && f[map] && f[map][id]; return (v && v.state) || v || "Background"; };
    els.clauses.forEach((o, id) => {
      const s = st("groups", id);
      let cls = "fm-clause " + s;
      if (colorOf) { const c = colorOf(id); if (c !== null) cls += " g" + c; }
      if (hot) cls += hot.has(id) ? " trace" : " faint";
      if (shown && !shown.has(id)) cls += " fm-hidden";
      o.g.setAttribute("class", cls);
      o.mark.textContent = s === "Covered" ? "✓" : s === "Rejected" ? "✗" : "";
    });
    els.ands.forEach(a => a.el.setAttribute("class", "fm-and" + (shown && !shown.has(a.after) ? " fm-hidden" : "")));
    els.L.ors.forEach(o => o.el.setAttribute("class", "fm-op" + (shown && !shown.has(o.clause) ? " fm-hidden" : "")));
    els.lits.forEach((o, id) => {
      let cls = "fm-lit " + st("nodes", id);
      if (hot) cls += hot.has(id) ? " trace" : " faint";
      if (shown && !shown.has(o.m.group)) cls += " fm-hidden";
      o.g.setAttribute("class", cls);
    });
    els.vars.forEach((o, id) => {
      const a = f && f.assignment ? f.assignment[id] : undefined;
      let cls = "fm-var " + st("variables", id);
      if (hot) cls += hot.has(id) ? " trace" : " faint";
      if (shown && shown.vars && !shown.vars.has(id)) cls += " fm-hidden";
      o.g.setAttribute("class", cls);
      const { shift } = drawLabel(o.t, false, o.v.label, VAR_PX);
      mk("tspan", { class: "val", dx: 7, ...(shift ? { dy: -shift } : {}) }, o.t).textContent = a === undefined ? "–" : a ? "T" : "F";
    });
  }

  /* ---------- the Formula picture ---------- */

  function create({ svg }) {
    addStyle();
    svg.classList.add("fm-svg");
    let spec, P, run, frames, frame0 = null, els, hover = null, kind = "SAT";

    function draw() {
      svg.innerHTML = "";
      const W = 800, L = layout(P, W);
      svg.setAttribute("viewBox", `0 0 ${W} ${L.height}`);
      const hookup = (g, id) => {
        const on = () => { hover = id; paint(); }, off = () => { hover = null; paint(); };
        g.addEventListener("pointerenter", on); g.addEventListener("pointerleave", off);
        g.addEventListener("focus", on); g.addEventListener("blur", off);
        g.addEventListener("click", () => { hover = hover === id ? null : id; paint(); });
      };
      els = drawFormula(svg, P, L, hookup);
    }
    function paint() { paintFormula(els, frame0, { hot: hover ? traceSet(P, hover) : null }); }

    function checks(f) {
      const total = spec.clauses.length, a = f ? f.assignment : {};
      const covered = f ? Object.values(f.groups).filter(s => s === "Covered").length : 0;
      const failed = f ? Object.values(f.groups).filter(s => s === "Rejected").length : 0;
      return [
        { label: `Clauses with a true literal: ${covered} / ${total}`, ok: f ? covered === total : null },
        { label: `Clauses with every literal false: ${failed}`, ok: f ? failed === 0 : null },
        { label: `Variables assigned: ${Object.keys(a).length} / ${spec.names.length}`, ok: f ? Object.keys(a).length === spec.names.length : null },
      ];
    }

    return {
      // kind: "SAT" or "SAT3"; solver: a key of SOLVERS
      load(str, k = "SAT", solver = "dpll") {
        kind = k; spec = parse(str, k); P = payload(spec);
        run = SOLVERS[solver].run(spec);
        frames = [baseFrame("Step 0: the formula, nothing assigned. Step forward to watch the solver."), ...build(spec, run)];
        frame0 = null; hover = null;
        draw(); paint();
        return { frames, ok: run.ok, solution: run.solution ? spec.names.map((n, i) => `${n}=${run.solution[i + 1] ? "T" : "F"}`) : null, incremental: run.incremental, payload: P };
      },
      // Viewer hook: draw a payload and frames built elsewhere (the backend's), without running a mockup solver.
      loadFrames(payloadIn, fr) { P = payloadIn; spec = { clauses: P.groups.map(g => g.members), names: P.variables.map(v => v.label) }; frames = fr; frame0 = null; hover = null; draw(); paint(); return { frames }; },
      show(i) { frame0 = i === null ? null : frames[i]; paint(); return frame0; },
      checks(i) { return checks(i === null ? null : frames[i]); },
      resetHover() { hover = null; paint(); },
    };
  }

  /* ---------- Karp's 3SAT Reduction (SAT ↦ 3SAT), as KarpSATToSAT3 builds it ---------- */

  function karp(str) {
    const src = parse(str, "SAT");
    if (src.clauses.length > 8) throw new Error("This mockup draws up to 8 clauses for this reduction.");
    const lit = l => (l < 0 ? "!" : "") + src.names[Math.abs(l) - 1];
    // Each target clause keeps its literal strings and, parallel to them, the source literal id each came from (null for new ones).
    const T = src.clauses.map((c, i) => ({ lits: c.map(lit), from: c.map((_, j) => litId(i, j)), origin: i }));
    const used = new Set(src.names);
    let fresh = "x1";
    const chain = [], helperOf = new Map(), splits = [];
    for (let idx = T.findIndex(c => c.lits.length > 3); idx >= 0; idx = T.findIndex(c => c.lits.length > 3)) {
      while (used.has(fresh)) fresh = "x" + (+fresh.slice(1) + 1);
      used.add(fresh);
      const c = T[idx], n = c.lits.length;
      const moved = { lits: [c.lits[n - 2], c.lits[n - 1], fresh], from: [c.from[n - 2], c.from[n - 1], null], origin: c.origin };
      splits.push({ y: fresh, a: c.lits[n - 2], b: c.lits[n - 1] });
      T.push(moved); chain.push([idx, T.length - 1]); helperOf.set(fresh, c.origin);
      c.lits.splice(n - 2, 2, "!" + fresh); c.from.splice(n - 2, 2, null);
    }
    const padded = new Map();
    T.forEach((c, t) => {
      if (c.lits.length === 2) { padded.set(t, 2); c.lits.push(c.lits[0]); c.from.push(c.from[0]); }
      else if (c.lits.length === 1) { padded.set(t, 1); c.lits.push(c.lits[0], c.lits[0]); c.from.push(c.from[0], c.from[0]); }
    });
    const tgtStr = T.map(c => "(" + c.lits.join(" | ") + ")").join(" & ");
    const tgt = parse(tgtStr, "SAT3");
    const Ps = payload(src), Pt = payload(tgt);
    const vSrc = n => varId(n);

    // gadgets: a group per source clause (it and every clause it became), an element per literal, a variable per variable,
    // and an element for each new variable (from the clause whose split made it), as KarpSATToSAT3 lists them.
    const gadgets = [];
    src.clauses.forEach((c, i) => {
      gadgets.push({ kind: "group", from: ["s:" + clauseId(i)], to: T.map((x, t) => (x.origin === i ? "t:" + clauseId(t) : null)).filter(Boolean) });
      c.forEach((_, j) => gadgets.push({ kind: "element", from: ["s:" + litId(i, j)], to: T.flatMap((x, t) => x.from.map((f, q) => (f === litId(i, j) ? "t:" + litId(t, q) : null))).filter(Boolean) }));
    });
    src.names.forEach(n => gadgets.push({ kind: "element", from: ["s:" + vSrc(n)], to: ["t:" + vSrc(n)] }));
    helperOf.forEach((origin, y) => gadgets.push({ kind: "element", from: ["s:" + clauseId(origin)], to: ["t:" + vSrc(y)], note: `${pretty(y)} is new: it links the clauses C${origin + 1} is split into` }));

    const frames = [];
    const shownC = new Set(), shownV = new Set();
    const empty = { event: "Try", partial: {}, caption: "" };
    src.clauses.forEach((c, i) => {
      const mine = T.map((x, t) => (x.origin === i ? t : -1)).filter(t => t >= 0);
      mine.forEach(t => shownC.add(clauseId(t)));
      const newVars = [...new Set(mine.flatMap(t => T[t].lits.map(l => l.replace(/^!/, ""))))].filter(n => !shownV.has(vSrc(n)));
      newVars.forEach(n => shownV.add(vSrc(n)));
      const focusSrc = [clauseId(i), ...c.map((_, j) => litId(i, j))];
      const focusTgt = mine.flatMap(t => [clauseId(t), ...T[t].lits.map((_, q) => litId(t, q))]).concat(newVars.map(vSrc));
      const k = c.length, txt = c.map(l => litShow(src, l)).join(" ∨ ");
      const helpers = mine.slice(1).map(t => T[t].lits[2]);
      let caption;
      if (k === 3) caption = `C${i + 1} (${txt}) already has three literals, so it is copied as it is.`;
      else if (k < 3) caption = `C${i + 1} (${txt}) has ${k === 1 ? "one literal" : "two literals"}, so ${k === 1 ? "it is written three times" : "its first literal is repeated"}: x ∨ x is still x.`;
      else caption = `C${i + 1} (${txt}) has ${k} literals, so it is split into ${mine.length} clauses chained by the new variable${helpers.length > 1 ? "s" : ""} ${helpers.map(pretty).join(", ")}. Each new variable says "the rest of the clause is true".`;
      frames.push({ phase: "build", event: "Try", shown: new Set(shownC), shownV: new Set(shownV), focusSrc, focusTgt, caption,
        src: frame(src, { ...empty, focus: focusSrc }), tgt: frame(tgt, { ...empty, focus: focusTgt }) });
    });
    // The construction's end is a plain "target built" frame (an Accept), never a verdict (#840).
    frames.push({ phase: "build", event: "Accept", shown: new Set(shownC), shownV: new Set(shownV), focusSrc: [], focusTgt: [], src: null, tgt: null,
      caption: `Done building: ${T.length} clauses of exactly three literals. The 3SAT formula is satisfiable exactly when the SAT formula is.` });

    // Solve B with the backtracking solver; map its answer back variable by variable.
    // As ReductionView.Create (local/viz-integration) does: the solve phase never ends the view, so its last frame is an Accept
    // or a Reject; only the last frame of the map phase is a Done, and only it carries ok (#840).
    const solved = solveDpll(tgt);
    if (!solved.ok) {
      frames.push({ phase: "solve", event: "Reject", src: null, tgt: frame(tgt, { event: "Reject", partial: {}, focus: [], caption: "" }),
        caption: "The 3SAT backtracking solver finds no satisfying assignment." });
      frames.push({ phase: "map", event: "Done", done: true, ok: false, focusSrc: [], focusTgt: [], src: null, tgt: frame(tgt, { event: "Reject", partial: {}, focus: [], caption: "" }),
        caption: "There is no 3SAT answer to map back, so the SAT formula has no satisfying assignment either." });
    } else {
      const tAsg = toPartial(tgt, solved.solution), tDone = frame(tgt, { event: "Done", partial: tAsg, focus: [], ok: true, caption: "" });
      frames.push({ phase: "solve", event: "Accept", src: null, tgt: tDone, caption: `Solve the 3SAT formula with the backtracking solver: ${tgt.names.map(n => `${pretty(n)}=${tAsg[n] ? "T" : "F"}`).join(", ")}.` });
      const sAsg = {};
      src.names.forEach((n, k) => {
        sAsg[n] = tAsg[n];
        // the target keeps its solution and highlights the variable being read
        frames.push({ phase: "map", event: "Accept", focusSrc: [vSrc(n)], focusTgt: [vSrc(n)], tgt: frame(tgt, { event: "Done", partial: tAsg, focus: [vSrc(n)], ok: true, caption: "" }),
          src: frame(src, { event: "Accept", partial: { ...sAsg }, focus: [vSrc(n)], caption: "" }, { incremental: true }),
          caption: `${pretty(n)} is ${tAsg[n] ? "T" : "F"} in the 3SAT answer, so ${pretty(n)} = ${tAsg[n] ? "T" : "F"} in the SAT answer.` + (k === 0 ? " The original variables keep their values." : "") });
      });
      if (helperOf.size) frames.push({ phase: "map", event: "Accept", focusSrc: [], focusTgt: [...helperOf.keys()].map(vSrc), tgt: frame(tgt, { event: "Done", partial: tAsg, focus: [...helperOf.keys()].map(vSrc), ok: true, caption: "" }),
        src: frame(src, { event: "Accept", partial: { ...sAsg }, focus: [], caption: "" }, { incremental: true }),
        caption: `${[...helperOf.keys()].map(pretty).join(", ")} ${helperOf.size > 1 ? "are" : "is"} the reduction's own, so ${helperOf.size > 1 ? "they are" : "it is"} dropped.` });
      const ok = src.clauses.every(c => c.some(l => (l > 0 ? sAsg[src.names[l - 1]] : !sAsg[src.names[-l - 1]])));
      frames.push({ phase: "map", event: "Done", done: true, ok, focusSrc: [], focusTgt: [], tgt: tDone,
        src: frame(src, { event: "Done", partial: { ...sAsg }, focus: [], ok, caption: "" }),
        caption: ok ? `Every SAT clause has a true literal: ${src.names.map(n => `${pretty(n)}=${sAsg[n] ? "T" : "F"}`).join(", ")} satisfies the formula.` : "The mapped assignment doesn't satisfy the formula." });
    }
    // every frame after the build shows the whole target
    const allC = new Set(T.map((_, t) => clauseId(t))), allV = new Set(tgt.names.map(vSrc));
    frames.forEach(f => { if (!f.shown) { f.shown = allC; f.shownV = allV; } });
    return { src, tgt, Ps, Pt, gadgets, frames, targetInstance: tgtStr, splits, padded };
  }

  const REDUCTIONS = {
    KARP_SAT3: { label: "Karp's 3SAT Reduction", from: "SAT", to: "SAT3", fromLabel: "SAT", toLabel: "3SAT", run: karp,
      def: "Karp's reduction keeps every clause of three literals, pads shorter ones by repeating a literal, and splits longer ones into a chain of three-literal clauses joined by new variables.",
      source: "Karp, Reducibility among combinatorial problems (1972)", contributors: "Andrija Sevaljevic",
      examples: [["Mixed widths", "(x1 | !x2 | x3 | x4 | !x5) & (!x1 | x2) & (x3)"], ["Redux default", "(x1 | !x2 | x3) & (!x1 | x3 | x4) & (x2 | !x3 | !x4 | x5)"], ["Unsatisfiable", "(x1 | x2) & (x1 | !x2) & (!x1 | x2) & (!x1 | !x2)"]] },
  };

  /* ---------- the Reduction View pair: both sides as formulas ---------- */

  function createPair({ svg }) {
    addStyle();
    svg.classList.add("fm-svg");
    let R, red, f = null, colors = false, hoverId = null, S, T, links, gIndex;
    const W = 1080, SW = 500, TX = 560, TW = 520;

    function draw() {
      svg.innerHTML = "";
      const Ls = layout(R.Ps, SW, 62), Lt = layout(R.Pt, TW, 62);
      const H = Math.max(Ls.height, Lt.height, 300);
      svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
      mk("text", { x: 12, y: 24, class: "fm-pane-title" }, svg).textContent = `${red.fromLabel.toUpperCase()} · THE INSTANCE YOU GAVE`;
      mk("text", { x: TX + 12, y: 24, class: "fm-pane-title" }, svg).textContent = `${red.toLabel.toUpperCase()} · BUILT BY THE REDUCTION`;
      mk("line", { class: "rd-sep", x1: TX - 30, y1: 40, x2: TX - 30, y2: H - 20, stroke: "var(--av-line)", "stroke-dasharray": "2 5" }, svg);
      mk("text", { x: TX - 30, y: H / 2 - 12, class: "fm-arrow" }, svg).textContent = "↦";
      const hook = side => (g, id) => {
        const key = side + id;
        const on = () => { hoverId = key; paint(); }, off = () => { hoverId = null; paint(); };
        g.addEventListener("pointerenter", on); g.addEventListener("pointerleave", off);
        g.addEventListener("focus", on); g.addEventListener("blur", off);
        g.addEventListener("click", () => { hoverId = hoverId === key ? null : key; paint(); });
      };
      const gs = mk("g", {}, svg), gt = mk("g", { transform: `translate(${TX},0)` }, svg);
      S = drawFormula(gs, R.Ps, Ls, hook("s:"));
      T = drawFormula(gt, R.Pt, Lt, hook("t:"));
      links = mk("g", {}, svg);
      gIndex = new Map();
      R.gadgets.forEach((gd, gi) => [...gd.from, ...gd.to].forEach(id => { if (!gIndex.has(id)) gIndex.set(id, []); gIndex.get(id).push(gi); }));
    }
    // where a link meets a piece: its right edge on the source side, its left edge on the target side
    function anchor(id) {
      const side = id.slice(0, 2), raw = id.slice(2), E = side === "s:" ? S : T, dx = side === "s:" ? 0 : TX;
      const box = E.clauses.get(raw)?.box || E.lits.get(raw)?.m || E.vars.get(raw)?.v;
      if (!box) return null;
      return { x: dx + (side === "s:" ? box.x + box.w : box.x), y: box.y + box.h / 2 };
    }
    function paint() {
      const hot = new Set(), pairs = [];
      if (hoverId) {
        hot.add(hoverId);
        const side = hoverId.slice(0, 2), raw = hoverId.slice(2);
        traceSet(side === "s:" ? R.Ps : R.Pt, raw).forEach(x => hot.add(side + x));
        [...hot].forEach(id => (gIndex.get(id) || []).forEach(gi => {
          const gd = R.gadgets[gi];
          gd.from.forEach(a => hot.add(a)); gd.to.forEach(b => hot.add(b));
          gd.from.forEach(a => gd.to.forEach(b => { if (pairs.length < 16) pairs.push([a, b]); }));
        }));
      }
      const groupList = R.gadgets.filter(g => g.kind === "group");
      const colorOf = side => colors ? id => { const gi = groupList.findIndex(g => (side === "s:" ? g.from : g.to).includes(side + id)); return gi < 0 ? null : gi % 6; } : null;
      const pick = side => hoverId ? new Set([...hot].filter(x => x.startsWith(side)).map(x => x.slice(2))) : null;
      const shownT = f ? Object.assign(new Set(f.shown), { vars: f.shownV }) : null;
      paintFormula(S, f && f.src, { hot: pick("s:"), colorOf: colorOf("s:") });
      paintFormula(T, f && f.tgt, { hot: pick("t:"), shown: shownT, colorOf: colorOf("t:") });
      links.innerHTML = "";
      pairs.forEach(([a, b]) => {
        const raw = b.slice(2);
        if (shownT && (raw.startsWith("v:") ? !shownT.vars.has(raw) : !shownT.has(raw.split("-")[0]))) return;
        const p = anchor(a), q = anchor(b);
        if (!p || !q) return;
        const mx = (p.x + q.x) / 2;
        mk("path", { d: `M${p.x},${p.y} C${mx},${p.y} ${mx},${q.y} ${q.x},${q.y}`, class: "fm-link" }, links);
      });
    }
    function checks(fr) {
      const total = R.Pt.groups.length, built = fr ? fr.shown.size : total;
      const out = [{ label: `3SAT clauses built: ${built} / ${total}`, ok: fr ? built === total : null },
        { label: "Every 3SAT clause has exactly three literals", ok: fr ? R.tgt.clauses.every(c => c.length === 3) : null }];
      if (fr && (fr.phase === "solve" || fr.phase === "map")) {
        const s = R.frames.find(x => x.phase === "solve");
        out.push({ label: `3SAT answer: ${s && s.event === "Accept" ? "found" : "none exists"}`, ok: !!(s && s.event === "Accept") });
      }
      if (fr && fr.done) out.push({ label: "Mapped back to a valid SAT answer", ok: !!fr.ok });
      return out;
    }
    return {
      load(src, key = "KARP_SAT3") {
        red = REDUCTIONS[key]; R = red.run(src); f = null; hoverId = null;
        draw(); paint();
        return { frames: R.frames, ok: R.frames.at(-1).ok, targetInstance: R.targetInstance, gadgets: R.gadgets, reduction: red };
      },
      show(i) { f = i === null ? null : R.frames[i]; paint(); return f; },
      checks(i) { return checks(i === null ? null : R.frames[i]); },
      setGadgetColors(on) { colors = !!on; if (R) paint(); },
      resetHover() { hoverId = null; if (R) paint(); },
    };
  }

  return { create, createPair, parse, payload, frame, build, SOLVERS, REDUCTIONS, layout, textOf, _karp: karp, _naturalCompare: naturalCompare };
})();
