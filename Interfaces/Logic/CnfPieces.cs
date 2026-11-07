namespace API.Interfaces.Logic;

/// <summary>
/// The ids and words of a CNF formula's pieces (SAT, 3SAT): its clauses, the literals in them and its variables. Every
/// picture of a CNF formula names its pieces with these ids (the Grouped Graph, the Formula, the Factor Graph, the 0-1
/// IP formula and the list format), and the SAT solvers and the reductions to and from SAT use them in their steps, focus,
/// gadgets and captions, whichever picture draws them.
/// <para>
/// <b>Ids.</b> Clause <c>i</c> (0-based, in formula order) is <c>c{i}</c>. The <c>j</c>-th literal of clause <c>i</c>
/// (0-based) is <c>c{i}-{j}</c>, one piece per occurrence, so a literal written twice is two pieces. Variable <c>v</c> is
/// <c>v:{name}</c>, with the name written by <see cref="EscapeName"/>, so two variables never share an id. Clause and
/// literal ids hold no names. An id is a label, not something to split.
/// </para>
/// </summary>
static class CnfPieces {
    // ---- Ids ----

    public static string ClauseId(int clause) => $"c{clause}";
    public static string LiteralId(int clause, int position) => $"c{clause}-{position}";
    /// <summary><c>v:{name}</c>, the name written with <see cref="EscapeName"/>.</summary>
    public static string VariableId(string name) => "v:" + EscapeName(name);

    /// <summary>
    /// A variable name as it is written inside an id: a backslash before every <c>\</c>, <c>~</c>, <c>-</c> and <c>:</c> (the
    /// separators of these ids), so no two different names give the same id. Ordinary names (<c>x1</c>) are
    /// unchanged. The same rule as <see cref="API.Interfaces.Graphs.GraphSubsetFrames.EscapeName"/>, over these ids' separators.
    /// </summary>
    public static string EscapeName(string name) =>
        name.IndexOfAny(['\\', '~', '-', ':']) < 0
            ? name
            : name.Replace(@"\", @"\\").Replace("~", @"\~").Replace("-", @"\-").Replace(":", @"\:");

    // ---- The pieces a reduction names in its gadgets and build steps ----
    // For a formula written the way Redux's CNF problems keep it (clauses as ["!x1", "x2"]).

    /// <summary>A clause and its literals: <c>c{i}</c>, <c>c{i}-0</c>, ...</summary>
    public static string[] ClauseIds(int clause, int literals) =>
        new[] { ClauseId(clause) }.Concat(Enumerable.Range(0, literals).Select(j => LiteralId(clause, j))).ToArray();

    /// <summary>The ids of every occurrence of <paramref name="variable"/>, plain or negated, in formula order.</summary>
    public static List<string> OccurrenceIds(IReadOnlyList<List<string>> clauses, string variable) {
        var ids = new List<string>();
        for (int i = 0; i < clauses.Count; i++)
            for (int j = 0; j < clauses[i].Count; j++)
                if (CnfFormula.VariableOf(clauses[i][j]) == variable) ids.Add(LiteralId(i, j));
        return ids;
    }

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
                        if (VariableId(CnfFormula.VariableOf(clauses[i][j])) == id) result.Add(LiteralId(i, j));
            } else {
                result.Add(id);
            }
        }
        return result.Distinct(StringComparer.Ordinal).ToList();
    }

    // ---- Words, for solvers to call inside a recorder lambda so they cost nothing when recording is off ----

    /// <summary>"x1 = True, x2 = False", in the order given.</summary>
    public static string Assigned(IEnumerable<KeyValuePair<string, bool>> values) =>
        string.Join(", ", values.Select(p => $"{p.Key} = {p.Value}"));

    /// <summary>
    /// The clauses (0-based) by the names the picture gives them: "C2", "C2 and C3", "C1, C2 and C3", or "5 clauses" past
    /// three. Empty for none.
    /// </summary>
    public static string ClauseNames(IReadOnlyList<int> clauses) => clauses.Count switch {
        0 => "",
        1 => $"C{clauses[0] + 1}",
        <= 3 => $"{string.Join(", ", clauses.SkipLast(1).Select(c => $"C{c + 1}"))} and C{clauses[^1] + 1}",
        _ => $"{clauses.Count} clauses",
    };

    /// <summary>"(x1 | !x2 | x3)": a clause as the instance writes it.</summary>
    public static string ClauseText(IEnumerable<string> literals) => "(" + string.Join(" | ", literals) + ")";

    // ---- Formula wording, shared by the SAT solvers' why text ----
    // Phrases that more than one solver says, or that would need a branch (has or have, one or many) inside a why line. Each
    // gives a piece of a sentence, with no capital letter and no full stop, so the step line still reads as the sentence it
    // builds: $"Try {Assigned(a)}: clause {FailedClause(c, clause)}." Call them only inside a $"..." hole, or inside a
    // converter that runs only while recording; then a run with steps off never builds them.

    /// <summary>"C3 (x1 | !x2 | x3) has no true literal": one clause (0-based), written out, that fails.</summary>
    public static string FailedClause(int clause, IEnumerable<string> literals) =>
        $"C{clause + 1} {ClauseText(literals)} has no true literal";

    /// <summary>
    /// "C2 has no true literal", "C2 and C3 have no true literal": the clauses (0-based) that fail, by name. Give at least
    /// one clause; for a list that may be empty, use <see cref="ClauseCheck"/>.
    /// </summary>
    public static string NoTrueLiteral(IReadOnlyList<int> clauses) =>
        $"{ClauseNames(clauses)} {(clauses.Count == 1 ? "has" : "have")} no true literal";

    /// <summary>
    /// The result of checking a whole assignment, given the clauses (0-based) it leaves false: "every clause has a true
    /// literal" when there are none, otherwise <see cref="NoTrueLiteral"/> ("C2 has no true literal").
    /// </summary>
    public static string ClauseCheck(IReadOnlyList<int> falseClauses) =>
        falseClauses.Count == 0 ? "every clause has a true literal" : NoTrueLiteral(falseClauses);

    /// <summary>
    /// A short note for a search tree node, naming the variables an answer never had to set: null when there are none,
    /// "x1, x2 free" for one or two, and "5 free" for more (a node has little room).
    /// </summary>
    public static string? FreeVariables(IReadOnlyList<string> free) => free.Count switch {
        0 => null,
        <= 2 => $"{string.Join(", ", free)} free",
        _ => $"{free.Count} free",
    };

    /// <summary>
    /// What an answer does with the variables the search never had to set: " x1 is free (either value works), so the answer
    /// sets it to False.", or " x1, x2 and x3 are free ... sets them to False." It starts with a space, so it can follow a
    /// sentence directly, and it is empty when there are none.
    /// </summary>
    public static string FreeSetToFalse(IReadOnlyList<string> free) => free.Count switch {
        0 => "",
        1 => $" {free[0]} is free (either value works), so the answer sets it to False.",
        _ => $" {string.Join(", ", free.SkipLast(1))} and {free[^1]} are free (either value works), so the answer sets them to False.",
    };
}
