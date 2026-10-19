using API.Interfaces;
using API.Problems.NPComplete.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_SAT3;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_CLIQUE.ReduceTo.NPC_SAT3;
using SPADE;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_CLIQUE;

class SipserReduceToCliqueStandard : IReversibleReduction<SAT3, CLIQUE, Assignment>, IRecordedReduction<SAT3, CLIQUE> {

    // --- Fields ---
    public string reductionName { get; } = "Sipser's Clique Reduction";
    public string reductionDefinition { get; } = "Sipsers reduction converts clauses from 3SAT into clusters of nodes in a graph for which CLIQUES exist";
    public string source { get; } = "Sipser, Michael. Introduction to the Theory of Computation.ACM Sigact News 27.1 (1996): 27-29.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Kaden Marchetti", "Alex Diviney", "Caleb Eardley", "Russell Phillips" };
    // reduce()'s double loop over all literal-node pairs (3 nodes per clause) adds an
    // edge for almost every pair (excluding same-clause / inverse-literal pairs) — O(n^2)
    // edges from O(n) literal-nodes.
    public ReductionCost cost { get; } = ReductionCost.Quadratic;
    // Declared, not derived. Literal-nodes are grouped into per-clause triangles whose
    // edges depend on cross-clause same-literal/inverse-literal checks -- the textbook
    // Garey & Johnson component-design example for 3SAT-to-Clique.
    public ReductionType reductionType { get; } = ReductionType.ComponentDesign;
    // Declared, not derived. Double loop over all literal-node pairs.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Polynomial;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(n^2), n = 3 * |SAT3.clauses| (literal-nodes)";
    private SAT3 _reductionFrom;
    private CLIQUE _reductionTo;


    // --- Properties ---
    public List<Gadget> gadgets { get; set; }
    public SAT3 reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public CLIQUE reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }

    // --- Methods Including Constructors ---
    public SipserReduceToCliqueStandard(SAT3 from) : this(from, StepRecorder<BuildDelta>.Off) { }

    // The one place the target is built; rec is Off except when Rebuild asks for the construction steps.
    private SipserReduceToCliqueStandard(SAT3 from, StepRecorder<BuildDelta> rec) {
        gadgets = new();
        _reductionFrom = from;
        _reductionTo = Build(rec);
    }

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new SipserReduceToCliqueStandard(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }
    public SipserReduceToCliqueStandard(string instance) : this(new SAT3(instance)) { }
    // Builds from the instance with rec recording the construction as it goes (IRecordedReduction): the Reduction View
    // and /buildSteps use it so that one build gives both the reduction and its steps.
    public SipserReduceToCliqueStandard(string instance, StepRecorder<BuildDelta> rec) : this(new SAT3(instance), rec) { }
    public SipserReduceToCliqueStandard() : this(new SAT3()) { }

    private bool fromSameClause(UtilCollection node1, UtilCollection node2) {
        List<string> node1List = node1.ToString().Split("_").ToList();
        List<string> node2List = node2.ToString().Split("_").ToList();
        return node1List[node1List.Count - 1] == node2List[node2List.Count - 1]; // node names may contain underscores, but clause number will always be the very last
    }
    // A literal that appears more than once in the same clause gets one apostrophe per earlier
    // occurrence (x1, x1', x1'', ...) so each occurrence is its own node; the node set would
    // otherwise silently merge them. The marker sits before the "_<clause>" suffix, so the clause
    // number is still the last "_" segment.
    private const char OccurrenceMarker = '\'';
    private static string removeOccurrenceMarker(string literal) {
        return literal.TrimEnd(OccurrenceMarker);
    }
    private string removeClauseNumber(string s) {

        int underscore = s.LastIndexOf('_');
        if (underscore == -1)
            return s;

        return s.Substring(0, underscore);
    }
    private bool isSameLiteral(UtilCollection node1, UtilCollection node2) {
        string coreA = removeOccurrenceMarker(removeClauseNumber(node1.ToString()));
        if (coreA.StartsWith("!"))
            coreA = coreA.Substring(1);
        string coreB = removeOccurrenceMarker(removeClauseNumber(node2.ToString()));
        if (coreB.StartsWith("!"))
            coreB = coreB.Substring(1);
        return coreA == coreB;
    }

    private bool isInverse(UtilCollection node1, UtilCollection node2) {
        if (!isSameLiteral(node1, node2)) return false;
        return node1.ToString()[0] == '!' ^ node2.ToString()[0] == '!';
    }

    public CLIQUE reduce() => Build(StepRecorder<BuildDelta>.Off);

    // Construction steps (see IRecordedReduction): one per clause (its literal nodes), one per pair of
    // clauses (the edges between them, and the contradictory pairs left without one), then K. They are
    // collected from the very decisions below, so what is recorded is what is built.
    // Gadgets: an element per literal and a group per clause, then (#908) the edge rules (one per clause: its nodes are
    // never joined; one per pair of nodes from different clauses: joined, or left out when they contradict), then K.
    private CLIQUE Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();
        UtilCollection nodes = new("{}");
        UtilCollection edges = new("{}");
        bool recording = rec.Enabled;
        int clauseCount = reductionFrom.clauses.Count;
        List<string[]> clauseNodes = new();
        // The literal id ("c{i}-{j}") each node stands for, so an edge rule names the two literals of its pair.
        var literalIdOf = new Dictionary<string, string>(StringComparer.Ordinal);
        // The same-clause rules come right after the elements and groups: one element per literal and one group per clause.
        int sameClauseRules = reductionFrom.clauses.Sum(c => c.Count) + clauseCount;
        for (int i = 0; i < reductionFrom.clauses.Count; i++) {
            List<string> nodesInClause = new();
            int firstGadget = gadgets.Count;
            Dictionary<string, int> occurrences = new();
            for (int j = 0; j < reductionFrom.clauses[i].Count; j++) {
                string literal = reductionFrom.clauses[i][j];
                occurrences.TryGetValue(literal, out int earlier);
                occurrences[literal] = earlier + 1;
                string nodeName = literal + new string(OccurrenceMarker, earlier) + "_" + i;
                nodes.Add(new UtilCollection(nodeName));

                gadgets.Add(new Gadget(GadgetKind.Element, new List<string>() { "c" + i + "-" + j }, new List<string> { nodeName }));
                literalIdOf[nodeName] = "c" + i + "-" + j;
                nodesInClause.Add(nodeName);
            }
            gadgets.Add(new Gadget(GadgetKind.Group, new List<string>() { "c" + i }, nodesInClause));
            clauseNodes.Add(nodesInClause.Distinct().ToArray());

            if (recording) {
                int clause = i;
                string[] clauseNames = clauseNodes[clause];
                string[] sourceIds = new[] { "c" + clause }
                    .Concat(Enumerable.Range(0, reductionFrom.clauses[clause].Count).Select(j => "c" + clause + "-" + j)).ToArray();
                int[] gadgetIdx = Enumerable.Range(firstGadget, gadgets.Count - firstGadget).Append(sameClauseRules + clause).ToArray();
                rec.Accept(
                    () => new BuildDelta { SourceFocus = sourceIds, Nodes = clauseNames, Gadgets = gadgetIdx },
                    () => $"Clause C{clause + 1} ({string.Join(" | ", reductionFrom.clauses[clause])}) becomes nodes {BuildCaptions.Names(clauseNames)}, with no edges between them.",
                    clauseNames);
            }
        }

        // The edges and the contradictory pairs, bucketed by the pair of clauses they join (the edge rules and the steps use them).
        SortedDictionary<(int, int), (List<(string A, string B)> Edges, List<(string A, string B)> Skipped)> byPair = new();
        foreach (UtilCollection node1 in nodes)
            foreach (UtilCollection node2 in nodes) {
                if (node1.Equals(node2)) continue;
                if (fromSameClause(node1, node2)) continue; //no edges between nodes of the same clause
                bool inverse = isInverse(node1, node2);
                int c1 = clauseOf(node1.ToString()), c2 = clauseOf(node2.ToString());
                // Every pair of nodes is visited twice; keep the visit that matches the edge's direction in the target.
                if (c1 < c2) {
                    if (!byPair.TryGetValue((c1, c2), out var bucket)) byPair[(c1, c2)] = bucket = (new(), new());
                    (inverse ? bucket.Skipped : bucket.Edges).Add((node1.ToString(), node2.ToString()));
                }
                if (inverse) continue; //no edges between literals that are always opposite of eachother

                UtilCollection edge = new("{}");
                edge.Add(node1);
                edge.Add(node2);
                edges.Add(edge);
            }
        reductionTo = new CLIQUE($"(({nodes},{edges}),{reductionFrom.clauses.Count})");

        // The pair rule (#908). One edge rule per clause for the pairs inside it, which are never joined: from the clause to its nodes.
        for (int i = 0; i < clauseCount; i++)
            gadgets.Add(new Gadget(GadgetKind.EdgeRule, new List<string> { "c" + i }, clauseNodes[i].ToList(),
                note: $"Nodes from the same clause, C{i + 1}, are never joined."));
        // Then one per pair of nodes from different clauses, from its two literals: to the edge between them when they are
        // joined, or to the two nodes when they contradict and are left unjoined (that pair has no edge of its own to name).
        // As many as the target has edges and missing pairs between clauses, like Weighted Cut's rule per pair of numbers.
        List<string> LiteralIds(string a, string b) => new List<string> { literalIdOf[a], literalIdOf[b] };
        var pairRules = new Dictionary<(int, int), int[]>();
        foreach (var ((c1, c2), bucket) in byPair) {
            var rules = new List<int>();
            foreach (var (a, b) in bucket.Edges.OrderBy(e => new BuildEdge(e.A, e.B).Id, StringComparer.Ordinal)) {
                rules.Add(gadgets.Count);
                gadgets.Add(new Gadget(GadgetKind.EdgeRule, LiteralIds(a, b), new List<string> { new BuildEdge(a, b).Id },
                    note: $"{a} and {b} are from different clauses and do not contradict each other, so they are joined."));
            }
            foreach (var (a, b) in bucket.Skipped.OrderBy(e => new BuildEdge(e.A, e.B).Id, StringComparer.Ordinal)) {
                rules.Add(gadgets.Count);
                gadgets.Add(new Gadget(GadgetKind.EdgeRule, LiteralIds(a, b), new List<string> { a, b },
                    note: $"{a} and {b} contradict each other, so they are not joined."));
            }
            pairRules[(c1, c2)] = rules.ToArray();
        }
        // K, from the whole formula: one node per clause. The clique graph has no piece of its own for K.
        int boundGadget = gadgets.Count;
        var clauseIds = Enumerable.Range(0, clauseCount).Select(i => "c" + i).ToList();
        gadgets.Add(new Gadget(GadgetKind.Bound, clauseIds, new List<string>(), note: $"K = {clauseCount}, the number of clauses"));

        if (recording) {
            foreach (var ((c1, c2), bucket) in byPair) {
                string[] focus = clauseNodes[c1].Concat(clauseNodes[c2]).ToArray();
                BuildEdge[] added = BuildEdge.Sorted(bucket.Edges.Select(e => new BuildEdge(e.A, e.B)));
                BuildEdge[] skipped = BuildEdge.Sorted(bucket.Skipped.Select(e => new BuildEdge(e.A, e.B)));
                var contradictions = bucket.Skipped;
                int[] rules = pairRules[(c1, c2)];
                Func<BuildDelta> delta = () => new BuildDelta { SourceFocus = new[] { "c" + c1, "c" + c2 }, Edges = added, Skipped = skipped, Gadgets = rules };
                Func<string> caption = () => $"Join clauses C{c1 + 1} and C{c2 + 1}: {BuildCaptions.Count(added.Length, "edge")}."
                    + string.Concat(contradictions.Select(e => $" {e.A} and {e.B} contradict each other, so no edge."));
                if (added.Length > 0) rec.Accept(delta, caption, focus); else rec.Reject(delta, caption, focus);
            }
            int k = reductionFrom.clauses.Count;
            rec.Done(new BuildDelta { SourceFocus = clauseIds.ToArray(), Bound = k, Gadgets = [boundGadget] }, true,
                $"K = {k}, one node per clause: a clique of {k} nodes has to take one node from every clause.");
        }
        return reductionTo;
    }

    // Node names are '<literal>_<clauseIdx>' and the clause number is always the very last part.
    private static int clauseOf(string nodeName) => int.Parse(nodeName[(nodeName.LastIndexOf('_') + 1)..]);
    private bool alreadyContainsNodeFromClause(string node, List<string> potentialNodes) {
        foreach (string selNode in potentialNodes) {
            List<string> selNodeSplit = selNode.Split("_").ToList();
            List<string> nodeSplit = node.Split("_").ToList();
            if (selNodeSplit[selNodeSplit.Count - 1] == nodeSplit[nodeSplit.Count - 1]) {
                return true;
            }
        }
        return false;
    }
    public string mapSolutions(string solution) {
        List<string> items = solution.TrimStart('(').TrimEnd(')').Split(",").ToList();
        HashSet<string> trueLiterals = new();
        foreach (string item in items) {
            List<string> split = item.Split(":").ToList();
            if (split.Count < 2) {
                throw new ReductionInputException(this, solution,
                    reductionFrom.certificateFormat,
                    $"assignment '{item}' is missing a ':' separator");
            }
            if (split[1] == "True")
                trueLiterals.Add(split[0]);
            else
                trueLiterals.Add("!" + split[0]);
        }

        List<string> potentialNodes = new();
        foreach (string node in reductionTo.nodes) {
            if (trueLiterals.Contains(removeOccurrenceMarker(removeClauseNumber(node)))) {
                if (alreadyContainsNodeFromClause(node, potentialNodes))
                    continue;
                potentialNodes.Add(node);
            }
        }

        string mappedSol = "{";
        foreach (string node in potentialNodes)
            mappedSol += node + ",";
        return mappedSol.TrimEnd(',') + "}";
    }

    public Assignment EmptyAnswer() => new Assignment([]);

    // "{}" as B's answer is a real (empty) clique only when K, the number of clauses, is 0, which CliqueVerifier
    // decides; A's answer is an assignment, never "{}". Both keep the default.

    // Backward map: the clique picks one literal node per clause, and making those literals true
    // satisfies every clause. Nodes are named '<literal>_<clauseIdx>' (see SipserNodes). A variable the
    // clique never mentions is free, so it is set to False (any value works; False is just the default).
    // If B's answer contains both x and !x (not a real clique: those nodes share no edge) the first one
    // wins and the later node is rejected; the Done step then reports what A's verifier says.
    // No K cap: extra nodes only add true literals, which cannot unsatisfy a clause.
    public (string Answer, Assignment Partial) MapBack(string problemToSolution, StepRecorder<Assignment> rec) {
        List<string> cliqueNodes = ReductionBack.ParseNodeSet(problemToSolution, reductionTo.nodes);

        var values = new Dictionary<string, string>();
        Assignment Snapshot() => new Assignment(values);

        foreach (string node in cliqueNodes) {
            (string variable, bool value) = SipserNodes.Assignment(node);
            string word = value ? "True" : "False";
            if (!values.TryGetValue(variable, out string? already)) {
                values[variable] = word;
                rec.Accept(Snapshot, () => $"Node {node} is in the clique, so {variable} = {word}.", node);
            } else if (already != word) {
                rec.Reject(Snapshot, () => $"Node {node} would make {variable} = {word}, but {variable} is already {already}, so it is ignored.", node);
            }
        }

        // Variables in the order the formula first mentions them.
        var order = new List<string>();
        foreach (string literal in reductionFrom.literals) {
            string variable = literal.StartsWith("!") ? literal.Substring(1) : literal;
            if (!order.Contains(variable)) order.Add(variable);
        }
        foreach (string variable in order) {
            if (values.ContainsKey(variable)) continue;
            values[variable] = "False";
            // No clique node stands for the variable, so the step focuses the 3SAT picture's own id for it (see MapBack).
            rec.Accept(Snapshot, () => $"No clique node mentions {variable}, so it can be anything: {variable} = False.",
                GroupedGraphFrames.VariableId(variable));
        }

        string answer = "(" + string.Join(",", order.Select(v => $"{v}:{values[v]}")) + ")";
        return (answer, Snapshot());
    }

}