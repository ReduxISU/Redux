/* ---- Automaton (#666): payload {kind:"dfa"|"nfa", layout:"layered", states[{id,label,layer}], start, accepting[ids], alphabet,
   transitions[{id,a,b,label,symbols[],epsilon}], input[chars], garbage?{id,label,missing[{state,symbol}]}, paths?[{id,label,accepted,outcome,frames}], pathsTruncated?}
   frames {event, caption, focus, position, active[ids], nodes{id:{state}}, edges{id:{state}}, ok?}
   Drawn with AutomatonView (../mockups/automaton.js) through its loadModel hook; states come from the backend as `ev`.
   NFA: a Path picker above the picture (the approved design) switches between all branches (the default, the response's
   own frames) and one path's run (payload.paths[i].frames, through usePath); the page's player then steps that run. */
(() => {
  const NODE_CLASS = { ElementHighlight: "Active", Solution: "Solution", Rejected: "Rejected", Untraveled: "Untraveled" };
  const EDGE_CLASS = { ElementHighlight: "hot" };

  Viewer.register("Automaton", {
    mount(ctx) {
      const { payload, note } = ctx;
      const svg = Viewer.svgIn(ctx.host, `${ctx.problem} automaton`);
      const tape = document.createElement("div");
      tape.style.marginTop = "10px";
      ctx.host.appendChild(tape);
      const view = AutomatonView.create({ svg, tape });
      // The mockup names states by their text; use the backend label as the name and map ids ↔ labels.
      const lbl = new Map(payload.states.map(s => [s.id, s.label ?? s.id]));
      if (new Set(lbl.values()).size !== lbl.size) note("Two states share a label; the mockup names states by their text, so they merge.");
      const garbage = payload.garbage ? payload.garbage.label || "Garbage" : null;
      if (payload.garbage) lbl.set(payload.garbage.id, garbage);
      const name = id => lbl.get(id) ?? id;
      const trans = [];
      for (const t of payload.transitions) for (const sym of t.symbols || [t.label]) trans.push({ from: name(t.a), sym: sym === "eps" || sym === "epsilon" ? "ε" : sym, to: name(t.b) });
      const mixed = payload.transitions.filter(t => (t.symbols || []).includes("ε") && t.symbols.length > 1 && !t.epsilon);
      if (mixed.length) note(`Transition ${mixed[0].id} mixes ε with symbols (${mixed[0].label}) and has epsilon:false; the mockup draws a solid edge (dashed only when every symbol is ε).`, false);
      const A = {
        states: payload.states.map(s => name(s.id)), alphabet: payload.alphabet, trans, start: name(payload.start),
        accept: new Set((payload.accepting || []).map(name)), input: payload.input || [], kind: payload.kind === "nfa" ? "NFA" : "DFA", garbage,
      };
      if (payload.garbage && payload.garbage.missing?.length) note(`Garbage state: the payload lists ${payload.garbage.missing.length} missing moves; the mockup draws the Garbage box with no edges into it (same as the design).`, false);
      if (payload.pathsTruncated) note(`NFA payload sends ${(payload.paths || []).length} per-path runs and says the list is truncated; the picker offers those.`, false);
      const SEP = view.SEP;
      const groupKey = new Map(payload.transitions.map(t => [t.id, name(t.a) + SEP + name(t.b)]));
      const convert = list => { let prevPos = 0; return list.map(f => {
        const nodes = new Map(), edges = new Map();
        for (const id of Object.keys(f.nodes || {})) {
          const st = Viewer.stateOf(f.nodes, id);
          if (st === "Background") continue;
          if (NODE_CLASS[st]) nodes.set(name(id), NODE_CLASS[st]); else note(`State "${st}" on an automaton state has no style in automaton.js.`);
        }
        for (const id of Object.keys(f.edges || {})) {
          const st = Viewer.stateOf(f.edges, id);
          if (st === "Background") continue;
          if (!groupKey.has(id)) { note(`Edge id ${id} is not a transition id in the payload.`); continue; }
          if (EDGE_CLASS[st]) edges.set(groupKey.get(id), EDGE_CLASS[st]); else note(`Edge state "${st}" has no style in automaton.js.`);
        }
        const pos = f.position ?? 0, moved = pos > prevPos;
        prevPos = pos;
        const done = f.event === "Done";
        return { active: (f.active || []).map(name), taken: [], visited: new Set(), pos, sym: moved ? A.input[pos - 1] : undefined,
          done, accepted: f.ok === true, caption: f.caption, ev: { nodes, edges } };
      }); };
      const frames = convert(ctx.frames);
      const runs = (payload.paths || []).filter(p => Array.isArray(p.frames) && p.frames.length);
      view.loadModel(A, frames, runs.map(p => ({ label: p.label, accepted: p.accepted, frames: convert(p.frames) })));
      // One-path picker: all branches (default) or one path. Picking a path replaces the frames the page steps through.
      if (runs.length && ctx.setFrames) {
        const wrap = document.createElement("label");
        wrap.className = "f inline vv-path-pick";
        wrap.textContent = "Path ";
        const sel = document.createElement("select");
        sel.setAttribute("aria-label", "Follow one path");
        const opt = (v, t) => { const o = document.createElement("option"); o.value = v; o.textContent = t; sel.appendChild(o); };
        opt("", `All branches at once (${runs.length} paths)`);
        runs.forEach((p, i) => opt(String(i), p.label || `Path ${i + 1}: ${p.accepted ? "accepts" : "rejects"}`));
        wrap.appendChild(sel);
        ctx.host.insertBefore(wrap, svg);
        sel.addEventListener("change", () => {
          const i = sel.value === "" ? null : +sel.value;
          view.usePath(i);
          ctx.setFrames(i === null ? ctx.frames : runs[i].frames);
        });
      }
      // Tag: states in payload order (+ garbage), edge paths per (from,to) group in first-appearance order.
      const nodeGs = [...svg.querySelectorAll("g.au-node")];
      nodeGs.forEach((g, i) => Viewer.tag(g, payload.states[i].id));
      const gb = svg.querySelector("g.au-garbage"); if (gb && payload.garbage) Viewer.tag(gb, payload.garbage.id);
      const seen = new Map();
      payload.transitions.forEach(t => { const k = groupKey.get(t.id); if (!seen.has(k)) seen.set(k, t.id); });
      [...svg.querySelectorAll("path.au-edge")].forEach((p, i) => Viewer.tag(p, [...seen.values()][i]));
      return { show(i) { view.show(i); } };
    },
  });
})();
