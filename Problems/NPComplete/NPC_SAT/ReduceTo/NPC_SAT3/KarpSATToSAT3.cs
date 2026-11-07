using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Logic;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT3;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_SAT.ReduceTo.NPC_SAT3;

// Karp's SAT to 3SAT: a clause of three literals stays as it is; a shorter one repeats its first literal to fill three; a
// longer one is split into a chain of three-literal clauses joined by new variables (the old clause keeps !y, and the
// new clause (l1 | l2 | y) takes its last two literals). It records the construction steps (one per clause of the SAT
// formula, then the Done; see BuildVariables) and the backward map's steps (one per variable of the SAT formula; see
// MapBack).
class KarpSATToSAT3 : IReversibleReduction<SAT, SAT3, Assignment>, IRecordedReduction<SAT, SAT3> {

    // --- Fields ---
    public string reductionName { get; } = "Karp's 3SAT Reduction";
    public string reductionDefinition { get; } = "Karp's Reduction from SAT to SAT3";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    // Each over-length clause split removes literals from the original clause and adds
    // one new 3-literal clause per split; total splits (and clauses produced) is O(total
    // literal count) across the whole formula — linear in input size.
    public ReductionCost cost { get; } = ReductionCost.Linear;

    // Declared, not derived. Each over-length clause is split independently by
    // introducing its own chain of fresh variables -- no coordination needed between
    // different clauses' splits.
    public ReductionType reductionType { get; } = ReductionType.LocalReplacement;
    // Declared, not derived. Total splits (and clauses produced) is O(total literal
    // count) across the whole formula.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Linear;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(L), L = total literal count across the SAT formula";
    public List<Gadget> gadgets { get; }
    private SAT _reductionFrom;
    private SAT3 _reductionTo;


    // --- Properties ---
    public SAT reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public SAT3 reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }



    // --- Methods Including Constructors ---
    public KarpSATToSAT3(SAT from) : this(from, StepRecorder<BuildDelta>.Off) { }

    // The one place the target is built; rec is Off except when the construction steps are asked for.
    private KarpSATToSAT3(SAT from, StepRecorder<BuildDelta> rec) {
        gadgets = new();
        _reductionFrom = from;
        _reductionTo = Build(rec);
    }
    public KarpSATToSAT3(string instance) : this(new SAT(instance)) { }
    // Builds from the instance with rec recording the construction as it goes (IRecordedReduction).
    public KarpSATToSAT3(string instance, StepRecorder<BuildDelta> rec) : this(new SAT(instance), rec) { }

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new KarpSATToSAT3(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }
    public KarpSATToSAT3() : this(new SAT()) { }
    // Clause "i" is c{i} and literal "i-j" is c{i}-{j}, in the gadgets' stable and legacy ids alike: both SAT pictures (the
    // list format's API_SAT and the Grouped Graph) use these ids.
    private static string StableId(string index) => "c" + index;

    public SAT3 reduce() => Build(StepRecorder<BuildDelta>.Off);

    // Builds the 3SAT formula: every clause longer than three is split, then every clause shorter than three padded. Its
    // construction steps are recorded where the variable gadgets are made, once the formula is done (see BuildVariables):
    // one per clause of the SAT formula, then the Done.
    private SAT3 Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();
        _splits.Clear();
        _chain.Clear();
        SAT SATInstance = _reductionFrom;
        SAT3 reducedSAT3 = new SAT3();

        List<string> literals = SATInstance.getLiterals(SATInstance.instance);
        List<List<string>> clauses = SATInstance.getClauses(SATInstance.instance);
        List<List<string>> original = clauses.Select(c => c.ToList()).ToList();
        var padded = new Dictionary<int, int>();

        for (int clauseIndex = 0; clauseIndex < clauses.Count; clauseIndex++) {
            string clauseId = StableId(clauseIndex.ToString());
            gadgets.Add(new Gadget(
                GadgetKind.Group,
                new List<string>() { clauseId },
                new List<string>() { clauseId }
            ));

            for (int literalIndex = 0; literalIndex < clauses[clauseIndex].Count; literalIndex++) {
                string literalId = StableId(clauseIndex + "-" + literalIndex);
                gadgets.Add(new Gadget(
                    GadgetKind.Element,
                    new List<string>() { literalId },
                    new List<string>() { literalId }
                ));
            }
        }


        string newInstance = "x1";

        int index = findSet(clauses);
        while (index != -1) {
            reduceSetSize(index, ref literals, ref newInstance, ref clauses);
            index = findSet(clauses);
        }

        reducedSAT3.clauses = clauses;
        reducedSAT3.literals = literals;

        string instance = "(";
        for (int c = 0; c < clauses.Count; c++) {
            var i = clauses[c];

            for (int k = 0; k < i.Count; k++) {
                instance += " " + i[k] + " |";
            }

            if (i.Count < 3) padded[c] = i.Count;
            if (i.Count == 2) {
                instance += " " + i[0] + " |";
                i.Add(i[0]);
                gadgets.Add(new Gadget(GadgetKind.Element, new List<string>() { StableId(c + "-0") }, new List<string>() { StableId(c + "-2") }));
            }

            if (i.Count == 1) {
                instance += " " + i[0] + " |" + " " + i[0] + " |";
                i.Add(i[0]);
                i.Add(i[0]);
                gadgets.Add(new Gadget(GadgetKind.Element, new List<string>() { StableId(c + "-0") }, new List<string>() { StableId(c + "-2"), StableId(c + "-1") }));
            }

            instance = instance.TrimEnd('|') + ") & (";
        }

        instance = instance.TrimEnd('(').TrimEnd().TrimEnd('&');

        reducedSAT3.instance = instance;

        reductionTo = reducedSAT3;
        BuildVariables(original, clauses, padded, rec);
        return reducedSAT3;
    }

    public void reduceSetSize(int index, ref List<string> literals, ref string newVar, ref List<List<string>> clauses) {

        while (literals.Contains(newVar) || literals.Contains("!" + newVar)) {
            int n = int.Parse(newVar.Substring(1)) + 1;
            newVar = "x" + n;
        }

        literals.Add(newVar);
        literals.Add("!" + newVar);

        // Getting last two literals from the clause
        string l1 = clauses[index][clauses[index].Count - 2];
        string l2 = clauses[index][clauses[index].Count - 1];
        _splits.Add((newVar, l1, l2));

        string index1 = index.ToString() + "-" + (clauses[index].Count - 2).ToString();
        string index2 = index.ToString() + "-" + (clauses[index].Count - 1).ToString();

        clauses.Add(new List<string> { l1, l2, newVar });
        _chain.Add((index, clauses.Count - 1));

        // Adding the new clause to its source clause's highlighting gadget. findSet only returns clauses
        // with more than 3 literals and every clause added here has exactly 3, so index is always an
        // original clause, and reduce() gave each original clause a group gadget before splitting.
        var existing = gadgets.First(g =>
            g.color == "ClauseHighlight" &&
            g.reductionFromIds.FirstOrDefault() == StableId(index.ToString())
        );
        existing.reductionToIds.Add(StableId((clauses.Count - 1).ToString()));
        existing.targetIds.Add(StableId((clauses.Count - 1).ToString()));

        // Adding element highlighting gadget
        var literal1 = gadgets.FirstOrDefault(g =>
            g.color == "ElementHighlight" &&
            g.reductionToIds.Contains(StableId(index1))
        );

        var literal2 = gadgets.FirstOrDefault(g =>
            g.color == "ElementHighlight" &&
            g.reductionToIds.Contains(StableId(index2))
        );

        if (literal1 != null) {
            literal1.reductionToIds.Remove(StableId(index1));
            literal1.reductionToIds.Add(StableId((clauses.Count - 1).ToString() + "-0"));
            literal1.targetIds.Remove(StableId(index1));
            literal1.targetIds.Add(StableId((clauses.Count - 1).ToString() + "-0"));
        }
        if (literal2 != null) {
            literal2.reductionToIds.Remove(StableId(index2));
            literal2.reductionToIds.Add(StableId((clauses.Count - 1).ToString() + "-1"));
            literal2.targetIds.Remove(StableId(index2));
            literal2.targetIds.Add(StableId((clauses.Count - 1).ToString() + "-1"));
        }

        // Removing last two literals from the original clause
        clauses[index].RemoveAt(clauses[index].Count - 1);
        clauses[index].RemoveAt(clauses[index].Count - 1);

        clauses[index].Add("!" + newVar);
    }


    public int findSet(List<List<string>> clauses) {
        for (int i = 0; i < clauses.Count; i++) {
            if (clauses[i].Count > 3) return i;
        }
        return -1;
    }


    // Every split, in the order reduce() made them: the new variable and the two literals moved with it into its new clause
    // (l1 | l2 | y), while the old clause keeps !y. mapSolutions reads them back.
    private readonly List<(string Variable, string A, string B)> _splits = new();

    // Every clause a split adds, with the clause of the SAT formula it came from, in the order reduce() made them.
    private readonly List<(int Origin, int Made)> _chain = new();

    /// <summary>
    /// Maps a SAT assignment to the 3SAT formula reduce() built. The original variables keep their values. A new variable
    /// y of a split is true exactly when both literals moved into its clause (l1 | l2 | y) are false: then that clause holds
    /// through y, and the shortened clause, which keeps !y, has to hold through the literals left in it, as the original
    /// clause did. Splits are read in the order they were made, so a literal that is an earlier split's !y already has a value.
    /// A variable the assignment does not mention is False.
    /// </summary>
    public string mapSolutions(string reductionFromSolution) {
        var values = new Dictionary<string, bool>();
        var order = new List<string>();
        string text = reductionFromSolution.Trim();
        if (text.StartsWith('(') && text.EndsWith(')')) text = text[1..^1];
        foreach (string raw in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            string[] parts = raw.Split(':', '=');
            bool? value = parts.Length != 2 ? null : parts[1].Trim().ToLowerInvariant() switch {
                "true" or "t" => true,
                "false" or "f" => false,
                _ => null,
            };
            if (value is not bool v || parts[0].Trim().Length == 0)
                throw new ReductionInputException(this, reductionFromSolution, reductionFrom.certificateFormat,
                    $"'{raw}' is not a variable:True or variable:False pair");
            string name = parts[0].Trim();
            if (!values.ContainsKey(name)) order.Add(name);
            values[name] = v;
        }
        bool Holds(string literal) => literal.StartsWith('!')
            ? !values.GetValueOrDefault(literal[1..])
            : values.GetValueOrDefault(literal);
        foreach (var (variable, a, b) in _splits) {
            if (!values.ContainsKey(variable)) order.Add(variable);
            values[variable] = !Holds(a) && !Holds(b);
        }
        return "(" + string.Join(",", order.Select(v => $"{v}:{(values[v] ? "True" : "False")}")) + ")";
    }

    // How the construction steps show a clause of the SAT formula: the clause and its literals in SAT; the 3SAT clauses it
    // becomes, their literals, the variables no earlier step mentions and the formula picture's edges, added to 3SAT; and its
    // gadgets. The state is (Clause, Literals, Pieces): the clause's index and literals, and those pieces.
    private static RunSteps<(int Clause, List<string> Literals, ClausePieces Pieces), BuildDelta> NewClauseSteps(StepRecorder<BuildDelta> rec) => new(rec,
        // What the Reduction View shows: the clause, the pieces added, and the gadgets that made them.
        partial: s => new BuildDelta {
            SourceFocus = GroupedGraphFrames.ClauseIds(s.Clause, s.Literals.Count),
            Nodes = s.Pieces.Nodes.ToArray(),
            Edges = BuildEdge.Sorted(s.Pieces.Edges),
            Gadgets = s.Pieces.Gadgets,
        },
        // The 3SAT clauses and their literals; the variables are not looked at.
        focus: s => s.Pieces.Nodes.Where(n => !n.StartsWith("v:", StringComparison.Ordinal)).ToArray());

    // What a clause of the SAT formula adds to the 3SAT picture (its clauses and literals, then the variables no earlier step
    // mentions; the edges between their literals and earlier ones), and the indices of the gadgets made from it.
    private sealed record ClausePieces(List<string> Nodes, List<BuildEdge> Edges, int[] Gadgets);

    // Makes the variable gadgets once the formula is built, recording the construction steps as it goes (see
    // IRecordedReduction): each variable of the SAT formula is the same variable of the 3SAT formula, and each new variable
    // comes from the clause whose split made it. Its steps, one per clause of the SAT formula:
    //   - an Accept when the clause is split into a chain of clauses, joined by new variables;
    //   - an Accept when it is padded to three literals by repeating its first;
    //   - an Accept when it has three literals and stays as it is;
    //   - then the Done: the 3SAT formula can be satisfied exactly when the SAT formula can.
    // A construction has no verdicts, so Accept means "these pieces go into the target". A clause's step is recorded where
    // the last of its gadgets is made, the gadgets of the new variables its split brings. Ids are the Grouped Graph
    // picture's, on both sides.
    // Gadgets: these go after the legacy ones (a group per clause and an element per literal, then one per padding), so the
    // legacy list keeps its order: an element per variable of the SAT formula, then an element per new variable.
    private void BuildVariables(List<List<string>> original, List<List<string>> target, Dictionary<int, int> padded, StepRecorder<BuildDelta> rec) {
        // The 3SAT clauses each clause of the SAT formula becomes: itself, then the clauses its split adds, in the order they
        // were made (which is formula order).
        var targetOf = Enumerable.Range(0, original.Count).ToDictionary(i => i, i => new List<int> { i });
        foreach (var (origin, made) in _chain) targetOf[origin].Add(made);
        var sourceVariables = CnfFormula.VariablesOf(original);

        // One element per variable of the SAT formula.
        var variableGadget = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string variable in sourceVariables) {
            string id = GroupedGraphFrames.VariableId(variable);
            variableGadget[variable] = gadgets.Count;
            // Redux_GUI draws literals, not variables: its legacy ids are the variable's occurrences on each side.
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { id }, new List<string> { id },
                legacyFromIds: GroupedGraphFrames.LegacyIds([id], original), legacyToIds: GroupedGraphFrames.LegacyIds([id], target)));
        }

        // The 3SAT clauses and the variables an earlier step already added to the picture; made by the first step recorded,
        // since only the picture reads them.
        List<int>? builtClauses = null;
        HashSet<string>? builtVariables = null;
        // What the clause's 3SAT clauses add to the picture. Finding their edges compares each of their literals with every
        // earlier one, and only the picture needs them, so it is called only when the steps are recorded. The picture's rule
        // for edges (GroupedGraphFrames.Payload): two occurrences in one clause are a clause edge; a variable and its negation
        // in two clauses are a complement edge. The earlier occurrence is written first.
        ClausePieces Pieces(int clause, List<int> mine) {
            builtClauses ??= new();
            builtVariables ??= new(StringComparer.Ordinal);
            var nodes = new List<string>();
            var edges = new List<BuildEdge>();
            foreach (int t in mine) {
                nodes.Add(GroupedGraphFrames.ClauseId(t));
                for (int j = 0; j < target[t].Count; j++) nodes.Add(GroupedGraphFrames.LiteralId(t, j));
            }
            var newVariables = mine.SelectMany(t => target[t]).Select(CnfFormula.VariableOf).Distinct(StringComparer.Ordinal)
                .Where(builtVariables.Add).ToList();
            nodes.AddRange(newVariables.Select(GroupedGraphFrames.VariableId));
            builtClauses.AddRange(mine);
            foreach (int t in mine)
                for (int j = 0; j < target[t].Count; j++)
                    foreach (int u in builtClauses)
                        for (int k = 0; k < target[u].Count; k++) {
                            // Each pair once: a pair inside this step's clauses is taken from its earlier occurrence.
                            if ((u, k).CompareTo((t, j)) >= 0 && mine.Contains(u)) continue;
                            var (first, second) = (u, k).CompareTo((t, j)) < 0 ? ((u, k), (t, j)) : ((t, j), (u, k));
                            string a = target[first.Item1][first.Item2], b = target[second.Item1][second.Item2];
                            bool edge = first.Item1 == second.Item1 || (CnfFormula.VariableOf(a) == CnfFormula.VariableOf(b) && a.StartsWith('!') != b.StartsWith('!'));
                            if (!edge) continue;
                            string idA = GroupedGraphFrames.LiteralId(first.Item1, first.Item2), idB = GroupedGraphFrames.LiteralId(second.Item1, second.Item2);
                            edges.Add(new BuildEdge(GroupedGraphFrames.EdgeId(idA, idB), idA, idB));
                        }
            // Its gadgets: the ones made from the clause or its literals, and the ones of the variables it brings.
            var used = gadgets.Select((g, index) => (g, index))
                .Where(p => p.g.sourceIds.Count > 0 && (p.g.sourceIds[0] == GroupedGraphFrames.ClauseId(clause) || p.g.sourceIds[0].StartsWith(GroupedGraphFrames.ClauseId(clause) + "-", StringComparison.Ordinal)))
                .Select(p => p.index)
                .Concat(newVariables.Where(variableGadget.ContainsKey).Select(v => variableGadget[v]))
                .Distinct().Order().ToArray();
            return new ClausePieces(nodes, edges, used);
        }

        // Then each clause of the SAT formula, in order: an element per new variable its split brings (from the clause to the
        // variable), and one step.
        var clauseSteps = NewClauseSteps(rec);
        for (int clause = 0; clause < original.Count; clause++) {
            var mine = targetOf[clause];
            for (int m = 1; m < mine.Count; m++) {
                string helper = target[mine[m]][2];
                variableGadget[helper] = gadgets.Count;
                gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { GroupedGraphFrames.ClauseId(clause) }, new List<string> { GroupedGraphFrames.VariableId(helper) },
                    legacyToIds: GroupedGraphFrames.LegacyIds([GroupedGraphFrames.VariableId(helper)], target),
                    note: $"{helper} is new: it links the clauses that clause C{clause + 1} is split into."));
            }
            // None when the steps are not recorded: no step then reads them.
            var pieces = rec.Enabled ? Pieces(clause, mine) : null!;
            if (mine.Count > 1)
                clauseSteps.Accept((clause, original[clause], pieces),
                    $"Clause C{clause + 1} {GroupedGraphFrames.ClauseText(original[clause])} has {original[clause].Count} literals, so it is split into a chain of {mine.Count} clauses joined by the new {(mine.Count == 2 ? "variable" : "variables")} {GraphGroupFrames.And(mine.Skip(1).Select(t => target[t][2]).ToList())}: {string.Join(", ", mine.Select(t => $"C{t + 1} {GroupedGraphFrames.ClauseText(target[t])}"))}. Each new variable carries \"a literal further along is true\" from one clause of the chain to the next.");
            else if (padded.ContainsKey(clause))
                clauseSteps.Accept((clause, original[clause], pieces),
                    $"Clause C{clause + 1} {GroupedGraphFrames.ClauseText(original[clause])} has {Words.Count(original[clause].Count, "literal")}, so {original[clause][0]} is repeated to fill three: {GroupedGraphFrames.ClauseText(target[clause])}. A repeated literal does not change the clause.");
            else
                clauseSteps.Accept((clause, original[clause], pieces),
                    $"Clause C{clause + 1} {GroupedGraphFrames.ClauseText(original[clause])} has three literals, so it stays as it is.");
        }

        // The closing step.
        clauseSteps.Done(new BuildDelta(), true,
            $"{Words.Count(target.Count, "clause")} of three literals: the 3SAT formula can be satisfied exactly when the SAT formula can.");
    }

    public Assignment EmptyAnswer() => new Assignment([]);

    // A SAT or 3SAT answer is an assignment, never "{}": both keep the default (no answer).

    // How the map-back steps show a variable read: the assignment so far, and the id the step is about (the variable).
    private static RunSteps<string, Assignment> NewMapSteps(StepRecorder<Assignment> rec, Dictionary<string, bool> values) => new(rec,
        // What every picture shows: the assignment so far, and the piece the step is about.
        partial: _ => GroupedGraphFrames.ToAssignment(values),
        focus: id => [id]);

    // Backward map: a 3SAT assignment to a SAT assignment. Every variable of the SAT formula is the same variable of the 3SAT
    // formula, so it keeps its value; the new variables of the splits only link the halves of a clause, so they are dropped.
    // SAT's verifier decides whether the assignment satisfies the formula.
    // Its steps: an Accept per variable the 3SAT answer gives (it keeps that value), and a Reject per variable it does not
    // give (it is set to False). The caller adds the Done.
    public (string Answer, Assignment Partial) MapBack(string problemToSolution, StepRecorder<Assignment> rec) {
        var given = SAT.ReadAssignment(problemToSolution, CnfFormula.VariablesOf(reductionTo.clauses), "3SAT");
        var values = new Dictionary<string, bool>();
        var steps = NewMapSteps(rec, values);
        var order = CnfFormula.VariablesOf(reductionFrom.clauses);
        foreach (string variable in order) {
            string id = GroupedGraphFrames.VariableId(variable);
            if (given.TryGetValue(variable, out bool value)) {
                values[variable] = value;
                steps.Accept(id, $"{variable} = {Assignment.Word(value)} in the 3SAT answer, and it is the same variable in the SAT formula: {variable} = {Assignment.Word(value)}.");
            } else {
                values[variable] = false;
                steps.Reject(id, $"The 3SAT answer does not give {variable}, so it is set to False.");
            }
        }
        return (SAT.Certificate(order, values), GroupedGraphFrames.ToAssignment(values));
    }
}
