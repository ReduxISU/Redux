//Graph.cs
//This is an abstract class for Undirected and Directed graphs to inherit from

using System.Collections.Generic;

namespace API.Interfaces.Graphs;


 abstract class Graph{

 protected List<Node> _nodeList;
 protected List<Edge> _edgeList;


public Graph(){
    _nodeList = new List<Node>();
    _edgeList = new List<Edge>();

}
protected abstract List<string> getNodes(string gInput);
protected abstract List<KeyValuePair<string,string>> getEdges(string gInput);

protected abstract int getK(string gInput);

}