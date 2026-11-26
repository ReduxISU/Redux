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
.pk-tile.Committed rect { fill: var(--av-com-fill); stroke: var(--av-com); stroke-width: 2.4; stroke-dasharray: 5 3; } .pk-tile.Committed text { fill: var(--av-com-ink); font-weight: 600; }
.pk-tile.Active rect { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; }
.pk-tile.Rejected rect { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 3; } .pk-tile.Rejected text { fill: var(--av-rej); }
.pk-tile.Unused rect { fill: transparent; stroke: var(--av-stroke); stroke-dasharray: 4 3; } .pk-tile.Unused text { fill: var(--av-muted); }
.pk-tile.trace rect, .pk-tile:focus-visible rect { stroke: var(--av-hot); stroke-width: 3; }
.pk-tile.faint, .pk-seg.faint { filter: grayscale(1); }
.pk-row-label { font-size: 13px; font-weight: 600; fill: var(--av-ink); }
.pk-row-sub { font-size: 11px; fill: var(--av-muted); }
.pk-track { fill: var(--av-surface); stroke: var(--av-stroke); stroke-width: 1.2; transition: fill .2s, stroke .2s; }
.pk-track.Covered { fill: var(--av-sol-fill); stroke: var(--av-sol); stroke-width: 2.4; }
.pk-track.Solution { fill: var(--av-sol-fill); stroke: var(--av-sol); stroke-width: 3; }
.pk-track.Active { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; }
.pk-track.Rejected { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 3; }
.pk-row-label.Covered, .pk-row-label.Solution { fill: var(--av-sol-ink); } .pk-row-label.Active { fill: var(--av-hl-ink); } .pk-row-label.Rejected { fill: var(--av-rej-ink); }
.pk-seg rect { stroke: var(--av-surface); stroke-width: 1.5; transition: fill .2s; }
.pk-seg text { font-size: 11.5px; text-anchor: middle; dominant-baseline: central; pointer-events: none; fill: var(--av-on-sol); font-weight: 600; }
.pk-seg.Solution rect { fill: var(--av-sol); }
.pk-seg.Committed rect { fill: var(--av-com-fill); stroke: var(--av-com); stroke-dasharray: 4 2; }
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
.pk-seg.Committed text { fill: var(--av-com-ink); }
.pk-tile.Active.kept rect { fill: var(--av-com-fill); } .pk-tile.Active.kept text { fill: var(--av-com-ink); font-weight: 600; }
.pk-best { fill: var(--av-com-fill); stroke: var(--av-com); stroke-width: 2; stroke-dasharray: 5 3; }
.pk-best-label { font-size: 11px; fill: var(--av-com-ink); font-weight: 600; text-anchor: middle; }
.pk-bound { stroke: var(--av-hl); stroke-width: 2.4; stroke-dasharray: 3 3; filter: drop-shadow(0 0 1.2px var(--av-surface)) drop-shadow(0 0 1.2px var(--av-surface)); }
.pk-bound-label { font-size: 11px; fill: var(--av-hl-ink); font-weight: 600; text-anchor: middle; }
.pk-tile g.pk-badge rect { fill: var(--av-ink); stroke: none; }
.pk-tile g.pk-badge text { font-size: 10.5px; font-weight: 700; fill: var(--av-surface); text-anchor: middle; dominant-baseline: central; }
.pk-cell rect { fill: var(--av-bg); stroke: var(--av-stroke); stroke-width: 1.2; transition: fill .2s, stroke .2s; }
.pk-cell text { text-anchor: middle; dominant-baseline: central; pointer-events: none; }
.pk-cell .amt { font-size: 9.5px; fill: var(--av-muted); }
.pk-cell .val { font-size: 13px; font-weight: 600; fill: var(--av-ink); }
.pk-cell .val.inf { fill: var(--av-muted); font-weight: 400; }
.pk-cell.read rect { stroke: var(--av-hl); stroke-width: 2; stroke-dasharray: 3 2; }
.pk-cell.pick rect { stroke: var(--av-hl); stroke-width: 3; stroke-dasharray: none; }
.pk-cell.Active rect { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; } .pk-cell.Active .amt { fill: var(--av-ink); }
.pk-cell.Committed rect { fill: var(--av-com-fill); stroke: var(--av-com); stroke-width: 2.4; stroke-dasharray: 5 3; } .pk-cell.Committed text { fill: var(--av-com-ink); }
.pk-cell.Solution rect { fill: var(--av-sol); stroke: var(--av-sol); } .pk-cell.Solution text { fill: var(--av-on-sol); }
.pk-cell.Rejected rect { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 3; } .pk-cell.Rejected text { fill: var(--av-rej-ink); }
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
    } else if (P.rule === "maxknapsack") {
      // Redux: (items,capacity), items an ordered list of (weight,value); zeros are allowed, indices are 1-based positions.
      if (top.t !== "tup" || top.items.length !== 2) throw new Error("Expected ((items),W), like (((10,60),(20,100)),50).");
      const raw = top.items[0];
      if (!raw || raw.t !== "tup") throw new Error("Items must be a list in round brackets, like ((10,60),(20,100)).");
      I.pairs = raw.items.map(p => {
        if (!p || p.t !== "tup" || p.items.length !== 2) throw new Error("Each item must be (weight,value).");
        return [int(p.items[0], "A weight"), int(p.items[1], "A value")];
      });
      items = I.pairs.map(p => p[0]);
      I.W = int(top.items[1], "W");
      if (I.W < 0 || I.pairs.some(p => p[0] < 0 || p[1] < 0)) throw new Error("Weights, values and W can't be negative.");
    } else if (P.rule === "changemaking") {
      // Redux: (C,X,K), C a set of positive coin values (repeats count once), X >= 1, K >= 0.
      if (top.t !== "tup" || top.items.length !== 3 || !top.items[0] || top.items[0].t !== "set") throw new Error("Expected ({C},X,K), like ({1,7,10,25},39,3).");
      items = [...new Set(numbers(top.items[0], "C"))];
      I.X = int(top.items[1], "X"); I.K = int(top.items[2], "K");
      if (I.X < 1) throw new Error("X must be at least 1.");
      if (I.K < 0) throw new Error("K can't be negative.");
      if (I.X > 99) throw new Error("This mockup draws amounts up to 99.");
    }
    if (!items.length) throw new Error("There must be at least one item.");
    if (items.length > 16) throw new Error("This mockup draws up to 16 items.");
    if (P.rule !== "maxknapsack" && items.some(x => x <= 0)) throw new Error("Every number must be positive.");
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

  /* ---------- Maximum-Value 0-1 Knapsack: no target V, it maximizes ----------
     A frame's assign is the set being looked at now; best is the best set found so far (Committed), bestValue its value,
     bound (Branch and Bound only) the most the current branch could still reach. Items are named by their 1-based index. */
  const pickIdx = assign => assign.map((a, k) => (a === 0 ? k : -1)).filter(k => k >= 0);
  const itemsText = idx => (idx.length ? (idx.length === 1 ? `item ${idx[0] + 1}` : `items ${idx.slice(0, -1).map(k => k + 1).join(", ")} and ${idx[idx.length - 1] + 1}`) : "no items");
  const wOf = (I, idx) => sumOf(idx.map(k => I.items[k].w)), vOf = (I, idx) => sumOf(idx.map(k => I.items[k].v));
  const asAssign = (n, idx) => { const a = none(n); idx.forEach(k => (a[k] = 0)); return a; };
  const itemName = t => `item ${t.i + 1} (${t.w}, ${t.v})`;
  const perUnit = t => { const d = t.v / t.w; return Number.isInteger(d) ? String(d) : d.toFixed(2).replace(/0$/, ""); };
  // Density order as Redux sorts it: value/weight descending (compared by cross-multiplying), ties by index.
  const byDensity = (a, b) => b.v * a.w - a.v * b.w || a.i - b.i;
  // Redux's pre-pass (Branch and Bound and Modified Greedy): zero-value items are never taken, zero-weight ones are free,
  // and an item heavier than W on its own is dropped.
  function mvkPrepass(I) {
    const free = [], cand = [], dropped = [];
    for (const t of I.items) {
      if (t.v === 0) continue;
      if (t.w === 0) free.push(t.i); else if (t.w <= I.W) cand.push(t); else dropped.push(t.i);
    }
    return { free, cand: cand.slice().sort(byDensity), dropped };
  }
  function mvkSortCaption(pre) {
    let s = pre.cand.length ? `Sort by value per unit of weight: ${pre.cand.map(t => `item ${t.i + 1} (${perUnit(t)})`).join(", ")}.` : "No item both fits and is worth something.";
    if (pre.free.length) s += ` ${itemsText(pre.free)[0].toUpperCase() + itemsText(pre.free).slice(1)} weigh${pre.free.length === 1 ? "s" : ""} nothing, so ${pre.free.length === 1 ? "it is" : "they are"} always taken.`;
    if (pre.dropped.length) s += ` ${itemsText(pre.dropped)[0].toUpperCase() + itemsText(pre.dropped).slice(1)} ${pre.dropped.length === 1 ? "is" : "are"} heavier than ${pre.dropped.length === 1 ? "the whole knapsack" : "the whole knapsack on their own"}.`;
    return s;
  }
  // Exact Brute Force: masks 0, 1, 2, … (bit k = item k+1); a feasible set replaces the best only if worth strictly more.
  function mvkBrute(I) {
    const n = I.n, frames = [];
    let best = [], bestValue = 0, tried = 0;
    for (let m = 0; m < 2 ** n; m++) {
      const idx = I.items.map((_, k) => k).filter(k => m & (1 << k)), w = wOf(I, idx), v = vOf(I, idx), fits = w <= I.W;
      const better = fits && v > bestValue;
      if (better) { best = idx; bestValue = v; }
      tried++;
      let caption;
      if (m === 0) caption = "Try no items: value 0. The best so far starts here.";
      else if (!fits) caption = `Try ${itemsText(idx)}: weight ${w} is over ${I.W}. Skip it.`;
      else if (better) caption = `Try ${itemsText(idx)}: weight ${w}, value ${v}. That beats the best so far, so keep it.`;
      else caption = `Try ${itemsText(idx)}: weight ${w}, value ${v}. No better than the best so far, ${bestValue}.`;
      if (tried <= SHOW) frames.push({ assign: asAssign(n, idx), best: asAssign(n, best), bestValue, trial: !fits, newBest: better || m === 0, caption });
    }
    frames.push({ assign: asAssign(n, best), done: true, ok: true, bestValue,
      caption: best.length ? `Checked all ${tried} sets${hidden(tried)}. ${itemsText(best)[0].toUpperCase() + itemsText(best).slice(1)}: weight ${wOf(I, best)}, value ${bestValue}, the most any set within ${I.W} is worth.`
        : `Checked all ${tried} sets${hidden(tried)}. Nothing worth anything fits in ${I.W}, so the answer is no items, value 0.` });
    return frames;
  }
  // Exact Branch and Bound, as Redux runs it: start from a greedy guess, then depth-first, "take it" before "leave it",
  // in density order; cut a branch when its fractional bound (rounded down) can't beat the best so far.
  function mvkBnB(I) {
    const n = I.n, frames = [], pre = mvkPrepass(I), pr = pre.cand;
    let count = 0;
    const push = f => { if (++count <= SHOW) frames.push(f); };
    const freeV = vOf(I, pre.free);
    push({ assign: asAssign(n, pre.free), caption: mvkSortCaption(pre) });
    let best = pre.free.slice(), bestValue = freeV, room = I.W;
    for (const t of pr) if (t.w <= room) { room -= t.w; best.push(t.i); bestValue += t.v; }
    push({ assign: asAssign(n, pre.free), best: asAssign(n, best), bestValue, newBest: true,
      caption: `A quick first guess: go down that order and take every item that still fits: ${itemsText(best.slice().sort((a, b) => a - b))}, value ${bestValue}. That's the best so far.` });
    const bound = (next, rem, val) => {
      let b = val;
      for (let k = next; k < pr.length && rem > 0; k++) { const t = pr[k]; if (t.w <= rem) { rem -= t.w; b += t.v; } else { b += Math.floor((rem * t.v) / t.w); break; } }
      return b;
    };
    let nodes = 0, cuts = 0;
    const stack = [{ next: 0, rem: I.W, val: freeV, path: pre.free.slice(), add: null }];
    while (stack.length) {
      const nd = stack.pop(); nodes++;
      let caption;
      if (nd.next === 0) caption = `Start the search with ${pre.free.length ? itemsText(pre.free) : "nothing"} taken: value ${nd.val}, room ${nd.rem}.`;
      else if (nd.add !== null) caption = `Take ${itemName(I.items[nd.add])}: value ${nd.val}, room left ${nd.rem}.`;
      else caption = `Leave out ${itemName(pr[nd.next - 1])}: value ${nd.val}, room left ${nd.rem}.`;
      const better = nd.val > bestValue;
      if (better) { bestValue = nd.val; best = nd.path.slice(); caption += " That beats the best so far, so keep it."; }
      const b = bound(nd.next, nd.rem, nd.val);
      const cut = b <= bestValue, leaf = nd.next === pr.length;
      if (cut && !leaf) cuts++;
      if (leaf) caption += " Every item is decided.";
      else if (cut) caption += ` At most ${b} is reachable from here${better ? "" : `, not more than the best so far, ${bestValue}`}, so stop this branch.`;
      else caption += ` Up to ${b} may be reachable from here, so keep going.`;
      push({ assign: asAssign(n, nd.path), best: asAssign(n, best), bestValue, bound: b, trying: nd.add === null ? undefined : nd.add, trial: cut && !leaf, newBest: better, caption });
      if (cut || nd.next === pr.length) continue;
      const t = pr[nd.next];
      stack.push({ next: nd.next + 1, rem: nd.rem, val: nd.val, path: nd.path.slice(), add: null });
      if (t.w <= nd.rem) stack.push({ next: nd.next + 1, rem: nd.rem - t.w, val: nd.val + t.v, path: [...nd.path, t.i], add: t.i });
    }
    best.sort((a, b) => a - b);
    frames.push({ assign: asAssign(n, best), done: true, ok: true, bestValue,
      caption: `Search done: ${nodes} branch${nodes === 1 ? "" : "es"} looked at, ${cuts} stopped early${hidden(count)}. ${itemsText(best)[0].toUpperCase() + itemsText(best).slice(1)}, value ${bestValue}, is the best.` });
    return frames;
  }
  // Modified Greedy (a 1/2-approximation): the density-order prefix, stopping at the first item that doesn't fit,
  // against the single most valuable item; the better of the two wins (the prefix on a tie).
  function mvkGreedy(I) {
    const n = I.n, frames = [], pre = mvkPrepass(I);
    frames.push({ assign: asAssign(n, pre.free), best: asAssign(n, pre.free), caption: mvkSortCaption(pre) });
    const prefix = [];
    let room = I.W, pv = 0, stopped = null;
    for (const t of pre.cand) {
      if (t.w > room) { stopped = t; break; }
      room -= t.w; pv += t.v; prefix.push(t.i);
      const kept = [...pre.free, ...prefix];
      frames.push({ assign: asAssign(n, kept), best: asAssign(n, kept), bestValue: vOf(I, kept), trying: t.i, caption: `Take ${itemName(t)}: it fits, room left ${room}. The greedy set is worth ${pv}.` });
    }
    const kept = [...pre.free, ...prefix], keptV = vOf(I, kept);
    if (stopped) frames.push({ assign: asAssign(n, kept), best: asAssign(n, kept), bestValue: keptV, trying: stopped.i, bad: stopped.i,
      caption: `${itemName(stopped)[0].toUpperCase() + itemName(stopped).slice(1)} doesn't fit in the ${room} left. Stop: the greedy set is worth ${pv}.` });
    let single = null;
    for (const t of I.items) if (pre.cand.includes(t) && (!single || t.v > single.v)) single = t;
    let pick = kept;
    if (single) {
      const wins = single.v > pv;
      frames.push({ assign: asAssign(n, [...pre.free, single.i]), best: asAssign(n, kept), bestValue: keptV, trying: single.i,
        caption: `Now the most valuable single item, ${itemName(single)}: ${single.v} against the greedy set's ${pv}. ${wins ? "The single item is worth more, so take it alone." : "The greedy set is worth at least as much, so keep it."}` });
      if (wins) pick = [...pre.free, single.i];
    }
    pick = pick.slice().sort((a, b) => a - b);
    frames.push({ assign: asAssign(n, pick), done: true, ok: true, bestValue: vOf(I, pick),
      caption: `Answer: ${itemsText(pick)}, value ${vOf(I, pick)}. Greedy promises at least half the best value, not the best itself.` });
    return frames;
  }
  // The true best value, for the optimality check (the Optimality Verifier's job in Redux).
  function mvkOptimum(I) {
    let best = 0;
    for (let m = 0; m < 2 ** I.n; m++) { const idx = I.items.map((_, k) => k).filter(k => m & (1 << k)); if (wOf(I, idx) <= I.W) best = Math.max(best, vOf(I, idx)); }
    return best;
  }

  /* ---------- Change-Making: coins reused, one container that must hit X exactly ----------
     A frame carries counts[k] (how many of coin k are in the container) and table: { best: fewest coins per amount
     (undefined = not worked out, Infinity = can't be made), cur, reads, pick, path, bad }. */
  const coinsIn = (I, counts) => I.items.flatMap((t, k) => Array(counts[k] || 0).fill(t.w));
  const coinCert = (I, counts) => "{" + I.items.filter((_, k) => counts[k] > 0).map(t => `(${t.w},${counts[t.i]})`).join(",") + "}";
  const coinWord = n => (n === 1 ? "1 coin" : `${n} coins`);
  function cmOptions(I, best, v) {
    return I.items.filter(t => t.w <= v).map(t => ({ t, from: v - t.w, n: best[v - t.w] }));
  }
  // Redux's choice at one amount: coins in input order, strictly fewer wins, so the first coin reaching the minimum is kept.
  function cmChoose(I, best, v) {
    let n = Infinity, c = null;
    for (const o of cmOptions(I, best, v)) if (o.n !== undefined && o.n !== Infinity && o.n + 1 < n) { n = o.n + 1; c = o.t; }
    return { n, c };
  }
  function cmCaption(I, best, v, ch) {
    const opts = cmOptions(I, best, v);
    if (!opts.length) return `Amount ${v}: every coin is bigger, so ${v} can't be made.`;
    const parts = opts.map(o => `${o.from} + ${o.t.w} → ${o.n === Infinity || o.n === undefined ? "can't" : coinWord(o.n + 1)}`);
    return `Amount ${v}: ${parts.join(", ")}. ${ch.c ? `Fewest: ${coinWord(ch.n)}, ending with a ${ch.c.w}.` : `None works, so ${v} can't be made.`}`;
  }
  // Follow the coin chosen at each amount back from X; shared by both solvers, as in Redux.
  function cmTrace(I, frames, best, last, extra) {
    const counts = Array(I.n).fill(0), path = [];
    const tbl = o => ({ best: best.slice(), ...o });
    if (!(best[I.X] <= I.K)) {
      frames.push({ assign: none(I.n), counts, table: tbl({ bad: I.X }), done: true, ok: false,
        caption: best[I.X] === Infinity ? `${I.X} can't be made from these coins. No.` : `Making ${I.X} takes at least ${coinWord(best[I.X])}, more than K = ${I.K}. No.` });
      return frames;
    }
    frames.push({ assign: none(I.n), counts: counts.slice(), table: tbl({ cur: I.X, path: [] }),
      caption: `${I.X} takes ${coinWord(best[I.X])}, within K = ${I.K}. Follow the table back from ${I.X} to find them.` });
    for (let v = I.X; v > 0; v -= last[v].w) {
      counts[last[v].i]++; path.push(v);
      frames.push({ assign: none(I.n), counts: counts.slice(), trying: last[v].i, table: tbl({ cur: v, path: path.slice(0, -1), pick: v - last[v].w }),
        caption: `At ${v} the table's last coin was a ${last[v].w}: put it in. ${v - last[v].w} left${v - last[v].w ? `, which takes ${coinWord(best[v - last[v].w])}` : ""}.` });
    }
    path.push(0);
    const coins = coinsIn(I, counts).sort((a, b) => b - a);
    frames.push({ assign: none(I.n), counts, table: tbl({ path }), done: true, ok: true,
      caption: `${I.X} = ${coins.join(" + ")}: ${coinWord(coins.length)}, within K = ${I.K}.${extra || ""}` });
    return frames;
  }
  // Change-Making Dynamic Programming: fill the table left to right, then trace back.
  function cmDP(I) {
    const frames = [], X = I.X, best = Array(X + 1).fill(undefined), last = [];
    best[0] = 0;
    frames.push({ assign: none(I.n), counts: Array(I.n).fill(0), table: { best: best.slice(), cur: 0 }, caption: "Amount 0 takes no coins. Each amount after it is one coin more than an amount already worked out." });
    for (let v = 1; v <= X; v++) {
      const ch = cmChoose(I, best, v), cap = cmCaption(I, best, v, ch);
      best[v] = ch.n; last[v] = ch.c;
      frames.push({ assign: none(I.n), counts: Array(I.n).fill(0), trying: ch.c ? ch.c.i : undefined,
        table: { best: best.slice(), cur: v, reads: cmOptions(I, best, v).map(o => o.from), pick: ch.c ? v - ch.c.w : undefined }, caption: cap });
    }
    return cmTrace(I, frames, best, last);
  }
  // Change-Making Memoization: start at X, work out an amount once every amount a coin below it is done; only amounts
  // reachable from X are ever worked out. Redux's explicit stack visits coins in input order.
  function cmMemo(I) {
    const frames = [], X = I.X, best = Array(X + 1).fill(undefined), last = [];
    best[0] = 0;
    frames.push({ assign: none(I.n), counts: Array(I.n).fill(0), table: { best: best.slice(), cur: X },
      caption: `Start at ${X} and work down: an amount is worked out once every amount a coin below it is done. 0 takes no coins.` });
    const stack = [[X, 0]];
    let done = 1;
    while (stack.length) {
      const [v, k] = stack.pop();
      if (k < I.n) { stack.push([v, k + 1]); const c = I.items[k].w; if (c <= v && best[v - c] === undefined) stack.push([v - c, 0]); continue; }
      const ch = cmChoose(I, best, v), cap = cmCaption(I, best, v, ch);
      best[v] = ch.n; last[v] = ch.c; done++;
      frames.push({ assign: none(I.n), counts: Array(I.n).fill(0), trying: ch.c ? ch.c.i : undefined,
        table: { best: best.slice(), cur: v, reads: cmOptions(I, best, v).map(o => o.from), pick: ch.c ? v - ch.c.w : undefined }, caption: cap });
    }
    return cmTrace(I, frames, best, last, done < X + 1 ? ` Only ${done} of the ${X + 1} amounts were needed.` : "");
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
    if (I.rule === "maxknapsack") return [
      { label: "Weight", sub: `at most ${I.W}`, src: 0, measure: "w", cap: I.W, lineLabel: `W = ${I.W}` },
      { label: "Value", sub: "as much as fits", src: 0, measure: "v", most: true }];
    if (I.rule === "changemaking") return [
      { label: "Amount", sub: `exactly ${I.X}`, src: 0, measure: "w", target: I.X, lineLabel: `X = ${I.X}` },
      { label: "Coins", sub: `at most ${I.K}`, src: 0, measure: "count", cap: I.K, lineLabel: `K = ${I.K}` }];
    return Array.from({ length: I.K }, (_, b) => ({ label: `Bin ${b + 1}`, sub: `holds ${I.C}`, src: b, measure: "w", cap: I.C, color: b }));
  }

  function create({ svg }) {
    if (!document.getElementById("pk-style")) {
      const st = document.createElement("style"); st.id = "pk-style"; st.textContent = css; document.head.appendChild(st);
    }
    svg.classList.add("pk-svg");
    let I, P, frames, rows, frame = null, hover = null, els, scales;

    const len = (row, x) => (row.log ? Math.log(Math.max(1, x)) : x);
    const cmTotal = (row, counts) => (counts ? sumOf(I.items.map((t, k) => (counts[k] || 0) * (row.measure === "count" ? 1 : t.w))) : 0);
    function rowTotal(row, assign) {
      const vals = I.items.filter((_, k) => assign && assign[k] === row.src).map(t => (row.measure === "v" ? t.v : t.w));
      return row.log ? (vals.length ? prodOf(vals) : 0) : sumOf(vals);
    }

    function draw() {
      svg.innerHTML = "";
      rows = rowsFor(I);
      // one scale per row, wide enough for its line and anything any frame ever puts in it
      scales = rows.map(row => {
        if (I.rule === "changemaking") {
          let max = Math.max(1, row.target !== undefined ? row.target : row.cap);
          for (const f of frames) max = Math.max(max, cmTotal(row, f.counts));
          return TW / (max * 1.08);
        }
        let max = len(row, row.target || row.cap || row.atLeast || 1);
        for (const f of frames) {
          const vals = I.items.filter((_, k) => f.assign[k] === row.src).map(t => len(row, row.measure === "v" ? t.v : t.w));
          max = Math.max(max, sumOf(vals));
          if (row.most) max = Math.max(max, f.bound || 0, f.bestValue || 0);
        }
        return TW / Math.max(1e-9, max * 1.08);
      });
      if (P.rule === "partition") { const s = Math.min(...scales); scales = scales.map(() => s); }
      if (P.rule === "binpacking") { const s = Math.min(...scales); scales = scales.map(() => s); }

      // pool of item tiles, wrapping
      mk("text", { x: 24, y: 26, class: "pk-head" }, svg).textContent = I.rule === "knapsack" || I.rule === "maxknapsack" ? "ITEMS · (WEIGHT, VALUE)" : I.rule === "changemaking" ? "COINS · EACH CAN BE USED AGAIN AND AGAIN" : "ITEMS";
      const tileW = t => (I.rule === "knapsack" ? 74 : I.rule === "maxknapsack" ? 70 + String(t.i + 1).length * 7 + Math.max(0, `${t.w}, ${t.v}`.length - 6) * 7.5 : Math.max(38, String(t.w).length * 9 + 22));
      const gap = I.rule === "changemaking" ? 22 : 8;
      let x = 24, y = 40;
      const tiles = [];
      I.items.forEach(t => {
        const w = tileW(t);
        if (x + w > W - 24) { x = 24; y += 40; }
        tiles.push({ t, x, y, w }); x += w + gap;
      });
      const poolBottom = y + 30;
      els = { tiles: [], segs: [], reads: [], beam: null, rowY: [], tracks: [], labels: [], badges: [], under: [], over: [], cells: [] };
      const gT = mk("g", {}, svg);
      tiles.forEach(({ t, x: tx, y: ty, w }) => {
        const g = mk("g", { class: "pk-tile", tabindex: "0", role: "button", "aria-label": I.rule === "knapsack" ? `Item weight ${t.w} value ${t.v}` : I.rule === "maxknapsack" ? `Item ${t.i + 1}, weight ${t.w}, value ${t.v}` : I.rule === "changemaking" ? `Coin ${t.w}` : `Item ${t.w}` }, gT);
        mk("rect", { x: tx, y: ty, width: w, height: 30, rx: 7 }, g);
        if (I.rule === "maxknapsack") {
          // the item's 1-based index (the certificate names items by it), then (weight, value)
          const iw = String(t.i + 1).length * 7 + 12;
          mk("text", { x: tx + 4 + iw / 2, y: ty + 15, class: "sub" }, g).textContent = `#${t.i + 1}`;
          mk("text", { x: tx + iw + (w - iw) / 2, y: ty + 15 }, g).textContent = `${t.w}, ${t.v}`;
        } else if (I.rule === "changemaking") {
          mk("text", { x: tx + w / 2, y: ty + 15 }, g).textContent = t.w;
          els.badges.push({ i: t.i, g: mk("g", { class: "pk-badge" }, g), x: tx + w, y: ty });
        } else if (I.rule === "knapsack") {
          mk("text", { x: tx + w / 2, y: ty + 15 }, g).textContent = `${t.w}, ${t.v}`;
        } else mk("text", { x: tx + w / 2, y: ty + 15 }, g).textContent = t.w;
        hook(g, t.i);
        els.tiles.push({ i: t.i, g });
      });

      // container rows
      let ry = poolBottom + 44;
      mk("text", { x: 24, y: ry - 18, class: "pk-head" }, svg).textContent =
        I.rule === "binpacking" ? `BINS · CAPACITY ${I.C}, AT MOST ${I.K}` : I.rule === "knapsack" || I.rule === "maxknapsack" ? "THE KNAPSACK" : I.rule === "changemaking" ? "THE CHANGE" : I.rule === "partition" ? "TWO SIDES THAT MUST MATCH" : "CHOSEN ITEMS";
      rows.forEach((row, r) => {
        const cy = ry + 16;
        els.rowY.push(cy);
        els.labels.push(mk("text", { x: 24, y: cy - 3, class: "pk-row-label" }, svg));
        els.labels[r].textContent = row.label;
        mk("text", { x: 24, y: cy + 13, class: "pk-row-sub" }, svg).textContent = row.sub;
        const lineAt = row.lineLabel ? (row.target !== undefined ? row.target : row.cap) : row.target || row.cap || row.atLeast;
        const trackLen = row.cap !== undefined && (row.cap || row.lineLabel) ? len(row, row.cap) * scales[r] : TW;
        els.tracks.push(mk("rect", { x: X0, y: cy - 15, width: Math.max(trackLen, row.lineLabel ? 6 : 0), height: 30, rx: 6, class: "pk-track" }, svg));
        if (row.most) els.under[r] = mk("g", {}, svg); // the best-so-far bar sits behind this row's items
        const g = mk("g", {}, svg);
        els.segs.push({ r, g });
        if (row.most) els.over[r] = mk("g", {}, svg); // and the bound marker in front
        if (lineAt || (row.lineLabel && lineAt !== undefined)) {
          const lx = X0 + len(row, lineAt) * scales[r];
          mk("line", { x1: lx, x2: lx, y1: cy - 22, y2: cy + 22, class: "pk-line" }, svg);
          mk("text", { x: lx, y: cy - 26, class: "pk-line-label" }, svg).textContent = row.lineLabel ? row.lineLabel : row.cap ? `${row.measure === "v" ? "V" : I.rule === "binpacking" ? "C" : "W"} = ${lineAt}` : row.atLeast ? `V = ${lineAt}` : `${row.log ? "T" : I.rule === "partition" ? "half" : "T"} = ${lineAt}`;
        }
        els.reads.push(mk("text", { x: X0 + TW + 14, y: cy + 4, class: "pk-read" }, svg));
        ry += 64;
      });
      if (P.rule === "product") mk("text", { x: X0, y: ry - 14, class: "pk-note" }, svg).textContent = "Log scale: multiplying factors adds their lengths.";
      if (I.rule === "changemaking") {
        // The DP table as a strip of amounts 0..X, 20 to a line: each cell holds the fewest coins that make its amount.
        mk("text", { x: 24, y: ry + 4, class: "pk-head" }, svg).textContent = "FEWEST COINS FOR EACH AMOUNT";
        const per = 20, step = (W - 48) / per, cw = step - 3;
        const y0 = ry + 16;
        for (let v = 0; v <= I.X; v++) {
          const cx = 24 + (v % per) * step, cyy = y0 + Math.floor(v / per) * 42;
          const g = mk("g", { class: "pk-cell" }, svg);
          mk("rect", { x: cx, y: cyy, width: cw, height: 36, rx: 4 }, g);
          mk("text", { x: cx + cw / 2, y: cyy + 9, class: "amt" }, g).textContent = v;
          const val = mk("text", { x: cx + cw / 2, y: cyy + 24, class: "val" }, g);
          const tip = mk("title", {}, g);
          els.cells.push({ v, g, val, tip });
        }
        ry = y0 + Math.ceil((I.X + 1) / per) * 42 + 4;
      }
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
      // Viewer hook (local/viz-viewer): a frame may carry each item's state already decided (`itemStates`), e.g. from the backend.
      if (f.itemStates) return f.itemStates[i] || "";
      if (I.rule === "maxknapsack") return mvkState(f, i);
      if (I.rule === "changemaking") return cmState(f, i);
      if (i === f.bad) return "Rejected";
      const a = f.assign[i];
      if (i === f.trying && !f.done) return "Active";
      if (a < 0) return f.done && f.ok ? "Unused" : "";
      if (f.trial) return P.rule === "partition" || P.rule === "binpacking" ? "" : "Active";
      return "Solution";
    }
    // Maximum-Value Knapsack: the set looked at now is ElementHighlight, the best set so far Committed; an item in both
    // (when they differ) keeps the amber ring on the Committed fill.
    function mvkState(f, i) {
      if (i === f.bad) return "Rejected";
      const cur = f.assign[i] === 0, kept = !!(f.best && f.best[i] === 0);
      if (f.done) return cur ? (f.ok ? "Solution" : "Rejected") : f.ok ? "Unused" : "";
      if (i === f.trying) return "Active";
      const same = f.best && f.assign.every((a, k) => (a === 0) === (f.best[k] === 0));
      if (same) return cur ? "Committed" : "";
      if (cur) return kept ? "Active kept" : "Active";
      return kept ? "Committed" : "";
    }
    // Change-Making: a coin in the change is Committed while the trace back runs, green on the answer; the coin the step
    // looks at is ElementHighlight.
    function cmState(f, i) {
      const c = f.counts ? f.counts[i] : 0;
      if (f.done) return c ? (f.ok ? "Solution" : "Rejected") : f.ok ? "Unused" : "";
      if (i === f.trying) return "Active";
      return c ? "Committed" : "";
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
      // Viewer hook (local/viz-viewer): a frame may carry each row's container state (`rowStates`[r] = "Covered" | "Rejected" |
      // "Active" | "Solution" | ""), e.g. a bin or side the backend marks; it styles that row's track and label.
      rows.forEach((row, r) => {
        const rs = f && f.rowStates ? f.rowStates[r] || "" : "";
        els.tracks[r].setAttribute("class", "pk-track" + (rs ? " " + rs : ""));
        els.labels[r].setAttribute("class", "pk-row-label" + (rs ? " " + rs : ""));
      });
      if (I.rule === "changemaking") { paintChange(f); return; }
      els.segs.forEach(({ r, g }) => {
        g.innerHTML = "";
        const row = rows[r], sc = scales[r];
        if (row.most) paintBest(f, r);
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

    // The best-so-far bar (behind the value row) and the bound marker (in front), Maximum-Value Knapsack only.
    function paintBest(f, r) {
      const under = els.under[r], over = els.over[r], cy = els.rowY[r], sc = scales[r];
      under.innerHTML = ""; over.innerHTML = "";
      if (!f || f.done) return;
      if (f.best) {
        const bv = f.bestValue !== undefined ? f.bestValue : sumOf(I.items.filter((_, k) => f.best[k] === 0).map(t => t.v));
        const bx = X0 + bv * sc;
        if (bv > 0) mk("rect", { x: X0, y: cy - 15, width: bv * sc, height: 30, rx: 5, class: "pk-best" }, under);
        mk("text", { x: Math.max(X0 + 40, bx), y: cy + 28, class: "pk-best-label" }, under).textContent = `best so far ${bv}`;
      }
      if (f.bound !== undefined) {
        const lx = X0 + f.bound * sc;
        mk("line", { x1: lx, x2: lx, y1: cy - 22, y2: cy + 20, class: "pk-bound" }, over);
        mk("text", { x: lx, y: cy - 26, class: "pk-bound-label" }, over).textContent = `at most ${f.bound}`;
      }
    }
    // Change-Making: count badges on the coins, one segment per coin used, and the table strip.
    function paintChange(f) {
      const counts = f && f.counts ? f.counts : Array(I.n).fill(0);
      els.badges.forEach(({ i, g, x, y }) => {
        g.innerHTML = "";
        if (!counts[i]) return;
        const label = `×${counts[i]}`, bw = label.length * 7 + 10;
        mk("rect", { x: x - bw / 2 - 2, y: y - 9, width: bw, height: 17, rx: 8.5 }, g);
        mk("text", { x: x - 2, y: y - 0.5 }, g).textContent = label;
      });
      els.segs.forEach(({ r, g }) => {
        g.innerHTML = "";
        const row = rows[r], sc = scales[r], rd = els.reads[r];
        if (!f) { rd.textContent = ""; return; }
        let x = X0, acc = 0;
        I.items.forEach((t, k) => {
          for (let j = 0; j < (counts[k] || 0); j++) {
            const val = row.measure === "count" ? 1 : t.w, L = Math.max(2, val * sc);
            acc += val;
            let cls = "pk-seg";
            if (f.done) cls += f.ok ? " Solution" : " Rejected";
            else cls += k === f.trying && j === counts[k] - 1 ? " Active" : " Committed";
            if (row.cap !== undefined && acc > row.cap) cls += " over";
            if (hover !== null) cls += k === hover ? " trace" : " faint";
            const sg = mk("g", { class: cls }, g);
            mk("rect", { x, y: els.rowY[r] - 13, width: L, height: 26, rx: 4 }, sg);
            if (L >= String(t.w).length * 7.5 + 6) mk("text", { x: x + L / 2, y: els.rowY[r] }, sg).textContent = t.w;
            mk("title", {}, sg).textContent = `coin ${t.w}`;
            x += L;
          }
        });
        const tot = cmTotal(row, counts), ok = row.target !== undefined ? tot === row.target : tot <= row.cap;
        rd.textContent = `${tot}${ok ? " ✓" : " ✗"}`;
        rd.setAttribute("class", "pk-read" + (ok ? " ok" : " bad"));
      });
      const tb = f && f.table;
      els.cells.forEach(({ v, g, val, tip }) => {
        const b = tb ? tb.best[v] : undefined;
        val.textContent = b === undefined ? "" : b === Infinity ? "∞" : String(b);
        val.setAttribute("class", "val" + (b === Infinity ? " inf" : ""));
        tip.textContent = b === undefined ? `amount ${v}: not worked out` : b === Infinity ? `amount ${v}: can't be made` : `amount ${v}: ${coinWord(b)}`;
        let cls = "pk-cell";
        if (tb) {
          const onPath = tb.path && tb.path.includes(v);
          if (tb.bad === v) cls += " Rejected";
          else if (f.done && f.ok && onPath) cls += " Solution";
          else if (tb.cur === v) cls += " Active";
          else if (onPath) cls += " Committed";
          if (tb.pick === v) cls += " pick";
          else if (tb.reads && tb.reads.includes(v)) cls += " read";
        }
        g.setAttribute("class", cls);
      });
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
      } else if (I.rule === "maxknapsack") {
        const w = rowTotal(rows[0], f.assign), v = rowTotal(rows[1], f.assign);
        out.push({ label: `Weight: ${w} (at most ${I.W})`, ok: w <= I.W });
        out.push({ label: f.done ? `Value: ${v}, the total the certificate claims` : `Value: ${v}`, ok: f.done ? true : null });
        // Optimality is a separate check (Redux's Optimality Verifier): the Solution Verifier accepts any set that fits.
        if (f.done) {
          const opt = mvkOptimum(I);
          out.push(v === opt ? { label: `Optimal: no set that fits is worth more than ${opt} (separate check)`, ok: true }
            : { label: `Not optimal: the best is ${opt} (separate check). Greedy promises at least half, ${Math.ceil(opt / 2)}.`, ok: false });
        }
        return out;
      } else if (I.rule === "changemaking") {
        const counts = f.counts || [], tot = cmTotal(rows[0], counts), used = cmTotal(rows[1], counts);
        out.push({ label: `Total: ${tot} (exactly ${I.X})`, ok: tot === I.X });
        out.push({ label: `Coins used: ${used} (at most ${I.K})`, ok: used <= I.K });
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
      if (I.rule === "changemaking") {
        const counts = f.counts || [], cs = coinsIn(I, counts);
        return cs.length ? [{ text: `${coinCert(I, counts)} · ${cs.slice().sort((a, b) => b - a).join(" + ")} = ${sumOf(cs)}` }] : [];
      }
      const p = pick(0);
      if (I.rule === "maxknapsack") {
        const idx = pickIdx(f.assign);
        if (!f.done && !idx.length) return [];
        return [{ text: `({${idx.map(k => k + 1).join(",")}},${vOf(I, idx)}) · weight ${wOf(I, idx)}` }];
      }
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
      // Viewer hook (local/viz-viewer): draw an instance built elsewhere (the shape parse() returns) with frames supplied
      // by the caller ({assign, itemStates?, ...}) instead of running a mockup solver.
      loadModel(model, prob, fr) { P = prob; I = model; frames = fr; frame = null; hover = null; draw(); paint(); return { frames }; },
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
    maxknapsack: { brute: mvkBrute, bnb: mvkBnB, greedy: mvkGreedy },
    changemaking: { dp: cmDP, memo: cmMemo },
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
    // Design mockup only (gui: false): not in the Redux_GUI mockup until the team approves the pictures (#1059, #1014).
    MAXIMUMVALUEKNAPSACK: { label: "Maximum-Value 0-1 Knapsack", rule: "maxknapsack", fmt: "((items),W)", gui: false, cert: "({i1,i2,…},totalValue)",
      def: "Choose each item at most once to maximize total value without exceeding capacity. Identical weight/value pairs are distinct item occurrences. The empty selection is allowed.",
      solvers: [["brute", "Maximum-Value 0-1 Knapsack Exact Brute Force"], ["bnb", "Maximum-Value 0-1 Knapsack Exact Branch and Bound"], ["greedy", "Maximum-Value 0-1 Knapsack Modified Greedy Approximation"]],
      examples: [["Redux default (greedy falls short)", "(((10,60),(20,100),(30,120)),50)"], ["One big item beats the greedy set", "(((1,2),(10,10)),10)"], ["Five items", "(((12,4),(2,2),(1,1),(4,10),(1,2)),15)"], ["Nothing fits", "(((60,10),(70,5)),50)"]] },
    CHANGEMAKING: { label: "Change-Making", rule: "changemaking", fmt: "({C},X,K)", gui: false, cert: "{(c,n),…}",
      def: "Given coin values c_1, ..., c_m (each usable any number of times), a target amount X, and a number K, can you make exactly X using at most K coins?",
      solvers: [["dp", "Change-Making Dynamic Programming"], ["memo", "Change-Making Memoization"]],
      examples: [["Redux default", "({1,7,10,25},39,3)"], ["K too small", "({1,7,10,25},39,2)"], ["Big coins only", "({10,25},65,5)"], ["Can't be made", "({4,6},23,10)"]] },
  };

  return { create, parse, PROBLEMS, SOLVE };
})();
