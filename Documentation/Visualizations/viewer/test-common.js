// Node test for adapters/common.js helpers that need no browser: Viewer.quantumServerDown on hand-made frames.
// Usage: node test-common.js
const fs = require("fs");
const Viewer = (0, eval)(fs.readFileSync(__dirname + "/adapters/common.js", "utf8") + ";Viewer");
let fails = 0;
const ok = (cond, msg) => { if (!cond) { fails++; console.log("  FAIL:", msg); } };
const err = JSON.stringify({ error: "One or more errors occurred. (Failed to POST to http://localhost:27100/deutsch-quantum: No connection could be made because the target machine actively refused it. (localhost:27100))" });

// What the backend sends when the quantum server is down (Deutsch, Oct 12).
ok(Viewer.quantumServerDown({ event: "Reject", caption: `The quantum server gave no answer: ${err}` }), "Reject frame with the server error in its caption");
ok(Viewer.quantumServerDown({ event: "Done", ok: false, caption: "No answer: the quantum server did not give one.", result: err }), "ok:false Done with the error as result");
ok(Viewer.quantumServerDown({ event: "Done", ok: false, caption: "", result: err }), "ok:false Done recognised by its result alone");
// Not a server failure.
ok(!Viewer.quantumServerDown({ event: "Done", ok: false, caption: "Item 0 is not marked: the measurement missed.", result: "0" }), "a wrong answer is not 'unavailable'");
ok(!Viewer.quantumServerDown({ event: "Done", ok: true, caption: "f is balanced.", result: "balanced" }), "an ok run");
ok(!Viewer.quantumServerDown({ event: "Done", ok: false, caption: "Stopped after 10 s." }), "a stopped run");
ok(!Viewer.quantumServerDown({ event: "Try", caption: "Send 15 to the quantum server, which runs Shor's algorithm." }), "a Try that mentions the server");
ok(!Viewer.quantumServerDown({ event: "Done", ok: false, result: JSON.stringify({ error: "Input must be a power of two." }) }), "an error unrelated to reaching the server");
ok(!Viewer.quantumServerDown(null), "no frame");

console.log(fails ? `${fails} FAILURES` : "ALL PASS");
process.exit(fails ? 1 : 0);
