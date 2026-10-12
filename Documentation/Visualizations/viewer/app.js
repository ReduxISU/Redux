/* ---- Redux frames viewer: page logic ----
   Picture mode: POST /ProblemProvider/visualize?visualization=V&format=frames[&solver=S], body = the instance as a JSON string.
   Reduction mode: POST /ProblemProvider/reductionView?reduction=R[&solver=S], body = the SOURCE instance.
   Every picture is drawn by the adapter registered for the response's "type" (adapters/*.js), which feeds a mockup renderer.

   URL parameters (handy for tests and sharing): backend, theme=light|dark, mode=picture|reduction, problem, vis, solver,
   instance, reduction, i (frame index), along=1 (search tree alongside), run=1 (run on load).
   When a run settles, <body data-state> becomes "ready" or "error" (the smoke test waits on it). */
(() => {
  const $ = id => document.getElementById(id);
  const NEW_TYPES = new Set(["Graph", "GroupedGraph", "BipartiteGraph", "Automaton", "LayeredGraph", "FlowNetwork", "Packing", "Board", "Table",
    "QuantumCircuit", "Schedule", "Geometry", "TripartiteMatching", "CodeTree", "SearchTree"]);
  const STATE_HELP = {
    Background: "Nothing has happened to it yet",
    ElementHighlight: "What the solver is looking at or trying now",
    Solution: "Part of the answer",
    Rejected: "Failed: falsified, uncovered, doubled, clashing",
    Untraveled: "Never reached during this run",
    Covered: "A constraint that is met",
    Blocked: "Can't be chosen without breaking an exactly-once rule",
    False: "Assigned and false",
  };
  const params = new URLSearchParams(location.search);
  const S = {
    backend: params.get("backend") || localGet("vv-backend") || "http://localhost:27000",
    mode: "picture",
    problems: [], info: new Map(), visProbe: new Map(),
    run: null,            // { kind: "picture"|"reduction", frames, handles: [...], resp }
    i: 0, timer: null,
  };

  window.__viewer = S; // for the smoke test and the browser console
  function localGet(k) { try { return localStorage.getItem(k); } catch { return null; } }
  function localSet(k, v) { try { localStorage.setItem(k, v); } catch { /* private window */ } }
  const setState = s => { document.body.dataset.state = s; };

  /* ---------- backend calls ---------- */
  const url = p => S.backend.replace(/\/+$/, "") + p;
  async function getJson(p) {
    const r = await fetch(url(p));
    const t = await r.text();
    if (!r.ok) throw new Error(`GET ${p} → ${r.status}: ${t.slice(0, 400)}`);
    return JSON.parse(t);
  }
  async function postJson(p, body) {
    const r = await fetch(url(p), { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify(body) });
    const t = await r.text();
    let d; try { d = JSON.parse(t); } catch { d = t; }
    if (!r.ok) throw new Error(`POST ${p} → ${r.status}: ${typeof d === "string" ? d.slice(0, 600) : JSON.stringify(d, null, 1).slice(0, 900)}`);
    return d;
  }
  async function info(name) {
    const k = S.backend + "|" + name;
    if (!S.info.has(k)) S.info.set(k, getJson("/ProblemProvider/info?interface=" + encodeURIComponent(name)));
    return S.info.get(k);
  }

  /* ---------- theme ---------- */
  function setTheme(t) {
    if (t === "auto") document.documentElement.removeAttribute("data-theme");
    else document.documentElement.setAttribute("data-theme", t);
    document.querySelectorAll("[data-theme-set]").forEach(b => b.setAttribute("aria-pressed", String(b.dataset.themeSet === t)));
  }
  document.querySelectorAll("[data-theme-set]").forEach(b => b.addEventListener("click", () => setTheme(b.dataset.themeSet)));

  /* ---------- health ---------- */
  async function checkHealth() {
    const h = $("health");
    h.className = "health"; h.textContent = "checking…";
    try {
      const t0 = performance.now();
      const probs = await getJson("/Navigation/ALL_ProblemsRefactor");
      h.className = "health up"; h.textContent = `up · ${probs.length} problems · ${Math.round(performance.now() - t0)} ms`;
      return probs;
    } catch (e) {
      h.className = "health down"; h.textContent = "down: " + (e.message.includes("Failed to fetch") ? "no answer (is the build running?)" : e.message.slice(0, 80));
      return null;
    }
  }

  /* ---------- picture mode controls ---------- */
  function opt(sel, value, text) { const o = document.createElement("option"); o.value = value; o.textContent = text; sel.appendChild(o); return o; }

  async function loadProblems() {
    const probs = await checkHealth();
    if (!probs) return false;
    S.problems = probs;
    const sel = $("problem"); sel.innerHTML = "";
    probs.forEach(p => opt(sel, p, p));
    return true;
  }

  async function onProblem(pre = {}) {
    const p = $("problem").value;
    $("pic-err").hidden = true;
    const [inf, viz, sol] = await Promise.all([
      info(p), getJson("/Navigation/Problem_VisualizationsRefactor?chosenProblem=" + encodeURIComponent(p)),
      getJson("/Navigation/Problem_SolversRefactor?chosenProblem=" + encodeURIComponent(p)),
    ]);
    const vs = $("vis"); vs.innerHTML = "";
    viz.forEach(v => opt(vs, v, v));
    if (pre.vis && viz.includes(pre.vis)) vs.value = pre.vis;
    const ss = $("solver"); ss.innerHTML = "";
    opt(ss, "", "(visualization's own solver)");
    sol.forEach(s => opt(ss, s, s));
    if (pre.solver && sol.includes(pre.solver)) ss.value = pre.solver;
    $("instance").value = pre.instance ?? inf.defaultInstance ?? "";
    $("instance").dataset.default = inf.defaultInstance ?? "";
    $("pic-hint").textContent = inf.problemName ? `${inf.problemName}. Format: ${(inf.instanceFormat || "").slice(0, 140)}` : "";
    S.currentViz = viz;
    probeVisualizations(p, viz, inf.defaultInstance);
    updateAlong();
  }

  /**
   * Label each visualization with the picture type it serves under format=frames. /info names it in "pictureType" (#841): a
   * picture type, or null for a visualization that only serves the older formats (format=frames then wraps its list output,
   * typed with its legacy visualizationType). A backend without the field is probed instead: one default-instance run per
   * visualization, cached.
   */
  function probeVisualizations(p, viz, defInst) {
    for (const v of viz) {
      const k = S.backend + "|" + v;
      if (!S.visProbe.has(k)) S.visProbe.set(k, info(v).catch(() => null).then(inf => {
        if (inf && "pictureType" in inf) return { type: inf.pictureType ?? inf.visualizationType ?? "legacy", from: "info" };
        return postJson(`/ProblemProvider/visualize?visualization=${encodeURIComponent(v)}&format=frames`, defInst)
          .then(d => ({ type: d.type, from: "probe" })).catch(() => ({ type: "error", from: "probe" }));
      }));
      S.visProbe.get(k).then(({ type, from }) => {
        const o = [...$("vis").options].find(x => x.value === v);
        if (!o) return;
        o.dataset.type = type;
        o.dataset.typeFrom = from;
        o.textContent = `${v} · ${type}${NEW_TYPES.has(type) ? " (frames)" : type === "error" ? "" : " (legacy, list wrapped)"}`;
        updateAlong();
      });
    }
  }

  function searchTreeVis() {
    return [...$("vis").options].find(o => o.dataset.type === "SearchTree" || /SearchTree/i.test(o.value));
  }
  function updateAlong() {
    const st = searchTreeVis(), cur = $("vis").selectedOptions[0];
    $("st-wrap").hidden = !st || (cur && (cur === st));
  }

  /* ---------- running ---------- */
  function stopPlay() { if (S.timer) { clearInterval(S.timer); S.timer = null; } $("play").textContent = "Play"; }

  function clearRun() {
    stopPlay();
    if (S.run) for (const h of S.run.handles) try { h.handle && h.handle.destroy && h.handle.destroy(); } catch { /* ignore */ }
    S.run = null;
    $("stages").innerHTML = ""; $("stages").className = "card stages";
    $("notes").innerHTML = ""; $("legend").innerHTML = ""; $("gadgets").innerHTML = "";
    $("phases").hidden = true; $("gadget-card").hidden = true;
  }

  const notesSeen = new Set();
  function note(text, warn = true) {
    const k = text;
    if (notesSeen.has(k)) return;
    notesSeen.add(k);
    const li = document.createElement("li"); li.textContent = text; if (warn) li.className = "warn";
    $("notes").appendChild(li);
  }

  function stage(title, tagText, sub) {
    const sec = document.createElement("section"); sec.className = "stage";
    const h = document.createElement("h2");
    h.innerHTML = `<b></b><span class="tag"></span><span></span>`;
    h.children[0].textContent = title; h.children[1].textContent = tagText; h.children[2].textContent = sub || "";
    const host = document.createElement("div"); host.className = "host";
    sec.append(h, host);
    $("stages").appendChild(sec);
    return host;
  }

  function mountPicture({ host, type, payload, frames, instance, problem, visualization, solver, pane, buildOrder = null, setFrames = null }) {
    if (!NEW_TYPES.has(type)) {
      host.innerHTML = `<div class="empty">Picture type "${type}" is a legacy visualization: format=frames only wraps its old list output, so there is nothing for a frames renderer to draw. See the raw JSON.</div>`;
      note(`${visualization || problem}: type "${type}" is not a frames picture type (legacy list output wrapped as frames).`, false);
      return { show() {} };
    }
    if (!Viewer.has(type)) {
      host.innerHTML = `<div class="empty">No adapter for "${type}" yet.</div>`;
      return { show() {} };
    }
    try {
      return Viewer.mount({ host, type, payload, frames, instance, problem, visualization, solver, pane, buildOrder, setFrames, note: (t, w) => note(`[${pane === "main" ? type : pane + " · " + type}] ${t}`, w) });
    } catch (e) {
      console.error(e);
      host.innerHTML = ""; const d = document.createElement("div"); d.className = "err"; d.textContent = `Adapter for ${type} failed: ${e.message}`; host.appendChild(d);
      return { show() {} };
    }
  }

  function legend(runValue) {
    const used = Viewer.statesIn(runValue);
    const lg = $("legend"); lg.innerHTML = "";
    for (const s of Viewer.STATES) {
      if (!used.has(s)) continue;
      const r = document.createElement("div"); r.className = "li";
      r.innerHTML = `<span class="sw ${s}"></span><div>${s}<small>${STATE_HELP[s]}</small></div>`;
      lg.appendChild(r);
    }
    if (!lg.children.length) lg.innerHTML = `<div class="hint">No state names in these frames.</div>`;
  }

  /** Steps that carry SolverStep.tree patches → a SearchTree response (cumulative node list per step). */
  function treeFromSteps(frames, problem, instance) {
    if (!frames.some(f => Array.isArray(f.tree))) return null;
    const nodes = new Map();
    const out = frames.map(f => {
      (f.tree || []).forEach(n => nodes.set(n.id, { ...(nodes.get(n.id) || {}), ...n }));
      return { event: f.event, caption: f.caption, focus: f.focus, nodes: [...nodes.values()].map(n => ({ ...n })), ok: f.ok };
    });
    // Not folded by the backend: searchtree.js folds it (clientFolds).
    return { type: "SearchTree", payload: { kind: "searchTree", problem, instance, clientFolds: true }, frames: out };
  }

  async function runPicture() {
    clearRun(); notesSeen.clear();
    setState("loading");
    $("pic-err").hidden = true; $("empty").hidden = false; $("empty").textContent = "Running…"; $("bench").hidden = true;
    const problem = $("problem").value, vis = $("vis").value, solver = $("solver").value, instance = $("instance").value;
    syncUrl();
    try {
      const q = `/ProblemProvider/visualize?visualization=${encodeURIComponent(vis)}&format=frames` + (solver ? `&solver=${encodeURIComponent(solver)}` : "");
      const t0 = performance.now();
      const resp = await postJson(q, instance);
      const ms = Math.round(performance.now() - t0);
      $("empty").hidden = true; $("bench").hidden = false;
      const handles = [];
      const frames = Array.isArray(resp.frames) ? resp.frames : [];
      const along = !$("st-wrap").hidden && $("st-along").checked;
      const host = stage(problem, resp.type, `${vis}${solver ? " · " + solver : ""} · ${frames.length} frames · ${ms} ms`);
      // An adapter may switch the frames the page steps through (the NFA one-path picker): the player restarts on them.
      const setFrames = fr => {
        if (!S.run || S.run.resp !== resp) return;
        stopPlay();
        S.run.frames = fr; S.run.handles[0].frames = fr;
        legend(S.run.handles.map(h => h.frames));
        setupPlayer(fr.length, 0);
      };
      handles.push({ frames, handle: mountPicture({ host, type: resp.type, payload: resp.payload, frames, instance, problem, visualization: vis, solver, pane: "main", setFrames }) });
      let sideResp = treeFromSteps(frames, problem, instance), sideLabel = "steps' tree patches";
      if (!sideResp && along) {
        const st = searchTreeVis();
        try {
          sideResp = await postJson(`/ProblemProvider/visualize?visualization=${encodeURIComponent(st.value)}&format=frames` + (solver ? `&solver=${encodeURIComponent(solver)}` : ""), instance);
          sideLabel = st.value;
          if (sideResp.frames.length !== frames.length) note(`Search tree alongside: ${st.value} has ${sideResp.frames.length} frames, the main picture ${frames.length}; synced by index (clamped).`, false);
        } catch (e) { note("Search tree alongside failed: " + e.message); }
      }
      if (sideResp) {
        $("stages").classList.add("split");
        const h2 = stage("Search tree", sideResp.type, sideLabel);
        handles.push({ frames: sideResp.frames, handle: mountPicture({ host: h2, type: sideResp.type, payload: sideResp.payload, frames: sideResp.frames, instance, problem, visualization: sideLabel, solver, pane: "side" }) });
      }
      S.run = { kind: "picture", resp, frames, handles };
      $("raw-payload-pre").textContent = JSON.stringify({ type: resp.type, payload: resp.payload }, null, 2);
      legend(handles.map(h => h.frames));
      const start = Math.min(+(params.get("i") ?? 0) || 0, Math.max(0, frames.length - 1));
      params.delete("i");
      setupPlayer(frames.length, start);
      setState("ready");
    } catch (e) {
      console.warn(e);
      $("empty").hidden = true;
      $("pic-err").hidden = false; $("pic-err").textContent = e.message;
      setState("error");
    }
  }

  /* ---------- player ---------- */
  function setupPlayer(n, start = 0) {
    $("scrub").max = Math.max(0, n - 1);
    show(start);
  }

  function statusBadges(f, i, n) {
    const b = [];
    if (f.phase) b.push(["hot", "phase: " + ({ build: "build", solve: "solve", map: "map back" }[f.phase] || f.phase)]);
    if (f.event) b.push([f.event === "Reject" ? "rej" : f.event === "Accept" ? "ok" : f.event === "Try" ? "hl" : "", "event: " + f.event]);
    if (f.ok === true) b.push(["ok", "ok: true"]);
    // A stopped run's Done: the picture's own (blank or "Stopped after ..."), or a Reduction View's map Done after a stopped solve.
    // A quantum run whose server could not be reached ended without an answer; that is not a wrong answer.
    const down = Viewer.quantumServerDown(f);
    if (f.ok === false) b.push(["rej", down ? "ok: false · quantum server unavailable" : f.event === "Done" && (!f.caption || /^Stopped|had not finished/.test(f.caption)) ? "ok: false · stopped (time limit)" : "ok: false"]);
    else if (down) b.push(["rej", "quantum server unavailable"]);
    if (i === n - 1) {
      const last = f;
      if (last.event !== "Done") b.push(["rej", "last frame is not Done"]);
    } else if (f.event === "Done") b.push(["rej", "Done before the last frame"]);
    // Only the last frame carries the verdict; a Reduction View's build and solve phases end without one (#840).
    if (i < n - 1 && f.ok !== undefined && S.run && S.run.kind === "reduction") b.push(["rej", "ok before the last frame"]);
    if (f.focus && f.focus.length) b.push(["", "focus: " + f.focus.slice(0, 6).join(", ") + (f.focus.length > 6 ? ` +${f.focus.length - 6}` : "")]);
    return b;
  }

  function show(i) {
    if (!S.run) return;
    const n = S.run.frames.length;
    if (!n) { $("caption").textContent = "The response has no frames."; $("count").textContent = "0 / 0"; return; }
    i = Math.max(0, Math.min(n - 1, i));
    S.i = i;
    for (const h of S.run.handles) {
      if (!h.handle) continue;
      const j = Math.min(i, h.frames.length - 1);
      try { if (j >= 0) h.handle.show(j); } catch (e) { console.error(e); note(`show(${j}) threw: ${e.message}`); }
    }
    const f = S.run.frames[i];
    const cap = $("caption");
    cap.textContent = f.caption || "(blank caption)";
    cap.className = "caption" + (!f.caption ? " blank" : "") + (f.ok === true ? " ok" : f.ok === false || f.event === "Reject" ? " rej" : f.event === "Try" ? " hl" : "");
    const bd = $("badges"); bd.innerHTML = "";
    for (const [cls, text] of statusBadges(f, i, n)) { const s = document.createElement("span"); s.className = "badge " + cls; s.textContent = text; bd.appendChild(s); }
    $("count").textContent = `${i + 1} / ${n}`;
    $("scrub").value = i;
    $("prev").disabled = i === 0; $("next").disabled = i === n - 1;
    const raw = $("raw-frame-pre");
    if ($("raw-frame").open) raw.textContent = JSON.stringify(f, null, 2);
    if (S.run.kind === "reduction") showReductionFrame(f, i);
  }
  $("raw-frame").addEventListener("toggle", () => { if (S.run) $("raw-frame-pre").textContent = JSON.stringify(S.run.frames[S.i], null, 2); });

  $("prev").onclick = () => { stopPlay(); show(S.i - 1); };
  $("next").onclick = () => { stopPlay(); show(S.i + 1); };
  $("scrub").oninput = e => { stopPlay(); show(+e.target.value); };
  function startPlay() {
    if (!S.run) return;
    if (S.i >= S.run.frames.length - 1) show(0);
    $("play").textContent = "Pause";
    S.timer = setInterval(() => {
      if (!S.run || S.i >= S.run.frames.length - 1) { stopPlay(); return; }
      show(S.i + 1);
    }, +$("speed").value);
  }
  $("play").onclick = () => { if (S.timer) stopPlay(); else startPlay(); };
  $("speed").onchange = () => { if (S.timer) { stopPlay(); startPlay(); } };
  document.addEventListener("keydown", e => {
    if (e.target.closest && e.target.closest("input, textarea, select") && e.target.type !== "range") return;
    if (!S.run) return;
    if (e.key === "ArrowRight") { stopPlay(); show(S.i + 1); e.preventDefault(); }
    else if (e.key === "ArrowLeft") { stopPlay(); show(S.i - 1); e.preventDefault(); }
    else if (e.key === "Home") { stopPlay(); show(0); e.preventDefault(); }
    else if (e.key === "End") { stopPlay(); show(S.run.frames.length - 1); e.preventDefault(); }
    else if (e.key === " " && e.target.tagName !== "BUTTON") { $("play").click(); e.preventDefault(); }
  });

  /* ---------- reduction mode ---------- */
  async function loadReductions() {
    const g = await getJson("/Navigation/Reductions");
    const list = [];
    for (const [from, tos] of Object.entries(g)) for (const [to, edges] of Object.entries(tos)) for (const e of edges) list.push({ from, to, ...e });
    list.sort((a, b) => (a.from + a.to).localeCompare(b.from + b.to));
    S.reductions = list;
    const sel = $("reduction"); sel.innerHTML = "";
    list.forEach(r => opt(sel, r.className, `${r.from} → ${r.to} · ${r.reductionName} (${r.className})`));
  }

  async function onReduction(pre = {}) {
    const r = S.reductions.find(x => x.className === $("reduction").value);
    if (!r) return;
    $("red-err").hidden = true;
    const [inf, sol] = await Promise.all([info(r.from), getJson("/Navigation/Problem_SolversRefactor?chosenProblem=" + encodeURIComponent(r.to))]);
    $("red-instance").value = pre.instance ?? inf.defaultInstance ?? "";
    $("red-instance").dataset.default = inf.defaultInstance ?? "";
    const ss = $("red-solver"); ss.innerHTML = "";
    opt(ss, "", "(the reduction's default)");
    sol.forEach(s => opt(ss, s, s));
    if (pre.solver && sol.includes(pre.solver)) ss.value = pre.solver;
    $("red-hint").textContent = `${r.from} → ${r.to}: ${r.reductionName}. Body is the ${r.from} instance.`;
  }

  async function runReduction() {
    clearRun(); notesSeen.clear();
    setState("loading");
    $("red-err").hidden = true; $("empty").hidden = false; $("empty").textContent = "Running…"; $("bench").hidden = true;
    const red = $("reduction").value, solver = $("red-solver").value, instance = $("red-instance").value;
    syncUrl();
    try {
      const t0 = performance.now();
      const rv = await postJson(`/ProblemProvider/reductionView?reduction=${encodeURIComponent(red)}` + (solver ? `&solver=${encodeURIComponent(solver)}` : ""), instance);
      const ms = Math.round(performance.now() - t0);
      $("empty").hidden = true; $("bench").hidden = false;
      $("stages").classList.add("split");
      const frames = rv.frames || [];
      const paneFrames = side => frames.map(f => ({ ...(f[side] || {}), caption: f.caption, event: f.event, ok: f.ok, phase: f.phase, focus: f[side + "Focus"] }));
      const handles = [];
      for (const side of ["source", "target"]) {
        const pic = rv[side].picture;
        const host = stage(`${side === "source" ? "A" : "B"} · ${rv[side].problem}`, pic.type, side === "target" ? `${rv.solver ? "solver: " + rv.solver : ""} · ${ms} ms` : rv.reductionName);
        host.dataset.pane = side;
        const pf = paneFrames(side);
        handles.push({ side, frames: pf, host, handle: mountPicture({ host, type: pic.type, payload: pic.payload, frames: pf, instance: rv[side].instance, problem: rv[side].problem, visualization: rv.reduction, solver: side === "target" ? rv.solver : "", pane: side, buildOrder: rv[side].buildOrder || null }) });
      }
      S.run = { kind: "reduction", resp: rv, frames, handles };
      const hdr = { ...rv }; delete hdr.frames;
      $("raw-payload-pre").textContent = JSON.stringify(hdr, null, 2);
      checkGadgets(rv);
      checkSingleDone(frames);
      buildPhases(frames);
      buildGadgets(rv);
      setupLinking(rv, handles);
      legend(frames);
      const start = Math.min(+(params.get("i") ?? 0) || 0, Math.max(0, frames.length - 1));
      params.delete("i");
      setupPlayer(frames.length, start);
      setState("ready");
    } catch (e) {
      console.warn(e);
      $("empty").hidden = true;
      $("red-err").hidden = false; $("red-err").textContent = e.message;
      setState("error");
    }
  }

  function checkGadgets(rv) {
    const g0 = rv.gadgets && rv.gadgets[0];
    if (g0 && !("kind" in g0) && "gadgetKind" in g0) note("Reduction View gadgets carry their kind as \"gadgetKind\"; the README/mockup contract calls it \"kind\".", false);
    const kinds = new Set(Object.keys(ReductionView.GADGET_KINDS));
    for (const g of rv.gadgets || []) { const k = g.kind ?? g.gadgetKind; if (!kinds.has(k)) note(`Gadget kind "${k}" is not in the shared list (${[...kinds].join(", ")}).`); }
  }

  /** The Reduction View has one Done, its last frame (the end of map back), and only that frame has ok (#840). */
  function checkSingleDone(frames) {
    const dones = frames.map((f, i) => f.event === "Done" ? i : -1).filter(i => i >= 0);
    if (dones.length !== 1 || dones[0] !== frames.length - 1)
      note(`The Reduction View should have exactly one Done, its last frame; it has ${dones.length} (frames ${dones.map(i => i + 1).join(", ") || "none"}).`);
    const early = frames.slice(0, -1).filter(f => f.ok !== undefined);
    if (early.length) note(`${early.length} frames before the last carry ok (phases ${[...new Set(early.map(f => f.phase))].join(", ")}); only the final Done should.`);
  }

  function buildPhases(frames) {
    const ph = $("phases"); ph.innerHTML = ""; ph.hidden = false;
    const names = [["build", "Build"], ["solve", "Solve"], ["map", "Map back"]];
    for (const [k, label] of names) {
      const first = frames.findIndex(f => f.phase === k), count = frames.filter(f => f.phase === k).length;
      const b = document.createElement("button"); b.dataset.phase = k;
      b.innerHTML = `${label}<small>${count}</small>`;
      b.disabled = first < 0;
      b.onclick = () => { stopPlay(); show(first); };
      ph.appendChild(b);
    }
    const other = frames.filter(f => !["build", "solve", "map"].includes(f.phase));
    if (other.length) note(`${other.length} frames have a phase outside build/solve/map: ${[...new Set(other.map(f => f.phase))].join(", ")}`);
  }

  function buildGadgets(rv) {
    $("gadget-card").hidden = false;
    const box = $("gadgets"); box.innerHTML = "";
    (rv.gadgets || []).forEach((g, gi) => {
      const d = document.createElement("div"); d.className = "g"; d.dataset.gi = gi;
      const kind = g.kind ?? g.gadgetKind;
      const name = (ReductionView.GADGET_KINDS[kind] || {}).name || kind;
      d.innerHTML = `<span class="k"></span><span class="ids"></span>`;
      d.children[0].textContent = name;
      d.children[1].textContent = `${(g.sourceIds || []).join(", ") || "∅"} ↦ ${(g.targetIds || []).join(", ") || "∅"}`;
      if (g.note) { const n = document.createElement("span"); n.className = "note"; n.textContent = g.note; d.appendChild(n); }
      d.addEventListener("pointerenter", () => S.link && S.link.highlight([gi]));
      d.addEventListener("pointerleave", () => S.link && S.link.clear());
      box.appendChild(d);
    });
  }

  function showReductionFrame(f) {
    $("phases").querySelectorAll("button").forEach(b => b.classList.toggle("on", b.dataset.phase === f.phase));
    const now = new Set(f.gadgets || []);
    $("gadgets").querySelectorAll(".g").forEach(d => d.classList.toggle("now", now.has(+d.dataset.gi)));
    const first = $("gadgets").querySelector(".g.now");
    if (first) first.scrollIntoView({ block: "nearest" });
    if (S.link) S.link.refresh();
  }

  /** Hover a piece in either pane (or a gadget row): its gadgets' pieces on both sides get an accent ring, the rest turn grey. */
  function setupLinking(rv, handles) {
    const gadgets = rv.gadgets || [];
    const idx = { source: new Map(), target: new Map() };
    gadgets.forEach((g, gi) => {
      for (const id of g.sourceIds || []) (idx.source.get(id) || idx.source.set(id, []).get(id)).push(gi);
      for (const id of g.targetIds || []) (idx.target.get(id) || idx.target.set(id, []).get(id)).push(gi);
    });
    const hosts = Object.fromEntries(handles.map(h => [h.side, h.host]));
    // Which gadget ids each pane actually draws (to report ids nobody tagged).
    let current = null;
    function clear() {
      current = null;
      for (const host of Object.values(hosts)) {
        host.classList.remove("linking");
        host.querySelectorAll(".vv-linked").forEach(e => e.classList.remove("vv-linked"));
        host.querySelectorAll(".vv-ring-layer").forEach(e => e.remove());
      }
      $("gadgets").querySelectorAll(".g.hover").forEach(d => d.classList.remove("hover"));
    }
    function ring(el) {
      const svg = el.ownerSVGElement;
      if (!svg) { el.classList.add("vv-linked"); return; }
      let layer = svg.querySelector(":scope > .vv-ring-layer");
      if (!layer) layer = Viewer.mk("g", { class: "vv-ring-layer" }, svg);
      try {
        const m = svg.getScreenCTM().inverse().multiply(el.getScreenCTM());
        if (/^(line|path|polyline)$/.test(el.tagName)) {
          const c = el.cloneNode(false);
          c.removeAttribute("data-vid"); c.removeAttribute("marker-end"); c.removeAttribute("marker-start");
          c.setAttribute("class", "vv-ring"); c.setAttribute("transform", `matrix(${m.a},${m.b},${m.c},${m.d},${m.e},${m.f})`);
          layer.appendChild(c);
        } else {
          const b = el.getBBox(), pad = 4;
          Viewer.mk("rect", { class: "vv-ring", x: b.x - pad, y: b.y - pad, width: b.width + 2 * pad, height: b.height + 2 * pad, rx: 6,
            transform: `matrix(${m.a},${m.b},${m.c},${m.d},${m.e},${m.f})` }, layer);
        }
      } catch { /* not rendered */ }
    }
    function highlight(gis) {
      clear();
      if (!gis.length) return;
      current = gis;
      const want = { source: new Set(), target: new Set() };
      for (const gi of gis) { (gadgets[gi].sourceIds || []).forEach(id => want.source.add(id)); (gadgets[gi].targetIds || []).forEach(id => want.target.add(id)); }
      for (const [side, host] of Object.entries(hosts)) {
        host.classList.add("linking");
        const found = new Set();
        host.querySelectorAll("[data-vid]").forEach(el => {
          if (!want[side].has(el.dataset.vid)) return;
          found.add(el.dataset.vid);
          el.classList.add("vv-linked");
          ring(el);
        });
        const missing = [...want[side]].filter(id => !found.has(id));
        if (missing.length) note(`Gadget ids with no drawn piece in the ${side} pane: ${missing.slice(0, 8).join(", ")}${missing.length > 8 ? " …" : ""}`);
      }
      $("gadgets").querySelectorAll(".g").forEach(d => d.classList.toggle("hover", gis.includes(+d.dataset.gi)));
    }
    for (const [side, host] of Object.entries(hosts)) {
      host.addEventListener("pointerover", e => {
        const el = e.target.closest && e.target.closest("[data-vid]");
        if (!el || !host.contains(el)) { if (current) clear(); return; }
        highlight(idx[side].get(el.dataset.vid) || []);
      });
      host.addEventListener("pointerleave", () => clear());
    }
    S.link = { highlight, clear, refresh() { if (current) highlight(current); } };
  }

  /* ---------- mode + url ---------- */
  function setMode(m) {
    S.mode = m;
    $("mode-picture").setAttribute("aria-pressed", String(m === "picture"));
    $("mode-reduction").setAttribute("aria-pressed", String(m === "reduction"));
    $("pic-controls").hidden = m !== "picture";
    $("red-controls").hidden = m !== "reduction";
  }
  function syncUrl() {
    const p = new URLSearchParams();
    if (S.backend !== "http://localhost:27000") p.set("backend", S.backend);
    const th = document.documentElement.getAttribute("data-theme"); if (th) p.set("theme", th);
    p.set("mode", S.mode);
    if (S.mode === "picture") {
      p.set("problem", $("problem").value); p.set("vis", $("vis").value);
      if ($("solver").value) p.set("solver", $("solver").value);
      if ($("instance").value !== $("instance").dataset.default) p.set("instance", $("instance").value);
      if (!$("st-wrap").hidden && $("st-along").checked) p.set("along", "1");
    } else {
      p.set("reduction", $("reduction").value);
      if ($("red-solver").value) p.set("solver", $("red-solver").value);
      if ($("red-instance").value !== $("red-instance").dataset.default) p.set("instance", $("red-instance").value);
    }
    history.replaceState(null, "", "?" + p.toString());
  }

  $("mode-picture").onclick = async () => { setMode("picture"); if (!S.problems.length) await loadProblems().then(ok => ok && onProblem()); };
  $("mode-reduction").onclick = async () => { setMode("reduction"); if (!S.reductions) { await loadReductions(); await onReduction(); } };
  $("problem").onchange = () => onProblem();
  $("vis").onchange = () => updateAlong();
  $("run").onclick = runPicture;
  $("red-run").onclick = runReduction;
  $("reset-inst").onclick = () => { $("instance").value = $("instance").dataset.default || ""; };
  $("red-reset").onclick = () => { $("red-instance").value = $("red-instance").dataset.default || ""; };
  $("reduction").onchange = () => onReduction();
  $("check").onclick = async () => {
    S.backend = $("backend").value.trim(); localSet("vv-backend", S.backend);
    S.info.clear();
    const ok = await loadProblems();
    if (ok) { await onProblem(); S.reductions = null; if (S.mode === "reduction") { await loadReductions(); await onReduction(); } }
  };

  /* ---------- boot ---------- */
  (async () => {
    $("backend").value = S.backend;
    if (params.get("theme")) setTheme(params.get("theme"));
    const mode = params.get("mode") === "reduction" ? "reduction" : "picture";
    setMode(mode);
    try {
      const ok = await loadProblems();
      if (!ok) { setState("error"); return; }
      if (mode === "picture") {
        if (params.get("problem")) $("problem").value = params.get("problem").toUpperCase();
        await onProblem({ vis: params.get("vis"), solver: params.get("solver"), instance: params.get("instance") ?? undefined });
        if (params.get("along") === "1") $("st-along").checked = true;
        if (params.get("run") === "1") { if (params.get("along") === "1") await new Promise(r => setTimeout(r, 0)); updateAlong(); await runPicture(); } else setState("idle");
      } else {
        await loadReductions();
        if (params.get("reduction")) $("reduction").value = params.get("reduction");
        await onReduction({ solver: params.get("solver"), instance: params.get("instance") ?? undefined });
        if (params.get("run") === "1") await runReduction(); else setState("idle");
      }
    } catch (e) {
      console.warn(e);
      setState("error");
      const err = mode === "picture" ? $("pic-err") : $("red-err");
      err.hidden = false; err.textContent = e.message;
    }
  })();
})();
