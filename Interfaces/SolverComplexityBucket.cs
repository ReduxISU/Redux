using System.Text.Json.Serialization;

namespace API.Interfaces;

/// <summary>
/// A coarse classification of a solver's WORST-CASE growth rate, not its typical-case
/// behavior. Declared, not derived — same convention as <see cref="SolverType"/>.
/// <para>
/// Several <see cref="SolverType.Backtracking"/>-tagged solvers are specifically
/// designed to run far faster than their worst-case bound on real instances (that is
/// the entire point of the pruning/bounding they do) — but this bucket intentionally
/// still reflects worst-case only, same as the existing free-text <c>complexity</c>
/// string already does for solvers like <c>KnapsackDP</c> ("O(n * W)" — pseudo-polynomial,
/// polynomial in the *value* of W but exponential in W's *bit-length*; the bucket still
/// says <see cref="Polynomial"/>, the string carries the nuance).
/// </para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SolverComplexityBucket>))]
public enum SolverComplexityBucket {
    /// <summary>No complexity bucket has been declared for this solver yet.</summary>
    Unclassified = 0,
    /// <summary>Worst-case running time is polynomial in instance size.</summary>
    Polynomial,
    /// <summary>Worst-case running time is exponential in instance size.</summary>
    Exponential,
    /// <summary>Worst-case running time is factorial in instance size.</summary>
    Factorial,
}
