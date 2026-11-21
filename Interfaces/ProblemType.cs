using System.Text.Json.Serialization;

namespace API.Interfaces;

/// <summary>
/// The subject-matter category a problem belongs to, following Garey &amp; Johnson's
/// classic NP-completeness taxonomy (extended with <see cref="Miscellaneous"/> as the
/// catch-all for problems that predate or fall outside that taxonomy, e.g. the quantum
/// query-complexity promise problems). Declared, not derived — same convention as
/// <see cref="ComplexityClass"/>.
/// <para>
/// A tag set (#914): a problem declares every member that fits, joined with <c>|</c>, in any order. The
/// values are powers of two in declared order; the API lists them in that order as <c>problemTypes</c>, and the
/// deprecated single <c>problemType</c> field holds the first of them. See <see cref="Tags"/>.
/// </para>
/// </summary>
[Flags]
[JsonConverter(typeof(TagJsonConverter<ProblemType>))]
public enum ProblemType {
    /// <summary>No problem type has been declared for this problem yet.</summary>
    Unclassified = 0,
    /// <summary>Structural/combinatorial graph problems: covering, coloring, matching, ordering.</summary>
    GraphTheory = 1 << 0,
    /// <summary>Designing or analyzing a network: spanning trees, cuts/connectivity, routing, flow.</summary>
    NetworkDesign = 1 << 1,
    /// <summary>Partitioning, covering, or matching a set or family of sets.</summary>
    SetsAndPartitions = 1 << 2,
    /// <summary>Storing, compressing, or comparing data.</summary>
    StorageAndRetrieval = 1 << 3,
    /// <summary>Ordering or scheduling tasks/jobs over time or resources.</summary>
    SequencingAndScheduling = 1 << 4,
    /// <summary>Linear/integer programming and related optimization-under-constraints problems.</summary>
    MathematicalProgramming = 1 << 5,
    /// <summary>Numeric/algebraic problems: factorization, modular arithmetic, and the like.</summary>
    AlgebraAndNumberTheory = 1 << 6,
    /// <summary>Recreational/combinatorial games and puzzles.</summary>
    GamesAndPuzzles = 1 << 7,
    /// <summary>Boolean-formula satisfiability and related logic problems.</summary>
    Logic = 1 << 8,
    /// <summary>Formal language/automata acceptance and recognition problems.</summary>
    AutomataAndLanguages = 1 << 9,
    /// <summary>Compiler/program-optimization problems (e.g. register allocation, code generation).</summary>
    ProgramOptimization = 1 << 10,
    /// <summary>Geometric problems over point/line/polygon inputs (e.g. convex hull, closest pair).</summary>
    ComputationalGeometry = 1 << 11,
    /// <summary>Doesn't fit any other category — includes problems that predate this taxonomy
    /// (e.g. the quantum query-complexity promise problems).</summary>
    Miscellaneous = 1 << 12,
}
