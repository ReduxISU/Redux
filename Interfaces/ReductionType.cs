using System.Text.Json.Serialization;

namespace API.Interfaces;

/// <summary>
/// The proof technique a reduction's <c>reduce()</c> uses to transform a FROM instance
/// into a TO instance. Declared, not derived — nobody statically infers this from the
/// generated code, a human reads <c>reduce()</c>'s construction and declares it here.
/// Same "declared, not derived" convention as <see cref="SolverType"/>,
/// <see cref="ComplexityClass"/>, <see cref="VisualizationType"/>, and
/// <see cref="ReductionCost"/>.
/// <para>
/// The vocabulary is the standard Garey &amp; Johnson taxonomy of NP-completeness proof
/// techniques. It classifies HOW the transformation is built, which is a different axis
/// from both <see cref="ReductionCost"/> (how much bigger the output instance is) and
/// <see cref="ReductionComplexityBucket"/> (how long building it takes) — a reduction
/// declares all three independently.
/// </para>
/// <para>
/// A tag set (#914): a reduction declares every member that fits, joined with <c>|</c>, in any order. The
/// values are powers of two in declared order; the API lists them in that order as <c>reductionTypes</c>, and the
/// deprecated single <c>reductionType</c> field holds the first of them. See <see cref="Tags"/>.
/// </para>
/// </summary>
[Flags]
[JsonConverter(typeof(TagJsonConverter<ReductionType>))]
public enum ReductionType {
    /// <summary>No reduction type has been declared for this reduction yet.</summary>
    Unclassified = 0,
    /// <summary>
    /// The FROM problem is recognized as a special case of the TO problem, so the
    /// transformation is essentially a re-labeling of the instance with little or no new
    /// structure built.
    /// </summary>
    Restriction = 1 << 0,
    /// <summary>
    /// Each basic unit of the FROM instance (a literal, a vertex, an edge) is replaced
    /// independently by a fixed piece of TO structure, with no coordination between the
    /// replacements.
    /// </summary>
    LocalReplacement = 1 << 1,
    /// <summary>
    /// Gadgets are designed to work together to enforce a global constraint — the
    /// pieces are not independent, and correctness depends on how they interact.
    /// </summary>
    ComponentDesign = 1 << 2,
}
