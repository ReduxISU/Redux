using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace API.Interfaces;

/// <summary>
/// A coarse classification of how long a reduction's <c>reduce()</c> takes to RUN, in
/// worst-case Big-O of input instance size. Declared, not derived — same convention as
/// <see cref="SolverComplexityBucket"/>.
/// <para>
/// Deliberately distinct from <see cref="ReductionCost"/>, which classifies how much
/// bigger the produced instance is rather than how long producing it takes. The two
/// usually track each other (you cannot emit an O(n^2)-sized instance in less than
/// O(n^2) time) but they are not the same axis, and a reduction may spend more time
/// than its output size suggests — e.g. searching for a structure it then discards.
/// This is also distinct from the pre-existing free-text <c>complexity</c> string some
/// reduction classes carry: that is unstructured commentary with no closed vocabulary,
/// so it cannot be compared or ranked. This enum can.
/// </para>
/// <para>
/// Members are ordered fastest-to-slowest so that ranking "most efficient" is a plain
/// comparison: a smaller value is faster. <see cref="Unclassified"/> is 0 and must be
/// excluded from ranking rather than treated as fastest — see
/// <c>ReductionEfficiency.IsFasterThan</c>.
/// </para>
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
[JsonConverter(typeof(JsonStringEnumConverter<ReductionComplexityBucket>))]
public enum ReductionComplexityBucket
{
    /// <summary>No complexity bucket has been declared for this reduction yet.</summary>
    Unclassified = 0,
    /// <summary>Worst-case running time is O(1) in input instance size.</summary>
    Constant,
    /// <summary>Worst-case running time is O(log n) in input instance size.</summary>
    Logarithmic,
    /// <summary>Worst-case running time is O(n) in input instance size.</summary>
    Linear,
    /// <summary>Worst-case running time is O(n log n) in input instance size.</summary>
    Linearithmic,
    /// <summary>Worst-case running time is O(n^k) in input instance size, for some k &gt; 1.</summary>
    Polynomial,
    /// <summary>Worst-case running time is exponential in input instance size.</summary>
    Exponential,
}
