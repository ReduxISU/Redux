using System.Text.Json.Serialization;

namespace API.Interfaces;

/// <summary>
/// The algorithmic style a solver uses to produce (or attempt to produce) a solution.
/// Declared, not derived — nobody statically infers this from the solver's code, a
/// human reads <c>solve()</c>'s strategy and declares it here. Same "declared, not
/// derived" convention as <see cref="ComplexityClass"/>, <see cref="VisualizationType"/>,
/// and <see cref="ReductionCost"/>.
/// <para>
/// A tag set (#914): a solver declares every member that fits, joined with <c>|</c>, in any order. The
/// values are powers of two in declared order; the API lists them in that order as <c>solverTypes</c>, and the
/// deprecated single <c>solverType</c> field holds the first of them. See <see cref="Tags"/>.
/// </para>
/// </summary>
[Flags]
[JsonConverter(typeof(TagJsonConverter<SolverType>))]
public enum SolverType {
    /// <summary>No solver type has been declared for this solver yet.</summary>
    Unclassified = 0,
    /// <summary>Exhaustive enumeration of the candidate space with no pruning.</summary>
    BruteForce = 1 << 0,
    /// <summary>Makes an irrevocable, locally-optimal choice at each step.</summary>
    Greedy = 1 << 1,
    /// <summary>Fills a memo table via an optimal-substructure recurrence.</summary>
    DynamicProgramming = 1 << 2,
    /// <summary>Runs in polynomial time with a proven approximation-ratio guarantee on solution quality.</summary>
    Approximation = 1 << 3,
    /// <summary>No worst-case guarantee; practical-only, chosen for typical-case performance.</summary>
    Heuristic = 1 << 4,
    /// <summary>Divides the problem into subproblems, solves them recursively, and combines the results.</summary>
    DivideAndConquer = 1 << 5,
    /// <summary>Delegates to the external quantum-simulator service.</summary>
    Quantum = 1 << 6,
    /// <summary>Analyzes an input (like a string) and transitions between states according to a set of rules, producing an output (a sequence of states) when it reaches a final state. This is distinct from <see cref="BruteForce"/> because it does not enumerate the entire candidate space, and distinct from <see cref="Backtracking"/> because it does not prune the search space.</summary>
    StateTransition = 1 << 7,
    /// <summary>Searches a tree or graph of candidates in a breadth-first manner.</summary>
    BreadthFirstSearch = 1 << 8,
    /// <summary>Searches a tree or graph of candidates in a depth-first manner.</summary>
    DepthFirstSearch = 1 << 9,
    /// <summary>
    /// Exact search WITH pruning/bounding (branch-and-bound, dancing-links/Algorithm X,
    /// constraint backtracking). Distinct from <see cref="BruteForce"/> specifically
    /// because these prune the search space and brute force does not.
    /// </summary>
    Backtracking = 1 << 10,
    /// <summary>
    /// Builds a valid solution directly from a closed-form rule or formula, without
    /// searching the candidate space at all (e.g. the explicit mod-12 N-Queens placement).
    /// Distinct from <see cref="Greedy"/> because there are no per-step candidate choices to
    /// make, and from <see cref="Backtracking"/> because nothing is ever tried and undone.
    /// </summary>
    Constructive = 1 << 11,
    /// <summary>randomly determined, unpredictable, or involving a random variable and probability.</summary>
    Stochastic = 1 << 12,
    /// <summary>Exploits a specific, bounded parameter of the input to solve hard problems exactly in polynomial time.</summary>
    Parameterized = 1 << 13
}
