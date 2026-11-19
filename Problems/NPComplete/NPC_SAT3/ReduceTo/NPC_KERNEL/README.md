# 3SAT to directed kernel

This implements the SAT-to-kernel construction described by [Václav Chvátal, *On the computational complexity of finding a kernel* (1973; author's reconstruction)](https://users.encs.concordia.ca/~chvatal/kernel.html). Redux's 3SAT permits one to three literals per clause; the same construction works for all of them, including repeated or complementary literals.

For every variable x create vertices x and ¬x, with arcs in both directions. For each clause create three new vertices in a directed cycle. Add arcs from each of those three vertices to the vertex of each literal occurring in the clause. A kernel is an independent set K such that each vertex outside K has an arc into K.

**Correctness.** If the formula is satisfiable, choose the vertex corresponding to the true literal of each variable. The resulting set is independent; every unchosen variable vertex points to its chosen opposite, and each clause-cycle vertex points to a chosen true literal of its clause. Thus it is a kernel.

Conversely, in any kernel exactly one vertex of each variable pair must be chosen: independence forbids both, and if neither were chosen neither could point into the kernel, since their only outgoing arcs go to one another. If a clause has no literal vertex in the kernel, its three cycle vertices have no outgoing arcs into the kernel outside their own cycle. They would therefore have to form a kernel of a directed three-cycle, which is impossible: independence allows at most one, and one leaves a cycle vertex without an arc to it. Hence each clause contains a chosen literal. Assign each variable according to its chosen vertex; every clause is true.

**Complexity.** With n distinct variables, m clauses, and L literal occurrences, the construction creates 2n + 3m vertices and at most 2n + 3m + 3L arcs. It scans the formula and emits a graph of polynomial encoded size. Dictionary and set operations, vertex numbering, serialization, and graph parsing all take polynomial time in the encoded input and output lengths. Therefore this is a polynomial-time mapping reduction. Together with the polynomial-time kernel verifier, it establishes NP-completeness.
