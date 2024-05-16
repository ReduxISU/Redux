//Graph.cs
//This is an abstract class for Undirected and Directed graphs to inherit from

using System.Collections.Generic;

namespace API.Interfaces.Graphs;


 abstract class WeightedGraph{

 protected List<Node> _nodeList;
 protected List<WeightedEdge> _edgeList;


public WeightedGraph(){
    _nodeList = new List<Node>();
    _edgeList = new List<WeightedEdge>();

}
protected abstract List<string> getNodes(string gInput);
protected abstract List<(string,string,int)> getEdges(string gInput);
protected abstract int getK(string gInput);

}