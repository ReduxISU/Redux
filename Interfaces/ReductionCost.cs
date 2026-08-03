using System.Text.Json.Serialization;

namespace API.Interfaces;

/// <summary>
/// How much a reduction's <c>reduce()</c> blows up OUTPUT instance size relative to
/// INPUT instance size, in Big-O terms (e.g. <see cref="Quadratic"/> = O(n^2)). Declared,
/// not derived — nobody statically measures this from the generated code, a human reads
/// the <c>reduce()</c> method's loop/data-structure structure and declares it here.
/// <para>
/// Not to be confused with the pre-existing, unrelated <c>public string? complexity</c>
/// field present on some reduction (and solver) classes: that field is free-text,
/// largely-unpopulated *runtime*-complexity commentary (how long the reduction takes to
/// compute), inherited from an earlier, looser convention. <see cref="ReductionCost"/> is
/// a declared, closed-vocabulary classification of *instance-size blow-up specifically*
/// (how much bigger the produced problem instance is, not how long producing it takes),
/// exposed on a differently-named member (<c>cost</c>, not <c>complexity</c>) so it can
/// coexist on classes that already carry the old field without colliding or being
/// confused with it.
/// </para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReductionCost>))]
public enum ReductionCost
{
    /// <summary>No cost has been declared for this reduction yet.</summary>
    Unclassified = 0,
    /// <summary>Output instance size is O(n) in input instance size.</summary>
    Linear,
    /// <summary>Output instance size is O(n^2) in input instance size.</summary>
    Quadratic,
    /// <summary>Output instance size is O(n^3) in input instance size.</summary>
    Cubic,
    /// <summary>Output instance size is O(n^k) in input instance size, for some k &gt; 3.</summary>
    HigherPolynomial,
}
