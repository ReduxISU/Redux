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
// output for 3SAT's default formula instead of `new CLIQUE()`. Excluded from
// ReductionGraphData's navigable graph so it isn't advertised as
// a working CLIQUE -> SAT3 reduction for any input; still directly callable by name via
// /ProblemProvider/reduce for its actual purpose (mapping a solution back through a
// paired SipserReduceToCliqueStandard run).
//
// Sipser's Clique to 3SAT reads the formula back out of a Sipser-formatted clique graph: the nodes that end in _i are the
// literals of clause i. It records the construction steps (one per clause, then the Done; see Build) and the backward map's
// steps (one per clause read; see MapBack).
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
    /// The gadgets: per clause, a group (the clique nodes of that clause to the clause and its literals), and per node, an
    /// element (the node to the literal it is written as).
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
    // Sipser-formatted CLIQUE that SipserReduceToCliqueStandard builds from 3SAT's default formula.
    public SipserReduceToSAT3() : this(new SipserReduceToCliqueStandard(new API.Problems.NPComplete.NPC_SAT3.SAT3()).reductionTo) { }

    // --- Methods ---
    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new SipserReduceToSAT3(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }

    public API.Problems.NPComplete.NPC_SAT3.SAT3 reduce() => Build(StepRecorder<BuildDelta>.Off);

    // How the construction steps show a clause: its nodes in the clique graph; the clause, its literals, the variables no
    // earlier clause mentions and the formula picture's edges it brings, added to 3SAT; and its gadgets. The state is
    // (Mine, FirstGadget, Pieces): the clause's clique nodes, the index of its first gadget (its group, then one element per
    // node), and those pieces.
    private static RunSteps<(List<(string Node, string Literal, string LiteralId)> Mine, int FirstGadget, (List<string> Nodes, List<BuildEdge> Edges) Pieces), BuildDelta> NewClauseSteps(StepRecorder<BuildDelta> rec) => new(rec,
        // What the Reduction View shows: the clause's nodes, the pieces added, and the gadgets that made them.
        partial: s => new BuildDelta {
            SourceFocus = s.Mine.Select(m => m.Node).ToArray(),
            Nodes = s.Pieces.Nodes.ToArray(),
            Edges = BuildEdge.Sorted(s.Pieces.Edges),
            Gadgets = Enumerable.Range(s.FirstGadget, 1 + s.Mine.Count).ToArray(),
        },
        // The clause and its literals; the variables are not looked at.
        focus: s => s.Pieces.Nodes.Where(n => !n.StartsWith("v:", StringComparison.Ordinal)).ToArray());

    // Builds the 3SAT formula, recording the construction steps as it goes (see IRecordedReduction). Its steps:
    //   - an Accept per clause: the nodes that end in its suffix become the clause, each node the literal written before it;
    //   - the Done: the formula, and the K the clique graph asks for.
    // A construction has no verdicts, so Accept means "these pieces go into the target". The clique graph's edges and K are
    // not read: Sipser's construction made them from the clauses.
    // Gadgets: per clause a group (its nodes to the clause and its literals), then an element per node (to its literal).
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

        var target = sat3.clauses;
        _clauseNodes = new();
        var clauseSteps = NewClauseSteps(rec);
        // The variables an earlier clause's step already added to the formula picture; made by the first step recorded, since
        // only the picture reads it.
        HashSet<string>? builtVariables = null;
        // Then each clause, in suffix order: its nodes, its gadgets and its step.
        int i = 0;
        foreach (var (suffix, lits) in clauseMap) {
            // Each node is the literal it is written as, at its place in the clause (a literal repeated in a clause is its
            // later occurrences, in order).
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            var mine = new List<(string Node, string Literal, string LiteralId)>();
            foreach (var (node, literal) in lits) {
                int nth = seen.TryGetValue(literal, out int n) ? n : 0;
                seen[literal] = nth + 1;
                var places = Enumerable.Range(0, target[i].Count).Where(j => target[i][j] == literal).ToList();
                int place = places.Count == 0 ? 0 : places[Math.Min(nth, places.Count - 1)];
                mine.Add((node, literal, GroupedGraphFrames.LiteralId(i, place)));
            }
            _clauseNodes.Add(mine);

            var targetIds = GroupedGraphFrames.ClauseIds(i, target[i].Count).ToList();
            int firstGadget = gadgets.Count;
            gadgets.Add(new Gadget(GadgetKind.Group, mine.Select(m => m.Node).ToList(), targetIds,
                legacyToIds: GroupedGraphFrames.LegacyIds(targetIds, target)));
            foreach (var (node, _, literalId) in mine)
                gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { node }, new List<string> { literalId },
                    legacyToIds: GroupedGraphFrames.LegacyIds([literalId], target)));

            // What the clause adds to the formula picture, worked out only when the steps are recorded: finding its edges
            // compares each of its literals with every earlier one, and only the picture needs them.
            var pieces = rec.Enabled ? GroupedGraphFrames.ClausePieces(target, i, builtVariables ??= new(StringComparer.Ordinal)) : default;
            if (mine.Count == 1)
                clauseSteps.Accept((mine, firstGadget, pieces),
                    $"Node {BuildCaptions.Names(mine.Select(m => m.Node).ToList())} of the clique graph ends in _{suffix}, so it becomes clause C{i + 1} {GroupedGraphFrames.ClauseText(target[i])}: each node is the literal written before its _{suffix}.");
            else
                clauseSteps.Accept((mine, firstGadget, pieces),
                    $"Nodes {BuildCaptions.Names(mine.Select(m => m.Node).ToList())} of the clique graph all end in _{suffix}, so they become clause C{i + 1} {GroupedGraphFrames.ClauseText(target[i])}: each node is the literal written before its _{suffix}.");
            i++;
        }

        // The closing step.
        clauseSteps.Done(new BuildDelta(), true,
            $"The formula has {Words.Count(target.Count, "clause")}. Sipser's construction joins two nodes exactly when they come from different clauses and do not contradict, so a clique with a node from every clause picks literals that can all be true at once. The clique graph asks for K = {_reductionFrom.K}.");
        return sat3;
    }

    public NodeSet EmptyAnswer() => new NodeSet([]);

    // A 3SAT answer is an assignment, never "{}": the target keeps the default (no answer). "{}" as a clique is the empty one,
    // which the Clique verifier judges against K.

    // How the map-back steps show a clause read: the clique so far, and the id the step is about (the clause, or the literal
    // whose node is read).
    private static RunSteps<string, NodeSet> NewMapSteps(StepRecorder<NodeSet> rec, List<string> clique) => new(rec,
        // What every picture shows: the clique so far, and the piece the step is about.
        partial: _ => new NodeSet(clique),
        focus: id => [id]);

    // Backward map: an assignment to a clique. Each clause, in order, gives the node of its first literal the assignment makes
    // true; nodes of different clauses whose literals are all true cannot contradict, so Sipser's construction joined them.
    // Once the clique has K nodes the rest are left out (any K nodes of a clique are still one). Clique's verifier decides.
    // Its steps: an Accept per clause whose true literal's node joins the clique, a Reject per clause with no true literal (it
    // gives no node), and a Reject per clause past K (its node is left out). The caller adds the Done.
    public (string Answer, NodeSet Partial) MapBack(string problemToSolution, StepRecorder<NodeSet> rec) {
        var values = SAT.ReadAssignment(problemToSolution, CnfFormula.VariablesOf(reductionTo.clauses), "3SAT");
        int k = reductionFrom.K;
        var clique = new List<string>();
        var steps = NewMapSteps(rec, clique);
        for (int clause = 0; clause < _clauseNodes.Count; clause++) {
            var chosen = _clauseNodes[clause].FirstOrDefault(m => values.TryGetValue(CnfFormula.VariableOf(m.Literal), out bool v) && v == !m.Literal.StartsWith('!'));
            if (chosen.Node == null) {
                steps.Reject(GroupedGraphFrames.ClauseId(clause),
                    $"No literal of clause C{clause + 1} {GroupedGraphFrames.ClauseText(reductionTo.clauses[clause])} is true, so none of its nodes joins the clique.");
                continue;
            }
            string variable = CnfFormula.VariableOf(chosen.Literal);
            if (clique.Count < k) {
                clique.Add(chosen.Node);
                steps.Accept(chosen.LiteralId,
                    $"{variable} = {Assignment.Word(values[variable])}, so {chosen.Literal} is true in clause C{clause + 1}: its node {chosen.Node} is in the clique.");
            } else {
                steps.Reject(chosen.LiteralId,
                    $"{variable} = {Assignment.Word(values[variable])}, so {chosen.Literal} is true in clause C{clause + 1}, but the clique already has K = {k} nodes, so node {chosen.Node} is left out.");
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
