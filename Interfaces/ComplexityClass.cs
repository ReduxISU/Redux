using System.Text.Json.Serialization;

namespace API.Interfaces;

/// <summary>
/// The computational complexity class a problem is declared to belong to. Declared,
/// not derived: the <c>Problems/&lt;Folder&gt;/</c> on-disk layout is a filing
/// convention only, and <c>Problems/NPComplete/</c> misfiles at least twelve of its
/// entries (e.g. six actually-P problems, one NP-intermediate problem, and five
/// quantum query-complexity promise problems that aren't classical-hierarchy
/// citizens at all). This enum is the source of truth instead.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ComplexityClass>))]
public enum ComplexityClass {
    /// <summary>No complexity class has been declared for this problem yet.</summary>
    Unclassified = 0,
    /// <summary>Solvable in polynomial time.</summary>
    P,
    /// <summary>In NP, and every problem in NP polynomial-time reduces to it.</summary>
    NPComplete,
    /// <summary>At least as hard as every problem in NP, but not (known to be) in NP itself.</summary>
    NPHard,
    /// <summary>In NP, not known to be NP-complete, and not known to be in P (e.g. integer factorization).</summary>
    NPIntermediate,
    /// <summary>
    /// Query/oracle-complexity promise problem. Deutsch, Deutsch-Jozsa,
    /// Bernstein-Vazirani, Simon, and Grover/UNSTRUCTUREDSEARCH belong here rather
    /// than being tagged "BQP": they are defined over an oracle, and given an
    /// explicit input instead of an oracle, unstructured search is trivially in P.
    /// Incomparable with the classical P/NP hierarchy — reduction-validity checks
    /// against those classes must skip members of this class rather than compare
    /// against them.
    /// </summary>
    QuantumOracle,
}
