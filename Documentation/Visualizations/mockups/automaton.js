/* ---- Automaton view (DFA / NFA), shared by both mockups ----
   Pages supply --av-* tokens. Layout is plain geometry: states in BFS layers left to right. */
const AutomatonView = (() => {
  const NS = "http://www.w3.org/2000/svg";
  const EPS = new Set(["ε", "eps", "epsilon"]);
  const R = 26, DX = 170, DY = 120, SEP = "\u0000";
  const mk = (tag, attrs = {}, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; };

  const css = `
.au-svg { width: 100%; height: auto; display: block; }
.au-svg text { font-family: var(--av-mono); }
.au-node { cursor: pointer; transition: opacity .2s; }
.au-node:focus { outline: none; }
.au-node .body { fill: var(--av-bg); stroke: var(--av-stroke); stroke-width: 2; transition: fill .2s, stroke .2s; }
.au-node .inner { fill: none; stroke: var(--av-stroke); stroke-width: 1.6; }
.au-node text { font-size: 15px; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); pointer-events: none; }
.au-node.Active .body { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; }
.au-node.Active .inner { stroke: var(--av-hl); }
.au-node.Solution .body { fill: var(--av-sol); stroke: var(--av-sol); }
.au-node.Solution .inner { stroke: #fff; } .au-node.Solution text { fill: #fff; font-weight: 600; }
.au-node.Rejected .body { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 3; }
.au-node.Rejected .inner { stroke: var(--av-rej); } .au-node.Rejected text { fill: var(--av-rej); }
.au-node.Untraveled .body { fill: transparent; stroke-dasharray: 4 4; }
.au-node.Untraveled .inner { stroke-dasharray: 4 4; } .au-node.Untraveled text { fill: var(--av-muted); }
.au-node.trace .body, .au-node:focus-visible .body { stroke: var(--av-hot); stroke-width: 3; }
.au-node.faint { opacity: .3; }
.au-garbage rect { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 2; stroke-dasharray: 5 3; transition: stroke-width .2s; }
.au-garbage text { font-size: 13px; font-weight: 600; text-anchor: middle; dominant-baseline: central; fill: var(--av-rej); }
.au-garbage.Active rect { stroke-dasharray: none; stroke-width: 3; box-shadow: none; }
.au-garbage.Active rect, .au-garbage.Rejected rect { stroke-dasharray: none; stroke-width: 3.5; }
.au-garbage.faint { opacity: .3; }
.au-garbage text.au-garbage-note { font-size: 11px; font-weight: 400; fill: var(--av-muted); text-anchor: middle; }
.au-edge { fill: none; stroke: var(--av-edge); stroke-width: 1.6; transition: opacity .2s, stroke .2s; }
.au-edge.eps { stroke-dasharray: 5 4; }
.au-edge.hot { stroke: var(--av-hl); stroke-width: 3; }
.au-edge.trace { stroke: var(--av-hot); stroke-width: 2.4; }
.au-edge.faint, .au-elabel.faint { opacity: .15; }
.au-elabel rect { fill: var(--av-surface); stroke: var(--av-line); }
.au-elabel text { font-size: 12.5px; text-anchor: middle; dominant-baseline: central; fill: var(--av-muted); }
.au-elabel.hot rect { fill: var(--av-hl); stroke: var(--av-hl); } .au-elabel.hot text { fill: #1b1300; font-weight: 600; }
.au-elabel.trace rect { stroke: var(--av-hot); } .au-elabel.trace text { fill: var(--av-hot); }
.au-start { stroke: var(--av-ink); stroke-width: 1.8; }
.au-m-def { fill: var(--av-edge); } .au-m-hot { fill: var(--av-hl); } .au-m-trace { fill: var(--av-hot); } .au-m-ink { fill: var(--av-ink); }
.au-tape { display: flex; flex-wrap: wrap; gap: 4px; align-items: center; font-family: var(--av-mono); }
.au-cell { min-width: 34px; height: 38px; display: grid; place-items: center; border: 1.5px solid var(--av-line); border-radius: 6px; font-size: 16px; background: var(--av-surface); color: var(--av-ink); transition: background .2s, border-color .2s; }
.au-cell.read { color: var(--av-muted); background: transparent; }
.au-cell.just { background: var(--av-hl-fill); }
.au-cell.head { border-color: var(--av-hl); box-shadow: 0 0 0 2px var(--av-hl); }
.au-cell.end { padding: 0 10px; font-size: 12px; border-style: dashed; color: var(--av-muted); background: transparent; }
.au-cell.end.acc { border: 1.5px solid var(--av-sol); color: var(--av-sol); }
.au-cell.end.rej { border: 1.5px solid var(--av-rej); color: var(--av-rej); }
@media (prefers-reduced-motion: reduce) { .au-node, .au-node .body, .au-edge, .au-cell { transition: none; } }`;

  function parse(str, kind) {
    const s = str.replace(/\s+/g, "");
    const m = /^\(\(\{([^{}]*)\},\{([^{}]*)\},\{(.*)\},([^,{}()]+),\{([^{}]*)\}\),([^,{}()]*)\)$/.exec(s);
    if (!m) throw new Error("Expected ((States, Alphabet, Transitions, Start, Accept), input).");
    const list = x => (x ? x.split(",") : []);
    const states = list(m[1]), alphabet = list(m[2]), start = m[4], accept = list(m[5]);
    const re = /\(([^,()]+),([^,()]+),([^,()]+)\)/g;
    if (m[3].replace(re, "").replace(/,/g, "")) throw new Error("Each transition must look like (from,symbol,to).");
    const trans = [];
    let t;
    while ((t = re.exec(m[3]))) trans.push({ from: t[1], sym: EPS.has(t[2]) ? "ε" : t[2], to: t[3] });
    const S = new Set(states);
    if (!states.length || S.size !== states.length) throw new Error("States must be a non-empty set with no repeats.");
    if (!S.has(start)) throw new Error(`Start state ${start} is not in the state set.`);
    for (const a of accept) if (!S.has(a)) throw new Error(`Accept state ${a} is not in the state set.`);
    for (const e of trans) {
      if (!S.has(e.from) || !S.has(e.to)) throw new Error(`Transition (${e.from},${e.sym},${e.to}) uses an unknown state.`);
      if (e.sym !== "ε" && !alphabet.includes(e.sym)) throw new Error(`Symbol ${e.sym} is not in the alphabet.`);
    }
    const input = [...m[6]];
    for (const ch of input) if (!alphabet.includes(ch)) throw new Error(`Input symbol ${ch} is not in the alphabet.`);
    if (kind === "DFA") {
      if (trans.some(e => e.sym === "ε")) throw new Error("A DFA can't have ε-transitions. Use NFA instead.");
      const seen = new Set();
      for (const e of trans) {
        const k = e.from + SEP + e.sym;
        if (seen.has(k)) throw new Error(`State ${e.from} has two transitions on ${e.sym}. Use NFA instead.`);
        seen.add(k);
      }
    }
    // A DFA missing any (state, symbol) transition gets an implicit Garbage trap state.
    let garbage = null;
    if (kind === "DFA" && states.some(st => alphabet.some(a => !trans.some(e => e.from === st && e.sym === a)))) {
      garbage = "Garbage";
      while (S.has(garbage)) garbage += "'";
    }
    return { states, alphabet, trans, start, accept: new Set(accept), input, kind, garbage };
  }

  // Every individual run of an NFA, as maximal move sequences. Capped so a branchy machine stays readable.
  const PATH_LIMIT = 40;
  function enumeratePaths(A) {
    const found = [], n = A.input.length;
    function dfs(s, pos, moves, seen) {
      if (found.length >= PATH_LIMIT) return;
      const opts = [];
      for (const e of A.trans) if (e.from === s) {
        if (e.sym === "ε") { if (!seen.has(e.to + SEP + pos)) opts.push({ e, pos }); }
        else if (pos < n && e.sym === A.input[pos]) opts.push({ e, pos: pos + 1 });
      }
      if (pos === n || !opts.some(o => o.e.sym !== "ε")) found.push({ moves: moves.slice(), end: s, pos });
      for (const o of opts) {
        const k = o.e.to + SEP + o.pos, fresh = !seen.has(k);
        seen.add(k); moves.push(o.e);
        dfs(o.e.to, o.pos, moves, seen);
        moves.pop(); if (fresh) seen.delete(k);
      }
    }
    dfs(A.start, 0, [], new Set([A.start + SEP + 0]));
    return found.map((p, i) => {
      const visited = new Set([A.start]);
      let f = { pos: 0, active: [A.start], taken: [] };
      const frames = [f];
      for (const e of p.moves) {
        visited.add(e.to);
        if (e.sym === "ε") { f.taken.push(e); f.active = [e.to]; }
        else { f = { pos: f.pos + 1, sym: e.sym, active: [e.to], taken: [e] }; frames.push(f); }
      }
      frames.forEach((fr, k) => {
        const eps = fr.taken.filter(e => e.sym === "ε").map(e => e.to);
        const tail = eps.length ? `, then ε to ${eps.join(", then ε to ")}` : "";
        fr.caption = k === 0 ? `Start in ${A.start}${tail}.` : `Read "${fr.sym}": ${fr.taken[0].from} → ${fr.taken[0].to}${tail}.`;
      });
      const finished = p.pos === n, accepted = finished && A.accept.has(p.end);
      const verdict = accepted ? "accepts" : finished ? "rejects" : `dies at symbol ${p.pos + 1}`;
      frames.push({ pos: p.pos, active: [p.end], taken: [], done: true, accepted, visited,
        caption: !finished ? `No "${A.input[p.pos]}" transition from ${p.end}, so this path dies. Rejected.`
          : accepted ? `This path ends in ${p.end}, an accept state. Accepted.`
          : `This path ends in ${p.end}, which is not an accept state. Rejected.` });
      const chain = [A.start, ...p.moves.map(e => (e.sym === "ε" ? "⇢ " : "→ ") + e.to)].join(" ");
      return { label: `Path ${i + 1}: ${chain}, ${verdict}`, accepted, frames };
    });
  }

  function simulate(A) {
    const out = (s, sym) => A.trans.filter(e => e.from === s && e.sym === sym);
    const closure = set => {
      const res = new Set(set), used = [], stack = [...set];
      while (stack.length) {
        const s = stack.pop();
        for (const e of out(s, "ε")) { used.push(e); if (!res.has(e.to)) { res.add(e.to); stack.push(e.to); } }
      }
      return { res, used };
    };
    const fmt = set => "{" + [...set].join(", ") + "}";
    const frames = [], visited = new Set();
    const c0 = closure([A.start]);
    c0.res.forEach(s => visited.add(s));
    const extra = [...c0.res].filter(s => s !== A.start);
    frames.push({ pos: 0, active: [...c0.res], taken: c0.used,
      caption: `Start in ${A.start}${extra.length ? `, and follow ε to ${extra.join(", ")}` : ""}. Nothing read yet.` });
    let cur = c0.res;
    for (let i = 0; i < A.input.length; i++) {
      const sym = A.input[i], taken = [], next = new Set();
      if (A.garbage && cur.has(A.garbage)) {
        visited.add(A.garbage);
        frames.push({ pos: i + 1, sym, active: [A.garbage], taken, caption: `Read "${sym}". Garbage has no way out, so the machine stays there.` });
        continue;
      }
      cur.forEach(s => out(s, sym).forEach(e => { taken.push(e); next.add(e.to); }));
      if (A.garbage && !next.size) {
        const from = [...cur][0];
        cur = new Set([A.garbage]); visited.add(A.garbage);
        frames.push({ pos: i + 1, sym, active: [A.garbage], taken, caption: `Read "${sym}". State ${from} has no "${sym}" transition, so the machine falls into Garbage.` });
        continue;
      }
      const cl = closure(next);
      cl.used.forEach(e => taken.push(e));
      cl.res.forEach(s => visited.add(s));
      let caption;
      if (!cl.res.size) caption = A.kind === "DFA"
        ? `Read "${sym}". State ${[...cur][0]} has no "${sym}" transition, so the machine is stuck.`
        : `Read "${sym}". No active state has a "${sym}" transition, so every branch dies.`;
      else if (A.kind === "DFA") caption = `Read "${sym}": ${[...cur][0]} → ${[...cl.res][0]}.`;
      else caption = `Read "${sym}". Active states are now ${fmt(cl.res)}${cl.used.length ? " (including ε moves)" : ""}.`;
      frames.push({ pos: i + 1, sym, active: [...cl.res], taken, caption });
      cur = cl.res;
      if (!cur.size) break;
    }
    const finished = frames[frames.length - 1].pos === A.input.length && cur.size > 0;
    const hits = [...cur].filter(s => A.accept.has(s));
    const accepted = finished && hits.length > 0;
    let caption;
    if (!finished) caption = "Rejected. The machine got stuck before reaching the end of the input.";
    else if (A.garbage && cur.has(A.garbage)) caption = "Input finished in Garbage. Rejected.";
    else if (accepted) caption = A.kind === "DFA"
      ? `Input finished in ${hits[0]}, an accept state. Accepted.`
      : `Input finished with ${fmt(cur)} active. ${hits.join(", ")} ${hits.length > 1 ? "are accept states" : "is an accept state"}, so a branch accepts. Accepted.`;
    else caption = A.kind === "DFA"
      ? `Input finished in ${[...cur][0]}, which is not an accept state. Rejected.`
      : `Input finished with ${fmt(cur)} active. None of them accept. Rejected.`;
    frames.push({ pos: frames[frames.length - 1].pos, active: [...cur], taken: [], done: true, accepted, visited, caption });
    return { frames, accepted };
  }

  function create({ svg, tape }) {
    if (!document.getElementById("au-style")) {
      const st = document.createElement("style"); st.id = "au-style"; st.textContent = css; document.head.appendChild(st);
    }
    svg.classList.add("au-svg");
    tape.classList.add("au-tape");
    let A, sim, paths, cur, P, L, nodeEls, groups, frame = null, hover = null;

    function draw() {
      svg.innerHTML = "";
      L = { [A.start]: 0 };
      const q = [A.start];
      while (q.length) { const s = q.shift(); for (const e of A.trans) if (e.from === s && !(e.to in L)) { L[e.to] = L[s] + 1; q.push(e.to); } }
      const reach = Math.max(0, ...Object.values(L));
      A.states.forEach(s => { if (!(s in L)) L[s] = reach + 1; });
      const cols = [];
      A.states.forEach(s => (cols[L[s]] ||= []).push(s));
      const maxRows = Math.max(...cols.filter(Boolean).map(c => c.length));
      const CW = 110 + (cols.length - 1) * DX + 80, W = Math.max(CW, 640), ox = (W - CW) / 2, Hm = Math.max(250, maxRows * DY + 150), H = Hm + (A.garbage ? 90 : 0);
      svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
      P = {};
      cols.forEach((c, li) => c.forEach((s, i) => { P[s] = { x: ox + 110 + li * DX, y: Hm / 2 + 20 + (i - (c.length - 1) / 2) * DY }; }));

      const defs = mk("defs", {}, svg);
      for (const k of ["def", "hot", "trace", "ink"]) {
        const m = mk("marker", { id: `au-arrow-${k}`, viewBox: "0 0 10 10", refX: 9, refY: 5, markerWidth: 10, markerHeight: 10, markerUnits: "userSpaceOnUse", orient: "auto" }, defs);
        mk("path", { d: "M0,0 L10,5 L0,10 z", class: `au-m-${k}` }, m);
      }
      const gE = mk("g", {}, svg), gL = mk("g", {}, svg), gN = mk("g", {}, svg);

      groups = new Map();
      A.trans.forEach(e => {
        const k = e.from + SEP + e.to;
        if (!groups.has(k)) groups.set(k, { key: k, from: e.from, to: e.to, syms: [] });
        const g = groups.get(k);
        if (!g.syms.includes(e.sym)) g.syms.push(e.sym);
      });
      for (const g of groups.values()) {
        const a = P[g.from], b = P[g.to];
        let d, lx, ly;
        if (g.from === g.to) {
          const a1 = -Math.PI / 2 - 0.45, a2 = -Math.PI / 2 + 0.45;
          const p0 = [a.x + R * Math.cos(a1), a.y + R * Math.sin(a1)], p1 = [a.x + R * Math.cos(a2), a.y + R * Math.sin(a2)];
          d = `M${p0} C${a.x - 42},${a.y - R - 60} ${a.x + 42},${a.y - R - 60} ${p1}`;
          lx = a.x; ly = a.y - R - 46;
        } else {
          const dx = b.x - a.x, dy = b.y - a.y, len = Math.hypot(dx, dy), nx = dy / len, ny = -dx / len;
          const span = Math.abs(L[g.to] - L[g.from]);
          let off = groups.has(g.to + SEP + g.from) ? 24 : 0;
          if (span > 1 || (span === 0 && Math.abs(dy) > DY * 1.5)) off = Math.max(off, 30 + 14 * span);
          const cx = (a.x + b.x) / 2 + nx * off * 2, cy = (a.y + b.y) / 2 + ny * off * 2;
          const sl = Math.hypot(cx - a.x, cy - a.y), tl = Math.hypot(cx - b.x, cy - b.y);
          const s = [a.x + (cx - a.x) / sl * R, a.y + (cy - a.y) / sl * R];
          const t = [b.x + (cx - b.x) / tl * (R + 1), b.y + (cy - b.y) / tl * (R + 1)];
          d = off ? `M${s} Q${cx},${cy} ${t}` : `M${s} L${t}`;
          lx = 0.25 * s[0] + 0.5 * cx + 0.25 * t[0]; ly = 0.25 * s[1] + 0.5 * cy + 0.25 * t[1];
        }
        g.path = mk("path", { d, class: "au-edge" + (g.syms.every(x => x === "ε") ? " eps" : "") }, gE);
        const text = g.syms.join(", "), w = text.length * 8 + 12;
        g.label = mk("g", { class: "au-elabel" }, gL);
        mk("rect", { x: lx - w / 2, y: ly - 10, width: w, height: 20, rx: 10 }, g.label);
        mk("text", { x: lx, y: ly }, g.label).textContent = text;
      }
      const s0 = P[A.start];
      mk("path", { d: `M${s0.x - R - 42},${s0.y} L${s0.x - R - 1},${s0.y}`, class: "au-start", "marker-end": "url(#au-arrow-ink)" }, gN);

      nodeEls = {};
      A.states.forEach(s => {
        const p = P[s];
        const g = mk("g", { class: "au-node", tabindex: "0", role: "button", "aria-label": `State ${s}${s === A.start ? ", start" : ""}${A.accept.has(s) ? ", accepting" : ""}` }, gN);
        mk("circle", { cx: p.x, cy: p.y, r: R, class: "body" }, g);
        if (A.accept.has(s)) mk("circle", { cx: p.x, cy: p.y, r: R - 5, class: "inner" }, g);
        mk("text", { x: p.x, y: p.y }, g).textContent = s;
        const on = () => { hover = s; paint(); }, off = () => { hover = null; paint(); };
        g.addEventListener("pointerenter", on); g.addEventListener("pointerleave", off);
        g.addEventListener("focus", on); g.addEventListener("blur", off);
        g.addEventListener("click", () => { hover = hover === s ? null : s; paint(); });
        nodeEls[s] = g;
      });
      if (A.garbage) {
        const gx = Math.max(ox + CW - 80, ox + 160), gy = H - 52;
        const g = mk("g", { class: "au-garbage", role: "img", "aria-label": "Garbage state: any missing transition leads here and the input is rejected" }, gN);
        mk("rect", { x: gx - 48, y: gy - 19, width: 96, height: 38, rx: 19 }, g);
        mk("text", { x: gx, y: gy }, g).textContent = "Garbage";
        mk("text", { x: gx, y: gy + 33, class: "au-garbage-note" }, g).textContent = "missing transitions";
        nodeEls[A.garbage] = g;
      }
    }

    function paint() {
      const f = frame, done = f && f.done;
      const active = new Set(f ? f.active : []);
      const taken = new Set(f && !done ? f.taken.map(e => e.from + SEP + e.to) : []);
      const targets = new Set(hover ? A.trans.filter(e => e.from === hover).map(e => e.to) : []);
      A.states.forEach(s => {
        let st = "Background";
        if (f && done) {
          if (active.has(s)) st = f.accepted ? (A.accept.has(s) ? "Solution" : "Background") : "Rejected";
          else if (!f.visited.has(s)) st = "Untraveled";
        } else if (f && active.has(s)) st = "Active";
        let cls = "au-node " + st;
        if (hover) cls += s === hover ? " trace" : targets.has(s) ? "" : " faint";
        nodeEls[s].setAttribute("class", cls);
      });
      if (A.garbage) {
        let cls = "au-garbage";
        if (active.has(A.garbage)) cls += done ? " Rejected" : " Active";
        if (hover) cls += " faint";
        nodeEls[A.garbage].setAttribute("class", cls);
      }
      for (const g of groups.values()) {
        let state = taken.has(g.key) ? "hot" : "";
        if (hover) state = g.from === hover ? "trace" : "faint";
        g.path.setAttribute("class", "au-edge" + (g.syms.every(x => x === "ε") ? " eps" : "") + (state ? " " + state : ""));
        g.path.setAttribute("marker-end", `url(#au-arrow-${state === "hot" ? "hot" : state === "trace" ? "trace" : "def"})`);
        g.label.setAttribute("class", "au-elabel" + (state ? " " + state : ""));
      }
      tape.innerHTML = "";
      A.input.forEach((ch, i) => {
        const c = document.createElement("div");
        let cls = "au-cell";
        if (f) {
          if (i < f.pos) cls += " read";
          if (!done && i === f.pos - 1 && f.sym !== undefined) cls += " just";
          if (!done && i === f.pos) cls += " head";
        }
        c.className = cls; c.textContent = ch; tape.appendChild(c);
      });
      const end = document.createElement("div");
      end.className = "au-cell end" + (done ? (f.accepted ? " acc" : " rej") : "") + (f && !done && f.pos === A.input.length ? " head" : "");
      end.textContent = done ? (f.accepted ? "accept" : "reject") : A.input.length ? "end" : "empty input";
      tape.appendChild(end);
    }

    return {
      load(str, kind) {
        const parsed = parse(str, kind);
        A = parsed; sim = simulate(A); cur = sim.frames; frame = null; hover = null;
        paths = A.kind === "NFA" ? enumeratePaths(A) : [];
        draw(); paint();
        return { frames: sim.frames, accepted: sim.accepted, states: A.states, accept: A.accept, start: A.start, garbage: A.garbage,
          paths: paths.map(p => ({ label: p.label, accepted: p.accepted })), pathLimitHit: paths.length >= PATH_LIMIT };
      },
      // null = all branches at once; otherwise follow paths[i] alone.
      usePath(i) {
        const p = i === null ? null : paths[i];
        cur = p ? p.frames : sim.frames; frame = null; paint();
        return { frames: cur, accepted: p ? p.accepted : sim.accepted };
      },
      show(i) { frame = i === null ? null : cur[i]; paint(); return frame; },
      resetHover() { hover = null; paint(); },
    };
  }

  return { create };
})();
