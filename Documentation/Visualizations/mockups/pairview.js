/* ---- Pair views: Dominoes (Post Correspondence Problem) and Two-Graph Mapping (Graph / Subgraph Isomorphism) ----
   Neither problem is in Redux yet; these are starting points for when they are added.
   One module, two pictures. Solvers emit frames in the problem's own terms; paint derives every color.
   Pages supply --av-* tokens; state names follow the shared vocabulary. */
const PairView = (() => {
  const NS = "http://www.w3.org/2000/svg";
  const mk = (tag, attrs = {}, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; };
  const SHOW = 150, SEP = "\u0000";
  const key = (a, b) => (a < b ? a + SEP + b : b + SEP + a);

  const css = `
.pv-svg { width: 100%; height: auto; display: block; }
.pv-svg text { font-family: var(--av-mono); }
.pv-head { font-size: 11px; font-weight: 600; letter-spacing: .08em; fill: var(--av-muted); }
.pv-note { font-size: 12px; fill: var(--av-muted); }
.pv-tile { cursor: pointer; transition: opacity .2s; }
.pv-tile:focus { outline: none; }
.pv-tile .half { fill: var(--av-bg); stroke: var(--av-stroke); stroke-width: 1.4; }
.pv-tile .mid { stroke: var(--av-stroke); stroke-width: 1; }
.pv-tile text { font-size: 14px; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); pointer-events: none; }
.pv-tile .num { font-size: 11px; font-weight: 700; fill: var(--av-muted); }
.pv-tile.c0 .half { stroke: #2b7fb0; } .pv-tile.c1 .half { stroke: #a87200; } .pv-tile.c2 .half { stroke: #7a5cb8; }
.pv-tile.c3 .half { stroke: #b5a90f; } .pv-tile.c4 .half { stroke: #2a8a70; } .pv-tile.c5 .half { stroke: #9a4c78; }
.pv-tile.used .half { stroke-width: 2.4; }
.pv-tile.Active .half { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; }
.pv-tile.trace .half, .pv-tile:focus-visible .half { stroke: var(--av-hot); stroke-width: 3; }
.pv-tile.faint { opacity: .3; }
.pv-cell rect { fill: var(--av-bg); stroke: var(--av-line); stroke-width: 1.2; transition: fill .2s, stroke .2s; }
.pv-cell text { font-size: 14px; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); }
.pv-cell.Covered rect { fill: var(--av-sol-fill); stroke: var(--av-sol); }
.pv-cell.Solution rect { fill: var(--av-sol); stroke: var(--av-sol); } .pv-cell.Solution text { fill: #fff; font-weight: 600; }
.pv-cell.Active rect { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 2.2; }
.pv-cell.Rejected rect { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 2.4; } .pv-cell.Rejected text { fill: var(--av-rej); font-weight: 700; }
.pv-cell.slot rect { fill: transparent; stroke: var(--av-hl); stroke-dasharray: 3 3; }
.pv-span line { stroke-width: 3; stroke-linecap: round; }
.pv-span text { font-size: 10.5px; font-weight: 700; text-anchor: middle; dominant-baseline: central; }
.pv-span.c0 line { stroke: #56B4E9; } .pv-span.c0 text { fill: #2b7fb0; }
.pv-span.c1 line { stroke: #E69F00; } .pv-span.c1 text { fill: #a87200; }
.pv-span.c2 line { stroke: #b59ce0; } .pv-span.c2 text { fill: #7a5cb8; }
.pv-span.c3 line { stroke: #d6c81f; } .pv-span.c3 text { fill: #8a800a; }
.pv-span.c4 line { stroke: #5fc4a8; } .pv-span.c4 text { fill: #2a8a70; }
.pv-span.c5 line { stroke: #CC79A7; } .pv-span.c5 text { fill: #9a4c78; }
.pv-tick { stroke: var(--av-line); stroke-width: 1; }
.pv-panel { fill: none; stroke: var(--av-line); stroke-width: 1; stroke-dasharray: 2 4; }
.pv-node { cursor: pointer; transition: opacity .2s; }
.pv-node:focus { outline: none; }
.pv-node .body { fill: var(--av-bg); stroke: var(--av-stroke); stroke-width: 1.8; transition: fill .2s, stroke .2s; }
.pv-node text { font-size: 13px; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); pointer-events: none; }
.pv-node .deg { font-size: 10.5px; fill: var(--av-muted); }
.pv-node .tag { font-size: 11px; font-weight: 700; fill: var(--av-ink); }
.pv-node.c0 .body { fill: #56B4E9; stroke: #2b7fb0; } .pv-node.c1 .body { fill: #E69F00; stroke: #a87200; }
.pv-node.c2 .body { fill: #b59ce0; stroke: #7a5cb8; } .pv-node.c3 .body { fill: #F0E442; stroke: #b5a90f; }
.pv-node.c4 .body { fill: #5fc4a8; stroke: #2a8a70; } .pv-node.c5 .body { fill: #CC79A7; stroke: #9a4c78; }
.pv-node[class*=" c"] text.lbl { fill: #10131a; font-weight: 600; }
.pv-node.Blocked .body { fill: transparent; stroke: var(--av-line); stroke-dasharray: 4 3; } .pv-node.Blocked text.lbl { fill: var(--av-muted); }
.pv-node.Rejected .body { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 3; } .pv-node.Rejected text.lbl { fill: var(--av-rej); }
.pv-node.ring .body { stroke: var(--av-hl); stroke-width: 3.6; }
.pv-node.trace .body, .pv-node:focus-visible .body { stroke: var(--av-hot); stroke-width: 3.2; }
.pv-node.faint { opacity: .28; }
.pv-edge { stroke: var(--av-edge); stroke-width: 1.6; transition: opacity .2s, stroke .2s; }
.pv-edge.sol { stroke: var(--av-sol); stroke-width: 3; }
.pv-edge.rej { stroke: var(--av-rej); stroke-width: 3; }
.pv-edge.extra { stroke-dasharray: 2 4; opacity: .55; }
.pv-edge.trace { stroke: var(--av-hot); stroke-width: 2.6; }
.pv-edge.faint { opacity: .1; }
.pv-phantom { stroke: var(--av-rej); stroke-width: 2.4; stroke-dasharray: 6 5; fill: none; }
.pv-link { stroke: var(--av-hot); stroke-width: 1.6; stroke-dasharray: 5 4; fill: none; }
.pv-link.hot { stroke: var(--av-hl); stroke-width: 2.4; }
@media (prefers-reduced-motion: reduce) { .pv-tile, .pv-cell rect, .pv-node, .pv-node .body, .pv-edge { transition: none; } }`;

  /* ---------- shared parsing (Redux tuple style) ---------- */
  function tokenize(s) {
    const out = []; let buf = "";
    for (const ch of s) {
      if ("{}(),".includes(ch)) { if (buf.trim()) out.push(buf.trim()); buf = ""; out.push(ch); }
      else buf += ch;
    }
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

  /* ---------- Post Correspondence Problem ---------- */
  const MAXT = 12, MAXO = 12, MAXS = 4000, MAXN = 20000;
  function parsePCP(str) {
    const top = tree(tokenize(str));
    if (top.t !== "set" && top.t !== "tup") throw new Error("Expected a list of tiles like {(a,ab),(b,ca)}.");
    if (!top.items.length) throw new Error("Give at least one tile.");
    if (top.items.length > 8) throw new Error("This mockup draws up to 8 tiles.");
    return top.items.map((it, i) => {
      if (typeof it === "string" || it.t !== "tup" || it.items.length !== 2 || it.items.some(x => typeof x !== "string"))
        throw new Error(`Tile ${i + 1} must look like (top,bottom).`);
      const [t, b] = it.items;
      if (/\s/.test(t + b)) throw new Error(`Tile ${i + 1}: strings can't contain spaces.`);
      if (t.length > 6 || b.length > 6) throw new Error(`Tile ${i + 1}: this mockup keeps each string to 6 letters.`);
      return { t, b };
    });
  }
  const build = (T, seq) => ({ top: seq.map(i => T[i].t).join(""), bot: seq.map(i => T[i].b).join("") });
  const seqTxt = seq => seq.map(i => i + 1).join(", ");
  // Extend an overhang by one tile. side "" = nothing pending, "top" / "bot" = which string runs ahead.
  function extend(side, over, tile) {
    const A = (side === "top" ? over : "") + tile.t, B = (side === "bot" ? over : "") + tile.b;
    const L = Math.min(A.length, B.length);
    for (let i = 0; i < L; i++) if (A[i] !== B[i]) return { ok: false };
    if (A.length === B.length) return { ok: true, side: "", over: "" };
    return A.length > B.length ? { ok: true, side: "top", over: A.slice(L) } : { ok: true, side: "bot", over: B.slice(L) };
  }
  function describe(T, seq, kind, extra) {
    const s = build(T, seq);
    if (kind === "mismatch") {
      let i = 0; while (i < Math.min(s.top.length, s.bot.length) && s.top[i] === s.bot[i]) i++;
      return `Try tiles ${seqTxt(seq)}: letter ${i + 1} is "${s.top[i]}" on top but "${s.bot[i]}" underneath. Dead end.`;
    }
    const L = Math.min(s.top.length, s.bot.length), side = s.top.length > s.bot.length ? "top" : "bottom";
    const over = (s.top.length > s.bot.length ? s.top : s.bot).slice(L);
    if (kind === "match") return `Tiles ${seqTxt(seq)} spell "${s.top}" on both top and bottom. That's a match.`;
    if (kind === "new") return `Try tiles ${seqTxt(seq)}: everything lines up so far, and the ${side} runs ahead by "${over}". Keep going from here.`;
    if (kind === "seen") return `Try tiles ${seqTxt(seq)}: the ${side} runs ahead by "${over}", the same leftover as the shorter tiles ${seqTxt(extra)}. Nothing new to explore, so skip it.`;
    if (kind === "long") return `Try tiles ${seqTxt(seq)}: the ${side} now runs ahead by ${over.length} letters, past this search's limit of ${MAXO}. Stop following it.`;
    if (kind === "deep") return `Try tiles ${seqTxt(seq)}: it lines up, but ${seq.length} tiles is this round's limit. Back up.`;
    return "";
  }
  function pcpBfs(T) {
    const frames = [], push = f => { if (frames.length < SHOW) frames.push(f); };
    const visited = new Map();
    let queue = [{ seq: [], side: "", over: "" }], capped = false, tries = 0;
    while (queue.length) {
      const cur = queue.shift();
      if (cur.seq.length >= MAXT) { capped = true; continue; }
      for (let ti = 0; ti < T.length; ti++) {
        const seq = cur.seq.concat(ti), r = extend(cur.side, cur.over, T[ti]);
        tries++;
        if (!r.ok) { push({ seq, kind: "mismatch", trial: true, caption: describe(T, seq, "mismatch") }); continue; }
        if (r.over === "") {
          push({ seq, kind: "match", caption: describe(T, seq, "match") });
          frames.push({ seq, done: true, ok: true, verdict: "match", caption: `Tiles ${seqTxt(seq)} make a match: "${build(T, seq).top}" reads the same on top and bottom. Breadth-first search finds the shortest match first, so no match uses fewer tiles. (${tries} tries.)` });
          return frames;
        }
        if (r.over.length > MAXO) { capped = true; push({ seq, kind: "long", trial: true, caption: describe(T, seq, "long") }); continue; }
        const k = r.side + "|" + r.over;
        if (visited.has(k)) { push({ seq, kind: "seen", blocked: true, caption: describe(T, seq, "seen", visited.get(k)) }); continue; }
        visited.set(k, seq); queue.push({ seq, side: r.side, over: r.over });
        push({ seq, kind: "new", caption: describe(T, seq, "new") });
      }
      if (visited.size > MAXS) { capped = true; break; }
    }
    if (!capped) frames.push({ seq: [], done: true, ok: false, verdict: "noMatch", caption: `Every leftover these tiles can produce has been explored, and none leads back to an even line. So this instance has no match. Some instances can be settled like this; PCP as a whole can't be. (${tries} tries, ${visited.size} different leftovers.)` });
    else frames.push({ seq: [], done: true, ok: false, verdict: "unknown", caption: `No match within ${MAXT} tiles (with leftovers up to ${MAXO} letters). PCP is undecidable, so no search can prove in general that a match doesn't exist: a longer sequence might still work. (${tries} tries.)` });
    return frames;
  }
  function pcpIds(T) {
    const frames = [], push = f => { if (frames.length < SHOW) frames.push(f); };
    let nodes = 0, found = null;
    for (let d = 1; d <= MAXT && !found && nodes < MAXN; d++) {
      let cut = false;
      push({ seq: [], caption: `Round ${d}: look for a match using at most ${d} tile${d > 1 ? "s" : ""}, depth first.` });
      const dfs = (seq, side, over) => {
        if (found || nodes >= MAXN) return;
        for (let ti = 0; ti < T.length && !found; ti++) {
          const s2 = seq.concat(ti), r = extend(side, over, T[ti]);
          nodes++;
          if (!r.ok) { push({ seq: s2, kind: "mismatch", trial: true, caption: describe(T, s2, "mismatch") }); continue; }
          if (r.over === "") { found = s2; push({ seq: s2, kind: "match", caption: describe(T, s2, "match") }); return; }
          if (s2.length >= d) { cut = true; push({ seq: s2, kind: "deep", caption: describe(T, s2, "deep") }); continue; }
          push({ seq: s2, kind: "new", caption: describe(T, s2, "new") });
          dfs(s2, r.side, r.over);
        }
      };
      dfs([], "", "");
      if (!found && !cut && nodes < MAXN) {
        frames.push({ seq: [], done: true, ok: false, verdict: "noMatch", caption: `In round ${d} every sequence hit a mismatch before reaching ${d} tiles, so no sequence of any length can match. This instance has no match. (${nodes} tries.)` });
        return frames;
      }
    }
    if (found) frames.push({ seq: found, done: true, ok: true, verdict: "match", caption: `Tiles ${seqTxt(found)} make a match: "${build(T, found).top}" reads the same on top and bottom. Rounds grow one tile at a time, so this is a shortest match. (${nodes} tries.)` });
    else frames.push({ seq: [], done: true, ok: false, verdict: "unknown", caption: `No match within ${MAXT} tiles. Depth-first rounds can't skip repeated leftovers the way breadth-first search does, and PCP is undecidable anyway, so this can't prove no match exists. (${nodes} tries.)` });
    return frames;
  }

  /* ---------- Graph Isomorphism / Subgraph Isomorphism ---------- */
  function parseGraph(node, which) {
    if (!node || node.t !== "tup" || node.items.length !== 2 || node.items[0].t !== "set" || node.items[1].t !== "set")
      throw new Error(`${which} must look like ({nodes},{{a,b},…}).`);
    const nodes = node.items[0].items.map(x => { if (typeof x !== "string") throw new Error(`${which}: node names must be plain names.`); return x; });
    if (!nodes.length || new Set(nodes).size !== nodes.length) throw new Error(`${which}: the node set must be non-empty with no repeats.`);
    if (nodes.length > 9) throw new Error(`${which}: this mockup draws up to 9 nodes per graph.`);
    const S = new Set(nodes), edges = [], seen = new Set();
    for (const e of node.items[1].items) {
      if (typeof e === "string" || e.t !== "set" || e.items.length !== 2 || e.items.some(x => typeof x !== "string")) throw new Error(`${which}: each edge must look like {a,b}.`);
      const [a, b] = e.items;
      if (!S.has(a) || !S.has(b)) throw new Error(`${which}: edge {${a},${b}} uses a node that isn't listed.`);
      if (a === b) throw new Error(`${which}: edge {${a},${b}} is a loop.`);
      const k = key(a, b); if (seen.has(k)) continue;
      seen.add(k); edges.push({ a, b, k });
    }
    const adj = new Map(nodes.map(n => [n, new Set()]));
    edges.forEach(e => { adj.get(e.a).add(e.b); adj.get(e.b).add(e.a); });
    return { nodes, edges, adj, has: (a, b) => adj.get(a).has(b), deg: n => adj.get(n).size };
  }
  function parseGI(str, sub) {
    const top = tree(tokenize(str));
    if (top.t !== "tup" || top.items.length !== 2) throw new Error("Expected two graphs: ((N1,E1),(N2,E2)).");
    const L = parseGraph(top.items[0], sub ? "The pattern H" : "Graph G"), R = parseGraph(top.items[1], sub ? "The host G" : "Graph H");
    if (sub && L.nodes.length > R.nodes.length) throw new Error("The pattern has more nodes than the host, so it can't fit.");
    return { L, R, sub };
  }
  const degSeq = g => g.nodes.map(g.deg).sort((a, b) => b - a);
  // Edges that break the mapping once u → v is added (u in L, v in R).
  function conflicts(I, map, u, v) {
    const bad = [];
    for (const [w, x] of Object.entries(map)) {
      const le = I.L.has(u, w), re = I.R.has(v, x);
      if (le && !re) bad.push(`${u}–${w} has no partner ${v}–${x}`);
      if (!I.sub && re && !le) bad.push(`${v}–${x} has no partner ${u}–${w}`);
    }
    return bad;
  }
  function giBacktrack(I) {
    const frames = [], push = f => { if (frames.length < SHOW) frames.push(f); };
    const { L, R, sub } = I;
    if (!sub) {
      const a = degSeq(L), b = degSeq(R);
      if (L.nodes.length !== R.nodes.length || L.edges.length !== R.edges.length || a.join() !== b.join()) {
        frames.push({ map: {}, done: true, ok: false, quick: true, caption: `Degree lists differ: G has [${a.join(", ")}], H has [${b.join(", ")}]. A mapping keeps every degree, so these can't be isomorphic. No search needed.` });
        return frames;
      }
      push({ map: {}, caption: `Degree lists match: [${a.join(", ")}]. That's necessary but not enough, so search for a mapping.` });
    } else push({ map: {}, caption: `Place each pattern node on a host node with at least as many neighbors, keeping every pattern edge.` });
    const order = L.nodes.slice().sort((a, b) => L.deg(b) - L.deg(a) || L.nodes.indexOf(a) - L.nodes.indexOf(b));
    const map = {}, used = new Set();
    let tries = 0;
    const fits = (u, v) => (sub ? R.deg(v) >= L.deg(u) : R.deg(v) === L.deg(u));
    function rec(i) {
      if (i === order.length) return true;
      const u = order[i];
      const free = R.nodes.filter(v => !used.has(v)), cands = free.filter(v => fits(u, v)), pruned = free.filter(v => !fits(u, v));
      push({ map: { ...map }, focus: u, pruned, caption: `Next: ${u}, degree ${L.deg(u)}. ${cands.length ? `Candidates: ${cands.join(", ")}` : "No candidate is left"}${pruned.length ? `. Ruled out by degree: ${pruned.join(", ")}` : ""}.` });
      for (const v of cands) {
        tries++;
        const bad = conflicts(I, map, u, v);
        if (bad.length) { push({ map: { ...map, [u]: v }, trying: [u, v], trial: true, caption: `Try ${u} → ${v}: ${bad[0]}${bad.length > 1 ? `, and ${bad.length - 1} more` : ""}. Reject.` }); continue; }
        map[u] = v; used.add(v);
        push({ map: { ...map }, trying: [u, v], caption: `Try ${u} → ${v}: every edge to the nodes placed so far has a partner. Keep it.` });
        if (rec(i + 1)) return true;
        delete map[u]; used.delete(v);
      }
      push({ map: { ...map }, failed: u, caption: `${u} has no candidate left. Undo the last choice.` });
      return false;
    }
    const ok = rec(0);
    const pairs = Object.entries(map).map(([a, b]) => `${a}→${b}`).join(", ");
    frames.push(ok ? { map: { ...map }, done: true, ok, caption: sub ? `Every pattern edge lands on a host edge: ${pairs}. The host contains the pattern. (${tries} tries.)` : `Every edge has a partner both ways: ${pairs}. G and H are isomorphic. (${tries} tries.)` }
      : { map: {}, done: true, ok, caption: sub ? `Every placement breaks a pattern edge somewhere. The host doesn't contain the pattern. (${tries} tries.)` : `Every mapping that keeps the degrees breaks an edge somewhere. G and H aren't isomorphic. (${tries} tries.)` });
    return frames;
  }
  function giBrute(I) {
    const frames = [], push = f => { if (frames.length < SHOW) frames.push(f); };
    const { L, R, sub } = I, n = L.nodes.length;
    if (!sub && (n !== R.nodes.length)) {
      frames.push({ map: {}, done: true, ok: false, quick: true, caption: `G has ${n} nodes and H has ${R.nodes.length}. A one-to-one mapping needs the same number, so no.` });
      return frames;
    }
    let tries = 0, found = null;
    const used = new Set(), cur = [];
    const broken = map => {
      let k = 0;
      L.edges.forEach(e => { if (!R.has(map[e.a], map[e.b])) k++; });
      if (!sub) R.edges.forEach(e => { const inv = Object.keys(map); const a = inv.find(x => map[x] === e.a), b = inv.find(x => map[x] === e.b); if (!L.has(a, b)) k++; });
      return k;
    };
    (function rec() {
      if (found || tries >= 40320) return;
      if (cur.length === n) {
        const map = Object.fromEntries(L.nodes.map((u, i) => [u, cur[i]]));
        tries++;
        const k = broken(map);
        push({ map, trial: k > 0, caption: k ? `Try ${L.nodes.map((u, i) => `${u}→${cur[i]}`).join(", ")}: ${k} edge${k > 1 ? "s" : ""} without a partner.` : `Try ${L.nodes.map((u, i) => `${u}→${cur[i]}`).join(", ")}: every edge has a partner.` });
        if (!k) found = map;
        return;
      }
      for (const v of R.nodes) { if (used.has(v)) continue; used.add(v); cur.push(v); rec(); cur.pop(); used.delete(v); if (found) return; }
    })();
    const hidden = tries > SHOW ? ` (${tries - SHOW} not shown)` : "";
    frames.push(found ? { map: found, done: true, ok: true, caption: `${sub ? "The host contains the pattern" : "G and H are isomorphic"}, found after trying ${tries} mappings${hidden}.` }
      : { map: {}, done: true, ok: false, caption: `Tried all ${tries} mappings${hidden}; every one breaks an edge. ${sub ? "The host doesn't contain the pattern." : "G and H aren't isomorphic."}` });
    return frames;
  }

  /* ---------- catalog ---------- */
  const SIPSER = "{(b,ca),(a,ab),(ca,a),(abc,c)}";
  const PROBLEMS = {
    PCP: {
      label: "Post Correspondence Problem", cls: "Undecidable", type: "Strings", vizType: "Dominoes", shape: "a sequence of tiles",
      def: "Given a list of dominoes, each with a string on top and a string on the bottom, is there a sequence of them (repeats allowed) whose top strings join into exactly the same string as their bottom strings? It is undecidable: no algorithm answers it for every input. (Not in Redux yet.)",
      grammar: "{T | T is a list of tiles (t,b), t and b non-empty strings, numbered 1, 2, … in the order listed}",
      input: "{(top,bottom),(top,bottom),…}", inputLong: "Tiles, each a top and a bottom string",
      certificate: "(i1,…,ik) | tile numbers, repeats allowed, top strings joined = bottom strings joined",
      solvers: [["bfs", "Breadth-first search over leftovers (not in Redux yet)"], ["ids", "Iterative deepening (not in Redux yet)"]],
      examples: [
        ["Sipser's example, match of 5", SIPSER],
        ["Match of 4", "{(a,baa),(ab,aa),(bba,bb)}"],
        ["Provably no match", "{(ab,aba),(bba,aa),(aba,bab)}"],
        ["Top always longer", "{(abc,ab),(ca,a),(acc,ba)}"],
        ["Hard: shortest match is 55 tiles", "{(b,baa),(a,ba),(baa,a)}"],
      ],
    },
    GRAPHISO: {
      label: "Graph Isomorphism", cls: "NP", type: "Graphs", vizType: "Two-Graph Mapping", shape: "a one-to-one mapping of nodes",
      def: "Given two graphs G and H, can H's nodes be renamed so it becomes exactly G: a one-to-one mapping that keeps every edge and every non-edge? It is in NP but is not known to be NP-complete, and not known to be in P. Babai's 2015 algorithm solves it in quasi-polynomial time. (Not in Redux yet.)",
      grammar: "{((N1,E1),(N2,E2)) | N1, N2 are sets, E1 subset N1 unorderedcross N1, E2 subset N2 unorderedcross N2}",
      input: "((N1,E1),(N2,E2))", inputLong: "Two graphs, G then H",
      certificate: "{(u,v),…} | one pair per node of G, u in N1, v in N2, each v used once, {u,w} in E1 exactly when {v,x} in E2",
      solvers: [["deg", "Backtracking with degree pruning (not in Redux yet)"], ["brute", "Try every mapping (not in Redux yet)"]],
      examples: [
        ["Same graph, renamed", "(({1,2,3,4,5},{{1,2},{2,3},{3,4},{4,1},{1,5},{2,5}}),({a,b,c,d,e},{{c,e},{e,a},{a,d},{d,c},{c,b},{e,b}}))"],
        ["Same degrees, not isomorphic", "(({1,2,3,4,5,6},{{1,2},{2,3},{3,4},{4,5},{5,6},{6,1}}),({a,b,c,d,e,f},{{a,b},{b,c},{c,a},{d,e},{e,f},{f,d}}))"],
        ["Different degrees", "(({1,2,3,4},{{1,2},{2,3},{3,4}}),({a,b,c,d},{{a,b},{a,c},{a,d}}))"],
      ],
    },
    SUBGRAPHISO: {
      label: "Subgraph Isomorphism", cls: "NP-Complete", type: "Graphs", vizType: "Two-Graph Mapping", shape: "a mapping of pattern nodes into the host",
      def: "Given a pattern graph H and a host graph G, does G contain a copy of H: a one-to-one placement of H's nodes on G's nodes so every edge of H lands on an edge of G? Extra host edges are allowed. It is NP-complete, since it includes Clique and Hamiltonian Cycle as special cases. (Not in Redux yet.)",
      grammar: "{((N_H,E_H),(N_G,E_G)) | the pattern H first, the host G second; E subset N unorderedcross N for each}",
      input: "((N_H,E_H),(N_G,E_G))", inputLong: "A pattern graph, then a host graph",
      certificate: "{(u,v),…} | one pair per pattern node, each host node used once, {u,w} in E_H implies {v,x} in E_G",
      solvers: [["deg", "Backtracking with degree pruning (not in Redux yet)"], ["brute", "Try every placement (not in Redux yet)"]],
      examples: [
        ["Find a triangle", "(({x,y,z},{{x,y},{y,z},{z,x}}),({1,2,3,4,5,6},{{1,2},{2,3},{3,4},{4,5},{5,6},{6,1},{2,5},{3,5}}))"],
        ["A 4-cycle in K(2,3)", "(({w,x,y,z},{{w,x},{x,y},{y,z},{z,w}}),({a,b,1,2,3},{{a,1},{a,2},{a,3},{b,1},{b,2},{b,3}}))"],
        ["A path of 4 in a star", "(({p,q,r,s},{{p,q},{q,r},{r,s}}),({hub,1,2,3,4},{{hub,1},{hub,2},{hub,3},{hub,4}}))"],
      ],
    },
  };
  function parse(str, k) { return k === "PCP" ? parsePCP(str) : parseGI(str, k === "SUBGRAPHISO"); }
  function solve(k, inst, solver) {
    if (k === "PCP") return solver === "ids" ? pcpIds(inst) : pcpBfs(inst);
    return solver === "brute" ? giBrute(inst) : giBacktrack(inst);
  }

  /* ---------- layout for one graph inside a panel (deterministic, as graphbase.js) ---------- */
  function layout(G, x0, y0, W, H) {
    const comps = components(G);
    if (comps.length > 1) {
      const cols = Math.ceil(Math.sqrt(comps.length)), rows = Math.ceil(comps.length / cols), out = new Map();
      comps.forEach((nodes, i) => {
        const sub = { nodes, edges: G.edges.filter(e => nodes.includes(e.a)) };
        const bw = W / cols, bh = H / rows, bx = x0 + (i % cols) * bw, by = y0 + Math.floor(i / cols) * bh;
        layoutOne(sub, bx, by, bw, bh).forEach((p, n) => out.set(n, p));
      });
      return out;
    }
    return layoutOne(G, x0, y0, W, H);
  }
  function components(G) {
    const seen = new Set(), out = [];
    for (const s of G.nodes) {
      if (seen.has(s)) continue;
      const comp = [], st = [s]; seen.add(s);
      while (st.length) { const u = st.pop(); comp.push(u); for (const v of G.adj.get(u)) if (!seen.has(v)) { seen.add(v); st.push(v); } }
      out.push(G.nodes.filter(n => comp.includes(n)));
    }
    return out;
  }
  function layoutOne(G, x0, y0, W, H) {
    const n = G.nodes.length, idx = new Map(G.nodes.map((x, i) => [x, i]));
    let pos = G.nodes.map((_, i) => ({ x: 100 * Math.cos((2 * Math.PI * i) / n - Math.PI / 2), y: 100 * Math.sin((2 * Math.PI * i) / n - Math.PI / 2) }));
    if (n > 2) {
      const k = Math.sqrt((W * H) / n) * 0.42;
      let t = 60;
      for (let it = 0; it < 450; it++) {
        const d = pos.map(() => ({ x: 0, y: 0 }));
        for (let i = 0; i < n; i++) for (let j = i + 1; j < n; j++) {
          const dx = pos[i].x - pos[j].x, dy = pos[i].y - pos[j].y, dist = Math.max(0.5, Math.hypot(dx, dy)), f = (k * k) / dist;
          d[i].x += (dx / dist) * f; d[i].y += (dy / dist) * f; d[j].x -= (dx / dist) * f; d[j].y -= (dy / dist) * f;
        }
        for (const e of G.edges) {
          const i = idx.get(e.a), j = idx.get(e.b);
          const dx = pos[i].x - pos[j].x, dy = pos[i].y - pos[j].y, dist = Math.max(0.5, Math.hypot(dx, dy)), f = (dist * dist) / k;
          d[i].x -= (dx / dist) * f; d[i].y -= (dy / dist) * f; d[j].x += (dx / dist) * f; d[j].y += (dy / dist) * f;
        }
        for (let i = 0; i < n; i++) {
          const len = Math.max(0.01, Math.hypot(d[i].x, d[i].y));
          pos[i].x += (d[i].x / len) * Math.min(len, t) - pos[i].x * 0.01;
          pos[i].y += (d[i].y / len) * Math.min(len, t) - pos[i].y * 0.01;
        }
        t = Math.max(0.5, t * 0.985);
      }
    }
    const xs = pos.map(p => p.x), ys = pos.map(p => p.y);
    const minX = Math.min(...xs), maxX = Math.max(...xs), minY = Math.min(...ys), maxY = Math.max(...ys);
    const pad = Math.min(44, W / 4, H / 4), sx = (W - 2 * pad) / Math.max(1e-6, maxX - minX), sy = (H - 2 * pad) / Math.max(1e-6, maxY - minY);
    const s = Math.min(sx, sy, 2.6);
    const ox = x0 + (W - (maxX - minX) * s) / 2, oy = y0 + (H - (maxY - minY) * s) / 2;
    return new Map(G.nodes.map((x, i) => [x, { x: ox + (pos[i].x - minX) * s, y: oy + (pos[i].y - minY) * s }]));
  }

  function create({ svg }) {
    if (!document.getElementById("pv-style")) {
      const st = document.createElement("style"); st.id = "pv-style"; st.textContent = css; document.head.appendChild(st);
    }
    svg.classList.add("pv-svg");
    let K, inst, frames, frame = null, hover = null, els;

    /* ----- Dominoes ----- */
    function drawPCP() {
      svg.innerHTML = "";
      const T = inst, W = 760;
      els = { tiles: [] };
      mk("text", { x: 24, y: 26, class: "pv-head" }, svg).textContent = "TILES";
      let x = 24, y = 44;
      T.forEach((tile, i) => {
        const w = Math.max(tile.t.length, tile.b.length) * 11 + 26;
        if (x + w > W - 24) { x = 24; y += 86; }
        const g = mk("g", { class: "pv-tile c" + (i % 6), tabindex: "0", role: "button", "aria-label": `Tile ${i + 1}: ${tile.t} over ${tile.b}` }, svg);
        mk("text", { x: x + w / 2, y: y + 6, class: "num" }, g).textContent = String(i + 1);
        mk("rect", { x, y: y + 16, width: w, height: 28, rx: 6, class: "half" }, g);
        mk("rect", { x, y: y + 44, width: w, height: 28, rx: 6, class: "half" }, g);
        mk("text", { x: x + w / 2, y: y + 30 }, g).textContent = tile.t;
        mk("text", { x: x + w / 2, y: y + 58 }, g).textContent = tile.b;
        const on = () => { hover = i; paint(); }, off = () => { hover = null; paint(); };
        g.addEventListener("pointerenter", on); g.addEventListener("pointerleave", off);
        g.addEventListener("focus", on); g.addEventListener("blur", off);
        els.tiles.push(g);
        x += w + 14;
      });
      els.alignY = y + 140;
      els.align = mk("g", {}, svg);
      svg.setAttribute("viewBox", `0 0 ${W} ${els.alignY + 150}`);
    }
    function paintPCP() {
      const T = inst, f = frame, seq = f ? f.seq : [];
      const lastTile = f && !f.done && seq.length ? seq[seq.length - 1] : null;
      els.tiles.forEach((g, i) => {
        let cls = "pv-tile c" + (i % 6);
        if (seq.includes(i)) cls += " used";
        if (i === lastTile) cls += " Active";
        if (hover !== null) cls += i === hover ? " trace" : " faint";
        g.setAttribute("class", cls);
      });
      const A = els.align; A.innerHTML = "";
      const y0 = els.alignY;
      mk("text", { x: 24, y: y0 - 42, class: "pv-head" }, A).textContent = "LAID OUT";
      if (!seq.length) {
        mk("text", { x: 24, y: y0 + 24, class: "pv-note" }, A).textContent = f && f.done ? (f.ok ? "" : "No sequence to show.") : "Tiles are placed here, left to right, as the search tries them.";
        return;
      }
      const s = build(T, seq), len = Math.max(s.top.length, s.bot.length), L = Math.min(s.top.length, s.bot.length);
      let mis = -1; for (let i = 0; i < L; i++) if (s.top[i] !== s.bot[i]) { mis = i; break; }
      const cw = Math.min(26, (760 - 120) / Math.max(len, 1)), x0 = 84, ch = 30, yTop = y0 + 8, yBot = y0 + 8 + ch + 6;
      mk("text", { x: 24, y: yTop + ch / 2, class: "pv-note", "dominant-baseline": "central" }, A).textContent = "top";
      mk("text", { x: 24, y: yBot + ch / 2, class: "pv-note", "dominant-baseline": "central" }, A).textContent = "bottom";
      const state = (i, row) => {
        const str = row === "top" ? s.top : s.bot, other = row === "top" ? s.bot : s.top;
        if (i >= str.length) return null;
        if (mis >= 0 && i === mis) return "Rejected";
        if (mis >= 0 && i > mis) return "";
        if (i >= other.length) return "Active";
        return f.done && f.ok ? "Solution" : "Covered";
      };
      for (const [row, yy, str] of [["top", yTop, s.top], ["bot", yBot, s.bot]]) {
        for (let i = 0; i < len; i++) {
          const st = state(i, row);
          if (st === null) {
            if (mis < 0) { const g = mk("g", { class: "pv-cell slot" }, A); mk("rect", { x: x0 + i * cw + 1, y: yy, width: cw - 2, height: ch, rx: 4 }, g); }
            continue;
          }
          const g = mk("g", { class: "pv-cell " + st }, A);
          mk("rect", { x: x0 + i * cw + 1, y: yy, width: cw - 2, height: ch, rx: 4 }, g);
          mk("text", { x: x0 + i * cw + cw / 2, y: yy + ch / 2 }, g).textContent = str[i];
        }
      }
      // tile spans: above the top row and below the bottom row, in each tile's color
      for (const [row, yy, part] of [["top", yTop - 10, "t"], ["bot", yBot + ch + 10, "b"]]) {
        let p = 0;
        seq.forEach((ti, k) => {
          const w = T[ti][part].length;
          const g = mk("g", { class: "pv-span c" + (ti % 6) }, A);
          mk("line", { x1: x0 + p * cw + 4, y1: yy, x2: x0 + (p + w) * cw - 4, y2: yy }, g);
          mk("text", { x: x0 + (p + w / 2) * cw, y: yy + (row === "top" ? -10 : 10) }, g).textContent = String(ti + 1);
          if (k > 0) mk("line", { x1: x0 + p * cw, y1: row === "top" ? yTop : yBot, x2: x0 + p * cw, y2: (row === "top" ? yTop : yBot) + ch, class: "pv-tick" }, A);
          p += w;
        });
      }
      let msg;
      if (mis >= 0) msg = `Letter ${mis + 1} disagrees: "${s.top[mis]}" on top, "${s.bot[mis]}" underneath.`;
      else if (s.top.length === s.bot.length) msg = `Top and bottom both read "${s.top}".`;
      else msg = `The ${s.top.length > s.bot.length ? "top" : "bottom"} runs ahead by "${(s.top.length > s.bot.length ? s.top : s.bot).slice(L)}" (amber).`;
      mk("text", { x: 24, y: yBot + ch + 48, class: "pv-note" }, A).textContent = msg;
    }

    /* ----- Two-Graph Mapping ----- */
    function drawGI() {
      svg.innerHTML = "";
      const W = 760, H = Math.max(inst.L.nodes.length, inst.R.nodes.length) > 6 ? 430 : 380, half = 372;
      svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
      const names = inst.sub ? ["PATTERN H", "HOST G"] : ["GRAPH G", "GRAPH H"];
      mk("text", { x: 24, y: 24, class: "pv-head" }, svg).textContent = names[0];
      mk("text", { x: half + 32, y: 24, class: "pv-head" }, svg).textContent = names[1];
      mk("line", { x1: half + 8, y1: 40, x2: half + 8, y2: H - 16, class: "pv-panel" }, svg);
      const PL = layout(inst.L, 8, 36, half - 8, H - 44), PR = layout(inst.R, half + 16, 36, half - 8, H - 44);
      const gE = mk("g", {}, svg), gPh = mk("g", {}, svg), gLk = mk("g", {}, svg), gN = mk("g", {}, svg);
      els = { PL, PR, gPh, gLk, edges: [], nodes: new Map() };
      for (const [side, G, P] of [["L", inst.L, PL], ["R", inst.R, PR]]) {
        G.edges.forEach(e => {
          const a = P.get(e.a), b = P.get(e.b);
          els.edges.push({ side, e, el: mk("line", { x1: a.x, y1: a.y, x2: b.x, y2: b.y, class: "pv-edge" }, gE) });
        });
        G.nodes.forEach(n => {
          const p = P.get(n);
          const g = mk("g", { class: "pv-node", tabindex: "0", role: "button", "aria-label": `${side === "L" ? names[0] : names[1]} node ${n}` }, gN);
          mk("circle", { cx: p.x, cy: p.y, r: 17, class: "body" }, g);
          mk("text", { x: p.x, y: p.y, class: "lbl" }, g).textContent = n;
          mk("text", { x: p.x, y: p.y + 28, class: "deg" }, g).textContent = "d" + G.deg(n);
          const tag = mk("text", { x: p.x, y: p.y - 27, class: "tag" }, g);
          const id = side + SEP + n;
          const on = () => { hover = id; paint(); }, off = () => { hover = null; paint(); };
          g.addEventListener("pointerenter", on); g.addEventListener("pointerleave", off);
          g.addEventListener("focus", on); g.addEventListener("blur", off);
          els.nodes.set(id, { g, tag, side, n });
        });
      }
    }
    function evalGI(f) {
      const out = { nodes: new Map(), edges: new Map(), phantom: [], checks: [] };
      const { L, R, sub } = inst;
      const map = f ? f.map : {};
      const inv = {}; Object.entries(map).forEach(([u, v]) => (inv[v] = u));
      const color = new Map(L.nodes.map((u, i) => [u, i % 6]));
      Object.entries(map).forEach(([u, v]) => { out.nodes.set("L" + SEP + u, { c: color.get(u), tag: "→" + v }); out.nodes.set("R" + SEP + v, { c: color.get(u), tag: "←" + u }); });
      if (f && f.trying) { const [u, v] = f.trying; out.nodes.get("L" + SEP + u).ring = true; out.nodes.get("R" + SEP + v).ring = true; }
      if (f && f.focus) out.nodes.set("L" + SEP + f.focus, { ...(out.nodes.get("L" + SEP + f.focus) || {}), ring: true });
      if (f && f.failed) out.nodes.set("L" + SEP + f.failed, { st: "Rejected" });
      if (f && f.pruned) f.pruned.forEach(v => out.nodes.set("R" + SEP + v, { st: "Blocked" }));
      let kept = 0, lost = 0, extra = 0;
      L.edges.forEach(e => {
        if (map[e.a] === undefined || map[e.b] === undefined) return;
        if (R.has(map[e.a], map[e.b])) { kept++; out.edges.set("L" + e.k, "sol"); out.edges.set("R" + key(map[e.a], map[e.b]), "sol"); }
        else { lost++; out.edges.set("L" + e.k, "rej"); out.phantom.push(["R", map[e.a], map[e.b]]); }
      });
      R.edges.forEach(e => {
        if (inv[e.a] === undefined || inv[e.b] === undefined || out.edges.get("R" + e.k) === "sol") return;
        if (sub) out.edges.set("R" + e.k, "extra");
        else { extra++; out.edges.set("R" + e.k, "rej"); out.phantom.push(["L", inv[e.a], inv[e.b]]); }
      });
      const done = f && f.done, nMap = Object.keys(map).length;
      if (!sub) {
        const same = degSeq(L).join() === degSeq(R).join() && L.nodes.length === R.nodes.length;
        out.checks.push({ label: `Degree lists ${same ? "match" : "differ"}`, ok: f ? same : null });
        out.checks.push({ label: `Nodes mapped: ${nMap} / ${L.nodes.length}`, ok: done ? nMap === L.nodes.length : null });
        out.checks.push({ label: `G's edges with a partner in H: ${kept} / ${L.edges.length}`, ok: done ? kept === L.edges.length : lost ? false : null });
        out.checks.push({ label: `H's edges with no partner in G: ${extra}`, ok: done ? extra === 0 && nMap === L.nodes.length : extra ? false : null });
      } else {
        out.checks.push({ label: `Pattern nodes placed: ${nMap} / ${L.nodes.length}`, ok: done ? nMap === L.nodes.length : null });
        out.checks.push({ label: `Pattern edges found in the host: ${kept} / ${L.edges.length}`, ok: done ? kept === L.edges.length : lost ? false : null });
      }
      if (done && !f.ok) out.checks = out.checks.map(c => ({ ...c, ok: c.label.startsWith("Degree lists") ? c.ok : false }));
      return out;
    }
    function paintGI() {
      const ev = evalGI(frame);
      const hv = hover ? els.nodes.get(hover) : null;
      const fmap = frame ? frame.map : {}, finv = {}; Object.entries(fmap).forEach(([u, v]) => (finv[v] = u));
      const pn = hv ? (hv.side === "L" ? fmap[hv.n] : finv[hv.n]) : undefined;
      const partner = pn !== undefined ? [(hv.side === "L" ? "R" : "L") + SEP + pn, els.nodes.get((hv.side === "L" ? "R" : "L") + SEP + pn)] : null;
      const nb = hv ? (hv.side === "L" ? inst.L : inst.R).adj.get(hv.n) : null;
      for (const [id, o] of els.nodes) {
        const s = ev.nodes.get(id) || {};
        let cls = "pv-node";
        if (s.st) cls += " " + s.st;
        if (s.c !== undefined && !s.st) cls += " c" + s.c;
        if (s.ring) cls += " ring";
        if (hv) cls += id === hover || (partner && id === partner[0]) ? " trace" : o.side === hv.side && nb.has(o.n) ? "" : " faint";
        o.g.setAttribute("class", cls);
        o.tag.textContent = s.tag || "";
      }
      els.edges.forEach(o => {
        const st = ev.edges.get(o.side + o.e.k) || "";
        let cls = "pv-edge" + (st ? " " + st : "");
        if (hv) cls = "pv-edge " + (o.side === hv.side && (o.e.a === hv.n || o.e.b === hv.n) ? "trace" : "faint");
        o.el.setAttribute("class", cls);
      });
      els.gPh.innerHTML = ""; els.gLk.innerHTML = "";
      if (!hv) ev.phantom.forEach(([side, a, b]) => {
        const P = side === "L" ? els.PL : els.PR, p = P.get(a), q = P.get(b);
        mk("line", { x1: p.x, y1: p.y, x2: q.x, y2: q.y, class: "pv-phantom" }, els.gPh);
      });
      const link = (u, v, cls) => {
        const p = els.PL.get(u), q = els.PR.get(v);
        if (!p || !q) return;
        const mx = (p.x + q.x) / 2;
        mk("path", { d: `M${p.x + 17} ${p.y} C${mx} ${p.y} ${mx} ${q.y} ${q.x - 17} ${q.y}`, class: cls }, els.gLk);
      };
      if (frame && frame.trying && !hv) link(frame.trying[0], frame.trying[1], "pv-link hot");
      if (hv && partner) { const a = hv.side === "L" ? hv.n : partner[1].n, b = hv.side === "L" ? partner[1].n : hv.n; link(a, b, "pv-link"); }
    }

    function draw() { if (K === "PCP") drawPCP(); else drawGI(); }
    function paint() { if (K === "PCP") paintPCP(); else paintGI(); }
    function checksPCP(f) {
      if (!f) return [{ label: `Tiles: ${inst.length}`, ok: null }];
      const seq = f.seq || [];
      if (f.done) {
        if (f.ok) { const s = build(inst, seq); return [{ label: `Tiles used: ${seq.length}`, ok: true }, { label: `Top and bottom both read "${s.top}"`, ok: true }]; }
        return [{ label: f.verdict === "noMatch" ? "No match exists (search finished)" : "No match found within the limit", ok: false }, { label: f.verdict === "noMatch" ? "Settled for this instance" : "Not settled: PCP is undecidable", ok: f.verdict === "noMatch" ? true : null }];
      }
      if (!seq.length) return [{ label: "Tiles placed: 0", ok: null }];
      const s = build(inst, seq), L = Math.min(s.top.length, s.bot.length);
      let mis = -1; for (let i = 0; i < L; i++) if (s.top[i] !== s.bot[i]) { mis = i; break; }
      return [{ label: `Tiles placed: ${seq.length}`, ok: null },
        { label: mis >= 0 ? `Mismatch at letter ${mis + 1}` : `Letters lined up: ${L}`, ok: mis >= 0 ? false : null },
        { label: mis >= 0 ? "Leftover: none (dead end)" : `Leftover: ${Math.abs(s.top.length - s.bot.length)} letter${Math.abs(s.top.length - s.bot.length) === 1 ? "" : "s"}`, ok: mis < 0 && s.top.length === s.bot.length ? true : null }];
    }
    return {
      load(str, k, solver) {
        const parsed = parse(str, k);
        K = k; inst = parsed;
        frames = solve(k, inst, solver || PROBLEMS[k].solvers[0][0]);
        frames.unshift(k === "PCP" ? { seq: [], caption: "Step 0: no tiles placed yet. Step forward to watch the search." } : { map: {}, caption: "Step 0: nothing mapped yet. Step forward to watch the search." });
        frame = null; hover = null;
        draw(); paint();
        const last = frames[frames.length - 1];
        return { frames, ok: last.ok };
      },
      show(i) { frame = i === null ? null : frames[i]; paint(); return frame; },
      checks(i) { const f = i === null ? null : frames[i]; return K === "PCP" ? checksPCP(f) : evalGI(f).checks; },
      chosen(i) {
        const f = i === null ? null : frames[i];
        if (!f) return [];
        if (K === "PCP") {
          if (!f.seq || !f.seq.length) return [];
          const chips = f.seq.map(t => ({ text: "tile " + (t + 1), color: t % 6 }));
          if (f.done && f.ok) chips.push({ text: `"${build(inst, f.seq).top}"` });
          return chips;
        }
        return inst.L.nodes.filter(u => f.map[u] !== undefined).map(u => ({ text: `${u}→${f.map[u]}`, color: inst.L.nodes.indexOf(u) % 6 }));
      },
      shape(k) { return PROBLEMS[k].shape; },
      resetHover() { hover = null; paint(); },
    };
  }

  return { create, parse, PROBLEMS, _solve: solve, _extend: extend };
})();
