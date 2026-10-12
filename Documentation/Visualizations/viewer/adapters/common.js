/* ---- Viewer adapter registry and shared helpers ----
   One adapter per picture type turns the backend's format=frames JSON into what a mockup renderer
   (../mockups/*.js) expects, and draws it. Contract:

     Viewer.register(type, { mount(ctx) { ...; return { show(i), destroy?() } } })

   ctx = {
     host,          // an empty <div> to draw into
     type,          // the response's "type" ("Graph", "Packing", ...)
     payload,       // response.payload: what does not change between frames
     frames,        // response.frames: one per kept solver step, then the solved frame
                    //   (in a Reduction pane: that pane's part of each frame, plus caption/event/ok/phase copied in)
     instance,      // the instance string the backend was given
     problem, visualization, solver,
     pane,          // "main", "side" (search tree alongside), "source" or "target" (Reduction View)
     buildOrder,    // Reduction target pane only: {nodes:[ids], edges:[ids]} in the order the construction adds them.
                    //   A build frame's builtNodes / builtEdges say how many of each are on screen: the FIRST n of buildOrder
                    //   (not of the payload order). Hide the rest. Solve/map frames omit both (everything is built).
                    //   A build frame may also carry skipped: [{id,a,b}] pairs the rule considered and left without an edge.
     note(text, warn = true),  // record a mismatch between the backend JSON and the mockup (shown in the side panel; deduped)
   }
   show(i) draws frames[i] (0 <= i < frames.length).

   For the Reduction View's gadget linking, tag every drawn piece that has a backend id with
   data-vid="<backend id>" (Viewer.tag helps). The page does the hover and highlighting.

   State vocabulary (backend PictureState): Background, ElementHighlight, Solution, Rejected, Untraveled,
   Covered, Blocked, False. Precedence when one piece has several: Rejected > ElementHighlight > Solution >
   Covered > Blocked > False > Background. */
const Viewer = (() => {
  const adapters = {};
  const STATES = ["Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked", "False"];
  const PRECEDENCE = ["Rejected", "ElementHighlight", "Solution", "Covered", "Blocked", "False", "Untraveled", "Background"];
  const NS = "http://www.w3.org/2000/svg";
  const mk = (tag, attrs = {}, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; };

  /** The state of an entry in a frame map: {id: {state}} or {id: "State"}. Missing means Background. */
  const stateOf = (map, id) => {
    if (!map) return "Background";
    const v = map[id];
    if (v == null) return "Background";
    return typeof v === "string" ? v : v.state || "Background";
  };
  /** Highest-precedence state of a list. */
  const strongest = list => { let best = "Background"; for (const s of list) if (PRECEDENCE.indexOf(s) < PRECEDENCE.indexOf(best)) best = s; return best; };

  /** Every state name used anywhere in a run (for the legend). */
  function statesIn(value, out = new Set()) {
    if (Array.isArray(value)) value.forEach(v => statesIn(v, out));
    else if (value && typeof value === "object") {
      for (const [k, v] of Object.entries(value)) {
        if ((k === "state" || k === "st") && typeof v === "string" && STATES.includes(v)) out.add(v);
        else if (typeof v === "string" && STATES.includes(v) && k !== "event" && k !== "caption") out.add(v);
        else statesIn(v, out);
      }
    }
    return out;
  }

  /** Tag an element with a backend id for gadget linking. */
  const tag = (el, id) => { if (el && id != null) el.setAttribute("data-vid", String(id)); return el; };

  /** A fresh <svg> inside host (most mockup views draw into an svg they are given). */
  function svgIn(host, label) {
    const svg = mk("svg", { role: "img", "aria-label": label || "Visualization" });
    host.appendChild(svg);
    return svg;
  }

  return {
    adapters, STATES, PRECEDENCE, NS, mk, stateOf, strongest, statesIn, tag, svgIn,
    register(type, impl) { adapters[type] = impl; },
    has(type) { return !!adapters[type]; },
    mount(ctx) {
      const a = adapters[ctx.type];
      if (!a) throw new Error(`No viewer adapter for picture type "${ctx.type}".`);
      return a.mount(ctx);
    },
  };
})();
