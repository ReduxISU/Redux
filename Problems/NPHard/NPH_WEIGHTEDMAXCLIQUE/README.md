# Weighted Maximum Clique — Phase 1

Given an undirected simple graph and nonnegative integer vertex weights, maximize
the sum of weights in a clique. The empty clique is allowed. Unit weights give
ordinary Maximum Clique. The model declares NPHard, Optimization, and GraphTheory.

Implementation plan: comments on https://github.com/ReduxISU/Redux/issues/860

## Instance contract

`((N,E),W)`, for example:

```text
(({a,b,c,d},{{a,b},{a,c},{b,c},{b,d}}),((a,4),(b,3),(c,5),(d,7)))
```

- `N` is a brace-delimited set of nonempty scalar vertex identifiers, compared
  ordinally and case-sensitively. Identifiers contain no whitespace or collection
  delimiters. Collection-valued identifiers are rejected.
- `E` is a brace-delimited set of brace-delimited pairs of distinct vertices in
  `N`. Directed pairs, self-loops, and unknown endpoints are rejected. Set
  repetitions are normalized; reversed copies of an edge are the same edge.
- `W` is a parenthesized list of ordered `(vertex,weight)` pairs. Its order has no
  significance. Exactly one assignment per vertex is required, including isolated
  vertices. Repeated assignments are rejected even if their weights match.
- Weights use decimal digits and fit a nonnegative `long` (`0..9223372036854775807`).
  The checked sum of **all** vertex weights must also fit `long`, even when the
  vertices cannot all belong to one clique. This makes subset totals and the
  planned candidate-weight upper bound safe. Each later arithmetic operation
  should still use checked arithmetic.
- Whitespace between tokens is supported. Empty graphs use `(({},{}),())`.
  Invalid instances throw Redux's `ProblemParseException` with a detailed message.

SPADE 0.1.2 parses the nested collections. An unconstrained root-list grammar
allows explicit validation of all tuple arities and member shapes. The familiar
graph grammar is documented in `InstanceGrammar` and exercised in tests, but its
`unorderedcross` constraint alone also accepts directed pairs. SPADE also ignores
trailing input and empty comma-separated entries. A small delimiter/entry guard
rejects those cases before parsing; it does not build a separate parse tree.

SPADE sets normalize repeated vertex names. Ordered weight lists preserve repeated
assignments, making duplicate detection possible. SPADE can retain reversed edges
because its collection hashing depends on enumeration order. The model explicitly
canonicalizes endpoints and deduplicates edges, then constructs `UtilCollectionGraph`
from those canonical edges. Vertex weights remain separate from graph edge weights.

## Certificate contract

`({v1,v2,...},totalWeight)`, for example `({a,b,c},12)`.

The first component is a brace-delimited set of distinct scalar vertex identifiers;
the second is a nonnegative decimal `long`. The empty certificate is `({},0)`.
Repeated selections must be rejected before set normalization can erase them.
Later certificate parsing must preserve those repetitions (for example, by parsing
the selected entries as an ordered collection after validating their set wrapper).

Future feasibility verification must check membership, uniqueness, pairwise
adjacency, and equality of the reported and checked recomputed total. A feasible
certificate may be suboptimal. The independent optimality verifier will additionally
compare with an exhaustively proven optimum; equal-weight optima are all acceptable.
No certificate parser, solver, or verifier is implemented in Phase 1.

## Registration and next phase

`WEIGHTEDMAXCLIQUE` is currently an internal model without `IProblem`/`IGraphProblem`.
The registry discovers all implementing classes automatically. Plain `IGraphProblem`
is supported, but its missing defaults fall back to dummy components, including a
solver returning an empty result. Registration is deferred until real components
exist; no registry, existing Clique, or frontend changes are needed in this phase.

Phase 2 can use the read-only `nodes`, canonical `edges`, identifier-keyed `weights`,
`totalVertexWeight`, original `instance`, and `graph`. Build adjacency from the
canonical edges and use an explicit deterministic traversal order for reproducible
solver output. Keep vertex identifiers intact in visualization. `UtilCollectionGraph`
is the existing mutable visualization adapter; algorithms should read the model's
read-only node/edge lists rather than mutate that adapter.

When adding the components, complete the standard discovery metadata and adopt the
existing generic interfaces or `[DefaultFor]` conventions. Add shared certificate
parsing/formatting, the 20-vertex exhaustive-search policy, and computation-limit
handling alongside the algorithms and verifiers that need them. None of those
policies should limit instance parsing itself.
