/* ---- Reduction view: problem A ↦ problem B, side by side ----
   One SVG, two panes. A reduction emits the target instance, a gadget map (source ids ↔ target ids,
   each with a kind from one shared list), construction frames, then solve + map-back frames.
   Pages supply --av-* tokens. Redux reductions mirrored here: Sipser's Clique Reduction (3SAT → Clique),
   Sipser's Vertex Cover Reduction (Clique → Vertex Cover), Karp's Graph Coloring Reduction (3SAT → Graph Coloring). */
const ReductionView = (() => {
  const NS = "http://www.w3.org/2000/svg";
  const mk = (tag, attrs = {}, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; };
  const SEP = "\u0000", NR = 16;
  const ekey = (a, b) => (a < b ? a + SEP + b : b + SEP + a);
  const SUBS = "₀₁₂₃₄₅₆₇₈₉";
  const pretty = nm => { const m = /^([A-Za-z_]+)(\d+)$/.exec(nm); return m ? m[1] + m[2].split("").map(d => SUBS[d]).join("") : nm; };
  const litText = l => (l.startsWith("!") ? "¬" + pretty(l.slice(1)) : pretty(l));
  const fmt = a => "{" + a.join(", ") + "}";

  /* One shared list of gadget kinds. Redux's Gadget.color today carries "ElementHighlight" / "ClauseHighlight";
     those map to element / group. */
  const GADGET_KINDS = {
    element: { name: "Element", def: "One piece of A becomes one piece of B", redux: "ElementHighlight" },
    group: { name: "Group", def: "One part of A becomes a group of pieces in B", redux: "ClauseHighlight" },
    edgeRule: { name: "Edge rule", def: "A rule that decides which pairs of B get an edge" },
    palette: { name: "Palette", def: "Fixed helper pieces every instance of B gets" },
    orGadget: { name: "OR gadget", def: "A small structure that only works if a clause has a true literal" },
    bound: { name: "Bound", def: "How B's number K is computed from A" },
  };

  const css = `
.rd-svg { width: 100%; height: auto; display: block; }
.rd-svg text { font-family: var(--av-mono); }
.rd-pane-title { font-size: 11.5px; font-weight: 600; letter-spacing: .07em; fill: var(--av-muted); }
.rd-arrow { font-size: 26px; fill: var(--av-muted); text-anchor: middle; dominant-baseline: central; }
.rd-arrow-lbl { font-size: 10.5px; fill: var(--av-muted); text-anchor: middle; }
.rd-node { cursor: pointer; transition: opacity .25s; }
.rd-node:focus { outline: none; }
.rd-node .body { fill: var(--av-bg); stroke: var(--av-stroke); stroke-width: 1.6; transition: fill .2s, stroke .2s; }
.rd-node text { font-size: 12px; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); pointer-events: none; }
.rd-node.hidden { opacity: 0; pointer-events: none; }
.rd-node.new .body { stroke: var(--av-hl); stroke-width: 3; }
.rd-node.Active .body { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; }
.rd-node.Solution .body { fill: var(--av-sol); stroke: var(--av-sol); } .rd-node.Solution text { fill: var(--av-on-sol); font-weight: 600; }
.rd-node.Covered .body { fill: var(--av-sol-fill); stroke: var(--av-sol); stroke-width: 2.2; }
.rd-node.Rejected .body { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 2.6; } .rd-node.Rejected text { fill: var(--av-rej); }
.rd-node.Blocked .body { fill: transparent; stroke: var(--av-stroke); stroke-dasharray: 4 3; } .rd-node.Blocked text { fill: var(--av-muted); }
.rd-node.c0 .body { fill: var(--av-g0); stroke: var(--av-g0s); } .rd-node.c1 .body { fill: var(--av-g1); stroke: var(--av-g1s); }
.rd-node.c2 .body { fill: var(--av-g2); stroke: var(--av-g2s); } .rd-node.c3 .body { fill: var(--av-g3); stroke: var(--av-g3s); }
.rd-node.c4 .body { fill: var(--av-g4); stroke: var(--av-g4s); } .rd-node.c5 .body { fill: var(--av-g5); stroke: var(--av-g5s); }
.rd-node[class*=" c"] text { fill: #10131a; font-weight: 600; }
.rd-node.trace .body { stroke: var(--av-hot); stroke-width: 3; }
.rd-node.faint { filter: grayscale(1); } .rd-node.faint .body { stroke-dasharray: 3 3; }
.rd-node.g0 .body { stroke: var(--av-g0s); stroke-width: 3; } .rd-node.g1 .body { stroke: var(--av-g1s); stroke-width: 3; }
.rd-node.g2 .body { stroke: var(--av-g2s); stroke-width: 3; } .rd-node.g3 .body { stroke: var(--av-g3s); stroke-width: 3; }
.rd-node.g4 .body { stroke: var(--av-g4s); stroke-width: 3; } .rd-node.g5 .body { stroke: var(--av-g5s); stroke-width: 3; }
.rd-clause .box { fill: var(--av-surface); stroke: var(--av-stroke); stroke-width: 1.4; transition: fill .2s, stroke .2s; }
.rd-clause .cid { font-size: 11px; fill: var(--av-muted); dominant-baseline: central; }
.rd-clause .or { font-size: 12px; fill: var(--av-muted); dominant-baseline: central; text-anchor: middle; }
.rd-clause.Covered .box { fill: var(--av-sol-fill); stroke: var(--av-sol); }
.rd-clause.Active .box { stroke: var(--av-hl); stroke-width: 2.6; }
.rd-clause.trace .box { stroke: var(--av-hot); stroke-width: 2.6; }
.rd-clause.faint { filter: grayscale(1); }
.rd-clause.g0 .box { stroke: var(--av-g0s); stroke-width: 2.6; } .rd-clause.g1 .box { stroke: var(--av-g1s); stroke-width: 2.6; }
.rd-clause.g2 .box { stroke: var(--av-g2s); stroke-width: 2.6; } .rd-clause.g3 .box { stroke: var(--av-g3s); stroke-width: 2.6; }
.rd-clause.g4 .box { stroke: var(--av-g4s); stroke-width: 2.6; } .rd-clause.g5 .box { stroke: var(--av-g5s); stroke-width: 2.6; }
.rd-lit { cursor: pointer; }
.rd-lit rect { fill: transparent; stroke: transparent; rx: 6; transition: fill .2s; }
.rd-lit text { font-size: 13px; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); pointer-events: none; }
.rd-lit.Active rect { fill: var(--av-hl-fill); stroke: var(--av-hl); }
.rd-lit.Solution rect { fill: var(--av-sol); } .rd-lit.Solution text { fill: var(--av-on-sol); font-weight: 600; }
.rd-lit.False text { fill: var(--av-muted); text-decoration: line-through; }
.rd-lit.trace rect { stroke: var(--av-hot); stroke-width: 2; }
.rd-var rect { fill: var(--av-surface); stroke: var(--av-stroke); }
.rd-var text { font-size: 12px; text-anchor: middle; dominant-baseline: central; fill: var(--av-muted); }
.rd-var.T rect { stroke: var(--av-sol); } .rd-var.T text { fill: var(--av-sol); font-weight: 600; }
.rd-var.F rect { stroke: var(--av-stroke); } .rd-var.F text { fill: var(--av-ink); }
.rd-var.Active rect { stroke: var(--av-hl); stroke-width: 2.4; }
.rd-hull { fill: transparent; stroke: var(--av-stroke); stroke-width: 1.2; stroke-dasharray: 3 4; transition: opacity .25s; }
.rd-hull.hidden { opacity: 0; }
.rd-hull.Active { stroke: var(--av-hl); stroke-width: 2.4; stroke-dasharray: none; }
.rd-hull.Covered { stroke: var(--av-sol); stroke-dasharray: none; }
.rd-hull.g0 { stroke: var(--av-g0s); } .rd-hull.g1 { stroke: var(--av-g1s); } .rd-hull.g2 { stroke: var(--av-g2s); }
.rd-hull.g3 { stroke: var(--av-g3s); } .rd-hull.g4 { stroke: var(--av-g4s); } .rd-hull.g5 { stroke: var(--av-g5s); }
.rd-hlabel { font-size: 11px; fill: var(--av-muted); dominant-baseline: central; }
.rd-edge { stroke: var(--av-edge); stroke-width: 1.2; transition: opacity .25s, stroke .2s; }
.rd-edge.hidden { opacity: 0; }
.rd-edge.dim { stroke: var(--av-edge-dim); stroke-width: .8; }
.rd-edge.new { stroke: var(--av-hl); stroke-width: 2.2; opacity: 1; }
.rd-edge.sol { stroke: var(--av-sol); stroke-width: 3; opacity: 1; }
.rd-edge.cov { stroke: var(--av-cov); stroke-width: 1.6; }
.rd-edge.trace { stroke: var(--av-hot); stroke-width: 2.2; opacity: 1; }
.rd-edge.faint { stroke: var(--av-edge-dim); stroke-width: .8; stroke-dasharray: 3 3; }
.rd-phantom { stroke: var(--av-rej); stroke-width: 2; stroke-dasharray: 5 4; fill: none; }
.rd-link { fill: none; stroke: var(--av-hot); stroke-width: 1.4; stroke-dasharray: 2 3; opacity: .85; pointer-events: none; }
@media (prefers-reduced-motion: reduce) { .rd-node, .rd-edge, .rd-hull, .rd-node .body { transition: none; } }`;

  /* ---------- parsing ---------- */
  function parseSat(str) {
    const s = str.trim();
    if (!s) throw new Error("Enter a 3SAT formula like (x1 | !x2 | x3) & (!x1 | x2 | x3).");
    const clauses = s.split("&").map((part, i) => {
      const inner = part.trim().replace(/^\(/, "").replace(/\)$/, "");
      if (/[()]/.test(inner) || !inner.trim()) throw new Error(`Clause ${i + 1} is malformed.`);
      return inner.split("|").map(tok => {
        let t = tok.trim(), neg = false;
        while (/^[!~¬]/.test(t)) { neg = !neg; t = t.slice(1).trim(); }
        if (!/^[A-Za-z_]\w*$/.test(t)) throw new Error(`"${tok.trim()}" isn't a literal.`);
        return (neg ? "!" : "") + t;
      });
    });
    if (clauses.some(c => c.length > 3)) throw new Error("3SAT clauses have at most three literals.");
    clauses.forEach((c, i) => { clauses[i] = [...new Set(c)]; }); // x ∨ x is just x; repeated literals would give two nodes the same name
    const vars = [];
    clauses.flat().forEach(l => { const v = l.replace(/^!/, ""); if (!vars.includes(v)) vars.push(v); });
    return { clauses, vars };
  }
  function parseClique(str) {
    const s = str.replace(/\s+/g, "");
    const m = /^\(\(\{([^{}]*)\},\{(.*)\}\),(\d+)\)$/.exec(s);
    if (!m) throw new Error("Expected a Clique instance like (({1,2,3},{{1,2},{2,3}}),2).");
    const nodes = m[1] ? m[1].split(",") : [];
    if (!nodes.length || new Set(nodes).size !== nodes.length) throw new Error("N must be a non-empty set with no repeats.");
    if (nodes.length > 10) throw new Error("This mockup draws up to 10 nodes for this reduction.");
    const re = /\{([^{},]+),([^{},]+)\}/g;
    if (m[2].replace(re, "").replace(/,/g, "")) throw new Error("Each edge must look like {a,b}.");
    const edges = [], seen = new Set();
    let t;
    while ((t = re.exec(m[2]))) {
      const [a, b] = [t[1], t[2]];
      if (!nodes.includes(a) || !nodes.includes(b)) throw new Error(`Edge {${a},${b}} uses a node that isn't in N.`);
      if (a === b) continue;
      const k = ekey(a, b); if (!seen.has(k)) { seen.add(k); edges.push([a, b]); }
    }
    return { nodes, edges, K: Number(m[3]), adj: k => seen.has(k) };
  }

  /* ---------- the three reductions: each returns {src, tgt, gadgets, frames, targetInstance} ---------- */

  // Sipser's Clique Reduction — node names follow Redux: literal + "_" + clause index.
  function sipserClique(str) {
    const F = parseSat(str);
    if (F.clauses.length > 7) throw new Error("This mockup draws up to 7 clauses for this reduction.");
    const m = F.clauses.length;
    const nodes = [], cluster = [];
    F.clauses.forEach((c, i) => c.forEach((l, j) => nodes.push({ id: "t:" + l + "_" + i, name: l + "_" + i, label: litText(l), lit: l, ci: i, li: j })));
    const comp = (a, b) => a.lit.replace(/^!/, "") === b.lit.replace(/^!/, "") && a.lit !== b.lit;
    const edges = [], frames = [], gadgets = [];
    F.clauses.forEach((c, i) => {
      c.forEach((l, j) => gadgets.push({ kind: "element", from: [`s:c${i}-${j}`], to: [`t:${l}_${i}`] }));
      gadgets.push({ kind: "group", from: [`s:c${i}`], to: [...c.map(l => `t:${l}_${i}`), `h:${i}`] });
    });
    F.vars.forEach(v => {
      const occ = nodes.filter(n => n.lit.replace(/^!/, "") === v);
      if (occ.some(n => n.lit === v) && occ.some(n => n.lit === "!" + v)) gadgets.push({ kind: "edgeRule", from: [`s:v:${v}`], to: occ.map(n => n.id), note: `no edge between ${pretty(v)} and ¬${pretty(v)}` });
    });
    gadgets.push({ kind: "bound", from: F.clauses.map((_, i) => `s:c${i}`), to: [], note: `K = ${m}` });

    let shownNodes = 0;
    F.clauses.forEach((c, i) => {
      shownNodes += c.length;
      frames.push({ phase: "build", nodes: shownNodes, edges: 0, focusSrc: [`s:c${i}`, ...c.map((_, j) => `s:c${i}-${j}`)], focusTgt: c.map(l => `t:${l}_${i}`), hull: i,
        caption: `Clause C${i + 1} (${c.map(litText).join(" ∨ ")}) becomes cluster ${i + 1}: nodes ${c.map(l => litText(l) + "_" + i).join(", ")}. Nodes in the same cluster never get an edge.` });
    });
    for (let i = 0; i < m; i++) for (let j = i + 1; j < m; j++) {
      const A = nodes.filter(n => n.ci === i), B = nodes.filter(n => n.ci === j), skipped = [];
      const newEdges = [];
      A.forEach(a => B.forEach(b => (comp(a, b) ? skipped.push([a.id, b.id]) : newEdges.push([a.id, b.id]))));
      newEdges.forEach(e => edges.push(e));
      frames.push({ phase: "build", nodes: shownNodes, edges: edges.length, newEdges: newEdges.length, skipped, focusSrc: [`s:c${i}`, `s:c${j}`], focusTgt: [...A, ...B].map(n => n.id),
        caption: `Join clusters ${i + 1} and ${j + 1}: ${newEdges.length} edge${newEdges.length === 1 ? "" : "s"}.` + (skipped.length
          ? ` No edge for ${skipped.map(([a, b]) => `${nodes.find(n => n.id === a).label}–${nodes.find(n => n.id === b).label}`).join(", ")}: a literal and its negation can't both be true.`
          : " Every pair is compatible.") });
    }
    frames.push({ phase: "build", nodes: shownNodes, edges: edges.length, focusSrc: F.clauses.map((_, i) => `s:c${i}`), focusTgt: [],
      caption: `K = ${m}, the number of clauses. A clique of ${m} nodes has to take exactly one node from each cluster.` });

    // Solve B (Clique Brute Force finds the same cliques; with no edges inside a cluster, a K-clique is one node per cluster).
    const adj = new Set(edges.map(([a, b]) => ekey(a, b)));
    let clique = null;
    const pick = (i, acc) => {
      if (clique) return;
      if (i === m) { clique = acc.slice(); return; }
      for (const n of nodes.filter(x => x.ci === i)) if (acc.every(a => adj.has(ekey(a.id, n.id)))) { acc.push(n); pick(i + 1, acc); acc.pop(); }
    };
    pick(0, []);
    const base = { nodes: shownNodes, edges: edges.length };
    if (!clique) {
      frames.push({ ...base, phase: "solve", done: true, ok: false, caption: `Clique Brute Force finds no clique of size ${m}, so the formula has no satisfying assignment.` });
    } else {
      const sol = clique.map(n => n.id), solEdges = [];
      for (let a = 0; a < sol.length; a++) for (let b = a + 1; b < sol.length; b++) solEdges.push(ekey(sol[a], sol[b]));
      frames.push({ ...base, phase: "solve", tgtSol: sol, tgtSolEdges: solEdges,
        caption: `Solve the Clique instance with Clique Brute Force: ${fmt(clique.map(n => n.label + "_" + n.ci))} is a clique of size ${m}.` });
      const assign = {};
      clique.forEach((n, k) => {
        const v = n.lit.replace(/^!/, ""), val = !n.lit.startsWith("!");
        assign[v] = val;
        frames.push({ ...base, phase: "map", tgtSol: sol, tgtSolEdges: solEdges, focusTgt: [n.id], focusSrc: [`s:c${n.ci}-${n.li}`, `s:v:${v}`], assign: { ...assign },
          caption: `${n.label}_${n.ci} is in the clique, so ${pretty(v)} = ${val ? "true" : "false"}. That makes clause C${n.ci + 1} true.` + (k === 0 ? " No clique holds both a literal and its negation, so these never conflict." : "") });
      });
      const free = F.vars.filter(v => !(v in assign));
      if (free.length) {
        free.forEach(v => (assign[v] = false));
        frames.push({ ...base, phase: "map", tgtSol: sol, tgtSolEdges: solEdges, focusSrc: free.map(v => `s:v:${v}`), assign: { ...assign },
          caption: `${free.map(pretty).join(", ")} ${free.length > 1 ? "aren't" : "isn't"} fixed by the clique, so any value works. Set ${free.length > 1 ? "them" : "it"} to false.` });
      }
      const sat = F.clauses.every(c => c.some(l => (l.startsWith("!") ? !assign[l.slice(1)] : assign[l])));
      frames.push({ ...base, phase: "map", done: true, ok: sat, tgtSol: sol, tgtSolEdges: solEdges, assign: { ...assign },
        caption: sat ? `Every clause has a true literal, so ${F.vars.map(v => `${pretty(v)}=${assign[v] ? "T" : "F"}`).join(", ")} satisfies the formula.` : "The mapped assignment doesn't satisfy the formula." });
    }
    const nodeNames = nodes.map(n => n.name);
    const targetInstance = `(({${nodeNames.join(",")}},{${edges.map(([a, b]) => `{${a.slice(2)},${b.slice(2)}}`).join(",")}}),${m})`;
    return { kind: "satToGraph", F, tgt: { nodes, edges, layout: "clusters", m }, gadgets, frames, targetInstance, targetLabel: "Clique", sourceLabel: "3SAT" };
  }

  // Sipser's Vertex Cover Reduction: complement graph, K' = N − K.
  function sipserVertexCover(str) {
    const G = parseClique(str);
    const n = G.nodes.length, Kp = n - G.K;
    const nodes = G.nodes.map(x => ({ id: "t:" + x, name: x, label: x }));
    const edges = [], frames = [], gadgets = [];
    G.nodes.forEach(x => gadgets.push({ kind: "element", from: ["s:" + x], to: ["t:" + x] }));
    gadgets.push({ kind: "edgeRule", from: [], to: [], note: "an edge in B exactly where A has none (the complement)" });
    gadgets.push({ kind: "bound", from: [], to: [], note: `K' = N − K = ${n} − ${G.K} = ${Kp}` });
    frames.push({ phase: "build", nodes: n, edges: 0, focusSrc: G.nodes.map(x => "s:" + x), focusTgt: nodes.map(x => x.id),
      caption: `Copy every node of G into the new graph, with the same names. Its edges will be the pairs G is missing.` });
    G.nodes.forEach((u, i) => {
      const later = G.nodes.slice(i + 1).filter(v => !G.adj(ekey(u, v)));
      later.forEach(v => edges.push(["t:" + u, "t:" + v]));
      frames.push({ phase: "build", nodes: n, edges: edges.length, newEdges: later.length, srcPhantom: later.map(v => ["s:" + u, "s:" + v]), focusSrc: ["s:" + u], focusTgt: ["t:" + u, ...later.map(v => "t:" + v)],
        caption: later.length ? `${u} has no edge to ${later.join(", ")} in G (dashed), so it gets ${later.length > 1 ? "those edges" : "that edge"} in the complement.`
          : `${u} is already joined to every later node in G, so it gets no new edges.` });
    });
    frames.push({ phase: "build", nodes: n, edges: edges.length, focusSrc: [], focusTgt: [],
      caption: `K' = N − K = ${n} − ${G.K} = ${Kp}. G has a clique of size ${G.K} exactly when the complement has a vertex cover of size ${Kp}.` });
    const base = { nodes: n, edges: edges.length };
    // Solve B with Vertex Cover Brute Force (smallest sizes first, up to K').
    const covers = S => edges.every(([a, b]) => S.has(a.slice(2)) || S.has(b.slice(2)));
    let cover = null;
    const combos = function* (arr, k, start = 0, acc = []) { if (acc.length === k) { yield acc.slice(); return; } for (let i = start; i <= arr.length - (k - acc.length); i++) { acc.push(arr[i]); yield* combos(arr, k, i + 1, acc); acc.pop(); } };
    for (let k = 0; k <= Kp && !cover && Kp >= 0; k++) for (const S of combos(G.nodes, k)) if (covers(new Set(S))) { cover = S; break; }
    if (!cover) {
      frames.push({ ...base, phase: "solve", done: true, ok: false, caption: `Vertex Cover Brute Force finds no cover of size ${Kp}, so G has no clique of size ${G.K}.` });
    } else {
      const C = new Set(cover);
      frames.push({ ...base, phase: "solve", tgtSol: cover.map(x => "t:" + x), tgtCovered: true,
        caption: `Solve the Vertex Cover instance with Vertex Cover Brute Force: ${fmt(cover)} covers every edge with ${cover.length} node${cover.length === 1 ? "" : "s"}.` });
      const clique = [], out = [];
      G.nodes.forEach(x => {
        (C.has(x) ? out : clique).push(x);
        frames.push({ ...base, phase: "map", tgtSol: cover.map(y => "t:" + y), tgtCovered: true, focusTgt: ["t:" + x], focusSrc: ["s:" + x], srcSol: clique.map(y => "s:" + y), srcOut: out.map(y => "s:" + y),
          caption: C.has(x) ? `${x} is in the cover, so it's left out of the clique.` : `${x} isn't in the cover, so it goes in the clique.` });
      });
      const ok = clique.every((a, i) => clique.slice(i + 1).every(b => G.adj(ekey(a, b)))) && clique.length >= G.K;
      const solEdges = [];
      for (let a = 0; a < clique.length; a++) for (let b = a + 1; b < clique.length; b++) solEdges.push(ekey("s:" + clique[a], "s:" + clique[b]));
      frames.push({ ...base, phase: "map", done: true, ok, tgtSol: cover.map(y => "t:" + y), tgtCovered: true, srcSol: clique.map(y => "s:" + y), srcOut: out.map(y => "s:" + y), srcSolEdges: solEdges,
        caption: ok ? `${fmt(clique)} has ${clique.length} nodes, every pair joined in G: a clique of size ${G.K}. No two of them could share a complement edge, or the cover would have missed it.`
          : "The mapped set isn't a clique." });
    }
    const targetInstance = `(({${G.nodes.join(",")}},{${edges.map(([a, b]) => `{${a.slice(2)},${b.slice(2)}}`).join(",")}}),${Kp})`;
    return { kind: "graphToGraph", G, tgt: { nodes, edges, layout: "mirror" }, gadgets, frames, targetInstance, targetLabel: "Vertex Cover", sourceLabel: "Clique" };
  }

  // Karp's Graph Coloring Reduction: palette F/T/B, a node per literal, a 6-node OR gadget per clause (names follow Redux: CiNj).
  function karpColoring(str) {
    const F = parseSat(str);
    if (F.clauses.length > 7) throw new Error("This mockup draws up to 7 clauses for this reduction.");
    const lits = [];
    F.clauses.flat().forEach(l => { if (!lits.includes(l)) lits.push(l); });
    lits.sort((a, b) => a.replace(/^!/, "").localeCompare(b.replace(/^!/, ""), undefined, { numeric: true }) || (a.startsWith("!") ? 1 : -1));
    const pad = c => { const p = c.slice(); while (p.length < 3) p.push(c[0]); return p; };
    const nodes = [], edges = [], frames = [], gadgets = [], seen = new Set();
    const add = (id, label, extra = {}) => nodes.push({ id: "t:" + id, name: id, label, ...extra });
    const link = (a, b) => { const k = ekey("t:" + a, "t:" + b); if (seen.has(k) || a === b) return 0; seen.add(k); edges.push(["t:" + a, "t:" + b]); return 1; };
    // palette
    ["F", "T", "B"].forEach(p => add(p, p, { role: "palette" }));
    link("F", "T"); link("T", "B"); link("F", "B");
    gadgets.push({ kind: "palette", from: [], to: ["t:F", "t:T", "t:B"], note: "F, T and B form a triangle, so they take three different colors" });
    frames.push({ phase: "build", nodes: nodes.length, edges: edges.length, focusSrc: [], focusTgt: ["t:F", "t:T", "t:B"],
      caption: "Palette: nodes F, T and B, joined in a triangle. In any 3-coloring they get three different colors; call them false, true and base." });
    // variables
    F.vars.forEach(v => {
      const here = lits.filter(l => l.replace(/^!/, "") === v);
      here.forEach(l => { add(l, litText(l), { role: "literal", lit: l }); link(l, "B"); });
      if (here.length === 2) link(here[0], here[1]);
      gadgets.push({ kind: "element", from: [`s:v:${v}`, ...F.clauses.flatMap((c, i) => c.map((l, j) => (l.replace(/^!/, "") === v ? `s:c${i}-${j}` : null))).filter(Boolean)], to: here.map(l => "t:" + l) });
      frames.push({ phase: "build", nodes: nodes.length, edges: edges.length, focusSrc: [`s:v:${v}`], focusTgt: here.map(l => "t:" + l),
        caption: here.length === 2 ? `Variable ${pretty(v)}: nodes ${pretty(v)} and ¬${pretty(v)}, each joined to B and to each other. So one gets the true color and the other the false color.`
          : `Variable ${pretty(v)} only appears as ${litText(here[0])}: one node, joined to B, so it's colored true or false.` });
    });
    // clauses
    F.clauses.forEach((c, i) => {
      const N = j => `C${i}N${j}`;
      for (let j = 0; j < 6; j++) add(N(j), `N${j}`, { role: "gadget", ci: i, gj: j });
      [[0, 1], [1, 2], [0, 2], [3, 4], [4, 5], [3, 5], [2, 3]].forEach(([a, b]) => link(N(a), N(b)));
      const [l0, l1, l2] = pad(c);
      link(l0, N(0)); link(l1, N(1)); link(l2, N(4)); link(N(2), "B"); link(N(5), "F"); link(N(5), "B");
      gadgets.push({ kind: "orGadget", from: [`s:c${i}`], to: [0, 1, 2, 3, 4, 5].map(j => "t:" + N(j)) });
      frames.push({ phase: "build", nodes: nodes.length, edges: edges.length, focusSrc: [`s:c${i}`, ...c.map((_, j) => `s:c${i}-${j}`)], focusTgt: [0, 1, 2, 3, 4, 5].map(j => "t:" + N(j)), gadgetOf: i,
        caption: `Clause C${i + 1} (${c.map(litText).join(" ∨ ")}): a 6-node OR gadget wired to ${[...new Set(pad(c))].map(litText).join(", ")}. Its output N5 is joined to F and B, so it must take the true color, and that only works if some literal is true.` });
    });
    frames.push({ phase: "build", nodes: nodes.length, edges: edges.length, focusSrc: [], focusTgt: [],
      caption: "K = 3 colors. The graph is 3-colorable exactly when the formula is satisfiable." });
    gadgets.push({ kind: "bound", from: [], to: [], note: "K = 3, always" });
    const base = { nodes: nodes.length, edges: edges.length };
    // Solve B: 3-coloring by backtracking (most-constrained node first), palette fixed to F=0, T=1, B=2.
    const adjL = new Map(nodes.map(x => [x.id, []]));
    edges.forEach(([a, b]) => { adjL.get(a).push(b); adjL.get(b).push(a); });
    const color = { "t:F": 0, "t:T": 1, "t:B": 2 };
    let tries = 0;
    const solve = () => {
      if (++tries > 200000) return false;
      let best = null, bestOpts = null;
      for (const x of nodes) {
        if (x.id in color) continue;
        const used = new Set(adjL.get(x.id).map(y => color[y]).filter(c => c !== undefined));
        const opts = [0, 1, 2].filter(c => !used.has(c));
        if (!bestOpts || opts.length < bestOpts.length) { best = x.id; bestOpts = opts; }
      }
      if (!best) return true;
      for (const c of bestOpts) { color[best] = c; if (solve()) return true; delete color[best]; }
      return false;
    };
    const colored = solve();
    if (!colored) {
      frames.push({ ...base, phase: "solve", done: true, ok: false, caption: "Graph Coloring Brute Force finds no 3-coloring, so the formula has no satisfying assignment." });
    } else {
      const colorBy = { ...color };
      frames.push({ ...base, phase: "solve", tgtColor: colorBy,
        caption: "Solve the Graph Coloring instance with Graph Coloring Brute Force: a 3-coloring exists. Blue nodes share F's color, orange share T's, purple share B's." });
      const assign = {};
      F.vars.forEach(v => {
        const pos = nodes.find(x => x.lit === v), neg = nodes.find(x => x.lit === "!" + v);
        const val = pos ? colorBy[pos.id] === 1 : colorBy[neg.id] !== 1;
        assign[v] = val;
        const node = pos || neg;
        frames.push({ ...base, phase: "map", tgtColor: colorBy, focusTgt: [node.id], focusSrc: [`s:v:${v}`], assign: { ...assign },
          caption: `${node.label} has ${colorBy[node.id] === 1 ? "T's" : "F's"} color, so ${pretty(v)} = ${val ? "true" : "false"}.` });
      });
      const sat = F.clauses.every(c => c.some(l => (l.startsWith("!") ? !assign[l.slice(1)] : assign[l])));
      frames.push({ ...base, phase: "map", done: true, ok: sat, tgtColor: colorBy, assign: { ...assign },
        caption: sat ? `Every OR gadget's output is true-colored, so every clause has a true literal: ${F.vars.map(v => `${pretty(v)}=${assign[v] ? "T" : "F"}`).join(", ")} satisfies the formula.` : "The mapped assignment doesn't satisfy the formula." });
    }
    const targetInstance = `(({${nodes.map(x => x.name).join(",")}},{${edges.map(([a, b]) => `{${a.slice(2)},${b.slice(2)}}`).join(",")}}),3)`;
    return { kind: "satToGraph", F, tgt: { nodes, edges, layout: "karp", m: F.clauses.length }, gadgets, frames, targetInstance, targetLabel: "Graph Coloring", sourceLabel: "3SAT" };
  }

  const REDUCTIONS = {
    SIPSER_CLIQUE: { label: "Sipser's Clique Reduction", from: "SAT3", to: "CLIQUE", fromLabel: "3SAT", toLabel: "Clique", run: sipserClique,
      def: "Sipser's reduction converts clauses from 3SAT into clusters of nodes in a graph for which cliques exist.",
      source: "Sipser, Introduction to the Theory of Computation", contributors: "Kaden Marchetti, Alex Diviney, Caleb Eardley, Russell Phillips",
      examples: [["Redux default", "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)"], ["Four clauses", "(x1 | x2 | x3) & (!x1 | !x2 | x3) & (x1 | !x3 | x4) & (!x2 | !x4 | x3)"], ["Unsatisfiable (2 variables)", "(x1 | x2) & (x1 | !x2) & (!x1 | x2) & (!x1 | !x2)"]] },
    SIPSER_VC: { label: "Sipser's Vertex Cover Reduction", from: "CLIQUE", to: "VERTEXCOVER", fromLabel: "Clique", toLabel: "Vertex Cover", run: sipserVertexCover,
      def: "Takes every possible edge of the Clique graph and removes the ones that are actually in it (the complement), with K' = N − K.",
      source: "Sipser, Introduction to the Theory of Computation", contributors: "Janita Aamir, Alex Diviney, Caleb Eardley",
      examples: [["Redux default", "(({1,2,3,4,5,6},{{4,1},{1,2},{4,3},{3,2},{2,4},{5,2},{3,5},{5,4},{3,6},{6,4},{1,6}}),4)"], ["No clique of size K", "(({1,2,3,4,5},{{1,2},{2,3},{3,4},{4,5},{5,1},{1,3}}),4)"]] },
    KARP_COLORING: { label: "Karp's Graph Coloring Reduction", from: "SAT3", to: "GRAPHCOLORING", fromLabel: "3SAT", toLabel: "Graph Coloring", run: karpColoring,
      def: "Karp's reduction converts each clause of a 3CNF into an OR gadget, with a palette triangle and a node per literal, so the truth assignment shows up as colors.",
      source: "cs.bme.hu/thalg/3sat-to-3col.pdf", contributors: "Daniel Igbokwe",
      examples: [["Two clauses", "(x1 | !x2 | x3) & (!x1 | x2 | !x3)"], ["Redux default", "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)"], ["Unsatisfiable (2 variables)", "(x1 | x2) & (x1 | !x2) & (!x1 | x2) & (!x1 | !x2)"]] },
  };

  /* ---------- drawing ---------- */
  function create({ svg }) {
    if (!document.getElementById("rd-style")) { const st = document.createElement("style"); st.id = "rd-style"; st.textContent = css; document.head.appendChild(st); }
    svg.classList.add("rd-svg");
    let R, red, frame = null, colors = false, els, gIndex;

    function layout() {
      const W = 1080, SX = 0, SW = 360, TX = 420, TW = 660;
      const P = new Map();
      let srcH, tgtH;
      if (R.kind === "satToGraph") {
        srcH = 92 + R.F.clauses.length * 54 + 100;
      } else {
        srcH = 380;
        const n = R.G.nodes.length;
        R.G.nodes.forEach((x, i) => { const a = -Math.PI / 2 + (2 * Math.PI * i) / n; P.set("s:" + x, { x: SX + SW / 2 + 130 * Math.cos(a), y: 210 + 130 * Math.sin(a) }); });
      }
      const T = R.tgt;
      if (T.layout === "clusters") {
        const m = T.m, cx = TX + TW / 2, R0 = m === 1 ? 0 : Math.max(120, 56 / Math.sin(Math.PI / m));
        tgtH = 2 * (R0 + 80) + 40;
        const cy = 56 + R0 + 60;
        for (let i = 0; i < m; i++) {
          const th = -Math.PI / 2 + (2 * Math.PI * i) / m, gx = cx + R0 * Math.cos(th), gy = cy + R0 * Math.sin(th);
          const mem = T.nodes.filter(n => n.ci === i), k = mem.length, rin = k <= 1 ? 0 : (NR + 6) / Math.sin(Math.PI / k);
          mem.forEach((n, j) => { const a = th + Math.PI + (2 * Math.PI * j) / k; P.set(n.id, { x: gx + rin * Math.cos(a), y: gy + rin * Math.sin(a) }); });
          const d = rin + NR + 14, c = Math.cos(th), sn = Math.sin(th);
          P.set("h:" + i, { x: gx, y: gy, r: rin + NR + 9, lx: gx + d * c, ly: gy + d * sn + (sn > 0.35 ? 12 : sn < -0.35 ? -6 : 0), anchor: c > 0.35 ? "start" : c < -0.35 ? "end" : "middle" });
        }
      } else if (T.layout === "mirror") {
        const n = R.G.nodes.length;
        R.G.nodes.forEach((x, i) => { const a = -Math.PI / 2 + (2 * Math.PI * i) / n; P.set("t:" + x, { x: TX + TW / 2 + 150 * Math.cos(a), y: 210 + 150 * Math.sin(a) }); });
        tgtH = 400;
      } else {
        const cx = TX + TW / 2;
        P.set("t:F", { x: cx - 70, y: 74 }); P.set("t:T", { x: cx + 70, y: 74 }); P.set("t:B", { x: cx, y: 124 });
        const litNodes = T.nodes.filter(n => n.role === "literal");
        litNodes.forEach((n, i) => P.set(n.id, { x: TX + 40 + (i + 0.5) * ((TW - 80) / litNodes.length), y: 196 }));
        const perRow = Math.min(T.m, 4), bw = (TW - 40) / perRow;
        for (let i = 0; i < T.m; i++) {
          const gx = TX + 20 + bw * (i % perRow) + bw / 2, gy = 286 + 130 * Math.floor(i / perRow);
          const pos = [[-52, 0], [-52, 58], [-18, 29], [18, 29], [52, 0], [52, 58]];
          pos.forEach(([dx, dy], j) => P.set(`t:C${i}N${j}`, { x: gx + dx, y: gy + dy }));
          P.set("h:" + i, { x: gx, y: gy + 29, lx: gx, ly: gy + 92, box: true });
        }
        tgtH = 286 + 130 * Math.ceil(T.m / perRow) + 20;
      }
      return { W, H: Math.max(srcH, tgtH, 360), P, SX, SW, TX, TW };
    }

    function draw() {
      svg.innerHTML = "";
      const L = layout(), P = L.P;
      svg.setAttribute("viewBox", `0 0 ${L.W} ${L.H}`);
      els = { P, nodes: new Map(), edges: [], hulls: new Map(), clauses: new Map(), lits: new Map(), vars: new Map(), gLinks: null, gPh: null, srcEdges: [] };
      mk("text", { x: L.SX + 12, y: 28, class: "rd-pane-title" }, svg).textContent = `${red.fromLabel.toUpperCase()} · THE INSTANCE YOU GAVE`;
      mk("text", { x: L.TX + 12, y: 28, class: "rd-pane-title" }, svg).textContent = `${red.toLabel.toUpperCase()} · BUILT BY THE REDUCTION`;
      mk("line", { class: "rd-sep", x1: L.TX - 30, y1: 40, x2: L.TX - 30, y2: L.H - 20, stroke: "var(--av-line)", "stroke-dasharray": "2 5" }, svg);
      mk("text", { x: L.TX - 30, y: L.H / 2 - 12, class: "rd-arrow" }, svg).textContent = "↦";
      const hookup = (g, id) => {
        const on = () => setHover(id), off = () => setHover(null);
        g.addEventListener("pointerenter", on); g.addEventListener("pointerleave", off);
        g.addEventListener("focus", on); g.addEventListener("blur", off);
        g.addEventListener("click", () => setHover(hoverId === id ? null : id));
      };
      // source pane
      if (R.kind === "satToGraph") {
        R.F.clauses.forEach((c, i) => {
          const y = 66 + i * 54, g = mk("g", { class: "rd-clause", tabindex: "0", role: "button", "aria-label": `Clause ${i + 1}` }, svg);
          mk("rect", { x: L.SX + 12, y, width: L.SW - 24, height: 40, rx: 8, class: "box" }, g);
          mk("text", { x: L.SX + 24, y: y + 20, class: "cid" }, g).textContent = "C" + (i + 1);
          P.set(`s:c${i}`, { x: L.SX + L.SW - 12, y: y + 20 });
          els.clauses.set(`s:c${i}`, g); hookup(g, `s:c${i}`);
          c.forEach((l, j) => {
            const x = L.SX + 82 + j * 92;
            if (j) mk("text", { x: x - 46, y: y + 20, class: "or" }, g).textContent = "∨";
            const lg = mk("g", { class: "rd-lit" }, svg);
            mk("rect", { x: x - 32, y: y + 6, width: 64, height: 28, rx: 6 }, lg);
            mk("text", { x, y: y + 20 }, lg).textContent = litText(l);
            P.set(`s:c${i}-${j}`, { x: x + 32, y: y + 20 });
            els.lits.set(`s:c${i}-${j}`, { g: lg, lit: l }); hookup(lg, `s:c${i}-${j}`);
          });
        });
        const vy = 66 + R.F.clauses.length * 54 + 34;
        mk("text", { x: L.SX + 12, y: vy - 12, class: "rd-pane-title" }, svg).textContent = "ASSIGNMENT";
        R.F.vars.forEach((v, i) => {
          const x = L.SX + 12 + (i % 5) * 68, y = vy + Math.floor(i / 5) * 36;
          const g = mk("g", { class: "rd-var" }, svg);
          mk("rect", { x, y, width: 60, height: 28, rx: 6 }, g);
          const t = mk("text", { x: x + 30, y: y + 14 }, g); t.textContent = `${pretty(v)} –`;
          P.set(`s:v:${v}`, { x: x + 60, y: y + 14 });
          els.vars.set(v, { g, t }); hookup(g, `s:v:${v}`);
        });
      } else {
        const gE = mk("g", {}, svg);
        R.G.edges.forEach(([a, b]) => { const p = P.get("s:" + a), q = P.get("s:" + b); els.srcEdges.push({ k: ekey("s:" + a, "s:" + b), a: "s:" + a, b: "s:" + b, el: mk("line", { x1: p.x, y1: p.y, x2: q.x, y2: q.y, class: "rd-edge" }, gE) }); });
        els.srcPh = mk("g", {}, svg);
        R.G.nodes.forEach(x => {
          const p = P.get("s:" + x), g = mk("g", { class: "rd-node", tabindex: "0", role: "button", "aria-label": `Node ${x} of G` }, svg);
          mk("circle", { cx: p.x, cy: p.y, r: NR + 2, class: "body" }, g); mk("text", { x: p.x, y: p.y }, g).textContent = x;
          els.nodes.set("s:" + x, g); hookup(g, "s:" + x);
        });
        mk("text", { x: L.SX + 12, y: 372, class: "rd-pane-title" }, svg).textContent = `K = ${R.G.K}`;
      }
      // target pane
      const T = R.tgt;
      if (T.layout !== "mirror") for (let i = 0; i < (T.m || 0); i++) {
        const h = P.get("h:" + i);
        const el = h.box ? mk("rect", { x: h.x - 78, y: h.y - 46, width: 156, height: 92, rx: 12, class: "rd-hull" }, svg) : mk("circle", { cx: h.x, cy: h.y, r: h.r, class: "rd-hull" }, svg);
        const lb = mk("text", { x: h.lx, y: h.ly, class: "rd-hlabel", "text-anchor": h.anchor || "middle" }, svg); lb.textContent = (h.box ? "OR gadget · C" : "cluster · C") + (i + 1);
        els.hulls.set(i, { el, lb });
      }
      const gE = mk("g", {}, svg);
      T.edges.forEach(([a, b], idx) => { const p = P.get(a), q = P.get(b); els.edges.push({ k: ekey(a, b), a, b, idx, el: mk("line", { x1: p.x, y1: p.y, x2: q.x, y2: q.y, class: "rd-edge hidden" }, gE) }); });
      els.gPh = mk("g", {}, svg);
      T.nodes.forEach((n, idx) => {
        const p = P.get(n.id), g = mk("g", { class: "rd-node hidden", tabindex: "0", role: "button", "aria-label": `${red.toLabel} node ${n.name}` }, svg);
        const r = n.role === "gadget" ? NR - 3 : NR;
        mk("circle", { cx: p.x, cy: p.y, r, class: "body" }, g);
        const t = mk("text", { x: p.x, y: p.y }, g); t.textContent = n.label; if (n.label.length > 3) t.style.fontSize = "10.5px";
        els.nodes.set(n.id, g); n.idx = idx; hookup(g, n.id);
      });
      els.gLinks = mk("g", {}, svg);
      gIndex = new Map();
      R.gadgets.forEach((gd, gi) => [...gd.from, ...gd.to].forEach(id => { if (!gIndex.has(id)) gIndex.set(id, []); gIndex.get(id).push(gi); }));
    }

    let hoverId = null;
    function setHover(id) { hoverId = id; paint(); }

    function paint() {
      const f = frame, T = R.tgt;
      const shownN = f ? f.nodes : T.nodes.length, shownE = f ? f.edges : T.edges.length;
      const lastFrame = R.frames[R.frames.length - 1];
      // hover group: every id in any gadget that contains the hovered id
      const hot = new Set(), links = [];
      if (hoverId) {
        hot.add(hoverId);
        (gIndex.get(hoverId) || []).forEach(gi => {
          const gd = R.gadgets[gi];
          gd.from.forEach(a => hot.add(a)); gd.to.forEach(b => hot.add(b));
          gd.from.forEach(a => gd.to.forEach(b => { if (links.length < 14 && els.P.has(a) && els.P.has(b) && !b.startsWith("h:")) links.push([a, b]); }));
        });
      }
      const focus = new Set([...(f && f.focusSrc || []), ...(f && f.focusTgt || [])]);
      const groupIdx = id => {
        if (!colors) return null;
        const list = R.gadgets.filter(gd => gd.kind === "group" || gd.kind === "orGadget" || (gd.kind === "element" && R.kind === "graphToGraph"));
        const gi = list.findIndex(gd => gd.from.includes(id) || gd.to.includes(id));
        return gi < 0 ? null : gi % 6;
      };
      // target nodes
      T.nodes.forEach(n => {
        const g = els.nodes.get(n.id);
        let cls = "rd-node";
        if (n.idx >= shownN) cls += " hidden";
        else {
          if (f && f.tgtColor && n.id in f.tgtColor) cls += " c" + [0, 1, 2][f.tgtColor[n.id]];
          else if (f && f.tgtSol && f.tgtSol.includes(n.id)) cls += " Solution";
          else if (f && f.tgtCovered && f.tgtSol) cls += "";
          if (f && f.phase === "build" && f.focusTgt && f.focusTgt.includes(n.id)) cls += " Active";
          else if (f && f.phase === "map" && f.focusTgt && f.focusTgt.includes(n.id)) cls += " Active";
          const gc = groupIdx(n.id); if (gc !== null && !(f && f.tgtColor)) cls += " g" + gc;
          if (hoverId) cls += hot.has(n.id) ? " trace" : " faint";
        }
        g.setAttribute("class", cls);
      });
      // target edges
      const solE = new Set(f && f.tgtSolEdges || []), newCount = f && f.newEdges || 0;
      const tgtSolSet = new Set(f && f.tgtSol || []);
      els.edges.forEach(o => {
        let cls = "rd-edge";
        if (o.idx >= shownE) cls += " hidden";
        else if (hoverId) cls += hot.has(o.a) && hot.has(o.b) ? " trace" : " faint";
        else if (f && f.phase === "build" && o.idx >= shownE - newCount) cls += " new";
        else if (solE.has(o.k)) cls += " sol";
        else if (f && f.tgtCovered && (tgtSolSet.has(o.a) || tgtSolSet.has(o.b))) cls += " cov";
        else if (f && (f.phase !== "build" || T.edges.length > 30)) cls += " dim";
        o.el.setAttribute("class", cls);
      });
      // hulls
      els.hulls.forEach((h, i) => {
        let cls = "rd-hull";
        const memberIds = T.nodes.filter(n => n.ci === i).map(n => n.id);
        if (memberIds.every(id => T.nodes.find(n => n.id === id).idx >= shownN)) cls += " hidden";
        else if (f && (f.hull === i || f.gadgetOf === i)) cls += " Active";
        else if (f && f.tgtSol && memberIds.some(id => f.tgtSol.includes(id))) cls += " Covered";
        const gc = groupIdx("h:" + i) ?? groupIdx(memberIds[0]); if (gc !== null) cls += " g" + gc;
        h.el.setAttribute("class", cls);
        h.lb.style.opacity = cls.includes("hidden") ? 0 : 1;
      });
      // phantoms (pairs the rule skipped, or pairs G is missing)
      els.gPh.innerHTML = "";
      if (f && f.skipped && !hoverId) f.skipped.forEach(([a, b]) => { const p = els.P.get(a), q = els.P.get(b); mk("line", { x1: p.x, y1: p.y, x2: q.x, y2: q.y, class: "rd-phantom" }, els.gPh); });
      if (els.srcPh) { els.srcPh.innerHTML = ""; if (f && f.srcPhantom && !hoverId) f.srcPhantom.forEach(([a, b]) => { const p = els.P.get(a), q = els.P.get(b); mk("line", { x1: p.x, y1: p.y, x2: q.x, y2: q.y, class: "rd-phantom" }, els.srcPh); }); }
      // source side
      const assign = f && f.assign || null;
      const litVal = l => assign && (l.replace(/^!/, "") in assign) ? (l.startsWith("!") ? !assign[l.slice(1)] : assign[l.replace(/^!/, "")]) : undefined;
      els.clauses.forEach((g, id) => {
        const i = +id.slice(3);
        let cls = "rd-clause";
        if (assign && R.F.clauses[i].some(l => litVal(l) === true)) cls += " Covered";
        if (focus.has(id)) cls += " Active";
        const gc = groupIdx(id); if (gc !== null) cls += " g" + gc;
        if (hoverId) cls += hot.has(id) ? " trace" : " faint";
        g.setAttribute("class", cls);
      });
      els.lits.forEach((o, id) => {
        const v = litVal(o.lit);
        let cls = "rd-lit";
        if (v === true) cls += " Solution"; else if (v === false) cls += " False";
        if (focus.has(id)) cls += " Active";
        if (hoverId && hot.has(id)) cls += " trace";
        o.g.setAttribute("class", cls);
      });
      els.vars.forEach((o, v) => {
        const val = assign && v in assign ? assign[v] : undefined;
        o.t.textContent = `${pretty(v)} ${val === undefined ? "–" : val ? "T" : "F"}`;
        o.g.setAttribute("class", "rd-var" + (val === undefined ? "" : val ? " T" : " F") + (focus.has("s:v:" + v) ? " Active" : ""));
      });
      if (R.kind === "graphToGraph") {
        const sol = new Set(f && f.srcSol || []), out = new Set(f && f.srcOut || []), sE = new Set(f && f.srcSolEdges || []);
        R.G.nodes.forEach(x => {
          const id = "s:" + x;
          let cls = "rd-node" + (sol.has(id) ? " Solution" : out.has(id) ? " Blocked" : "");
          if (focus.has(id)) cls += " Active";
          const gc = groupIdx(id); if (gc !== null && !sol.has(id)) cls += " g" + gc;
          if (hoverId) cls += hot.has(id) ? " trace" : " faint";
          els.nodes.get(id).setAttribute("class", cls);
        });
        els.srcEdges.forEach(o => o.el.setAttribute("class", "rd-edge" + (hoverId ? (hot.has(o.a) && hot.has(o.b) ? " trace" : " faint") : sE.has(o.k) ? " sol" : sol.size ? " dim" : "")));
      }
      // cross-pane links on hover
      els.gLinks.innerHTML = "";
      links.forEach(([a, b]) => {
        const p = els.P.get(a), q = els.P.get(b);
        if (!p || !q) return;
        const tgtNode = T.nodes.find(n => n.id === b);
        if (tgtNode && tgtNode.idx >= shownN) return;
        const mx = (p.x + q.x) / 2;
        mk("path", { d: `M${p.x},${p.y} C${mx},${p.y} ${mx},${q.y} ${q.x},${q.y}`, class: "rd-link" }, els.gLinks);
      });
    }

    function checks(f) {
      const T = R.tgt, out = [];
      const nN = f ? f.nodes : T.nodes.length, nE = f ? f.edges : T.edges.length;
      out.push({ label: `${red.toLabel} nodes built: ${nN} / ${T.nodes.length}`, ok: f ? nN === T.nodes.length : null });
      out.push({ label: `${red.toLabel} edges built: ${nE} / ${T.edges.length}`, ok: f ? nE === T.edges.length : null });
      const last = R.frames[R.frames.length - 1];
      if (f && (f.phase === "solve" || f.phase === "map")) {
        const solved = R.frames.find(x => x.phase === "solve");
        out.push({ label: `${red.toLabel} answer: ${solved && !solved.done ? "found" : "none exists"}`, ok: !!(solved && !solved.done) });
      }
      if (f && f.done) out.push({ label: `Mapped back to a valid ${red.fromLabel} answer`, ok: !!f.ok });
      if (f && !f.done && last.done && f === last) out.push({ label: "Done", ok: true });
      return out;
    }

    return {
      load(src, key) {
        red = REDUCTIONS[key];
        R = red.run(src);
        frame = null; hoverId = null;
        draw(); paint();
        return { frames: R.frames, ok: R.frames[R.frames.length - 1].ok, targetInstance: R.targetInstance, gadgets: R.gadgets, reduction: red };
      },
      show(i) { frame = i === null ? null : R.frames[i]; paint(); return frame; },
      checks(i) { return checks(i === null ? null : R.frames[i]); },
      setGadgetColors(on) { colors = !!on; if (R) paint(); },
      resetHover() { hoverId = null; if (R) paint(); },
    };
  }

  return { create, REDUCTIONS, GADGET_KINDS, parse: { sat: parseSat, clique: parseClique }, _run: k => REDUCTIONS[k].run };
})();
