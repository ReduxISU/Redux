/* ---- Search Tree view (and its Game Tree variant), shared by both mockups ----
   A solver's run drawn as the tree of choices it made. Each solver records a list of NODE PATCHES:
     { id, parent, label, st, note, val, order, caption }
   i.e. the step it already records ("tried x2 = false", "pruned: bound 230 < 240") plus a parent pointer.
   The view replays patches up to the current step, folds finished subtrees when the tree gets big,
   and lays it out level by level. Pages supply the --av-* tokens. */
const SearchTreeView = (() => {
  const NS = "http://www.w3.org/2000/svg";
  const mk = (tag, attrs = {}, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; };
  const ROW = 74, NH = 26, MAX_LEAVES = 12, MAX_FRAMES = 420, GUTTER = 92;

  const css = `
.st-svg { height: auto; display: block; }
.st-svg text { font-family: var(--av-mono); }
.st-edge { fill: none; stroke: var(--av-edge); stroke-width: 1.4; }
.st-edge.pruned, .st-edge.skipped { stroke-dasharray: 4 4; }
.st-edge.pruned { stroke: var(--av-rej); }
.st-edge.sol { stroke: var(--av-sol); stroke-width: 3; }
.st-edge.cur { stroke: var(--av-hl); stroke-width: 2.6; }
.st-node rect { fill: var(--av-bg); stroke: var(--av-stroke); stroke-width: 1.5; }
.st-node text.lbl { font-size: 12px; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); }
.st-node text.note { font-size: 10.5px; text-anchor: middle; fill: var(--av-muted); }
.st-node.queued rect { stroke-dasharray: 3 3; }
.st-node.explored rect { fill: var(--av-sol-fill); stroke: var(--av-sol); }
.st-node.failed rect { fill: var(--av-rej-fill); stroke: var(--av-rej); }
.st-node.failed text.lbl { fill: var(--av-rej); }
.st-node.pruned rect { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-dasharray: 4 3; }
.st-node.pruned text.lbl, .st-node.pruned text.note { fill: var(--av-rej); }
.st-node.skipped rect { fill: transparent; stroke: var(--av-stroke); stroke-dasharray: 4 4; }
.st-node.skipped text.lbl, .st-node.skipped text.note { fill: var(--av-muted); }
.st-node.solution rect { fill: var(--av-sol); stroke: var(--av-sol); }
.st-node.solution text.lbl { fill: var(--av-on-sol); font-weight: 600; }
.st-node.cur rect { stroke: var(--av-hl); stroke-width: 3.2; stroke-dasharray: none; }
.st-node.fold rect { fill: var(--av-surface); stroke: var(--av-stroke); stroke-dasharray: 2 3; }
.st-node.fold text.lbl { fill: var(--av-muted); font-size: 11px; }
.st-node.trace rect { stroke: var(--av-hot); stroke-width: 3; }
.st-node { cursor: default; }
.st-ord circle { fill: var(--av-ink); stroke: var(--av-surface); stroke-width: 1.5; } .st-ord text { fill: var(--av-surface); font-size: 9.5px; font-weight: 700; text-anchor: middle; dominant-baseline: central; }
.st-val circle { stroke-width: 1.6; } .st-val text { font-size: 10px; font-weight: 700; text-anchor: middle; dominant-baseline: central; }
.st-val.win circle { fill: var(--av-sol); stroke: var(--av-sol); } .st-val.win text { fill: var(--av-on-sol); }
.st-val.lose circle { fill: var(--av-rej-fill); stroke: var(--av-rej); } .st-val.lose text { fill: var(--av-rej); }
.st-val.p1 circle { fill: var(--av-g0); stroke: var(--av-g0s); } .st-val.p2 circle { fill: var(--av-g1); stroke: var(--av-g1s); } .st-val.p1 text, .st-val.p2 text { fill: #10131a; }
.st-band { fill: var(--av-surface); }
.st-band.alt { fill: var(--av-hl-fill); opacity: .35; }
.st-lvl { font-size: 11px; fill: var(--av-muted); }
.st-lvl.q { font-weight: 700; fill: var(--av-ink); }`;

  /* ---------------- parsing ---------------- */
  function parseCNF(str, max3) {
    const s = String(str).replace(/\s+/g, "").replace(/[¬~]/g, "!");
    if (!s) throw new Error("The formula is empty.");
    return s.replace(/[()]/g, "").split("&").map((c, i) => {
      const lits = c.split("|");
      if (!c || lits.some(l => !l)) throw new Error(`Clause ${i + 1} is empty or has an empty literal.`);
      if (max3 && lits.length > 3) throw new Error(`Clause ${i + 1} has ${lits.length} literals; 3SAT allows at most 3.`);
      for (const l of lits) if (!/^!?[A-Za-z]\w*$/.test(l)) throw new Error(`"${l}" isn't a literal. Use a name like x1, or !x1 for its negation.`);
      return lits;
    });
  }
  const vname = l => (l[0] === "!" ? l.slice(1) : l);
  const litVal = (l, a) => { const v = a.get(vname(l)); return v === undefined ? undefined : l[0] === "!" ? !v : v; };

  function tokenize(s) {
    const out = []; let buf = "";
    for (const ch of s) { if ("{}(),".includes(ch)) { if (buf.trim()) out.push(buf.trim()); buf = ""; out.push(ch); } else buf += ch; }
    if (buf.trim()) out.push(buf.trim());
    return out;
  }
  function tree(tokens) {
    let i = 0;
    function val() {
      const t = tokens[i];
      if (t === "{" || t === "(") {
        i++;
        const close = t === "{" ? "}" : ")", node = { t: t === "{" ? "set" : "tup", items: [] };
        if (tokens[i] === close) { i++; return node; }
        for (;;) {
          node.items.push(val());
          if (tokens[i] === ",") { i++; continue; }
          if (tokens[i] === close) { i++; return node; }
          throw new Error("Brackets don't match up.");
        }
      }
      if (t === undefined || "}),".includes(t)) throw new Error(t === undefined ? "The instance ends too early." : `Unexpected "${t}".`);
      i++; return t;
    }
    const v = val();
    if (i !== tokens.length) throw new Error("Extra text after the instance.");
    return v;
  }
  // ((N,E),K) with edges {a,b} or ({a,b},w)
  function parseGraph(str, weighted) {
    const top = tree(tokenize(str));
    if (top.t !== "tup" || !top.items[0] || top.items[0].t !== "tup") throw new Error("Expected ((N,E),K).");
    const [Nn, En] = top.items[0].items, K = Number(top.items[1]);
    if (!Nn || Nn.t !== "set" || !En || En.t !== "set") throw new Error("Expected a node set and an edge set: ((N,E),K).");
    if (!Number.isInteger(K) || K < 0) throw new Error("K must be a whole number.");
    const nodes = Nn.items.map(x => { if (typeof x !== "string") throw new Error("Node names must be plain names."); return x; });
    if (!nodes.length || new Set(nodes).size !== nodes.length) throw new Error("N must be a non-empty set with no repeats.");
    const S = new Set(nodes), edges = [];
    for (const e of En.items) {
      let pair, w = null;
      if (e.t === "set") pair = e.items;
      else if (e.t === "tup" && e.items[0] && e.items[0].t === "set") { pair = e.items[0].items; w = Number(e.items[1]); if (!Number.isFinite(w)) throw new Error("Edge weights must be numbers."); }
      else throw new Error(weighted ? "Each edge must look like ({a,b},w)." : "Each edge must look like {a,b}.");
      if (pair.length !== 2 || pair.some(x => typeof x !== "string")) throw new Error("Each edge joins exactly two nodes.");
      if (!S.has(pair[0]) || !S.has(pair[1])) throw new Error(`Edge {${pair[0]},${pair[1]}} uses a node that isn't in N.`);
      if (weighted && w === null) throw new Error("Every edge needs a weight: ({a,b},w).");
      edges.push({ a: pair[0], b: pair[1], w });
    }
    return { nodes, edges, K };
  }
  function parseKnapsack(str) {
    const s = String(str).replace(/\s+/g, "");
    const m = /^\(\{(.*)\},(-?\d+),(-?\d+)\)$/.exec(s);
    if (!m) throw new Error("Expected ({(w1,v1),(w2,v2),…},W,V).");
    const items = [], re = /\((\d+),(\d+)\)/g;
    if (m[1].replace(re, "").replace(/,/g, "")) throw new Error("Each item must look like (weight,value).");
    let t; while ((t = re.exec(m[1]))) items.push({ w: +t[1], v: +t[2], i: items.length });
    if (!items.length) throw new Error("There must be at least one item.");
    if (items.length > 12) throw new Error("The tree view draws up to 12 items.");
    return { items, W: +m[2], V: +m[3] };
  }
  function parseN(str) {
    const n = Number(String(str).trim());
    if (!Number.isInteger(n) || n < 1) throw new Error("Enter a whole number n, the board size.");
    if (n > 7) throw new Error("The tree view draws boards up to 7 × 7; past that the tree is too big to read.");
    return n;
  }
  // QBF: a quantifier prefix, a colon, then a CNF formula in Redux's SAT syntax.
  function parseQBF(str) {
    const at = String(str).indexOf(":");
    if (at < 0) throw new Error("Expected a quantifier prefix, a colon, then the formula, e.g. forall x1 exists x2 : (x1 | !x2).");
    const pre = str.slice(0, at).replace(/∀/g, " forall ").replace(/∃/g, " exists ").trim().split(/[\s,]+/).filter(Boolean);
    const prefix = []; let q = null;
    for (const t of pre) {
      const lo = t.toLowerCase();
      if (lo === "forall" || lo === "a") { q = "A"; continue; }
      if (lo === "exists" || lo === "e") { q = "E"; continue; }
      if (!q) throw new Error(`"${t}" needs a quantifier (forall or exists) in front of it.`);
      if (!/^[A-Za-z]\w*$/.test(t)) throw new Error(`"${t}" isn't a variable name.`);
      if (prefix.some(p => p.v === t)) throw new Error(`${t} is quantified twice.`);
      prefix.push({ q, v: t });
    }
    if (!prefix.length) throw new Error("The prefix needs at least one quantified variable.");
    if (prefix.length > 6) throw new Error("The tree view draws up to 6 quantified variables.");
    const clauses = parseCNF(str.slice(at + 1), false);
    for (const c of clauses) for (const l of c) if (!prefix.some(p => p.v === vname(l))) throw new Error(`${vname(l)} appears in the formula but isn't quantified.`);
    return { prefix, clauses };
  }
  // Generalized Geography: (N, E, s) with directed edges (u,v), Redux's SSSP shape without weights.
  function parseGeography(str) {
    const top = tree(tokenize(str));
    if (top.t !== "tup" || top.items.length !== 3) throw new Error("Expected (N, E, start), e.g. ({1,2,3},{(1,2),(2,3)},1).");
    const [Nn, En, s] = top.items;
    if (!Nn || Nn.t !== "set" || !En || En.t !== "set" || typeof s !== "string") throw new Error("Expected a node set, an edge set and a start node.");
    const nodes = Nn.items.map(x => { if (typeof x !== "string") throw new Error("Node names must be plain names."); return x; });
    if (new Set(nodes).size !== nodes.length) throw new Error("N has a repeated node.");
    if (!nodes.includes(s)) throw new Error(`The start node ${s} isn't in N.`);
    if (nodes.length > 10) throw new Error("The tree view draws up to 10 nodes.");
    const out = new Map(nodes.map(n => [n, []]));
    for (const e of En.items) {
      if (e.t !== "tup" || e.items.length !== 2 || e.items.some(x => typeof x !== "string")) throw new Error("Each edge must look like (u,v).");
      const [u, v] = e.items;
      if (!out.has(u) || !out.has(v)) throw new Error(`Edge (${u},${v}) uses a node that isn't in N.`);
      if (!out.get(u).includes(v)) out.get(u).push(v);
    }
    return { nodes, out, s };
  }

  /* ---------------- recorder: node patches + captions ---------------- */
  function recorder() {
    const ev = []; let next = 0;
    return {
      ev,
      add(parent, label, extra, caption, merge) { const id = next++; ev.push({ id, parent, label, ...extra, caption, merge }); return id; },
      set(id, patch, caption, merge) { ev.push({ id, ...patch, caption, merge }); },
      note(caption, extra) { ev.push({ caption, ...(extra || {}) }); },
    };
  }

  /* ---------------- 3SAT: Redux's "3SAT Backtracking" (best-first) ---------------- */
  // Follows Sat3BacktrackingSolver + SAT3PQObject: the next variable is the first literal left in the
  // reduced formula; a child that falsifies a one-literal clause is never created; children go into a
  // priority queue keyed by totalVars - depth - (largest positive weight).
  function sat3Redux(clauses0) {
    const R = recorder();
    const all = clauses0.flat();
    const totalVars = new Set(all.map(l => l[l.length - 1])).size; // Redux counts by a literal's LAST CHARACTER
    const mkNode = (clauses, depth, weights, states) => {
      const lits = clauses.flat();
      return { clauses, depth, weights, states, nextVar: vname(lits[0]) };
    };
    const rootClauses = clauses0.map(c => [...new Set(c)]);
    const rootW = new Map();
    clauses0.flat().slice(1).forEach(l => { const v = vname(l); rootW.set(v, (rootW.get(v) || 0) + 1); });
    const root = mkNode(rootClauses, 0, rootW, new Map());
    const hi = w => { let h = 0; for (const x of w.values()) if (x > h) h = x; return h; };
    const pqW = n => totalVars - n.depth - hi(n.weights);
    root.id = R.add(null, "start", { st: "queued", note: `priority ${pqW(root)}` }, `Start with the whole formula. Its first literal is ${root.nextVar}, so ${root.nextVar} is decided first. The root waits in the priority queue at priority ${pqW(root)}.`);
    const pq = [{ n: root, w: pqW(root), seq: 0 }]; let seq = 1, order = 0, found = null;
    const fmtC = cs => cs.map(c => "(" + c.join(" | ") + ")").join(" & ");
    while (!found && pq.length) {
      let bi = 0; for (let i = 1; i < pq.length; i++) if (pq[i].w < pq[bi].w || (pq[i].w === pq[bi].w && pq[i].seq < pq[bi].seq)) bi = i;
      const { n } = pq.splice(bi, 1)[0];
      R.set(n.id, { st: "explored", order: ++order }, `Take the queued node with the lowest priority (${pqW(n)}) and branch on ${n.nextVar}.`);
      for (const b of [true, false]) {
        let valid = true; const out = [];
        for (const clause of n.clauses) {
          let tmp = [], sat = false;
          for (const lit of clause) {
            if (vname(lit) === n.nextVar) {
              if ((lit[0] === "!" && !b) || (lit[0] !== "!" && b)) {
                for (const o of clause) if (vname(o) !== n.nextVar) n.weights.set(o, (n.weights.get(o) || 0) - 1);
                tmp = []; sat = true; break;
              } else if (clause.length === 1) valid = false;
            } else tmp.push(lit);
          }
          if (!sat && tmp.length) out.push(tmp);
        }
        const label = `${n.nextVar}=${b ? "T" : "F"}`;
        if (!valid) {
          R.add(n.id, label, { st: "pruned", note: "unit clause false" }, `${label} would make a one-literal clause false, so that child is never created.`, true);
          continue;
        }
        const states = new Map(n.states); if (!states.has(n.nextVar)) states.set(n.nextVar, b);
        if (!out.length) {
          const id = R.add(n.id, label, { st: "solution" }, `${label} satisfies every remaining clause. Solution found.`);
          found = { id, states };
          break;
        }
        const child = mkNode(out, n.depth + 1, new Map(n.weights), states);
        child.id = R.add(n.id, label, { st: "queued", note: `priority ${pqW(child)}` }, `${label} leaves ${fmtC(out)}. Queue it at priority ${pqW(child)}.`);
        pq.push({ n: child, w: pqW(child), seq: seq++ });
      }
    }
    if (found) {
      const asg = new Map(found.states);
      for (const l of all) if (!asg.has(vname(l))) asg.set(vname(l), false);
      return { R, ok: true, end: found.id, asg, caption: `Satisfiable: ${[...asg].map(([k, v]) => `${k}=${v ? "T" : "F"}`).join(", ")}. Variables the search never set default to false, as in Redux.` };
    }
    return { R, ok: false, caption: "The queue ran empty without satisfying every clause. No solution." };
  }

  /* ---------------- 3SAT: DPLL with unit propagation (not in Redux yet) ---------------- */
  function sat3Dpll(clauses) {
    const R = recorder();
    const vars = [...new Set(clauses.flat().map(vname))];
    const propagate = a => {
      const forced = [];
      for (;;) {
        let changed = false;
        for (const c of clauses) {
          const vals = c.map(l => litVal(l, a));
          if (vals.some(v => v === true)) continue;
          const open = c.filter((l, i) => vals[i] === undefined);
          if (!open.length) return { conflict: c, forced };
          if (open.length === 1) { const l = open[0]; a.set(vname(l), l[0] !== "!"); forced.push(`${vname(l)}=${l[0] !== "!" ? "T" : "F"}`); changed = true; }
        }
        if (!changed) return { forced };
      }
    };
    const allSat = a => clauses.every(c => c.some(l => litVal(l, a) === true));
    let found = null;
    const rootA = new Map(), p0 = propagate(rootA);
    const root = R.add(null, "start", { st: "explored", note: p0.forced.length ? "unit: " + p0.forced.join(" ") : "" }, p0.forced.length ? `Before branching, one-literal clauses force ${p0.forced.join(", ")}.` : "Start with nothing assigned. No clause forces anything yet.");
    if (p0.conflict) { R.set(root, { st: "failed", note: "conflict" }, `Clause (${p0.conflict.join(" | ")}) is already false. No solution.`); return { R, ok: false, caption: "Unsatisfiable before any choice." }; }
    function rec(id, a) {
      if (allSat(a)) { found = { id, a }; return true; }
      const v = vars.find(x => !a.has(x));
      if (v === undefined) return false;
      for (const b of [true, false]) {
        const a2 = new Map(a); a2.set(v, b);
        const p = propagate(a2);
        const label = `${v}=${b ? "T" : "F"}`;
        if (p.conflict) {
          R.add(id, label, { st: "pruned", note: "conflict" }, `${label}${p.forced.length ? ", then unit " + p.forced.join(", ") : ""} makes (${p.conflict.join(" | ")}) false. Cut this branch.`, true);
          continue;
        }
        const cid = R.add(id, label, { st: "explored", note: p.forced.length ? "unit: " + p.forced.join(" ") : "" }, `Try ${label}${p.forced.length ? `. Unit clauses then force ${p.forced.join(", ")}` : ""}.`);
        if (rec(cid, a2)) return true;
        R.set(cid, { st: "failed" }, `Every branch under ${label} failed. Back up.`);
      }
      return false;
    }
    if (rec(root, rootA)) {
      for (const x of vars) if (!found.a.has(x)) found.a.set(x, false);
      return { R, ok: true, end: found.id, asg: found.a, caption: `Every clause is true: ${[...found.a].map(([k, v]) => `${k}=${v ? "T" : "F"}`).join(", ")}.` };
    }
    R.set(root, { st: "failed" }, "Every branch failed.");
    return { R, ok: false, caption: "Every branch hits a false clause. Unsatisfiable." };
  }

  /* ---------------- Knapsack: depth-first branch and bound (not in Redux yet) ---------------- */
  function knapsackBB(K) {
    const R = recorder();
    const items = K.items.slice().sort((x, y) => y.v / y.w - x.v / x.w || x.i - y.i);
    const bound = (k, w, v) => { let cap = K.W - w, b = v; for (let j = k; j < items.length && cap > 0; j++) { const t = Math.min(1, cap / items[j].w); b += items[j].v * t; cap -= items[j].w * t; } return Math.floor(b * 100) / 100; };
    let found = null;
    const root = R.add(null, "start", { st: "explored", note: `≤ ${bound(0, 0, 0)}` }, `Sort items by value per unit of weight: ${items.map(it => `(${it.w},${it.v})`).join(", ")}. Even taking a fraction of the next item, the best possible value is ${bound(0, 0, 0)}.`);
    function rec(id, k, w, v, picked) {
      if (v >= K.V) { found = { id, picked, w, v }; return true; }
      if (k === items.length) return false;
      const it = items[k];
      for (const take of [true, false]) {
        const w2 = w + (take ? it.w : 0), v2 = v + (take ? it.v : 0), b = bound(k + 1, w2, v2);
        const label = `${take ? "take" : "skip"} (${it.w},${it.v})`;
        if (w2 > K.W) { R.add(id, label, { st: "pruned", note: `weight ${w2} > ${K.W}` }, `Taking (${it.w},${it.v}) brings the weight to ${w2}, over W = ${K.W}. Cut.`, true); continue; }
        if (b < K.V) { R.add(id, label, { st: "pruned", note: `≤ ${b} < ${K.V}` }, `${take ? "Taking" : "Skipping"} (${it.w},${it.v}): even the best case reaches only ${b}, below V = ${K.V}. Cut.`, true); continue; }
        const cid = R.add(id, label, { st: v2 >= K.V ? "solution" : "explored", note: `w ${w2} · v ${v2} · ≤ ${b}` }, `${take ? "Take" : "Skip"} (${it.w},${it.v}): weight ${w2}, value ${v2}, best case ${b}.`);
        if (rec(cid, k + 1, w2, v2, take ? [...picked, it] : picked)) return true;
        R.set(cid, { st: "failed" }, `Nothing under "${label}" reaches V = ${K.V}. Back up.`);
      }
      return false;
    }
    if (rec(root, 0, 0, 0, [])) return { R, ok: true, end: found.id, picked: found.picked, caption: `Reached value ${found.v} with weight ${found.w}: at least V = ${K.V} and at most W = ${K.W}.` };
    R.set(root, { st: "failed" }, "Every branch is cut or falls short.");
    return { R, ok: false, caption: `No set of items reaches V = ${K.V} within W = ${K.W}.` };
  }

  /* ---------------- TSP: Redux's "Traveling Salesperson Branch and Bound" ---------------- */
  function tspBB(G) {
    const R = recorder(), n = G.nodes.length, INF = Infinity;
    const idx = new Map(G.nodes.map((x, i) => [x, i]));
    const M0 = Array.from({ length: n }, () => Array(n).fill(INF));
    for (const e of G.edges) { const i = idx.get(e.a), j = idx.get(e.b); M0[i][j] = e.w; M0[j][i] = e.w; }
    const clone = M => M.map(r => r.slice());
    const reduce = M => {
      let c = 0;
      for (let i = 0; i < n; i++) { const m = Math.min(...M[i]); if (m > 0 && m < INF) { c += m; for (let j = 0; j < n; j++) if (M[i][j] < INF) M[i][j] -= m; } }
      for (let j = 0; j < n; j++) { let m = INF; for (let i = 0; i < n; i++) m = Math.min(m, M[i][j]); if (m > 0 && m < INF) { c += m; for (let i = 0; i < n; i++) if (M[i][j] < INF) M[i][j] -= m; } }
      return c;
    };
    const cost = p => { let t = 0; for (let i = 0; i + 1 < p.length; i++) { if (M0[p[i]][p[i + 1]] === INF) return INF; t += M0[p[i]][p[i + 1]]; } const r = M0[p[p.length - 1]][p[0]]; return r === INF ? INF : t + r; };
    // Initial best from Redux's TSPGreedy: nearest neighbour from every start, keeping the cheapest tour within K.
    let best = null, bestCost = INF;
    for (let s = 0; s < n; s++) {
      const route = [s], seen = new Set([s]); let cur = s, ok = true, tot = 0;
      while (seen.size < n) {
        let nx = -1, bw = INF;
        for (let j = 0; j < n; j++) if (!seen.has(j) && M0[cur][j] < bw) { bw = M0[cur][j]; nx = j; }
        if (nx < 0) { ok = false; break; }
        tot += bw; route.push(nx); seen.add(nx); cur = nx;
      }
      if (!ok || M0[cur][s] === INF) continue;
      tot += M0[cur][s];
      if (tot <= G.K && tot < bestCost) { bestCost = tot; best = route; }
    }
    const nm = i => G.nodes[i];
    R.note(best ? `First, Redux's greedy solver gives a starting tour: ${best.map(nm).join(" → ")} → ${nm(best[0])}, cost ${bestCost}. Any branch whose lower bound reaches ${bestCost} can be cut.` : `Greedy finds no tour within K = ${G.K}, so the search starts with no tour to beat.`);
    const M = clone(M0), lb = reduce(M);
    const pq = []; let pushId = 0, order = 0;
    const enqueue = s => { s.prio = [s.bound / s.path.length, s.bound, -s.path.length, pushId++]; pq.push(s); };
    const less = (a, b) => { for (let k = 0; k < 4; k++) if (a.prio[k] !== b.prio[k]) return a.prio[k] < b.prio[k]; return false; };
    const root = { path: [0], M, bound: lb, visited: new Set([0]) };
    root.id = R.add(null, nm(0), { st: "queued", note: `lb ${lb}` }, `Start at ${nm(0)}. Reducing every row and column of the cost table gives a lower bound of ${lb} for any tour.`);
    enqueue(root);
    let bestId = null;
    while (pq.length) {
      let bi = 0; for (let i = 1; i < pq.length; i++) if (less(pq[i], pq[bi])) bi = i;
      const cur = pq.splice(bi, 1)[0];
      if (cur.bound >= bestCost) { R.set(cur.id, { st: "pruned", note: `lb ${cur.bound} ≥ ${bestCost}` }, `${cur.path.map(nm).join(" → ")} waited in the queue, but its bound ${cur.bound} now reaches the best tour (${bestCost}). Cut.`, true); continue; }
      if (cur.path.length === n) {
        const c = cost(cur.path);
        if (c < bestCost) {
          if (bestId !== null) R.set(bestId, { st: "explored" });
          bestCost = c; best = cur.path.slice(); bestId = cur.id;
          R.set(cur.id, { st: "solution", order: ++order, note: `tour ${c}` }, `Complete tour ${cur.path.map(nm).join(" → ")} → ${nm(0)} costs ${c}, the new best.`);
        } else R.set(cur.id, { st: "failed", order: ++order, note: `tour ${c}` }, `Complete tour costs ${c}, no better than ${bestCost}.`);
        continue;
      }
      R.set(cur.id, { st: "explored", order: ++order }, `Expand ${cur.path.map(nm).join(" → ")}: it has the lowest score in the queue (bound ${cur.bound} ÷ ${cur.path.length} ${cur.path.length === 1 ? "city" : "cities"}), Redux's ordering.`);
      const last = cur.path[cur.path.length - 1];
      for (let nx = 0; nx < n; nx++) {
        if (cur.visited.has(nx) || cur.M[last][nx] === INF) continue;
        const cm = clone(cur.M);
        for (let j = 0; j < n; j++) cm[last][j] = INF;
        for (let i = 0; i < n; i++) cm[i][nx] = INF;
        cm[nx][0] = INF; cm[nx][last] = INF;
        const b = cur.bound + cur.M[last][nx] + reduce(cm);
        if (b >= bestCost) { R.add(cur.id, nm(nx), { st: "pruned", note: `lb ${b} ≥ ${bestCost}` }, `Going on to ${nm(nx)} gives a lower bound of ${b}, no better than ${bestCost}. Cut.`, true); continue; }
        const ch = { path: [...cur.path, nx], M: cm, bound: b, visited: new Set([...cur.visited, nx]) };
        ch.id = R.add(cur.id, nm(nx), { st: "queued", note: `lb ${b}` }, `Going on to ${nm(nx)} gives a lower bound of ${b}. Queue it.`);
        enqueue(ch);
      }
    }
    if (best && bestCost <= G.K) {
      if (bestId === null) R.note(`The search never beat the greedy tour, so the greedy tour is optimal.`);
      return { R, ok: true, end: bestId, tour: best.map(nm), cost: bestCost, caption: `Best tour: ${best.map(nm).join(" → ")} → ${nm(best[0])}, cost ${bestCost}, within K = ${G.K}.` };
    }
    return { R, ok: false, caption: best ? `The best tour costs ${bestCost}, more than K = ${G.K}.` : `No tour within K = ${G.K}.` };
  }

  /* ---------------- Graph Coloring: backtracking (not in Redux yet) ---------------- */
  function colorBT(G) {
    const R = recorder(), adj = new Map(G.nodes.map(x => [x, new Set()]));
    G.edges.forEach(e => { adj.get(e.a).add(e.b); adj.get(e.b).add(e.a); });
    const K = Math.min(G.K, G.nodes.length), color = new Map();
    let found = null;
    const root = R.add(null, "start", { st: "explored" }, `Color the nodes in order ${G.nodes.join(", ")}, trying colors 1 to ${K}.`);
    function rec(id, i) {
      if (i === G.nodes.length) { found = id; return true; }
      const x = G.nodes[i];
      for (let c = 1; c <= K; c++) {
        const clash = [...adj.get(x)].find(y => color.get(y) === c);
        const label = `${x}=${c}`;
        if (clash !== undefined) { R.add(id, label, { st: "pruned", note: `${clash} has ${c}` }, `${label} clashes with neighbor ${clash}, which already has color ${c}. Cut.`, true); continue; }
        color.set(x, c);
        const cid = R.add(id, label, { st: i === G.nodes.length - 1 ? "solution" : "explored" }, `Give ${x} color ${c}. No neighbor has it.`);
        if (rec(cid, i + 1)) return true;
        color.delete(x);
        R.set(cid, { st: "failed" }, `No color works further down from ${label}. Back up.`);
      }
      return false;
    }
    if (rec(root, 0)) return { R, ok: true, end: found, color: new Map(color), caption: `Every node is colored with no clash, using at most K = ${G.K} colors.` };
    R.set(root, { st: "failed" }, "Every branch clashes.");
    return { R, ok: false, caption: `Not colorable with K = ${G.K}.` };
  }

  /* ---------------- N-Queens: Redux's "N-Queens Backtracking" ---------------- */
  function queensBT(n) {
    const R = recorder(), board = [];
    let found = null;
    const root = R.add(null, "start", { st: "explored" }, `Place queens row by row, trying columns 0 to ${n - 1} in order, as Redux's solver does.`);
    function rec(id, row) {
      if (row === n) { found = id; return true; }
      for (let col = 0; col < n; col++) {
        let hit = -1;
        for (let i = 0; i < row; i++) if (board[i] === col || Math.abs(board[i] - col) === Math.abs(i - row)) { hit = i; break; }
        const label = `(${row},${col})`;
        if (hit >= 0) { R.add(id, label, { st: "pruned", note: `hit by (${hit},${board[hit]})` }, `Row ${row}, column ${col}: attacked by the queen at (${hit},${board[hit]}). Skip.`, true); continue; }
        board[row] = col;
        const cid = R.add(id, label, { st: row === n - 1 ? "solution" : "explored" }, `Put a queen at row ${row}, column ${col}.`);
        if (rec(cid, row + 1)) return true;
        R.set(cid, { st: "failed" }, `No safe column in the rows below (${row},${col}). Take it back.`);
      }
      return false;
    }
    if (rec(root, 0)) return { R, ok: true, end: found, board: board.slice(), caption: `All ${n} queens placed, none attacking another.` };
    R.set(root, { st: "failed" }, "Every placement runs out of safe columns.");
    return { R, ok: false, caption: `No way to place ${n} queens.` };
  }

  /* ---------------- QBF: game-tree evaluation (not in Redux yet) ---------------- */
  function qbfGame(Q) {
    const R = recorder();
    const decide = a => {
      let all = true;
      for (const c of Q.clauses) {
        const vals = c.map(l => litVal(l, a));
        if (vals.some(v => v === true)) continue;
        if (vals.every(v => v === false)) return false;
        all = false;
      }
      return all ? true : undefined;
    };
    const root = R.add(null, "start", { st: "explored", lvl: 0 }, `Players take turns setting variables in prefix order. ∃ wants the formula true, ∀ wants it false.`);
    function rec(id, k, a) {
      const d = decide(a);
      if (d !== undefined) { R.set(id, { val: d, st: d ? "explored" : "failed" }, d ? "Every clause is already true, whatever comes next. This position is true." : "A clause is already false. This position is false."); return d; }
      const { q, v } = Q.prefix[k];
      let result = q === "A";
      const kids = [];
      for (const b of [true, false]) kids.push({ b, label: `${v}=${b ? "T" : "F"}` });
      for (let j = 0; j < 2; j++) {
        const { b, label } = kids[j];
        const a2 = new Map(a); a2.set(v, b);
        const cid = R.add(id, label, { st: "explored", lvl: k + 1 }, `${q === "E" ? "∃" : "∀"} tries ${label}.`);
        const r = rec(cid, k + 1, a2);
        if (q === "E" && r) { result = true; if (j === 0) R.add(id, kids[1].label, { st: "skipped", lvl: k + 1, note: "not needed" }, `∃ already wins with ${label}, so ${kids[1].label} is never looked at.`); break; }
        if (q === "A" && !r) { result = false; if (j === 0) R.add(id, kids[1].label, { st: "skipped", lvl: k + 1, note: "not needed" }, `∀ already wins with ${label}, so ${kids[1].label} is never looked at.`); break; }
      }
      R.set(id, { val: result, st: result ? "explored" : "failed" }, `${q === "E" ? "∃" : "∀"} at ${v}: this position is ${result ? "true" : "false"}.`);
      return result;
    }
    const ok = rec(root, 0, new Map());
    return { R, ok, end: null, caption: ok ? "The formula is true: ∃ has a winning strategy." : "The formula is false: ∀ has a winning strategy.", qbfTrue: ok };
  }

  /* ---------------- Generalized Geography: game-tree search (not in Redux yet) ---------------- */
  function geoGame(Gg) {
    const R = recorder();
    const root = R.add(null, Gg.s, { st: "explored", lvl: 0 }, `The token starts on ${Gg.s}. Player 1 moves first. Each move follows an edge to a node nobody has visited; whoever can't move loses.`);
    function rec(id, at, seen, depth) {
      const mover = depth % 2 === 0 ? 1 : 2;
      const moves = Gg.out.get(at).filter(v => !seen.has(v));
      if (!moves.length) { R.set(id, { val: 3 - mover, note: `P${mover} stuck` }, `Player ${mover} has no unvisited node to move to from ${at} and loses.`); return false; }
      let win = false;
      for (let j = 0; j < moves.length; j++) {
        const v = moves[j];
        const cid = R.add(id, v, { st: "explored", lvl: depth + 1 }, `Player ${mover} tries moving to ${v}.`);
        const oppWins = rec(cid, v, new Set([...seen, v]), depth + 1);
        if (!oppWins) {
          win = true;
          for (const u of moves.slice(j + 1)) R.add(id, u, { st: "skipped", lvl: depth + 1, note: "not needed" }, `Moving to ${v} already wins for player ${mover}, so ${u} is never looked at.`, true);
          break;
        }
      }
      R.set(id, { val: win ? mover : 3 - mover, note: `P${win ? mover : 3 - mover} wins` }, `From ${at} with player ${mover} to move: player ${win ? mover : 3 - mover} wins.`);
      return win;
    }
    const p1 = rec(root, Gg.s, new Set([Gg.s]), 0);
    return { R, ok: true, end: null, caption: `Player ${p1 ? 1 : 2} has a winning strategy from ${Gg.s}.`, p1 };
  }

  /* ---------------- catalog ---------------- */
  const PROBLEMS = {
    SAT3: { label: "3SAT", kind: "sat", cls: "NP-Complete",
      def: "3SAT, or the Boolean satisfiability problem, is a problem that asks for a list of assignments to the literals of phi (with a maximum of 3 literals per clause) to result in 'True'.",
      input: "(x1 | !x2 | x3) & (…)",
      vizDef: "The solver's choices as a tree. Each level sets one variable; red dashed nodes are branches cut by a false clause.",
      solvers: [["redux", "3SAT Backtracking"], ["dpll", "DPLL with unit propagation (not in Redux yet)"]],
      examples: [["Redux default", "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)"],
                 ["7 clauses, 4 variables", "(!x1 | x2 | x3) & (!x1 | !x2 | x4) & (!x1 | !x3 | !x4) & (!x1 | !x2 | x3) & (!x1 | x2 | x4) & (x1 | x2 | !x4) & (x1 | !x3 | x4)"],
                 ["Unsatisfiable", "(x1 | x2) & (x1 | !x2) & (!x1 | x3) & (!x1 | !x3)"]] },
    KNAPSACK: { label: "Knapsack (Binary)", kind: "knap", cls: "NP-Complete",
      def: "The 0-1 KNAPSACK decision problem is given a knapsack with a maximum capacity W and target value V and a set of n items with weights and values; find the combination of singular items that provide at least V value while staying within W.",
      input: "({(w1,v1),(w2,v2),…},W,V)",
      vizDef: "Take-or-skip decisions as a tree, best value per unit of weight first. Each node shows its weight, value and the best value still possible; branches that can't reach V are cut.",
      solvers: [["bb", "Branch and bound (not in Redux yet)"]],
      examples: [["Redux default", "({(10,60),(20,100),(30,120)},50,220)"],
                 ["Bounds cut most branches", "({(2,40),(3,50),(4,60),(5,65),(9,80),(1,10)},12,165)"],
                 ["Target out of reach", "({(10,60),(20,100),(30,120)},50,230)"]] },
    TSP: { label: "Traveling Salesperson", kind: "tsp", cls: "NP-Complete",
      def: "Find a tour that visits every city exactly once and returns home, with total weight at most K.",
      input: "((N,E),K), edges ({a,b},w)",
      vizDef: "Partial tours as a tree, explored cheapest-bound-first. Each node shows its lower bound; branches whose bound can't beat the best tour so far are cut.",
      solvers: [["bb", "Traveling Salesperson Branch and Bound"]],
      examples: [["Redux default", "(({New York,Chicago,Denver,Los Angeles,Miami},{({New York,Chicago},790),({New York,Denver},1770),({New York,Los Angeles},2450),({New York,Miami},1280),({Chicago,Denver},1000),({Chicago,Los Angeles},2015),({Chicago,Miami},1370),({Denver,Los Angeles},1015),({Denver,Miami},2060),({Los Angeles,Miami},2745)}),8000)"],
                 ["Greedy isn't optimal", "(({a,b,c,d,e},{({a,b},3),({a,c},3),({a,d},5),({a,e},8),({b,c},3),({b,d},4),({b,e},10),({c,d},8),({c,e},12),({d,e},9)}),100)"]] },
    GRAPHCOLORING: { label: "Graph Coloring", kind: "color", cls: "NP-Complete",
      def: "An assignment of labels (e.g., colors) to the vertices of a graph such that no two adjacent vertices are of the same label. This is called a vertex coloring.",
      input: "((N,E),K)",
      vizDef: "Each level colors one node; red dashed nodes are colors that clash with a neighbor.",
      solvers: [["bt", "Backtracking (not in Redux yet)"]],
      examples: [["Needs backtracking", "(({a,b,c,d,e,f},{{a,b},{a,c},{b,c},{b,d},{c,e},{d,e},{d,f},{e,f}}),3)"],
                 ["Not 3-colorable (K4)", "(({a,b,c,d},{{a,b},{a,c},{a,d},{b,c},{b,d},{c,d}}),3)"],
                 ["Redux default", "(({a,b,c,d,e,f,g,h,i},{{a,b},{b,c},{a,c},{d,a},{d,e},{a,e},{a,f},{f,g},{g,a},{a,h},{h,i},{i,a}}),3)"]] },
    NQUEENS: { label: "N-Queens", kind: "queens", cls: "P",
      def: "Given an integer n, determine whether n queens can be placed on an n x n chessboard so that no two queens share a row, column, or diagonal.",
      input: "n",
      vizDef: "Each level places one row's queen; red dashed nodes are attacked squares the solver skips.",
      solvers: [["bt", "N-Queens Backtracking"]],
      examples: [["Redux default", "4"], ["5 × 5", "5"], ["6 × 6", "6"], ["No solution (3 × 3)", "3"]] },
    QBF: { label: "Quantified Boolean Formula", kind: "qbf", cls: "PSPACE-Complete", game: true, standalone: true,
      def: "Given a fully quantified Boolean formula, decide whether it is true. Equivalently, a game: the ∃ player sets existential variables to make the formula true, the ∀ player sets universal ones to make it false, in prefix order.",
      input: "forall x1 exists x2 … : CNF formula",
      vizDef: "The game tree. ∃ levels need one true child, ∀ levels need every child true; once a level is decided the remaining branch is never looked at.",
      solvers: [["game", "Game-tree evaluation (not in Redux yet)"]],
      examples: [["True (∃ wins)", "exists x1 forall x2 exists x3 : (x1 | x2) & (!x2 | x3) & (x2 | !x3)"],
                 ["False (∀ wins)", "forall x1 exists x2 : (x1 | x2) & (x1 | !x2)"],
                 ["Three alternations", "forall x1 exists x2 forall x3 exists x4 : (x1 | x2 | x3) & (!x1 | !x2 | x4) & (x2 | !x3 | !x4) & (!x2 | x3 | x4)"]] },
    GEOGRAPHY: { label: "Generalized Geography", kind: "geo", cls: "PSPACE-Complete", game: true, standalone: true,
      def: "Two players move a token along the edges of a directed graph, starting at a given node. Neither may move to a node already visited. The player who cannot move loses. Does player 1 have a winning strategy?",
      input: "(N, E, start), edges (u,v)",
      vizDef: "The game tree. Levels alternate between the two players; a position wins for the player to move if some move leads to a position the opponent loses.",
      solvers: [["game", "Game-tree search (not in Redux yet)"]],
      examples: [["Player 1 wins", "({1,2,3,4,5},{(1,2),(1,3),(2,4),(3,4),(4,5),(3,5)},1)"],
                 ["Player 2 wins", "({1,2,3,4},{(1,2),(2,3),(2,4),(3,4)},1)"],
                 ["Longer game", "({a,b,c,d,e,f},{(a,b),(a,c),(b,d),(c,d),(d,e),(e,f),(f,b),(d,f),(c,e)},a)"]] },
  };

  function run(str, P, solver) {
    switch (P.kind) {
      case "sat": { const c = parseCNF(str, true); return { inst: c, out: solver === "dpll" ? sat3Dpll(c) : sat3Redux(c) }; }
      case "knap": { const k = parseKnapsack(str); return { inst: k, out: knapsackBB(k) }; }
      case "tsp": { const g = parseGraph(str, true); if (g.nodes.length > 8) throw new Error("The tree view draws up to 8 cities."); return { inst: g, out: tspBB(g) }; }
      case "color": { const g = parseGraph(str, false); if (g.nodes.length > 10) throw new Error("The tree view draws up to 10 nodes."); return { inst: g, out: colorBT(g) }; }
      case "queens": { const n = parseN(str); return { inst: n, out: queensBT(n) }; }
      case "qbf": { const q = parseQBF(str); return { inst: q, out: qbfGame(q) }; }
      case "geo": { const g = parseGeography(str); return { inst: g, out: geoGame(g) }; }
    }
    throw new Error("Unknown problem.");
  }

  // Turn patches into frames: consecutive "merge" patches (cuts) share one frame.
  function framesFrom(out) {
    const ev = out.R.ev, frames = [{ upto: -1, caption: "Step 0: nothing explored yet. Step forward to watch the solver grow its tree." }];
    for (let i = 0; i < ev.length; i++) {
      const e = ev[i], prev = frames[frames.length - 1];
      if (e.merge && prev.merge && prev.upto === i - 1) { prev.upto = i; prev.caption += " " + e.caption; prev.cur = e.id ?? prev.cur; continue; }
      frames.push({ upto: i, caption: e.caption, cur: e.id, merge: !!e.merge, bad: e.st === "pruned" || e.st === "failed" });
    }
    let hidden = 0;
    if (frames.length > MAX_FRAMES) { hidden = frames.length - MAX_FRAMES; frames.splice(MAX_FRAMES - 1, hidden); }
    frames.push({ upto: ev.length - 1, done: true, ok: out.ok, cur: out.end ?? null, caption: out.caption + (hidden ? ` (${hidden} steps in the middle aren't shown.)` : "") });
    return frames;
  }

  function snapshot(ev, upto) {
    const nodes = new Map();
    for (let i = 0; i <= upto; i++) {
      const e = ev[i];
      if (e.id === undefined) continue;
      let n = nodes.get(e.id);
      if (!n) { n = { id: e.id, parent: e.parent ?? null, label: e.label, kids: [] }; nodes.set(e.id, n); if (n.parent !== null && nodes.has(n.parent)) nodes.get(n.parent).kids.push(n); }
      for (const k of ["st", "note", "val", "order", "lvl"]) if (e[k] !== undefined) n[k] = e[k];
    }
    return nodes;
  }

  function create({ svg }) {
    if (!document.getElementById("st-style")) {
      const st = document.createElement("style"); st.id = "st-style"; st.textContent = css; document.head.appendChild(st);
    }
    svg.classList.add("st-svg");
    let P, inst, out, frames, frame = null, hover = null;

    function solPath(nodes, endId) {
      const s = new Set();
      let n = endId !== null && endId !== undefined ? nodes.get(endId) : null;
      while (n) { s.add(n.id); n = n.parent !== null ? nodes.get(n.parent) : null; }
      return s;
    }

    function draw() {
      svg.innerHTML = "";
      const f = frame;
      const nodes = f ? snapshot(out.R.ev, f.upto) : new Map();
      const root = [...nodes.values()].find(n => n.parent === null);
      const W0 = 680;
      if (!root) {
        svg.setAttribute("viewBox", `0 0 ${W0} 120`); svg.style.width = "100%"; svg.style.minWidth = "";
        mk("text", { x: W0 / 2, y: 64, "text-anchor": "middle", class: "st-lvl" }, svg).textContent = f ? "Nothing explored yet." : "Choose Solved or Steps to grow the tree.";
        return;
      }
      const sol = f && f.done && f.ok ? solPath(nodes, f.cur) : new Set();
      const cur = f && !f.done ? f.cur : null;
      // "open" = still in play: current, queued, on the answer, or above one of those
      const open = new Set();
      const mark = n => { let x = n; while (x && !open.has(x.id)) { open.add(x.id); x = x.parent !== null ? nodes.get(x.parent) : null; } };
      for (const n of nodes.values()) if (n.id === cur || n.st === "queued" || sol.has(n.id)) mark(n);
      mark(root);
      const size = new Map();
      const sz = n => { let s = 1; n.kids.forEach(k => (s += sz(k))); size.set(n.id, s); return s; };
      sz(root);
      // keep the picture about MAX_LEAVES leaves wide: first group cut-off leaves, then fold finished subtrees
      const folded = new Set();
      const cutKids = (n, on) => (on ? n.kids.filter(k => !k.kids.length && k.st === "pruned" && k.id !== cur) : []);
      const leaves = on => {
        let c = 0;
        const rec = n => {
          if (folded.has(n.id) || !n.kids.length) { c++; return; }
          const cuts = cutKids(n, on); let seen = false;
          for (const k of n.kids) { if (cuts.length > 1 && cuts.includes(k)) { if (!seen) { c++; seen = true; } continue; } rec(k); }
        };
        rec(root); return c;
      };
      const groupCuts = leaves(false) > MAX_LEAVES;
      while (leaves(groupCuts) > MAX_LEAVES) {
        let pick = null;
        for (const n of nodes.values()) {
          if (open.has(n.id) || folded.has(n.id) || n.kids.length === 0 || size.get(n.id) < 3) continue;
          let up = n.parent !== null ? nodes.get(n.parent) : null, hidden = false;
          while (up) { if (folded.has(up.id)) { hidden = true; break; } up = up.parent !== null ? nodes.get(up.parent) : null; }
          if (hidden) continue;
          if (!pick || size.get(n.id) > size.get(pick.id)) pick = n;
        }
        if (!pick) break;
        folded.add(pick.id);
      }
      // layout: leaves left to right, parents centered over their children
      const width = n => Math.max(34, String(n.label).length * 7.2 + 16);
      const noteW = n => (n.note ? Math.min(22, n.note.length) * 6.4 + 8 : 0);
      let slot = 0;
      const vis = [];
      const walk = (n, d) => {
        const item = { n, d, kids: [] };
        vis.push(item);
        if (folded.has(n.id)) {
          const k = size.get(n.id) - 1;
          const fold = { n: { id: "f" + n.id, label: `+${k} more`, st: "fold", kids: [] }, d: d + 1, kids: [], fold: true, of: n };
          vis.push(fold); item.kids.push(fold);
        } else {
          // when the tree is still too big, cut-off leaves under one node share a single "k cut" node
          const cuts = groupCuts ? n.kids.filter(k => !k.kids.length && k.st === "pruned" && k.id !== cur) : [];
          let placed = false;
          n.kids.forEach(k => {
            if (cuts.length > 1 && cuts.includes(k)) {
              if (placed) return;
              placed = true;
              const g = { n: { id: "c" + n.id, parent: n.id, label: `${cuts.length} cut`, st: "pruned", note: "", kids: [] }, d: d + 1, kids: [], cutGroup: cuts, of: n };
              vis.push(g); item.kids.push(g); return;
            }
            item.kids.push(walk(k, d + 1));
          });
        }
        return item;
      };
      const rootItem = walk(root, 0);
      vis.forEach(v => (slot = Math.max(slot, width(v.n), noteW(v.n))));
      slot += 14;
      let nextX = 0, maxD = 0;
      const place = it => {
        maxD = Math.max(maxD, it.d);
        if (!it.kids.length) { it.x = nextX + slot / 2; nextX += slot; return; }
        it.kids.forEach(place);
        it.x = (it.kids[0].x + it.kids[it.kids.length - 1].x) / 2;
      };
      place(rootItem);
      const left = P.game ? GUTTER : 20;
      const W = Math.max(W0, nextX + left + 20), H = (maxD + 1) * ROW + 40;
      svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
      svg.style.width = W > W0 * 1.05 ? `${Math.round(W)}px` : "100%";
      const ox = left + (W - left - 20 - nextX) / 2, y = d => 30 + d * ROW;
      // game levels
      if (P.game) {
        for (let d = 0; d <= maxD; d++) {
          mk("rect", { x: 0, y: y(d) - NH / 2 - 14, width: W, height: ROW, class: "st-band" + (d % 2 ? " alt" : "") }, svg);
          let txt;
          if (P.kind === "qbf") { const q = inst.prefix[d]; txt = q ? `${q.q === "E" ? "∃" : "∀"} ${q.v}` : "result"; }
          else txt = d % 2 === 0 ? "P1 moves" : "P2 moves";
          mk("text", { x: 10, y: y(d) + 4, class: "st-lvl q" }, svg).textContent = txt;
        }
      }
      const gE = mk("g", {}, svg), gN = mk("g", {}, svg);
      const nodeStateClass = n => n.st === "fold" ? "fold" : sol.has(n.id) ? "solution" : n.st === "solution" && !(f && f.done) ? "explored" : n.st || "explored";
      vis.forEach(it => {
        it.x += ox;
        if (it.fold) {
          const p = vis.find(v => v.n === it.of);
          mk("path", { d: `M${p.x} ${y(p.d) + NH / 2}L${it.x} ${y(it.d) - NH / 2}`, class: "st-edge skipped" }, gE);
        }
      });
      vis.forEach(it => {
        if (it.fold || it.n.parent === null) return;
        const p = vis.find(v => v.n.id === it.n.parent);
        if (!p) return;
        const cls = sol.has(it.n.id) ? "sol" : it.n.id === cur ? "cur" : it.n.st === "pruned" ? "pruned" : it.n.st === "skipped" ? "skipped" : "";
        mk("path", { d: `M${p.x} ${y(p.d) + NH / 2}C${p.x} ${y(p.d) + ROW / 2} ${it.x} ${y(it.d) - ROW / 2} ${it.x} ${y(it.d) - NH / 2}`, class: "st-edge " + cls }, gE);
      });
      const pathText = n => { const a = []; let x = n; while (x) { a.unshift(x.label); x = x.parent !== null ? nodes.get(x.parent) : null; } return a.join(" → "); };
      vis.forEach(it => {
        const n = it.n, w = width(n), cx = it.x, cy = y(it.d);
        let cls = "st-node " + nodeStateClass(n);
        if (n.id === cur) cls += " cur";
        if (hover !== null && hover === n.id) cls += " trace";
        const g = mk("g", { class: cls, tabindex: it.fold ? -1 : 0 }, gN);
        mk("title", {}, g).textContent = it.fold ? `${size.get(it.of.id) - 1} finished nodes folded away` : it.cutGroup ? `Cut off: ${it.cutGroup.map(k => k.label + (k.note ? " (" + k.note + ")" : "")).join(", ")}` : pathText(n) + (n.note ? ` · ${n.note}` : "");
        mk("rect", { x: cx - w / 2, y: cy - NH / 2, width: w, height: NH, rx: 7 }, g);
        mk("text", { x: cx, y: cy, class: "lbl" }, g).textContent = n.label;
        if (n.note) mk("text", { x: cx, y: cy + NH / 2 + 13, class: "note" }, g).textContent = n.note.length > 22 ? n.note.slice(0, 21) + "…" : n.note;
        if (n.order) { const o = mk("g", { class: "st-ord" }, g); mk("circle", { cx: cx - w / 2 + 2, cy: cy - NH / 2 + 1, r: 8 }, o); mk("text", { x: cx - w / 2 + 2, y: cy - NH / 2 + 1 }, o).textContent = n.order; }
        if (n.val !== undefined) {
          const v = mk("g", { class: "st-val " + (P.kind === "geo" ? "p" + n.val : n.val ? "win" : "lose") }, g);
          mk("circle", { cx: cx + w / 2 - 1, cy: cy - NH / 2 + 1, r: 9 }, v);
          mk("text", { x: cx + w / 2 - 1, y: cy - NH / 2 + 1 }, v).textContent = P.kind === "qbf" ? (n.val ? "T" : "F") : String(n.val);
        }
        if (!it.fold && !it.cutGroup) {
          const on = () => { hover = n.id; draw(); }, off = () => { hover = null; draw(); };
          g.addEventListener("pointerenter", on); g.addEventListener("pointerleave", off);
          g.addEventListener("focus", on); g.addEventListener("blur", off);
        }
      });
    }

    function counts(i) {
      const f = i === null ? null : frames[i];
      if (!f) return { all: 0, cut: 0, done: 0 };
      const nodes = snapshot(out.R.ev, f.upto);
      let cut = 0, skip = 0;
      for (const n of nodes.values()) { if (n.st === "pruned") cut++; if (n.st === "skipped") skip++; }
      return { all: nodes.size, cut, skip };
    }

    return {
      load(str, key, solverKey) {
        P = typeof key === "string" ? PROBLEMS[key] : key;
        const r = run(str, P, solverKey || P.solvers[0][0]);
        inst = r.inst; out = r.out;
        frames = framesFrom(out);
        frame = null; hover = null;
        draw();
        return { frames, ok: out.ok };
      },
      show(i) { frame = i === null ? null : frames[i]; draw(); return frame; },
      checks(i) {
        if (i === null) return [{ label: "Tree not grown yet", ok: null }];
        const c = counts(i), f = frames[i], list = [{ label: `Nodes in the tree: ${c.all}`, ok: null }];
        if (P.game) list.push({ label: `Branches never needed: ${c.skip}`, ok: null });
        else list.push({ label: `Branches cut off: ${c.cut}`, ok: null });
        if (f.done) {
          if (P.kind === "qbf") list.push({ label: `Formula is ${out.qbfTrue ? "true" : "false"}`, ok: out.qbfTrue });
          else if (P.kind === "geo") list.push({ label: `Winner: player ${out.p1 ? 1 : 2}`, ok: null });
          else list.push({ label: out.ok ? "Answer found" : "No answer exists", ok: out.ok });
        }
        return list;
      },
      chosen(i) {
        const f = i === null ? null : frames[i];
        if (!f || !f.done) return [];
        if (P.kind === "sat") return out.ok ? [...out.asg].map(([k, v]) => ({ text: `${k}=${v ? "T" : "F"}` })) : [];
        if (P.kind === "knap") return out.ok ? [...out.picked.map(it => ({ text: `(${it.w},${it.v})` })), { text: `w ${out.picked.reduce((a, b) => a + b.w, 0)} · v ${out.picked.reduce((a, b) => a + b.v, 0)}` }] : [];
        if (P.kind === "tsp") return out.ok ? [{ text: out.tour.join(" → ") + " → " + out.tour[0] }, { text: `cost ${out.cost}` }] : [];
        if (P.kind === "color") { if (!out.ok) return []; const cls = {}; out.color.forEach((c, x) => (cls[c] = cls[c] || []).push(x)); return Object.keys(cls).map(c => ({ text: `${c}: {${cls[c].join(",")}}`, color: +c - 1 })); }
        if (P.kind === "queens") return out.ok ? out.board.map((c, r) => ({ text: `(${r},${c})` })) : [];
        if (P.kind === "qbf") return [{ text: out.qbfTrue ? "true" : "false" }];
        if (P.kind === "geo") return [{ text: `player ${out.p1 ? 1 : 2} wins` }];
        return [];
      },
      resetHover() { hover = null; draw(); },
    };
  }

  return { create, PROBLEMS, parse: { cnf: parseCNF, graph: parseGraph, knapsack: parseKnapsack, n: parseN, qbf: parseQBF, geography: parseGeography }, _run: run, _frames: framesFrom };
})();
