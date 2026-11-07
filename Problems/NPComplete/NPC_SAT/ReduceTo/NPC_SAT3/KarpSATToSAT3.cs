using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Logic;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT3;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_SAT.ReduceTo.NPC_SAT3;

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
        RecordSteps(original, clauses, padded, rec);
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
    /// Maps a SAT assignment to the 3SAT formula reduce() built (#821). The original variables keep their values. A new variable
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

    // Construction steps (see IRecordedReduction), one per clause of the SAT formula: the clause as it is, padded, or split
    // into a chain, with the 3SAT clauses, literals and variables it brings, then Done. Also adds the variable gadgets: each
    // variable of the SAT formula is the same variable of the 3SAT formula, and each new variable comes from the clause whose
    // split made it. Ids are the Grouped Graph picture's, on both sides.
    private void RecordSteps(List<List<string>> original, List<List<string>> target, Dictionary<int, int> padded, StepRecorder<BuildDelta> rec) {
        var targetOf = Enumerable.Range(0, original.Count).ToDictionary(i => i, i => new List<int> { i });
        foreach (var (origin, made) in _chain) targetOf[origin].Add(made);
        var helperOf = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (origin, made) in _chain) helperOf[target[made][2]] = origin;
        var sourceVariables = CnfFormula.VariablesOf(original);

        // Variable gadgets go after the legacy ones, so the legacy list keeps its order.
        var variableGadget = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string variable in sourceVariables) {
            string id = GroupedGraphFrames.VariableId(variable);
            variableGadget[variable] = gadgets.Count;
            // Redux_GUI draws literals, not variables: its legacy ids are the variable's occurrences on each side.
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { id }, new List<string> { id },
                legacyFromIds: GroupedGraphFrames.LegacyIds([id], original), legacyToIds: GroupedGraphFrames.LegacyIds([id], target)));
        }
        foreach (var (helper, origin) in helperOf) {
            variableGadget[helper] = gadgets.Count;
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { GroupedGraphFrames.ClauseId(origin) }, new List<string> { GroupedGraphFrames.VariableId(helper) },
                legacyToIds: GroupedGraphFrames.LegacyIds([GroupedGraphFrames.VariableId(helper)], target),
                note: $"{helper} is new: it links the clauses that clause C{origin + 1} is split into."));
        }
        if (!rec.Enabled) return;

        // The picture's literal occurrences, and its rule for edges (GroupedGraphFrames.Payload): two occurrences in one clause
        // are a clause edge; a variable and its negation in two clauses are a complement edge. The earlier occurrence is written
        // first.
        var builtClauses = new List<int>();
        var builtVariables = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < original.Count; i++) {
            int clause = i;
            var mine = targetOf[clause].OrderBy(t => t).ToList();
            var nodes = new List<string>();
            var edges = new List<BuildEdge>();
            foreach (int t in mine) {
                nodes.Add(GroupedGraphFrames.ClauseId(t));
                for (int j = 0; j < target[t].Count; j++) nodes.Add(GroupedGraphFrames.LiteralId(t, j));
            }
            var newVariables = mine.SelectMany(t => target[t]).Select(CnfFormula.VariableOf).Distinct(StringComparer.Ordinal)
                .Where(builtVariables.Add).ToList();
            nodes.AddRange(newVariables.Select(GroupedGraphFrames.VariableId));
            var before = builtClauses.ToList();
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

            var used = gadgets.Select((g, index) => (g, index))
                .Where(p => p.g.sourceIds.Count > 0 && (p.g.sourceIds[0] == GroupedGraphFrames.ClauseId(clause) || p.g.sourceIds[0].StartsWith(GroupedGraphFrames.ClauseId(clause) + "-", StringComparison.Ordinal)))
                .Select(p => p.index)
                .Concat(newVariables.Where(variableGadget.ContainsKey).Select(v => variableGadget[v]))
                .Distinct().Order().ToArray();
            string[] sourceIds = GroupedGraphFrames.ClauseIds(clause, original[clause].Count);
            string[] focus = nodes.Where(n => !n.StartsWith("v:", StringComparison.Ordinal)).ToArray();
            var helpers = _chain.Where(c => c.Origin == clause).Select(c => target[c.Made][2]).ToList();
            rec.Accept(
                () => new BuildDelta { SourceFocus = sourceIds, Nodes = nodes.ToArray(), Edges = BuildEdge.Sorted(edges), Gadgets = used },
                () => {
                    string text = GroupedGraphFrames.ClauseText(original[clause]);
                    int n = original[clause].Count;
                    if (mine.Count > 1)
                        return $"Clause C{clause + 1} {text} has {n} literals, so it is split into a chain of {mine.Count} clauses joined by the new "
                            + $"{(helpers.Count == 1 ? "variable" : "variables")} {GraphGroupFrames.And(helpers)}: "
                            + string.Join(", ", mine.Select(t => $"C{t + 1} {GroupedGraphFrames.ClauseText(target[t])}"))
                            + ". Each new variable carries \"a literal further along is true\" from one clause of the chain to the next.";
                    if (padded.ContainsKey(clause))
                        return $"Clause C{clause + 1} {text} has {Words.Count(n, "literal")}, so {original[clause][0]} is repeated to fill three: {GroupedGraphFrames.ClauseText(target[clause])}. A repeated literal does not change the clause.";
                    return $"Clause C{clause + 1} {text} has three literals, so it stays as it is.";
                },
                focus);
        }
        rec.Done(new BuildDelta(), true,
            $"{Words.Count(target.Count, "clause")} of three literals: the 3SAT formula can be satisfied exactly when the SAT formula can.");
    }

    public Assignment EmptyAnswer() => new Assignment([]);

    // A SAT or 3SAT answer is an assignment, never "{}": both keep the default (no answer).

    /// <summary>
    /// Backward map (#693): a 3SAT assignment to a SAT assignment. Every variable of the SAT formula is the same variable of the
    /// 3SAT formula, so it keeps its value; the new variables of the splits only link the halves of a clause, so they are
    /// dropped. A variable the answer does not give is set to False and the step is a Reject; the Done step then reports what
    /// SAT's verifier says.
    /// </summary>
    public (string Answer, Assignment Partial) MapBack(string problemToSolution, StepRecorder<Assignment> rec) {
        var given = ReadAssignment(problemToSolution);
        var values = new Dictionary<string, bool>();
        var order = CnfFormula.VariablesOf(reductionFrom.clauses);
        foreach (string variable in order) {
            string id = GroupedGraphFrames.VariableId(variable);
            if (given.TryGetValue(variable, out bool value)) {
                values[variable] = value;
                rec.Accept(() => GroupedGraphFrames.ToAssignment(values),
                    () => $"{variable} = {Assignment.Word(value)} in the 3SAT answer, and it is the same variable in the SAT formula: {variable} = {Assignment.Word(value)}.", id);
            } else {
                values[variable] = false;
                rec.Reject(() => GroupedGraphFrames.ToAssignment(values), () => $"The 3SAT answer does not give {variable}, so it is set to False.", id);
            }
        }
        return (SAT.Certificate(order, values), GroupedGraphFrames.ToAssignment(values));
    }

    // A 3SAT assignment, (x1:True,x2:False) (':' or '=', T and F accepted). Every name must be a variable of the 3SAT formula;
    // malformed text throws FormatException, which the endpoint reports as a 400.
    private Dictionary<string, bool> ReadAssignment(string certificate) {
        var known = new HashSet<string>(reductionTo.clauses.SelectMany(c => c).Select(CnfFormula.VariableOf), StringComparer.Ordinal);
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
            if (!known.Contains(name)) throw new FormatException($"'{name}' is not a variable of the 3SAT formula");
            values[name] = v;
        }
        return values;
    }
}
