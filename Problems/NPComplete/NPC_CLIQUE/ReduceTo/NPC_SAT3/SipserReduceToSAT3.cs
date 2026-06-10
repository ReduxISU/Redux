using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_SAT3;

namespace API.Problems.NPComplete.NPC_CLIQUE.ReduceTo.NPC_SAT3;

class SipserReduceToSAT3 : IReduction<CLIQUE, API.Problems.NPComplete.NPC_SAT3.SAT3>
{
    // --- Fields ---
    public string reductionName { get; } = "Sipser's Inverse Clique-to-3SAT Reduction";
    public string reductionDefinition { get; } =
        "Inverse of SipserReduceToCliqueStandard. Reconstructs the 3SAT formula from a "
        + "Sipser-formatted CLIQUE instance (nodes named '<literal>_<clauseIdx>') and "
        + "maps a clique certificate back to a 3SAT assignment. Only meaningful for "
        + "CLIQUE instances produced by SipserReduceToCliqueStandard.";
    public string source { get; } = "Sipser, Michael. Introduction to the Theory of Computation. ACM Sigact News 27.1 (1996): 27-29.";
    public string[] contributors { get; } = { "Jason Wright" };

    public List<Gadget> gadgets { get; set; }

    private CLIQUE _reductionFrom;
    private API.Problems.NPComplete.NPC_SAT3.SAT3 _reductionTo;

    // --- Properties ---
    public CLIQUE reductionFrom
    {
        get { return _reductionFrom; }
        set { _reductionFrom = value; }
    }
    public API.Problems.NPComplete.NPC_SAT3.SAT3 reductionTo
    {
        get { return _reductionTo; }
        set { _reductionTo = value; }
    }

    // --- Constructors ---
    public SipserReduceToSAT3(CLIQUE from)
    {
        gadgets = new();
        _reductionFrom = from;
        _reductionTo = reduce();
    }
    public SipserReduceToSAT3(string instance) : this(new CLIQUE(instance)) { }
    public SipserReduceToSAT3() : this(new CLIQUE()) { }

    // --- Methods ---
    public API.Problems.NPComplete.NPC_SAT3.SAT3 reduce()
    {
        // Group nodes back into clauses by their trailing '_<clauseIdx>' suffix,
        // preserving within-clause literal order.
        var clauseMap = new SortedDictionary<int, List<string>>();
        foreach (string node in _reductionFrom.nodes)
        {
            (string literal, int idx) = ParseSipserNode(node);
            if (!clauseMap.ContainsKey(idx))
                clauseMap[idx] = new List<string>();
            clauseMap[idx].Add(literal);
        }

        var clauses = clauseMap.Values
            .Select(lits => "(" + string.Join(" | ", lits) + ")");
        string formula = string.Join(" & ", clauses);
        return new API.Problems.NPComplete.NPC_SAT3.SAT3(formula);
    }

    public string mapSolutions(string problemFromSolution)
    {
        // problemFromSolution is a clique certificate like "{x1_0,x2_1,x1_2}".
        var assigned = new Dictionary<string, bool>();
        var order = new List<string>();
        foreach (string raw in problemFromSolution.Trim('{', '}', '(', ')', ' ').Split(','))
        {
            string node = raw.Trim();
            if (node.Length == 0) continue;
            (string literal, int _) = ParseSipserNode(node);
            bool positive = !literal.StartsWith("!");
            string varName = positive ? literal : literal.Substring(1);
            if (!assigned.ContainsKey(varName))
            {
                assigned[varName] = positive;
                order.Add(varName);
            }
        }
        var parts = order.Select(v => $"{v}:{(assigned[v] ? "True" : "False")}");
        return "(" + string.Join(",", parts) + ")";
    }

    private static (string literal, int clauseIdx) ParseSipserNode(string node)
    {
        int u = node.LastIndexOf('_');
        if (u < 0)
            throw new ArgumentException(
                $"Node '{node}' is not Sipser-formatted: expected '<literal>_<clauseIdx>'");
        if (!int.TryParse(node.Substring(u + 1), out int idx))
            throw new ArgumentException(
                $"Node '{node}' is not Sipser-formatted: clause index '{node.Substring(u + 1)}' is not an integer");
        return (node.Substring(0, u), idx);
    }
}
