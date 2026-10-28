namespace API.Interfaces;

/// <summary>Shared pieces of backward mapping (see <see cref="IReversibleReduction{T, U, TPartial}"/>).</summary>
static class ReductionBack {
    /// <summary>The "no solution" answer, for every problem.</summary>
    public const string NoAnswer = "{}";

    /// <summary>
    /// True when <paramref name="certificate"/> is the text "{}". That is the "no solution" answer, but for a
    /// set-valued problem it is also the empty set, which can be a real answer (see
    /// <see cref="IReversibleReduction{T, U, TPartial}.TargetAcceptsEmpty"/>).
    /// </summary>
    public static bool IsNoAnswer(string certificate) => certificate.Trim() == NoAnswer;

    /// <summary>
    /// Whether <paramref name="problem"/>'s default verifier accepts <paramref name="certificate"/>. A verifier
    /// that throws (malformed or empty text) counts as rejecting it: this answers "is it valid", never "is it
    /// well-formed".
    /// </summary>
    public static bool Accepts<P>(P problem, string certificate) where P : IProblem {
        try {
            return problem.defaultVerifier is IVerifier<P> typed
                ? typed.verify(problem, certificate)
                : problem.defaultVerifier.verify(problem.instance, certificate);
        } catch (Exception) {
            return false;
        }
    }

    /// <summary>
    /// Reads a node-set certificate such as "{a,b,c}" and checks every name is a node of the problem
    /// it answers. "{}" reads as the empty set. Malformed text throws <see cref="FormatException"/>, which the endpoint reports as a
    /// 400 with that problem's certificate format.
    /// </summary>
    public static List<string> ParseNodeSet(string certificate, IReadOnlyCollection<string> validNodes) {
        string text = certificate.Trim();
        if (text.Length < 2 || text[0] != '{' || text[^1] != '}')
            throw new FormatException($"'{certificate}' is not a set in braces like {{a,b,c}}");
        var nodes = new List<string>();
        if (text[1..^1].Trim().Length == 0)
            return nodes;
        foreach (string raw in text[1..^1].Split(',')) {
            string node = raw.Trim();
            if (node.Length == 0)
                throw new FormatException($"'{certificate}' has an empty node name");
            if (!validNodes.Contains(node))
                throw new FormatException($"'{node}' is not a node of the problem");
            nodes.Add(node);
        }
        return nodes;
    }

    /// <summary>Writes a node set as certificate text, e.g. "{a,b,c}".</summary>
    public static string FormatNodeSet(IEnumerable<string> nodes) => "{" + string.Join(",", nodes) + "}";
}
