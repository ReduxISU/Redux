using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_CLIQUECOVER;
using SPADE;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo.NPC_CLIQUECOVER;

class GraphColoringToCliqueCover : IReduction<GRAPHCOLORING, CLIQUECOVER>
{

    // --- Fields ---
    public string reductionName {get;} = "Clique Cover Reduction";
    public string reductionDefinition {get;} = "Karp's Reduction from Graph Coloring to Clique Cover";
    public string source {get;} = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors {get;} = { "Andrija Sevaljevic" };

    private string _complexity = "";
    public List<Gadget> gadgets { get; }
    private GRAPHCOLORING _reductionFrom;
    private CLIQUECOVER _reductionTo;


    // --- Properties ---
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
    public GraphColoringToCliqueCover(GRAPHCOLORING from)
    {
        gadgets = new();
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public GraphColoringToCliqueCover(string instance) : this(new GRAPHCOLORING(instance)) { }
    public GraphColoringToCliqueCover() : this(new GRAPHCOLORING()) { }
    public CLIQUECOVER reduce()
    {
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

        foreach (UtilCollection node in reductionFrom.graph.Nodes)
        {
            gadgets.Add(new Gadget("ElementHighlight", new List<string>() { node.ToString() }, new List<string>() { node.ToString() }));
        }
        // --- Generate G string for new CLIQUE ---

        instance = instance.TrimEnd('{').TrimEnd(',') +"})," + reductionFrom.K.ToString() + ')';

        reducedCLIQUECOVER.K = reductionFrom.K;
        reducedCLIQUECOVER.nodes = reductionFrom.nodes;
        reducedCLIQUECOVER.instance = instance;

        reductionTo = reducedCLIQUECOVER;
        return reducedCLIQUECOVER;
    }

    public string mapSolutions(string problemFromSolution)
    {
        return "";
    }
}
// return an instance of what you are reducing to