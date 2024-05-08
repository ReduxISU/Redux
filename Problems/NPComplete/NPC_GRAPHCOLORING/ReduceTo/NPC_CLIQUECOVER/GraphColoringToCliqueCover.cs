using API.Interfaces;
using API.Problems.NPComplete.NPC_CLIQUECOVER;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo.NPC_CLIQUECOVER;

class CliqueCoverReduction : IReduction<GRAPHCOLORING, CLIQUECOVER>
{

    // --- Fields ---
    private string _reductionName = "Clique Cover Reduction";
    private string _reductionDefinition = "Karp's Reduction from Graph Coloring to Clique Cover";
    private string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string[] _contributors = { "Andrija Sevaljevic" };

    private string _complexity = "";
    private Dictionary<Object, Object> _gadgetMap = new Dictionary<Object, Object>();

    private GRAPHCOLORING _reductionFrom;
    private CLIQUECOVER _reductionTo;


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
    public GRAPHCOLORING reductionFrom
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
    public CLIQUECOVER reductionTo
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
    public CliqueCoverReduction(GRAPHCOLORING from)
    {
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public CLIQUECOVER reduce()
    {
        GRAPHCOLORING GRAPHCOLORINGInstance = _reductionFrom;
        CLIQUECOVER reducedCLIQUECOVER = new CLIQUECOVER();

        string instance = "(({";
        foreach (var node in reductionFrom.nodes)
        {
            instance += node + ',';
        }

        instance = instance.TrimEnd(',') + "},{{";
        foreach (var node in reductionFrom.nodes)
        {
            foreach (var node2 in reductionFrom.nodes)
            {
                KeyValuePair<string, string> pairCheck1 = new KeyValuePair<string, string>(node, node2);
                KeyValuePair<string, string> pairCheck2 = new KeyValuePair<string, string>(node2, node);
                if (!(reductionFrom.edges.Contains(pairCheck1) || reductionFrom.edges.Contains(pairCheck1)) && node != node2)
                {
                    instance += node + ',' + node2 + "},{";
                }
            }
        }

        instance = instance.TrimEnd('{').TrimEnd(',') +"})," + reductionFrom.K.ToString() + ')';

        reducedCLIQUECOVER.K = reductionFrom.K;
        reducedCLIQUECOVER.nodes = reductionFrom.nodes;
        reducedCLIQUECOVER.instance = instance;

        reductionTo = reducedCLIQUECOVER;
        return reducedCLIQUECOVER;
    }

    public string mapSolutions(GRAPHCOLORING problemFrom, CLIQUECOVER problemTo, string problemFromSolution)
    {
        if (!problemFrom.defaultVerifier.verify(problemFrom, problemFromSolution))
        {
            return "Solution is incorect";
        }

        return false.ToString();




    }
}
// return an instance of what you are reducing to