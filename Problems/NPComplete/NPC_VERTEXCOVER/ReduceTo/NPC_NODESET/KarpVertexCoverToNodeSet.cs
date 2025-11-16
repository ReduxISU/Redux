using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_NODESET;
using API.Problems.NPComplete.NPC_VERTEXCOVER;
using SPADE;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.ReduceTo.NPC_NODESET;

class KarpVertexCoverToNodeSet : IReduction<VERTEXCOVER, NODESET>
{

    // --- Fields ---
    public string reductionName {get;} = "Karp Vertex Cover to Node Set Reduction";
    public string reductionDefinition {get;} = "Karp's Reduction from Vertex Cover to Feedback Node Set";
    public string source {get;} = "This reduction was found by the Algorithms Seminar at the Cornell University Computer Science Department. Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string[] contributors {get;} = { "Andrija Sevaljevic" };

    private string _complexity = "";
    public List<Gadget> gadgets { get; }
    private VERTEXCOVER _reductionFrom;
    private NODESET _reductionTo;


    // --- Properties ---
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
    public KarpVertexCoverToNodeSet(VERTEXCOVER from)
    {
        gadgets = new();
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public KarpVertexCoverToNodeSet(string instance) : this(new VERTEXCOVER(instance)) { }
    public KarpVertexCoverToNodeSet() : this(new VERTEXCOVER()) { }
    public NODESET reduce()
    {
        VERTEXCOVER VERTEXCOVERInstance = _reductionFrom;
        NODESET reducedNODESET = new NODESET();

        string instance = "(({";
        foreach (var node in reductionFrom.nodes)
        {
            instance += node + ',';
        }

        foreach (UtilCollection node in VERTEXCOVERInstance.graph.Nodes)
        {
            gadgets.Add(new Gadget("ElementHighlight", new List<string>() { node.ToString() }, new List<string>() { node.ToString() }));
        }
        // --- Generate G string for new CLIQUE ---

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

    public string mapSolutions(string problemFromSolution)
    {
        return "";
    }
}
// return an instance of what you are reducing to