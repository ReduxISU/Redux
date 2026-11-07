using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Logic;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT3;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_CLIQUE;

namespace API.Problems.NPComplete.NPC_CLIQUE.ReduceTo.NPC_SAT3;

// Not a general reduction: reduce() only produces a meaningful SAT3 formula for CLIQUE
// instances already Sipser-formatted by SipserReduceToCliqueStandard (nodes named
// '<literal>_<clauseIdx>') -- see SipserNodes.Parse and reductionDefinition. An
// arbitrary CLIQUE instance (e.g. CLIQUE.defaultInstance, plain node names) has no such
// structure to invert, so the parameterless constructor starts from SipserReduceToCliqueStandard's
// output for 3SAT's default formula instead of `new CLIQUE()` (#767). Excluded from
// ReductionGraphData's navigable graph so it isn't advertised as
// a working CLIQUE -> SAT3 reduction for any input; still directly callable by name via
// /ProblemProvider/reduce for its actual purpose (mapping a solution back through a
// paired SipserReduceToCliqueStandard run).
[NotAGeneralReduction]
class SipserReduceToSAT3 : IReversibleReduction<CLIQUE, API.Problems.NPComplete.NPC_SAT3.SAT3, NodeSet>, IRecordedReduction<CLIQUE, API.Problems.NPComplete.NPC_SAT3.SAT3> {
    // --- Fields ---
    public string reductionName { get; } = "Sipser's 3SAT Reduction";
    public string reductionDefinition { get; } =
        "Inverse of SipserReduceToCliqueStandard. Reconstructs the 3SAT formula from a "
        + "Sipser-formatted CLIQUE instance (nodes named '<literal>_<clauseIdx>') and "
        + "maps a clique certificate back to a 3SAT assignment. Only meaningful for "
        + "CLIQUE instances produced by SipserReduceToCliqueStandard.";
    public string source { get; } = "Sipser, Michael. Introduction to the Theory of Computation. ACM Sigact News 27.1 (1996): 27-29.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Jason L. Wright" };
    // reduce() does a single pass over the CLIQUE instance's nodes, grouping them into
    // clauses by trailing '_<clauseIdx>' suffix — output clause count is O(n) in the
    // number of input nodes.
    public ReductionCost cost { get; } = ReductionCost.Linear;

    // Declared, not derived. Each node is placed into its clause bucket independently
    // by parsing its own suffix -- no coordination needed between nodes/clauses.
    public ReductionType reductionType { get; } = ReductionType.LocalReplacement;
    // Declared, not derived. Single pass over the input's nodes, grouping by suffix.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Linear;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(n), n = |CLIQUE.nodes|";

    /// <summary>
    /// The gadgets (#694): per clause, a group (the clique nodes of that clause to the clause and its literals), and per node,
    /// an element (the node to the literal it is written as).
    /// </summary>
    public List<Gadget> gadgets { get; set; }

    // The clique nodes of each clause of the target, in clause order, with the literal each is and that literal's id.
    // Private, so it stays out of the JSON.
    private List<List<(string Node, string Literal, string LiteralId)>> _clauseNodes = new();

    private CLIQUE _reductionFrom;
    private API.Problems.NPComplete.NPC_SAT3.SAT3 _reductionTo;

    // --- Properties ---
    public CLIQUE reductionFrom {
        get { return _reductionFrom; }
        set { _reductionFrom = value; }
    }
    public API.Problems.NPComplete.NPC_SAT3.SAT3 reductionTo {
        get { return _reductionTo; }
        set { _reductionTo = value; }
    }

    // --- Constructors ---
    public SipserReduceToSAT3(CLIQUE from) : this(from, StepRecorder<BuildDelta>.Off) { }

    // The one place the target is built; rec is Off except when the construction steps are asked for.
    private SipserReduceToSAT3(CLIQUE from, StepRecorder<BuildDelta> rec) {
        gadgets = new();
        _reductionFrom = from;
        try {
            _reductionTo = Build(rec);
        } catch (ArgumentException ex) {
            // SipserNodes.Parse rejected one of the instance's node names.
            // The CLIQUE itself is well-formed; it's just not the
            // Sipser-formatted variety this reduction needs as input.
            throw new ReductionInputException(this, from.instance,
                _reductionFrom.instanceFormat +
                " | Additionally, this reduction requires Sipser-formatted node names of the form '<literal>_<clauseIdx>'.",
                ex.Message);
        }
    }
    public SipserReduceToSAT3(string instance) : this(new CLIQUE(instance)) { }
    // Builds from the instance with rec recording the construction as it goes (IRecordedReduction).
    public SipserReduceToSAT3(string instance, StepRecorder<BuildDelta> rec) : this(new CLIQUE(instance), rec) { }
    // CLIQUE's own default instance has plain node names, which reduce() cannot invert, so the default is the
    // Sipser-formatted CLIQUE that SipserReduceToCliqueStandard builds from 3SAT's default formula (#767).
    public SipserReduceToSAT3() : this(new SipserReduceToCliqueStandard(new API.Problems.NPComplete.NPC_SAT3.SAT3()).reductionTo) { }

    // --- Methods ---
    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new SipserReduceToSAT3(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }

    public API.Problems.NPComplete.NPC_SAT3.SAT3 reduce() => Build(StepRecorder<BuildDelta>.Off);

    // Construction steps (see IRecordedReduction): one per clause, its nodes read back into the clause they were made from,
    // then Done. The clique graph's edges and K are not read: Sipser's construction made them from the clauses.
    private API.Problems.NPComplete.NPC_SAT3.SAT3 Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();
        // Group nodes back into clauses by their trailing '_<clauseIdx>' suffix,
        // preserving within-clause literal order.
        var clauseMap = new SortedDictionary<int, List<(string Node, string Literal)>>();
        foreach (string node in _reductionFrom.nodes) {
            (string literal, int idx) = SipserNodes.Parse(node);
            if (!clauseMap.ContainsKey(idx))
                clauseMap[idx] = new List<(string, string)>();
            clauseMap[idx].Add((node, literal));
        }

        var clauses = clauseMap.Values
            .Select(lits => "(" + string.Join(" | ", lits.Select(l => l.Literal)) + ")");
        string formula = string.Join(" & ", clauses);
        var sat3 = new API.Problems.NPComplete.NPC_SAT3.SAT3(formula);

        // Each node is the literal it is written as, at its place in the clause (a literal repeated in a clause is its
        // later occurrences, in order).
        var target = sat3.clauses;
        _clauseNodes = new();
        int c = 0;
        foreach (var lits in clauseMap.Values) {
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            var nodes = new List<(string, string, string)>();
            foreach (var (node, literal) in lits) {
                int nth = seen.TryGetValue(literal, out int n) ? n : 0;
                seen[literal] = nth + 1;
                var places = Enumerable.Range(0, target[c].Count).Where(j => target[c][j] == literal).ToList();
                int place = places.Count == 0 ? 0 : places[Math.Min(nth, places.Count - 1)];
                nodes.Add((node, literal, GroupedGraphFrames.LiteralId(c, place)));
            }
            _clauseNodes.Add(nodes);
            c++;
        }

        var groupOf = new List<int>();
        var elementsOf = new List<int[]>();
        for (int i = 0; i < _clauseNodes.Count; i++) {
            var mine = _clauseNodes[i];
            var targetIds = GroupedGraphFrames.ClauseIds(i, target[i].Count).ToList();
            groupOf.Add(gadgets.Count);
            gadgets.Add(new Gadget(GadgetKind.Group, mine.Select(m => m.Node).ToList(), targetIds,
                legacyToIds: GroupedGraphFrames.LegacyIds(targetIds, target)));
            var elements = new List<int>();
            foreach (var (node, _, literalId) in mine) {
                elements.Add(gadgets.Count);
                gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { node }, new List<string> { literalId },
                    legacyToIds: GroupedGraphFrames.LegacyIds([literalId], target)));
            }
            elementsOf.Add(elements.ToArray());
        }

        if (rec.Enabled) {
            var builtVariables = new HashSet<string>(StringComparer.Ordinal);
            var suffixes = clauseMap.Keys.ToList();
            for (int i = 0; i < _clauseNodes.Count; i++) {
                int clause = i;
                var (pieceNodes, pieceEdges) = GroupedGraphFrames.ClausePieces(target, clause, builtVariables);
                string[] sourceIds = _clauseNodes[clause].Select(m => m.Node).ToArray();
                int[] used = new[] { groupOf[clause] }.Concat(elementsOf[clause]).ToArray();
                string[] focus = pieceNodes.Where(n => !n.StartsWith("v:", StringComparison.Ordinal)).ToArray();
                string suffix = "_" + suffixes[clause];
                bool one = sourceIds.Length == 1;
                rec.Accept(
                    () => new BuildDelta { SourceFocus = sourceIds, Nodes = pieceNodes.ToArray(), Edges = BuildEdge.Sorted(pieceEdges), Gadgets = used },
                    () => $"{(one ? "Node" : "Nodes")} {BuildCaptions.Names(sourceIds)} of the clique graph {(one ? "ends" : "all end")} in {suffix}, "
                        + $"so {(one ? "it becomes" : "they become")} clause C{clause + 1} {GroupedGraphFrames.ClauseText(target[clause])}: each node is the literal written before its {suffix}.",
                    focus);
            }
            int k = _reductionFrom.K;
            rec.Done(new BuildDelta(), true,
                $"The formula has {Words.Count(target.Count, "clause")}. Sipser's construction joins two nodes exactly when they come from different clauses "
                + $"and do not contradict, so a clique with a node from every clause picks literals that can all be true at once. The clique graph asks for K = {k}.");
        }
        return sat3;
    }

    public NodeSet EmptyAnswer() => new NodeSet([]);

    // A 3SAT answer is an assignment, never "{}": the target keeps the default (no answer). "{}" as a clique is the empty one,
    // which the Clique verifier judges against K.

    /// <summary>
    /// Backward map (#694): an assignment to a clique. Each clause, in order, gives the node of its first literal the
    /// assignment makes true; nodes of different clauses whose literals are all true cannot contradict, so Sipser's construction
    /// joined them. A clause with no true literal gives no node (a Reject on the clause), and once the clique has K nodes the
    /// rest are left out (any K nodes of a clique are still one). The Done step reports what Clique's verifier says.
    /// </summary>
    public (string Answer, NodeSet Partial) MapBack(string problemToSolution, StepRecorder<NodeSet> rec) {
        var values = SAT.ReadAssignment(problemToSolution, CnfFormula.VariablesOf(reductionTo.clauses), "3SAT");
        int k = reductionFrom.K;
        var clique = new List<string>();
        for (int i = 0; i < _clauseNodes.Count; i++) {
            int clause = i;
            var chosen = _clauseNodes[clause].FirstOrDefault(m => values.TryGetValue(CnfFormula.VariableOf(m.Literal), out bool v) && v == !m.Literal.StartsWith('!'));
            if (chosen.Node == null) {
                rec.Reject(() => new NodeSet(clique),
                    () => $"No literal of clause C{clause + 1} {GroupedGraphFrames.ClauseText(reductionTo.clauses[clause])} is true, so none of its nodes joins the clique.",
                    GroupedGraphFrames.ClauseId(clause));
                continue;
            }
            string variable = CnfFormula.VariableOf(chosen.Literal);
            string why = $"{variable} = {Assignment.Word(values[variable])}, so {chosen.Literal} is true";
            if (clique.Count < k) {
                clique.Add(chosen.Node);
                rec.Accept(() => new NodeSet(clique), () => $"{why} in clause C{clause + 1}: its node {chosen.Node} is in the clique.", chosen.LiteralId);
            } else {
                rec.Reject(() => new NodeSet(clique),
                    () => $"{why} in clause C{clause + 1}, but the clique already has K = {k} nodes, so node {chosen.Node} is left out.", chosen.LiteralId);
            }
        }
        return (ReductionBack.FormatNodeSet(clique), new NodeSet(clique));
    }

    public string mapSolutions(string problemFromSolution) {
        // problemFromSolution is a clique certificate like "{x1_0,x2_1,x1_2}".
        try {
            var assigned = new Dictionary<string, bool>();
            var order = new List<string>();
            foreach (string raw in problemFromSolution.Trim('{', '}', '(', ')', ' ').Split(',')) {
                string node = raw.Trim();
                if (node.Length == 0) continue;
                (string varName, bool positive) = SipserNodes.Assignment(node);
                if (!assigned.ContainsKey(varName)) {
                    assigned[varName] = positive;
                    order.Add(varName);
                }
            }
            var parts = order.Select(v => $"{v}:{(assigned[v] ? "True" : "False")}");
            return "(" + string.Join(",", parts) + ")";
        } catch (ArgumentException ex) {
            throw new ReductionInputException(this, problemFromSolution,
                _reductionFrom.certificateFormat +
                " | Additionally, this reduction requires Sipser-formatted node names of the form '<literal>_<clauseIdx>'.",
                ex.Message);
        }
    }
}
