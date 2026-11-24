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

This implementation provides initial and final pictures only. Native graph frames,
search-step recording, and extra solution-weight metadata are outside its scope.
