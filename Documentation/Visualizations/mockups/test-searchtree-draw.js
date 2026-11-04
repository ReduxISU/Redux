// Drawing test for searchtree.js's serverFolds hook: a tree the backend already folded is drawn as given; without the
// flag searchtree.js folds a wide tree itself (past 12 leaves).
const { load, svg } = require("./dom-shim.js");
const SV = load("searchtree.js", "SearchTreeView");
let fails = 0;
const ok = (cond, msg) => { if (!cond) { fails++; console.log("  FAIL:", msg); } };

// Root with 4 finished children, each with 4 cut leaves: 16 leaves, wider than 12.
const tree = [{ id: "r", parent: null, label: "start", st: "explored" }];
for (let a = 0; a < 4; a++) {
  tree.push({ id: "a" + a, parent: "r", label: "a=" + a, st: "explored" });
  for (let b = 0; b < 4; b++) tree.push({ id: `a${a}b${b}`, parent: "a" + a, label: "b=" + b, st: "pruned" });
}
const frame = { tree, cur: null, done: false };
const draw = serverFolds => {
  const s = svg(), v = SV.create({ svg: s });
  v.loadModel({ kind: "viewer", game: false, serverFolds }, [frame]);
  v.show(0);
  return s.querySelectorAll("g.st-node").map(g => g.querySelector("text").textContent);
};
const client = draw(false), server = draw(true);
ok(client.some(t => /cut$/.test(t)) || client.some(t => /more$/.test(t)), "without serverFolds a 16-leaf tree is folded client-side (" + client.length + " nodes)");
ok(server.length === tree.length, "with serverFolds every node is drawn as sent (" + server.length + " of " + tree.length + ")");
ok(!server.some(t => /cut$|more$/.test(t)), "with serverFolds there are no client-made fold nodes");
console.log(fails ? `${fails} FAILURES` : "ALL PASS");
process.exit(fails ? 1 : 0);
