using API.Interfaces.Steps;

namespace API.Interfaces.Graphs;

/// <summary>
/// The ids and words of a set problem's pieces (Set Cover, Exact Cover, Hitting Set): its elements, its sets and the
/// memberships between them. Every picture of a set problem names its pieces with these ids (the Bipartite Graph, the
/// Nested Set and the list format), and the set solvers and reductions use them in their steps, focus, gadgets and
/// captions, whichever picture draws them.
/// <para>
/// <b>Ids.</b> Element <c>x</c> is <c>e:{x}</c>, with the name written by <see cref="EscapeName"/> (a <c>\</c> before every
/// <c>\</c>, <c>~</c> and <c>:</c> in it). Set <c>i</c> (0-based, instance order) is <c>S{i+1}</c>: sets are named by
/// position, so two sets with the same elements are two pieces. A membership is <c>{elementId}~{setId}</c>
/// (<c>e:1~S2</c>). Set ids hold no name and element ids all start with <c>e:</c>, so no two pieces share an id, and the
/// escaping keeps a separator from ever coming bare from a name. An id is a label, never something to split.
/// </para>
/// </summary>
static class SetPieces {
    // ---- Ids ----

    /// <summary><c>e:{name}</c>, the name written with <see cref="EscapeName"/>.</summary>
    public static string ElementId(string name) => "e:" + EscapeName(name);

    /// <summary><c>S{index + 1}</c> for the set at 0-based <paramref name="index"/>.</summary>
    public static string SetId(int index) => $"S{index + 1}";

    /// <summary>The id of the membership between an element id and a set id: <c>{element}~{set}</c>.</summary>
    public static string EdgeId(string elementId, string setId) => $"{elementId}~{setId}";

    /// <summary>
    /// The membership of element <paramref name="element"/> (its name) in set <paramref name="setIndex"/> (0-based) as a
    /// construction step adds it: its id, the element's id and the set's id.
    /// </summary>
    public static BuildEdge BuildMembership(string element, int setIndex) =>
        new(EdgeId(ElementId(element), SetId(setIndex)), ElementId(element), SetId(setIndex));

    /// <summary>
    /// An element name as it is written inside an id: a backslash before every <c>\</c>, <c>~</c> and <c>:</c> (the
    /// separators of these ids). Ordinary names (<c>1</c>, <c>a</c>) are unchanged. The same rule as
    /// <see cref="GraphSubsetFrames.EscapeName"/>, over these ids' separators.
    /// </summary>
    public static string EscapeName(string name) =>
        name.IndexOfAny(['\\', '~', ':']) < 0 ? name : name.Replace(@"\", @"\\").Replace("~", @"\~").Replace(":", @"\:");

    // ---- Words, for solvers to call inside a recorder lambda so they cost nothing when recording is off ----

    /// <summary>"{1,2,3}" for captions.</summary>
    public static string Braces(IEnumerable<string> items) => "{" + string.Join(",", items) + "}";

    /// <summary>"S2 {2,4}": a set's name and its elements, as the instance lists them.</summary>
    public static string SetText(IReadOnlyList<IReadOnlyList<string>> sets, int index) => $"{SetId(index)} {Braces(sets[index])}";

    /// <summary>"4", "4 and 5", "3, 4 and 5".</summary>
    public static string List(IReadOnlyList<string> names) =>
        names.Count <= 1 ? string.Join("", names) : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];
}
