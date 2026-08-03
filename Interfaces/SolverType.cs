using System.Text.Json.Serialization;

namespace API.Interfaces;

/// <summary>
/// The algorithmic style a solver uses to produce (or attempt to produce) a solution.
/// Declared, not derived — nobody statically infers this from the solver's code, a
/// human reads <c>solve()</c>'s strategy and declares it here. Same "declared, not
/// derived" convention as <see cref="ComplexityClass"/>, <see cref="VisualizationType"/>,
/// and <see cref="ReductionCost"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SolverType>))]
public enum SolverType
{
    /// <summary>No solver type has been declared for this solver yet.</summary>
    Unclassified = 0,
    /// <summary>Exhaustive enumeration of the candidate space with no pruning.</summary>
    BruteForce,
    /// <summary>Makes an irrevocable, locally-optimal choice at each step.</summary>
    Greedy,
    /// <summary>Fills a memo table via an optimal-substructure recurrence.</summary>
    DynamicProgramming,
    /// <summary>Runs in polynomial time with a proven approximation-ratio guarantee on solution quality.</summary>
    Approximation,
    /// <summary>No worst-case guarantee; practical-only, chosen for typical-case performance.</summary>
    Heuristic,
    /// <summary>Divides the problem into subproblems, solves them recursively, and combines the results.</summary>
    DivideAndConquer,
    /// <summary>Delegates to the external quantum-simulator service.</summary>
    Quantum,
    /// <summary>Analyzes an input (like a string) and transitions between states according to a set of rules, producing an output (a sequence of states) when it reaches a final state. This is distinct from <see cref="BruteForce"/> because it does not enumerate the entire candidate space, and distinct from <see cref="Backtracking"/> because it does not prune the search space.</summary>
    StateTransition,
    /// <summary>Searches a tree or graph of candidates in a breadth-first manner.</summary>
    BreadthFirstSearch,
    /// <summary>Searches a tree or graph of candidates in a depth-first manner.</summary>
    DepthFirstSearch,
    /// <summary>
    /// Exact search WITH pruning/bounding (branch-and-bound, dancing-links/Algorithm X,
    /// constraint backtracking). Distinct from <see cref="BruteForce"/> specifically
    /// because these prune the search space and brute force does not.
    /// </summary>
    Backtracking,
}
