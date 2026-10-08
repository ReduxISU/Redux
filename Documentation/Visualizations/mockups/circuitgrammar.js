/* ---- Circuits & Grammars: Boolean Circuit (Circuit Value, Circuit-SAT) and Grammar (CYK membership) ----
   Not in Redux yet. Same API as the other views: create({svg}) → load / show / checks / chosen / shape / resetHover.
   Each problem supplies parse + solvers (frames in the problem's own terms) + draw + paint. Pages supply --av-* tokens. */
const CircuitGrammarView = (() => {
  const NS = "http://www.w3.org/2000/svg";
  const mk = (tag, attrs = {}, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; };
  const SHOW = 150;

  const css = `
.cg-svg { width: 100%; height: auto; display: block; }
.cg-svg text { font-family: var(--av-mono); }
.cg-gate { cursor: pointer; transition: opacity .2s; }
.cg-gate:focus { outline: none; }
.cg-gate .body { fill: var(--av-bg); stroke: var(--av-stroke); stroke-width: 1.8; transition: fill .2s, stroke .2s; }
.cg-gate .bub { fill: var(--av-bg); stroke: var(--av-stroke); stroke-width: 1.6; }
.cg-gate .nm { font-size: 11px; fill: var(--av-muted); text-anchor: middle; pointer-events: none; }
.cg-gate .in-nm { font-size: 12.5px; fill: var(--av-ink); text-anchor: middle; dominant-baseline: central; pointer-events: none; }
.cg-gate.Active .body, .cg-gate.Active .bub { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; }
.cg-gate.Solution .body, .cg-gate.Solution .bub { fill: var(--av-sol-fill); stroke: var(--av-sol); stroke-width: 3; }
.cg-gate.Rejected .body, .cg-gate.Rejected .bub { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 3; }
.cg-gate.trace .body, .cg-gate:focus-visible .body { stroke: var(--av-hot); stroke-width: 3; }
.cg-gate.faint { filter: grayscale(1); }
.cg-val { font-size: 12px; font-weight: 700; dominant-baseline: central; fill: var(--av-ink); }
.cg-val.v0 { font-weight: 400; fill: var(--av-muted); }
.cg-val.Solution { fill: var(--av-sol); } .cg-val.Rejected { fill: var(--av-rej); }
.cg-wire { fill: none; stroke: var(--av-edge); stroke-width: 1.5; transition: opacity .2s, stroke .2s, stroke-width .2s; }
.cg-wire.v1 { stroke: var(--av-ink); stroke-width: 3.2; }
.cg-wire.v0 { stroke: var(--av-muted); stroke-width: 1.4; stroke-dasharray: 5 4; }
.cg-wire.hot { stroke: var(--av-hl); }
.cg-wire.trace { stroke: var(--av-hot); stroke-width: 2.8; opacity: 1; }
.cg-wire.faint { stroke: var(--av-edge-dim); }
.cg-out { stroke: var(--av-ink); stroke-width: 1.6; }
.cg-out-l { font-size: 11px; fill: var(--av-muted); dominant-baseline: central; }
.cg-m { fill: var(--av-ink); }
.cg-cell { cursor: pointer; transition: opacity .2s; }
.cg-cell:focus { outline: none; }
.cg-cell rect { fill: var(--av-bg); stroke: var(--av-stroke); stroke-width: 1.4; transition: fill .2s, stroke .2s; }
.cg-cell text { font-size: 12.5px; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); pointer-events: none; }
.cg-cell.unset rect { fill: transparent; stroke-dasharray: 3 3; }
.cg-cell.empty text { fill: var(--av-muted); }
.cg-cell.Active rect { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; }
.cg-cell.split rect { stroke: var(--av-hl); stroke-width: 2.4; stroke-dasharray: 5 3; }
.cg-cell.used rect { stroke: var(--av-sol); stroke-width: 2.6; }
.cg-cell.Solution rect { fill: var(--av-sol-fill); stroke: var(--av-sol); stroke-width: 3; }
.cg-cell.Rejected rect { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 3; } .cg-cell.Rejected text { fill: var(--av-rej); }
.cg-cell.trace rect, .cg-cell:focus-visible rect { stroke: var(--av-hot); stroke-width: 3; }
.cg-cell.faint { filter: grayscale(1); }
.cg-ch { font-size: 15px; font-weight: 600; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); }
.cg-ch.lit { fill: var(--av-hot); }
.cg-rowl { font-size: 10.5px; fill: var(--av-muted); text-anchor: end; dominant-baseline: central; }
.cg-head { font-size: 11px; font-weight: 600; letter-spacing: .08em; fill: var(--av-muted); }
.cg-tn circle { fill: var(--av-sol-fill); stroke: var(--av-sol); stroke-width: 2; }
.cg-tn.leaf circle { fill: var(--av-bg); stroke: var(--av-stroke); }
.cg-tn text { font-size: 12.5px; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); pointer-events: none; }
.cg-tn.new circle { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; }
.cg-tn { cursor: pointer; }
.cg-tn.trace circle { stroke: var(--av-hot); stroke-width: 3; }
.cg-te { stroke: var(--av-sol); stroke-width: 1.8; fill: none; }
.cg-note { font-size: 11.5px; fill: var(--av-muted); }
@media (prefers-reduced-motion: reduce) { .cg-gate, .cg-gate .body, .cg-wire, .cg-cell, .cg-cell rect { transition: none; } }`;

  /* =================== Boolean Circuit =================== */
  const OPS = { AND: 2, OR: 2, XOR: 2, NAND: 2, NOR: 2, NOT: 1 };
  // three-valued (Kleene) gate logic: 0, 1, or undefined when it can't be known yet
  function gateVal(op, xs) {
    const has0 = xs.includes(0), has1 = xs.includes(1), allKnown = xs.every(v => v !== undefined);
    let v;
    if (op === "AND" || op === "NAND") v = has0 ? 0 : allKnown ? 1 : undefined;
    else if (op === "OR" || op === "NOR") v = has1 ? 1 : allKnown ? 0 : undefined;
    else if (op === "XOR") v = allKnown ? xs.reduce((a, b) => a ^ b, 0) : undefined;
    else if (op === "NOT") v = xs[0] === undefined ? undefined : 1 - xs[0];
    if ((op === "NAND" || op === "NOR") && v !== undefined) v = 1 - v;
    return v;
  }
  const fmtGate = (g, vals) => `${g.name} = ${g.op}(${g.ins.map(n => vals && vals[n] !== undefined ? `${n}=${vals[n]}` : n).join(", ")})`;

  function parseCircuit(str, withAssign) {
    const s = str.replace(/\s+/g, "");
    let m, X, G, out, A = null;
    if (withAssign) {
      m = /^\(\(\{([^{}]*)\},\{(.*)\},([^,{}()]+)\),\{(.*)\}\)$/.exec(s);
      if (!m) throw new Error("Expected ((X, G, o), A): inputs, gates, the output gate, then an input assignment like {(x1,1),(x2,0)}.");
      [, X, G, out, A] = m;
    } else {
      m = /^\(\{([^{}]*)\},\{(.*)\},([^,{}()]+)\)$/.exec(s);
      if (!m) throw new Error("Expected (X, G, o): inputs, gates and the output gate.");
      [, X, G, out] = m;
    }
    const inputs = X ? X.split(",") : [];
    if (!inputs.length || new Set(inputs).size !== inputs.length) throw new Error("X must be a non-empty set of input names with no repeats.");
    if (inputs.length > 10) throw new Error("This mockup draws up to 10 inputs.");
    const re = /\(([^()]*)\)/g;
    if (G.replace(re, "").replace(/,/g, "")) throw new Error("Each gate must look like (name,OP,in1,in2).");
    const gates = [];
    let t;
    while ((t = re.exec(G))) {
      const parts = t[1].split(",");
      const [name, opRaw, ...ins] = parts;
      const op = (opRaw || "").toUpperCase();
      if (!name || !(op in OPS)) throw new Error(`Gate (${t[1]}) needs a name and one of ${Object.keys(OPS).join(", ")}.`);
      if (op === "NOT" && ins.length !== 1) throw new Error(`NOT gate ${name} takes exactly one input.`);
      if (op !== "NOT" && ins.length < 2) throw new Error(`${op} gate ${name} needs at least two inputs.`);
      gates.push({ name, op, ins });
    }
    if (!gates.length) throw new Error("The circuit needs at least one gate.");
    if (gates.length > 24) throw new Error("This mockup draws up to 24 gates.");
    const names = new Set(inputs);
    for (const g of gates) { if (names.has(g.name)) throw new Error(`The name ${g.name} is used twice.`); names.add(g.name); }
    for (const g of gates) for (const i of g.ins) if (!names.has(i)) throw new Error(`Gate ${g.name} reads ${i}, which isn't an input or a gate.`);
    if (!gates.some(g => g.name === out)) throw new Error(`The output ${out} isn't one of the gates.`);
    // depth by longest path; a cycle never settles
    const byName = new Map(gates.map(g => [g.name, g])), depth = new Map(inputs.map(x => [x, 0])), visiting = new Set();
    const dep = n => {
      if (depth.has(n)) return depth.get(n);
      if (visiting.has(n)) throw new Error(`The gates loop back on themselves at ${n}. A circuit can't have cycles.`);
      visiting.add(n);
      const d = 1 + Math.max(...byName.get(n).ins.map(dep));
      visiting.delete(n); depth.set(n, d); return d;
    };
    gates.forEach(g => dep(g.name));
    const order = gates.slice().sort((a, b) => depth.get(a.name) - depth.get(b.name) || gates.indexOf(a) - gates.indexOf(b));
    let assign = null;
    if (withAssign) {
      assign = {};
      const ra = /\(([^,()]+),([^,()]+)\)/g;
      if (A.replace(ra, "").replace(/,/g, "")) throw new Error("The assignment must look like {(x1,1),(x2,0)}.");
      while ((t = ra.exec(A))) {
        if (!inputs.includes(t[1])) throw new Error(`${t[1]} isn't an input.`);
        if (t[2] !== "0" && t[2] !== "1") throw new Error(`Input ${t[1]} must be 0 or 1.`);
        assign[t[1]] = +t[2];
      }
      const miss = inputs.filter(x => assign[x] === undefined);
      if (miss.length) throw new Error(`No value given for ${miss.join(", ")}.`);
    }
    return { inputs, gates, order, out, depth, assign, byName };
  }
  function evaluate(C, assign) {
    const vals = { ...assign };
    for (const g of C.order) vals[g.name] = gateVal(g.op, g.ins.map(i => vals[i]));
    return vals;
  }
  const fmtAssign = (C, a) => C.inputs.map(x => `${x}=${a[x] === undefined ? "?" : a[x]}`).join(", ");

  function cvpSolve(C) {
    const frames = [], vals = { ...C.assign };
    frames.push({ vals: { ...vals }, caption: `Step 0: the inputs are set (${fmtAssign(C, C.assign)}). Step forward to evaluate one gate at a time, left to right.` });
    for (const g of C.order) {
      const before = { ...vals };
      vals[g.name] = gateVal(g.op, g.ins.map(i => vals[i]));
      frames.push({ vals: { ...vals }, active: g.name, caption: `${fmtGate(g, before)} gives ${vals[g.name]}.` });
    }
    const v = vals[C.out];
    frames.push({ vals: { ...vals }, done: true, ok: v === 1, caption: `The output ${C.out} is ${v}${v === 1 ? ", so the answer is yes" : ", so the answer is no"}. Every gate was evaluated once, in order: a polynomial-time run.` });
    return frames;
  }
  function csatBrute(C) {
    const frames = [{ vals: {}, assign: {}, caption: "Step 0: no inputs set yet. Brute force tries every assignment in binary order." }], n = C.inputs.length;
    let tried = 0, found = null;
    for (let mask = 0; mask < 1 << n; mask++) {
      const a = {};
      C.inputs.forEach((x, i) => (a[x] = (mask >> (n - 1 - i)) & 1));
      const vals = evaluate(C, a);
      tried++;
      const good = vals[C.out] === 1;
      if (tried <= SHOW) frames.push({ vals, assign: a, trial: !good, caption: `Try ${fmtAssign(C, a)}: the output ${C.out} is ${vals[C.out]}.${good ? " It works." : ""}` });
      if (good) { found = a; break; }
    }
    const hidden = tried > SHOW ? ` (${tried - SHOW} tries not shown)` : "";
    if (found) frames.push({ vals: evaluate(C, found), assign: found, done: true, ok: true, caption: `${fmtAssign(C, found)} makes the output 1, found after ${tried} of ${2 ** n} assignments${hidden}. Satisfiable.` });
    else frames.push({ vals: {}, assign: {}, done: true, ok: false, caption: `All ${2 ** n} assignments give output 0${hidden}. Unsatisfiable.` });
    return frames;
  }
  function csatBacktrack(C) {
    const frames = [{ vals: {}, assign: {}, caption: "Step 0: no inputs set yet. Each choice is evaluated as far as it can go; a branch stops as soon as the output is forced to 0." }];
    const a = {};
    let count = 0, found = null, early = [];
    function rec(i) {
      if (i === C.inputs.length) { const v = evaluate(C, a); if (v[C.out] === 1) { found = { ...a }; return true; } return false; }
      const x = C.inputs[i];
      for (const b of [0, 1]) {
        a[x] = b;
        const vals = evaluate(C, a), o = vals[C.out];
        const tail = o === 0 ? ` That already forces ${C.out} to 0, so this branch is pruned.` : o === 1 ? ` That already forces ${C.out} to 1.` : ` ${C.out} is still unknown.`;
        if (++count <= SHOW) frames.push({ vals, assign: { ...a }, active: null, trial: o === 0, pruned: o === 0, caption: `Set ${x} = ${b} (${fmtAssign(C, a)}).${tail}` });
        if (o === 1) { found = { ...a }; early = C.inputs.slice(i + 1); early.forEach(y => (found[y] = 0)); return true; }
        if (o !== 0 && rec(i + 1)) return true;
        delete a[x];
      }
      return false;
    }
    rec(0);
    const hidden = count > SHOW ? ` (${count - SHOW} steps not shown)` : "";
    if (found) frames.push({ vals: evaluate(C, found), assign: found, done: true, ok: true, caption: `${fmtAssign(C, found)} makes the output 1, after ${count} choices${hidden}.${early.length ? ` ${early.join(", ")} ${early.length > 1 ? "don't" : "doesn't"} matter, so ${early.length > 1 ? "they're" : "it's"} set to 0.` : ""} Satisfiable.` });
    else frames.push({ vals: {}, assign: {}, done: true, ok: false, caption: `Every branch was pruned${hidden}. Unsatisfiable.` });
    return frames;
  }

  // gate glyph: path, input-pin x offset, output-pin x, bubble?
  const GW = 46, GH = 36;
  function glyph(op, x, y) {
    const w = GW, h = GH, cy = y + h / 2;
    if (op === "NOT") return { d: `M${x},${y + 3} L${x + w - 12},${cy} L${x},${y + h - 3} Z`, pinX: x, outX: x + w - 4, bubble: [x + w - 8, cy] };
    if (op === "AND" || op === "NAND") return { d: `M${x},${y} H${x + w * 0.5} A${h / 2},${h / 2} 0 0 1 ${x + w * 0.5},${y + h} H${x} Z`, pinX: x, outX: op === "NAND" ? x + w * 0.5 + h / 2 + 8 : x + w * 0.5 + h / 2, bubble: op === "NAND" ? [x + w * 0.5 + h / 2 + 4, cy] : null };
    const ox = op === "XOR" ? x + 7 : x;
    const body = `M${ox},${y} Q${ox + w * 0.3},${cy} ${ox},${y + h} Q${ox + w * 0.62},${y + h} ${x + w},${cy} Q${ox + w * 0.62},${y} ${ox},${y} Z`;
    const extra = op === "XOR" ? ` M${x},${y} Q${x + w * 0.3},${cy} ${x},${y + h}` : "";
    return { d: body + extra, pinX: x + 7, outX: op === "NOR" ? x + w + 8 : x + w, bubble: op === "NOR" ? [x + w + 4, cy] : null };
  }

  function drawCircuit(svg, C, api) {
    svg.innerHTML = "";
    const maxD = Math.max(...[...C.depth.values()]);
    const cols = Array.from({ length: maxD + 1 }, () => []);
    C.inputs.forEach(x => cols[0].push(x));
    C.order.forEach(g => cols[C.depth.get(g.name)].push(g.name));
    // order within columns by the average row of what feeds each gate (two sweeps)
    const rowOf = new Map();
    cols.forEach(c => c.forEach((n, i) => rowOf.set(n, i)));
    for (let sweep = 0; sweep < 2; sweep++) for (let d = 1; d <= maxD; d++) {
      cols[d].sort((a, b) => {
        const m = n => { const ins = C.byName.get(n).ins; return ins.reduce((s, i) => s + rowOf.get(i) / Math.max(1, cols[C.depth.get(i)].length), 0) / ins.length; };
        return m(a) - m(b);
      });
      cols[d].forEach((n, i) => rowOf.set(n, i));
    }
    const DX = 150, DY = 74, x0 = 60;
    const rows = Math.max(...cols.map(c => c.length));
    const W = Math.max(640, x0 + maxD * DX + 150), H = rows * DY + 70;
    svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
    const defs = mk("defs", {}, svg);
    const mkr = mk("marker", { id: "cg-arrow", viewBox: "0 0 10 10", refX: 9, refY: 5, markerWidth: 9, markerHeight: 9, markerUnits: "userSpaceOnUse", orient: "auto" }, defs);
    mk("path", { d: "M0,0 L10,5 L0,10 z", class: "cg-m" }, mkr);
    const pos = new Map();
    cols.forEach((c, d) => c.forEach((n, i) => pos.set(n, { x: x0 + d * DX, y: 40 + ((rows - c.length) * DY) / 2 + i * DY + DY / 2 })));
    const gW = mk("g", {}, svg), gN = mk("g", {}, svg);
    const els = { gates: new Map(), wires: [], vals: new Map(), C };
    // output pins
    const outPin = new Map();
    C.inputs.forEach(x => { const p = pos.get(x); outPin.set(x, [p.x + 24, p.y]); });
    const geo = new Map();
    C.gates.forEach(g => { const p = pos.get(g.name), gg = glyph(g.op, p.x - GW / 2, p.y - GH / 2); geo.set(g.name, gg); outPin.set(g.name, [gg.outX, p.y]); });
    // wires: each source feeding a column gets its own vertical trunk (a lane) just left of that column,
    // so fan-out from one source reads as one branching wire and different sources never share a leg
    const lanes = new Map();
    for (let d = 1; d <= maxD; d++) {
      const srcs = [];
      cols[d].forEach(n => C.byName.get(n).ins.forEach(s => { if (!srcs.includes(s)) srcs.push(s); }));
      srcs.sort((a, b) => outPin.get(a)[1] - outPin.get(b)[1]);
      srcs.forEach((s, i) => lanes.set(d + "|" + s, i));
    }
    C.gates.forEach(g => {
      const p = pos.get(g.name), gg = geo.get(g.name), k = g.ins.length, d = C.depth.get(g.name);
      g.ins.forEach((src, j) => {
        const [sx, sy] = outPin.get(src);
        const ty = p.y - GH / 2 + (GH * (j + 1)) / (k + 1);
        const tx = gg.pinX;
        const colLeft = x0 + d * DX - GW / 2;
        const mx = Math.max(sx + 10, colLeft - 14 - lanes.get(d + "|" + src) * 9);
        const pd = Math.abs(sy - ty) < 0.5 ? `M${sx},${sy} H${tx}` : `M${sx},${sy} H${mx} V${ty} H${tx}`;
        els.wires.push({ src, dst: g.name, el: mk("path", { d: pd, class: "cg-wire" }, gW) });
      });
    });
    const hook = (g, name) => {
      const on = () => api.set(name), off = () => api.set(null);
      g.addEventListener("pointerenter", on); g.addEventListener("pointerleave", off);
      g.addEventListener("focus", on); g.addEventListener("blur", off);
      g.addEventListener("click", () => api.set(api.get() === name ? null : name));
    };
    C.inputs.forEach(x => {
      const p = pos.get(x);
      const g = mk("g", { class: "cg-gate", tabindex: "0", role: "button", "aria-label": `Input ${x}` }, gN);
      mk("rect", { x: p.x - 24, y: p.y - 14, width: 48, height: 28, rx: 7, class: "body" }, g);
      mk("text", { x: p.x, y: p.y, class: "in-nm" }, g).textContent = x;
      const v = mk("text", { x: p.x + 30, y: p.y - 12, class: "cg-val" }, gN);
      els.gates.set(x, g); els.vals.set(x, v); hook(g, x);
    });
    C.gates.forEach(gt => {
      const p = pos.get(gt.name), gg = geo.get(gt.name);
      const g = mk("g", { class: "cg-gate", tabindex: "0", role: "button", "aria-label": `${gt.op} gate ${gt.name}` }, gN);
      mk("path", { d: gg.d, class: "body" }, g);
      if (gg.bubble) mk("circle", { cx: gg.bubble[0], cy: gg.bubble[1], r: 4, class: "bub" }, g);
      mk("text", { x: p.x, y: p.y - GH / 2 - 7, class: "nm" }, g).textContent = `${gt.name} · ${gt.op}`;
      const v = mk("text", { x: gg.outX + 6, y: p.y - 12, class: "cg-val" }, gN);
      els.gates.set(gt.name, g); els.vals.set(gt.name, v); hook(g, gt.name);
    });
    const [ox, oy] = outPin.get(C.out);
    mk("path", { d: `M${ox},${oy} H${ox + 46}`, class: "cg-out", "marker-end": "url(#cg-arrow)" }, gN);
    mk("text", { x: ox + 52, y: oy, class: "cg-out-l" }, gN).textContent = "output";
    return els;
  }
  function paintCircuit(els, C, f, hover) {
    const vals = f ? f.vals : {};
    const near = new Set();
    if (hover) { near.add(hover); els.wires.forEach(w => { if (w.src === hover) near.add(w.dst); if (w.dst === hover) near.add(w.src); }); }
    for (const [n, g] of els.gates) {
      let cls = "cg-gate";
      if (f && n === f.active && !f.done) cls += " Active";
      if (n === C.out && f && vals[n] !== undefined) {
        if (f.done) cls += f.ok ? " Solution" : " Rejected";
        else if (f.trial && vals[n] === 0) cls += " Rejected";
      }
      if (hover) cls += n === hover ? " trace" : near.has(n) ? "" : " faint";
      g.setAttribute("class", cls);
      const v = vals[n], t = els.vals.get(n);
      t.textContent = v === undefined ? "" : String(v);
      let vc = "cg-val" + (v === 0 ? " v0" : "");
      if (n === C.out && f && f.done && v !== undefined) vc += f.ok ? " Solution" : " Rejected";
      t.setAttribute("class", vc);
    }
    els.wires.forEach(w => {
      const v = vals[w.src];
      let cls = "cg-wire" + (v === 1 ? " v1" : v === 0 ? " v0" : "");
      if (f && !f.done && w.dst === f.active) cls += " hot";
      if (hover) cls = "cg-wire" + (v === 1 ? " v1" : v === 0 ? " v0" : "") + (w.src === hover || w.dst === hover ? " trace" : " faint");
      w.el.setAttribute("class", cls);
    });
  }
  function checksCVP(C, f) {
    const vals = f ? f.vals : {};
    const done = C.gates.filter(g => vals[g.name] !== undefined).length;
    return [
      { label: `Gates evaluated: ${done} / ${C.gates.length}`, ok: f ? done === C.gates.length : null },
      { label: `Output ${C.out}: ${vals[C.out] === undefined ? "not yet" : vals[C.out]}`, ok: f && f.done ? vals[C.out] === 1 : null },
    ];
  }
  function checksCSAT(C, f) {
    const a = f && f.assign ? f.assign : {}, vals = f ? f.vals : {};
    const set = C.inputs.filter(x => a[x] !== undefined).length;
    return [
      { label: `Inputs set: ${set} / ${C.inputs.length}`, ok: f && f.done ? f.ok : null },
      { label: `Output ${C.out}: ${vals[C.out] === undefined ? "unknown" : vals[C.out]}`, ok: f && (f.done || vals[C.out] !== undefined) ? vals[C.out] === 1 : null },
    ];
  }

  /* =================== Grammar: CYK membership =================== */
  const EPS = new Set(["ε", "eps", "epsilon", ""]);
  function parseGrammar(str) {
    const s = str.replace(/\s+/g, "");
    const m = /^\(\(\{([^{}]*)\},\{([^{}]*)\},\{(.*)\},([^,{}()]+)\),([^,{}()]*)\)$/.exec(s);
    if (!m) throw new Error("Expected ((V, Σ, R, S), w): variables, terminals, rules (A,BC) or (A,a), the start variable, then the string.");
    const V = m[1] ? m[1].split(",") : [], T = m[2] ? m[2].split(",") : [], start = m[4], w = [...m[5]];
    if (!V.length || new Set(V).size !== V.length) throw new Error("V must be a non-empty set of variables with no repeats.");
    if (!T.length || new Set(T).size !== T.length) throw new Error("Σ must be a non-empty set of terminals with no repeats.");
    for (const t of T) if ([...t].length !== 1) throw new Error(`Terminal ${t} must be a single character.`);
    for (const t of T) if (V.includes(t)) throw new Error(`${t} is both a variable and a terminal.`);
    if (!V.includes(start)) throw new Error(`The start variable ${start} isn't in V.`);
    const re = /\(([^,()]+),([^,()]*)\)/g;
    if (m[3].replace(re, "").replace(/,/g, "")) throw new Error("Each rule must look like (A,BC) or (A,a).");
    const unary = [], binary = [];
    let eps = false, t;
    // split a body into two variable names: explicit "·" or "." first, then any split where both halves are variables
    const split2 = body => {
      const sep = body.split(/[·.]/);
      if (sep.length === 2 && V.includes(sep[0]) && V.includes(sep[1])) return sep;
      for (let k = 1; k < body.length; k++) { const a = body.slice(0, k), b = body.slice(k); if (V.includes(a) && V.includes(b)) return [a, b]; }
      return null;
    };
    while ((t = re.exec(m[3]))) {
      const [, A, body] = t;
      if (!V.includes(A)) throw new Error(`Rule (${A},${body}): ${A} isn't a variable.`);
      if (EPS.has(body)) {
        if (A !== start) throw new Error(`Rule (${A},ε) isn't in Chomsky Normal Form: only the start variable may produce ε.`);
        eps = true; continue;
      }
      if (T.includes(body)) { unary.push({ A, a: body }); continue; }
      const bc = split2(body);
      if (!bc) throw new Error(`Rule (${A},${body}) isn't in Chomsky Normal Form: a body must be one terminal or two variables.`);
      binary.push({ A, B: bc[0], C: bc[1] });
    }
    if (!unary.length && !binary.length && !eps) throw new Error("The grammar has no rules.");
    for (const ch of w) if (!T.includes(ch)) throw new Error(`The string uses ${ch}, which isn't in Σ.`);
    if (w.length > 9) throw new Error("This mockup draws strings up to 9 characters.");
    return { V, T, start, w, unary, binary, eps };
  }
  const setText = s => (s.size ? [...s].join(", ") : "∅");
  function cyk(G) {
    const n = G.w.length, table = [], back = [], order = [];
    for (let i = 0; i < n; i++) { table.push([]); back.push([]); }
    const frames = [{ filled: 0, caption: n ? `Step 0: an empty table. Row 1 holds the variables that produce each single character; row k holds the ones that produce each run of k characters.` : "Step 0: the string is empty." }];
    if (!n) {
      frames.push({ filled: 0, done: true, ok: G.eps, caption: G.eps ? `${G.start} → ε is a rule, so the empty string is in the language.` : `There's no rule ${G.start} → ε, so the empty string isn't in the language.` });
      return { frames, table, back, order, n };
    }
    for (let L = 1; L <= n; L++) for (let i = 0; i + L <= n; i++) {
      const cell = new Set(), bp = new Map(), splits = [];
      if (L === 1) {
        G.unary.forEach(r => { if (r.a === G.w[i]) { cell.add(r.A); if (!bp.has(r.A)) bp.set(r.A, { term: true }); } });
      } else {
        for (let k = 1; k < L; k++) {
          const left = table[i][k - 1], right = table[i + k][L - k - 1], made = new Set();
          G.binary.forEach(r => { if (left.has(r.B) && right.has(r.C)) { made.add(r.A); cell.add(r.A); if (!bp.has(r.A)) bp.set(r.A, { k, B: r.B, C: r.C }); } });
          splits.push({ k, made });
        }
      }
      table[i][L - 1] = cell; back[i][L - 1] = bp;
      order.push([i, L]);
      const sub = G.w.slice(i, i + L).join("");
      let caption;
      if (L === 1) caption = `"${sub}" at position ${i + 1}: the rules that produce it give {${setText(cell)}}.`;
      else caption = `"${sub}" (positions ${i + 1}–${i + L}): ` + splits.map(sp => `${G.w.slice(i, i + sp.k).join("")}|${G.w.slice(i + sp.k, i + L).join("")} gives ${sp.made.size ? "{" + [...sp.made].join(", ") + "}" : "∅"}`).join("; ") + `. Cell = {${setText(cell)}}.`;
      frames.push({ filled: order.length, cur: [i, L], splits: splits.filter(sp => sp.made.size).map(sp => sp.k), allSplits: splits.map(sp => sp.k), caption });
    }
    const ok = table[0][n - 1].has(G.start);
    if (ok) {
      // parse tree from back-pointers, then grow it one node per frame (top down)
      const nodes = [];
      const build = (A, i, L, depth, parent) => {
        const id = nodes.length, b = back[i][L - 1].get(A);
        nodes.push({ id, sym: A, i, L, depth, parent });
        if (b.term) nodes.push({ id: nodes.length, sym: G.w[i], i, L: 1, depth: depth + 1, parent: id, leaf: true });
        else { build(b.B, i, b.k, depth + 1, id); build(b.C, i + b.k, L - b.k, depth + 1, id); }
      };
      build(G.start, 0, n, 0, null);
      const shown = [0];
      frames.push({ filled: order.length, top: true, shown: [...shown], nodes, caption: `${G.start} is in the top cell, so "${G.w.join("")}" is in the language. Now follow the back-pointers to build a parse tree.` });
      nodes.forEach((nd, k) => {
        if (nd.leaf) return;
        const kids = nodes.filter(x => x.parent === nd.id);
        kids.forEach(x => shown.push(x.id));
        frames.push({ filled: order.length, top: true, shown: [...shown], nodes, newNode: nd.id,
          caption: kids.length === 1 && kids[0].leaf ? `${nd.sym} → ${kids[0].sym} covers position ${nd.i + 1}.` : `${nd.sym} → ${kids.map(x => x.sym).join(" ")} splits "${G.w.slice(nd.i, nd.i + nd.L).join("")}" into "${G.w.slice(kids[0].i, kids[0].i + kids[0].L).join("")}" and "${G.w.slice(kids[1].i, kids[1].i + kids[1].L).join("")}".` });
      });
      frames.push({ filled: order.length, top: true, shown: nodes.map(x => x.id), nodes, done: true, ok: true, caption: `Accepted. The parse tree has ${nodes.filter(x => !x.leaf).length} variable nodes over ${n} characters.` });
    } else frames.push({ filled: order.length, top: true, done: true, ok: false, caption: `The top cell is {${setText(table[0][n - 1])}}, which doesn't contain ${G.start}. "${G.w.join("")}" isn't in the language. Rejected.` });
    return { frames, table, back, order, n };
  }

  function drawGrammar(svg, G, api, R) {
    svg.innerHTML = "";
    const n = Math.max(1, G.w.length);
    const longest = Math.max(1, ...R.order.map(([i, L]) => setText(R.table[i][L - 1]).length));
    const cw = Math.max(58, Math.min(110, longest * 8 + 18)), ch = 38;
    const left = 86, tableTop = 46;
    const baseY = tableTop + n * ch;
    const accepted = R.frames[R.frames.length - 1].ok && R.frames.some(f => f.nodes);
    const nodes = accepted ? R.frames.find(f => f.nodes).nodes : [];
    const maxDepth = nodes.length ? Math.max(...nodes.map(x => x.depth)) : 0;
    const treeTop = baseY + 70, rowH = 44;
    const W = Math.max(640, left + n * cw + 30), H = accepted ? treeTop + maxDepth * rowH + 40 : baseY + 56;
    svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
    mk("text", { x: 24, y: 24, class: "cg-head" }, svg).textContent = "CYK TABLE · ROW k = RUNS OF k CHARACTERS";
    const cells = new Map(), chars = [];
    const cellXY = (i, L) => ({ x: left + i * cw + ((L - 1) * cw) / 2, y: baseY - L * ch });
    const hook = (g, key) => {
      const on = () => api.set(key), off = () => api.set(null);
      g.addEventListener("pointerenter", on); g.addEventListener("pointerleave", off);
      g.addEventListener("focus", on); g.addEventListener("blur", off);
    };
    for (let L = 1; L <= G.w.length; L++) {
      const p = cellXY(0, L);
      mk("text", { x: left - 14, y: p.y + ch / 2, class: "cg-rowl" }, svg).textContent = `${L}`;
      for (let i = 0; i + L <= G.w.length; i++) {
        const q = cellXY(i, L);
        const g = mk("g", { class: "cg-cell unset", tabindex: "0", role: "button", "aria-label": `Cell for "${G.w.slice(i, i + L).join("")}"` }, svg);
        mk("rect", { x: q.x + 2, y: q.y + 2, width: cw - 4, height: ch - 4, rx: 6 }, g);
        const t = mk("text", { x: q.x + cw / 2, y: q.y + ch / 2 }, g);
        cells.set(`${i},${L}`, { g, t, i, L });
        hook(g, `c${i},${L}`);
      }
    }
    if (G.w.length) mk("text", { x: left - 14, y: tableTop - 10, class: "cg-rowl" }, svg).textContent = "k";
    G.w.forEach((c, i) => {
      const t = mk("text", { x: left + i * cw + cw / 2, y: baseY + 18, class: "cg-ch" }, svg);
      t.textContent = c; chars.push(t);
    });
    if (!G.w.length) mk("text", { x: left, y: baseY + 18, class: "cg-note" }, svg).textContent = "(empty string)";
    const tree = { nodes: new Map(), edges: [] };
    if (accepted) {
      mk("text", { x: 24, y: baseY + 48, class: "cg-head" }, svg).textContent = "PARSE TREE · BUILT FROM THE BACK-POINTERS";
      const xOf = new Map(), yOf = nd => treeTop + nd.depth * rowH;
      // leaves sit under their characters; a parent is centered over its children
      const place = nd => {
        if (nd.leaf) { xOf.set(nd.id, left + nd.i * cw + cw / 2); return xOf.get(nd.id); }
        const kids = nodes.filter(x => x.parent === nd.id);
        const xs = kids.map(place);
        xOf.set(nd.id, xs.reduce((a, b) => a + b, 0) / xs.length);
        return xOf.get(nd.id);
      };
      place(nodes[0]);
      const leafY = treeTop + maxDepth * rowH;
      const gE = mk("g", {}, svg), gN = mk("g", {}, svg);
      nodes.forEach(nd => {
        const y = nd.leaf ? leafY : yOf(nd);
        if (nd.parent !== null) {
          const pp = nodes[nd.parent];
          tree.edges.push({ id: nd.id, el: mk("line", { x1: xOf.get(pp.id), y1: yOf(pp) + 13, x2: xOf.get(nd.id), y2: y - 13, class: "cg-te" }, gE) });
        }
        const g = mk("g", { class: "cg-tn" + (nd.leaf ? " leaf" : ""), tabindex: "0", role: "img", "aria-label": nd.leaf ? `Character ${nd.sym}` : `Variable ${nd.sym}` }, gN);
        mk("circle", { cx: xOf.get(nd.id), cy: y, r: 13 }, g);
        mk("text", { x: xOf.get(nd.id), y }, g).textContent = nd.sym;
        tree.nodes.set(nd.id, { g, nd });
        if (!nd.leaf) hook(g, `c${nd.i},${nd.L}`);
      });
    }
    return { cells, chars, tree, R };
  }
  function paintGrammar(els, G, f, hover) {
    const R = els.R, filled = f ? f.filled : 0;
    const filledSet = new Set(R.order.slice(0, filled).map(([i, L]) => `${i},${L}`));
    const shown = new Set(f && f.shown ? f.shown : []), used = new Set();
    if (f && f.nodes) f.nodes.forEach(nd => { if (shown.has(nd.id) && !nd.leaf) used.add(`${nd.i},${nd.L}`); });
    const splitCells = new Set(), allSplit = new Set();
    if (f && f.cur && !f.done) {
      const [i, L] = f.cur;
      (f.splits || []).forEach(k => { splitCells.add(`${i},${k}`); splitCells.add(`${i + k},${L - k}`); });
      (f.allSplits || []).forEach(k => { allSplit.add(`${i},${k}`); allSplit.add(`${i + k},${L - k}`); });
    }
    let hi = null;
    if (hover && hover[0] === "c") { const [i, L] = hover.slice(1).split(",").map(Number); hi = { i, L }; }
    const n = G.w.length;
    for (const [key, c] of els.cells) {
      const set = R.table[c.i][c.L - 1];
      let cls = "cg-cell";
      if (!filledSet.has(key)) { cls += " unset"; c.t.textContent = ""; }
      else { c.t.textContent = setText(set); if (!set.size) cls += " empty"; }
      if (f && f.cur && !f.done && f.cur[0] === c.i && f.cur[1] === c.L) cls += " Active";
      else if (splitCells.has(key)) cls += " split";
      if (used.has(key)) cls += " used";
      if (f && f.top && c.i === 0 && c.L === n) cls += f.ok === false || !set.has(G.start) ? " Rejected" : " Solution";
      if (hi) cls += hi.i === c.i && hi.L === c.L ? " trace" : " faint";
      c.g.setAttribute("class", cls);
    }
    els.chars.forEach((t, i) => t.setAttribute("class", "cg-ch" + (hi && i >= hi.i && i < hi.i + hi.L ? " lit" : "")));
    for (const [id, o] of els.tree.nodes) {
      const vis = shown.has(id);
      o.g.setAttribute("visibility", vis ? "visible" : "hidden");
      let cls = "cg-tn" + (o.nd.leaf ? " leaf" : "");
      if (f && f.newNode !== undefined && (id === f.newNode || o.nd.parent === f.newNode)) cls += " new";
      if (hi && !o.nd.leaf && o.nd.i === hi.i && o.nd.L === hi.L) cls += " trace";
      o.g.setAttribute("class", cls);
    }
    els.tree.edges.forEach(e => e.el.setAttribute("visibility", shown.has(e.id) ? "visible" : "hidden"));
  }
  function checksGrammar(G, R, f) {
    const total = R.order.length, filled = f ? f.filled : 0;
    const out = [{ label: `Cells filled: ${filled} / ${total}`, ok: f ? filled === total : null }];
    if (f && f.top) out.push({ label: `${G.start} in the top cell: ${f.ok === false ? "no" : "yes"}`, ok: f.ok !== false });
    if (f && f.nodes) out.push({ label: `Parse tree nodes: ${f.shown.length} / ${f.nodes.length}`, ok: f.shown.length >= f.nodes.length });
    return out;
  }
  function bracket(nodes, id) {
    const nd = nodes[id], kids = nodes.filter(x => x.parent === id);
    if (nd.leaf) return nd.sym;
    return `${nd.sym}(${kids.map(k => bracket(nodes, k.id)).join(" ")})`;
  }

  /* =================== catalog =================== */
  const PROBLEMS = {
    CVP: { label: "Circuit Value", cls: "P", type: "Circuits", vizType: "Boolean Circuit",
      def: "Given a Boolean circuit of AND, OR and NOT gates and a value for every input, does the output gate evaluate to 1? It is P-complete: easy to solve, but among the hardest problems in P to speed up with parallel computers.",
      input: "((X, G, o), A)", inputLong: "Inputs X, gates G as (name,OP,in1,in2), the output gate o, and an assignment A of 0 or 1 to every input",
      solvers: [["eval", "Gate-by-gate evaluation (not in Redux yet)"]],
      examples: [
        ["Majority of three", "(({x1,x2,x3},{(g1,AND,x1,x2),(g2,AND,x1,x3),(g3,AND,x2,x3),(g4,OR,g1,g2),(g5,OR,g4,g3)},g5),{(x1,1),(x2,0),(x3,1)})"],
        ["Full adder, carry out", "(({a,b,cin},{(s1,XOR,a,b),(c1,AND,a,b),(c2,AND,s1,cin),(cout,OR,c1,c2)},cout),{(a,1),(b,0),(cin,1)})"],
        ["XOR from four NANDs (output 0)", "(({a,b},{(n1,NAND,a,b),(n2,NAND,a,n1),(n3,NAND,b,n1),(y,NAND,n2,n3)},y),{(a,1),(b,1)})"],
      ] },
    CIRCUITSAT: { label: "Circuit-SAT", cls: "NP-Complete", type: "Circuits", vizType: "Boolean Circuit",
      def: "Given a Boolean circuit, is there some setting of its inputs that makes the output 1? Circuit-SAT is the problem the Cook-Levin theorem is usually proved through, and it reduces to SAT and 3SAT.",
      input: "(X, G, o)", inputLong: "Inputs X, gates G as (name,OP,in1,in2), and the output gate o",
      solvers: [["brute", "Brute Force (not in Redux yet)"], ["bt", "Backtracking with partial evaluation (not in Redux yet)"]],
      examples: [
        ["Satisfiable, found late", "({x1,x2,x3},{(n2,NOT,x2),(a1,AND,n2,x3),(y,AND,x1,a1)},y)"],
        ["A 3SAT formula as a circuit", "({x1,x2,x3},{(n1,NOT,x1),(n3,NOT,x3),(c1,OR,x1,x2,x3),(c2,OR,n1,x2),(c3,OR,n3,x2),(a1,AND,c1,c2),(y,AND,a1,c3)},y)"],
        ["Unsatisfiable", "({x1,x2},{(n1,NOT,x1),(a,AND,x1,x2),(y,AND,a,n1)},y)"],
      ] },
    CFGMEMBER: { label: "Context-Free Grammar Membership", cls: "P", type: "Languages", vizType: "Grammar",
      def: "Given a context-free grammar in Chomsky Normal Form and a string w, can the grammar derive w? The CYK algorithm answers it in O(n³) time by filling a triangular table of which variables produce each substring.",
      input: "((V, Σ, R, S), w)", inputLong: "Variables V, terminals Σ, rules R as (A,BC) or (A,a), the start variable S, and the string w",
      solvers: [["cyk", "CYK Algorithm (not in Redux yet)"]],
      examples: [
        ["Textbook grammar, accepted", "(({S,A,B,C},{a,b},{(S,AB),(S,BC),(A,BA),(A,a),(B,CC),(B,b),(C,AB),(C,a)},S),baaba)"],
        ["aⁿbⁿ, accepted", "(({S,T,A,B},{a,b},{(S,AT),(S,AB),(T,SB),(A,a),(B,b)},S),aaabbb)"],
        ["aⁿbⁿ, rejected", "(({S,T,A,B},{a,b},{(S,AT),(S,AB),(T,SB),(A,a),(B,b)},S),aabbb)"],
      ] },
  };

  const IMPL = {
    CVP: {
      parse: s => parseCircuit(s, true),
      solve: C => cvpSolve(C),
      draw: (svg, C, api) => drawCircuit(svg, C, api),
      paint: (els, C, f, h) => paintCircuit(els, C, f, h),
      checks: (C, f) => checksCVP(C, f),
      chosen: (C, f) => (f && f.vals[C.out] !== undefined ? [{ text: `${C.out} = ${f.vals[C.out]}` }] : []),
      shape: "the output value",
    },
    CIRCUITSAT: {
      parse: s => parseCircuit(s, false),
      solve: (C, s) => (s === "bt" ? csatBacktrack(C) : csatBrute(C)),
      draw: (svg, C, api) => drawCircuit(svg, C, api),
      paint: (els, C, f, h) => paintCircuit(els, C, f, h),
      checks: (C, f) => checksCSAT(C, f),
      chosen: (C, f) => (f && f.assign && (!f.done || f.ok) ? C.inputs.filter(x => f.assign[x] !== undefined).map(x => ({ text: `${x}=${f.assign[x]}` })) : []),
      shape: "an input assignment",
    },
    CFGMEMBER: {
      parse: s => parseGrammar(s),
      solve: G => cyk(G).frames,
      draw: null, paint: null, checks: null,
      chosen: (G, f) => (f && f.nodes && f.shown.length >= f.nodes.length ? [{ text: `"${G.w.join("")}" accepted` }, { text: bracket(f.nodes, 0) }] : []),
      shape: "a parse tree",
    },
  };

  function create({ svg }) {
    if (!document.getElementById("cg-style")) {
      const st = document.createElement("style"); st.id = "cg-style"; st.textContent = css; document.head.appendChild(st);
    }
    svg.classList.add("cg-svg");
    let key = null, model = null, run = null, frames = [], frame = null, hover = null, els = null;
    const paint = () => {
      if (!model) return;
      if (key === "CFGMEMBER") paintGrammar(els, model, frame, hover);
      else IMPL[key].paint(els, model, frame, hover);
    };
    const api = { set: h => { hover = h; paint(); }, get: () => hover };
    return {
      load(str, k, solver) {
        const P = PROBLEMS[k];
        if (!P) throw new Error("Unknown problem " + k);
        const m = IMPL[k].parse(str);
        key = k; model = m; frame = null; hover = null;
        if (k === "CFGMEMBER") { run = cyk(m); frames = run.frames; els = drawGrammar(svg, m, api, run); }
        else { run = null; frames = IMPL[k].solve(m, solver || P.solvers[0][0]); els = IMPL[k].draw(svg, m, api); }
        paint();
        return { frames, ok: frames[frames.length - 1].ok };
      },
      show(i) { frame = i === null ? null : frames[i]; paint(); return frame; },
      checks(i) {
        const f = i === null ? null : frames[i];
        return key === "CFGMEMBER" ? checksGrammar(model, run, f) : IMPL[key].checks(model, f);
      },
      chosen(i) { return IMPL[key].chosen(model, i === null ? null : frames[i], run); },
      shape(k) { return IMPL[k].shape; },
      resetHover() { hover = null; paint(); },
    };
  }

  return { create, parse: (str, k) => IMPL[k].parse(str), PROBLEMS, _impl: { IMPL, cyk, evaluate, gateVal, parseCircuit, parseGrammar } };
})();
