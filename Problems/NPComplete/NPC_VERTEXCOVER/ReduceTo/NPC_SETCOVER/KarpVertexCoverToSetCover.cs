using API.Interfaces;
using API.Problems.NPComplete.NPC_SETCOVER;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.ReduceTo.NPC_SETCOVER;

class KarpVertexCoverToSetCover : IReduction<VERTEXCOVER, SETCOVER>
{

    // --- Fields ---
    private string _reductionName = "Karp's Clique to Set Cover Reduction";
    private string _reductionDefinition = "";
    private string _source = "";
    private string[] _contributors = { "Caleb Eardley" };
    private Dictionary<Object, Object> _gadgetMap = new Dictionary<Object, Object>();

    private VERTEXCOVER _reductionFrom;
    private SETCOVER _reductionTo;


    // --- Properties ---
    public string reductionName
    {
        get
        {
            return _reductionName;
        }
    }
    public string reductionDefinition
    {
        get
        {
            return _reductionDefinition;
        }
    }
    public string source
    {
        get
        {
            return _source;
        }
    }
    public string[] contributors
    {
        get
        {
            return _contributors;
        }
    }
    public Dictionary<Object, Object> gadgetMap
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
    public VERTEXCOVER reductionFrom
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
    public SETCOVER reductionTo
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

    // --- Methods Including Constructors ---
    public KarpVertexCoverToSetCover(VERTEXCOVER from)
    {
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    /***************************************************
     * reduce() called after GareyAndJohnsonReduction reduction, and returns a THREE_DM object, that
     * is a reduction from the VERTEXCOVER object passed into GareyAndJohnsonReduction.
     */
    public SETCOVER reduce()
    {
        VERTEXCOVER VERTEXCOVERInstance = _reductionFrom;
        SETCOVER reducedSetCover = new SETCOVER();

        List<List<string>> subsets = new List<List<string>>();
        List<string> universal = new List<string>();

        for (int i = 0; i < reductionFrom.nodes.Count; i++)
        {
            subsets.Add(new List<string>());
            foreach (var j in reductionFrom.edges)
            {
                if (j.Key == reductionFrom.nodes[i] || j.Value == reductionFrom.nodes[i])
                {
                    subsets[i].Add(j.Key + "_" + j.Value);
                    if(!universal.Contains(j.Key + "_" + j.Value)) universal.Add(j.Key + "_" + j.Value);
                }
            }
        }

        string instance = "{{";

        foreach (var i in universal)
        {
            instance += i + ",";
        }
        instance = instance.TrimEnd(',') + "},{{";

        foreach (var i in subsets)
        {
            foreach (var j in i)
            {
                instance += j + ",";
            }
            instance = instance.TrimEnd(',') + "},{";
            if (i.Count == 0) instance = instance.TrimEnd('{').TrimEnd(',').TrimEnd('}');
        }
        instance = instance.TrimEnd('{').TrimEnd(',') + "}," + reductionFrom.K + "}";

        reducedSetCover.universal = universal;
        reducedSetCover.subsets = subsets;
        reducedSetCover.instance = instance;

        //return new THREE_DM();
        return reducedSetCover;
    }

    public string mapSolutions(VERTEXCOVER problemFrom, SETCOVER problemTo, string problemFromSolution)
    {
        return "No mapping";
    }
}
