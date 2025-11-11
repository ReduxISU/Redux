using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPComplete.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_INDEPENDENTSET;
using SPADE;

namespace API.Problems.NPComplete.NPC_INDEPENDENTSET.ReduceTo.NPC_CLIQUE;

class reduceToCLIQUE : IReduction<INDEPENDENTSET, CLIQUE> {


    // --- Fields ---
    public string reductionName {get;} = "Clique reduction";
    public string reductionDefinition {get;} = @"This reduction converts an independent set problem into a clique problem, 
                                            by taking the complement of the graph, or inverting all the edges.";
    public string source {get;} = "";
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Independent_set_(graph_theory)#Relationship_to_other_graph_parameters";
    public string[] contributors {get;} = {"Russell Phillips"};

    public List<Gadget> gadgets { get; }
    private INDEPENDENTSET _reductionFrom;
    private CLIQUE _reductionTo;

    private string _complexity = "";


    // --- Properties ---
    public INDEPENDENTSET reductionFrom {
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
    public reduceToCLIQUE(INDEPENDENTSET from)
    {
        gadgets = new();
        _reductionFrom = from;
        _reductionTo = reduce();

    }

    public reduceToCLIQUE(string from) : this(new INDEPENDENTSET(from))
    {

    }
    public reduceToCLIQUE() : this(new INDEPENDENTSET()) { }

    /// <summary>
    /// Reduces a CLIQUE instance to a VERTEXCOVER instance.
    /// </summary>
    /// <returns> A Vertexcover instance</returns>
    /// <remarks>
    /// authored by Janita Aamir. Contributed to by Alex Diviney.
    /// </remarks>
    public CLIQUE reduce() {
        INDEPENDENTSET INDPENDENTSETInstance = _reductionFrom;
        CLIQUE reducedCLIQUE = new CLIQUE();
        reducedCLIQUE.nodes = INDPENDENTSETInstance.nodes;

        List<KeyValuePair<string, string>> edges = new List<KeyValuePair<string, string>>();

        foreach (var i in INDPENDENTSETInstance.nodes)
        {
            foreach (var j in INDPENDENTSETInstance.nodes)
            {
                KeyValuePair<string, string> pairCheck1 = new KeyValuePair<string, string>(i, j);
                KeyValuePair<string, string> pairCheck2 = new KeyValuePair<string, string>(j, i);
                if (!(INDPENDENTSETInstance.edges.Contains(pairCheck1) || INDPENDENTSETInstance.edges.Contains(pairCheck2) || i.Equals(j) || edges.Contains(pairCheck1) || edges.Contains(pairCheck2)))
                {
                    edges.Add(pairCheck1);
                }
            }
        }

        //set up gadgets
        foreach (UtilCollection node in INDPENDENTSETInstance.graph.Nodes)
        {
            gadgets.Add(new Gadget("ElementHighlight", new List<string>() { node.ToString() }, new List<string>() { node.ToString() }));
        }

        // --- Generate G string for new CLIQUE ---
        string nodesString = "";
        foreach (string nodes in INDPENDENTSETInstance.nodes) {
            nodesString += nodes + ",";
        }
        nodesString = nodesString.Trim(',');

        string edgesString = "";
        foreach (KeyValuePair<string,string> edge in edges) {
            edgesString += "{" + edge.Key + "," + edge.Value + "}" + ",";
        }
        edgesString = edgesString.Trim(',');

        string G = "(({" + nodesString + "},{" + edgesString + "})," + INDPENDENTSETInstance.K.ToString() + ")";

        reducedCLIQUE = new CLIQUE(G);
        reductionTo = reducedCLIQUE;
        return reducedCLIQUE;

    }

    public string mapSolutions(string problemFromSolution)
    {
        return problemFromSolution;
    }

}
