using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using API.Interfaces.Steps;

namespace API.Interfaces.Graphs;

/// <summary>
/// What a CNF problem hands the Grouped Graph picture: its clauses and the names of its variables. No layout, no
/// colors, no edges; the base derives those.
/// </summary>
/// <param name="Clauses">One array per clause, in formula order. A literal is a variable's 1-based index into
/// <paramref name="Variables"/>, negative when the literal is negated (<c>[[-1, 2, 3], [-1, -2, 4]]</c>).</param>
/// <param name="Variables">The display name of each variable (<c>x1</c>, <c>x2</c>, ...), in index order.</param>
sealed record GroupedGraphSpec(IReadOnlyList<int[]> Clauses, IReadOnlyList<string> Variables) {
    /// <summary>
    /// The spec for clauses written the way Redux's CNF problems keep them (<c>["!x1", "x2", "x3"]</c>: a leading
    /// <c>!</c> negates). Variables are numbered in natural order of name (<c>x2</c> before <c>x10</c>), not in the
    /// order they first appear, so <c>x1..xn</c> read as <c>1..n</c> whatever the formula's order.
    /// </summary>
    public static GroupedGraphSpec FromLiterals(IEnumerable<IEnumerable<string>> clauses) {
        var written = clauses.Select(c => c.Where(l => l.Length > 0).Select(l => (Negated: l[0] == '!', Name: l.TrimStart('!'))).ToList()).ToList();
        var names = written.SelectMany(c => c).Select(l => l.Name).Distinct(StringComparer.Ordinal).Order(GroupedGraphFrames.NaturalOrder).ToList();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < names.Count; i++) index[names[i]] = i + 1;
        return new GroupedGraphSpec(written.Select(c => c.Select(l => l.Negated ? -index[l.Name] : index[l.Name]).ToArray()).ToList(), names);
    }
}

// --- JSON shapes (explicit names so the contract does not depend on serializer naming options) ---

sealed record GroupedGraphGroupDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("members")] IReadOnlyList<string> Members);

sealed record GroupedGraphNodeDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("variable")] string Variable,
    [property: JsonPropertyName("positive")] bool Positive,
    [property: JsonPropertyName("group")] string Group);

sealed record GroupedGraphVariableDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label);

/// <summary>The static part of a Grouped Graph picture, sent once per response.</summary>
sealed record GroupedGraphPayload(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("layout")] string Layout,
    [property: JsonPropertyName("shape")] string Shape,
    [property: JsonPropertyName("rule")] string Rule,
    [property: JsonPropertyName("groups")] IReadOnlyList<GroupedGraphGroupDto> Groups,
    [property: JsonPropertyName("nodes")] IReadOnlyList<GroupedGraphNodeDto> Nodes,
    [property: JsonPropertyName("variables")] IReadOnlyList<GroupedGraphVariableDto> Variables,
    [property: JsonPropertyName("edges")] IReadOnlyList<GraphEdgeDto> Edges,
    [property: JsonPropertyName("clauseEdges")] IReadOnlyList<GraphEdgeDto> ClauseEdges);

/// <summary>
/// One picture of the clause graph. <c>nodes</c> (literal occurrences) and <c>groups</c> (clauses) list only elements whose
/// state is not <see cref="PictureState.Background"/>; an id that is absent is Background. <c>assignment</c> says what
/// is assigned so far, keyed by variable id; a variable that is absent is unassigned. A literal that is false under the
/// assignment is <see cref="PictureState.False"/> in <c>nodes</c>; a literal of an unassigned variable is Background.
/// </summary>
sealed record GroupedGraphFrame(
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("nodes")] IReadOnlyDictionary<string, StateRef> Nodes,
    [property: JsonPropertyName("groups")] IReadOnlyDictionary<string, StateRef> Groups,
    [property: JsonPropertyName("assignment")] IReadOnlyDictionary<string, bool> Assignment) {
    /// <summary>The solver step this frame was drawn from (see <see cref="FramesResponse"/>, #895); null for an added frame. Always written.</summary>
    [JsonPropertyName("step")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? Step { get; init; }

    [JsonPropertyName("ok")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ok { get; init; }
}

/// <summary>
/// Derives the Grouped Graph picture (the clause graph of SAT and 3SAT) from (clauses, partial <see cref="Assignment"/>,
/// step event, focus). Nothing here knows about a particular problem or solver.
/// <para>
/// <b>Ids</b> (a contract: the Reduction View matches gadgets on them). Clause <c>i</c> (0-based, in formula order) is
/// <c>c{i}</c>. The <c>j</c>-th literal of clause <c>i</c> (0-based) is <c>c{i}-{j}</c>, one node per occurrence, so a
/// literal written twice is two nodes. Variable <c>v</c> is <c>v:{name}</c>, with the name written by
/// <see cref="EscapeName"/> (a <c>\</c> before every <c>\</c>, <c>~</c>, <c>-</c> and <c>:</c> in it), so two variables never
/// share an id and a separator inside a variable id never comes bare from a name. An edge id joins its two literal ids with
/// <c>~</c> (<c>c0-1~c2-0</c>); the separator is not <c>-</c>, because literal ids contain it. Clause and literal ids hold no
/// names, so they never contain <c>~</c>. The earlier occurrence (by clause, then position, compared as numbers, so
/// <c>c2</c> comes before <c>c10</c>) is written first, so an edge has exactly one id. An id is a label, not something to
/// split: every payload edge gives its two ends as <c>a</c> and <c>b</c>, and every node gives its <c>variable</c> id.
/// </para>
/// <para>
/// <b>Edges.</b> <c>edges</c> are the complement pairs: a literal and the negation of the same variable in a
/// <i>different</i> clause. <c>clauseEdges</c> join every two literal occurrences inside one clause. Together they are exactly
/// the graph of the 3SAT to Independent Set reduction. The frontend's Edges switch chooses between <c>edges</c> alone and
/// both lists.
/// </para>
/// <para>
/// <b>States.</b> A true literal is <c>Solution</c> in the confirmed answer (the Done, or an Accept that ends the run),
/// <c>Rejected</c> in a Done with ok = false (the run found nothing), <c>Committed</c> in a step that keeps the assignment without confirming it (an Accept mid-run, a Backtrack; #886), and
/// <c>ElementHighlight</c> in a step that only tries a whole assignment (Try, Reject of a run that is not
/// <see cref="SolveRun.Incremental"/>: see <see cref="GraphSubsetFrames.Picked"/>); either way its clause is <c>Covered</c>.
/// In an incremental run (the 3SAT backtracking) the assignment is the variables decided so far, so their true literals
/// stay <c>Committed</c> in every step until the answer, and only the variable being tried (the focus) is highlighted; when that value fails,
/// the clause it leaves with no true literal is what is <c>Rejected</c>. A literal that is false under the assignment is <c>False</c>; a literal whose variable
/// is unassigned stays <c>Background</c> (nothing has been decided about it). A clause whose literals are all false is
/// <c>Rejected</c>, and so are its literals. The variable being tried (a <c>v:</c> id in the step's focus) is
/// <c>ElementHighlight</c> on every occurrence that is not false: a false occurrence stays <c>False</c>, so the tried value's
/// false literals never look like its true ones (a literal id in the focus follows the same rule). A clause in the focus is
/// <c>ElementHighlight</c>, or <c>Rejected</c> in a <see cref="StepEvent.Reject"/> step (the clause that just failed). When
/// several states apply to one element the order is
/// <c>Rejected &gt; ElementHighlight &gt; Solution &gt; Committed &gt; Covered &gt; Blocked &gt; False &gt; Background</c>
/// (<see cref="GraphSubsetFrames.Rank"/>), except that the focus highlight never covers <c>False</c> on a literal. Whether a clause is falsified is judged from the assignment alone, never from
/// the event, so no event is treated as "still in progress".
/// </para>
/// </summary>
static class GroupedGraphFrames {
    public const string KindName = "groupedGraph";
    public const string LayoutName = "ring";
    public const string ShapeName = "assignment";
    public const string RuleName = "satisfiability";

    // ---- Ids ----

    public static string ClauseId(int clause) => $"c{clause}";
    public static string LiteralId(int clause, int position) => $"c{clause}-{position}";
    /// <summary><c>v:{name}</c>, the name written with <see cref="EscapeName"/>.</summary>
    public static string VariableId(string name) => "v:" + EscapeName(name);

    /// <summary>
    /// A variable name as it is written inside an id: a backslash before every <c>\</c>, <c>~</c>, <c>-</c> and <c>:</c> (the
    /// separators of this picture's ids), so no two different names give the same id. Ordinary names (<c>x1</c>) are
    /// unchanged. The same rule as <see cref="GraphSubsetFrames.EscapeName"/>, over this picture's separators.
    /// </summary>
    public static string EscapeName(string name) =>
        name.IndexOfAny(['\\', '~', '-', ':']) < 0
            ? name
            : name.Replace(@"\", @"\\").Replace("~", @"\~").Replace("-", @"\-").Replace(":", @"\:");

    /// <summary>The id of an edge between two literals: <c>"a~b"</c>, written in the order given (callers give the earlier first).</summary>
    public static string EdgeId(string a, string b) => $"{a}~{b}";

    // ---- The payload ----

    /// <summary>"x2", or "¬x2" for a negated literal.</summary>
    private static string LabelOf(bool positive, string name) => positive ? name : "¬" + name;

    public static GroupedGraphPayload Payload(GroupedGraphSpec spec) {
        var groups = new List<GroupedGraphGroupDto>();
        var nodes = new List<GroupedGraphNodeDto>();
        var occurrences = new List<(string Id, int Clause, int Variable, bool Positive)>();
        for (int i = 0; i < spec.Clauses.Count; i++) {
            var members = new List<string>();
            for (int j = 0; j < spec.Clauses[i].Length; j++) {
                int literal = spec.Clauses[i][j];
                int variable = Math.Abs(literal);
                string name = spec.Variables[variable - 1];
                string id = LiteralId(i, j);
                members.Add(id);
                nodes.Add(new GroupedGraphNodeDto(id, LabelOf(literal > 0, name), VariableId(name), literal > 0, ClauseId(i)));
                occurrences.Add((id, i, variable, literal > 0));
            }
            groups.Add(new GroupedGraphGroupDto(ClauseId(i), $"C{i + 1}", members));
        }

        // occurrences is in (clause, position) order, so a pair (a before b) is already written earlier-first.
        var complement = new List<GraphEdgeDto>();
        var within = new List<GraphEdgeDto>();
        for (int a = 0; a < occurrences.Count; a++)
            for (int b = a + 1; b < occurrences.Count; b++) {
                var x = occurrences[a];
                var y = occurrences[b];
                if (x.Clause == y.Clause) within.Add(new GraphEdgeDto(EdgeId(x.Id, y.Id), x.Id, y.Id));
                else if (x.Variable == y.Variable && x.Positive != y.Positive) complement.Add(new GraphEdgeDto(EdgeId(x.Id, y.Id), x.Id, y.Id));
            }

        var variables = spec.Variables.Select(n => new GroupedGraphVariableDto(VariableId(n), n)).ToList();
        return new GroupedGraphPayload(KindName, LayoutName, ShapeName, RuleName, groups, nodes, variables, complement, within);
    }

    // ---- Frames ----

    private static bool? ParseValue(string value) => value.Trim().ToLowerInvariant() switch {
        "true" or "t" => true,
        "false" or "f" => false,
        _ => null,
    };

    /// <summary>
    /// The frame for an assignment at one step. <c>incremental</c> says whether the step comes from an incremental run
    /// (<see cref="SolveRun.Incremental"/>); <c>endsRun</c> whether it is an Accept that ends the run
    /// (<see cref="GraphSubsetFrames.EndsRun"/>).
    /// </summary>
    public static GroupedGraphFrame Frame(GroupedGraphSpec spec, Assignment partial, StepEvent ev, string[] focus, string caption, bool? ok = null, bool incremental = false, bool endsRun = false) {
        // Variable index (1-based) to its value, for the variables this formula has.
        var values = new Dictionary<int, bool>();
        var assignment = new SortedDictionary<string, bool>(StringComparer.Ordinal);
        for (int v = 1; v <= spec.Variables.Count; v++)
            if (partial.Values.TryGetValue(spec.Variables[v - 1], out var text) && ParseValue(text) is bool value) {
                values[v] = value;
                assignment[VariableId(spec.Variables[v - 1])] = value;
            }

        // A true literal of a whole candidate that is only being tried is highlighted, not drawn as the answer; in an
        // incremental run the variables decided so far are kept so far (Committed) until the answer.
        var picked = GraphSubsetFrames.Picked(ev, incremental, endsRun, ok == true);
        var nodes = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var groups = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var literalsOfVariable = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        for (int i = 0; i < spec.Clauses.Count; i++) {
            int[] clause = spec.Clauses[i];
            int trues = 0, falses = 0;
            for (int j = 0; j < clause.Length; j++) {
                string id = LiteralId(i, j);
                int variable = Math.Abs(clause[j]);
                string variableId = VariableId(spec.Variables[variable - 1]);
                if (!literalsOfVariable.TryGetValue(variableId, out var list)) literalsOfVariable[variableId] = list = [];
                list.Add(id);
                if (!values.TryGetValue(variable, out bool assigned)) continue;   // unassigned: Background
                if (assigned == clause[j] > 0) {
                    trues++;
                    GraphSubsetFrames.Raise(nodes, id, picked);
                } else {
                    falses++;
                    GraphSubsetFrames.Raise(nodes, id, PictureState.False);
                }
            }
            if (trues > 0) GraphSubsetFrames.Raise(groups, ClauseId(i), PictureState.Covered);
            else if (clause.Length > 0 && falses == clause.Length) {
                GraphSubsetFrames.Raise(groups, ClauseId(i), PictureState.Rejected);
                for (int j = 0; j < clause.Length; j++) GraphSubsetFrames.Raise(nodes, LiteralId(i, j), PictureState.Rejected);
            }
        }

        // Focus is what the solver is looking at; a clause in a Reject step is the one that failed. A literal that is false
        // stays False when its variable is the one being looked at: it is false, whichever variable is being tried.
        void HighlightLiteral(string literal) {
            if (!nodes.TryGetValue(literal, out var state) || state != PictureState.False)
                GraphSubsetFrames.Raise(nodes, literal, PictureState.ElementHighlight);
        }
        var clauseMark = ev == StepEvent.Reject ? PictureState.Rejected : PictureState.ElementHighlight;
        foreach (var id in focus) {
            if (literalsOfVariable.TryGetValue(id, out var occurrences))
                foreach (var literal in occurrences) HighlightLiteral(literal);
            else if (IsClauseId(spec, id)) GraphSubsetFrames.Raise(groups, id, clauseMark);
            else if (IsLiteralId(spec, id)) HighlightLiteral(id);
        }

        return new GroupedGraphFrame(ev, caption, focus, GraphSubsetFrames.ToRefs(nodes), GraphSubsetFrames.ToRefs(groups), assignment) { Ok = ok };
    }

    private static bool IsClauseId(GroupedGraphSpec spec, string id) =>
        id.Length > 1 && id[0] == 'c' && int.TryParse(id.AsSpan(1), out int clause) && clause >= 0 && clause < spec.Clauses.Count && id == ClauseId(clause);

    private static bool IsLiteralId(GroupedGraphSpec spec, string id) {
        var match = LiteralIdPattern.Match(id);
        return match.Success
            && int.TryParse(match.Groups[1].Value, out int clause) && clause < spec.Clauses.Count
            && int.TryParse(match.Groups[2].Value, out int position) && position < spec.Clauses[clause].Length
            && id == LiteralId(clause, position);
    }

    private static readonly Regex LiteralIdPattern = new(@"^c(\d+)-(\d+)$", RegexOptions.Compiled);

    // ---- Reading answers ----

    /// <summary>
    /// The assignment named by a certificate such as <c>(x1:True,x2:False)</c> (<c>=</c> also separates; <c>T</c>/<c>F</c> are
    /// accepted; parentheses are optional). The value follows the <i>last</i> <c>:</c> or <c>=</c> of an item, so a name
    /// that contains one (<c>a:b:True</c>) is read whole. Values are normalized to <c>True</c>/<c>False</c>. "No Solution"
    /// and anything unreadable give no assignment. A name that contains <c>,</c> cannot be read back (the certificate
    /// format has no escape for it).
    /// </summary>
    public static Assignment ParseAssignment(string answer) {
        var pairs = new List<KeyValuePair<string, string>>();
        foreach (var item in answer.Trim().TrimStart('(').TrimEnd(')').Split(',')) {
            int at = item.LastIndexOfAny([':', '=']);
            if (at < 0) continue;
            string name = item[..at].Trim();
            if (name.Length == 0 || ParseValue(item[(at + 1)..]) is not bool value) continue;
            pairs.Add(new(name, value ? "True" : "False"));
        }
        return new Assignment(pairs.GroupBy(p => p.Key, StringComparer.Ordinal).Select(g => g.First()));
    }

    // ---- Captions and reasons, for solvers to call inside a recorder lambda so they cost nothing when recording is off ----

    /// <summary>A solver's values as the Assignment shape (<c>x1</c> to <c>"True"</c>), in the order given.</summary>
    public static Assignment ToAssignment(IEnumerable<KeyValuePair<string, bool>> values) =>
        new(values.Select(p => new KeyValuePair<string, string>(p.Key, p.Value.ToString())));

    /// <summary>A copy of <paramref name="values"/> with <paramref name="variable"/> set to <paramref name="value"/>: the values once a choice is made.</summary>
    public static Dictionary<string, bool> WithValue(IReadOnlyDictionary<string, bool> values, string variable, bool value) =>
        new(values) { [variable] = value };

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

    /// <summary>
    /// The index (0-based, formula order) of the first clause whose literals are all false under <paramref name="values"/>
    /// (a variable it does not list counts as unassigned, so it saves its clause), or -1 if there is none.
    /// </summary>
    public static int FirstFalsifiedClause(IReadOnlyList<List<string>> clauses, IReadOnlyDictionary<string, bool> values) {
        for (int i = 0; i < clauses.Count; i++) {
            if (clauses[i].Count == 0) continue;
            bool allFalse = true;
            foreach (var literal in clauses[i]) {
                bool negated = literal.StartsWith('!');
                if (!values.TryGetValue(negated ? literal.TrimStart('!') : literal, out bool value) || value != negated) { allFalse = false; break; }
            }
            if (allFalse) return i;
        }
        return -1;
    }

    /// <summary>
    /// Orders names the way people count: <c>x2</c> before <c>x10</c>, a before b. A name is read as runs of ASCII digits
    /// and runs of other characters, compared run by run: two digit runs by value (then by text, so <c>x01</c> comes
    /// before <c>x1</c>), two other runs ordinally, and a digit run before another run; a name that runs out first comes
    /// first. That is a total order, so the result never depends on the order the names arrive in.
    /// </summary>
    public static readonly IComparer<string> NaturalOrder = Comparer<string>.Create(NaturalCompare);

    private static int NaturalCompare(string? a, string? b) {
        if (a is null || b is null) return a is null ? (b is null ? 0 : -1) : 1;
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length) {
            bool digitA = char.IsAsciiDigit(a[i]), digitB = char.IsAsciiDigit(b[j]);
            int endA = RunEnd(a, i, digitA), endB = RunEnd(b, j, digitB);
            var runA = a.AsSpan(i, endA - i);
            var runB = b.AsSpan(j, endB - j);
            int byRun;
            if (digitA && digitB) {
                var va = runA.TrimStart('0');
                var vb = runB.TrimStart('0');
                byRun = va.Length != vb.Length ? va.Length.CompareTo(vb.Length) : va.SequenceCompareTo(vb);
                if (byRun == 0) byRun = runA.SequenceCompareTo(runB);
            } else if (digitA != digitB) {
                byRun = digitA ? -1 : 1;
            } else {
                byRun = runA.SequenceCompareTo(runB);
            }
            if (byRun != 0) return Math.Sign(byRun);
            i = endA;
            j = endB;
        }
        return (a.Length - i).CompareTo(b.Length - j);
    }

    private static int RunEnd(string s, int start, bool digits) {
        int end = start;
        while (end < s.Length && char.IsAsciiDigit(s[end]) == digits) end++;
        return end;
    }

    // ---- The whole response ----

    /// <summary>
    /// The caption of a solved frame built from an answer string: the assignment; "No answer was found." when the solver
    /// says there is none (<c>No Solution</c>, <c>No solution exists</c>, <c>{}</c>, nothing); and, for any other text it
    /// could not read (a solver error, another format), that the answer could not be drawn, so a failure is not passed off
    /// as an unsatisfiable formula.
    /// </summary>
    internal static string AnswerCaption(string answerText, Assignment answer) {
        if (answer.Values.Count > 0)
            return $"The answer is {Assigned(answer.Values.Select(p => new KeyValuePair<string, bool>(p.Key, p.Value == "True")))}.";
        string text = answerText.Trim();
        return text.Length == 0 || text == "{}" || text.StartsWith("No ", StringComparison.OrdinalIgnoreCase)
            ? "No answer was found."
            : "The solver's answer is not an assignment, so there is nothing to draw.";
    }

    /// <summary>
    /// The frame that closes a run stopped by its time limit: a Done frame with ok = false, no assignment, and every node
    /// and clause Background. The solver had not finished, so no literal is true and no clause is judged (an empty
    /// assignment is not a failed one). See <see cref="GraphSubsetFrames.StoppedFrame"/>.
    /// </summary>
    public static GroupedGraphFrame StoppedFrame(string caption) =>
        new(StepEvent.Done, caption, [], new SortedDictionary<string, StateRef>(StringComparer.Ordinal),
            new SortedDictionary<string, StateRef>(StringComparer.Ordinal), new SortedDictionary<string, bool>(StringComparer.Ordinal)) { Ok = false };

    /// <summary>
    /// What the SAT brute force answers when its own timer (<see cref="ISolver.timerHasExpired"/>) stops it: the shared
    /// <see cref="SolveRun.TimedOutAnswer"/> since #935. It is not a certificate; <see cref="Build"/> treats it like a run
    /// stopped by the recording time limit.
    /// </summary>
    public const string SolverTimeoutAnswer = SolveRun.TimedOutAnswer;

    /// <summary>
    /// Whether <paramref name="answer"/> says the solver was stopped by its own timer: <see cref="SolverTimeoutAnswer"/>, or
    /// the older "timeout" some solvers answered before #935, in any case.
    /// </summary>
    public static bool IsSolverTimeout(string answer) {
        string text = answer.Trim();
        return string.Equals(text, SolverTimeoutAnswer, StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "timeout", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The caption of the Done step a solver records when its own timer stops it.</summary>
    public const string SolverTimeoutCaption = "The solver stopped without finishing: its time ran out before it found an answer or ruled one out.";

    /// <summary>
    /// The whole response: the payload, a frame per kept step, then a solved frame built from the answer unless the last
    /// step already is the Done step. A run that was stopped, by the recording time limit (<see cref="SolveRun.TimedOut"/>)
    /// or by the solver's own timer (the answer <see cref="SolverTimeoutAnswer"/>), ends with <see cref="StoppedFrame"/>
    /// instead, and its answer string is never read.
    /// </summary>
    public static FramesResponse Build(GroupedGraphSpec spec, SolveRun run, Func<string, Assignment> parseAnswer, Func<string, bool> verifyAnswer) {
        bool stopped = run.TimedOut || GroupedGraphFrames.IsSolverTimeout(run.Answer);
        var frames = new List<object>();
        SolverStep<Assignment>? last = null;
        foreach (var (step, endsRun, index) in GraphSubsetFrames.DrawnSteps<Assignment>(run)) {
            frames.Add((stopped && step.Event == StepEvent.Done
                ? StoppedFrame(step.Caption)
                : Frame(spec, step.Partial, step.Event, step.Focus, step.Caption, step.Ok, run.Incremental, endsRun)) with { Step = index });
            last = step;
        }
        if (stopped) {
            if (last is not { Event: StepEvent.Done })
                frames.Add(StoppedFrame(run.TimedOut ? GraphSubsetFrames.StoppedCaption : SolverTimeoutCaption));
        } else if (last is not { Event: StepEvent.Done }) {
            Assignment answer = parseAnswer(run.Answer);
            frames.Add(Frame(spec, answer, StepEvent.Done, [], AnswerCaption(run.Answer, answer), verifyAnswer(run.Answer)));
        }
        return new FramesResponse(nameof(VisualizationType.GroupedGraph), Payload(spec), frames);
    }
}

/// <summary>
/// A visualization for a CNF problem (SAT, 3SAT). The visualization declares only what the problem is
/// (<see cref="Describe"/>: its clauses); the frames hook then comes for free. Implement members explicitly so nothing new
/// appears in /info. The class must also declare <c>Type? IVisualization.StepShape =&gt; typeof(Assignment)</c>. A solver's
/// steps carry the assignment so far (<see cref="Assignment"/>: variable name to <c>True</c>/<c>False</c>); only the solved
/// frame of a solver that records no steps has to be rebuilt from the answer string.
/// </summary>
interface IGroupedGraphVisualization<TProblem> : IFramesVisualization where TProblem : IProblem {
    GroupedGraphSpec Describe(TProblem problem);

    /// <summary>Reads a solver's answer string as an assignment. The default accepts <c>(x1:True,x2:False)</c>.</summary>
    Assignment ParseAnswer(TProblem problem, string answer) => GroupedGraphFrames.ParseAssignment(answer);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        return GroupedGraphFrames.Build(Describe(problem), run,
            answer => ParseAnswer(problem, answer),
            answer => {
                // The problem's own verifier decides; a certificate it cannot read (such as "No Solution") is not correct.
                try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
            });
    }
}
