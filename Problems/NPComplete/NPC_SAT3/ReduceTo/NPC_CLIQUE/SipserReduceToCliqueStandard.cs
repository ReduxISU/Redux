using API.Interfaces;
using API.Problems.NPComplete.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_SAT3;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using SPADE;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_CLIQUE;

class SipserReduceToCliqueStandard : IReduction<SAT3, CLIQUE> {

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
    public SipserReduceToCliqueStandard(SAT3 from) {
        gadgets = new();
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public SipserReduceToCliqueStandard(string instance) : this(new SAT3(instance)) { }
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

    public CLIQUE reduce() {
        UtilCollection nodes = new("{}");
        UtilCollection edges = new("{}");
        for (int i = 0; i < reductionFrom.clauses.Count; i++) {
            List<string> nodesInClause = new();
            Dictionary<string, int> occurrences = new();
            for (int j = 0; j < reductionFrom.clauses[i].Count; j++) {
                string literal = reductionFrom.clauses[i][j];
                occurrences.TryGetValue(literal, out int earlier);
                occurrences[literal] = earlier + 1;
                string nodeName = literal + new string(OccurrenceMarker, earlier) + "_" + i;
                nodes.Add(new UtilCollection(nodeName));

                gadgets.Add(new Gadget("ElementHighlight", new List<string>() { i + "-" + j }, new List<string> { nodeName }));
                nodesInClause.Add(nodeName);
            }
            gadgets.Add(new Gadget("ClauseHighlight", new List<string>() { i.ToString() }, nodesInClause));
        }

        foreach (UtilCollection node1 in nodes)
            foreach (UtilCollection node2 in nodes) {
                if (node1.Equals(node2)) continue;
                if (fromSameClause(node1, node2)) continue; //no edges between nodes of the same clause
                if (isInverse(node1, node2)) continue; //no edges between literals that are always opposite of eachother

                UtilCollection edge = new("{}");
                edge.Add(node1);
                edge.Add(node2);
                edges.Add(edge);
            }
        reductionTo = new CLIQUE($"(({nodes},{edges}),{reductionFrom.clauses.Count})");

        return reductionTo;

    }
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

}