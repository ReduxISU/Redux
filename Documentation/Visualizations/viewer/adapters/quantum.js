/* ---- Quantum circuit (#672): payload {kind:"quantumCircuit", problem, qubits[{id,name,role}], columns[{id, gates[{id,type,qubits[],label?,oracle?,control?}]}],
   stages[{label,from,to}], measure[], readout, inputBits, simulated, note?, number?}
   frames {event, caption, focus, applied (columns applied so far), queries[{input,output}], columns{id:{state}}, gates{id:{state}}, qubits{id:{state}}, result?, ok?}
   Drawn with QuantumView (../mockups/quantum.js). "simulated": true means the frontend simulates the circuit, which the mockup does in the
   browser from the instance: load() builds the mockup's own circuit + statevector snapshots, useFrames() then shows the backend's frames
   (applied → which columns are done, and that column's snapshot for the bars). The backend's column/gate states are applied on top. */
(() => {
  const KEYS = { BERNSTEINVAZIRANI: "BERNSTEINVAZIRANI", DEUTSCH: "DEUTSCH", DEUTSCHJOZSA: "DEUTSCHJOZSA", SIMON: "SIMON", UNSTRUCTUREDSEARCH: "UNSTRUCTUREDSEARCH", PRIMEFACTOR: "PRIMEFACTOR",
    bernsteinVazirani: "BERNSTEINVAZIRANI", deutsch: "DEUTSCH", deutschJozsa: "DEUTSCHJOZSA", simon: "SIMON", unstructuredSearch: "UNSTRUCTUREDSEARCH", primeFactorization: "PRIMEFACTOR" };
  const esc = s => String(s).replace(/[&<>"]/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));

  function ensureCss() {
    if (document.getElementById("vv-quantum-style")) return;
    const st = document.createElement("style"); st.id = "vv-quantum-style";
    st.textContent = `.qc-svg .vv-q-hl { stroke: var(--av-hl); stroke-width: 2.6; fill: var(--av-hl-fill); }
.qc-svg .vv-q-sol { stroke: var(--av-sol); stroke-width: 2.6; fill: var(--av-sol-fill); }
.qc-svg .vv-q-rej { stroke: var(--av-rej); stroke-width: 2.6; fill: var(--av-rej-fill); }
.vv-queries { font: 12.5px var(--av-mono); color: var(--av-ink); display: flex; flex-wrap: wrap; gap: 6px; align-items: center; margin-top: 6px; }
.vv-queries .lab { color: var(--av-muted); }
.vv-queries span.q { border: 1.2px solid var(--av-stroke); border-radius: 6px; padding: 1px 7px; }
.vv-queries span.q.last { border-color: var(--av-hl); box-shadow: 0 0 0 1.5px var(--av-hl); }
.vv-queries .res { color: var(--av-sol-ink); font-weight: 600; }
.vv-queries .res.bad { color: var(--av-rej-ink); }`;
    document.head.appendChild(st);
  }
  const GATE_CLASS = { ElementHighlight: "vv-q-hl", Solution: "vv-q-sol", Covered: "vv-q-sol", Rejected: "vv-q-rej" };

  /** Number of SVG children the mockup draws for one gate, and which of them is the gate's body. */
  const gateParts = g => (g.type === "measure" ? [3, 0] : g.type === "box" ? (g.ctrl !== undefined ? [4, 2] : [2, 0]) : [2, 0]);

  function compareCircuit(circ, payload, note) {
    if (!circ) {
      if (payload.columns.length) note(`The mockup builds no circuit for this instance, the backend sends ${payload.columns.length} columns.`);
      return false;
    }
    let same = true;
    if (circ.wires.length !== payload.qubits.length) { note(`Qubits: backend ${payload.qubits.length}, mockup ${circ.wires.length}.`); same = false; }
    payload.qubits.forEach((q, k) => { const w = circ.wires[k]; if (w && (w.name !== q.name || w.role !== q.role)) note(`Qubit ${q.id}: backend ${q.name}/${q.role}, mockup ${w.name}/${w.role}.`); });
    if (circ.columns.length !== payload.columns.length) { note(`Columns: backend ${payload.columns.length}, mockup ${circ.columns.length}.`); same = false; }
    payload.columns.forEach((c, i) => {
      const mc = circ.columns[i];
      if (!mc) return;
      const a = c.gates.map(g => `${g.type}${g.label ? ":" + g.label : ""}[${g.qubits.join(",")}]${g.control != null ? "c" + g.control : ""}`).join(" ");
      const b = mc.gates.map(g => `${g.type}${g.label ? ":" + g.label : ""}[${g.q.join(",")}]${g.ctrl != null ? "c" + g.ctrl : ""}`).join(" ");
      if (a !== b) { note(`Column ${c.id}: backend ${a} vs mockup ${b}.`); if (c.gates.length !== mc.gates.length) same = false; }
    });
    const ms = (s, i) => s && `${s.label}:${s.from}-${s.to}`;
    const sa = payload.stages.map(ms).join(" "), sb = circ.stages.map(ms).join(" ");
    if (sa !== sb) note(`Stages: backend ${sa} vs mockup ${sb}.`);
    return same;
  }

  /** "(1,0,1)" or "101" → register value with the first bit most significant (the mockup's bit order), else null. */
  function outcomeOf(result, nbits) {
    if (result == null) return null;
    const bits = String(result).replace(/[()\s,]/g, "");
    return /^[01]+$/.test(bits) && bits.length === nbits ? parseInt(bits, 2) : null;
  }

  Viewer.register("QuantumCircuit", {
    mount(ctx) {
      ensureCss();
      const { payload, note } = ctx;
      const key = KEYS[String(ctx.problem || "").toUpperCase()] || KEYS[payload.problem];
      if (!key) throw new Error(`No mockup circuit for problem "${ctx.problem}" / "${payload.problem}".`);
      const svg = Viewer.svgIn(ctx.host, `${ctx.problem} quantum circuit`);
      const extraBox = document.createElement("div"); extraBox.className = "vv-queries"; ctx.host.appendChild(extraBox);
      const view = QuantumView.create({ svg });
      const res = view.load(ctx.instance, key);
      const { B } = view.useFrames(res.frames);
      const circ = B.circ;
      const sameShape = compareCircuit(circ, payload, note);
      if (payload.note) note(`Payload note: ${payload.note}`, false);
      if (payload.simulated === false && payload.columns.length) note("payload.simulated is false (the backend does not simulate this circuit), so the bars come from the mockup's in-browser simulation.", false);
      // Snapshot (bars) per number of applied columns, from the mockup's simulation.
      const dist = new Map();
      for (const f of res.frames) if (f.dist && !f.classical && !dist.has(f.col)) dist.set(f.col, f.dist);
      const C = circ ? circ.columns.length : 0, nbits = circ ? circ.reg.length : 0;
      const known = new Set(["event", "caption", "focus", "applied", "queries", "columns", "gates", "qubits", "result", "ok", "phase", "bars"]);
      const extra = [...new Set(ctx.frames.flatMap(f => Object.keys(f)))].filter(k => !known.has(k));
      if (extra.length) note(`Frame fields the Quantum adapter ignores: ${extra.join(", ")}`, false);
      if (ctx.frames.some(f => f.queries && f.queries.length)) note("Classical oracle queries (frame.queries) have no place in the mockup circuit; the viewer lists them under it.", false);
      if (ctx.frames.some(f => Object.keys(f.qubits || {}).length)) note("Qubit states (frame.qubits) are not drawn by quantum.js.");

      if (ctx.frames.some(f => Array.isArray(f.bars))) note("Frames carry bars[{outcome,probability,sign}] (the backend's own simulation); the viewer draws those and checks them against the mockup's in-browser simulation.", false);
      /** Backend bars → the mockup's dist {probs, signs}; compared with the mockup's simulation of the same column. */
      function barsDist(bars, nb, sim, applied) {
        const N = 1 << nb, probs = new Array(N).fill(0), signs = new Array(N).fill(1);
        let anySign = false;
        for (const b of bars) {
          if (!(b.outcome >= 0 && b.outcome < N)) { note(`Bar outcome ${b.outcome} is outside the ${nb}-bit register.`); continue; }
          probs[b.outcome] = b.probability; if (b.sign != null) { signs[b.outcome] = b.sign < 0 ? -1 : 1; anySign = true; }
        }
        if (sim) {
          const bad = sim.probs.findIndex((p, k) => Math.abs(p - probs[k]) > 1e-6);
          if (bad >= 0) note(`After ${applied} columns the backend's bars and the mockup's simulation differ (outcome ${bad}: ${probs[bad]} vs ${sim.probs[bad].toFixed(4)}).`);
          else if (sim.signs && anySign && sim.signs.some((g, k) => probs[k] > 1e-9 && Math.sign(g) !== signs[k])) note(`After ${applied} columns the backend's amplitude signs differ from the mockup's simulation.`);
        }
        return { probs, signs: anySign ? signs : (sim ? sim.signs : null), r: sim ? sim.r : undefined };
      }
      const frames = ctx.frames.map(f => {
        const applied = f.applied ?? 0;
        const done = f.event === "Done";
        const sim = applied > 0 ? dist.get(Math.min(applied, C)) || null : null;
        const out = { col: Math.min(applied, C), dist: Array.isArray(f.bars) && C ? barsDist(f.bars, nbits, sim, applied) : sim, classical: applied === 0, done, ok: f.ok, caption: f.caption, answer: f.result };
        if (applied >= C && C) {
          const o = outcomeOf(f.result, nbits);
          out.outcome = o;
          if (done && f.result != null && o === null && f.ok !== false && key !== "SIMON" && key !== "DEUTSCH" && key !== "DEUTSCHJOZSA") note(`Result "${f.result}" is not a reading of the measured register, so no bits are written on the wires.`, false);
        }
        if (done && f.ok === false && typeof f.result === "string" && /error/i.test(f.result)) note(`The run failed: result carries a raw error (${f.result.slice(0, 120)}…) and ok:false.`);
        if (applied > C) note(`applied = ${applied} is past the ${C} columns.`);
        return out;
      });
      view.useFrames(frames);

      function decorate(i) {
        const f = ctx.frames[i];
        const cols = [...svg.querySelectorAll("g.qc-col")];
        cols.forEach((g, c) => {
          const pc = payload.columns[c];
          if (!pc) return;
          const s = Viewer.stateOf(f.columns, pc.id);
          if (s === "ElementHighlight") g.setAttribute("class", "qc-col now");
          else if (s === "Untraveled") g.setAttribute("class", "qc-col future");
          else if (s !== "Background") note(`Column state "${s}" is not drawn by quantum.js.`);
          if (!sameShape) return;
          // Gate bodies, in the mockup's drawing order (it draws the same gates in the same order when the shapes match).
          let k = 0;
          circ.columns[c].gates.forEach((mg, gi) => {
            const [count, body] = gateParts(mg), el = g.children[k + body], pg = pc.gates[gi];
            k += count;
            if (!el || !pg) return;
            Viewer.tag(el, pg.id);
            el.classList.remove("vv-q-hl", "vv-q-sol", "vv-q-rej");
            const gs = Viewer.stateOf(f.gates, pg.id);
            if (GATE_CLASS[gs]) el.classList.add(GATE_CLASS[gs]);
            else if (gs !== "Background") note(`Gate state "${gs}" is not drawn by quantum.js.`);
          });
        });
        const qs = f.queries || [];
        const bitsIn = Math.max(1, payload.inputBits || 1);
        extraBox.innerHTML = (qs.length ? `<span class="lab">Oracle queries:</span>` + qs.map((q, j) => `<span class="q${j === qs.length - 1 && f.event !== "Done" ? " last" : ""}">f(${q.input.toString(2).padStart(bitsIn, "0")}) = ${esc(q.output)}</span>`).join("") : "")
          + (f.result != null ? ` <span class="lab">result:</span> <span class="res${f.ok === false ? " bad" : ""}">${esc(String(f.result).slice(0, 80))}</span>` : "");
      }
      return { show(i) { view.show(i); decorate(i); } };
    },
  });
})();
