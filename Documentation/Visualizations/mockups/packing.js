/* ---- Packing view: Partition, Subset Sum, Subset Product, Knapsack, Bin Packing ----
   Items sit in a pool at the top; containers below are drawn to scale with their target or capacity line.
   Every solver emits frames in the problem's own terms: which container each item is in (assign[i], -1 = none),
   the item being tried, and the item that didn't fit. The view derives every color from that. */
const PackingView = (() => {
  const NS = "http://www.w3.org/2000/svg";
  const mk = (tag, attrs = {}, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; };
  const SHOW = 150;
  const W = 680, X0 = 128, TW = 400;

  const css = `
.pk-svg { width: 100%; height: auto; display: block; }
.pk-svg text { font-family: var(--av-mono); }
.pk-head { font-size: 11px; font-weight: 600; letter-spacing: .08em; fill: var(--av-muted); }
.pk-tile { cursor: pointer; transition: opacity .2s; }
.pk-tile:focus { outline: none; }
.pk-tile rect { fill: var(--av-bg); stroke: var(--av-stroke); stroke-width: 1.5; transition: fill .2s, stroke .2s; }
.pk-tile text { font-size: 12.5px; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); pointer-events: none; }
.pk-tile .sub { font-size: 10.5px; fill: var(--av-muted); }
.pk-tile.Solution rect { fill: var(--av-sol); stroke: var(--av-sol); } .pk-tile.Solution text { fill: var(--av-on-sol); font-weight: 600; }
.pk-tile.Active rect { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; }
.pk-tile.Rejected rect { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 3; } .pk-tile.Rejected text { fill: var(--av-rej); }
.pk-tile.Unused rect { fill: transparent; stroke: var(--av-stroke); stroke-dasharray: 4 3; } .pk-tile.Unused text { fill: var(--av-muted); }
.pk-tile.trace rect, .pk-tile:focus-visible rect { stroke: var(--av-hot); stroke-width: 3; }
.pk-tile.faint, .pk-seg.faint { filter: grayscale(1); }
.pk-row-label { font-size: 13px; font-weight: 600; fill: var(--av-ink); }
.pk-row-sub { font-size: 11px; fill: var(--av-muted); }
.pk-track { fill: var(--av-surface); stroke: var(--av-stroke); stroke-width: 1.2; }
.pk-seg rect { stroke: var(--av-surface); stroke-width: 1.5; transition: fill .2s; }
.pk-seg text { font-size: 11.5px; text-anchor: middle; dominant-baseline: central; pointer-events: none; fill: var(--av-on-sol); font-weight: 600; }
.pk-seg.Solution rect { fill: var(--av-sol); }
.pk-seg.Active rect { fill: var(--av-hl); } .pk-seg.Active text { fill: var(--av-on-hl); }
.pk-seg.Rejected rect { fill: var(--av-rej); }
.pk-seg.trace rect { stroke: var(--av-hot); stroke-width: 3; }
.pk-tile.g0 rect, .pk-seg.g0 rect { fill: var(--av-g0); stroke: var(--av-g0s); } .pk-tile.g1 rect, .pk-seg.g1 rect { fill: var(--av-g1); stroke: var(--av-g1s); }
.pk-tile.g2 rect, .pk-seg.g2 rect { fill: var(--av-g2); stroke: var(--av-g2s); } .pk-tile.g3 rect, .pk-seg.g3 rect { fill: var(--av-g3); stroke: var(--av-g3s); }
.pk-tile.g4 rect, .pk-seg.g4 rect { fill: var(--av-g4); stroke: var(--av-g4s); } .pk-tile.g5 rect, .pk-seg.g5 rect { fill: var(--av-g5); stroke: var(--av-g5s); }
.pk-tile[class*=" g"] text, .pk-seg[class*=" g"] text { fill: #10131a; font-weight: 600; }
.pk-tile.g0.Active rect, .pk-tile.g1.Active rect, .pk-tile.g2.Active rect, .pk-tile.g3.Active rect, .pk-tile.g4.Active rect, .pk-tile.g5.Active rect,
.pk-seg.g0.Active rect, .pk-seg.g1.Active rect, .pk-seg.g2.Active rect, .pk-seg.g3.Active rect, .pk-seg.g4.Active rect, .pk-seg.g5.Active rect { stroke: var(--av-hl); stroke-width: 3; }
.pk-seg.over rect { fill: var(--av-rej); stroke: var(--av-rej); } .pk-seg.over text { fill: var(--av-on-rej); }
.pk-line { stroke: var(--av-ink); stroke-width: 1.6; stroke-dasharray: 5 4; filter: drop-shadow(0 0 1.2px var(--av-surface)) drop-shadow(0 0 1.2px var(--av-surface)); }
.pk-line-label { font-size: 11px; fill: var(--av-ink); text-anchor: middle; }
.pk-read { font-size: 13px; font-weight: 600; fill: var(--av-ink); }
.pk-read.ok { fill: var(--av-sol); } .pk-read.bad { fill: var(--av-rej); }
.pk-note { font-size: 11px; fill: var(--av-muted); }
.pk-beam { stroke: var(--av-ink); stroke-width: 3; stroke-linecap: round; filter: drop-shadow(0 0 1.2px var(--av-surface)) drop-shadow(0 0 1.2px var(--av-surface)); transition: transform .35s; }
.pk-fulcrum { fill: var(--av-stroke); }
@media (prefers-reduced-motion: reduce) { .pk-tile, .pk-tile rect, .pk-seg rect, .pk-beam { transition: none; } }`;

  /* ---------- parsing ---------- */
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
  const int = (x, what) => {
    if (typeof x !== "string" || !/^-?\d+$/.test(x)) throw new Error(`${what} must be a whole number.`);
    return Number(x);
  };
  const numbers = (node, what) => {
    if (!node || typeof node === "string") throw new Error(`${what} must be a list of numbers in braces.`);
    return node.items.map(x => int(x, "Every item"));
  };

  function parse(str, P) {
    const top = tree(tokenize(str));
    let items, I = { rule: P.rule };
    if (P.rule === "partition") {
      if (top.t !== "set") throw new Error("Expected a set of numbers, like {1,7,12}.");
      items = numbers(top, "S");
    } else if (P.rule === "sum" || P.rule === "product") {
      if (top.t !== "tup" || top.items.length !== 2) throw new Error("Expected ({S},T), like ({1,7,12},19).");
      items = numbers(top.items[0], "S");
      I.T = int(top.items[1], "T");
    } else if (P.rule === "knapsack") {
      if (top.t !== "tup" || top.items.length !== 3) throw new Error("Expected ({(w,v),…},W,V).");
      const raw = top.items[0];
      if (!raw || typeof raw === "string") throw new Error("Items must look like {(10,60),(20,100)}.");
      I.pairs = raw.items.map(p => {
        if (!p || p.t !== "tup" || p.items.length !== 2) throw new Error("Each item must be (weight,value).");
        return [int(p.items[0], "A weight"), int(p.items[1], "A value")];
      });
      items = I.pairs.map(p => p[0]);
      I.W = int(top.items[1], "W"); I.V = int(top.items[2], "V");
    } else if (P.rule === "binpacking") {
      if (top.t !== "tup" || top.items.length !== 3) throw new Error("Expected ((sizes),C,K), like ((4,7,3),10,2).");
      items = numbers(top.items[0], "S");
      I.C = int(top.items[1], "C"); I.K = int(top.items[2], "K");
      if (I.K < 1) throw new Error("K must be at least 1.");
      if (I.K > 6) throw new Error("This mockup draws up to 6 bins.");
    }
    if (!items.length) throw new Error("There must be at least one item.");
    if (items.length > 16) throw new Error("This mockup draws up to 16 items.");
    if (items.some(x => x <= 0)) throw new Error("Every number must be positive.");
    I.items = items.map((w, i) => ({ i, w, v: I.pairs ? I.pairs[i][1] : null, label: I.pairs ? `${w}` : `${w}` }));
    I.n = items.length;
    return I;
  }

  /* ---------- solvers: frames say which container each item sits in ---------- */
  const fmt = a => "{" + a.join(",") + "}";
  const none = n => Array(n).fill(-1);
  const subsetOf = (I, assign) => I.items.filter((_, i) => assign[i] === 0).map(t => t.w);
  const sumOf = a => a.reduce((s, x) => s + x, 0);
  const prodOf = a => a.reduce((s, x) => s * x, 1);
  const hidden = tried => (tried > SHOW ? ` (${tried - SHOW} tries not shown)` : "");

  // Partition Brute Force: Redux starts its counter at the second item alone and counts upward, wrapping round to "first item alone" last.
  function partitionBrute(I) {
    const n = I.n, frames = [], half = sumOf(I.items.map(t => t.w)) / 2;
    let tried = 0, found = null;
    for (let c = 0; c < 2 ** n; c++) {
      const mask = (2 + c) % (2 ** n);
      const assign = I.items.map((_, i) => (mask & (1 << i) ? 0 : 1));
      const a = sumOf(I.items.filter((_, i) => assign[i] === 0).map(t => t.w)), b = sumOf(I.items.filter((_, i) => assign[i] === 1).map(t => t.w));
      const ok = a === b;
      tried++;
      if (tried <= SHOW) frames.push({ assign, trial: !ok, caption: ok ? `Try S1 = ${fmt(subsetOf(I, assign))}: ${a} against ${b}. Equal.` : `Try S1 = ${fmt(subsetOf(I, assign))}: ${a} against ${b}, off by ${Math.abs(a - b)}.` });
      if (ok) { found = assign; break; }
    }
    frames.push(found
      ? { assign: found, done: true, ok: true, caption: `Both sides sum to ${half}. Found after ${tried} tries${hidden(tried)}.` }
      : { assign: none(n), done: true, ok: false, caption: Number.isInteger(half) ? `Checked all ${tried} splits${hidden(tried)}. None balances.` : `The total is odd (${half * 2}), so no split can balance. Checked all ${tried} splits${hidden(tried)}.` });
    return frames;
  }
  // Largest-first greedy: each item goes on the lighter side.
  function partitionGreedy(I) {
    const frames = [], assign = none(I.n), side = [0, 0];
    const order = I.items.slice().sort((a, b) => b.w - a.w || a.i - b.i);
    frames.push({ assign: assign.slice(), caption: `Sort largest first: ${order.map(t => t.w).join(", ")}.` });
    for (const t of order) {
      const s = side[0] <= side[1] ? 0 : 1;
      assign[t.i] = s; side[s] += t.w;
      frames.push({ assign: assign.slice(), trying: t.i, caption: `${t.w} goes on the lighter side, S${s + 1}. Now ${side[0]} against ${side[1]}.` });
    }
    const ok = side[0] === side[1];
    frames.push({ assign, done: true, ok, caption: ok ? `Balanced at ${side[0]} each.` : `Ends ${side[0]} against ${side[1]}. Greedy can miss an equal split, so try brute force.` });
    return frames;
  }
  // Subset Sum / Subset Product Brute Force: masks 1, 2, 3, … where bit i picks item i.
  function subsetBrute(I, product) {
    const n = I.n, frames = [], T = I.T;
    let tried = 0, found = null;
    for (let mask = 1; mask < 2 ** n; mask++) {
      const assign = I.items.map((_, i) => (mask & (1 << i) ? 0 : -1));
      const pick = subsetOf(I, assign), got = product ? prodOf(pick) : sumOf(pick), ok = got === T;
      tried++;
      if (tried <= SHOW) frames.push({ assign, trial: !ok, caption: `Try ${fmt(pick)}: ${product ? pick.join(" × ") : pick.join(" + ")} = ${got}${ok ? `. That's T.` : `, not ${T}.`}` });
      if (ok) { found = assign; break; }
    }
    frames.push(found
      ? { assign: found, done: true, ok: true, caption: `${fmt(subsetOf(I, found))} ${product ? "multiplies" : "adds up"} to exactly ${T}, found after ${tried} tries${hidden(tried)}.` }
      : { assign: none(n), done: true, ok: false, caption: `Checked all ${tried} subsets${hidden(tried)}. None ${product ? "multiplies" : "adds up"} to ${T}.` });
    return frames;
  }
  // Fast Approximation Algorithm (Ibarra and Kim): keep a trimmed list of reachable sums, 5% tolerance, as Redux does.
  function subsetApprox(I) {
    const frames = [], n = I.n, T = I.T, delta = 0.05 / (2 * n);
    if (T <= 0) { frames.push({ assign: none(n), done: true, ok: false, caption: "T isn't positive, so the approximation returns nothing." }); return frames; }
    let L = [{ sum: 0, idx: [] }];
    for (const t of I.items) {
      const merged = L.slice();
      for (const s of L) if (s.sum + t.w <= T) merged.push({ sum: s.sum + t.w, idx: [...s.idx, t.i] });
      merged.sort((a, b) => a.sum - b.sum);
      const kept = [merged[0]];
      let last = merged[0].sum;
      for (let k = 1; k < merged.length; k++) if (merged[k].sum > last * (1 + delta)) { kept.push(merged[k]); last = merged[k].sum; }
      const dropped = merged.length - kept.length;
      L = kept;
      const best = L[L.length - 1];
      const assign = I.items.map((_, i) => (best.idx.includes(i) ? 0 : -1));
      frames.push({ assign, trying: t.i, caption: `Add ${t.w} to every sum kept so far. ${L.length} sums kept${dropped ? `, ${dropped} trimmed as too close to another` : ""}. Best so far: ${best.sum}.` });
    }
    const best = L[L.length - 1], ok = best.sum === T;
    frames.push({ assign: I.items.map((_, i) => (ok && best.idx.includes(i) ? 0 : -1)), done: true, ok,
      caption: ok ? `The best sum kept is exactly ${T}.` : `The best sum kept is ${best.sum}, not ${T}, so the approximation answers "no", even if an exact subset exists.` });
    return frames;
  }
  // Knapsack Brute Force: i = 0, 1, 2, … where bit k picks item k; the first set within W reaching V wins.
  function knapsackBrute(I) {
    const n = I.n, frames = [];
    let tried = 0, found = null;
    for (let m = 0; m < 2 ** n; m++) {
      const assign = I.items.map((_, k) => (m & (1 << k) ? 0 : -1));
      const w = sumOf(I.items.filter((_, k) => assign[k] === 0).map(t => t.w)), v = sumOf(I.items.filter((_, k) => assign[k] === 0).map(t => t.v));
      const ok = w <= I.W && v >= I.V;
      tried++;
      const why = w > I.W ? `weight ${w} is over ${I.W}` : v < I.V ? `value ${v} is short of ${I.V}` : "";
      if (tried <= SHOW) frames.push({ assign, trial: !ok, caption: ok ? `Try ${pairsText(I, assign)}: weight ${w}, value ${v}. It works.` : `Try ${pairsText(I, assign)}: ${why}.` });
      if (ok) { found = assign; break; }
    }
    frames.push(found
      ? { assign: found, done: true, ok: true, caption: `${pairsText(I, found)} fits in ${I.W} and is worth at least ${I.V}. Found after ${tried} tries${hidden(tried)}.` }
      : { assign: none(n), done: true, ok: false, caption: `Checked all ${tried} sets${hidden(tried)}. None is worth ${I.V} within weight ${I.W}.` });
    return frames;
  }
  const pairsText = (I, assign) => { const p = I.items.filter((_, k) => assign[k] === 0).map(t => `(${t.w},${t.v})`); return p.length ? "{" + p.join(",") + "}" : "{}"; };
  // Knapsack Dynamic Programming: one row of the table per item; the frame shows the best set within W so far.
  function knapsackDP(I) {
    const n = I.n, C = I.W, frames = [];
    const dp = Array.from({ length: n + 1 }, () => Array(C + 1).fill(0));
    for (let i = 1; i <= n; i++) {
      const { w, v } = I.items[i - 1];
      for (let c = 0; c <= C; c++) dp[i][c] = w <= c ? Math.max(dp[i - 1][c], dp[i - 1][c - w] + v) : dp[i - 1][c];
    }
    const trace = rows => { const a = none(n); let c = C; for (let i = rows; i > 0 && c > 0; i--) if (dp[i][c] !== dp[i - 1][c]) { a[i - 1] = 0; c -= I.items[i - 1].w; } return a; };
    for (let i = 1; i <= n; i++) {
      const t = I.items[i - 1], took = dp[i][C] !== dp[i - 1][C];
      frames.push({ assign: trace(i), trying: i - 1,
        caption: `Row ${i} of the table, item (${t.w},${t.v}): ${took ? "taking it raises" : "it doesn't raise"} the best value within ${C} ${took ? `to ${dp[i][C]}` : `above ${dp[i][C]}`}.` });
    }
    const ok = dp[n][C] >= I.V;
    frames.push({ assign: ok ? trace(n) : none(n), done: true, ok,
      caption: ok ? `The table's best value within ${C} is ${dp[n][C]}, at least ${I.V}. Trace back for the items.` : `The table's best value within ${C} is ${dp[n][C]}, short of ${I.V}. No.` });
    return frames;
  }
  // Bin Packing First Fit Decreasing.
  function binFFD(I) {
    const frames = [], n = I.n, C = I.C, K = I.K, assign = none(n), sums = [];
    const order = I.items.slice().sort((a, b) => b.w - a.w || a.i - b.i);
    const big = order.find(t => t.w > C);
    if (big) { frames.push({ assign, bad: big.i, done: true, ok: false, caption: `${big.w} is bigger than a whole bin (${C}), so nothing can work.` }); return frames; }
    frames.push({ assign: assign.slice(), caption: `Sort largest first: ${order.map(t => t.w).join(", ")}.` });
    for (const t of order) {
      const b = sums.findIndex(s => s + t.w <= C);
      if (b >= 0) {
        assign[t.i] = b; sums[b] += t.w;
        frames.push({ assign: assign.slice(), trying: t.i, caption: b === 0 ? `${t.w} fits in bin 1.` : `${t.w} doesn't fit bins 1 to ${b}, so it goes in bin ${b + 1}.` });
      } else if (sums.length + 1 > K) {
        frames.push({ assign: assign.slice(), trying: t.i, bad: t.i, caption: `No open bin has room for ${t.w}, and opening another would need ${sums.length + 1} bins, more than K = ${K}.` });
        frames.push({ assign: assign.slice(), bad: t.i, done: true, ok: false, caption: `First Fit Decreasing needs more than ${K} bins. It's a heuristic, so brute force may still find a packing.` });
        return frames;
      } else {
        sums.push(t.w); assign[t.i] = sums.length - 1;
        frames.push({ assign: assign.slice(), trying: t.i, caption: `No open bin has room for ${t.w}, so open bin ${sums.length}.` });
      }
    }
    frames.push({ assign, done: true, ok: true, caption: `Everything is packed into ${sums.length} bin${sums.length > 1 ? "s" : ""}, within K = ${K}.` });
    return frames;
  }
  // Bin Packing Brute Force: place items in order, try each bin, back up when nothing fits.
  // Like Redux, it never tries a second empty bin for the same item (they're all alike).
  function binBrute(I) {
    const frames = [], n = I.n, C = I.C, K = I.K, assign = none(n), sums = Array(K).fill(0);
    const big = I.items.find(t => t.w > C);
    if (big) { frames.push({ assign, bad: big.i, done: true, ok: false, caption: `${big.w} is bigger than a whole bin (${C}), so nothing can work.` }); return frames; }
    let count = 0;
    const push = f => { if (++count <= SHOW) frames.push(f); };
    function place(i) {
      if (i === n) return true;
      const t = I.items[i];
      for (let b = 0; b < K; b++) {
        if (sums[b] + t.w <= C) {
          sums[b] += t.w; assign[i] = b;
          push({ assign: assign.slice(), trying: i, caption: `Put ${t.w} in bin ${b + 1} (now ${sums[b]} of ${C}).` });
          if (place(i + 1)) return true;
          sums[b] -= t.w; assign[i] = -1;
        }
        if (sums[b] === 0) break;
      }
      push({ assign: assign.slice(), bad: i, caption: `No bin has room for ${t.w}. Take back the last choice and try the next bin.` });
      return false;
    }
    const ok = place(0);
    frames.push(ok
      ? { assign: assign.slice(), done: true, ok, caption: `All items fit in ${new Set(assign).size} bin${new Set(assign).size > 1 ? "s" : ""} of ${C}${hidden(count)}.` }
      : { assign: none(n), done: true, ok, caption: `Every arrangement overflows. ${n} items don't fit in ${K} bins of ${C}${hidden(count)}.` });
    return frames;
  }

  /* ---------- rows: what each problem draws below the pool ---------- */
  function rowsFor(I) {
    const total = sumOf(I.items.map(t => t.w));
    if (I.rule === "partition") return [
      { label: "S1", sub: `needs ${total / 2}`, src: 0, measure: "w", target: total / 2, color: 0 },
      { label: "S2", sub: `needs ${total / 2}`, src: 1, measure: "w", target: total / 2, color: 1 }];
    if (I.rule === "sum") return [{ label: "Chosen", sub: `target ${I.T}`, src: 0, measure: "w", target: I.T }];
    if (I.rule === "product") return [{ label: "Chosen", sub: `target ${I.T}`, src: 0, measure: "w", target: I.T, log: true }];
    if (I.rule === "knapsack") return [
      { label: "Weight", sub: `at most ${I.W}`, src: 0, measure: "w", cap: I.W },
      { label: "Value", sub: `at least ${I.V}`, src: 0, measure: "v", atLeast: I.V }];
    return Array.from({ length: I.K }, (_, b) => ({ label: `Bin ${b + 1}`, sub: `holds ${I.C}`, src: b, measure: "w", cap: I.C, color: b }));
  }

  function create({ svg }) {
    if (!document.getElementById("pk-style")) {
      const st = document.createElement("style"); st.id = "pk-style"; st.textContent = css; document.head.appendChild(st);
    }
    svg.classList.add("pk-svg");
    let I, P, frames, rows, frame = null, hover = null, els, scales;

    const len = (row, x) => (row.log ? Math.log(Math.max(1, x)) : x);
    function rowTotal(row, assign) {
      const vals = I.items.filter((_, k) => assign && assign[k] === row.src).map(t => (row.measure === "v" ? t.v : t.w));
      return row.log ? (vals.length ? prodOf(vals) : 0) : sumOf(vals);
    }

    function draw() {
      svg.innerHTML = "";
      rows = rowsFor(I);
      // one scale per row, wide enough for its line and anything any frame ever puts in it
      scales = rows.map(row => {
        let max = len(row, row.target || row.cap || row.atLeast || 1);
        for (const f of frames) {
          const vals = I.items.filter((_, k) => f.assign[k] === row.src).map(t => len(row, row.measure === "v" ? t.v : t.w));
          max = Math.max(max, sumOf(vals));
        }
        return TW / Math.max(1e-9, max * 1.08);
      });
      if (P.rule === "partition") { const s = Math.min(...scales); scales = scales.map(() => s); }
      if (P.rule === "binpacking") { const s = Math.min(...scales); scales = scales.map(() => s); }

      // pool of item tiles, wrapping
      mk("text", { x: 24, y: 26, class: "pk-head" }, svg).textContent = I.rule === "knapsack" ? "ITEMS · (WEIGHT, VALUE)" : "ITEMS";
      const tileW = t => (I.rule === "knapsack" ? 74 : Math.max(38, String(t.w).length * 9 + 22));
      let x = 24, y = 40;
      const tiles = [];
      I.items.forEach(t => {
        const w = tileW(t);
        if (x + w > W - 24) { x = 24; y += 40; }
        tiles.push({ t, x, y, w }); x += w + 8;
      });
      const poolBottom = y + 30;
      els = { tiles: [], segs: [], reads: [], beam: null, rowY: [] };
      const gT = mk("g", {}, svg);
      tiles.forEach(({ t, x: tx, y: ty, w }) => {
        const g = mk("g", { class: "pk-tile", tabindex: "0", role: "button", "aria-label": I.rule === "knapsack" ? `Item weight ${t.w} value ${t.v}` : `Item ${t.w}` }, gT);
        mk("rect", { x: tx, y: ty, width: w, height: 30, rx: 7 }, g);
        if (I.rule === "knapsack") {
          mk("text", { x: tx + w / 2, y: ty + 15 }, g).textContent = `${t.w}, ${t.v}`;
        } else mk("text", { x: tx + w / 2, y: ty + 15 }, g).textContent = t.w;
        hook(g, t.i);
        els.tiles.push({ i: t.i, g });
      });

      // container rows
      let ry = poolBottom + 44;
      mk("text", { x: 24, y: ry - 18, class: "pk-head" }, svg).textContent =
        I.rule === "binpacking" ? `BINS · CAPACITY ${I.C}, AT MOST ${I.K}` : I.rule === "knapsack" ? "THE KNAPSACK" : I.rule === "partition" ? "TWO SIDES THAT MUST MATCH" : "CHOSEN ITEMS";
      rows.forEach((row, r) => {
        const cy = ry + 16;
        els.rowY.push(cy);
        mk("text", { x: 24, y: cy - 3, class: "pk-row-label" }, svg).textContent = row.label;
        mk("text", { x: 24, y: cy + 13, class: "pk-row-sub" }, svg).textContent = row.sub;
        const lineAt = row.target || row.cap || row.atLeast;
        const trackLen = row.cap ? len(row, row.cap) * scales[r] : TW;
        mk("rect", { x: X0, y: cy - 15, width: trackLen, height: 30, rx: 6, class: "pk-track" }, svg);
        const g = mk("g", {}, svg);
        els.segs.push({ r, g });
        if (lineAt) {
          const lx = X0 + len(row, lineAt) * scales[r];
          mk("line", { x1: lx, x2: lx, y1: cy - 22, y2: cy + 22, class: "pk-line" }, svg);
          mk("text", { x: lx, y: cy - 26, class: "pk-line-label" }, svg).textContent = row.cap ? `${row.measure === "v" ? "V" : I.rule === "binpacking" ? "C" : "W"} = ${lineAt}` : row.atLeast ? `V = ${lineAt}` : `${row.log ? "T" : I.rule === "partition" ? "half" : "T"} = ${lineAt}`;
        }
        els.reads.push(mk("text", { x: X0 + TW + 14, y: cy + 4, class: "pk-read" }, svg));
        ry += 64;
      });
      if (P.rule === "product") mk("text", { x: X0, y: ry - 14, class: "pk-note" }, svg).textContent = "Log scale: multiplying factors adds their lengths.";
      if (P.rule === "partition") {
        const bx = X0 + TW / 2, by = ry + 30;
        mk("polygon", { points: `${bx - 12},${by + 22} ${bx + 12},${by + 22} ${bx},${by}`, class: "pk-fulcrum" }, svg);
        els.beam = mk("g", { style: `transform-origin: ${bx}px ${by}px` }, svg);
        mk("line", { x1: bx - 120, x2: bx + 120, y1: by, y2: by, class: "pk-beam" }, els.beam);
        els.beamA = mk("text", { x: bx - 120, y: by - 10, class: "pk-line-label" }, els.beam);
        els.beamB = mk("text", { x: bx + 120, y: by - 10, class: "pk-line-label" }, els.beam);
        ry += 66;
      }
      svg.setAttribute("viewBox", `0 0 ${W} ${ry + 6}`);
    }

    function hook(g, i) {
      const on = () => { hover = i; paint(); }, off = () => { hover = null; paint(); };
      g.addEventListener("pointerenter", on); g.addEventListener("pointerleave", off);
      g.addEventListener("focus", on); g.addEventListener("blur", off);
      g.addEventListener("click", () => { hover = hover === i ? null : i; paint(); });
    }

    function itemState(f, i) {
      if (!f) return "";
      if (i === f.bad) return "Rejected";
      const a = f.assign[i];
      if (i === f.trying && !f.done) return "Active";
      if (a < 0) return f.done && f.ok ? "Unused" : "";
      if (f.trial) return P.rule === "partition" || P.rule === "binpacking" ? "" : "Active";
      return "Solution";
    }
    const groupOf = (f, i) => (f && f.assign[i] >= 0 && (P.rule === "partition" || P.rule === "binpacking") ? f.assign[i] : null);

    function paint() {
      const f = frame;
      els.tiles.forEach(({ i, g }) => {
        let cls = "pk-tile";
        const st = itemState(f, i), grp = groupOf(f, i);
        if (st) cls += " " + st;
        if (grp !== null && st !== "Rejected") cls += " g" + (grp % 6);
        if (hover !== null) cls += i === hover ? " trace" : " faint";
        g.setAttribute("class", cls);
      });
      els.segs.forEach(({ r, g }) => {
        g.innerHTML = "";
        const row = rows[r], sc = scales[r];
        if (!f) { els.reads[r].textContent = ""; return; }
        let x = X0, acc = 0;
        const limit = row.cap !== undefined ? len(row, row.cap) : Infinity;
        I.items.forEach((t, i) => {
          if (f.assign[i] !== row.src) return;
          const val = row.measure === "v" ? t.v : t.w, L = Math.max(row.log ? 6 : 2, len(row, val) * sc);
          acc += len(row, val);
          let cls = "pk-seg", st = itemState(f, i);
          if (st && st !== "Unused") cls += " " + st;
          if (P.rule === "partition" || P.rule === "binpacking") cls += " g" + (row.color % 6);
          if (acc > limit + 1e-9) cls += " over";
          if (hover !== null) cls += i === hover ? " trace" : " faint";
          const sg = mk("g", { class: cls }, g);
          mk("rect", { x, y: els.rowY[r] - 13, width: L, height: 26, rx: 4 }, sg);
          const text = row.measure === "v" ? String(t.v) : String(t.w);
          if (L >= text.length * 7.5 + 6) mk("text", { x: x + L / 2, y: els.rowY[r] }, sg).textContent = text;
          mk("title", {}, sg).textContent = row.measure === "v" ? `value ${t.v}` : `${t.w}`;
          x += L;
        });
        const tot = rowTotal(row, f.assign), rd = els.reads[r];
        let ok = null;
        if (row.target !== undefined) ok = tot === row.target;
        if (row.cap !== undefined) ok = tot <= row.cap;
        if (row.atLeast !== undefined) ok = tot >= row.atLeast;
        rd.textContent = `${tot}${ok === null ? "" : ok ? " ✓" : " ✗"}`;
        rd.setAttribute("class", "pk-read" + (ok === null ? "" : ok ? " ok" : " bad"));
      });
      if (els.beam) {
        const a = f ? rowTotal(rows[0], f.assign) : 0, b = f ? rowTotal(rows[1], f.assign) : 0, tot = sumOf(I.items.map(t => t.w));
        const ang = f ? Math.max(-14, Math.min(14, ((a - b) / Math.max(1, tot)) * 40)) : 0;
        els.beam.style.transform = `rotate(${-ang}deg)`;
        els.beamA.textContent = f ? `S1 ${a}` : "S1";
        els.beamB.textContent = f ? `S2 ${b}` : "S2";
      }
    }

    function checks(f) {
      if (!f) return [];
      const out = [], placedCount = f.assign.filter(a => a >= 0).length;
      if (I.rule === "partition") {
        const a = rowTotal(rows[0], f.assign), b = rowTotal(rows[1], f.assign);
        out.push({ label: `Items placed: ${placedCount} / ${I.n}`, ok: placedCount === I.n });
        out.push({ label: `S1 = ${a}, S2 = ${b}`, ok: placedCount === I.n && a === b });
      } else if (I.rule === "sum" || I.rule === "product") {
        const t = rowTotal(rows[0], f.assign);
        out.push({ label: `${I.rule === "sum" ? "Sum" : "Product"}: ${t} (T = ${I.T})`, ok: placedCount > 0 && t === I.T });
      } else if (I.rule === "knapsack") {
        const w = rowTotal(rows[0], f.assign), v = rowTotal(rows[1], f.assign);
        out.push({ label: `Weight: ${w} (at most ${I.W})`, ok: w <= I.W });
        out.push({ label: `Value: ${v} (at least ${I.V})`, ok: v >= I.V });
      } else {
        const sums = rows.map(row => rowTotal(row, f.assign)), used = sums.filter(s => s > 0).length;
        out.push({ label: `Items packed: ${placedCount} / ${I.n}`, ok: placedCount === I.n });
        out.push({ label: `Bins used: ${used} (at most ${I.K})`, ok: used <= I.K });
        out.push({ label: `Fullest bin: ${Math.max(0, ...sums)} (capacity ${I.C})`, ok: Math.max(0, ...sums) <= I.C });
      }
      if (f.done && !f.ok) return out.map(c => ({ ...c, ok: false }));
      return out;
    }

    function chosen(f) {
      if (!f) return [];
      const pick = r => I.items.filter((_, k) => f.assign[k] === r);
      if (I.rule === "partition") return f.assign.some(a => a >= 0) ? [0, 1].map(r => ({ text: `S${r + 1} ${fmt(pick(r).map(t => t.w))} = ${sumOf(pick(r).map(t => t.w))}`, color: r })) : [];
      if (I.rule === "binpacking") return rows.map((row, b) => ({ b, items: pick(b) })).filter(x => x.items.length).map(x => ({ text: `Bin ${x.b + 1}: ${fmt(x.items.map(t => t.w))}`, color: x.b }));
      const p = pick(0);
      if (!p.length) return [];
      if (I.rule === "knapsack") return [{ text: `${pairsText(I, f.assign)} · weight ${sumOf(p.map(t => t.w))}, value ${sumOf(p.map(t => t.v))}` }];
      if (I.rule === "product") return [{ text: `${p.map(t => t.w).join(" × ")} = ${prodOf(p.map(t => t.w))}` }];
      return [{ text: `${p.map(t => t.w).join(" + ")} = ${sumOf(p.map(t => t.w))}` }];
    }

    return {
      load(str, prob, solverKey) {
        P = prob; I = parse(str, P);
        const key = solverKey || P.solvers[0][0];
        frames = SOLVE[P.rule][key](I);
        frames.unshift({ assign: none(I.n), caption: "Step 0: nothing chosen yet. Step forward to watch the solver." });
        frame = null; hover = null;
        draw(); paint();
        const last = frames[frames.length - 1];
        return { frames, ok: !!last.ok };
      },
      show(i) { frame = i === null ? null : frames[i]; paint(); return frame; },
      checks(i) { return checks(i === null ? null : frames[i]); },
      chosen(i) { return chosen(i === null ? null : frames[i]); },
      resetHover() { hover = null; paint(); },
    };
  }

  const SOLVE = {
    partition: { brute: partitionBrute, greedy: partitionGreedy },
    sum: { brute: I => subsetBrute(I, false), approx: subsetApprox },
    product: { brute: I => subsetBrute(I, true) },
    knapsack: { brute: knapsackBrute, dp: knapsackDP },
    binpacking: { ffd: binFFD, brute: binBrute },
  };

  /* Catalog shared by both mockups. Names, definitions, defaults and solver names are copied from Redux. */
  const PROBLEMS = {
    PARTITION: { label: "Partition", rule: "partition", fmt: "{S}",
      def: "The partition problem is the task of deciding whether a given multiset S of positive integers can be partitioned into two subsets S1 and S2 such that the sum of the numbers in S1 equals the sum of the numbers in S2",
      solvers: [["brute", "Partition Brute Force"], ["greedy", "Largest-first greedy (not in Redux yet)"]],
      examples: [["Redux default", "{1,7,12,15,33,12,11,5,6,9,21,18}"], ["Greedy misses it", "{3,3,2,2,2}"], ["Odd total", "{3,5,7}"]] },
    SUBSETSUM: { label: "Subset Sum", rule: "sum", fmt: "({S},T)",
      def: "The problem is to determine whether there exists a sum of elements that totals to the number T.",
      solvers: [["brute", "Subset Sum Brute Force"], ["approx", "Fast Approximation Algorithm"]],
      examples: [["Redux default", "({1,7,12,15},28)"], ["No subset", "({4,6,10},7)"], ["Approximation misses", "({236,220,216,256,216},928)"]] },
    SUBSETPRODUCT: { label: "Subset Product", rule: "product", fmt: "({S},T)",
      def: "The problem is to determine whether some of the numbers multiply together to exactly the target T.",
      solvers: [["brute", "Subset Product Brute Force"]],
      examples: [["Redux default", "({2,3,5,7},30)"], ["No subset", "({2,4,9},12)"]] },
    KNAPSACK: { label: "Knapsack (Binary)", rule: "knapsack", fmt: "({(w,v),…},W,V)",
      def: "The 0-1 KNAPSACK decision problem is given a knapsack with a maximum capacity W and target value V and a set of n items x_1, x_2,... x_n with weights w_1,w_2,... w_n and values v_1,v_2,... v_n find the combination of singular items that provide greater than V value while staying under W.",
      solvers: [["dp", "Knapsack Dynamic Programming"], ["brute", "Knapsack Brute Force"]],
      examples: [["Redux default", "({(10,60),(20,100),(30,120)},50,220)"], ["Value out of reach", "({(10,60),(20,100),(30,120)},50,250)"], ["Five items", "({(12,4),(2,2),(1,1),(4,10),(1,2)},15,15)"]] },
    BINPACKING: { label: "Bin Packing", rule: "binpacking", fmt: "((S),C,K)",
      def: "The Bin Packing decision problem asks: given a multiset of item sizes, a bin capacity C, and a bin limit K, can all items be packed into at most K bins such that the total size in each bin does not exceed C? Bin Packing is NP-Complete; the optimization variant (minimize the number of bins) is NP-Hard.",
      solvers: [["ffd", "Bin Packing First Fit Decreasing"], ["brute", "Bin Packing Brute Force"]],
      examples: [["Redux default", "((4,7,3,6,2,8),10,3)"], ["First Fit Decreasing falls short", "((3,3,3,3,2,2,2,2),10,2)"], ["Too much to fit", "((6,6,6),10,2)"]] },
  };

  return { create, parse, PROBLEMS, SOLVE };
})();
