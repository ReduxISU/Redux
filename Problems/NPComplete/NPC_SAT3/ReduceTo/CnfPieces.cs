using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_SAT3.ReduceTo;

/// <summary>
/// The pieces of a CNF formula (SAT or 3SAT, clauses written as <c>["!x1", "x2"]</c>) as the reductions out of it name them
/// in gadgets and steps: the Grouped Graph picture's ids (clause <c>c{i}</c>, literal <c>c{i}-{j}</c>, variable
/// <c>v:{name}</c>) and the words of their captions.
/// </summary>
static class CnfPieces {
    /// <summary>The variable a literal mentions: <c>!x1</c> and <c>x1</c> both mention <c>x1</c>.</summary>
    public static string VariableOf(string literal) => literal.StartsWith('!') ? literal[1..] : literal;

    /// <summary>The variables, in the order the formula first mentions them.</summary>
    public static List<string> Variables(IEnumerable<IEnumerable<string>> clauses) =>
        clauses.SelectMany(c => c).Select(VariableOf).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>The ids of every occurrence of <paramref name="variable"/>, plain or negated, in formula order.</summary>
    public static List<string> OccurrenceIds(IReadOnlyList<List<string>> clauses, string variable) {
        var ids = new List<string>();
        for (int i = 0; i < clauses.Count; i++)
            for (int j = 0; j < clauses[i].Count; j++)
                if (VariableOf(clauses[i][j]) == variable) ids.Add(GroupedGraphFrames.LiteralId(i, j));
        return ids;
    }

    /// <summary>A clause and its literals: <c>c{i}</c>, <c>c{i}-0</c>, ...</summary>
    public static string[] ClauseIds(int clause, int literals) =>
        new[] { GroupedGraphFrames.ClauseId(clause) }.Concat(Enumerable.Range(0, literals).Select(j => GroupedGraphFrames.LiteralId(clause, j))).ToArray();

    /// <summary>
    /// The ids Redux_GUI reads for the same pieces (a gadget's legacy <c>reductionFromIds</c>/<c>reductionToIds</c>), which it
    /// matches against the list-format SAT picture (<see cref="API.Interfaces.JSON_Objects.API_SAT"/>). That picture gives
    /// clauses and literals the same ids (<c>c{i}</c>, <c>c{i}-{j}</c>), so those are kept; a variable <c>v:{name}</c>, which it
    /// does not draw, is every occurrence of it. Other ids are kept as they are. Each id once, in order.
    /// </summary>
    public static List<string> LegacyIds(IEnumerable<string> stableIds, IReadOnlyList<List<string>> clauses) {
        var result = new List<string>();
        foreach (string id in stableIds) {
            if (id.StartsWith("v:", StringComparison.Ordinal)) {
                for (int i = 0; i < clauses.Count; i++)
                    for (int j = 0; j < clauses[i].Count; j++)
                        if (GroupedGraphFrames.VariableId(VariableOf(clauses[i][j])) == id) result.Add(GroupedGraphFrames.LiteralId(i, j));
            } else {
                result.Add(id);
            }
        }
        return result.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// What the Grouped Graph picture of <paramref name="clauses"/> gains with clause <paramref name="clause"/>, when the clauses
    /// are built in order: the clause, its literals and the variables no earlier clause mentions (added to
    /// <paramref name="builtVariables"/>) as nodes; the edges between two of its literals, and between one of its literals and
    /// the opposite literal of an earlier clause (the picture's two edge rules, <see cref="GroupedGraphFrames.Payload"/>),
    /// earlier occurrence first. Replaying every clause gives the whole picture.
    /// </summary>
    public static (List<string> Nodes, List<BuildEdge> Edges) ClausePieces(IReadOnlyList<List<string>> clauses, int clause, HashSet<string> builtVariables) {
        var nodes = new List<string> { GroupedGraphFrames.ClauseId(clause) };
        var mine = clauses[clause];
        for (int j = 0; j < mine.Count; j++) nodes.Add(GroupedGraphFrames.LiteralId(clause, j));
        foreach (string variable in mine.Select(VariableOf).Distinct(StringComparer.Ordinal))
            if (builtVariables.Add(variable)) nodes.Add(GroupedGraphFrames.VariableId(variable));
        var edges = new List<BuildEdge>();
        for (int u = 0; u < clause; u++)
            for (int k = 0; k < clauses[u].Count; k++)
                for (int j = 0; j < mine.Count; j++)
                    if (VariableOf(clauses[u][k]) == VariableOf(mine[j]) && clauses[u][k].StartsWith('!') != mine[j].StartsWith('!'))
                        edges.Add(Edge(GroupedGraphFrames.LiteralId(u, k), GroupedGraphFrames.LiteralId(clause, j)));
        for (int j = 0; j < mine.Count; j++)
            for (int k = j + 1; k < mine.Count; k++)
                edges.Add(Edge(GroupedGraphFrames.LiteralId(clause, j), GroupedGraphFrames.LiteralId(clause, k)));
        return (nodes, edges);
    }

    private static BuildEdge Edge(string a, string b) => new(GroupedGraphFrames.EdgeId(a, b), a, b);

    /// <summary>"(x1 | !x2 | x3)".</summary>
    public static string ClauseText(IEnumerable<string> clause) => GroupedGraphFrames.ClauseText(clause);

    /// <summary>An assignment as certificate text, "(x1:True,x2:False)", in the given order.</summary>
    public static string Certificate(IEnumerable<string> order, IReadOnlyDictionary<string, bool> values) =>
        "(" + string.Join(",", order.Select(v => $"{v}:{Word(values[v])}")) + ")";

    /// <summary>
    /// An assignment as a SAT or 3SAT solver writes it, <c>(x1:True,x2:False)</c> (':' or '=', T and F accepted). Every name
    /// must be one of <paramref name="variables"/>; malformed text throws <see cref="FormatException"/>, which the endpoint
    /// reports as a 400.
    /// </summary>
    public static Dictionary<string, bool> ReadAssignment(string certificate, IEnumerable<string> variables, string formula) {
        var known = new HashSet<string>(variables, StringComparer.Ordinal);
        var values = new Dictionary<string, bool>(StringComparer.Ordinal);
        string text = certificate.Trim();
        if (text.StartsWith('(') && text.EndsWith(')')) text = text[1..^1];
        foreach (string raw in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            string[] parts = raw.Split(':', '=');
            bool? value = parts.Length != 2 ? null : parts[1].Trim().ToLowerInvariant() switch {
                "true" or "t" => true,
                "false" or "f" => false,
                _ => null,
            };
            if (value is not bool v) throw new FormatException($"'{raw}' is not a variable:True or variable:False pair");
            string name = parts[0].Trim();
            if (!known.Contains(name)) throw new FormatException($"'{name}' is not a variable of the {formula} formula");
            values[name] = v;
        }
        return values;
    }

    /// <summary>"True" or "False", as an <see cref="Assignment"/> and a certificate write it.</summary>
    public static string Word(bool value) => value ? "True" : "False";

    /// <summary>The assignment so far, as the source picture's partial answer.</summary>
    public static Assignment Snapshot(IReadOnlyDictionary<string, bool> values) =>
        new(values.Select(p => new KeyValuePair<string, string>(p.Key, Word(p.Value))));
}
