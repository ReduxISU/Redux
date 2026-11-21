namespace API.Interfaces;

/// <summary>
/// One size measure of a parsed instance, for the Big-O precheck (#843): a part of the instance, named as in the
/// problem's SPADE instance grammar, and how big it is. A set part (the nodes N, the edges E of a graph) is measured by
/// its number of elements and is written <c>|N|</c> in a solver's <see cref="ISolver.timeComplexity"/>; a number part
/// (a bound K) is measured by its value and is written <c>K</c>.
/// <para>
/// Each problem reports its measures from the instance it already parses (<see cref="IProblem.SizeMeasures"/>). When
/// the instance parts come from SPADE (#851), the measures will be read from the part list instead, and only where
/// they come from changes: the precheck (<c>ComputePrecheck</c>) reads the same records.
/// </para>
/// </summary>
/// <param name="Part">The part's name in the instance grammar, e.g. "N", "E" or "K".</param>
/// <param name="Value">The number of elements of a set part, or the value of a number part.</param>
/// <param name="IsCount">True for a set part measured by its number of elements.</param>
public sealed record SizeMeasure(string Part, double Value, bool IsCount) {
    /// <summary>The measure of a set part: its number of elements, written <c>|Part|</c> in a Big-O.</summary>
    public static SizeMeasure Count(string part, int count) => new(part, count, IsCount: true);

    /// <summary>The measure of a number part: its value, written <c>Part</c> in a Big-O.</summary>
    public static SizeMeasure Number(string part, double value) => new(part, value, IsCount: false);

    /// <summary>The measure as a Big-O writes it: <c>|N|</c> for a set part, <c>K</c> for a number part.</summary>
    public string Label => IsCount ? $"|{Part}|" : Part;
}
