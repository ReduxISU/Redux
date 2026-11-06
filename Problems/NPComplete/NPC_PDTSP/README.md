# Pickup and Delivery Traveling Salesperson

This is the decision version of the single-vehicle, uncapacitated PDTSP with paired pickup and delivery locations. Each pickup must precede its paired delivery. There are no time windows, capacity limits, or FIFO/LIFO constraints. The model follows [Renaud, Boctor, and Ouenniche (2000)](https://doi.org/10.1016/S0305-0548(99)00066-0), with an explicit tour-cost bound for the decision problem.

Instances have the form `((N,E),R,S,K)`:

- `N`: a nonempty set of node labels.
- `E`: undirected weighted edges `({u,v},w)`, with nonnegative 32-bit integer weights. Missing edges cannot be traversed. Self-loops and parallel edges are rejected.
- `R`: ordered `(pickup,delivery)` pairs. Every nondepot node belongs to exactly one pair; the depot belongs to none.
- `S`: the depot, which must belong to `N`.
- `K`: a nonnegative 64-bit integer cost bound, including the return to the depot.

The small default instance is:

```text
(({S,P1,D1,P2,D2},{({S,P1},1),({P1,D1},1),({D1,P2},1),({P2,D2},1),({D2,S},1)}),{(P1,D1),(P2,D2)},S,5)
```

Its certificate is `{S,P1,D1,P2,D2,S}`, a tour of cost 5. Certificates are ordered lists despite using braces, following Redux's TSP convention. The depot must appear first and last; every other node must appear exactly once. For a depot-only graph with no requests, `{S,S}` is a zero-cost tour without requiring a self-loop.

All three solvers return a feasible tour within `K`, or `{}` after proving infeasibility. They search for feasibility and do not promise a minimum-cost tour. A timeout returns Redux's shared `SolveRun.TimedOutAnswer`.

| Solver class | Approach |
| --- | --- |
| `PDTSPBruteForce` | Enumerates all nondepot permutations; checks each complete tour with the verifier. |
| `PDTSPBacktracking` | Prunes prefixes violating precedence, edge availability, or the cost bound. |
| `PDTSPIntelligent` (default) | Exact branch-and-bound with cheaper-edge ordering, a minimum-outgoing-edge lower bound, and dominance by visited set and last vertex. |

The intelligent solver's bound adds the cheapest legal next edge to a minimum outgoing edge for each remaining vertex. Allowing these outgoing edges to lead to already visited vertices relaxes the completion constraints, so the bound cannot exclude a feasible solution. At the same visited set and last vertex, a more expensive arrival cannot improve any continuation and can be discarded. Visited sets use arbitrary-width integers.

Redux discovers `PDTSP`, the three solvers, and `PDTSPVerifier` automatically. The existing placeholder visualization applies until a PDTSP visualization is added.
