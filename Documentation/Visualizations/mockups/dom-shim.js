// A tiny DOM for the drawing tests (node, no dependencies): enough of createElementNS / attributes / children /
// classList / querySelectorAll for the mockup renderers to draw into, so a test can check what got drawn.
// innerHTML only supports clearing, or storing an HTML string that the test then reads back as a string.
// Usage: const { document, load } = require("./dom-shim.js"); const PV = load("packing.js", "PackingView");
const fs = require("fs");

class El {
  constructor(tag) { this.tagName = tag; this.attrs = {}; this.children = []; this.parentNode = null; this.style = {}; this._text = ""; this._html = null; this.listeners = {}; }
  setAttribute(k, v) { this.attrs[k] = String(v); }
  getAttribute(k) { return k in this.attrs ? this.attrs[k] : null; }
  hasAttribute(k) { return k in this.attrs; }
  removeAttribute(k) { delete this.attrs[k]; }
  appendChild(c) { if (c.parentNode) c.parentNode.removeChild(c); c.parentNode = this; this.children.push(c); this._html = null; return c; }
  append(...cs) { cs.forEach(c => this.appendChild(c)); }
  insertBefore(c, ref) { if (c.parentNode) c.parentNode.removeChild(c); const i = ref ? this.children.indexOf(ref) : -1; c.parentNode = this; if (i < 0) this.children.push(c); else this.children.splice(i, 0, c); return c; }
  removeChild(c) { const i = this.children.indexOf(c); if (i >= 0) this.children.splice(i, 1); c.parentNode = null; return c; }
  remove() { if (this.parentNode) this.parentNode.removeChild(this); }
  get firstChild() { return this.children[0] || null; }
  get lastChild() { return this.children[this.children.length - 1] || null; }
  get nextSibling() { const p = this.parentNode; if (!p) return null; return p.children[p.children.indexOf(this) + 1] || null; }
  get textContent() { return this._text + this.children.map(c => c.textContent).join(""); }
  set textContent(v) { this.children = []; this._text = String(v); this._html = null; }
  get innerHTML() { return this._html ?? ""; }
  set innerHTML(v) { this.children.forEach(c => (c.parentNode = null)); this.children = []; this._text = ""; this._html = String(v) || null; }
  get className() { return this.attrs.class || ""; }
  set className(v) { this.attrs.class = String(v); }
  get classList() {
    const el = this, get = () => (el.attrs.class || "").split(/\s+/).filter(Boolean), set = l => { el.attrs.class = l.join(" "); };
    return {
      add: (...c) => set([...new Set([...get(), ...c])]), remove: (...c) => set(get().filter(x => !c.includes(x))),
      contains: c => get().includes(c), toggle: (c, on) => { const has = get().includes(c); if (on === undefined) on = !has; if (on && !has) set([...get(), c]); if (!on && has) set(get().filter(x => x !== c)); return on; },
    };
  }
  get dataset() {
    const el = this;
    return new Proxy({}, { get: (_, k) => el.attrs["data-" + String(k).replace(/[A-Z]/g, m => "-" + m.toLowerCase())], set: (_, k, v) => { el.attrs["data-" + String(k).replace(/[A-Z]/g, m => "-" + m.toLowerCase())] = String(v); return true; } });
  }
  addEventListener(t, f) { (this.listeners[t] ||= []).push(f); }
  removeEventListener() {}
  dispatch(t) { (this.listeners[t] || []).forEach(f => f({ type: t, target: this })); }
  getComputedTextLength() { return this.textContent.length * 7; }
  getBBox() { return { x: 0, y: 0, width: 10, height: 10 }; }
  cloneNode() { const e = new El(this.tagName); e.attrs = { ...this.attrs }; return e; }
  get ownerSVGElement() { let p = this.parentNode; while (p && p.tagName !== "svg") p = p.parentNode; return p; }
  /** All descendants, depth first. */
  all() { const out = []; const rec = e => e.children.forEach(c => { out.push(c); rec(c); }); rec(this); return out; }
  matches(sel) { return sel.split(",").some(s => matchChain(this, s.trim().replace(/^:scope\s*>\s*/, "").split(/\s+/))); }
  querySelectorAll(sel) { return this.all().filter(e => e.matches(sel)); }
  querySelector(sel) { return this.querySelectorAll(sel)[0] || null; }
  closest(sel) { let e = this; while (e && e.matches) { if (e.matches(sel)) return e; e = e.parentNode; } return null; }
}
// "tag.a.b[attr]" against one element
function matchOne(el, s) {
  const m = /^([a-zA-Z][\w-]*|\*)?((?:[.#][\w-]+|\[[^\]]+\])*)$/.exec(s);
  if (!m) throw new Error("dom-shim: unsupported selector " + s);
  if (m[1] && m[1] !== "*" && el.tagName !== m[1]) return false;
  const cls = (el.attrs.class || "").split(/\s+/);
  for (const part of m[2].match(/[.#][\w-]+|\[[^\]]+\]/g) || []) {
    if (part[0] === ".") { if (!cls.includes(part.slice(1))) return false; }
    else if (part[0] === "#") { if (el.attrs.id !== part.slice(1)) return false; }
    else { const [k, v] = part.slice(1, -1).split("="); if (!(k in el.attrs)) return false; if (v !== undefined && el.attrs[k] !== v.replace(/^["']|["']$/g, "")) return false; }
  }
  return true;
}
function matchChain(el, parts) {
  if (!matchOne(el, parts[parts.length - 1])) return false;
  if (parts.length === 1) return true;
  let p = el.parentNode;
  while (p && p.tagName) { if (matchChain(p, parts.slice(0, -1))) return true; p = p.parentNode; }
  return false;
}

const head = new El("head"), body = new El("body");
const document = {
  head, body,
  createElementNS: (ns, tag) => new El(tag),
  createElement: tag => new El(tag),
  getElementById: id => [head, body].flatMap(r => [r, ...r.all()]).find(e => e.attrs.id === id) || null,
};
global.document = document;
global.window = global.window || {};

/** Load a classic-script renderer from this folder and return its global (e.g. "PackingView"). */
function load(file, name) { return (0, eval)(fs.readFileSync(__dirname + "/" + file, "utf8") + ";" + name); }
/** A fresh <svg> to draw into. */
const svg = () => { const s = new El("svg"); body.appendChild(s); return s; };
const div = () => { const d = new El("div"); body.appendChild(d); return d; };

module.exports = { document, El, load, svg, div };
