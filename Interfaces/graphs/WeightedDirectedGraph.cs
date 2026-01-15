using System;
using API.Interfaces;
using API.Interfaces.JSON_Objects.Graphs;
using SPADE;

namespace API.Interfaces.Graphs;
// By Michael Trosper //
/* This class was built to provide a more useful version of a graph 
 * that matched the transition function of a DFA better */
class WeightedDirectedGraph : Graph
{
    private List<string> _nodeList;
    private List<WeightedEdge> _edgeList;
    private List<Node> _nodes;
    private List<Edge> _edges;

    public WeightedDirectedGraph()
    {
        _nodeList = new List<string>();
        _edgeList = new List<WeightedEdge>();
    }

    public WeightedDirectedGraph(List<string> N, List<WeightedEdge> E)
    {
        _nodeList = N;
        _edgeList = E;
    }

    public override List<Node> nodes
    {
        get
        {
            return _nodes;
        }
    }

    public override List<Edge> edges
    {
        get
        {
            return _edges;
        }
    }
    
    public List<string> Nodes { get => _nodeList; }
    public List<WeightedEdge> Edges { get => _edgeList; }

    // Added API_GraphJSON Conversion -- Michael Trosper -- 1/13/2026 //
    public override API_GraphJSON ToAPIGraph()
    {
        API_GraphJSON graph = new API_GraphJSON(Nodes, Edges);

        for (int i = 0; i < graph.links.Count; i++)
        {
            graph.links[i].weight = Edges[i].value.ToString();
            graph.links[i].directed = true;
        }

        return graph;
    }
}
