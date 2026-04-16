using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPComplete.NPC_INDEPENDENTSET;
using SPADE;
using System.Collections.Generic;
using System.Linq;

namespace API.Problems.NPComplete.NPC_SETPACKING.ReduceTo.NPC_INDEPENDENTSET;

class reduceToINDEPENDENTSET : IReduction<SETPACKING, INDEPENDENTSET>
{
    // --- Fields ---
    public string reductionName { get; } = "Set Packing to Independent Set";

    public string reductionDefinition { get; } =
        "Transforms Set Packing into Independent Set by mapping each set to a node and adding edges between overlapping sets.";

    public string source { get; } = "Karp-style reduction";
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Set_packing";
    public string[] contributors { get; } = { "Sansar Kharal" };

    private string _complexity = "O(n^2)";
    private Dictionary<object, object> _gadgetMap = new Dictionary<object, object>();

    private SETPACKING _reductionFrom;
    private INDEPENDENTSET _reductionTo;

    // --- Properties ---
    public Dictionary<object, object> gadgetMap
    {
        get { return _gadgetMap; }
        set { _gadgetMap = value; }
    }

    public string complexity
    {
        get { return _complexity; }
    }

    public SETPACKING reductionFrom
    {
        get { return _reductionFrom; }
        set { _reductionFrom = value; }
    }

    public INDEPENDENTSET reductionTo
    {
        get { return _reductionTo; }
        set { _reductionTo = value; }
    }

    // --- Constructors ---
    public reduceToINDEPENDENTSET(SETPACKING from)
    {
        _reductionFrom = from;
        _reductionTo = reduce();
    }

    public reduceToINDEPENDENTSET(string from)
        : this(new SETPACKING(from)) { }

    public reduceToINDEPENDENTSET()
        : this(new SETPACKING()) { }

    // --- Reduction Logic ---
    public INDEPENDENTSET reduce()
    {
        SETPACKING sp = _reductionFrom;

        List<string> nodes = sp.setNames;
        List<KeyValuePair<string, string>> edges = new();

        // Create edges for overlapping sets
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                var s1 = sp.sets[nodes[i]];
                var s2 = sp.sets[nodes[j]];

                if (s1.Intersect(s2).Any())
                {
                    edges.Add(new KeyValuePair<string, string>(nodes[i], nodes[j]));
                }
            }
        }

        // Map gadgets (optional)
        foreach (var node in nodes)
        {
            gadgetMap[node] = node;
        }

        // Build graph string
        string nodeStr = string.Join(",", nodes);

        string edgeStr = "";
        foreach (var e in edges)
        {
            edgeStr += "{" + e.Key + "," + e.Value + "},";
        }
        edgeStr = edgeStr.TrimEnd(',');

        string G = "(({" + nodeStr + "},{" + edgeStr + "})," + sp.K + ")";

        INDEPENDENTSET result = new INDEPENDENTSET(G);
        reductionTo = result;

        return result;
    }

    // --- Solution Mapping ---
    public string mapSolutions(string problemFromSolution)
    {
        return problemFromSolution;
    }
}