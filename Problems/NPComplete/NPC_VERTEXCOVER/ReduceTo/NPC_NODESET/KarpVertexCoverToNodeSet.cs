using API.Interfaces;
using API.Problems.NPComplete.NPC_NODESET;
using API.Problems.NPComplete.NPC_VERTEXCOVER;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.ReduceTo.NPC_NODESET;

class VertexCoverReduction : IReduction<VERTEXCOVER, NODESET>
{

    // --- Fields ---
    private string _reductionName = "Karp Vertex Cover to Node Set Reduction";
    private string _reductionDefinition = "Karp's Reduction from Vertex Cover to Feedback Node Set";
    private string _source = "This reduction was found by the Algorithms Seminar at the Cornell University Computer Science Department. Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string[] _contributors = { "Andrija Sevaljevic" };

    private string _complexity = "";
    private Dictionary<Object, Object> _gadgetMap = new Dictionary<Object, Object>();

    private VERTEXCOVER _reductionFrom;
    private NODESET _reductionTo;


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
    public NODESET reductionTo
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
    public VertexCoverReduction(VERTEXCOVER from)
    {
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public NODESET reduce()
    {
        VERTEXCOVER VERTEXCOVERInstance = _reductionFrom;
        NODESET reducedNODESET = new NODESET();

        string instance = "(({";
        foreach (var node in reductionFrom.nodes)
        {
            instance += node + ',';
        }

        instance = instance.TrimEnd(',') + "},{(";
        foreach (var node in reductionFrom.nodes)
        {
            foreach (var node2 in reductionFrom.nodes)
            {
                KeyValuePair<string, string> pairCheck1 = new KeyValuePair<string, string>(node, node2);
                KeyValuePair<string, string> pairCheck2 = new KeyValuePair<string, string>(node2, node);
                if ((reductionFrom.edges.Contains(pairCheck1) || reductionFrom.edges.Contains(pairCheck1)) && node != node2)
                {
                    instance += node + ',' + node2 + "),(";
                    instance += node2 + ',' + node + "),(";
                }
            }
        }

        instance = instance.TrimEnd('(').TrimEnd(',') +"})," + reductionFrom.K.ToString() + ')';

        reducedNODESET.K = reductionFrom.K;
        reducedNODESET.nodes = reductionFrom.nodes;
        reducedNODESET.instance = instance;

        reductionTo = reducedNODESET;
        return reducedNODESET;
    }

    public string mapSolutions(VERTEXCOVER problemFrom, NODESET problemTo, string problemFromSolution)
    {
        if (true)
        {
            return "Solution is incorect";
        }

        return false.ToString();




    }
}
// return an instance of what you are reducing to