// In-page contrast audit. Injected with page.addScriptTag; exposes window.__audit(rootSelector, ctx).
// Text must reach 4.5:1 (3:1 at 24px+ or 18.66px bold); strokes, markers and control borders 3:1.
// Colors are composited with every ancestor opacity, then compared with what is painted underneath.
(() => {
  const cv = document.createElement("canvas"); cv.width = cv.height = 1;
  const cx = cv.getContext("2d", { willReadFrequently: true });
  const cache = new Map();
  function rgba(str) {
    if (!str || str === "none" || str === "transparent") return null;
    if (cache.has(str)) return cache.get(str);
    if (str.startsWith("url(")) { cache.set(str, null); return null; }
    cx.clearRect(0, 0, 1, 1); cx.fillStyle = "#000"; cx.fillStyle = str;
    cx.fillRect(0, 0, 1, 1);
    const d = cx.getImageData(0, 0, 1, 1).data;
    const v = d[3] === 0 ? null : [d[0], d[1], d[2], d[3] / 255];
    cache.set(str, v); return v;
  }
  const over = (fg, a, bg) => [0, 1, 2].map(i => fg[i] * a + bg[i] * (1 - a));
  const lum = c => { const f = v => { v /= 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; }; return 0.2126 * f(c[0]) + 0.7152 * f(c[1]) + 0.0722 * f(c[2]); };
  const ratio = (a, b) => { const x = lum(a), y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
  const hex = c => "#" + c.map(v => Math.round(v).toString(16).padStart(2, "0")).join("");
  function opacityChain(el) { let o = 1; for (let e = el; e && e.nodeType === 1; e = e.parentElement) { const s = getComputedStyle(e); o *= parseFloat(s.opacity); } return o; }
  // Solid color behind an HTML element: composite ancestor backgrounds from the page down.
  function htmlBg(el) {
    const chain = []; for (let e = el; e && e.nodeType === 1; e = e.parentElement) chain.push(e);
    let bg = [255, 255, 255];
    const root = rgba(getComputedStyle(document.body).backgroundColor) || rgba(getComputedStyle(document.documentElement).backgroundColor);
    if (root) bg = root.slice(0, 3);
    for (const e of chain.reverse()) { const c = rgba(getComputedStyle(e).backgroundColor); if (c) bg = over(c, c[3] * parseFloat(getComputedStyle(e).opacity), bg); }
    return bg;
  }
  const visible = el => { const r = el.getBoundingClientRect(); if (r.width === 0 && r.height === 0) return false; const s = getComputedStyle(el); return s.visibility !== "hidden" && s.display !== "none" && !el.closest("[hidden]"); };
  // Decoration, not content: plates behind labels (their text is checked), guide grids, panel separators, and gaps cut in the surface color.
  const EXEMPT_STROKE = ".gb-order circle,.st-ord circle,.sc-start,.au-elabel rect,.fl-lab rect,.gb-w rect,.ly-w rect,.ms-tip rect,.mc-elabel rect,.sc-grid,.ms-grid,.qc-grid,.pv-panel,.rd-sep,.pk-seg rect,.bd-queen *";
  const shapeSel = "rect,circle,ellipse,polygon,path";
  const desc = el => { const cls = (el.getAttribute("class") || "").trim().replace(/\s+/g, "."); let p = el.parentElement, pc = ""; for (let i = 0; i < 3 && p; i++, p = p.parentElement) { const c = (p.getAttribute("class") || "").trim().split(/\s+/)[0]; if (c) { pc = c; break; } } return `${el.tagName.toLowerCase()}${cls ? "." + cls : ""}${pc ? " in ." + pc : ""}`; };

  // What is painted under point (x,y) inside an svg, before element `self` in paint order.
  function svgBg(svg, self, x, y, base) {
    let bg = base;
    for (const s of svg.querySelectorAll(shapeSel)) {
      if (s === self) break;
      if (self.compareDocumentPosition(s) & Node.DOCUMENT_POSITION_FOLLOWING) break;
      if (!visible(s)) continue;
      const st = getComputedStyle(s); const f = rgba(st.fill); if (!f) continue;
      const fo = parseFloat(st.fillOpacity) * f[3] * opacityChain(s); if (fo < 0.02) continue;
      const r = s.getBoundingClientRect(); if (x < r.left || x > r.right || y < r.top || y > r.bottom) continue;
      if (s.tagName === "path" || s.tagName === "polygon") { try { const pt = svg.createSVGPoint(); const m = s.getScreenCTM().inverse(); pt.x = x; pt.y = y; const lp = pt.matrixTransform(m); if (!s.isPointInFill(lp)) continue; } catch { continue; } }
      if (s.tagName === "circle" || s.tagName === "ellipse") { const cxp = (r.left + r.right) / 2, cyp = (r.top + r.bottom) / 2, rx = r.width / 2, ry = r.height / 2; if (((x - cxp) / rx) ** 2 + ((y - cyp) / ry) ** 2 > 1) continue; }
      bg = over(f, fo, bg);
    }
    return bg;
  }

  window.__audit = (rootSel, ctx) => {
    const out = [];
    const add = (el, kind, fg, bg, need, extra = "") => { const r = ratio(fg, bg); if (r + 1e-6 < need) out.push({ ...ctx, kind, what: desc(el), fg: hex(fg), bg: hex(bg), ratio: +r.toFixed(2), need, text: extra }); };
    const roots = [...document.querySelectorAll(rootSel)].filter(visible);
    for (const root of roots) {
      // SVG content
      for (const svg of root.querySelectorAll("svg")) {
        if (!visible(svg)) continue;
        const base = htmlBg(svg);
        for (const el of svg.querySelectorAll("*")) {
          if (el.closest("defs,marker,clipPath,mask,pattern")) continue;
          if (!visible(el)) continue;
          const st = getComputedStyle(el); const op = opacityChain(el);
          if (op < 0.01) continue;
          const r = el.getBoundingClientRect(); const mx = (r.left + r.right) / 2, my = (r.top + r.bottom) / 2;
          if (el.tagName === "text" || el.tagName === "tspan") {
            if (el.tagName === "tspan" && el.parentElement.tagName === "text") continue;
            if (!el.textContent.trim()) continue;
            const f = rgba(st.fill); if (!f) continue;
            const bg = svgBg(svg, el, mx, my, base);
            const size = parseFloat(st.fontSize), bold = parseInt(st.fontWeight) >= 700;
            const need = size >= 24 || (size >= 18.66 && bold) ? 3 : 4.5;
            add(el, "svg-text", over(f, f[3] * parseFloat(st.fillOpacity) * op, bg), bg, need, el.textContent.trim().slice(0, 24));
            continue;
          }
          if (!el.matches("line,path,circle,ellipse,rect,polygon,polyline")) continue;
          if (el.closest(":disabled,.dis")) continue;
          const sk = rgba(st.stroke), sw = parseFloat(st.strokeWidth);
          if (sk && sw > 0 && !el.matches(EXEMPT_STROKE)) {
            const so = sk[3] * parseFloat(st.strokeOpacity) * op;
            if (so < 0.01) continue;
            // compare against what is outside the shape: the base under the svg (edges and outlines)
            const halo = /drop-shadow/.test(st.filter);
            const bg = halo ? base : el.tagName === "line" || el.tagName === "polyline" || (el.tagName === "path" && !rgba(st.fill)) ? svgBg(svg, el, mx, my, base) : svgBg(svg, el, mx, r.top + Math.min(1, r.height / 2), base);
            add(el, "svg-stroke", over(sk, so, bg), bg, 3);
          } else if (!sk) {
            const f = rgba(st.fill); if (!f) continue;
            const fo = f[3] * parseFloat(st.fillOpacity) * op; if (fo < 0.01) continue;
            if (r.width * r.height > 2500) continue; // large fills are panels and bands, judged by what sits on them
            const bg = svgBg(svg, el, mx, my, base);
            add(el, "svg-fill", over(f, fo, bg), bg, 3);
          }
        }
      }
      // HTML text and control borders
      const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
      const seen = new Set();
      for (let n; (n = walker.nextNode());) {
        const el = n.parentElement; if (!el || seen.has(el) || !n.textContent.trim()) continue;
        if (el.closest("svg")) continue;
        seen.add(el); if (!visible(el)) continue;
        if (el.closest(":disabled,.dis,option")) continue;
        const st = getComputedStyle(el); const c = rgba(st.color); if (!c) continue;
        const bg = htmlBg(el); const op = opacityChain(el);
        const size = parseFloat(st.fontSize), bold = parseInt(st.fontWeight) >= 700;
        const need = size >= 24 || (size >= 18.66 && bold) ? 3 : 4.5;
        add(el, "html-text", over(c, c[3] * op, bg), bg, need, n.textContent.trim().slice(0, 24));
      }
      for (const el of root.querySelectorAll("select,input,textarea")) {
        if (!visible(el) || el.disabled) continue;
        const st = getComputedStyle(el); const c = rgba(st.borderTopColor); if (!c || parseFloat(st.borderTopWidth) === 0) continue;
        const bg = htmlBg(el.parentElement);
        add(el, "control-border", over(c, c[3], bg), bg, 3);
      }
    }
    return out;
  };
})();
