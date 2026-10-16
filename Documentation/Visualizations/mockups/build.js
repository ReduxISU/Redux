const fs = require("fs");
const mods = { "/*@@AUTOMATON@@*/": fs.readFileSync("automaton.js", "utf8"), "/*@@BIPARTITE@@*/": fs.readFileSync("bipartite.js", "utf8"), "/*@@GRAPHBASE@@*/": fs.readFileSync("graphbase.js", "utf8"), "/*@@FLOW@@*/": fs.readFileSync("flow.js", "utf8"), "/*@@LAYERED@@*/": fs.readFileSync("layered.js", "utf8"), "/*@@PACKING@@*/": fs.readFileSync("packing.js", "utf8"), "/*@@SCHEDULE@@*/": fs.readFileSync("schedule.js", "utf8"), "/*@@QUANTUM@@*/": fs.readFileSync("quantum.js", "utf8"), "/*@@BOARD@@*/": fs.readFileSync("board.js", "utf8"), "/*@@TABLE@@*/": fs.readFileSync("table.js", "utf8"), "/*@@MISC@@*/": fs.readFileSync("misc.js", "utf8"), "/*@@CIRCUITGRAMMAR@@*/": fs.readFileSync("circuitgrammar.js", "utf8"), "/*@@REDUCTION@@*/": fs.readFileSync("reduction.js", "utf8"), "/*@@MACHINE@@*/": fs.readFileSync("machine.js", "utf8"), "/*@@DOMINO@@*/": fs.readFileSync("pairview.js", "utf8"), "/*@@SEARCHTREE@@*/": fs.readFileSync("searchtree.js", "utf8") };
for (const [src, out] of [["design.src.html", "design.html"], ["redux-gui.src.html", "redux-gui.html"]]) {
  const html = fs.readFileSync(src, "utf8");
  let outHtml = html;
  for (const [ph, code] of Object.entries(mods)) {
    if (!outHtml.includes(ph)) throw new Error(ph + " missing in " + src);
    outHtml = outHtml.replace(ph, () => code);
  }
  fs.writeFileSync(out, outHtml);
  const js = [...fs.readFileSync(out, "utf8").matchAll(/<script>([\s\S]*?)<\/script>/g)].map(m => m[1]).join("\n;\n");
  new Function(js); // syntax check
  console.log("built", out);
}
