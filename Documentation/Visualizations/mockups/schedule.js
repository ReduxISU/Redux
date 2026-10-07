/* ---- Schedule view: time on the x axis, drawn to scale ----
   Job Sequencing: one row per job, each job's run as a bar, its deadline as a tick, late runs in red.
   Pump Scheduling: a 24-hour Gantt row per pump, the tank level under it, and the cost of each hour.
   Pages supply --av-* tokens. Frames are in the problem's own terms; the view derives every color. */
const ScheduleView = (() => {
  const NS = "http://www.w3.org/2000/svg";
  const mk = (tag, attrs = {}, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; };
  const txt = (parent, x, y, s, attrs = {}) => { const t = mk("text", { x, y, ...attrs }, parent); t.textContent = s; return t; };
  const SHOW = 120, HOURS = 24;
  const num = v => Math.round(v).toLocaleString("en-US");
  const money = v => "$" + v.toFixed(2);

  const css = `
.sc-svg { width: 100%; height: auto; display: block; }
.sc-svg text { font-family: var(--av-mono); fill: var(--av-ink); }
.sc-svg .sc-muted { fill: var(--av-muted); }
.sc-svg .sc-small { font-size: 11px; }
.sc-svg .sc-label { font-size: 12.5px; }
.sc-svg .sc-head { font-size: 11px; font-weight: 600; letter-spacing: .06em; fill: var(--av-muted); }
.sc-svg .sc-read { font-size: 12.5px; }
.sc-axis { stroke: var(--av-line); stroke-width: 1; }
.sc-grid { stroke: var(--av-line); stroke-width: 1; opacity: .55; }
.sc-peak { fill: var(--av-line); opacity: .35; }
.sc-bar { stroke-width: 1.6; transition: fill .2s, stroke .2s; }
.sc-bar.Background { fill: var(--av-bg); stroke: var(--av-stroke); }
.sc-bar.Covered { fill: var(--av-sol-fill); stroke: var(--av-sol); }
.sc-bar.Solution { fill: var(--av-sol); stroke: var(--av-sol); }
.sc-bar.Active { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 2.6; }
.sc-bar.Rejected { fill: var(--av-rej-fill); stroke: var(--av-rej); stroke-width: 2.2; }
.sc-over { fill: var(--av-rej); opacity: .35; }
.sc-dead { stroke: var(--av-ink); stroke-width: 2; }
.sc-dead-late { stroke: var(--av-rej); }
.sc-ghost { fill: none; stroke: var(--av-line); stroke-dasharray: 4 3; }
.sc-row.hot rect.sc-rowbg { fill: var(--av-hl-fill); opacity: .5; }
.sc-rowbg { fill: transparent; }
.sc-cell { fill: transparent; stroke: var(--av-line); stroke-width: .6; }
.sc-cell.on { fill: var(--av-sol); stroke: var(--av-sol); }
.sc-cell.on.dim { opacity: .35; }
.sc-start { fill: var(--av-ink); }
.sc-col { fill: none; stroke: var(--av-hl); stroke-width: 2.4; }
.sc-colhover { fill: var(--av-hl-fill); opacity: .45; }
.sc-tank { fill: none; stroke: var(--av-ink); stroke-width: 2.4; stroke-linejoin: round; }
.sc-tankpt { fill: var(--av-ink); }
.sc-tankbad { fill: var(--av-rej); }
.sc-band { fill: var(--av-hl-fill); stroke: var(--av-hl); stroke-width: 1.2; }
.sc-limit { stroke: var(--av-muted); stroke-width: 1.2; stroke-dasharray: 5 4; }
.sc-limit-min { stroke: var(--av-rej); }
.sc-costbar { fill: var(--av-muted); opacity: .55; }
.sc-costbar.cur { fill: var(--av-hl); opacity: 1; }
.sc-costbar.start { fill: var(--av-ink); opacity: .85; }
.sc-budget { fill: none; stroke: var(--av-hot); stroke-width: 2; }
.sc-hit { fill: transparent; cursor: crosshair; }
@media (prefers-reduced-motion: reduce) { .sc-bar { transition: none; } }`;

  /* ---------- parsing: nested ( ) / { } tuples of atoms ---------- */
  function tree(str) {
    const toks = []; let buf = "";
    for (const ch of str) {
      if ("(){},".includes(ch)) { if (buf.trim()) toks.push(buf.trim()); buf = ""; toks.push(ch); }
      else buf += ch;
    }
    if (buf.trim()) toks.push(buf.trim());
    let i = 0;
    function val() {
      const t = toks[i];
      if (t === "(" || t === "{") {
        i++; const close = t === "(" ? ")" : "}", out = [];
        if (toks[i] === close) { i++; return out; }
        for (;;) {
          out.push(val());
          if (toks[i] === ",") { i++; continue; }
          if (toks[i] === close) { i++; return out; }
          throw new Error("Brackets don't match up.");
        }
      }
      if (t === undefined || ")},".includes(t)) throw new Error(t === undefined ? "The instance ends too early." : `Unexpected "${t}".`);
      i++; return t;
    }
    const v = val();
    if (i !== toks.length) throw new Error("Extra text after the instance.");
    return v;
  }
  const nums = (arr, what) => {
    if (!Array.isArray(arr)) throw new Error(`${what} must be a list in parentheses.`);
    return arr.map(x => { const n = Number(x); if (typeof x !== "string" || !Number.isFinite(n)) throw new Error(`${what} must hold only numbers.`); return n; });
  };

  function parseJob(str) {
    const t = tree(str);
    if (!Array.isArray(t) || t.length !== 4) throw new Error("Expected ((T),(D),(P),K): times, deadlines, penalties, then K.");
    const T = nums(t[0], "T"), D = nums(t[1], "D"), P = nums(t[2], "P"), K = Number(t[3]);
    if (!T.length) throw new Error("There must be at least one job.");
    if (T.length !== D.length || T.length !== P.length) throw new Error(`T, D and P must be the same length (got ${T.length}, ${D.length}, ${P.length}).`);
    if (T.some(x => x <= 0 || !Number.isInteger(x))) throw new Error("Every time in T must be a positive whole number.");
    if (D.some(x => x < 0) || P.some(x => x < 0)) throw new Error("Deadlines and penalties can't be negative.");
    if (!Number.isFinite(K) || K < 0) throw new Error("K must be a number, 0 or more.");
    if (T.length > 9) throw new Error("This mockup draws up to 9 jobs (brute force tries every order: 9! = 362,880).");
    return { kind: "job", T, D, P, K, n: T.length };
  }

  function parsePump(str, em) {
    const t = tree(str);
    const want = em ? 4 : 3;
    if (!Array.isArray(t) || t.length !== want) throw new Error(em ? "Instance must have 4 sections: (tank),(demand),(pumps),budget." : "Instance must have 3 sections: (tank),(demand),(pumps).");
    const tank = nums(t[0], "The tank");
    if (tank.length !== (em ? 3 : 2)) throw new Error(em ? "Tank section must have exactly 3 values: (capacity,currentLevel,minLevel)." : "Tank section must have exactly 2 values: (capacity,currentLevel).");
    const [cap, level, minLevel = 0] = tank;
    if (cap <= 0) throw new Error("Tank capacity must be positive.");
    if (cap > 50000) throw new Error("This mockup runs Redux's 1-gallon-bucket DP in the browser, so it caps the tank at 50,000.");
    if (em && (minLevel < 0 || minLevel >= cap)) throw new Error("Tank minimum level must be in [0, capacity).");
    if (level < minLevel || level > cap) throw new Error(em ? "Tank current level must be within [minLevel, capacity]." : "Tank current level must be within [0, capacity].");
    const d = t[1];
    if (!Array.isArray(d) || d.length !== 3) throw new Error("Demand section must have 3 sub-lists: (demands),(peak_hours),(rates).");
    const demand = nums(d[0], "Demand");
    if (demand.length !== HOURS) throw new Error(`Demand curve must have exactly 24 values; got ${demand.length}.`);
    const peaks = new Set(nums(d[1], "Peak hours"));
    for (const h of peaks) if (!Number.isInteger(h) || h < 0 || h > 23) throw new Error("Peak hours are 0-based hours, 0 to 23.");
    const rates = nums(d[2], "Rates");
    if (rates.length !== 2) throw new Error("Rate sub-list must have exactly 2 values: (on_peak_rate,off_peak_rate).");
    if (!Array.isArray(t[2]) || !t[2].length) throw new Error("At least one pump is required.");
    const pumps = t[2].map(p => {
      if (!Array.isArray(p) || p.length !== 4) throw new Error(`Each pump needs 4 fields (name,flow_gph,kw,startup_cost); got ${Array.isArray(p) ? p.length : 1}.`);
      const [name, ...rest] = p;
      if (typeof name !== "string") throw new Error("A pump's first field is its name.");
      const [flow, kw, start] = nums(rest, `Pump ${name}`);
      return { name, flow, kw, start };
    });
    if (pumps.length > 5) throw new Error("This mockup draws up to 5 pumps (the DP tries every on/off mix: 2^n of them).");
    const budget = em ? Number(t[3]) : null;
    if (em && (!Number.isFinite(budget) || budget < 0)) throw new Error("The budget must be a number, 0 or more (0 means 1.5 times the cheapest schedule).");
    return { kind: em ? "em" : "cm", cap, level, minLevel: em ? minLevel : 0, demand, peaks, onRate: rates[0], offRate: rates[1], pumps, budget };
  }

  /* ---------- Job Sequencing: shared timeline math ---------- */
  function jobTimeline(J, order) {
    let t = 0;
    return order.map(j => { const s = t; t += J.T[j]; return { j, s, f: t, late: t > J.D[j] }; });
  }
  const penaltyOf = (J, tl) => tl.reduce((a, r) => a + (r.late ? J.P[r.j] : 0), 0);
  const certOf = order => "(" + order.join(",") + ")";

  // Redux's JobSeqBruteForce: every permutation in lexicographic order, first one within K.
  function jobBrute(J) {
    const frames = [], a = J.T.map((_, i) => i);
    let tried = 0, found = null, best = null, bestPen = Infinity;
    const next = () => {
      let i = a.length - 2; while (i >= 0 && a[i] >= a[i + 1]) i--;
      if (i < 0) return false;
      let j = a.length - 1; while (a[j] <= a[i]) j--;
      [a[i], a[j]] = [a[j], a[i]];
      a.splice(i + 1, a.length - i - 1, ...a.slice(i + 1).reverse());
      return true;
    };
    do {
      tried++;
      const tl = jobTimeline(J, a), pen = penaltyOf(J, tl);
      if (pen < bestPen) { bestPen = pen; best = a.slice(); }
      const ok = pen <= J.K;
      if (!found && tried <= SHOW) {
        const late = tl.filter(r => r.late).map(r => r.j);
        frames.push({ order: a.slice(), trial: !ok, caption: ok
          ? `Try order ${certOf(a)}. Late penalty ${pen} is within K = ${J.K}. It works.`
          : `Try order ${certOf(a)}: job${late.length > 1 ? "s" : ""} ${late.join(", ")} finish late, penalty ${pen} > K = ${J.K}.` });
      }
      if (ok && !found) found = a.slice();
    } while (!found && next());
    // When nothing works, keep scanning (silently) so the caption can say what the best order would cost.
    if (!found) { while (next()) { const pen = penaltyOf(J, jobTimeline(J, a)); if (pen < bestPen) { bestPen = pen; best = a.slice(); } } }
    const hidden = tried > SHOW ? ` (${(tried - SHOW).toLocaleString("en-US")} tries not shown)` : "";
    frames.push(found
      ? { order: found, done: true, ok: true, caption: `${certOf(found)} is the first order that keeps the late penalty within K = ${J.K}, found after ${tried.toLocaleString("en-US")} tries${hidden}.` }
      : { order: best, done: true, ok: false, caption: `Checked all ${tried.toLocaleString("en-US")} orders${hidden}. None keeps the penalty within K = ${J.K}; the best possible is ${bestPen}, shown here.` });
    return frames;
  }

  // Not in Redux: take jobs by penalty, keep one only if every kept job still meets its deadline in deadline order.
  function jobGreedy(J) {
    const frames = [], kept = [], aside = [];
    const byPen = J.T.map((_, i) => i).sort((x, y) => J.P[y] - J.P[x] || J.D[x] - J.D[y] || x - y);
    const edf = s => s.slice().sort((x, y) => J.D[x] - J.D[y] || x - y);
    for (const j of byPen) {
      const cand = edf([...kept, j]);
      const tl = jobTimeline(J, cand), clash = tl.find(r => r.late);
      if (!clash) {
        kept.push(j);
        frames.push({ order: cand, aside: aside.slice(), trying: j, caption: `Job ${j} (penalty ${J.P[j]}) fits: in deadline order every kept job still finishes on time. Keep it.` });
      } else {
        aside.push(j);
        frames.push({ order: cand, aside: aside.slice(0, -1), trying: j, trial: true, caption: `Job ${j} (penalty ${J.P[j]}) would make job ${clash.j} finish at ${clash.f}, after its deadline ${J.D[clash.j]}. Set job ${j} aside to run at the end.` });
      }
    }
    const order = [...edf(kept), ...aside];
    const pen = penaltyOf(J, jobTimeline(J, order)), ok = pen <= J.K;
    frames.push({ order, done: true, ok, caption: ok
      ? `Kept jobs run in deadline order, set-aside jobs run last. Late penalty ${pen} is within K = ${J.K}.`
      : `Kept jobs run in deadline order, set-aside jobs run last. Late penalty ${pen} is more than K = ${J.K}. This greedy rule isn't exact, so try brute force.` });
    return frames;
  }

  /* ---------- Pump Scheduling: Redux's DP, run the same way (1-gallon buckets) ---------- */
  function pumpTables(I) {
    const n = I.pumps.length, M = 1 << n;
    const flow = new Float64Array(M), kw = new Float64Array(M);
    for (let m = 0; m < M; m++) for (let p = 0; p < n; p++) if (m & (1 << p)) { flow[m] += I.pumps[p].flow; kw[m] += I.pumps[p].kw; }
    const startCost = (prev, m) => { let c = 0; const s = ~prev & m & (M - 1); for (let p = 0; p < n; p++) if (s & (1 << p)) c += I.pumps[p].start; return c; };
    const energy = (m, h) => kw[m] * (I.peaks.has(h) ? I.onRate : I.offRate);
    return { n, M, flow, kw, startCost, energy };
  }

  // Simulate a schedule exactly (what Redux's verifiers do).
  function simulate(I, masks) {
    const tb = pumpTables(I);
    let level = I.level, prev = 0, total = 0, ok = true, low = I.level, high = I.level;
    const hours = masks.map((m, h) => {
      const e = tb.energy(m, h), s = tb.startCost(prev, m);
      total += e + s;
      const inflow = tb.flow[m];
      level = level + inflow - I.demand[h];
      if (level < I.minLevel || level > I.cap) ok = false;
      low = Math.min(low, level); high = Math.max(high, level);
      const startups = I.pumps.map((_, p) => !!(m & (1 << p)) && !(prev & (1 << p)));
      prev = m;
      return { m, energy: e, startup: s, cost: e + s, inflow, level, startups };
    });
    return { hours, total, ok, low, high, stored: hours.reduce((a, r) => a + r.level, 0) };
  }

  // Cost minimization (PumpSchedulingCMSolver), with per-hour frontier stats kept instead of thrown away.
  function cmDP(I, minLevel) {
    const tb = pumpTables(I), B = Math.ceil(I.cap) + 1, M = tb.M, S = B * M;
    const toB = l => Math.min(B - 1, Math.max(0, Math.round(l)));
    let dp = new Float64Array(S).fill(Infinity);
    const parB = Array.from({ length: HOURS + 1 }, () => new Int32Array(S).fill(-1));
    const parM = Array.from({ length: HOURS + 1 }, () => new Int8Array(S).fill(-1));
    dp[toB(I.level) * M] = 0;
    const stats = [];
    for (let h = 0; h < HOURS; h++) {
      const next = new Float64Array(S).fill(Infinity), d = I.demand[h];
      for (let b = 0; b < B; b++) for (let pm = 0; pm < M; pm++) {
        const c = dp[b * M + pm]; if (c === Infinity) continue;
        for (let m = 0; m < M; m++) {
          const nl = b - d + tb.flow[m];
          if (nl < minLevel || nl > I.cap) continue;
          const nb = toB(nl), cand = c + tb.energy(m, h) + tb.startCost(pm, m), k = nb * M + m;
          if (cand < next[k]) { next[k] = cand; parB[h + 1][k] = b; parM[h + 1][k] = pm; }
        }
      }
      dp = next;
      let count = 0, lo = Infinity, hi = -Infinity, best = Infinity;
      for (let b = 0; b < B; b++) for (let m = 0; m < M; m++) { const c = dp[b * M + m]; if (c < Infinity) { count++; if (b < lo) lo = b; if (b > hi) hi = b; if (c < best) best = c; } }
      stats.push({ h, count, lo, hi, best });
      if (!count) break;
    }
    let best = Infinity, bk = -1;
    if (stats.length === HOURS) for (let k = 0; k < S; k++) if (dp[k] < best) { best = dp[k]; bk = k; }
    if (bk < 0) return { stats, masks: null, cost: Infinity };
    const masks = new Array(HOURS);
    let b = Math.floor(bk / M), m = bk % M;
    for (let h = HOURS; h > 0; h--) { masks[h - 1] = m; const k = b * M + m; const pb = parB[h][k], pm = parM[h][k]; b = pb; m = pm; }
    return { stats, masks, cost: best };
  }

  // Emergency resilience (PumpSchedulingEMSolver): most stored water within the budget, same state space.
  function emDP(I, budget) {
    const tb = pumpTables(I), B = Math.ceil(I.cap) + 1, M = tb.M, S = B * M;
    const toB = l => Math.min(B - 1, Math.max(0, Math.round(l)));
    let cost = new Float64Array(S).fill(Infinity), score = new Float64Array(S).fill(-Infinity);
    const parB = Array.from({ length: HOURS + 1 }, () => new Int32Array(S).fill(-1));
    const parM = Array.from({ length: HOURS + 1 }, () => new Int8Array(S).fill(-1));
    const s0 = toB(I.level) * M; cost[s0] = 0; score[s0] = 0;
    const stats = [];
    for (let h = 0; h < HOURS; h++) {
      const nc = new Float64Array(S).fill(Infinity), ns = new Float64Array(S).fill(-Infinity), d = I.demand[h];
      for (let b = 0; b < B; b++) for (let pm = 0; pm < M; pm++) {
        const c = cost[b * M + pm]; if (c === Infinity) continue;
        const sc = score[b * M + pm];
        for (let m = 0; m < M; m++) {
          const nl = b - d + tb.flow[m];
          if (nl < I.minLevel || nl > I.cap) continue;
          const tc = c + tb.energy(m, h) + tb.startCost(pm, m);
          if (tc > budget) continue;
          const nb = toB(nl), k = nb * M + m, ts = sc + nl;
          if (ts > ns[k]) { nc[k] = tc; ns[k] = ts; parB[h + 1][k] = b; parM[h + 1][k] = pm; }
        }
      }
      cost = nc; score = ns;
      let count = 0, lo = Infinity, hi = -Infinity, best = -Infinity;
      for (let b = 0; b < B; b++) for (let m = 0; m < M; m++) { const k = b * M + m; if (cost[k] < Infinity) { count++; if (b < lo) lo = b; if (b > hi) hi = b; if (score[k] > best) best = score[k]; } }
      stats.push({ h, count, lo, hi, best });
      if (!count) break;
    }
    let best = -Infinity, bk = -1;
    if (stats.length === HOURS) for (let k = 0; k < S; k++) if (cost[k] < Infinity && score[k] > best) { best = score[k]; bk = k; }
    if (bk < 0) return { stats, masks: null };
    const masks = new Array(HOURS);
    let b = Math.floor(bk / M), m = bk % M;
    for (let h = HOURS; h > 0; h--) { masks[h - 1] = m; const k = b * M + m; const pb = parB[h][k], pm = parM[h][k]; b = pb; m = pm; }
    return { stats, masks };
  }

  const onNames = (I, m) => I.pumps.filter((_, p) => m & (1 << p)).map(p => p.name);

  function pumpFrames(I) {
    const frames = [];
    let res, budget = null, auto = false, fellBack = false;
    if (I.kind === "cm") res = cmDP(I, 0);
    else {
      budget = I.budget;
      if (budget <= 0) { auto = true; const pre = cmDP(I, I.minLevel); budget = (pre.masks ? pre.cost : 0) * 1.5; }
      res = emDP(I, budget);
    }
    const goal = I.kind === "cm" ? "cheapest" : "most-water";
    res.stats.forEach(st => {
      const what = I.kind === "cm" ? `cheapest so far ${money(st.best)}` : `most water stored so far ${num(st.best)} gal·h`;
      frames.push({ phase: "forward", h: st.h, stats: res.stats.slice(0, st.h + 1), budget,
        caption: st.count
          ? `Forward pass, hour ${st.h}: ${st.count.toLocaleString("en-US")} reachable (tank level, pumps on) states, levels ${num(st.lo)} to ${num(st.hi)} gal, ${what}.`
          : `Forward pass, hour ${st.h}: no state survives. Every pump mix either ${I.kind === "cm" ? "drains or overflows the tank" : "breaks the tank limits or the budget"}.` });
    });
    let masks = res.masks;
    if (!masks && I.kind === "em") {
      fellBack = true;
      masks = new Array(HOURS).fill((1 << I.pumps.length) - 1);
    }
    if (!masks) {
      frames.push({ phase: "done", done: true, ok: false, stats: res.stats, caption: "No schedule keeps the tank between empty and full for all 24 hours, so there is no answer." });
      return { frames, budget, auto, fellBack };
    }
    const sim = simulate(I, masks);
    const within = budget === null || sim.total <= budget + 0.01;
    const ok = sim.ok && within;
    frames.push({ phase: "trace", masks, sim, stats: res.stats, budget,
      caption: fellBack
        ? "No schedule fits the tank limits and the budget. Redux's solver then falls back to running every pump every hour, so this does too."
        : `Trace back from the ${goal} state at hour 24, following each state's parent, to recover the schedule.` });
    for (let h = 0; h < HOURS; h++) {
      const r = sim.hours[h], on = onNames(I, r.m);
      frames.push({ phase: "replay", masks, sim, cursor: h, budget, stats: res.stats,
        caption: `Hour ${h}${I.peaks.has(h) ? " (peak)" : ""}: ${on.length ? on.join(", ") : "no pumps"} on. In ${num(r.inflow)}, out ${num(I.demand[h])} gal, so the tank ends at ${num(r.level)} gal. Hour cost ${money(r.cost)}${r.startup ? `, including ${money(r.startup)} to start pumps` : ""}.` });
    }
    const summary = I.kind === "cm"
      ? `Cheapest schedule: ${money(sim.total)} for the day. The tank stays between ${num(sim.low)} and ${num(sim.high)} gal.`
      : fellBack ? `Fallback schedule: ${money(sim.total)} against a ${money(budget)} budget, and the tank ${sim.ok ? "stays within limits" : "breaks its limits"}. Redux returns it anyway.`
        : `Most water stored within the ${money(budget)} budget${auto ? " (1.5 times the cheapest schedule)" : ""}: ${num(sim.stored)} gal·h for ${money(sim.total)}.`;
    frames.push({ phase: "done", masks, sim, done: true, ok, budget, stats: res.stats, caption: summary });
    return { frames, budget, auto, fellBack };
  }

  /* ---------- drawing ---------- */
  function niceStep(span, target) {
    const raw = span / target, p = Math.pow(10, Math.floor(Math.log10(raw))), f = raw / p;
    return (f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10) * p;
  }

  function drawJob(svg, I, f, hover, setHover) {
    const W = 760, L = 128, R = 104, rowH = 34, top = 60;
    const order = f && f.order ? f.order : [];
    const tl = jobTimeline(I, order), at = new Map(tl.map(r => [r.j, r]));
    const span = Math.max(I.T.reduce((a, b) => a + b, 0), Math.max(...I.D)) || 1;
    const H = top + I.n * rowH + 46;
    svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
    const X = t => L + (t / span) * (W - L - R);
    const step = niceStep(span, 8);
    // readout
    const hj = hover !== null ? hover : null;
    if (hj !== null) {
      const r = at.get(hj);
      txt(svg, 16, 22, `Job ${hj} · takes ${I.T[hj]} · due ${I.D[hj]} · penalty ${I.P[hj]}${r ? ` · runs ${r.s}–${r.f} · ${r.late ? "late" : "on time"}` : " · not placed"}`, { class: "sc-read" });
    } else txt(svg, 16, 22, "Hover a job to read it. Bars are runs, the black tick is the deadline.", { class: "sc-read sc-muted" });
    txt(svg, 16, 46, "JOB", { class: "sc-head" });
    txt(svg, W - R + 12, 46, "PENALTY", { class: "sc-head" });
    // time grid
    for (let t = 0; t <= span; t += step) {
      mk("line", { x1: X(t), x2: X(t), y1: top - 6, y2: top + I.n * rowH, class: "sc-grid" }, svg);
      txt(svg, X(t), top + I.n * rowH + 16, String(t), { class: "sc-small sc-muted", "text-anchor": "middle" });
    }
    txt(svg, X(span / 2), top + I.n * rowH + 36, "time", { class: "sc-small sc-muted", "text-anchor": "middle" });
    const aside = new Set(f && f.aside ? f.aside : []);
    for (let j = 0; j < I.n; j++) {
      const y = top + j * rowH, g = mk("g", { class: "sc-row" + (hj === j ? " hot" : "") }, svg);
      mk("rect", { x: 0, y, width: W, height: rowH, class: "sc-rowbg" }, g);
      txt(g, 16, y + rowH / 2 + 4, `Job ${j}`, { class: "sc-label" });
      txt(g, 70, y + rowH / 2 + 4, `t=${I.T[j]}`, { class: "sc-small sc-muted" });
      const r = at.get(j);
      if (r) {
        let st = "Covered";
        if (r.late) st = "Rejected";
        else if (f.done) st = f.ok ? "Solution" : "Covered";
        if (j === f.trying || (f.trial && !r.late && !f.done)) st = r.late ? "Rejected" : "Active";
        mk("rect", { x: X(r.s), y: y + 7, width: Math.max(2, X(r.f) - X(r.s)), height: rowH - 14, rx: 4, class: "sc-bar " + st }, g);
        if (r.late) { const from = Math.max(I.D[j], r.s); mk("rect", { x: X(from), y: y + 7, width: Math.max(0, X(r.f) - X(from)), height: rowH - 14, rx: 4, class: "sc-over" }, g); }
      } else if (f && aside.has(j)) {
        txt(g, L + 6, y + rowH / 2 + 4, "set aside: runs at the end", { class: "sc-small sc-muted" });
      } else if (f) txt(g, L + 6, y + rowH / 2 + 4, "not placed yet", { class: "sc-small sc-muted" });
      mk("line", { x1: X(I.D[j]), x2: X(I.D[j]), y1: y + 3, y2: y + rowH - 3, class: "sc-dead" + (r && r.late ? " sc-dead-late" : "") }, g);
      txt(g, W - R + 12, y + rowH / 2 + 4, String(I.P[j]), { class: "sc-label" + (r && r.late ? "" : " sc-muted"), ...(r && r.late ? { style: "fill: var(--av-rej); font-weight: 700" } : {}) });
      const hit = mk("rect", { x: 0, y, width: W, height: rowH, class: "sc-hit" }, g);
      hit.addEventListener("pointerenter", () => setHover(j));
      hit.addEventListener("pointerleave", () => setHover(null));
    }
  }

  function drawPump(svg, I, f, hover, setHover) {
    const W = 760, L = 132, R = 38, cw = (W - L - R) / HOURS;
    const n = I.pumps.length, rowH = 26;
    const yDem = 62, yRows = 86, yTank = yRows + n * rowH + 34, tankH = 132, yCost = yTank + tankH + 46, costH = 84;
    const H = yCost + costH + 40;
    svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
    const X = h => L + h * cw;
    const sim = f && f.sim ? f.sim : null, masks = f && f.masks ? f.masks : null;
    const cursor = f && f.phase === "replay" ? f.cursor : null;
    const shownUpTo = f && f.phase === "replay" ? f.cursor : HOURS - 1; // replay reveals hours one at a time
    // readout
    const hh = hover;
    if (hh !== null) {
      const r = sim ? sim.hours[hh] : null;
      txt(svg, 16, 22, `Hour ${hh}${I.peaks.has(hh) ? " · peak $" + I.onRate + "/kWh" : " · off-peak $" + I.offRate + "/kWh"} · demand ${num(I.demand[hh])} gal${r ? ` · in ${num(r.inflow)} · tank ${num(r.level)} gal · cost ${money(r.cost)}` : ""}`, { class: "sc-read" });
    } else txt(svg, 16, 22, sim ? `Total ${money(sim.total)}${f.budget !== null && f.budget !== undefined ? ` of a ${money(f.budget)} budget` : ""}. Hover an hour to read it.` : "Hover an hour to read it. Shaded hours are peak tariff.", { class: "sc-read" + (sim ? "" : " sc-muted") });
    // peak shading behind everything
    for (let h = 0; h < HOURS; h++) if (I.peaks.has(h)) {
      mk("rect", { x: X(h), y: yDem - 14, width: cw, height: yCost + costH - yDem + 14, class: "sc-peak" }, svg);
    }
    // hover column
    if (hh !== null) mk("rect", { x: X(hh), y: yDem - 14, width: cw, height: yCost + costH - yDem + 14, class: "sc-colhover" }, svg);
    // hour axis on top
    for (let h = 0; h <= HOURS; h += 3) txt(svg, X(h), 44, String(h).padStart(2, "0") + ":00", { class: "sc-small sc-muted", "text-anchor": h === HOURS ? "end" : "start" });
    // demand row
    txt(svg, 16, yDem + 4, "Demand gal/h", { class: "sc-small sc-muted" });
    for (let h = 0; h < HOURS; h++) {
      const d = I.demand[h];
      txt(svg, X(h) + cw / 2, yDem + 4, d >= 1000 ? (d / 1000).toFixed(d % 1000 ? 1 : 0) + "k" : String(d), { class: "sc-small sc-muted", "text-anchor": "middle" });
    }
    // pump rows
    I.pumps.forEach((p, i) => {
      const y = yRows + i * rowH;
      txt(svg, 16, y + rowH / 2 + 4, p.name, { class: "sc-label" });
      txt(svg, 72, y + rowH / 2 + 4, `${num(p.flow)} gph`, { class: "sc-small sc-muted" });
      for (let h = 0; h < HOURS; h++) {
        const on = masks && (masks[h] & (1 << i));
        const cls = "sc-cell" + (on ? " on" + (h > shownUpTo ? " dim" : "") : "");
        mk("rect", { x: X(h) + 1.5, y: y + 4, width: cw - 3, height: rowH - 8, rx: 3, class: cls }, svg);
        if (sim && sim.hours[h].startups[i] && h <= shownUpTo) mk("path", { d: `M${X(h) + 2} ${y + 2} l6 0 l-6 6 z`, class: "sc-start" }, svg);
      }
    });
    // tank panel
    const lo = 0, hi = I.cap, Y = v => yTank + tankH - ((v - lo) / (hi - lo)) * tankH;
    txt(svg, 16, yTank - 10, "TANK LEVEL, GAL", { class: "sc-head" });
    const tstep = niceStep(hi - lo, 4);
    for (let v = 0; v <= hi + 1e-9; v += tstep) {
      mk("line", { x1: L, x2: W - R, y1: Y(v), y2: Y(v), class: "sc-grid" }, svg);
      txt(svg, L - 8, Y(v) + 4, num(v), { class: "sc-small sc-muted", "text-anchor": "end" });
    }
    mk("line", { x1: L, x2: W - R, y1: Y(I.cap), y2: Y(I.cap), class: "sc-limit" }, svg);
    txt(svg, W - R, Y(I.cap) - 5, "capacity", { class: "sc-small sc-muted", "text-anchor": "end" });
    if (I.minLevel > 0) {
      mk("line", { x1: L, x2: W - R, y1: Y(I.minLevel), y2: Y(I.minLevel), class: "sc-limit sc-limit-min" }, svg);
      txt(svg, W - R, Y(I.minLevel) - 5, "minimum", { class: "sc-small", "text-anchor": "end", style: "fill: var(--av-rej)" });
    }
    // forward-pass band: the reachable levels the DP holds after each hour
    if (f && f.stats && f.phase !== "replay" && f.phase !== "done") {
      const st = f.stats.filter(s => s.count);
      if (st.length) {
        let d = `M${X(0)} ${Y(I.level)}`;
        st.forEach(s => (d += ` L${X(s.h + 1)} ${Y(s.hi)}`));
        for (let k = st.length - 1; k >= 0; k--) d += ` L${X(st[k].h + 1)} ${Y(st[k].lo)}`;
        d += " Z";
        mk("path", { d, class: "sc-band" }, svg);
      }
    }
    // the chosen trajectory
    if (sim) {
      const upto = f.phase === "replay" ? f.cursor : HOURS - 1;
      let d = `M${X(0)} ${Y(I.level)}`;
      for (let h = 0; h <= upto; h++) d += ` L${X(h + 1)} ${Y(Math.max(lo, Math.min(hi, sim.hours[h].level)))}`;
      mk("path", { d, class: "sc-tank" }, svg);
      for (let h = 0; h <= upto; h++) {
        const v = sim.hours[h].level, bad = v < I.minLevel || v > I.cap;
        if (bad) mk("circle", { cx: X(h + 1), cy: Y(Math.max(lo, Math.min(hi, v))), r: 4, class: "sc-tankbad" }, svg);
      }
    }
    mk("circle", { cx: X(0), cy: Y(I.level), r: 3.5, class: "sc-tankpt" }, svg);
    // cost panel
    txt(svg, 16, yCost - 10, "COST PER HOUR, $", { class: "sc-head" });
    if (sim) {
      const upto = f.phase === "replay" ? f.cursor : HOURS - 1;
      const cmax = Math.max(...sim.hours.map(r => r.cost), 0.01), C = v => (v / cmax) * costH;
      const cstep = niceStep(cmax, 3);
      for (let v = 0; v <= cmax + 1e-9; v += cstep) {
        const y = yCost + costH - C(v);
        mk("line", { x1: L, x2: W - R, y1: y, y2: y, class: "sc-grid" }, svg);
        txt(svg, L - 8, y + 4, v.toFixed(cstep < 1 ? 2 : 0), { class: "sc-small sc-muted", "text-anchor": "end" });
      }
      for (let h = 0; h <= upto; h++) {
        const r = sim.hours[h];
        const eH = C(r.energy), sH = C(r.startup), x = X(h) + 3, w = cw - 6;
        mk("rect", { x, y: yCost + costH - eH, width: w, height: eH, class: "sc-costbar" + (h === cursor ? " cur" : "") }, svg);
        if (r.startup) mk("rect", { x, y: yCost + costH - eH - sH, width: w, height: sH, class: "sc-costbar start" }, svg);
      }
      if (f.budget !== null && f.budget !== undefined) {
        // budget used so far, as a share of the budget, on a right-hand reading
        let cum = 0, d = "";
        for (let h = 0; h <= upto; h++) { cum += sim.hours[h].cost; const y = yCost + costH - Math.min(1, cum / f.budget) * costH; d += `${h ? " L" : "M"}${X(h + 1)} ${y}`; }
        mk("line", { x1: L, x2: W - R, y1: yCost, y2: yCost, class: "sc-budget", "stroke-dasharray": "4 4", "stroke-width": 1.2 }, svg);
        mk("path", { d, class: "sc-budget" }, svg);
        for (const pct of [0, 50, 100]) txt(svg, W - R + 4, yCost + costH - (pct / 100) * costH + 4, pct + "%", { class: "sc-small", style: "fill: var(--av-hot)" });
        txt(svg, W - R, yCost - 10, `line, right axis: share of the ${money(f.budget)} budget spent`, { class: "sc-small", "text-anchor": "end", style: "fill: var(--av-hot)" });
      } else txt(svg, W - R, yCost - 10, "dark cap: pump startup cost", { class: "sc-small sc-muted", "text-anchor": "end" });
    } else txt(svg, L + 6, yCost + costH / 2, "Costs appear once pumps are scheduled.", { class: "sc-small sc-muted" });
    mk("line", { x1: L, x2: W - R, y1: yCost + costH, y2: yCost + costH, class: "sc-axis" }, svg);
    // cursor column during replay
    if (cursor !== null) mk("rect", { x: X(cursor), y: yDem - 14, width: cw, height: yCost + costH - yDem + 14, class: "sc-col" }, svg);
    // hit areas per hour
    for (let h = 0; h < HOURS; h++) {
      const hit = mk("rect", { x: X(h), y: yDem - 14, width: cw, height: yCost + costH - yDem + 14, class: "sc-hit" }, svg);
      hit.addEventListener("pointerenter", () => setHover(h));
      hit.addEventListener("pointerleave", () => setHover(null));
    }
  }

  /* ---------- checks + chips ---------- */
  function jobChecks(I, f) {
    if (!f || !f.order) return [{ label: `Jobs: ${I.n}, penalty budget K = ${I.K}`, ok: null }];
    const tl = jobTimeline(I, f.order), pen = penaltyOf(I, tl), late = tl.filter(r => r.late).map(r => r.j);
    const out = [];
    if (f.order.length < I.n) out.push({ label: `Jobs placed: ${f.order.length} / ${I.n}`, ok: null });
    else out.push({ label: `Every job runs once: ${new Set(f.order).size} / ${I.n}`, ok: new Set(f.order).size === I.n });
    out.push({ label: `Late jobs: ${late.length ? late.join(", ") : "none"}`, ok: f.order.length < I.n ? null : late.length === 0 ? true : null });
    out.push({ label: `Penalty of late jobs: ${pen} (K = ${I.K})`, ok: f.order.length < I.n ? null : pen <= I.K });
    return out;
  }
  function pumpChecks(I, f) {
    if (!f) return [{ label: `Pumps: ${I.pumps.length}, tank ${num(I.level)} of ${num(I.cap)} gal`, ok: null }];
    if (f.phase === "forward") {
      const s = f.stats[f.stats.length - 1];
      return [{ label: `Hours planned: ${s.h + 1} / 24`, ok: null }, { label: `Reachable states now: ${s.count.toLocaleString("en-US")}`, ok: s.count ? null : false }];
    }
    if (!f.sim) return [{ label: "A schedule exists for all 24 hours", ok: false }];
    const s = f.sim, lim = `${num(I.minLevel)}–${num(I.cap)} gal`;
    const out = [{ label: `Tank stays within ${lim} (lowest ${num(s.low)}, highest ${num(s.high)})`, ok: s.ok }];
    if (I.kind === "cm") out.push({ label: `Day's cost: ${money(s.total)} (cheapest possible)`, ok: s.ok ? true : null });
    else {
      out.push({ label: `Day's cost: ${money(s.total)} (budget ${money(f.budget)})`, ok: s.total <= f.budget + 0.01 });
      out.push({ label: `Water stored, summed over the hours: ${num(s.stored)} gal·h`, ok: null });
    }
    return out;
  }

  function create({ svg }) {
    if (!document.getElementById("sc-style")) {
      const st = document.createElement("style"); st.id = "sc-style"; st.textContent = css; document.head.appendChild(st);
    }
    svg.classList.add("sc-svg");
    let I = null, frames = [], frame = null, hover = null, meta = {};
    function paint() {
      svg.innerHTML = "";
      const setHover = v => { if (hover !== v) { hover = v; paint(); } };
      if (I.kind === "job") drawJob(svg, I, frame, hover, setHover);
      else drawPump(svg, I, frame, hover, setHover);
    }
    return {
      load(str, key, solver) {
        const P = PROBLEMS[key];
        if (!P) throw new Error("Unknown problem " + key);
        I = key === "JOBSEQ" ? parseJob(str) : parsePump(str, key === "PUMPEM");
        const s = solver || P.solvers[0][0];
        if (I.kind === "job") frames = s === "greedy" ? jobGreedy(I) : jobBrute(I);
        else { const r = pumpFrames(I); frames = r.frames; meta = r; }
        frames.unshift(I.kind === "job"
          ? { order: [], caption: "Step 0: no job placed yet. Step forward to watch the solver." }
          : { phase: "start", caption: "Step 0: hour 0, tank at " + num(I.level) + " gal. Step forward to watch the dynamic program fill its table hour by hour." });
        frame = null; hover = null; paint();
        const last = frames[frames.length - 1];
        return { frames, ok: !!last.ok };
      },
      show(i) { frame = i === null ? null : frames[i]; paint(); return frame; },
      checks(i) { const f = i === null ? null : frames[i]; return I.kind === "job" ? jobChecks(I, f) : pumpChecks(I, f); },
      chosen(i) {
        const f = i === null ? null : frames[i];
        if (!f) return [];
        if (I.kind === "job") {
          if (!f.order || !f.order.length) return [];
          const pen = penaltyOf(I, jobTimeline(I, f.order));
          return [{ text: certOf(f.order) }, { text: `penalty ${pen}` }];
        }
        if (!f.sim) return [];
        const shown = f.phase === "replay" ? f.cursor + 1 : HOURS;
        const chips = I.pumps.map((p, k) => ({ text: `${p.name}: ${f.masks.slice(0, shown).filter(m => m & (1 << k)).length} h on` }));
        chips.push({ text: money(f.phase === "replay" ? f.sim.hours.slice(0, shown).reduce((a, r) => a + r.cost, 0) : f.sim.total) });
        return chips;
      },
      resetHover() { hover = null; if (I) paint(); },
    };
  }

  /* Catalog shared by both mockups. Names, definitions, defaults and solver names are copied from Redux. */
  const PUMP_CM_DEFAULT = "((10000,5000),((600,600,600,600,600,600,600,600,1000,1000,1000,1000,600,600,600,600,600,1000,1000,1000,1000,1000,600,600),(8,9,10,11,17,18,19,20),(0.12,0.06)),((PumpA,200,5.0,2.5),(PumpB,350,8.5,4.0),(PumpC,500,12.0,6.0)))";
  const PUMP_EM_DEFAULT = "((10000,5000,2000),((600,600,600,600,600,600,600,600,1000,1000,1000,1000,600,600,600,600,600,1000,1000,1000,1000,1000,600,600),(8,9,10,11,17,18,19,20),(0.12,0.06)),((PumpA,200,5.0,2.5),(PumpB,350,8.5,4.0),(PumpC,500,12.0,6.0)),0)";
  const PROBLEMS = {
    JOBSEQ: {
      label: "Job Sequencing", cls: "NP-Complete",
      def: "Job sequencing is the task of deciding in what order to do a series of jobs. Each job has a length of time it takes, a deadline, and a penalty that is applied if the deadline is missed. The task is to find an ordering of the jobs that results in a penalty that is less than k.",
      format: "((T),(D),(P),K): job times, deadlines, penalties, and the most total penalty allowed",
      solvers: [["brute", "Job Sequencing Brute Force"], ["greedy", "Penalty-first greedy (not in Redux yet)"]],
      examples: [
        ["Redux default", "((4,2,5,9,4,3),(9,13,2,17,21,16),(1,4,3,2,5,8),4)"],
        ["Greedy misses, brute force finds one", "((1,1,4,1),(6,2,4,4),(4,5,6,3),6)"],
        ["Nothing fits K", "((4,4,4),(4,5,6),(5,5,5),4)"],
      ],
    },
    PUMPCM: {
      label: "Pump Scheduling Cost Minimization", cls: "NP-Hard",
      def: "Determine which pumps to activate each hour over a 24-hour period so that water demand is met, the storage tank never overflows or fails to supply the water demand, and the total energy cost (based on peak/off-peak tariffs plus pump startup costs) is minimized.",
      format: "((capacity,level),((24 hourly demands),(peak hours),(on-peak $/kWh,off-peak $/kWh)),((name,gph,kW,startup $),…))",
      solvers: [["dp", "Pump Scheduling Cost Minimization Dynamic Programming"]],
      examples: [
        ["Redux default", PUMP_CM_DEFAULT],
        ["Small tank: no room to store water ahead", "((1500,800),((600,600,600,600,600,600,600,600,1000,1000,1000,1000,600,600,600,600,600,1000,1000,1000,1000,1000,600,600),(8,9,10,11,17,18,19,20),(0.12,0.06)),((PumpA,200,5.0,2.5),(PumpB,350,8.5,4.0),(PumpC,500,12.0,6.0)))"],
        ["Demand the pumps can't meet", "((3000,1500),((900,900,900,900,900,900,900,900,1500,1500,1500,1500,900,900,900,900,900,1500,1500,1500,1500,1500,900,900),(8,9,10,11,17,18,19,20),(0.12,0.06)),((PumpA,200,5.0,2.5),(PumpB,350,8.5,4.0),(PumpC,500,12.0,6.0)))"],
      ],
    },
    PUMPEM: {
      label: "Pump Scheduling Emergency Resilience", cls: "NP-Hard",
      def: "Determine which pumps to activate each hour over a 24-hour period so that: the storage tank never drops below its minimum level or exceeds its capacity, budget total energy cost (tariffs plus startup costs) stays within the given budget, and the cumulative water stored across all hours is maximized. This models emergency resilience scenarios where maximizing stored water supply is prioritized within an operational cost constraint.",
      format: "((capacity,level,minimum),(demand section as in Cost Minimization),(pumps),budget), budget 0 = 1.5 times the cheapest schedule",
      solvers: [["dp", "Pump Scheduling Emergency Resilience Longest Path"]],
      examples: [
        ["Redux default (budget auto)", PUMP_EM_DEFAULT],
        ["Fixed $40 budget", PUMP_EM_DEFAULT.replace(/,0\)$/, ",40)")],
        ["Budget too small: Redux falls back", PUMP_EM_DEFAULT.replace(/,0\)$/, ",5)")],
      ],
    },
  };

  return { create, parse: (str, key) => (key === "JOBSEQ" ? parseJob(str) : parsePump(str, key === "PUMPEM")), PROBLEMS };
})();
