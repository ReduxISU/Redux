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
    /// The ids Redux_GUI reads for the same pieces (a gadget's legacy <c>reductionFromIds</c>/<c>reductionToIds</c>): clause
    /// <c>c{i}</c> is <c>i</c>, literal <c>c{i}-{j}</c> is <c>i-j</c>, and a variable <c>v:{name}</c>, which the GUI does not
    /// draw, is every occurrence of it. Other ids are kept as they are. Each id once, in order.
    /// </summary>
    public static List<string> LegacyIds(IEnumerable<string> stableIds, IReadOnlyList<List<string>> clauses) {
        var result = new List<string>();
        foreach (string id in stableIds) {
            if (id.StartsWith("v:", StringComparison.Ordinal)) {
                for (int i = 0; i < clauses.Count; i++)
                    for (int j = 0; j < clauses[i].Count; j++)
                        if (GroupedGraphFrames.VariableId(VariableOf(clauses[i][j])) == id) result.Add($"{i}-{j}");
            } else if (System.Text.RegularExpressions.Regex.IsMatch(id, @"^c\d+(-\d+)?$")) {
                result.Add(id[1..]);
            } else {
                result.Add(id);
            }
        }
        return result.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>"(x1 | !x2 | x3)".</summary>
    public static string ClauseText(IEnumerable<string> clause) => GroupedGraphFrames.ClauseText(clause);

    /// <summary>An assignment as certificate text, "(x1:True,x2:False)", in the given order.</summary>
    public static string Certificate(IEnumerable<string> order, IReadOnlyDictionary<string, bool> values) =>
        "(" + string.Join(",", order.Select(v => $"{v}:{Word(values[v])}")) + ")";

    /// <summary>"True" or "False", as an <see cref="Assignment"/> and a certificate write it.</summary>
    public static string Word(bool value) => value ? "True" : "False";

    /// <summary>The assignment so far, as the source picture's partial answer.</summary>
    public static Assignment Snapshot(IReadOnlyDictionary<string, bool> values) =>
        new(values.Select(p => new KeyValuePair<string, string>(p.Key, Word(p.Value))));
}
