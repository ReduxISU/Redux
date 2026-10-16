/* ---- Schedule (#673)
   Job sequencing: payload {kind:"schedule", rule:"jobSequencing", jobs[{id,label,time,deadline,penalty}], k, span}
                   frames {event, caption, focus, order[job ids], runs[{job,start,end,late}], jobs{id:{state}}, penalty, ok?}
   Pump scheduling: payload {rule:"costMinimization"|"emergencyResilience", hours[{id,label,demand,peak}], pumps[{id,name,flow,power,startupCost}],
                   tank{capacity,level,minimum}, rates{onPeak,offPeak}, budget?}
                   frames {event, caption, focus, reached[{states,low,high,best}], schedule[{hour,on[],starts[],inflow,level,energy,startup}],
                   upTo, hours{id:{state}}, cells{"<pump><hour>":{state}}, budget?, ok?}
   Drawn with ScheduleView (../mockups/schedule.js) through its loadFrames hook (instance parsed by the mockup, frames from the backend).
   Job bars take the backend's job states through the `states` hook; pump frames are rebuilt in the mockup's own terms
   (forward pass stats, masks, simulated hours) from reached/schedule. */
(() => {
  const JOB_CLASS = { ElementHighlight: "Active", Solution: "Solution", Covered: "Covered", Rejected: "Rejected", Background: "Background" };

  function jobFrames(ctx, I) {
    const { payload, note } = ctx;
    const idx = new Map(payload.jobs.map((j, i) => [j.id, i]));
    payload.jobs.forEach((j, i) => {
      if (j.time !== I.T[i] || j.deadline !== I.D[i] || j.penalty !== I.P[i]) note(`Job ${j.id}: payload (${j.time},${j.deadline},${j.penalty}) differs from the instance (${I.T[i]},${I.D[i]},${I.P[i]}).`);
    });
    if (payload.jobs.some((j, i) => j.label !== `Job ${i}`)) note("Job labels differ from the mockup's \"Job <index>\" names.", false);
    if (payload.jobs.some((j, i) => j.id !== `J${i}`)) note(`Job ids are 1-based (${payload.jobs[0].id} = "${payload.jobs[0].label}") while labels and the certificate are 0-based; the mockup only uses 0-based indices.`, false);
    if (payload.k !== I.K) note(`k: payload ${payload.k}, instance ${I.K}.`);
    return ctx.frames.map(f => {
      const order = (f.order || []).map(id => idx.get(id));
      // Check the backend's runs against the timeline the mockup would draw for the same order.
      let t = 0;
      order.forEach((j, k) => {
        const r = (f.runs || [])[k], s = t, e = t + I.T[j]; t = e;
        if (!r || idx.get(r.job) !== j || r.start !== s || r.end !== e || r.late !== (e > I.D[j])) note(`Run ${k} of order ${f.order.join(",")}: backend ${JSON.stringify(r)}, mockup timeline ${s}-${e}${e > I.D[j] ? " late" : ""}.`);
      });
      const states = {};
      for (const id of Object.keys(f.jobs || {})) {
        const s = Viewer.stateOf(f.jobs, id);
        if (JOB_CLASS[s]) states[idx.get(id)] = JOB_CLASS[s];
        else note(`Job state "${s}" has no style in schedule.js.`);
      }
      return { order, states, done: f.event === "Done", ok: f.ok, caption: f.caption };
    });
  }

  function pumpFrames(ctx, I) {
    const { payload, note } = ctx;
    const pumps = payload.pumps, pidx = new Map(pumps.map((p, i) => [p.id, i]));
    pumps.forEach((p, i) => { const q = I.pumps[i]; if (!q || q.name !== p.name || q.flow !== p.flow || q.kw !== p.power || q.start !== p.startupCost) note(`Pump ${p.id}: payload differs from the instance.`); });
    if (payload.tank.capacity !== I.cap || payload.tank.level !== I.level || (payload.tank.minimum || 0) !== I.minLevel) note("Tank: payload differs from the instance.");
    payload.hours.forEach((h, k) => { if (h.demand !== I.demand[k] || h.peak !== I.peaks.has(k)) note(`Hour ${h.id}: payload demand/peak differs from the instance.`); });
    return ctx.frames.map(f => {
      const stats = (f.reached || []).map((r, h) => ({ h, count: r.states, lo: r.low, hi: r.high, best: r.best }));
      const budget = f.budget ?? payload.budget ?? null;
      const out = { caption: f.caption, stats, budget: I.kind === "em" ? budget : null };
      const sched = f.schedule || [];
      for (const [id, v] of Object.entries(f.hours || {})) if (v.state !== "ElementHighlight") note(`Hour state "${v.state}" (${id}) is not drawn by schedule.js.`);
      const hl = Object.keys(f.hours || {}).filter(id => Viewer.stateOf(f.hours, id) === "ElementHighlight");
      if (sched.length) {
        const masks = sched.map(s => s.on.reduce((m, id) => m | (1 << pidx.get(id)), 0));
        const hours = sched.map((s, h) => ({ m: masks[h], inflow: s.inflow, level: s.level, energy: s.energy, startup: s.startup, cost: s.energy + s.startup,
          startups: pumps.map(p => s.starts.includes(p.id)) }));
        const levels = hours.map(r => r.level);
        const sim = { hours, total: hours.reduce((a, r) => a + r.cost, 0), low: Math.min(...levels), high: Math.max(...levels),
          ok: levels.every(v => v >= I.minLevel && v <= I.cap), stored: levels.reduce((a, b) => a + b, 0) };
        Object.assign(out, { masks, sim });
        // Backend cell states vs the schedule's on/off.
        for (const [id, v] of Object.entries(f.cells || {})) {
          const m = /^(.+?)(t\d+)$/.exec(id), p = m && pidx.get(m[1]), h = m && payload.hours.findIndex(x => x.id === m[2]);
          if (p == null || h < 0) { note(`Cell id "${id}" is not <pump id><hour id>.`); continue; }
          if (v.state !== "Solution") note(`Cell state "${v.state}" is not drawn by schedule.js (it draws on/off from the schedule).`);
          else if (!(masks[h] & (1 << p))) note(`Cell ${id} is Solution but the pump is off that hour.`);
        }
        if (f.event === "Done") Object.assign(out, { phase: "done", done: true, ok: f.ok });
        else if (hl.length) Object.assign(out, { phase: "replay", cursor: f.upTo ?? payload.hours.findIndex(x => x.id === hl[0]) });
        else out.phase = "trace";
      } else if (f.event === "Done") Object.assign(out, { phase: "done", done: true, ok: f.ok });
      else out.phase = stats.length ? "forward" : "start";
      return out;
    });
  }

  Viewer.register("Schedule", {
    mount(ctx) {
      const { payload, note } = ctx;
      const svg = Viewer.svgIn(ctx.host, `${ctx.problem} schedule`);
      const view = ScheduleView.create({ svg });
      const key = payload.rule === "jobSequencing" ? "JOBSEQ" : payload.rule === "emergencyResilience" ? "PUMPEM" : "PUMPCM";
      const { I } = view.loadFrames(ctx.instance, key, []);
      const frames = key === "JOBSEQ" ? jobFrames(ctx, I) : pumpFrames(ctx, I);
      if (key === "JOBSEQ" && payload.span !== I.T.reduce((a, b) => a + b, 0)) note(`span ${payload.span} is not the sum of the job times.`, false);
      const known = new Set(["event", "caption", "focus", "order", "runs", "jobs", "penalty", "ok", "phase", "reached", "schedule", "upTo", "hours", "cells", "budget"]);
      const extra = [...new Set(ctx.frames.flatMap(f => Object.keys(f)))].filter(k => !known.has(k));
      if (extra.length) note(`Frame fields the Schedule adapter ignores: ${extra.join(", ")}`, false);
      if (key !== "JOBSEQ") note("Pump frames: the mockup's forward-pass band and replay are rebuilt from reached/schedule/upTo; the backend's per-cell Solution states only confirm the on/off cells.", false);
      view.loadFrames(ctx.instance, key, frames);
      return {
        show(i) {
          view.show(i);
          if (key === "JOBSEQ") [...svg.querySelectorAll("g.sc-row")].forEach((g, j) => Viewer.tag(g, payload.jobs[j] && payload.jobs[j].id));
          else {
            const cells = [...svg.querySelectorAll("rect.sc-cell")], H = payload.hours.length;
            cells.forEach((c, k) => { const p = payload.pumps[Math.floor(k / H)], h = payload.hours[k % H]; if (p && h) Viewer.tag(c, p.id + h.id); });
          }
        },
      };
    },
  });
})();
