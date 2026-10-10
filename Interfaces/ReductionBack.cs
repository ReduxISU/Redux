namespace API.Interfaces;

/// <summary>Shared pieces of backward mapping (see <see cref="IReversibleReduction{T, U, TPartial}"/>).</summary>
static class ReductionBack {
    /// <summary>The "no solution" answer, for every problem.</summary>
    public const string NoAnswer = "{}";

    /// <summary>True when <paramref name="certificate"/> is the "no solution" answer.</summary>
    public static bool IsNoAnswer(string certificate) => certificate.Trim() == NoAnswer;

    /// <summary>
    /// Reads a node-set certificate such as "{a,b,c}" and checks every name is a node of the problem
    /// it answers. Malformed text throws <see cref="FormatException"/>, which the endpoint reports as a
    /// 400 with that problem's certificate format.
    /// </summary>
    public static List<string> ParseNodeSet(string certificate, IReadOnlyCollection<string> validNodes) {
        string text = certificate.Trim();
        if (text.Length < 2 || text[0] != '{' || text[^1] != '}')
            throw new FormatException($"'{certificate}' is not a set in braces like {{a,b,c}}");
        var nodes = new List<string>();
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
