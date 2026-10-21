/* ---- Formula view: a formula drawn from its payload alone (proposed generalization, pending approval) ----
   The renderer knows no problem. The payload's `layout` picks the drawing:
     "rows"      groups as boxes joined by an OUTER operator, each group's content joined by an INNER operator (or given
                 as an expression tree); boxes flow left to right and wrap ("flow"), or take a line each ("stack").
     "cnf"/"dnf" presets of "rows": CNF = ∧ outside, ∨ inside; DNF = ∨ outside, ∧ inside.
     "equation"  each group is one expression on its own line(s), wrapped at its top-level operators.
     "tree"      reserved for nested formulas; drawn as "equation" until it is built.
   Operators are referenced by name and drawn from ONE table (OPS); an unknown name is written name(a, b).
   Every piece that can change state has a stable id, so frames, gadgets and the verifier can target it. A frame may also
   replace a group's expression (`exprs`), for solvers that build the formula as they go (Prime Factorization).
   Every <text> belongs to a piece's <g> (data-piece) and moves with it; picture-wide facts are in legend(P), drawn
   outside the SVG. SAT/3SAT keep the Grouped Graph's ids and frames (clause c{i}, literal occurrence c{i}-{j}, variable
   v:{name}), so the Grouped Graph, the Factor Graph and this picture color together. Also the SAT ↦ 3SAT Reduction View
   pair (Karp's 3SAT Reduction), both sides drawn as formulas. Pages supply --av-* tokens.

   Sections: styles · operators · payload model (normalize) · legend · layout · drawing · SAT/3SAT/DNF (parse, frames,
   reference solvers) · Prime Factorization · 0-1 ILP · Subset Sum · problem catalog · the picture · Karp's reduction pair. */
const FormulaView = (() => {
  const NS = "http://www.w3.org/2000/svg";
  const mk = (tag, attrs = {}, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; };
  const SUBS = "₀₁₂₃₄₅₆₇₈₉";
  const pretty = nm => { const m = /^([A-Za-z_]+)(\d+)$/.exec(nm); return m ? m[1] + m[2].split("").map(d => SUBS[d]).join("") : nm; };
  // Numbers as drawn: a real minus sign. Captions follow Redux's N0 format (thousands separators).
  const fmtNum = n => (n < 0 ? "−" + -n : String(n));
  const N0 = n => n.toLocaleString("en-US");

  const css = `
.fm-svg { width: 100%; height: auto; display: block; }
.fm-svg text { font-family: var(--av-mono); }
.fm-slabel { font-size: 11px; font-weight: 600; letter-spacing: .08em; fill: var(--av-muted); }
.fm-clause { cursor: pointer; }
.fm-clause:focus { outline: none; }
.fm-clause .box { fill: var(--av-bg); stroke: var(--av-stroke); stroke-width: 1.6; transition: fill .2s, stroke .2s; }
.fm-clause.fm-eq .box { fill: none; stroke: none; pointer-events: all; }
.fm-clause .cid { font-size: 11px; font-weight: 600; fill: var(--av-muted); dominant-baseline: central; }
.fm-clause .mark { font-size: 12px; font-weight: 700; dominant-baseline: central; }
.fm-clause .note { font-size: 11.5px; fill: var(--av-muted); dominant-baseline: central; }
.fm-clause.Covered .box { fill: var(--av-sol-fill); stroke: var(--av-sol); stroke-width: 2.2; }
.fm-clause.Covered .mark, .fm-clause.Covered .note { fill: var(--av-sol-ink); }
.fm-clause.False .box { fill: var(--av-surface); stroke: var(--av-stroke); stroke-dasharray: 2 3; }
.fm-clause.ElementHighlight .box { stroke: var(--av-hl); stroke-width: 3; }
.fm-clause.Rejected .box { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 3; }
.fm-clause.Rejected .cid, .fm-clause.Rejected .mark, .fm-clause.Rejected .note { fill: var(--av-rej-ink); }
.fm-clause.trace .box, .fm-clause:focus-visible .box { stroke: var(--av-hot); stroke-width: 3; }
.fm-clause.faint { filter: grayscale(1); } .fm-clause.faint .box { stroke-dasharray: 3 3; }
.fm-op { font-size: 14px; fill: var(--av-muted); text-anchor: middle; dominant-baseline: central; pointer-events: none; }
.fm-and { font-size: 17px; font-weight: 600; fill: var(--av-ink); text-anchor: middle; dominant-baseline: central; pointer-events: none; }
.fm-lit { cursor: pointer; }
.fm-lit:focus { outline: none; }
.fm-lit rect { fill: var(--av-bg); stroke: var(--av-stroke); stroke-width: 1.4; transition: fill .2s, stroke .2s; }
.fm-lit text { font-size: 15px; text-anchor: start; dominant-baseline: central; fill: var(--av-ink); pointer-events: none; }
.fm-lit.fm-num.Background rect { fill: none; stroke: none; }
.fm-lit.ElementHighlight rect { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; }
.fm-lit.Solution rect { fill: var(--av-sol); stroke: var(--av-sol); } .fm-lit.Solution text { fill: var(--av-on-sol); font-weight: 600; }
.fm-lit.Committed rect { fill: var(--av-com-fill); stroke: var(--av-com); stroke-width: 2.4; stroke-dasharray: 5 3; } .fm-lit.Committed text { fill: var(--av-com-ink); font-weight: 600; }
.fm-lit .strike { display: none; }
.fm-lit.False rect { fill: var(--av-surface); stroke: var(--av-stroke); stroke-dasharray: 2 3; } .fm-lit.False text { fill: var(--av-muted); }
.fm-lit.False .strike { display: inline; stroke: var(--av-muted); stroke-width: 1.6; }
.fm-lit.False.on-rej rect { stroke: var(--av-muted); }
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

  /* ---------- operators: the one shared table ---------- */

  // fix: "infix" (a ∨ b ∨ c, n-ary), "prefix" (¬a), "juxt" (2x, no symbol), "call" (Σ(a, b)). prec decides parentheses:
  // a child binding looser than its parent is wrapped; relations (prec 0) never chain unwrapped. assoc: a same-operator
  // child on the right needs no parentheses. rel: how the legend reads a relation.
  const OPS = {
    or:    { sym: "∨", fix: "infix", prec: 1, assoc: true, word: "or" },
    and:   { sym: "∧", fix: "infix", prec: 2, assoc: true, word: "and" },
    eq:    { sym: "=", fix: "infix", prec: 0, rel: "both sides are equal" },
    le:    { sym: "≤", fix: "infix", prec: 0, rel: "the left side is at most the right side" },
    ge:    { sym: "≥", fix: "infix", prec: 0, rel: "the left side is at least the right side" },
    lt:    { sym: "<", fix: "infix", prec: 0, rel: "the left side is less than the right side" },
    gt:    { sym: ">", fix: "infix", prec: 0, rel: "the left side is more than the right side" },
    plus:  { sym: "+", fix: "infix", prec: 3, assoc: true },
    minus: { sym: "−", fix: "infix", prec: 3 },
    times: { sym: "×", fix: "infix", prec: 4, assoc: true },
    mul:   { sym: "", fix: "juxt", prec: 4 },
    not:   { sym: "¬", fix: "prefix", prec: 6 },
    neg:   { sym: "−", fix: "prefix", prec: 6 },
    sum:   { sym: "Σ", fix: "call", prec: 9, says: "Σ(…) adds up what is inside" },
  };
  const opInfo = name => OPS[name] || { sym: String(name), fix: "call", prec: 9, unknown: true };
  // the layout presets: CNF and DNF are "rows" with their operators fixed
  const PRESETS = { cnf: { outer: "and", inner: "or", noun: "clause" }, dnf: { outer: "or", inner: "and", noun: "term" } };

  /* ---------- sizes ---------- */

  const CW = 9, PH = 30, PAD = 9, BH = PH + 2 * PAD, OR_W = 24, AND_W = 34, LABEL_H = 18, ROW_GAP = 16, VW = 66, VH = 30;
  const PRE_W = 12, PAREN_W = 10, COMMA_W = 12, OP_CW = 8.4; // operator text is 14 px mono: 8.4 per character
  const TOP = 10, MIN_W = 320, MAX_W = 800;
  // Labels: a name of letters then digits (x10) is drawn as its letters with the digits as a real subscript, at SUBF of the
  // label size; any other name (a, foo_2, 60) is drawn plain. Mono, so a width is characters × CW (CW at the 15 px size).
  const LIT_PX = 15, VAR_PX = 14, SUBF = 0.78;
  const split = nm => { const m = /^([A-Za-z]+)(\d+)$/.exec(nm); return m ? { base: m[1], sub: m[2] } : { base: nm, sub: "" }; };
  const labelW = (neg, nm, px = LIT_PX) => { const p = split(nm); return ((neg ? 1 : 0) + p.base.length + p.sub.length * SUBF) * CW * px / LIT_PX; };
  const pillW = (neg, nm) => Math.max(34, labelW(neg, nm) + 18);
  const numW = text => Math.max(22, labelW(false, text) + 12);
  /** Writes a label into a <text> drawn from its left edge x0: base (with ¬), then the subscript a little lower. */
  function drawLabel(t, neg, nm, px) {
    t.textContent = "";
    const p = split(nm), shift = +(0.32 * px).toFixed(1);
    mk("tspan", { class: "base" }, t).textContent = (neg ? "¬" : "") + p.base;
    if (p.sub) mk("tspan", { class: "sub", "font-size": +(SUBF * px).toFixed(1), dy: shift }, t).textContent = p.sub;
    return { p, shift: p.sub ? shift : 0 };
  }

  /* ---------- the payload model: any layout to groups of tokens ---------- */

  // An expression is a tree of
  //   { id, var: "v:x1" }                 a variable occurrence (a pill, labelled with the variable's label)
  //   { id, num: 7, variable?: "v:x2" }    a number (plain text until it has a state); `variable` ties it to a chip
  //   { id, label: "?", variable? }        a placeholder, drawn like a number
  //   { id?, op: "plus", args: [...] }     an operator node, by name; `not` over a variable occurrence folds into one ¬x pill
  // A group with `members` (the CNF/DNF literal list) is the same as an `inner` operator over its literals.
  // Tokens: pieces ({ t: "piece", kind: "lit"|"num", id, ... }) and symbols ({ t: "sym", text, w, owner, brk }). `brk` is
  // the nesting depth a line may break at, before that token: the formula wraps at its shallowest operators first.
  function exprTokens(expr, varIndex, gid) {
    const out = []; let auto = 0;
    const leaf = n => {
      if (n.node) { const neg = n.node.positive === false, name = neg ? String(n.node.label).slice(1) : String(n.node.label); return { t: "piece", kind: "lit", id: n.node.id, name, neg, text: String(n.node.label), variable: n.node.variable || null, w: pillW(neg, name) }; }
      if (n.var !== undefined) { const v = varIndex.get(n.var), name = String(n.label ?? (v ? v.label : n.var)); return { t: "piece", kind: "lit", id: n.id, name, neg: false, text: name, variable: n.var, w: pillW(false, name) }; }
      const text = n.num !== undefined && n.num !== null ? fmtNum(n.num) : String(n.label ?? "?");
      return { t: "piece", kind: "num", id: n.id, name: text, neg: false, text, variable: n.variable || null, w: numW(text) };
    };
    const sym = (text, w, owner, brk) => { const s = { t: "sym", text, w, owner }; if (brk !== undefined) s.brk = brk; out.push(s); return s; };
    function walk(n, depth) {
      if (!n.op) { out.push(leaf(n)); return; }
      const info = opInfo(n.op), owner = n.id || `${gid}/op${auto++}`, args = n.args || [];
      if (n.op === "not" && args.length === 1 && !args[0].op && (args[0].var !== undefined || args[0].node)) {
        const p = leaf(args[0]); p.neg = !p.neg; p.text = (p.neg ? "¬" : "") + p.name; p.w = pillW(p.neg, p.name); out.push(p); return;
      }
      const sub = (c, k) => {
        const ci = c.op ? opInfo(c.op) : null;
        let paren = false;
        if (ci && info.fix !== "call") {
          if (ci.prec < info.prec) paren = true;
          else if (ci.prec === info.prec && info.fix === "infix") paren = info.prec === 0 || (k > 0 && !(info.assoc && c.op === n.op));
          else if (ci.prec === info.prec && info.fix === "juxt") paren = k > 0;
        }
        // same-precedence chains (a + b − c) are one level, so they wrap together
        const d = info.fix === "call" || paren || (ci && ci.prec !== info.prec) ? depth + 1 : depth;
        if (paren) sym("(", PAREN_W, owner);
        walk(c, d);
        if (paren) sym(")", PAREN_W, owner);
      };
      if (info.fix === "infix") args.forEach((c, k) => { if (k) sym(info.sym, OR_W, owner, depth); sub(c, k); });
      else if (info.fix === "prefix") { sym(info.sym, PRE_W, owner); args.forEach(sub); }
      else if (info.fix === "juxt") args.forEach(sub);
      else {
        sym(info.sym + "(", info.sym.length * OP_CW + PAREN_W, owner);
        args.forEach((c, k) => {
          if (k) sym(",", COMMA_W, owner);
          const at = out.length;
          sub(c, k);
          if (k && out[at] && out[at].brk === undefined) out[at].brk = depth + 1; // a call wraps after its commas
        });
        sym(")", PAREN_W, owner);
      }
    }
    walk(expr, 0);
    return out;
  }

  const NORM = new WeakMap();
  /** The payload as layout reads it: { layout: "rows" | "equation", outer, inner, arrange, noun, groups: [{ id, label, tokens }], vars }. */
  function normalize(P) {
    if (NORM.has(P)) return NORM.get(P);
    const preset = PRESETS[P.layout] || null;
    const eq = P.layout === "equation" || P.layout === "tree";
    const outer = eq ? null : P.outer || (preset && preset.outer) || "and";
    const inner = P.inner || (preset && preset.inner) || "or";
    const vars = (P.variables || []).map(v => ({ ...v, type: v.type || "bool" }));
    const varIndex = new Map(vars.map(v => [v.id, v]));
    const nodeIndex = new Map((P.nodes || []).map(n => [n.id, n]));
    const groups = (P.groups || []).map(g => {
      let expr = g.expr;
      if (!expr) {
        const leaves = (g.members || []).map(id => nodeIndex.get(id)).filter(Boolean).map(node => ({ node }));
        expr = leaves.length === 1 ? leaves[0] : { id: g.id + "/inner", op: inner, args: leaves };
      }
      return { id: g.id, label: g.label ?? "", tokens: exprTokens(expr, varIndex, g.id), expr };
    });
    const M = { layout: eq ? "equation" : "rows", preset: preset ? P.layout : null, outer, inner, arrange: eq ? "stack" : P.arrange || "flow",
      noun: P.noun || (preset && preset.noun) || (eq ? "equation" : "group"), groups, vars, varIndex, flat: (P.groups || []).every(g => !g.expr) };
    NORM.set(P, M);
    return M;
  }

  /* ---------- the legend: how to read the picture, from the layout and operators (drawn outside the SVG) ---------- */

  function legend(P) {
    const M = normalize(P), out = [], sym = op => opInfo(op).sym, Noun = M.noun;
    const used = new Set(), walkOps = e => { if (e && e.op) { used.add(e.op); (e.args || []).forEach(walkOps); } };
    M.groups.forEach(g => walkOps(g.expr));
    if (M.layout === "rows") {
      const tag = M.preset ? ` (${M.preset.toUpperCase()})` : "";
      const all = M.outer === "and" ? `Every ${Noun} must hold` : M.outer === "or" ? `One ${Noun} that holds is enough` : `The ${Noun}s are combined by ${sym(M.outer)}`;
      if (M.flat) {
        const each = M.inner === "or" ? `a ${Noun} holds when one of its literals is true` : M.inner === "and" ? `a ${Noun} holds when all its literals are true` : `a ${Noun} is its literals combined by ${sym(M.inner)}`;
        out.push(`Boxes are ${Noun}s, joined by ${sym(M.outer)}; the literals inside are joined by ${sym(M.inner)}${tag}. ${all}: ${each}.`);
      } else out.push(`Each box is a ${Noun}, joined by ${sym(M.outer)}${M.arrange === "stack" ? ", one per line" : ""}. ${all}.`);
    } else {
      const rels = [...new Set(M.groups.map(g => g.expr && g.expr.op).filter(op => op && opInfo(op).rel))];
      const holds = rels.length === 1 ? `; it holds when ${opInfo(rels[0]).rel}` : "";
      out.push(`${M.groups.length > 1 ? "One equation per line" : "One equation"}, wrapped at its top-level operators${holds}.`);
    }
    const says = [...used].map(op => (OPS[op] ? OPS[op].says : `${op}(…) is the operator "${op}", written by name`)).filter(Boolean);
    if (says.length) out.push(says.join("; ") + ".");
    if (M.vars.length) {
      const types = new Set(M.vars.map(v => v.type));
      const what = types.has("int") && types.has("bool") ? "T, F or a number" : types.has("int") ? "a number" : "T or F";
      out.push(`The strip shows each variable's value (${what}; – while it is not set).`);
    }
    return out.join(" ");
  }

  /* ---------- layout: pure math, (payload, width) → boxes ---------- */

  /** Breaks a token list into lines no wider than maxW. A run that fits goes on the current line, or else starts the next;
      a run wider than a line is cut at its shallowest operators and each part placed the same way, so a formula breaks
      at its top-level operators first and inside a sub-expression only when that sub-expression can't fit a line. The
      operator at a break leads the new line. A flat CNF clause (one level) packs exactly as before. */
  function pack(tokens, maxW) {
    const levels = [...new Set(tokens.filter((t, i) => i && t.brk !== undefined).map(t => t.brk))].sort((a, b) => a - b);
    const lines = [[]]; let w = 0;
    const width = toks => toks.reduce((a, t) => a + t.w, 0);
    function place(toks, li) {
      const tw = width(toks), line = lines.at(-1);
      if (w + tw <= maxW || (!line.length && li >= levels.length)) { line.push(...toks); w += tw; return; }
      if (tw <= maxW || li >= levels.length) { if (line.length) { lines.push([]); w = 0; } lines.at(-1).push(...toks); w = tw; return; }
      const parts = [];
      toks.forEach((t, i) => { if (!parts.length || (i && t.brk !== undefined && t.brk <= levels[li])) parts.push([]); parts.at(-1).push(t); });
      parts.forEach(p => place(p, li + 1));
    }
    place(tokens, 0);
    return lines;
  }

  const valChars = v => {
    if (v.type !== "int") return 1;
    const r = v.range || [0, 999];
    return 2 + Math.max(...r.map(x => fmtNum(x).length));
  };

  function layout(P, width = MAX_W, top = TOP) {
    const M = normalize(P);
    width = Math.min(MAX_W, Math.max(MIN_W, width));
    const left = 12, right = width - 12;
    const joinSym = M.outer ? opInfo(M.outer).sym : null;
    const groups = [], pieces = new Map(), joins = [], ops = [];
    const maxInner = right - left - 2 * PAD - (M.layout === "equation" ? 0 : AND_W);
    let x = left, y = top, rowH = 0, rowStart = true;
    M.groups.forEach((g, i) => {
      const lines = pack(g.tokens, maxInner);
      const lineWs = lines.map(l => l.reduce((a, t) => a + t.w, 0));
      const bw = 2 * PAD + Math.max(0, ...lineWs), bh = BH + (lines.length - 1) * (PH + 8);
      if (M.arrange === "flow") {
        const need = (i ? AND_W : 0) + bw;
        if (!rowStart && x + need > right) { y += rowH + ROW_GAP; x = left; rowH = 0; rowStart = true; }
        if (i) { joins.push({ x: x + AND_W / 2, y: y + LABEL_H + BH / 2, text: joinSym, after: g.id }); x += AND_W; }
      } else {
        if (i) { y += rowH + ROW_GAP; rowH = 0; }
        x = left + (joinSym ? AND_W : 0);
        if (i && joinSym) joins.push({ x: left + AND_W / 2, y: y + LABEL_H + BH / 2, text: joinSym, after: g.id });
      }
      const box = { id: g.id, label: g.label, x, y: y + LABEL_H, w: bw, h: bh, members: [], plain: M.layout === "equation", lines: lines.length };
      lines.forEach((line, k) => {
        let px = x + PAD;
        const py = box.y + PAD + k * (PH + 8);
        line.forEach(t => {
          if (t.t === "sym") ops.push({ x: px + t.w / 2, y: py + PH / 2, text: t.text, group: g.id, owner: t.owner });
          else { pieces.set(t.id, { ...t, group: g.id, x: px, y: py, h: PH }); box.members.push(t.id); }
          px += t.w;
        });
      });
      groups.push(box);
      x += bw; rowH = Math.max(rowH, LABEL_H + bh); rowStart = false;
    });
    let vy = y + rowH + 40;
    const vars = [];
    let vx = left;
    M.vars.forEach(v => {
      const w = Math.max(VW, labelW(false, v.label, VAR_PX) + (1 + valChars(v)) * CW * VAR_PX / LIT_PX + 18);
      if (vx + w > right) { vx = left; vy += VH + 10; }
      vars.push({ ...v, x: vx, y: vy, w, h: VH });
      vx += w + 8;
    });
    const stripLabel = M.vars.every(v => v.type === "bool") ? "ASSIGNMENT" : "VALUES";
    return { M, width, groups, pieces, joins, ops, vars, stripLabel, stripY: y + rowH + 22, height: M.vars.length ? vy + VH + 14 : y + rowH + 14 };
  }

  /* ---------- drawing one formula into a group ---------- */

  // Every <text> is drawn inside the <g> of the piece it belongs to (data-piece): a group's label, mark, note, operator
  // symbols and the joining operator before it; a pill's label; the strip's label with its chips.
  function drawFormula(root, P, L, hookup) {
    const M = L.M, noun = M.noun, Noun = noun[0].toUpperCase() + noun.slice(1);
    const els = { clauses: new Map(), lits: new Map(), vars: new Map(), P, L };
    const word = t => (t === "∨" ? "or" : t === "∧" ? "and" : t);
    L.groups.forEach(c => {
      const mg = M.groups.find(g => g.id === c.id);
      const spoken = mg.tokens.map(t => (t.t === "sym" ? word(t.text) : t.text)).join(" ");
      const g = mk("g", { class: "fm-clause" + (c.plain ? " fm-eq" : ""), "data-piece": c.id, tabindex: "0", role: "button", "aria-label": `${Noun}${c.label ? " " + c.label : ""}: ${spoken}` }, root);
      const j = L.joins.find(a => a.after === c.id);
      if (j) mk("text", { x: j.x, y: j.y, class: "fm-and" }, g).textContent = j.text;
      mk("rect", { x: c.x, y: c.y, width: c.w, height: c.h, rx: 9, class: "box" }, g);
      mk("text", { x: c.x + 2, y: c.y - 9, class: "cid" }, g).textContent = c.label;
      const mx = c.x + 6 + c.label.length * 7.4;
      const mark = mk("text", { x: mx, y: c.y - 9, class: "mark" }, g);
      const note = mk("text", { x: mx + 16, y: c.y - 9, class: "note" }, g);
      L.ops.filter(o => o.group === c.id).forEach(o => { mk("text", { x: o.x, y: o.y, class: "fm-op", "data-op": o.owner }, g).textContent = o.text; });
      els.clauses.set(c.id, { g, mark, note, box: c });
      hookup(g, c.id);
    });
    L.pieces.forEach((m, id) => {
      const grp = L.groups.find(c => c.id === m.group);
      const g = mk("g", { class: "fm-lit" + (m.kind === "num" ? " fm-num" : ""), "data-piece": id, tabindex: "0", role: "button", "aria-label": `${m.text} in ${noun}${grp.label ? " " + grp.label : ""}` }, root);
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
    // the strip: its label is part of it, so it moves with it
    const strip = mk("g", { class: "fm-strip", "data-piece": "strip" }, root);
    if (L.vars.length) mk("text", { x: 12, y: L.stripY, class: "fm-slabel" }, strip).textContent = L.stripLabel;
    L.vars.forEach(v => {
      const g = mk("g", { class: "fm-var", "data-piece": v.id, tabindex: "0", role: "button", "aria-label": `Variable ${v.label}` }, strip);
      mk("rect", { x: v.x, y: v.y, width: v.w, height: v.h, rx: 7 }, g);
      const t = mk("text", { x: v.x + 9, y: v.y + v.h / 2, class: "lab" }, g);
      t.textContent = v.label + " –";
      els.vars.set(v.id, { g, t, v });
      hookup(g, v.id);
    });
    return els;
  }

  // What a hovered id lights up: a variable traces its pieces; a piece its variable's pieces; a group its pieces.
  function traceSet(P, id) {
    const hot = new Set([id]);
    if (!id) return hot;
    const M = normalize(P);
    const all = M.groups.flatMap(g => g.tokens.filter(t => t.t === "piece").map(t => ({ ...t, group: g.id })));
    const piece = all.find(t => t.id === id);
    const vid = M.varIndex.has(id) || id.startsWith("v:") ? id : piece ? piece.variable : null;
    if (vid) { hot.add(vid); all.forEach(t => { if (t.variable === vid) hot.add(t.id); }); }
    if (M.groups.some(g => g.id === id)) all.forEach(t => { if (t.group === id) { hot.add(t.id); if (t.variable) hot.add(t.variable); } });
    return hot;
  }

  function paintFormula(els, f, { hot = null, shown = null, colorOf = null } = {}) {
    // a state is a name here, or a { state } ref as the backend writes it
    const raw = (map, id) => f && f[map] && f[map][id];
    const st = (map, id) => { const v = raw(map, id); return (v && v.state) || v || "Background"; };
    els.clauses.forEach((o, id) => {
      const s = st("groups", id), v = raw("groups", id);
      let cls = "fm-clause" + (o.box.plain ? " fm-eq" : "") + " " + s;
      if (colorOf) { const c = colorOf(id); if (c !== null) cls += " g" + c; }
      if (hot) cls += hot.has(id) ? " trace" : " faint";
      if (shown && !shown.has(id)) cls += " fm-hidden";
      o.g.setAttribute("class", cls);
      o.mark.textContent = s === "Covered" ? "✓" : s === "Rejected" ? "✗" : "";
      o.note.textContent = (v && v.note) || "";
    });
    els.lits.forEach((o, id) => {
      const s = st("nodes", id);
      let cls = "fm-lit" + (o.m.kind === "num" ? " fm-num" : "") + " " + s;
      // a False piece inside a Rejected group (a 0 in a broken row): its dotted outline darkens to stay visible on the red fill
      if (s === "False" && st("groups", o.m.group) === "Rejected") cls += " on-rej";
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
      const val = o.v.type === "int" ? "= " + (a === undefined || a === null ? "–" : fmtNum(+a)) : a === undefined ? "–" : a ? "T" : "F";
      mk("tspan", { class: "val", dx: 7, ...(shift ? { dy: -shift } : {}) }, o.t).textContent = val;
    });
  }

  /* ================= SAT, 3SAT (CNF) and the DNF illustration ================= */

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

  /** Redux's SAT format, `(x1 | !x2 | x3) & (!x1 | x3)`, to { clauses (signed 1-based variable indices), names }.
      kind "DNF" reads the mirror image, `(x1 & !x2) | (!x1 & x3)`, for the layout illustration. */
  function parse(str, kind = "SAT") {
    const s = str.trim(), dnf = kind === "DNF", OUT = dnf ? "|" : "&", IN = dnf ? "&" : "|";
    if (!s) throw new Error(dnf ? "Enter a formula like (x1 & !x2) | (!x1 & x3)." : "Enter a formula like (x1 | !x2 | x3) & (!x1 | x3).");
    const written = s.split(OUT).map((part, i) => {
      const inner = part.trim().replace(/^\(/, "").replace(/\)$/, "");
      if (/[()]/.test(inner) || !inner.trim()) throw new Error(`${dnf ? "Term" : "Clause"} ${i + 1} is malformed.`);
      return inner.split(IN).map(tok => {
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
    const spec = { clauses: written.map(c => c.map(l => (l.neg ? -1 : 1) * index.get(l.name))), names };
    if (dnf) Object.assign(spec, { kind: "DNF", outer: "or", inner: "and" });
    return spec;
  }
  const isDnf = spec => spec.inner === "and";
  const textOf = spec => { const [o, i] = isDnf(spec) ? [" | ", " & "] : [" & ", " | "]; return spec.clauses.map(c => "(" + c.map(l => (l < 0 ? "!" : "") + spec.names[Math.abs(l) - 1]).join(i) + ")").join(o); };

  /** The payload: the Grouped Graph's groups, nodes and variables, without its edges. */
  function payload(spec) {
    const groups = [], nodes = [], dnf = isDnf(spec);
    spec.clauses.forEach((c, i) => {
      const members = [];
      c.forEach((l, j) => {
        const name = spec.names[Math.abs(l) - 1];
        members.push(litId(i, j));
        nodes.push({ id: litId(i, j), label: (l > 0 ? "" : "¬") + name, variable: varId(name), positive: l > 0, group: clauseId(i) });
      });
      groups.push({ id: clauseId(i), label: (dnf ? "D" : "C") + (i + 1), members });
    });
    return { kind: "formula", layout: dnf ? "dnf" : "cnf", shape: "assignment", rule: "satisfiability", groups, nodes, variables: spec.names.map(n => ({ id: varId(n), label: n })) };
  }

  /* ---------- frames: GroupedGraphFrames.Frame, plus the variables map ---------- */

  const RANK = ["Background", "False", "Blocked", "Covered", "Committed", "Solution", "ElementHighlight", "Rejected"];
  const raise = (map, id, st) => { if (RANK.indexOf(st) > RANK.indexOf(map[id] || "Background")) map[id] = st; };
  // GraphSubsetFrames.Picked: what a true literal (a pick) is in a step.
  const picked = (ev, incremental, endsRun, ok) =>
    ev === "Done" ? (ok ? "Solution" : "Rejected") : ev === "Accept" && endsRun ? "Solution" : (ev === "Try" || ev === "Reject") && !incremental ? "ElementHighlight" : "Committed";

  /** One step ({ event, partial: { name: bool }, focus: [ids], caption, ok? }) as the frame the backend would send.
      CNF: a clause with a true literal is Covered, one with every literal false Rejected (with its literals).
      DNF: a term with every literal true is Covered, one with a false literal False; when every term is False, all are Rejected. */
  function frame(spec, step, { incremental = false, endsRun = false, index = null } = {}) {
    const values = new Map(), assignment = {}, dnf = isDnf(spec);
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
      if (dnf) { if (c.length && trues === c.length) raise(groups, clauseId(i), "Covered"); else if (falses) raise(groups, clauseId(i), "False"); }
      else if (trues) raise(groups, clauseId(i), "Covered");
      else if (c.length && falses === c.length) { raise(groups, clauseId(i), "Rejected"); c.forEach((_, j) => raise(nodes, litId(i, j), "Rejected")); }
    });
    if (dnf && spec.clauses.length && spec.clauses.every((_, i) => groups[clauseId(i)] === "False")) spec.clauses.forEach((_, i) => raise(groups, clauseId(i), "Rejected"));
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

  // Backtracking with unit propagation (DPLL). Incremental: what it has decided so far is kept (Committed). CNF only.
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

  // Brute force: every whole assignment in turn. Not incremental, so each candidate is only highlighted. CNF or DNF.
  const SHOW_TRIES = 64;
  function solveBrute(spec) {
    const steps = [], n = spec.names.length, dnf = isDnf(spec);
    if (n > 12) return { steps: [{ event: "Done", partial: {}, focus: [], ok: false, caption: "Too many variables for the mockup to brute-force. Use the backtracking solver." }], incremental: false, ok: false, solution: null };
    let found = null, tried = 0, last = null;
    for (let m = 0; m < 1 << n; m++) {
      const asg = {}; for (let v = 1; v <= n; v++) asg[v] = !(m & (1 << (n - v)));
      last = asg; tried++;
      const txt = spec.names.map((_, i) => `${show(spec, i + 1)}=${asg[i + 1] ? "T" : "F"}`).join(" ");
      let good, step;
      if (dnf) {
        const win = spec.clauses.findIndex(c => c.every(l => valOf(asg, l) === true));
        good = win >= 0;
        step = good ? { event: "Accept", partial: toPartial(spec, asg), focus: [], caption: `Try ${txt}: every literal of D${win + 1} is true.` }
          : { event: "Reject", partial: toPartial(spec, asg), focus: [], caption: `Try ${txt}: every term has a false literal.` };
      } else {
        const bad = spec.clauses.findIndex(c => c.every(l => valOf(asg, l) === false));
        good = bad < 0;
        step = good ? { event: "Accept", partial: toPartial(spec, asg), focus: [], caption: `Try ${txt}: every clause has a true literal.` }
          : { event: "Reject", partial: toPartial(spec, asg), focus: [clauseId(bad)], caption: `Try ${txt}: C${bad + 1} is false.` };
      }
      if (tried <= SHOW_TRIES || good) steps.push(step);
      if (good) { found = asg; break; }
    }
    const hidden = tried > SHOW_TRIES ? ` (${tried - SHOW_TRIES} tries not shown)` : "";
    // The mockup's brute force ends an unsatisfiable run on its last candidate, to show that a Done with ok = false draws its picks Rejected.
    if (found) steps.push({ event: "Done", partial: toPartial(spec, found), focus: [], ok: true, caption: `Satisfiable, found after ${tried} tries${hidden}.` });
    else steps.push({ event: "Done", partial: toPartial(spec, last), focus: [], ok: false, caption: `Checked all ${tried} assignments${hidden}; none ${dnf ? "makes a term true" : "satisfies every clause"}. The last one tried is drawn red.` });
    return { steps, incremental: false, ok: !!found, solution: found };
  }

  const SOLVERS = {
    dpll: { label: "Backtracking (DPLL)", run: spec => solveDpll(spec) },
    brute: { label: "Brute Force", run: spec => solveBrute(spec) },
    stopped: { label: "Backtracking, stopped by the time limit", run: spec => solveDpll(spec, { limit: 5 }) },
  };

  function satChecks(spec, f) {
    const total = spec.clauses.length, a = f ? f.assignment : {};
    const covered = f ? Object.values(f.groups).filter(s => s === "Covered").length : 0;
    const failed = f ? Object.values(f.groups).filter(s => s === "Rejected").length : 0;
    if (isDnf(spec)) return [
      { label: `Terms with every literal true: ${covered} / ${total}`, ok: f ? covered > 0 : null },
      { label: `Variables assigned: ${Object.keys(a).length} / ${spec.names.length}`, ok: f ? Object.keys(a).length === spec.names.length : null },
    ];
    return [
      { label: `Clauses with a true literal: ${covered} / ${total}`, ok: f ? covered === total : null },
      { label: `Clauses with every literal false: ${failed}`, ok: f ? failed === 0 : null },
      { label: `Variables assigned: ${Object.keys(a).length} / ${spec.names.length}`, ok: f ? Object.keys(a).length === spec.names.length : null },
    ];
  }
  function satRun(spec, solver) {
    const run = (SOLVERS[solver] || SOLVERS.brute).run(spec);
    return { frames: [baseFrame("Step 0: the formula, nothing assigned. Step forward to watch the solver."), ...build(spec, run)], ok: run.ok, incremental: run.incremental,
      solution: run.solution ? spec.names.map((n, i) => `${n}=${run.solution[i + 1] ? "T" : "F"}`) : null };
  }

  /* ================= Prime Factorization: the equation layout ================= */

  // PrimeFactorSolver.Solve ("Prime Factorization Trial Division", local/viz-integration): for i = 2, 3, … while
  // i·i ≤ what is left, a Reject when i does not divide it, else an Accept for each division; then an Accept for a last
  // prime factor over 1, and Done. Incremental: the factors found are kept. Its real steps are QuantumProgress (the factors
  // so far); the Formula needs i and what is left too, so a Formula step shape would carry them.
  function parsePrime(str) {
    const s = str.trim().replace(/,/g, "");
    if (!/^\d+$/.test(s)) throw new Error("Enter a positive integer, like 60.");
    const n = +s;
    if (n < 2) throw new Error("Enter an integer of at least 2: 1 is the empty product.");
    if (n > 1e6) throw new Error("This mockup factors numbers up to 1,000,000.");
    return { n };
  }
  // the right side: the factors found so far, times what is left (dropped once it is a factor itself, or 1)
  const primeExpr = (n, factors, rest) => {
    const right = [...factors.map((f, k) => ({ id: "f" + k, num: f })), ...(rest === null ? [] : [{ id: "rest", num: rest, variable: "v:rest" }])];
    return { id: "eq", op: "eq", args: [{ id: "n", num: n }, right.length === 1 ? right[0] : { id: "prod", op: "times", args: right }] };
  };
  function primePayload(spec) {
    const n = spec.n;
    return { kind: "formula", layout: "equation", noun: "equation", shape: "values", rule: "factorization",
      groups: [{ id: "e0", label: "", expr: { id: "eq", op: "eq", args: [{ id: "n", num: n }, { id: "rest", label: "?", variable: "v:rest" }] } }],
      variables: [{ id: "v:i", label: "i", type: "int", range: [2, Math.floor(Math.sqrt(n)) + 1] }, { id: "v:rest", label: "rest", type: "int", range: [1, n] }] };
  }
  function solvePrime(spec) {
    const n = spec.n, factors = [], frames = [baseFrame(`Step 0: ${N0(n)}, not factored yet. Step forward to watch trial division.`)];
    let rest = n, i = 2;
    const push = (event, caption, { fresh = false, iState = null, restState = null, keepRest = true, done = false, focus = [] } = {}) => {
      const nodes = {};
      factors.forEach((_, k) => { nodes["f" + k] = done ? "Solution" : "Committed"; });
      if (fresh) nodes["f" + (factors.length - 1)] = "ElementHighlight";
      if (restState && keepRest) nodes.rest = restState;
      const variables = {};
      if (iState) variables["v:i"] = iState;
      if (restState) variables["v:rest"] = restState;
      frames.push({ event, caption, focus, step: frames.length - 1, nodes, groups: done ? { e0: "Covered" } : {}, assignment: { "v:i": i, "v:rest": rest }, variables,
        exprs: { e0: primeExpr(n, factors, keepRest ? rest : null) }, ...(done ? { ok: true } : {}), mock: { factors: [...factors], rest: keepRest ? rest : 1 } }); // mock: for the page's checks only, not part of the frame
    };
    while (i * i <= rest) {
      if (rest % i !== 0) push("Reject", `${N0(i)} does not divide ${N0(rest)}.`, { iState: "Rejected", focus: ["v:i"] });
      while (rest % i === 0) {
        const before = rest;
        factors.push(i); rest /= i;
        push("Accept", `${N0(before)} ÷ ${N0(i)} = ${N0(rest)}, so ${N0(i)} is a prime factor.`, { fresh: true, iState: "ElementHighlight", restState: "ElementHighlight", focus: ["f" + (factors.length - 1)] });
      }
      i++;
    }
    if (rest > 1) {
      factors.push(rest);
      push("Accept", factors.length === 1 ? `No number from 2 to √${N0(rest)} divides ${N0(rest)}, so ${N0(rest)} is prime.`
        : `No number from 2 to √${N0(rest)} divides ${N0(rest)}, so ${N0(rest)} is prime: the last factor.`, { fresh: true, keepRest: false, focus: ["f" + (factors.length - 1)] });
    }
    push("Done", factors.length === 1 ? `${N0(n)} is prime.` : `${N0(n)} = ${factors.map(N0).join(" × ")}.`, { keepRest: false, done: true });
    return { frames, ok: true, incremental: true, solution: [`(${factors.join(",")})`] };
  }
  function primeChecks(spec, f) {
    const m = f && f.mock;
    const prod = m ? m.factors.reduce((a, b) => a * b, 1) : null;
    return [
      { label: `Factors found: ${m && m.factors.length ? m.factors.join(" × ") : "none yet"}`, ok: null },
      { label: `Factors × what is left = ${N0(spec.n)}`, ok: m ? prod * m.rest === spec.n : null },
      { label: "Every factor is prime", ok: m ? m.factors.every(p => { for (let d = 2; d * d <= p; d++) if (p % d === 0) return false; return p > 1; }) : null },
    ];
  }

  /* ================= 0-1 Integer Linear Programming: stacked rows of linear constraints ================= */

  // Redux's format: (row1),…,(rowM)<=(d1 … dM), each row one row of C; every constraint is Cx ≤ d.
  function parseILP(str) {
    const m = /^(.*)<=(.*)$/.exec(str.trim());
    if (!m) throw new Error("Write it as (row1),…,(rowM)<=(d1 … dM), like (-1 1 -1),(0 0 -1),(-1 -1 1)<=(0 0 0).");
    const ints = t => t.replace(/[()]/g, " ").trim().split(/\s+/).filter(Boolean).map(x => { if (!/^-?\d+$/.test(x)) throw new Error(`"${x}" isn't an integer.`); return +x; });
    const C = m[1].split(",").map(ints), d = ints(m[2]);
    if (!C.length || C.some(r => !r.length)) throw new Error("Every row needs coefficients.");
    const n = C[0].length;
    if (C.some(r => r.length !== n)) throw new Error("Every row needs the same number of coefficients.");
    if (d.length !== C.length) throw new Error(`d has ${d.length} bound${d.length === 1 ? "" : "s"} for ${C.length} rows.`);
    if (n > 8) throw new Error("This mockup draws up to 8 variables.");
    if (C.length > 12) throw new Error("This mockup draws up to 12 constraints.");
    return { C, d, n };
  }
  const xv = j => "v:x" + (j + 1);
  // One row's left side as an expression: −x₁ + x₂ − 2x₃. Ids: r{i}-x{j} the occurrence of x_{j+1}, r{i}-c{j} its
  // coefficient (when not ±1), r{i}-d the bound.
  function ilpRow(row, i) {
    const terms = row.map((c, j) => ({ c, j })).filter(t => t.c);
    if (!terms.length) return { id: `r${i}-zero`, num: 0 };
    const term = (c, j) => {
      const x = { id: `r${i}-x${j}`, var: xv(j) };
      if (c === 1) return x;
      if (c === -1) return { id: `r${i}-n${j}`, op: "neg", args: [x] };
      return { id: `r${i}-m${j}`, op: "mul", args: [{ id: `r${i}-c${j}`, num: c, variable: xv(j) }, x] };
    };
    // a run of + (or of −) is one n-ary node; ids r{i}-s{k} for the k-th such node, left to right
    let e = term(terms[0].c, terms[0].j), k = 0, chain = false;
    terms.slice(1).forEach(t => {
      const op = t.c < 0 ? "minus" : "plus", rhs = term(Math.abs(t.c), t.j);
      if (chain && e.op === op) e.args.push(rhs); else { e = { id: `r${i}-s${k++}`, op, args: [e, rhs] }; chain = true; }
    });
    return e;
  }
  function ilpPayload(spec) {
    return { kind: "formula", layout: "rows", outer: "and", arrange: "stack", noun: "constraint", shape: "assignment", rule: "constraints",
      groups: spec.C.map((row, i) => ({ id: "r" + i, label: "R" + (i + 1), expr: { id: `r${i}-rel`, op: "le", args: [ilpRow(row, i), { id: `r${i}-d`, num: spec.d[i] }] } })),
      variables: Array.from({ length: spec.n }, (_, j) => ({ id: xv(j), label: "x" + (j + 1), type: "int", range: [0, 1] })) };
  }
  const ilpTotal = (row, x) => row.reduce((a, c, j) => a + c * x[j], 0);
  /** A try as a frame: rows that hold Covered, rows over their bound Rejected, each with its total as the note; a chosen
      variable's occurrences take the pick's state (Rejected inside a row it breaks), an unchosen one is False. */
  function ilpFrame(spec, x, { event, caption, focus = [], pick, ok, index }) {
    const nodes = {}, groups = {}, variables = {}, assignment = {};
    x.forEach((b, j) => { assignment[xv(j)] = b; variables[xv(j)] = b ? pick : "False"; });
    spec.C.forEach((row, i) => {
      const t = ilpTotal(row, x), holds = t <= spec.d[i];
      groups["r" + i] = { state: holds ? "Covered" : "Rejected", note: `total ${fmtNum(t)} ${holds ? "≤" : ">"} ${fmtNum(spec.d[i])}` };
      row.forEach((c, j) => {
        if (!c) return;
        const s = !x[j] ? "False" : holds ? pick : "Rejected";
        nodes[`r${i}-x${j}`] = s;
        if (Math.abs(c) !== 1) nodes[`r${i}-c${j}`] = s;
      });
    });
    const out = { event, caption, focus, step: index, nodes, groups, assignment, variables, mock: { x: [...x] } }; // mock: page checks only
    if (ok !== undefined) out.ok = ok;
    return out;
  }
  // IntegerProgrammingBruteForce.Solve (local/viz-integration): the 0/1 counter from all zeros, x₁ flipping fastest; a Reject
  // naming the rows over their bounds, or the Accept of the first assignment every row allows; Done with the answer, or with
  // the last assignment tried when none works (#905). Not incremental.
  function solveILP(spec) {
    const frames = [baseFrame("Step 0: the constraints, nothing tried yet. Step forward to watch the brute force.")];
    const x = Array(spec.n).fill(0), total = 2 ** spec.n;
    const cert = () => `(${x.join(" ")})`;
    let tried = 0, last = "";
    for (let k = 0; k < total; k++) {
      const c = cert(); last = c; tried++;
      const over = spec.C.map((_, r) => r).filter(r => ilpTotal(spec.C[r], x) > spec.d[r]);
      if (!over.length) {
        frames.push(ilpFrame(spec, x, { event: "Accept", caption: `Try ${c}: every row stays within its bound.`, pick: "Solution", index: frames.length - 1 }));
        frames.push(ilpFrame(spec, x, { event: "Done", caption: `${c} satisfies every row, found on try ${tried} of ${total}.`, pick: "Solution", ok: true, index: frames.length - 1 }));
        return { frames, ok: true, incremental: false, solution: [c] };
      }
      const part = r => `row ${r + 1} adds up to ${ilpTotal(spec.C[r], x)}, over its bound ${spec.d[r]}`;
      frames.push(ilpFrame(spec, x, { event: "Reject", caption: `Try ${c}: ${over.map(part).join("; ")}.`, focus: over.map(r => "r" + r), pick: "ElementHighlight", index: frames.length - 1 }));
      if (k + 1 < total) { for (let j = 0; j < x.length; j++) { if (x[j] === 0) { x[j] = 1; break; } x[j] = 0; } }
    }
    frames.push(ilpFrame(spec, x, { event: "Done", caption: `No 0-1 assignment satisfies every row: all ${tried} were tried, the last ${last}.`, pick: "Rejected", ok: false, index: frames.length - 1 }));
    return { frames, ok: false, incremental: false, solution: null };
  }
  function ilpChecks(spec, f) {
    const g = f && f.groups ? Object.values(f.groups).map(v => (v && v.state) || v) : [];
    const held = g.filter(s => s === "Covered").length, set = f && f.assignment ? Object.keys(f.assignment).length : 0;
    return [
      { label: `Constraints that hold: ${held} / ${spec.C.length}`, ok: f && set ? held === spec.C.length : null },
      { label: `Variables set to 0 or 1: ${set} / ${spec.n}`, ok: f ? set === spec.n : null },
    ];
  }

  /* ================= Subset Sum: Σ(chosen) = T ================= */

  function parseSubsetSum(str) {
    const m = /^\(\s*\{([^}]*)\}\s*,\s*(-?\d+)\s*\)$/.exec(str.trim());
    if (!m) throw new Error("Write it as ({S},T), like ({1,7,12,15},28).");
    const S = m[1].split(",").map(s => s.trim()).filter(Boolean).map(s => { if (!/^-?\d+$/.test(s)) throw new Error(`'${s}' is not a valid integer in S`); return +s; });
    if (!S.length) throw new Error("S needs at least one number.");
    if (S.length > 10) throw new Error("This mockup draws up to 10 numbers.");
    return { S, T: +m[2] };
  }
  // One equation: Σ over every number of S (ids s{i}, each tied to its chip x{i+1}) = T (id T).
  function subsetPayload(spec) {
    return { kind: "formula", layout: "equation", noun: "equation", shape: "assignment", rule: "subsetSum",
      groups: [{ id: "e0", label: "", expr: { id: "eq", op: "eq", args: [{ id: "sum", op: "sum", args: spec.S.map((a, i) => ({ id: "s" + i, num: a, variable: xv(i) })) }, { id: "T", num: spec.T }] } }],
      variables: spec.S.map((_, i) => ({ id: xv(i), label: "x" + (i + 1), type: "int", range: [0, 1] })) };
  }
  function subsetFrame(spec, chosen, { event, caption, pick, ok, index, blank = false }) {
    const nodes = {}, variables = {}, assignment = {}, groups = {};
    if (!blank) {
      const s = spec.S.reduce((a, v, i) => a + (chosen[i] ? v : 0), 0), holds = s === spec.T;
      spec.S.forEach((_, i) => { nodes["s" + i] = chosen[i] ? pick : "False"; variables[xv(i)] = chosen[i] ? pick : "False"; assignment[xv(i)] = chosen[i] ? 1 : 0; });
      groups.e0 = { state: holds ? "Covered" : "Rejected", note: `sum ${fmtNum(s)} ${holds ? "=" : "≠"} ${fmtNum(spec.T)}` };
    }
    const out = { event, caption, focus: chosen.map((c, i) => (c ? "s" + i : null)).filter(Boolean), step: index, nodes, groups, assignment, variables, mock: { chosen: [...chosen] } };
    if (ok !== undefined) out.ok = ok;
    return out;
  }
  // SubsetSumBruteForce.Solve (local/viz-integration): every subset in mask order from 1 (bit i picks the i-th number; the
  // empty set is skipped); an Accept for the subset that adds up to T, a Reject for every other; Done with that subset, or
  // with nothing picked when none works. Not incremental.
  function solveSubsetSum(spec) {
    const frames = [baseFrame("Step 0: the numbers and the target, nothing chosen yet. Step forward to watch the brute force.")];
    const picked = spec.S.map(() => false), n = spec.S.length;
    const next = () => { for (let i = 0; i < n; i++) { picked[i] = !picked[i]; if (picked[i]) return true; } return false; };
    const members = () => spec.S.filter((_, i) => picked[i]);
    const set = () => "{" + members().join(",") + "}";
    const adds = () => { const m = members(); return m.length === 0 ? "0" : m.length === 1 ? String(m[0]) : m.join(" + ") + " = " + m.reduce((a, b) => a + b, 0); };
    const tries = k => (k === 1 ? "1 subset" : `${k} subsets`);
    let tried = 0;
    while (next()) {
      tried++;
      const s = members().reduce((a, b) => a + b, 0);
      if (s === spec.T) {
        frames.push(subsetFrame(spec, picked, { event: "Accept", caption: `Try ${set()}: ${adds()}. That's T.`, pick: "Solution", index: frames.length - 1 }));
        frames.push(subsetFrame(spec, picked, { event: "Done", caption: `${set()} adds up to exactly ${spec.T}, found after ${tries(tried)}.`, pick: "Solution", ok: true, index: frames.length - 1 }));
        return { frames, ok: true, incremental: false, solution: [set()] };
      }
      frames.push(subsetFrame(spec, picked, { event: "Reject", caption: `Try ${set()}: ${adds()}, not ${spec.T}.`, pick: "ElementHighlight", index: frames.length - 1 }));
    }
    const empty = spec.T === 0;
    frames.push(subsetFrame(spec, picked, { event: "Done", blank: true, ok: empty, index: frames.length - 1,
      caption: empty ? `Checked all ${tries(tried)} that hold something. None adds up to ${spec.T}, but the empty set does, so it is the answer.` : `Checked all ${tries(tried)}. None adds up to ${spec.T}.` }));
    return { frames, ok: empty, incremental: false, solution: empty ? ["{}"] : null };
  }
  function subsetChecks(spec, f) {
    const c = f && f.mock ? f.mock.chosen : null;
    const s = c ? spec.S.reduce((a, v, i) => a + (c[i] ? v : 0), 0) : null;
    const blank = f && !Object.keys(f.groups || {}).length;
    return [
      { label: `Chosen: ${c && !blank ? "{" + spec.S.filter((_, i) => c[i]).join(", ") + "}" : "nothing yet"}`, ok: null },
      { label: `Sum of the chosen numbers: ${c && !blank ? s : "–"} (target ${spec.T})`, ok: c && !blank ? s === spec.T : null },
    ];
  }

  /* ================= the problem catalog the mockup page reads ================= */

  // Problem names, solver names and default instances are Redux's (Problems/NPComplete on CSharpAPI; steps from the solvers on
  // local/viz-integration). DNF is not a Redux problem: it only illustrates the dnf layout.
  const PROBLEMS = {
    SAT: { label: "SAT", parse: s => parse(s, "SAT"), payload, run: satRun, checks: satChecks, solvers: { dpll: SOLVERS.dpll.label, brute: SOLVERS.brute.label, stopped: SOLVERS.stopped.label }, solver: "dpll",
      info: "Redux format: <code>(x1 | !x2 | x3 | x4) &amp; (…)</code>. Clauses of any width. Layout <code>cnf</code>.",
      examples: [["Mixed widths", "(x1 | !x2 | x3 | !x4) & (!x1 | x2) & (!x3 | x4 | x5) & (!x2 | !x5) & (x4 | !x1)"],
        ["A long formula (wraps)", "(x1 | x2 | !x3) & (!x1 | x4) & (x2 | !x4 | x5 | x6) & (!x2 | x3) & (!x5 | !x6) & (x3 | x6 | !x7) & (x7 | !x8) & (!x3 | x8 | x1) & (x5 | x7) & (!x6 | !x7 | x8) & (x4 | !x8) & (!x4 | x6 | x2)"],
        ["Unsatisfiable", "(x1 | x2) & (x1 | !x2) & (!x1 | x2) & (!x1 | !x2)"]] },
    SAT3: { label: "3SAT", parse: s => parse(s, "SAT3"), payload, run: satRun, checks: satChecks, solvers: { dpll: SOLVERS.dpll.label, brute: SOLVERS.brute.label, stopped: SOLVERS.stopped.label }, solver: "dpll",
      info: "Redux format: <code>(x1 | !x2 | x3) &amp; (…)</code>. At most three literals per clause. Layout <code>cnf</code>.",
      examples: [["Redux default", "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)"],
        ["Needs backtracking", "(!x1 | x2 | x3) & (!x1 | !x2 | x4) & (!x1 | !x3 | !x4) & (!x1 | !x2 | x3) & (!x1 | x2 | x4) & (x1 | x2 | !x4) & (x1 | !x3 | x4)"],
        ["Unsatisfiable", "(x1 | x2 | x3) & (x1 | x2 | !x3) & (x1 | !x2 | x3) & (x1 | !x2 | !x3) & (!x1 | x2 | x3) & (!x1 | x2 | !x3) & (!x1 | !x2 | x3) & (!x1 | !x2 | !x3)"]] },
    DNF: { label: "DNF (layout illustration)", parse: s => parse(s, "DNF"), payload, run: satRun, checks: satChecks, solvers: { brute: "Brute Force (illustration)" }, solver: "brute",
      note: "Not a Redux problem: no Redux problem takes a formula in DNF. This shows the dnf layout, the mirror image of cnf (terms joined by ∨, literals inside joined by ∧), with a brute force written for the mockup.",
      info: "Mockup format: <code>(x1 &amp; !x2) | (…)</code>. Layout <code>dnf</code>.",
      examples: [["Three terms", "(x1 & !x2) | (!x1 & x3 & x2) | (x2 & !x3)"], ["Only the last term can be true", "(x1 & !x1) | (x2 & !x2 & x3) | (!x1 & !x2 & x3)"], ["Every term contradicts itself", "(x1 & !x1) | (x2 & !x2)"]] },
    PRIMEFACTOR: { label: "Prime Factorization", parse: parsePrime, payload: primePayload, run: spec => solvePrime(spec), checks: primeChecks, solvers: { trial: "Prime Factorization Trial Division" }, solver: "trial",
      note: "Steps follow Prime Factorization Trial Division as recorded on the viz branch (a Reject when i does not divide what is left, an Accept per division, an Accept for a last prime). Its recorded step carries only the factors so far; the strip's i and rest are what its loop holds, which a Formula step shape would have to add. Redux's default solver for this problem is Shor's (the Quantum Circuit picture).",
      info: "Redux format: one positive integer, like <code>60</code>. Layout <code>equation</code>; each frame replaces the right side (<code>exprs</code>).",
      examples: [["60 = 2 × 2 × 3 × 5", "60"], ["Redux default (12)", "12"], ["1,001: divisors that fail", "1001"], ["A prime: 97", "97"]] },
    INTPROGRAMMING01: { label: "0-1 Integer Linear Programming", parse: parseILP, payload: ilpPayload, run: spec => solveILP(spec), checks: ilpChecks, solvers: { brute: "0-1 ILP Brute Force" }, solver: "brute",
      note: "Steps follow 0-1 ILP Brute Force as recorded on the viz branch: every 0/1 vector from all zeros with x₁ flipping fastest, each checked against every row; with no answer the Done keeps the last try (#905).",
      info: "Redux format: <code>(row1),…,(rowM)&lt;=(d1 … dM)</code>, each constraint Cx ≤ d. Layout <code>rows</code>, joined by ∧, one per line.",
      examples: [["Redux default", "(-1 1 -1),(0 0 -1),(-1 -1 1)<=(0 0 0)"], ["Needs several tries", "(-1 -1 -1 -1),(1 1 0 0),(0 0 1 1),(1 0 1 0)<=(-2 1 1 1)"],
        ["Coefficients", "(2 3 -1),(-1 -1 0),(1 0 4)<=(5 -1 4)"], ["No 0-1 solution", "(-1 -1 0),(1 1 0),(0 0 1)<=(-2 1 1)"]] },
    SUBSETSUM: { label: "Subset Sum", parse: parseSubsetSum, payload: subsetPayload, run: spec => solveSubsetSum(spec), checks: subsetChecks, solvers: { brute: "Subset Sum Brute Force" }, solver: "brute",
      note: "Steps follow Subset Sum Brute Force as recorded on the viz branch: every non-empty subset in mask order (bit i picks the i-th number), stopping at the first that adds up to T; with none, the Done is blank. Subset Sum's default picture stays the Packing picture.",
      info: "Redux format: <code>({S},T)</code>, like <code>({1,7,12,15},28)</code>. Layout <code>equation</code> with Σ.",
      examples: [["Redux default", "({1,7,12,15},28)"], ["No subset adds up", "({3,5,9},7)"]] },
  };

  /* ================= the Formula picture ================= */

  // width: a number, or a function the picture asks before each layout (the box it is drawn in), clamped to MIN_W..MAX_W
  function create({ svg, width = MAX_W }) {
    addStyle();
    const W = () => Math.min(MAX_W, Math.max(MIN_W, (typeof width === "function" ? width() : width) || MAX_W));
    svg.classList.add("fm-svg");
    let prob = null, spec, P, Pdrawn, drawnKey = null, frames, frame0 = null, els, hover = null;

    const hookup = (g, id) => {
      const on = () => { hover = id; paint(); }, off = () => { hover = null; paint(); };
      g.addEventListener("pointerenter", on); g.addEventListener("pointerleave", off);
      g.addEventListener("focus", on); g.addEventListener("blur", off);
      g.addEventListener("click", () => { hover = hover === id ? null : id; paint(); });
    };
    // a frame may replace a group's expression (a solver that builds the formula as it goes)
    function effective(f) { return f && f.exprs ? { ...P, groups: P.groups.map(g => (f.exprs[g.id] ? { ...g, expr: f.exprs[g.id] } : g)) } : P; }
    function draw() {
      const w = W(), Pe = effective(frame0), key = w + ":" + (Pe === P ? "base" : JSON.stringify(Pe.groups.map(g => g.expr || null)));
      if (key === drawnKey && els) return;
      svg.innerHTML = "";
      const L = layout(Pe, w);
      svg.setAttribute("viewBox", `0 0 ${L.width} ${L.height}`);
      els = drawFormula(svg, Pe, L, hookup);
      Pdrawn = Pe; drawnKey = key;
    }
    function paint() { paintFormula(els, frame0, { hot: hover ? traceSet(Pdrawn, hover) : null }); }
    function fresh() { frame0 = null; hover = null; drawnKey = null; els = null; draw(); paint(); }

    return {
      // kind: a key of PROBLEMS ("SAT", "SAT3", "DNF", "PRIMEFACTOR", "INTPROGRAMMING01", "SUBSETSUM"); solver: one of its solvers
      load(str, k = "SAT", solver) {
        prob = PROBLEMS[k]; spec = prob.parse(str); P = prob.payload(spec);
        const run = prob.run(spec, solver && prob.solvers[solver] ? solver : prob.solver);
        frames = run.frames;
        fresh();
        return { frames, ok: run.ok, solution: run.solution, incremental: run.incremental, payload: P, legend: legend(P) };
      },
      // Viewer hook: draw a payload and frames built elsewhere (the backend's), without running a mockup solver.
      loadFrames(payloadIn, fr) { prob = null; P = payloadIn; spec = null; frames = fr; fresh(); return { frames, legend: legend(P) }; },
      show(i) { frame0 = i === null ? null : frames[i]; draw(); paint(); return frame0; },
      checks(i) {
        const f = i === null ? null : frames[i];
        if (prob) return prob.checks(spec, f);
        const g = f ? Object.values(f.groups || {}).map(v => (v && v.state) || v) : [];
        return [{ label: `Groups that hold: ${g.filter(s => s === "Covered").length} / ${P.groups.length}`, ok: null }];
      },
      legend() { return P ? legend(P) : ""; },
      // the box changed width: lay out again (a no-op when the clamped width is the same)
      relayout() { if (P) { draw(); paint(); } },
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
    const W = 1080, SW = 500, TX = 560, TW = 520, PTOP = 44;

    function draw() {
      svg.innerHTML = "";
      const Ls = layout(R.Ps, SW, PTOP), Lt = layout(R.Pt, TW, PTOP);
      const H = Math.max(Ls.height, Lt.height, 300);
      svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
      const hook = side => (g, id) => {
        const key = side + id;
        const on = () => { hoverId = key; paint(); }, off = () => { hoverId = null; paint(); };
        g.addEventListener("pointerenter", on); g.addEventListener("pointerleave", off);
        g.addEventListener("focus", on); g.addEventListener("blur", off);
        g.addEventListener("click", () => { hoverId = hoverId === key ? null : key; paint(); });
      };
      // each pane's title is part of its pane, and the ↦ part of the separator, so each moves with what it names
      const gs = mk("g", { class: "fm-pane", "data-piece": "pane:s" }, svg), gt = mk("g", { class: "fm-pane", "data-piece": "pane:t", transform: `translate(${TX},0)` }, svg);
      mk("text", { x: 12, y: 24, class: "fm-pane-title" }, gs).textContent = `${red.fromLabel.toUpperCase()} · THE INSTANCE YOU GAVE`;
      mk("text", { x: 12, y: 24, class: "fm-pane-title" }, gt).textContent = `${red.toLabel.toUpperCase()} · BUILT BY THE REDUCTION`;
      const sep = mk("g", { class: "fm-sep", "data-piece": "sep" }, svg);
      mk("line", { class: "rd-sep", x1: TX - 30, y1: 40, x2: TX - 30, y2: H - 20, stroke: "var(--av-line)", "stroke-dasharray": "2 5" }, sep);
      mk("text", { x: TX - 30, y: H / 2 - 12, class: "fm-arrow" }, sep).textContent = "↦";
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
        return { frames: R.frames, ok: R.frames.at(-1).ok, targetInstance: R.targetInstance, gadgets: R.gadgets, reduction: red, legend: legend(R.Ps) };
      },
      show(i) { f = i === null ? null : R.frames[i]; paint(); return f; },
      checks(i) { return checks(i === null ? null : R.frames[i]); },
      setGadgetColors(on) { colors = !!on; if (R) paint(); },
      resetHover() { hoverId = null; if (R) paint(); },
    };
  }

  return { create, createPair, parse, payload, frame, build, SOLVERS, PROBLEMS, REDUCTIONS, OPS, layout, legend, normalize, textOf,
    MIN_W, MAX_W, _karp: karp, _naturalCompare: naturalCompare, _pack: pack, _exprTokens: exprTokens };
})();
