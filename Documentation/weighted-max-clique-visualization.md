# Weighted Maximum Clique visualization

`WeightedMaxCliqueVisualization` uses the existing `GraphD3` renderer and
`/ProblemProvider/visualize` endpoint. It is the problem's default visualization;
the endpoint's existing `solver` parameter selects any of the three solvers.

Graph nodes have one optional `weight` property. Its wire value is an invariant
decimal **string**, including `"0"` and `"9223372036854775807"`. This avoids precision
loss when JavaScript parses JSON numbers above its maximum safe integer. Clients
display the string directly as a secondary `w=...` label. Node `name` and `id`,
link endpoints, circle sizes, and edge weights retain their existing meanings.
Unweighted nodes omit `weight` entirely, preserving existing graph payloads.

Both initial and solved graphs include every vertex weight. A solved graph
highlights only a certificate accepted by solution/value verification, including
feasible suboptimal cliques and the empty clique `({},0)`. Highlighting establishes
feasibility, not optimality. Malformed or infeasible certificates draw the initial
graph without solution highlights, following the existing weighted SAT fallback.

This visualization provides initial and final pictures only. The step-by-step
picture is `WeightedMaxCliqueGraphVisualization` (#1058): the Graph picture type
(`format=frames`), opt-in with `includePictures=true`, so Redux_GUI keeps seeing
only this one. Its list format is exactly this visualization's. In its frames
payload each node's `weight` is an invariant integer string, as the Formula
picture's clause weights are, and every frame carries `selectedWeight`, the
chosen nodes' total, also as a string (#1034).
The brute force and branch and bound solvers record its steps. See the Graph
section of `Visualizations/PICTURE-TYPES.md`.
