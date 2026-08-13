//Graph.cs
//This is an abstract class for Undirected and Directed graphs to inherit from

using System.Collections.Generic;
using SPADE;
using API.Interfaces.JSON_Objects.Graphs;

namespace API.Interfaces.Graphs;


abstract class WeightedGraph : Graph {

    protected List<Node> _nodeList;
    protected List<WeightedEdge> _edgeList;

    public WeightedGraph() {
        _nodeList = new List<Node>();
        _edgeList = new List<WeightedEdge>();
    }

    public override List<Node> nodes {
        get {
            return _nodeList;
        }
    }

    public override List<Edge> edges {
        get {
            return _edgeList.Select(e => new Edge(e.source, e.target)).ToList();
        }
    }

    protected abstract List<string> getNodes(string gInput);
    protected abstract List<(string, string, int)> getEdges(string gInput);
    protected abstract int getK(string gInput);
}