using System;
using API.Interfaces;
using API.Interfaces.JSON_Objects.Graphs;
using SPADE;

namespace API.Interfaces.Graphs;
// By Michael Trosper //
/* This class was built to provide a more useful version of a graph 
 * that matched the transition function of a DFA better */
class WeightedDirectedGraph : Graph {
    private List<string> _nodeList;
    private List<LabeledEdge> _edgeList;
    private List<string> _acceptStates;
    private string _startState;

    public WeightedDirectedGraph() {
        _nodeList = new List<string>();
        _edgeList = new List<LabeledEdge>();
        _acceptStates = new List<string>();
        _startState = string.Empty;
    }

    public WeightedDirectedGraph(List<string> N, List<LabeledEdge> E, string S, List<string> F) {
        _nodeList = N;
        _edgeList = E;
        _startState = S;
        _acceptStates = F;
    }

    // This graph stores its data in the string-based DFA model (Nodes/Edges below),
    // not the base class's Node/Edge view, so the inherited accessors are unsupported.
    // Use Nodes/Edges instead. See ToAPIGraph for how the data is surfaced.
    public override List<Node> nodes
        => throw new NotSupportedException(
            "WeightedDirectedGraph does not expose the base Node/Edge view; use Nodes/Edges instead.");

    public override List<Edge> edges
        => throw new NotSupportedException(
            "WeightedDirectedGraph does not expose the base Node/Edge view; use Nodes/Edges instead.");

    public List<string> Nodes { get => _nodeList; }
    public List<LabeledEdge> Edges { get => _edgeList; }
    public string StartState { get => _startState; }
    public List<string> AcceptStates { get => _acceptStates; }

    // Added API_GraphJSON Conversion -- Michael Trosper -- 1/13/2026 //
    public override API_GraphJSON ToAPIGraph() {
        API_GraphJSON graph = new API_GraphJSON(Nodes, Edges, StartState, AcceptStates);

        for (int i = 0; i < graph.links.Count; i++) {
            graph.links[i].weighted = true;
            graph.links[i].weight = Edges[i].value.ToString();
            graph.links[i].directed = true;
        }

        return graph;
    }
}
