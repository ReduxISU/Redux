using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.Graphs.GraphParser;
namespace API.Problems.NPComplete.NPC_VERTEXCOVER;

class VertexCoverGraph:UnweightedUndirectedGraph{


    public VertexCoverGraph() : base(){
        
    }

    /// <summary>
 /// Takes a String and creates a VertexCoverGraph from it
 /// NOTE: DEPRECATED format, ex: {{a,b,c} : {{a,b} &amp; {b,c}} : 1}
 /// </summary>
 /// <param name="vertInput"> string input</param>
    public VertexCoverGraph(string vertInput) : base (vertInput){
        
    }

    //Constructor for standard graph formatted string input.
     /// <summary>
     /// 
     /// </summary>
     /// <param name="vertInput"> Undirected Graph string input
     /// ex. {{1,2,3},{{1,2},{2,3}},0}
     /// </param>
     /// <param name="decoy"></param>
    public VertexCoverGraph(string vertInput, bool decoy) : base (vertInput, decoy){
    
    }


    public API_UndirectedGraphJSON visualizeGraph(){
        API_UndirectedGraphJSON apiGraphRepresentation = new API_UndirectedGraphJSON(this._nodeList,this._edgeList);
        return apiGraphRepresentation;
    }

/// <summary>
/// This method allows us to, given a solution string, return an API Undirected graph that can be jsoned with each node's attribute1 characteristic being used 
/// by the api to mark if the node is in the solution set or not. 
/// </summary>
/// <param name="solutionString"></param>
/// <returns></returns>
    public API_UndirectedGraphJSON visualizeSolution(string solutionString){
        API_UndirectedGraphJSON apiGraph = visualizeGraph();
        GraphParser gParser = new GraphParser();
        List<string> parsedNodes = gParser.getNodesFromNodeListString(solutionString);
        foreach(API_Node_Programmable_Small progNode in apiGraph.nodes){ //For every node in the graph
            if(parsedNodes.Contains(progNode.name)){ //if that node is found in the solution set. (note, inefficient)
                progNode.attribute1 = "true"; //we mark the programmable attribute1 value as true.
            }
            else{
                progNode.attribute1 = "false";
            }
        }
        return apiGraph;
    }

     public string reduction(){
        List<Node> newNodes = new List<Node>();
        foreach(Node n in _nodeList){
            Node newNode1 = new VertexCoverNode(n.name);
            Node newNode2 = new VertexCoverNode(n.name);
            newNode1.name = n.name+"0";
            newNode2.name = n.name+"1";
            newNodes.Add(newNode1);
            newNodes.Add(newNode2);
        }
        //Turn undirected edges into paired directed edges.
        List<Edge> newEdges = new List<Edge>();
        List<Edge> numberedEdges = new List<Edge>();
        foreach(Edge e in _edgeList){
            Edge newEdge1 = new Edge(e.source,e.target);
            Edge newEdge2 = new Edge(e.target,e.source);
            newEdges.Add(newEdge1);
            newEdges.Add(newEdge2);
        }

        //map edges to to nodes
        foreach(Edge e in newEdges){
            Node newNode1 = new VertexCoverNode(e.source.name+"1");
            Node newNode2 = new VertexCoverNode(e.target.name+"0");
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

      //  Console.WriteLine("Nodes: ");
        foreach(Node n in newNodes){
            //Console.Write(n.name+",");
        }
       // Console.WriteLine("Edges: ");

        foreach(Edge e in newEdges){
          //  Console.WriteLine(e.directedString());
        }
        
        //"{{1,2,3,4},{(4,1),(1,2),(4,3),(3,2),(2,4)},1}" //formatting
        string nodeListStr = "";
        foreach(Node node in newNodes){
    
            nodeListStr= nodeListStr+ node.name +",";
        }
        nodeListStr = nodeListStr.TrimEnd(',');
        string edgeListStr = "";
        foreach(Edge edge in newEdges){
           string edgeStr = edge.directedString() +","; //this line is what makes this class distinct from Undirected Graph
           //Console.WriteLine("Edge: "+ edge.directedString());
            edgeListStr = edgeListStr+ edgeStr+""; 
        }
        edgeListStr = edgeListStr.TrimEnd(',',' ');
        //edgeListStr = edgeListStr.TrimEnd(' ');
        string toStr = "(({"+nodeListStr+"}"+ ",{" + edgeListStr+"}"+"),"+_K+")";
        return toStr;
        //DirectedGraph reductionGraph = new DirectedGraph(newNodes,newEdges,_K);
       // return reductionGraph;

    }
}