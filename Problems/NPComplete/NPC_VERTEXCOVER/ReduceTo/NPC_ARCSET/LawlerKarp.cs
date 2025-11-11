using API.Interfaces;
using API.Problems.NPComplete.NPC_ARCSET;
using API.Problems.NPComplete.NPC_VERTEXCOVER;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Interfaces.Graphs;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using SPADE;
using System.Collections;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.ReduceTo.NPC_ARCSET;
class LawlerKarp : IReduction<VERTEXCOVER, ARCSET> {

  

    // --- Fields ---
    public string reductionName {get;} = "Lawler and Karp Arcset Reduction";
    public string reductionDefinition {get;} = @"This Reduction is an implementation of Lawler and Karp's reduction as laid out in Karp's 21 NP_Complete Problems. 
                                            It takes an instance of an undirected graph (specifically an instance of VERTEXCOVER) and returns an instance of ARCSET (ie. a Directed Graph)
                                            Specifically, a reduction follows the following algorithm: 
                                            For an undirected graph H: Where H is made up of <V,E>
                                            Convert the undirected edges in E to pairs of directed edges. So an undirected edge {{A,B}} turns into the directed pair of edges {(A,B),(B,A)} 
                                            Then turn every node into a pair of nodes denoted by 0 and 1. So a node 'A' turns into the two nodes '<A,0>' and '<A,1>'
                                            Now looks at the pairs of edges in E and maps from 1 to 0. So an edge (A,B) turns into (<A,1>, <B,0>) and edge (B,A) becomes (<B,1>,<A,0>)
                                            Then add directed edges from every 0 node 'u' to 1 node 'u'. ie. creates edges from <A,0> to <A,1>, <B,0> to <B,1> … <Z,0> to <Z,1>
                                            Now the algorithm has created an ARCSET instance (in other words, a Digraph). ";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors { get; } = { "Daniel Igbokwe", "Caleb Eardley" };
     public List<Gadget> gadgets { get; }
    private VERTEXCOVER _reductionFrom;
    private ARCSET _reductionTo;
    // --- Properties ---
    public VERTEXCOVER reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public ARCSET reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }

    // --- Methods Including Constructors ---

    public LawlerKarp(string instance) : this(new VERTEXCOVER(instance)) { }
    public LawlerKarp() : this(new VERTEXCOVER()) { }
    public LawlerKarp(VERTEXCOVER from) {
        gadgets = new();
        _reductionFrom = from;
        _reductionTo = reduce();
    }
    /// <summary>
    ///  Uses the VertexCover object's reduction utility to convert to a Arcset Graph and returns that equivalent object.
    /// </summary>
    /// <returns>
    /// An Arcset Object
    /// </returns>
    public ARCSET reduce() {
        VERTEXCOVER vertexcover = new VERTEXCOVER(_reductionFrom.instance);
        List<Node> newNodes = new List<Node>();
        foreach (UtilCollection n in vertexcover.graph.Nodes)
        {
            string name = n.ToString();
            Node newNode1 = new Node(name);
            Node newNode2 = new Node(name);
            newNode1.name = name + "0";
            newNode2.name = name + "1";
            newNodes.Add(newNode1);
            newNodes.Add(newNode2);
            gadgets.Add(new Gadget("ElementHighlight", new List<string>() { name.ToString() }, new List<string>() { name + "0", name + "1"  }));
        }
        
        //Turn undirected edges into paired directed edges.
        List<Edge> newEdges = new List<Edge>();
        List<Edge> numberedEdges = new List<Edge>();
        foreach(UtilCollection e in vertexcover.graph.Edges){
            List<UtilCollection> cast = e.ToList();
            Node source = new Node(cast[0].ToString());
            Node target = new Node(cast[1].ToString());
            Edge newEdge1 = new Edge(source,target);
            Edge newEdge2 = new Edge(target,source);
            newEdges.Add(newEdge1);
            newEdges.Add(newEdge2);
        }

        //map edges to to nodes
        foreach(Edge e in newEdges){
            Node newNode1 = new Node(e.source.name+"1");
            Node newNode2 = new Node(e.target.name+"0");
            Edge numberedEdge = new Edge(newNode1,newNode2);
            numberedEdges.Add(numberedEdge);
        }

        //map from every 0 to 1
        for(int i=0;i<newNodes.Count;++i){
            if(i%2==0){
                Edge newEdge = new Edge(newNodes[i],newNodes[i+1]);
                numberedEdges.Add(newEdge);
            }
        }
        newEdges.Clear(); //Getting rid of unsplit edges
        newEdges = numberedEdges;
        
        //"{{1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)},1}" //formatting
        string nodeListStr = "";
        foreach(Node node in newNodes){
    
            nodeListStr= nodeListStr+ node.name +",";
        }
        nodeListStr = nodeListStr.TrimEnd(',');
        string edgeListStr = "";
        foreach(Edge edge in newEdges){
           string edgeStr = edge.directedString() +","; //this line is what makes this class distinct from Undirected Graph
            edgeListStr = edgeListStr+ edgeStr+""; 
        }
        edgeListStr = edgeListStr.TrimEnd(',',' ');
        string toStr = "(({"+nodeListStr+"}"+ ",{" + edgeListStr+"}"+"),"+vertexcover.K+")";
        ARCSET arcset = new ARCSET(toStr);

        return arcset;
    }

    public string mapSolutions(string problemFromSolution){
        //Parse problemFromSolution into a list of nodes
        List<string> solutionList = GraphParser.parseNodeListWithStringFunctions(problemFromSolution);

        //Map solution 
        List<string> mappedSolutionList = new List<string>();
        foreach(string node in solutionList){
            mappedSolutionList.Add(string.Format("({0}0,{0}1)",node));
        }
        string problemToSolution = "";
        foreach(string edge in mappedSolutionList){
            problemToSolution += edge + ',';
        }
        return '{' + problemToSolution.TrimEnd(',') + '}';
    }
}
// return an instance of what you are reducing to