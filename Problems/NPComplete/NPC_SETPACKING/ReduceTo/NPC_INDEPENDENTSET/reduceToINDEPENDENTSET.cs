using API.Interfaces;
using API.Problems.NPComplete.NPC_INDEPENDENTSET;
using System.Collections.Generic;
using System.Linq;

namespace API.Problems.NPComplete.NPC_SETPACKING.ReduceTo.NPC_INDEPENDENTSET;

class reduceToINDEPENDENTSET : IReduction<SETPACKING, INDEPENDENTSET>
{
    public string reductionName { get; } = "Set Packing to Independent Set";

    public string reductionDefinition { get; } =
        "This reduction converts Set Packing into Independent Set by building a conflict graph. Each set becomes a node, and an edge connects two nodes if their sets overlap.";

    public string source { get; } = "";
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Set_packing";
    public string[] contributors { get; } = { "Sansar Kharal" };

    private string _complexity = "O(n^2)";
    private Dictionary<object, object> _gadgetMap = new Dictionary<object, object>();

    private SETPACKING _reductionFrom;
    private INDEPENDENTSET _reductionTo;

    // --- Properties required by IReduction ---
    public Dictionary<object, object> gadgetMap
    {
        get
        {
            return _gadgetMap;
        }
        set
        {
            _gadgetMap = value;
        }
    }

    public string complexity
    {
        get
        {
            return _complexity;
        }
    }

    public SETPACKING reductionFrom
    {
        get
        {
            return _reductionFrom;
        }
        set
        {
            _reductionFrom = value;
        }
    }

    public INDEPENDENTSET reductionTo
    {
        get
        {
            return _reductionTo;
        }
        set
        {
            _reductionTo = value;
        }
    }

    // --- Constructors ---
    public reduceToINDEPENDENTSET(SETPACKING from)
    {
        _reductionFrom = from;
        _reductionTo = reduce();
    }

    public reduceToINDEPENDENTSET(string from)
        : this(new SETPACKING(from))
    {
    }

    public reduceToINDEPENDENTSET()
        : this(new SETPACKING())
    {
    }

    public INDEPENDENTSET reduce()
    {
        SETPACKING setPackingInstance = _reductionFrom;

        List<string> nodes = setPackingInstance.setNames;
        List<KeyValuePair<string, string>> edges = new List<KeyValuePair<string, string>>();

        // Create conflict edges.
        // Two sets conflict if they share at least one element.
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                string left = nodes[i];
                string right = nodes[j];

                if (setPackingInstance.sets[left].Intersect(setPackingInstance.sets[right]).Any())
                {
                    edges.Add(new KeyValuePair<string, string>(left, right));
                }
            }
        }

        // Map each Set Packing set to the same Independent Set node.
        foreach (string node in nodes)
        {
            gadgetMap[node] = node;
        }

        // Build Independent Set instance string:
        // (({nodes},{edges}),K)
        string nodesString = "";

        foreach (string node in nodes)
        {
            nodesString += node + ",";
        }

        nodesString = nodesString.Trim(',');

        string edgesString = "";

        foreach (KeyValuePair<string, string> edge in edges)
        {
            edgesString += "{" + edge.Key + "," + edge.Value + "},";
        }

        edgesString = edgesString.Trim(',');

        string independentSetInstance =
            "(({" + nodesString + "},{" + edgesString + "})," + setPackingInstance.K.ToString() + ")";

        _reductionTo = new INDEPENDENTSET(independentSetInstance);
        reductionTo = _reductionTo;

        return _reductionTo;
    }

    // --- Solution Mapping ---
    public string mapSolutions(string problemFromSolution)
    {
        return problemFromSolution;
    }
}