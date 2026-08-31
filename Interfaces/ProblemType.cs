using System.Text.Json.Serialization;

namespace API.Interfaces;

/// <summary>
/// The subject-matter category a problem belongs to, following Garey &amp; Johnson's
/// classic NP-completeness taxonomy (extended with <see cref="Miscellaneous"/> as the
/// catch-all for problems that predate or fall outside that taxonomy, e.g. the quantum
/// query-complexity promise problems). Declared, not derived — same convention as
/// <see cref="ComplexityClass"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProblemType>))]
public enum ProblemType {
    /// <summary>No problem type has been declared for this problem yet.</summary>
    Unclassified = 0,
    /// <summary>Structural/combinatorial graph problems: covering, coloring, matching, ordering.</summary>
    GraphTheory,
    /// <summary>Designing or analyzing a network: spanning trees, cuts/connectivity, routing, flow.</summary>
    NetworkDesign,
    /// <summary>Partitioning, covering, or matching a set or family of sets.</summary>
    SetsAndPartitions,
    /// <summary>Storing, compressing, or comparing data.</summary>
    StorageAndRetrieval,
    /// <summary>Ordering or scheduling tasks/jobs over time or resources.</summary>
    SequencingAndScheduling,
    /// <summary>Linear/integer programming and related optimization-under-constraints problems.</summary>
    MathematicalProgramming,
    /// <summary>Numeric/algebraic problems: factorization, modular arithmetic, and the like.</summary>
    AlgebraAndNumberTheory,
    /// <summary>Recreational/combinatorial games and puzzles.</summary>
    GamesAndPuzzles,
    /// <summary>Boolean-formula satisfiability and related logic problems.</summary>
    Logic,
    /// <summary>Formal language/automata acceptance and recognition problems.</summary>
    AutomataAndLanguages,
    /// <summary>Compiler/program-optimization problems (e.g. register allocation, code generation).</summary>
    ProgramOptimization,
    /// <summary>Doesn't fit any other category — includes problems that predate this taxonomy
    /// (e.g. the quantum query-complexity promise problems).</summary>
    Miscellaneous,
}
