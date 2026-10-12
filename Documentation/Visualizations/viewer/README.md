# Frames viewer (local debug tool)

A page that calls a running Redux backend with `format=frames` and draws the real frames with the mockup renderers in `../mockups/`. It covers every picture type and the Reduction View. It is a local demo and debugging tool, not part of Redux_GUI or Redux_Frontend.

## Run it

1. **Start the backend** (the standalone build of `local/viz-integration`). In Git Bash:

   ```bash
   cd /c/Users/mtros/Programming/Redux_local_build
   ASPNETCORE_URLS=http://localhost:27000 ./API.exe
   ```

   In PowerShell: `cd C:\Users\mtros\Programming\Redux_local_build; $env:ASPNETCORE_URLS="http://localhost:27000"; .\API.exe`

2. **Start the viewer server** (Node, no dependencies), from the repo root:

   ```bash
   node Documentation/Visualizations/viewer/serve.mjs
   ```

3. **Open** http://localhost:27080/viewer/

Opening `index.html` straight from disk (`file://`) also works: the scripts are classic scripts, and the backend's CORS allows any origin. The server is there for a stable URL.

### A stopped run

The backend has no per-request time limit. It reads `Visualization:SolveTimeLimitSeconds` (default 10) at startup. For a stopped run, start a second backend with a tiny limit and point the page's Backend field at it (or add `&backend=http://localhost:27001` to the URL):

```bash
cd /c/Users/mtros/Programming/Redux_local_build
ASPNETCORE_URLS=http://localhost:27001 Visualization__SolveTimeLimitSeconds=0.02 ./API.exe
```

Use a solver with a long run, for example Graph Coloring with `GraphColoringBruteForce`. A warm backend finishes small default instances inside 0.02 s.

## The page

- **Backend** field and **Check**: health check (`GET /Navigation/ALL_ProblemsRefactor`).
- **Picture** mode: problem, visualization and solver pickers, and the instance (prefilled with the default). Each visualization is labelled with the picture type it serves under `format=frames`, read from `pictureType` in its `/info` (null means legacy). A backend without that field is probed instead, with one default-instance run per visualization. Legacy ones say "legacy, list wrapped". **Run** posts `visualize?visualization=…&format=frames[&solver=…]`.
- **Search tree alongside**: shown when the problem has a Search Tree visualization. It fetches that visualization with the same solver and steps it in sync, by frame index. If a run's steps carry `tree` patches (`SolverStep.Tree`), the tree is built from them instead.
- **Reduction** mode: pick a reduction from `/Navigation/Reductions`, edit the source instance, and optionally pick the target solver. **Run** posts `reductionView?reduction=…[&solver=…]`. The two panes are drawn by each problem's own adapter. The phase buttons (Build, Solve, Map back) jump to each phase. The view has one Done, its last frame, so the `ok` badge shows only at the end of Map back; the Build phase ends on a plain Accept and the Solve phase on an Accept or a Reject. A note flags any other Done or `ok`. The gadget list outlines the gadgets built in the current step. Hover a gadget row, or any piece in either pane, to put an accent ring on the linked pieces on both sides and turn everything else grey.
- **Player**: slider, ◀ ▶, Play/Pause with speed, and keyboard (← → Home End, Space). Under the stepper are the caption, the event, phase, `ok` and focus badges, and a "stopped" badge on a time-limited Done. A Done before the last frame gets a warning badge.
- **States in this run**: a legend of the state names that actually occur in the frames.
- **Adapter notes**: every mismatch the adapters found between the backend JSON and what the mockup renderer expects.
- **Raw JSON**: the current frame, and the payload (or the Reduction View header).
- **Theme**: Auto, Light or Dark, using the same `--av-*` tokens as the mockups (`tokens.css` is copied from `../mockups/design.src.html`).

URL parameters reproduce a view: `mode`, `problem`, `vis`, `solver`, `instance`, `reduction`, `along=1`, `i` (frame), `theme`, `backend`, `run=1`. For example:

- http://localhost:27080/viewer/?problem=CLIQUE&vis=CliqueDefaultVisualization&run=1
- http://localhost:27080/viewer/?mode=reduction&reduction=KarpReduceGRAPHCOLORING&run=1

## Files

| File | What |
| --- | --- |
| `index.html`, `viewer.css`, `tokens.css` | The page. It loads the renderers from `../mockups/*.js` by relative path. |
| `app.js` | Pickers, fetching, player, search tree alongside, Reduction View panes, phases, gadget linking. |
| `adapters/common.js` | The adapter contract (read this first) and helpers. |
| `adapters/<type>.js` | One adapter per picture type: backend frame JSON → the mockup renderer's input. |
| `serve.mjs` | Static server on port 27080 (serves `Documentation/Visualizations/`). |
| `cases.mjs` | The runs the tests drive: every picture type (with variants), every reduction with a Reduction View, and stopped runs. |
| `smoke.mjs` | Playwright smoke test. It fails on console errors, failed runs or empty panes. |
| `contrast.mjs` | The mockups' contrast audit (`../mockups/contrast/audit-lib.js`) run over the viewer in light and dark mode. |

A few renderers got small, additive "viewer hooks" (marked `Viewer hook (local/viz-viewer)` in `../mockups/*.js`). These let them take a model and frames from outside instead of parsing an instance and running a JavaScript solver.

## Tests

Both need Playwright. Redux_GUI has a copy in its `node_modules`:

```bash
cd Documentation/Visualizations/viewer
export PLAYWRIGHT=/c/Users/mtros/Programming/Redux_GUI/node_modules/playwright
node smoke.mjs                          # every case, light theme; exits 1 on any failure
node smoke.mjs --themes light,dark --shots /tmp/viewer-shots   # also saves a mid and a last screenshot per case
node smoke.mjs --only Graph,Red-        # a subset (case name prefix or picture type)
NOTES=1 node smoke.mjs                  # also print the adapter notes per case
node contrast.mjs                       # contrast audit, light and dark
```

Stopped-run cases use `--stop-backend` (default http://localhost:27001). They are skipped when that backend is not running.

A case can carry an `expect` (see `checkExpect` in `smoke.mjs`), which steps through every frame and fails the case if the picture does not show what the backend sent. The `QuantumRun-*` cases use it: they run the quantum solvers (`DeutschQuantumSolver`, `DeutschJozsaQuantumSolver`, `BernsteinVaziraniQuantumSolver` on the Default and D3 visualizations, `UnstructuredGroverSolver`, and `ShorsQuantumSolver` on 15), check that `applied` rises to the last column with the drawn circuit and bars following it, and check the final verdict and the measured qubits' Solution / Rejected drawing.

- These cases need the quantum server the backend calls (http://localhost:27100). Without it the runs end with a Reject and an ok:false Done whose result is the call's error JSON; the viewer reads that as "quantum server unavailable", not as a wrong answer, and the `ok: true` expectations fail.
- Grover's server answer is wrong about 3 runs in 4 (a known server bug: the iteration count), so `QuantumRun-grover` accepts ok:true or ok:false and checks only the animation and that the measured qubits are drawn with the verdict the backend sent. Before that fix it returned HTTP 500 when the quantum server was down (#838).
- Shor's solver applies no columns (`payload.simulated` is false), so its bars are the browser's own simulation; the chart says so.

Node tests (no browser): `node test-common.js` here, and `node ../mockups/test-*.js` for the renderers. `../mockups/dom-shim.js` is a tiny DOM that lets the `test-*-draw.js` tests draw a renderer and check what it drew.
