/* ---- Misc views: Convex Hull (Geometry), 3-Dimensional Matching (Tripartite Matching), Lossless Data Compression (Code Tree) ----
   One module, three pictures. Each problem supplies parse + solvers (frames in the problem's own terms) + draw + paint.
   Pages supply --av-* tokens; state names follow the shared vocabulary. */
const MiscView = (() => {
  const NS = "http://www.w3.org/2000/svg";
  const mk = (tag, attrs = {}, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; };
  const SHOW = 150;

  const css = `
.ms-svg { width: 100%; height: auto; display: block; }
.ms-svg text { font-family: var(--av-mono); }
.ms-grid { stroke: var(--av-line); stroke-width: 1; }
.ms-axis { stroke: var(--av-stroke); stroke-width: 1.3; }
.ms-tick { font-size: 10.5px; fill: var(--av-muted); }
.ms-pt { cursor: pointer; transition: opacity .2s; }
.ms-pt:focus { outline: none; }
.ms-pt .dot { fill: var(--av-bg); stroke: var(--av-stroke); stroke-width: 1.6; transition: fill .2s, stroke .2s; }
.ms-pt .lbl { font-size: 11px; fill: var(--av-muted); pointer-events: none; }
.ms-pt.Active .dot { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 2.6; }
.ms-pt.Solution .dot { fill: var(--av-sol); stroke: var(--av-sol); }
.ms-pt.Covered .dot { fill: var(--av-sol-fill); stroke: var(--av-sol); }
.ms-pt.Rejected .dot { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 2.4; }
.ms-pt.c0 .dot { fill: var(--av-g0); stroke: var(--av-g0s); } .ms-pt.c1 .dot { fill: var(--av-g1); stroke: var(--av-g1s); }
.ms-pt.ring .dot { stroke: var(--av-hl); stroke-width: 3.4; }
.ms-pt.trace .dot, .ms-pt:focus-visible .dot { stroke: var(--av-hot); stroke-width: 3; }
.ms-pt.faint { filter: grayscale(1); }
.ms-hull { fill: none; stroke: var(--av-edge); stroke-width: 1.4; stroke-dasharray: 4 3; }
.ms-hull.c0 { stroke: var(--av-g0s); fill: rgba(86,180,233,.13); stroke-dasharray: none; stroke-width: 1.8; }
.ms-hull.c1 { stroke: var(--av-g1s); fill: rgba(230,159,0,.13); stroke-dasharray: none; stroke-width: 1.8; }
.ms-hull.hot { stroke: var(--av-hl); stroke-width: 2.4; stroke-dasharray: none; fill: var(--av-hl-fill); fill-opacity: .5; }
.ms-hull.sol { stroke: var(--av-sol); stroke-width: 2.6; stroke-dasharray: none; fill: var(--av-sol-fill); fill-opacity: .55; }
.ms-chain { fill: none; stroke: var(--av-hl); stroke-width: 2.6; }
.ms-chain.sol { stroke: var(--av-sol); }
.ms-cand { stroke: var(--av-hl); stroke-width: 2.4; stroke-dasharray: 6 4; }
.ms-tip rect { fill: var(--av-surface); stroke: var(--av-line); }
.ms-tip text { font-size: 11.5px; fill: var(--av-ink); dominant-baseline: central; }
.ms-ord circle { fill: var(--av-ink); } .ms-ord text { fill: var(--av-surface); font-size: 10px; font-weight: 700; text-anchor: middle; dominant-baseline: central; }
.ms-el { cursor: pointer; transition: opacity .2s; }
.ms-el:focus { outline: none; }
.ms-el .body { fill: var(--av-bg); stroke: var(--av-stroke); stroke-width: 1.6; transition: fill .2s, stroke .2s; }
.ms-el text { font-size: 12.5px; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); pointer-events: none; }
.ms-el.Active .body { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; }
.ms-el.Covered .body { fill: var(--av-sol-fill); stroke: var(--av-sol); stroke-width: 2.2; }
.ms-el.Rejected .body { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 3; } .ms-el.Rejected text:not(.ms-badge *) { fill: var(--av-rej); }
.ms-el.trace .body, .ms-el:focus-visible .body { stroke: var(--av-hot); stroke-width: 3; }
.ms-el.faint, .ms-trow.faint { filter: grayscale(1); } .ms-tri.faint { stroke: var(--av-edge-dim); stroke-dasharray: 3 3; }
.ms-tri { fill: none; stroke: var(--av-edge); stroke-width: 1.6; transition: opacity .2s, stroke .2s; }
.ms-tri.sol { stroke: var(--av-sol); stroke-width: 3.4; }
.ms-tri.hot { stroke: var(--av-hl); stroke-width: 3.4; }
.ms-tri.rej { stroke: var(--av-rej); stroke-width: 3; }
.ms-tri.blk { stroke: var(--av-edge-dim); stroke-dasharray: 4 4; }
.ms-tri.dim { stroke: var(--av-edge-dim); }
.ms-tri.trace { stroke: var(--av-hot); stroke-width: 3; opacity: 1; }
.ms-trow { cursor: pointer; transition: opacity .2s; }
.ms-trow:focus { outline: none; }
.ms-trow rect { fill: transparent; stroke: transparent; }
.ms-trow text { font-size: 12px; dominant-baseline: central; fill: var(--av-ink); pointer-events: none; }
.ms-trow .tid { font-weight: 700; }
.ms-trow.sol rect { fill: var(--av-sol-fill); stroke: var(--av-sol); }
.ms-trow.hot rect { fill: var(--av-hl-fill); stroke: var(--av-hl); }
.ms-trow.rej rect { fill: var(--av-rej-fill); stroke: var(--av-rej); }
.ms-trow.blk text, .ms-trow.dim text { fill: var(--av-muted); }
.ms-trow.trace rect, .ms-trow:focus-visible rect { stroke: var(--av-hot); stroke-width: 2; }
.ms-colhead { font-size: 11px; font-weight: 700; letter-spacing: .08em; fill: var(--av-muted); }
.ms-badge circle { fill: var(--av-rej); } .ms-badge text { fill: var(--av-on-rej); font-size: 10.5px; font-weight: 700; text-anchor: middle; dominant-baseline: central; }
.ms-tn { cursor: pointer; transition: opacity .25s; }
.ms-tn:focus { outline: none; }
.ms-tn .body { fill: var(--av-bg); stroke: var(--av-stroke); stroke-width: 1.6; transition: fill .2s, stroke .2s; }
.ms-tn text { text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); pointer-events: none; }
.ms-tn .ch { font-size: 13px; font-weight: 700; }
.ms-tn .w { font-size: 10.5px; fill: var(--av-muted); }
.ms-tn.Active .body { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 3; }
.ms-tn.Covered .body { fill: var(--av-sol-fill); stroke: var(--av-sol); }
.ms-tn.Solution .body { fill: var(--av-sol); stroke: var(--av-sol); } .ms-tn.Solution text { fill: var(--av-on-sol); }
.ms-tn.trace .body, .ms-tn:focus-visible .body { stroke: var(--av-hot); stroke-width: 3; }
.ms-tn.hidden, .ms-te.hidden, .ms-bit.hidden, .ms-code.hidden { opacity: 0; pointer-events: none; }
.ms-tn.faint { filter: grayscale(1); }
.ms-te { stroke: var(--av-edge); stroke-width: 1.6; transition: opacity .25s, stroke .2s; }
.ms-te.hot { stroke: var(--av-hl); stroke-width: 3; }
.ms-te.sol { stroke: var(--av-sol); stroke-width: 2.2; }
.ms-te.trace { stroke: var(--av-hot); stroke-width: 3; }
.ms-bit { font-size: 11px; font-weight: 700; fill: var(--av-muted); text-anchor: middle; dominant-baseline: central; transition: opacity .25s; }
.ms-bit.trace { fill: var(--av-hot); }
.ms-code { font-size: 10.5px; font-weight: 700; fill: var(--av-sol); text-anchor: middle; dominant-baseline: central; transition: opacity .25s; }
.ms-cell rect { fill: var(--av-surface); stroke: var(--av-edge-dim); }
.ms-cell.alt rect { fill: var(--av-bg); }
.ms-cell .c { font-size: 12px; text-anchor: middle; dominant-baseline: central; fill: var(--av-ink); }
.ms-cell .b { font-size: 10px; text-anchor: middle; dominant-baseline: central; fill: var(--av-sol); font-weight: 700; transition: opacity .25s; }
.ms-cell .b.hidden { opacity: 0; }
.ms-cell.trace rect { stroke: var(--av-hot); stroke-width: 2.2; fill: var(--av-hl-fill); }
.ms-cap { font-size: 11px; font-weight: 700; letter-spacing: .08em; fill: var(--av-muted); }
@media (prefers-reduced-motion: reduce) { .ms-pt, .ms-pt .dot, .ms-el, .ms-el .body, .ms-tri, .ms-tn, .ms-tn .body, .ms-te, .ms-bit, .ms-code, .ms-cell .b { transition: none; } }`;

  const hook = (g, key, api) => {
    g.addEventListener("pointerenter", () => api.set(key));
    g.addEventListener("pointerleave", () => api.set(null));
    g.addEventListener("focus", () => api.set(key));
    g.addEventListener("blur", () => api.set(null));
    g.addEventListener("click", () => api.set(api.get() === key ? null : key));
  };
  const plural = (n, one, many) => `${n} ${n === 1 ? one : many}`;

  /* =============================== Convex Hull · Geometry =============================== */
  const HULL = (() => {
    const orient = (a, b, c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    const d2 = (a, b) => (a.x - b.x) ** 2 + (a.y - b.y) ** 2;
    const nm = i => "P" + (i + 1);
    const list = a => a.map(nm).join(", ");
    const num = v => String(+v.toFixed(3));

    function parse(str) {
      const s = str.replace(/\s+/g, "");
      const m = /^\{(.*)\}$/.exec(s);
      if (!m) throw new Error("Expected a set of points like {(0,0),(1,0),(0,1)}.");
      const NUM = "(-?(?:\\d+\\.?\\d*|\\.\\d+)(?:[eE][-+]?\\d+)?)";
      const re = new RegExp(`\\(${NUM},${NUM}\\)`, "g");
      if (m[1].replace(re, "").replace(/,/g, "")) throw new Error("Each point must look like (x,y), with numbers for x and y.");
      const pts = [];
      let t;
      while ((t = re.exec(m[1]))) pts.push({ x: Number(t[1]), y: Number(t[2]) });
      if (pts.length < 3) throw new Error("Give at least 3 points.");
      if (pts.length > 60) throw new Error("This mockup draws up to 60 points.");
      const seen = new Set();
      for (const p of pts) { const k = p.x + "," + p.y; if (seen.has(k)) throw new Error(`Point (${p.x},${p.y}) appears twice.`); seen.add(k); }
      return { pts };
    }

    // A straight port of Redux's ConvexHullSolver (divide and conquer, Preparata-Hong style merge), recording each move.
    function divideConquer(D) {
      const P = D.pts, n = P.length, frames = [];
      const order = P.map((_, i) => i).sort((a, b) => P[a].x - P[b].x);
      const desk = [], dropped = new Set();
      const snap = extra => Object.assign({ hulls: desk.map(h => h.slice()), dropped: [...dropped] }, extra);
      frames.push(snap({ caption: `Sort the ${n} points by x: ${list(order)}. Then split the list in half, again and again, until each piece has at most 3 points.` }));
      function base(idx) {
        if (idx.length <= 2) return idx.slice();
        const [a, b, c] = idx, o = orient(P[a], P[b], P[c]);
        if (o === 0) { const s = idx.slice().sort((i, j) => P[i].x - P[j].x || P[i].y - P[j].y); dropped.add(s[1]); return [s[0], s[2]]; }
        return o > 0 ? [a, b, c] : [a, c, b];
      }
      function rec(idx) {
        if (idx.length <= 3) {
          const h = base(idx);
          desk.push(h);
          const what = idx.length === 1 ? `${nm(idx[0])} on its own is its own hull.`
            : idx.length === 2 ? `Smallest piece: ${list(idx)}. Its hull is the segment between them.`
            : h.length === 2 ? `Smallest piece: ${list(idx)}. They sit on one line, so the middle one is dropped.`
            : `Smallest piece: ${list(idx)}. Its hull is the triangle they make.`;
          frames.push(snap({ focus: [h], caption: what }));
          return h;
        }
        const mid = idx.length >> 1;
        const L = rec(idx.slice(0, mid)), R = rec(idx.slice(mid));
        return merge(L, R);
      }
      function merge(L, R) {
        let iL = 0, iR = 0;
        for (let i = 1; i < L.length; i++) if (P[L[i]].x > P[L[iL]].x) iL = i;
        for (let i = 1; i < R.length; i++) if (P[R[i]].x < P[R[iR]].x) iR = i;
        let uL = iL, uR = iR, lL = iL, lR = iR;
        frames.push(snap({ focus: [L, R], cand: [L[iL], R[iR]], caption: `Merge the left hull (${list(L)}) with the right hull (${list(R)}). The bridges that will join them start at the closest corners, ${nm(L[iL])} and ${nm(R[iR])}.` }));
        let settled = false;
        while (!settled) {
          settled = true;
          for (;;) {
            const nx = uL === 0 ? L.length - 1 : uL - 1, o = orient(P[R[uR]], P[L[uL]], P[L[nx]]);
            if (o > 0 || (o === 0 && d2(P[R[uR]], P[L[nx]]) > d2(P[R[uR]], P[L[uL]]))) {
              uL = nx;
              frames.push(snap({ focus: [L, R], cand: [L[uL], R[uR]], caption: `Bottom bridge: ${nm(L[uL])} is below (or on) the line from ${nm(R[uR])}, so the left end moves to ${nm(L[uL])}.` }));
            } else break;
          }
          for (;;) {
            const nx = (uR + 1) % R.length, o = orient(P[L[uL]], P[R[uR]], P[R[nx]]);
            if (o < 0 || (o === 0 && d2(P[L[uL]], P[R[nx]]) > d2(P[L[uL]], P[R[uR]]))) {
              uR = nx; settled = false;
              frames.push(snap({ focus: [L, R], cand: [L[uL], R[uR]], caption: `Bottom bridge: ${nm(R[uR])} is below (or on) the line from ${nm(L[uL])}, so the right end moves to ${nm(R[uR])}.` }));
            } else break;
          }
        }
        frames.push(snap({ focus: [L, R], cand: [L[uL], R[uR]], caption: `Bottom bridge found: ${nm(L[uL])} to ${nm(R[uR])}. Every point sits on or above it.` }));
        const low = [L[uL], R[uR]];
        settled = false;
        while (!settled) {
          settled = true;
          for (;;) {
            const nx = (lL + 1) % L.length, o = orient(P[R[lR]], P[L[lL]], P[L[nx]]);
            if (o < 0 || (o === 0 && d2(P[R[lR]], P[L[nx]]) > d2(P[R[lR]], P[L[lL]]))) {
              lL = nx;
              frames.push(snap({ focus: [L, R], cand: [L[lL], R[lR]], bridge: low, caption: `Top bridge: ${nm(L[lL])} is above (or on) the line from ${nm(R[lR])}, so the left end moves to ${nm(L[lL])}.` }));
            } else break;
          }
          for (;;) {
            const nx = lR === 0 ? R.length - 1 : lR - 1, o = orient(P[L[lL]], P[R[lR]], P[R[nx]]);
            if (o > 0 || (o === 0 && d2(P[L[lL]], P[R[nx]]) > d2(P[L[lL]], P[R[lR]]))) {
              lR = nx; settled = false;
              frames.push(snap({ focus: [L, R], cand: [L[lL], R[lR]], bridge: low, caption: `Top bridge: ${nm(R[lR])} is above (or on) the line from ${nm(L[lL])}, so the right end moves to ${nm(R[lR])}.` }));
            } else break;
          }
        }
        const merged = [];
        let v = uR; merged.push(R[v]);
        while (v !== lR) { v = (v + 1) % R.length; merged.push(R[v]); }
        v = lL; merged.push(L[v]);
        while (v !== uL) { v = (v + 1) % L.length; merged.push(L[v]); }
        const gone = [...L, ...R].filter(i => !merged.includes(i));
        gone.forEach(i => dropped.add(i));
        desk.splice(desk.indexOf(L), 1); desk.splice(desk.indexOf(R), 1); desk.push(merged);
        frames.push(snap({ focus: [merged], caption: `Top bridge found: ${nm(L[lL])} to ${nm(R[lR])}. Walking the two hulls between the bridges gives ${list(merged)}.` + (gone.length ? ` ${list(gone)} ${gone.length > 1 ? "end up inside and are" : "ends up inside and is"} dropped.` : "") }));
        return merged;
      }
      const hull = rec(order), v = validity(P, hull);
      const outside = P.map((_, i) => i).filter(i => !hull.includes(i) && !v.insideOf(i));
      frames.push({ hull, hulls: [], dropped: [...dropped], done: true, ok: v.inside && v.convex,
        caption: v.inside && v.convex
          ? `Done. The hull has ${hull.length} corners, listed ${area(P, hull) > 0 ? "counterclockwise" : "clockwise"} from ${nm(hull[0])}: ${list(hull)}. Every other point lies inside.`
          : `The solver returns ${list(hull)}, but that isn't the hull: ${outside.length ? `${list(outside)} ${outside.length > 1 ? "lie" : "lies"} outside it` : "it isn't convex"}. Redux's solver sorts by x alone, and repeated x values (or points in a line) can break its merge step.` });
      return frames;
    }

    // Andrew's monotone chain: not in Redux, offered so the two approaches can be compared on the same points.
    function monotone(D) {
      const P = D.pts, frames = [];
      const order = P.map((_, i) => i).sort((a, b) => P[a].x - P[b].x || P[a].y - P[b].y);
      frames.push({ chain: [], popped: [], caption: `Sort by x, then y: ${list(order)}. Walk left to right to build the bottom edge, then right to left for the top edge.` });
      const walk = (seq, phase, done) => {
        const chain = [], popped = [];
        for (const i of seq) {
          while (chain.length >= 2 && orient(P[chain[chain.length - 2]], P[chain[chain.length - 1]], P[i]) <= 0) {
            const j = chain.pop(); popped.push(j);
            frames.push({ chain: chain.slice(), test: i, pop: j, popped: popped.slice(), lowerDone: done, phase,
              caption: `Going on to ${nm(i)} would turn clockwise at ${nm(j)}, so ${nm(j)} can't be on the ${phase} edge. Take it off.` });
          }
          chain.push(i);
          frames.push({ chain: chain.slice(), test: i, popped: popped.slice(), lowerDone: done, phase, caption: `Add ${nm(i)} to the ${phase} edge.` });
        }
        return chain;
      };
      const lower = walk(order, "bottom", null);
      const upper = walk(order.slice().reverse(), "top", lower.slice());
      const hull = lower.slice(0, -1).concat(upper.slice(0, -1));
      const inside = P.map((_, i) => i).filter(i => !hull.includes(i));
      frames.push({ hull, done: true, ok: true, caption: `Join the bottom and top edges: ${list(hull)}, counterclockwise from ${nm(hull[0])}. ${inside.length ? `${list(inside)} ${inside.length > 1 ? "are" : "is"} inside.` : "Every point is a corner."}` });
      return frames;
    }

    const validity = (P, h) => {
      const sgn = area(P, h) > 0 ? 1 : -1;
      const insideOf = i => h.every((a, k) => sgn * orient(P[a], P[h[(k + 1) % h.length]], P[i]) >= -1e-12);
      return { insideOf, inside: P.every((_, i) => h.includes(i) || insideOf(i)), convex: h.length >= 3 && h.every((a, k) => sgn * orient(P[a], P[h[(k + 1) % h.length]], P[h[(k + 2) % h.length]]) > 0) };
    };
    const area = (P, h) => { let s = 0; for (let i = 0; i < h.length; i++) { const a = P[h[i]], b = P[h[(i + 1) % h.length]]; s += a.x * b.y - b.x * a.y; } return s / 2; };

    function draw(svg, D, api) {
      svg.innerHTML = "";
      const P = D.pts, W = 680, pad = 50;
      const xs = P.map(p => p.x), ys = P.map(p => p.y);
      const minX = Math.min(...xs), maxX = Math.max(...xs), minY = Math.min(...ys), maxY = Math.max(...ys);
      const dx = Math.max(maxX - minX, 1e-9), dy = Math.max(maxY - minY, 1e-9);
      let s = (W - 2 * pad) / dx, H = dy * s + 2 * pad;
      if (H > 560) { s = (560 - 2 * pad) / dy; H = 560; }
      if (H < 300) H = 300;
      const ox = (W - dx * s) / 2 - minX * s, oy = (H + dy * s) / 2 + minY * s;
      const X = x => ox + x * s, Y = y => oy - y * s;
      svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
      const xLo = (0 - ox) / s, xHi = (W - ox) / s, yLo = (oy - H) / s, yHi = oy / s;
      const nice = r => { const e = 10 ** Math.floor(Math.log10(r)), f = r / e; return (f < 1.5 ? 1 : f < 3.5 ? 2 : f < 7.5 ? 5 : 10) * e; };
      const step = nice(Math.max(xHi - xLo, yHi - yLo) / 8), dec = Math.max(0, -Math.floor(Math.log10(step)));
      const gG = mk("g", {}, svg);
      for (let t = Math.ceil(xLo / step) * step; t <= xHi; t += step) {
        mk("line", { x1: X(t), x2: X(t), y1: 0, y2: H, class: Math.abs(t) < step / 2 ? "ms-axis" : "ms-grid" }, gG);
        if (X(t) > 24 && X(t) < W - 10) mk("text", { x: X(t) + 3, y: H - 6, class: "ms-tick" }, gG).textContent = (+t.toFixed(dec)).toString();
      }
      for (let t = Math.ceil(yLo / step) * step; t <= yHi; t += step) {
        mk("line", { x1: 0, x2: W, y1: Y(t), y2: Y(t), class: Math.abs(t) < step / 2 ? "ms-axis" : "ms-grid" }, gG);
        if (Y(t) > 14 && Y(t) < H - 18) mk("text", { x: 4, y: Y(t) - 4, class: "ms-tick" }, gG).textContent = (+t.toFixed(dec)).toString();
      }
      const gH = mk("g", {}, svg), gL = mk("g", {}, svg), gP = mk("g", {}, svg), gT = mk("g", {}, svg);
      const pts = P.map((p, i) => {
        const g = mk("g", { class: "ms-pt", tabindex: "0", role: "button", "aria-label": `${nm(i)} at (${num(p.x)}, ${num(p.y)})` }, gP);
        mk("circle", { cx: X(p.x), cy: Y(p.y), r: 6, class: "dot" }, g);
        if (P.length <= 30) mk("text", { x: X(p.x) + 9, y: Y(p.y) - 8, class: "lbl" }, g).textContent = nm(i);
        const ob = mk("g", { class: "ms-ord", visibility: "hidden" }, g);
        mk("circle", { cx: X(p.x) - 11, cy: Y(p.y) - 11, r: 8 }, ob);
        const ot = mk("text", { x: X(p.x) - 11, y: Y(p.y) - 11 }, ob);
        hook(g, i, api);
        return { g, ob, ot };
      });
      const tip = mk("g", { class: "ms-tip", visibility: "hidden" }, gT);
      const tipR = mk("rect", { rx: 6, height: 22 }, tip), tipT = mk("text", {}, tip);
      return { P, X, Y, W, H, gH, gL, pts, tip, tipR, tipT };
    }

    const poly = (E, h) => h.map(i => `${E.X(E.P[i].x)},${E.Y(E.P[i].y)}`).join(" ");
    function paint(E, D, f, hover) {
      E.gH.innerHTML = ""; E.gL.innerHTML = "";
      const st = new Map();
      const set = (i, c) => st.set(i, (st.get(i) || "") + " " + c);
      if (f) {
        if (f.done && f.hull) {
          mk("polygon", { points: poly(E, f.hull), class: "ms-hull " + (f.ok ? "sol" : "hot") }, E.gH);
          const v = validity(D.pts, f.hull);
          D.pts.forEach((_, i) => set(i, f.hull.includes(i) ? (f.ok ? "Solution" : "Active") : v.insideOf(i) ? "Covered" : "Rejected"));
        } else if (f.hulls) {
          const focus = f.focus || [];
          f.hulls.forEach(h => {
            const fi = focus.findIndex(x => x.length === h.length && x.every((v, k) => v === h[k]));
            const cls = fi < 0 ? "" : focus.length === 1 ? "hot" : "c" + fi;
            if (h.length >= 3) mk("polygon", { points: poly(E, h), class: "ms-hull " + cls }, E.gH);
            else if (h.length === 2) mk("polyline", { points: poly(E, h), class: "ms-hull " + cls }, E.gH);
            h.forEach(i => set(i, fi < 0 ? "" : focus.length === 1 ? "Active" : "c" + fi));
          });
          (f.dropped || []).forEach(i => set(i, "Rejected"));
          if (f.bridge) mk("line", { x1: E.X(E.P[f.bridge[0]].x), y1: E.Y(E.P[f.bridge[0]].y), x2: E.X(E.P[f.bridge[1]].x), y2: E.Y(E.P[f.bridge[1]].y), class: "ms-cand", style: "stroke-dasharray:none" }, E.gL);
          if (f.cand) {
            mk("line", { x1: E.X(E.P[f.cand[0]].x), y1: E.Y(E.P[f.cand[0]].y), x2: E.X(E.P[f.cand[1]].x), y2: E.Y(E.P[f.cand[1]].y), class: "ms-cand" }, E.gL);
            f.cand.forEach(i => set(i, "ring"));
          }
        } else if (f.chain) {
          if (f.lowerDone) { mk("polyline", { points: poly(E, f.lowerDone), class: "ms-chain sol" }, E.gL); f.lowerDone.forEach(i => set(i, "Solution")); }
          if (f.chain.length) mk("polyline", { points: poly(E, f.chain), class: "ms-chain" }, E.gL);
          f.chain.forEach(i => set(i, "Active"));
          (f.popped || []).forEach(i => set(i, "Rejected"));
          if (f.test !== undefined) set(f.test, "ring");
        }
      }
      E.pts.forEach((o, i) => {
        let cls = "ms-pt" + (st.get(i) || "");
        if (hover !== null) cls += hover === i ? " trace" : "";
        o.g.setAttribute("class", cls);
        const ord = f && f.done && f.hull ? f.hull.indexOf(i) : -1;
        o.ob.setAttribute("visibility", ord >= 0 ? "visible" : "hidden");
        if (ord >= 0) o.ot.textContent = ord + 1;
      });
      if (hover !== null) {
        const p = E.P[hover], text = `${nm(hover)} (${num(p.x)}, ${num(p.y)})`, w = text.length * 7 + 14;
        let x = E.X(p.x) + 12, y = E.Y(p.y) + 10;
        if (x + w > E.W - 4) x = E.X(p.x) - 12 - w;
        if (y + 22 > E.H - 4) y = E.Y(p.y) - 32;
        E.tipR.setAttribute("x", x); E.tipR.setAttribute("y", y); E.tipR.setAttribute("width", w);
        E.tipT.setAttribute("x", x + 7); E.tipT.setAttribute("y", y + 11); E.tipT.textContent = text;
        E.tip.setAttribute("visibility", "visible");
      } else E.tip.setAttribute("visibility", "hidden");
    }

    function checks(D, f) {
      const P = D.pts;
      if (!f) return [{ label: `Points: ${P.length}`, ok: null }];
      if (f.done && f.hull) {
        const h = f.hull, A = area(P, h), { inside, convex } = validity(P, h);
        return [
          { label: `Corners: ${h.length} of ${P.length} points`, ok: null },
          { label: "Every point is inside or on the hull", ok: inside },
          { label: "Every corner turns the same way (convex)", ok: convex },
          { label: `Listed ${A > 0 ? "counterclockwise" : "clockwise"} from ${nm(h[0])}`, ok: null },
        ];
      }
      if (f.hulls) return [{ label: `Hulls waiting to be merged: ${f.hulls.length}`, ok: null }, { label: `Points dropped so far: ${(f.dropped || []).length}`, ok: null }];
      return [{ label: `Points on the ${f.phase || "bottom"} edge so far: ${(f.chain || []).length}`, ok: null }, { label: `Points taken off: ${(f.popped || []).length}`, ok: null }];
    }
    function chosen(D, f) {
      if (!f) return [];
      if (f.done && f.hull) return f.hull.map((i, k) => ({ text: `${k + 1} ${nm(i)} (${num(D.pts[i].x)}, ${num(D.pts[i].y)})` }));
      if (f.focus) return f.focus.map((h, k) => ({ text: list(h), color: f.focus.length > 1 ? k : undefined }));
      if (f.chain && f.chain.length) return [{ text: list(f.chain) }];
      return [];
    }
    return { parse, solve: (D, s) => (s === "chain" ? monotone(D) : divideConquer(D)), draw, paint, checks, chosen, shape: "an ordered list of corners" };
  })();

  /* =============================== 3-Dimensional Matching · Tripartite Matching =============================== */
  const DM3 = (() => {
    function parse(str) {
      let t = str.trim();
      if (!t.startsWith("{") || !t.endsWith("}")) throw new Error("Expected {X}{Y}{Z} followed by one {x,y,z} per triple.");
      t = t.slice(1, -1);
      const groups = t.split("}{").map(g => g.split(",").map(x => x.trim()));
      if (groups.length < 3) throw new Error("Expected at least the three groups {X}{Y}{Z}.");
      if (groups.some(g => g.some(x => !x || /[{}]/.test(x)))) throw new Error("Each group is names separated by commas, like {a,b,c}.");
      const uniq = a => [...new Set(a)];
      const X = uniq(groups[0]), Y = uniq(groups[1]), Z = uniq(groups[2]);
      const all = [...X, ...Y, ...Z], dup = all.find((v, i) => all.indexOf(v) !== i);
      if (dup) throw new Error(`${dup} appears in more than one of X, Y and Z. Redux's verifier needs every name to be different.`);
      const M = groups.slice(3).map((g, i) => {
        if (g.length !== 3) throw new Error(`Triple ${i + 1}, {${g.join(",")}}, must name exactly three things.`);
        const [x, y, z] = g;
        if (!X.includes(x)) throw new Error(`Triple {${g.join(",")}}: ${x} is not in X.`);
        if (!Y.includes(y)) throw new Error(`Triple {${g.join(",")}}: ${y} is not in Y.`);
        if (!Z.includes(z)) throw new Error(`Triple {${g.join(",")}}: ${z} is not in Z.`);
        return { id: "T" + (i + 1), x, y, z };
      });
      if (!M.length) throw new Error("Add at least one triple after {X}{Y}{Z}.");
      if (M.length > 30 || X.length > 10 || Y.length > 10 || Z.length > 10) throw new Error("This mockup draws up to 10 names per set and 30 triples.");
      return { X, Y, Z, M };
    }
    const keys = t => ["x:" + t.x, "y:" + t.y, "z:" + t.z];
    const ids = (D, c) => "{" + c.map(i => D.M[i].id).join(",") + "}";
    const tri = t => `${t.id} (${t.x}, ${t.y}, ${t.z})`;

    // Redux's solver: every way to pick |X| triples from M, in order, until one passes the verifier.
    function brute(D) {
      const n = D.X.length, m = D.M.length, frames = [];
      if (n > m) {
        frames.push({ chosen: [], done: true, ok: false, caption: `M has only ${m} triples, but a matching needs ${n}, one per element of X. No combination exists to try.` });
        return frames;
      }
      function* gen(start, acc) { if (acc.length === n) { yield acc.slice(); return; } for (let i = start; i <= m - (n - acc.length); i++) { acc.push(i); yield* gen(i + 1, acc); acc.pop(); } }
      let tried = 0, found = null;
      for (const c of gen(0, [])) {
        tried++;
        const cnt = new Map();
        c.forEach(i => keys(D.M[i]).forEach(k => cnt.set(k, (cnt.get(k) || 0) + 1)));
        const rep = [...cnt].filter(([, v]) => v > 1).map(([k]) => k.slice(2));
        const ok = !rep.length;
        if (tried <= SHOW) frames.push({ chosen: c, trial: !ok,
          caption: ok ? `Try ${ids(D, c)}. No name repeats, so the verifier accepts it.` : `Try ${ids(D, c)}: ${rep.slice(0, 3).join(", ")} ${rep.length > 1 ? "are" : "is"} used twice.` });
        if (ok) { found = c; break; }
      }
      const hidden = tried > SHOW ? ` (${tried - SHOW} tries not shown)` : "";
      if (found) {
        const short = D.Y.length !== n || D.Z.length !== n;
        frames.push({ chosen: found, done: true, ok: !short,
          caption: short ? `${ids(D, found)} repeats no name, so Redux's verifier accepts it after ${tried} tries${hidden}. But X, Y and Z differ in size, so some of Y or Z is never matched.`
            : `${ids(D, found)} uses every element of X, Y and Z exactly once. Found after ${tried} tries${hidden}.` });
      } else frames.push({ chosen: [], done: true, ok: false, caption: `Checked all ${tried} ways to pick ${n} of the ${m} triples${hidden}. Every one repeats a name, so there's no perfect matching.` });
      return frames;
    }

    // Not in Redux: take the first unmatched element of X, try each triple that still fits, back up on a dead end.
    function backtrack(D) {
      const frames = [], chosen = [], used = new Set();
      let count = 0;
      function rec() {
        const x = D.X.find(v => !used.has("x:" + v));
        if (x === undefined) return true;
        const opts = D.M.map((_, i) => i).filter(i => D.M[i].x === x && !used.has("y:" + D.M[i].y) && !used.has("z:" + D.M[i].z));
        if (!opts.length) {
          if (++count <= SHOW) frames.push({ chosen: chosen.slice(), failed: "x:" + x, caption: `No triple is left for ${x} without reusing a name. Back up.` });
          return false;
        }
        for (const i of opts) {
          const t = D.M[i];
          chosen.push(i); keys(t).forEach(k => used.add(k));
          if (++count <= SHOW) frames.push({ chosen: chosen.slice(), trying: i, caption: `${x} has ${plural(opts.length, "triple", "triples")} that still ${opts.length > 1 ? "fit" : "fits"}. Try ${tri(t)}.` });
          if (rec()) return true;
          chosen.pop(); keys(t).forEach(k => used.delete(k));
        }
        return false;
      }
      const ok = rec() && D.Y.length === D.X.length && D.Z.length === D.X.length;
      frames.push(ok ? { chosen: chosen.slice(), done: true, ok, caption: `Every element of X, Y and Z is used exactly once: ${ids(D, chosen)}.` }
        : { chosen: [], done: true, ok: false, caption: chosen.length ? "X is covered, but Y or Z has elements no triple can reach." : "Every branch dead-ends. No perfect matching exists." });
      return frames;
    }

    function draw(svg, D, api) {
      svg.innerHTML = "";
      const cols = [D.X, D.Y, D.Z], heads = ["X", "Y", "Z"], pre = ["x:", "y:", "z:"];
      const widths = cols.map(c => Math.max(48, ...c.map(v => v.length * 7.6 + 22)));
      const ROW = 46, TROW = 22, top = 64;
      const rows = Math.max(...cols.map(c => c.length));
      const H = Math.max(rows * ROW, D.M.length * TROW) + top + 22;
      const listX = 560, W = 790;
      const cx = [80, 270, 460];
      svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
      const pos = new Map();
      cols.forEach((c, k) => {
        const y0 = top + (rows * ROW - c.length * ROW) / 2 + ROW / 2;
        c.forEach((v, i) => pos.set(pre[k] + v, { x: cx[k], y: y0 + i * ROW, w: widths[k] }));
      });
      heads.forEach((h, k) => mk("text", { x: cx[k], y: 28, class: "ms-colhead", "text-anchor": "middle" }, svg).textContent = h);
      mk("text", { x: listX, y: 28, class: "ms-colhead" }, svg).textContent = "TRIPLES · M";
      const gT = mk("g", {}, svg), gN = mk("g", {}, svg), gR = mk("g", {}, svg);
      const m = D.M.length, lane = Math.min(3, 18 / Math.max(1, m));
      const tris = D.M.map((t, i) => {
        const [a, b, c] = keys(t).map(k => pos.get(k)), o = (i - (m - 1) / 2) * lane;
        const d = `M${a.x + a.w / 2},${a.y + o} L${b.x - b.w / 2},${b.y + o} M${b.x + b.w / 2},${b.y + o} L${c.x - c.w / 2},${c.y + o}`;
        const path = mk("path", { d, class: "ms-tri" }, gT);
        const g = mk("g", { class: "ms-trow", tabindex: "0", role: "button", "aria-label": tri(t) }, gR);
        const y = top + 4 + i * TROW;
        mk("rect", { x: listX - 6, y: y - 9, width: W - listX - 4, height: 18, rx: 5 }, g);
        mk("text", { x: listX, y, class: "tid" }, g).textContent = t.id;
        mk("text", { x: listX + 30, y }, g).textContent = `${t.x} · ${t.y} · ${t.z}`;
        hook(g, "t:" + i, api);
        return { path, g };
      });
      const els = new Map();
      for (const [k, p] of pos) {
        const g = mk("g", { class: "ms-el", tabindex: "0", role: "button", "aria-label": `${k[0].toUpperCase()} element ${k.slice(2)}` }, gN);
        mk("rect", { x: p.x - p.w / 2, y: p.y - 14, width: p.w, height: 28, rx: 14, class: "body" }, g);
        mk("text", { x: p.x, y: p.y }, g).textContent = k.slice(2);
        const badge = mk("g", { class: "ms-badge", visibility: "hidden" }, g);
        mk("circle", { cx: p.x + p.w / 2 - 2, cy: p.y - 14, r: 8 }, badge);
        const bt = mk("text", { x: p.x + p.w / 2 - 2, y: p.y - 14 }, badge);
        hook(g, k, api);
        els.set(k, { g, badge, bt });
      }
      return { tris, els };
    }

    function paint(E, D, f, hover) {
      const chosen = new Set(f ? f.chosen : []);
      const cnt = new Map();
      chosen.forEach(i => keys(D.M[i]).forEach(k => cnt.set(k, (cnt.get(k) || 0) + 1)));
      const hk = hover === null ? null : String(hover);
      const hoverTri = hk && hk.startsWith("t:") ? +hk.slice(2) : null;
      const near = new Set();
      if (hk && !hk.startsWith("t:")) D.M.forEach((t, i) => { if (keys(t).includes(hk)) { near.add("t:" + i); keys(t).forEach(k => near.add(k)); } });
      if (hoverTri !== null) keys(D.M[hoverTri]).forEach(k => near.add(k));
      const tryKeys = f && f.trying !== undefined && !f.done ? keys(D.M[f.trying]) : [];
      for (const [k, o] of E.els) {
        const n = cnt.get(k) || 0;
        let st = "Background";
        if (f) {
          if (k === f.failed || n > 1) st = "Rejected";
          else if (tryKeys.includes(k)) st = "Active";
          else if (n === 1) st = "Covered";
        }
        let cls = "ms-el " + st;
        if (hk) cls += hk === k ? " trace" : near.has(k) ? "" : " faint";
        o.g.setAttribute("class", cls);
        o.badge.setAttribute("visibility", n > 1 ? "visible" : "hidden");
        if (n > 1) o.bt.textContent = "×" + n;
      }
      E.tris.forEach((o, i) => {
        const t = D.M[i];
        let st = "";
        if (f) {
          if (chosen.has(i)) {
            const clash = keys(t).some(k => (cnt.get(k) || 0) > 1);
            st = f.done ? (f.ok ? "sol" : "rej") : f.trial ? (clash ? "rej" : "hot") : i === f.trying ? "hot" : "sol";
          } else if (f.done) st = chosen.size ? "dim" : "";
          else if (keys(t).some(k => cnt.get(k))) st = "blk";
        }
        let cls = "ms-tri" + (st ? " " + st : ""), rcls = "ms-trow" + (st ? " " + st : "");
        if (hk) {
          const on = hoverTri === i || near.has("t:" + i);
          cls = "ms-tri " + (on ? "trace" : "faint"); rcls += on ? " trace" : " faint";
        }
        o.path.setAttribute("class", cls); o.g.setAttribute("class", rcls);
      });
    }

    function checks(D, f) {
      const n = D.X.length, same = D.Y.length === n && D.Z.length === n;
      const sizes = { label: `Sizes: |X| = ${n}, |Y| = ${D.Y.length}, |Z| = ${D.Z.length}`, ok: same };
      if (!f) return [sizes];
      const cnt = new Map();
      f.chosen.forEach(i => keys(D.M[i]).forEach(k => cnt.set(k, (cnt.get(k) || 0) + 1)));
      const once = (S, p) => S.filter(v => cnt.get(p + v) === 1).length;
      const live = !!f.chosen.length || f.done;
      const row = (S, p, name) => ({ label: `${name} used exactly once: ${once(S, p)} / ${S.length}`, ok: live ? once(S, p) === S.length && ![...cnt].some(([k, v]) => k.startsWith(p) && v > 1) : null });
      return [
        sizes,
        { label: `Triples chosen: ${f.chosen.length} (a matching needs ${n})`, ok: live ? f.chosen.length === n : null },
        row(D.X, "x:", "X"), row(D.Y, "y:", "Y"), row(D.Z, "z:", "Z"),
      ];
    }
    function chosen(D, f) { return f ? f.chosen.map(i => ({ text: tri(D.M[i]) })) : []; }
    return { parse, solve: (D, s) => (s === "bt" ? backtrack(D) : brute(D)), draw, paint, checks, chosen, step0: true, shape: "a set of triples" };
  })();

  /* =============================== Lossless Data Compression · Code Tree =============================== */
  const LDC = (() => {
    const disp = c => (c === " " ? "␣" : c === "\n" ? "↵" : c === "\t" ? "⇥" : c);
    function parse(str) {
      const text = str;
      if (!text.length) throw new Error("Type some text to compress.");
      if (text.length > 240) throw new Error("This mockup compresses up to 240 characters.");
      if (new Set(text.split("")).size > 40) throw new Error("This mockup draws up to 40 different characters.");
      return { text };
    }
    // A port of Redux's LosslessDataCompressionSolver (Huffman), including its tie-breaking, recording each merge.
    function huffman(D) {
      const text = D.text, freq = new Map();
      for (let i = 0; i < text.length; i++) freq.set(text[i], (freq.get(text[i]) || 0) + 1);
      const chars = [...freq.keys()].sort((a, b) => a.charCodeAt(0) - b.charCodeAt(0));
      const nodes = [];
      let serial = 0;
      const queue = chars.map(c => { const n = { id: nodes.length, ch: c, w: freq.get(c), low: c.charCodeAt(0), serial: serial++, level: 0 }; nodes.push(n); return n; });
      const label = n => (n.ch !== null ? `"${disp(n.ch)}" (${n.w})` : `a tree of weight ${n.w}`);
      const frames = [{ built: 0, caption: `Count each character: ${plural(text.length, "character", "characters")}, ${chars.length} different. Each starts as its own one-node tree, weighted by how often it appears.` }];
      const cmp = (a, b) => a.w - b.w || a.low - b.low || a.serial - b.serial;
      let built = 0;
      while (queue.length > 1) {
        queue.sort(cmp);
        const [a, b] = queue.splice(0, 2);
        const tie = queue.length && queue[0].w === b.w;
        const p = { id: nodes.length, ch: null, w: a.w + b.w, low: Math.min(a.low, b.low), serial: serial++, left: a.id, right: b.id, level: 1 + Math.max(a.level, b.level), order: ++built };
        nodes.push(p); queue.push(p);
        frames.push({ built, merging: [a.id, b.id], parent: p.id,
          caption: `Merge ${label(a)} and ${label(b)}, the two lightest trees left, into one of weight ${p.w}.${tie ? " (A tie: the tree holding the earliest character goes first.)" : ""}` });
      }
      const root = queue[0];
      const codes = new Map();
      (function walk(id, code) {
        const n = nodes[id];
        if (n.ch !== null) { n.code = code || "0"; codes.set(n.ch, n.code); return; }
        walk(n.left, code + "0"); walk(n.right, code + "1");
      })(root.id, "");
      const bits = text.split("").reduce((s, c) => s + codes.get(c).length, 0);
      D.nodes = nodes; D.root = root.id; D.codes = codes; D.freq = freq; D.bits = bits;
      D.encoded = text.split("").map(c => codes.get(c)).join("");
      frames.push({ built, codes: true, caption: chars.length === 1 ? `Only one character, so its code is just "0".` : "Read each code from the top: going left adds 0, going right adds 1. The most frequent characters sit near the top and get the shortest codes." });
      const fixed = 8 * text.length;
      frames.push({ built, codes: true, done: true, ok: true,
        caption: `Encoded: ${bits} bits, versus ${fixed} bits at 8 bits per character, ${Math.round((1 - bits / fixed) * 100)}% smaller. No code is the start of another, so the bits decode back to exactly the input.` });
      return frames;
    }

    function draw(svg, D, api) {
      svg.innerHTML = "";
      const N = D.nodes, rootLevel = N[D.root].level, SLOT = 42, LH = 52, top = 36;
      const leaves = [];
      (function inorder(id) { const n = N[id]; if (n.ch !== null) { leaves.push(id); return; } inorder(n.left); inorder(n.right); })(D.root);
      const W = Math.max(680, leaves.length * SLOT + 60);
      const x = new Map(), y = new Map();
      const left0 = (W - leaves.length * SLOT) / 2 + SLOT / 2;
      leaves.forEach((id, i) => x.set(id, left0 + i * SLOT));
      (function place(id) { const n = N[id]; if (n.ch === null) { place(n.left); place(n.right); x.set(id, (x.get(n.left) + x.get(n.right)) / 2); } y.set(id, top + (rootLevel - n.level) * LH + 20); })(D.root);
      const maxCode = Math.max(...[...D.codes.values()].map(c => c.length));
      const leafY = top + rootLevel * LH + 20, codeTop = leafY + 30;
      // encoded strip: one cell per input character, wrapped into rows
      const cells = [], text = D.text;
      let cxp = 30, row = 0;
      const stripTop = codeTop + maxCode * 12 + 46, CH = 40;
      for (let i = 0; i < text.length; i++) {
        const code = D.codes.get(text[i]), w = Math.max(16, code.length * 6.4 + 8);
        if (cxp + w > W - 30) { row++; cxp = 30; }
        cells.push({ i, ch: text[i], code, x: cxp, y: stripTop + row * (CH + 6), w });
        cxp += w + 2;
      }
      const H = stripTop + (row + 1) * (CH + 6) + 16;
      svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
      const gE = mk("g", {}, svg), gB = mk("g", {}, svg), gN = mk("g", {}, svg), gC = mk("g", {}, svg), gS = mk("g", {}, svg);
      mk("text", { x: 30, y: 20, class: "ms-cap" }, svg).textContent = "CODE TREE";
      const capS = mk("text", { x: 30, y: stripTop - 12, class: "ms-cap" }, svg);
      const edges = [];
      N.forEach(n => {
        if (n.ch !== null) return;
        [[n.left, "0"], [n.right, "1"]].forEach(([c, bit]) => {
          const x1 = x.get(n.id), y1 = y.get(n.id), x2 = x.get(c), y2 = y.get(c);
          const line = mk("line", { x1, y1, x2, y2, class: "ms-te" }, gE);
          const mx = x1 + (x2 - x1) * 0.45, my = y1 + (y2 - y1) * 0.45;
          const t = mk("text", { x: mx + (bit === "0" ? -9 : 9), y: my - 4, class: "ms-bit" }, gB);
          t.textContent = bit;
          edges.push({ parent: n.id, child: c, line, t });
        });
      });
      const nodeEls = new Map();
      N.forEach(n => {
        const g = mk("g", { class: "ms-tn", tabindex: "0", role: "button", "aria-label": n.ch !== null ? `Character ${disp(n.ch)}, appears ${n.w} times` : `Merged tree of weight ${n.w}` }, gN);
        const cxn = x.get(n.id), cyn = y.get(n.id);
        if (n.ch !== null) {
          mk("rect", { x: cxn - 16, y: cyn - 19, width: 32, height: 38, rx: 7, class: "body" }, g);
          mk("text", { x: cxn, y: cyn - 7, class: "ch" }, g).textContent = disp(n.ch);
          mk("text", { x: cxn, y: cyn + 9, class: "w" }, g).textContent = n.w;
        } else {
          mk("circle", { cx: cxn, cy: cyn, r: 15, class: "body" }, g);
          mk("text", { x: cxn, y: cyn, class: "w", style: "font-size:11.5px;font-weight:700" }, g).textContent = n.w;
        }
        hook(g, "n:" + n.id, api);
        nodeEls.set(n.id, g);
      });
      const codeEls = new Map();
      leaves.forEach(id => {
        const g = mk("g", { class: "ms-code" }, gC);
        N[id].code.split("").forEach((b, k) => { mk("text", { x: x.get(id), y: codeTop + k * 12 }, g).textContent = b; });
        codeEls.set(id, g);
      });
      const cellEls = cells.map((c, k) => {
        const g = mk("g", { class: "ms-cell" + (k % 2 ? " alt" : "") }, gS);
        mk("rect", { x: c.x, y: c.y, width: c.w, height: CH, rx: 4 }, g);
        mk("text", { x: c.x + c.w / 2, y: c.y + 12, class: "c" }, g).textContent = disp(c.ch);
        const b = mk("text", { x: c.x + c.w / 2, y: c.y + 29, class: "b" }, g);
        b.textContent = c.code;
        hook(g, "c:" + c.ch, api);
        return { g, b, ch: c.ch, alt: k % 2 };
      });
      const leafOf = new Map(leaves.map(id => [N[id].ch, id]));
      return { N, edges, nodeEls, codeEls, cellEls, capS, leafOf };
    }

    function paint(E, D, f, hover) {
      const N = D.nodes, built = f ? f.built : N.length;
      const visible = n => n.ch !== null || (n.order || 0) <= built;
      const parentOf = new Map();
      N.forEach(n => { if (n.ch === null && visible(n)) { parentOf.set(n.left, n.id); parentOf.set(n.right, n.id); } });
      const codesOn = !f || !!f.codes;
      // hover: a leaf (or a cell holding that character) lights its path to the root
      let leaf = null;
      if (hover !== null) {
        const h = String(hover);
        if (h.startsWith("c:")) leaf = E.leafOf.get(h.slice(2));
        else if (h.startsWith("n:") && N[+h.slice(2)].ch !== null) leaf = +h.slice(2);
      }
      const path = new Set();
      if (leaf !== null) { let v = leaf; path.add(v); while (parentOf.has(v)) { v = parentOf.get(v); path.add(v); } }
      N.forEach(n => {
        let st = "Background";
        if (f) {
          if (f.merging && (f.merging.includes(n.id) || f.parent === n.id)) st = "Active";
          else if (f.codes) st = n.ch !== null ? "Solution" : "Covered";
          else if (parentOf.has(n.id)) st = "Covered";
        }
        let cls = "ms-tn " + st + (visible(n) ? "" : " hidden");
        if (leaf !== null) cls += path.has(n.id) ? " trace" : " faint";
        E.nodeEls.get(n.id).setAttribute("class", cls);
      });
      E.edges.forEach(e => {
        const vis = visible(N[e.parent]);
        let cls = "ms-te" + (vis ? "" : " hidden");
        if (vis && f && f.parent === e.parent) cls += " hot";
        else if (vis && f && f.codes) cls += " sol";
        if (vis && leaf !== null && path.has(e.parent) && path.has(e.child)) cls = "ms-te trace";
        e.line.setAttribute("class", cls);
        e.t.setAttribute("class", "ms-bit" + (vis ? "" : " hidden") + (vis && leaf !== null && path.has(e.parent) && path.has(e.child) ? " trace" : ""));
      });
      E.codeEls.forEach((g, id) => g.setAttribute("class", "ms-code" + (codesOn ? "" : " hidden")));
      const hch = leaf !== null ? N[leaf].ch : null;
      E.cellEls.forEach(c => {
        c.g.setAttribute("class", "ms-cell" + (c.alt ? " alt" : "") + (hch !== null && c.ch === hch ? " trace" : ""));
        c.b.setAttribute("class", "b" + (codesOn ? "" : " hidden"));
      });
      E.capS.textContent = codesOn ? `ENCODED · ${D.bits} BITS` : "INPUT";
    }

    function checks(D, f) {
      const L = D.text.length, k = D.freq.size;
      if (!f || !f.codes) {
        const left = f ? k - f.built : k;
        return [{ label: `Characters: ${L}, different: ${k}`, ok: null }, { label: `Trees left to merge: ${left}`, ok: null }];
      }
      const codes = [...D.codes.values()];
      const prefixFree = codes.every((a, i) => codes.every((b, j) => i === j || !b.startsWith(a)));
      const rev = new Map([...D.codes].map(([c, b]) => [b, c]));
      let cur = "", out = "";
      for (const bit of D.encoded) { cur += bit; if (rev.has(cur)) { out += rev.get(cur); cur = ""; } }
      const decodes = cur === "" && out === D.text;
      return [
        { label: "Prefix-free: no code is the start of another", ok: prefixFree },
        { label: "The bits decode back to the input", ok: decodes },
        { label: `Size: ${D.bits} bits vs ${8 * L} at 8 bits each`, ok: f.done ? D.bits <= 8 * L : null },
        { label: `Average: ${(D.bits / L).toFixed(2)} bits per character`, ok: null },
      ];
    }
    function chosen(D, f) {
      if (!f || !f.codes) return [];
      return [...D.codes].sort((a, b) => D.freq.get(b[0]) - D.freq.get(a[0]) || a[0].charCodeAt(0) - b[0].charCodeAt(0))
        .map(([c, code]) => ({ text: `${disp(c)} ${code}` }));
    }
    return { parse, solve: D => huffman(D), draw, paint, checks, chosen, shape: "a code table plus the encoded bits" };
  })();

  const IMPL = { CONVEXHULL: HULL, DM3, LDC };

  const circle12 = "{" + Array.from({ length: 12 }, (_, i) => `(${+Math.cos(i * Math.PI / 6).toFixed(3)},${+Math.sin(i * Math.PI / 6).toFixed(3)})`).join(",") + "}";
  const PROBLEMS = {
    CONVEXHULL: { label: "Convex Hull", cls: "P", type: "Geometry", vizType: "Geometry",
      def: "Given a set of points in the plane, compute the smallest convex polygon that contains all the points. The output is typically given as the vertices of the polygon in clockwise order.",
      input: "{(x,y), …}", inputLong: "A set of points {(x,y), …}",
      solvers: [["dc", "Convex Hull Divide and Conquer"], ["chain", "Monotone Chain (not in Redux yet)"]],
      examples: [
        ["Redux default", "{(-0.49429993857731547,-0.14539088193288174),(0.7674622377407927,-0.21537444528240846),(-0.9308276577586132,-0.1423800479224624),(-0.6984449872706371,0.3857380723376367),(-0.27394905790800017,-0.7488048223660126),(0.6681904385614421,-0.11341930745315643),(0.6077591838324792,0.5288040272918157),(0.32371432910023556,0.12231914413229394),(-0.32705115386597394,0.6744065707101621),(0.2723211656942368,-0.8053758131859647)}"],
        ["A square with points inside", "{(0,0),(4,0),(4,4),(0,4),(1,1),(2,3),(3,2),(2,2),(1,3)}"],
        ["Twelve points on a circle", circle12],
        ["Points on a line, plus one above", "{(0,0),(1,0),(2,0),(3,0),(1.5,2)}"],
        ["Repeated x values (shows a Redux bug)", "{(4.75,0.75),(4,0.25),(6.5,1.75),(4.75,4.75),(9.75,4.5),(1.75,4.5),(4.25,7.25),(4.5,5.25),(4.75,2.75),(6.75,2.25)}"],
      ] },
    DM3: { label: "3-Dimensional Matching", cls: "NP-Complete", type: "Sets", vizType: "Tripartite Matching",
      def: "3-Dimensional Matching is when, given 3 equally sized sets, X, Y, and Z, and a set of constraints M, being a subset of XxYxZ, are you able to select a set of constraints which contain each element of X, Y, and Z in one and only one 3-tuple.",
      input: "{X}{Y}{Z}{x,y,z}…", inputLong: "{X}{Y}{Z} followed by one {x,y,z} per triple in M",
      solvers: [["brute", "3-Dimensional Matching Brute Force"], ["bt", "Backtracking (not in Redux yet)"]],
      examples: [
        ["Redux default", "{Paul,Sally,Dave}{Madison,Austin,Bob}{Chloe,Frank,Jake}{Paul,Madison,Chloe}{Paul,Austin,Jake}{Sally,Bob,Chloe}{Sally,Madison,Frank}{Dave,Austin,Chloe}{Dave,Bob,Chloe}"],
        ["Needs backtracking", "{Ann,Ben,Cal}{Dee,Eve,Fay}{Gus,Hal,Ian}{Ann,Dee,Gus}{Ann,Eve,Ian}{Ben,Fay,Hal}{Ben,Dee,Hal}{Cal,Fay,Gus}{Cal,Eve,Hal}"],
        ["No perfect matching", "{a,b,c}{d,e,f}{g,h,i}{a,d,g}{b,d,h}{c,e,i}{a,f,h}{b,e,g}"],
      ] },
    LDC: { label: "Lossless Data Compression", cls: "P", type: "Strings", vizType: "Code Tree",
      def: "Lossless Data Compression is the problem of reducing the size of data while still allowing the original data to be perfectly reconstructed. For this Redux contribution, the selected algorithm is Huffman Encoding. Huffman Encoding builds a prefix-free binary code where characters that appear more often usually receive shorter codes, and characters that appear less often usually receive longer codes.",
      input: "S, any text", inputLong: "S, a raw text string to compress",
      solvers: [["huffman", "Huffman's Algorithm"]],
      examples: [
        ["Redux default", "this is an example of lossless data compression using huffman encoding"],
        ["Few different letters", "abracadabra"],
        ["Every letter equally often", "abcdabcdabcdabcd"],
      ] },
  };

  function create({ svg }) {
    if (!document.getElementById("ms-style")) {
      const st = document.createElement("style"); st.id = "ms-style"; st.textContent = css; document.head.appendChild(st);
    }
    svg.classList.add("ms-svg");
    let impl = null, model = null, frames = [], frame = null, hover = null, els = null;
    const paint = () => { if (impl) impl.paint(els, model, frame, hover); };
    const api = { set: h => { hover = h; paint(); }, get: () => hover };
    return {
      load(str, key, solver) {
        const P = PROBLEMS[key];
        if (!P) throw new Error("Unknown problem " + key);
        const im = IMPL[key], m = im.parse(str);
        const fr = im.solve(m, solver || P.solvers[0][0]);
        if (im.step0) fr.unshift({ chosen: [], caption: "Step 0: nothing chosen yet. Step forward to watch the solver." });
        impl = im; model = m; frames = fr; frame = null; hover = null;
        els = impl.draw(svg, model, api);
        paint();
        return { frames, ok: frames[frames.length - 1].ok };
      },
      show(i) { frame = i === null ? null : frames[i]; paint(); return frame; },
      checks(i) { return impl.checks(model, i === null ? null : frames[i]); },
      chosen(i) { return impl.chosen(model, i === null ? null : frames[i]); },
      shape(key) { return IMPL[key].shape; },
      resetHover() { hover = null; paint(); },
    };
  }

  return { create, parse: (str, key) => IMPL[key].parse(str), PROBLEMS, _impl: IMPL };
})();
