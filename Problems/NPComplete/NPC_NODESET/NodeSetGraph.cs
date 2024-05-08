using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using API.Interfaces.Graphs;
namespace API.Problems.NPComplete.NPC_NODESET;

class NodeSetGraph:DirectedGraph{


    public NodeSetGraph(string arcInput) : base (arcInput){
        
    }

    public NodeSetGraph(string arcInput, bool decoy) : base (arcInput, decoy){
        _edgeList.Sort();
    }


 /// <summary>
 /// Processes a user String input of edges and removes all of the input edges from the graph.
 /// </summary>
 /// <param name="certificate"></param>
    public void processCertificate(String certificate){

        string edgePattern = @"([\w!]+)+,([\w!]+)";
        MatchCollection nMatches =  Regex.Matches(certificate,edgePattern);
        List<KeyValuePair<string,string>> certEdges = new List<KeyValuePair<string, string>>();

        //splits edges into a match collection of "a,b" form strings
        foreach(Match m in nMatches){ 
            string edgeStr = m.Value;
            string[] edgePair = edgeStr.Split(',');
            KeyValuePair<string,string> edgeKVP= new KeyValuePair<string, string>(edgePair[0],edgePair[1]);
            certEdges.Add(edgeKVP);
        }

        if (!certificate.Equals(String.Empty)){
            foreach(KeyValuePair<string, string> e in certEdges){
                removeEdge(e);
            }
        }

    }

    public void reverseCertificate(String certificate){

        string edgePattern = @"([\w!]+)+,([\w!]+)";
        MatchCollection nMatches =  Regex.Matches(certificate,edgePattern);
        List<KeyValuePair<string,string>> certEdges = new List<KeyValuePair<string, string>>();

        //splits edges into a match collection of "a,b" form strings
        foreach(Match m in nMatches){ 
            string edgeStr = m.Value;
            string[] edgePair = edgeStr.Split(',');
            KeyValuePair<string,string> edgeKVP= new KeyValuePair<string, string>(edgePair[0],edgePair[1]);
            certEdges.Add(edgeKVP);
        }

        if (!certificate.Equals(String.Empty)){
            foreach(KeyValuePair<string, string> e in certEdges){
                addEdge(e);
            }
        }

    }
    public int cerfitficateLength(String certificate){

        string edgePattern = @"([\w!]+)+,([\w!]+)";
        MatchCollection nMatches =  Regex.Matches(certificate,edgePattern);
        List<KeyValuePair<string,string>> certEdges = new List<KeyValuePair<string, string>>();

        //splits edges into a match collection of "a,b" form strings
        foreach(Match m in nMatches){ 
            string edgeStr = m.Value;
            string[] edgePair = edgeStr.Split(',');
            KeyValuePair<string,string> edgeKVP= new KeyValuePair<string, string>(edgePair[0],edgePair[1]);
            certEdges.Add(edgeKVP);
        }
        return nMatches.Count;
    }

/// <summary>
///  Gets a String of all the edges that are backedges.
///  ex. (4,1) (3,2)
/// </summary>
/// <returns>
/// String representation of backedges
/// </returns>
     public String getBackEdges(){
        List<Edge> backEdgeList = new List<Edge>();
        String toStr = "";
        backEdgeList = DFS();

        foreach(Edge e in backEdgeList){
            toStr += " "+e.directedString();
        }
        return toStr;
    }




/// <summary>
/// /This method uses a DFS to check a graph for cycles, returning a list of all all backedges (can be empty)
/// </summary>
/// <returns>
/// A list of all backedges. 
/// </returns>
    public List<Edge> DFS(int start = 0){
    
        bool[] visited = new bool[_nodeList.Count]; //makes array equal entry for entry to nodeList
        // bool[] mapNodeNum = new bool[nodeList.Count];
        int[] preVisitArr = new int[_nodeList.Count];
        int[] postVisitArr = new int[_nodeList.Count];
        lazyCounter = 0;
        List<Edge> backEdges = new List<Edge>();
        int i = 0;
        string nameNodeInit = "";
        if(_nodeList.Count!=0){
            nameNodeInit = _nodeList[start].name; //This will start the DFS using the first node in the list as the first one. need to add error handling
            Node currentNode = new Node(); //Instantiates Object. This is messy solution, but avoids a O(n) search of nodeList. 

            try{
                currentNode = _nodeDict[nameNodeInit];
                }
            catch(KeyNotFoundException k){
                Console.WriteLine("Key not found "+k.StackTrace);
            }

            //we want to map our node name to a position int
            Dictionary<string,int> nodePositionDict = new Dictionary<string,int>(); //creates a dictionary of KVPs

            //we want to map our node name to a previsit int
            Dictionary<string,int> nodePreDict = new Dictionary<string,int>(); //creates a dictionary of KVPs

        //we want to map our node name to a previsit int
            Dictionary<string,int> nodePostDict = new Dictionary<string,int>(); //creates a dictionary of KVPs

            //O(n)
            foreach(var nodeKVP in _nodeDict){
                visited[i] = false; //sets initial visit value of every node to false
                string nodeName = nodeKVP.Key;
                //maps name of node to position
                nodePositionDict.Add(nodeName,i); //now nodeNumDict will be able to find a position given a name.
                i++;
            }
            
            //O(n)
            foreach(var entry in _nodeDict){
                int mappedPos = -1;
                try{
                    mappedPos = nodePositionDict[entry.Key];
                }
                catch(KeyNotFoundException k ){
                    Console.WriteLine("Key not found"+k.StackTrace);
                }
                //nodePositionDict.TryGetValue(entry.Key,out mappedPos); //looks for a position given name.
                if(!visited[mappedPos]){ 
                    //if the boolean visit array sees the position isn't visited
                    explore(currentNode,visited,preVisitArr,postVisitArr,nodePositionDict,nodePreDict,nodePostDict); //explore the position (start recursion). O(n)
                }
            }
            //checks for backedges.  O(e)      
            foreach(Edge e in _edgeList){
                // Console.WriteLine(e.ToString());
                String nodeFrom = e.source.name;
                String nodeTo = e.target.name;

                int node1Pos;
                int node2Pos;
                try{
                    nodePositionDict.TryGetValue(nodeFrom, out node1Pos);
                    nodePositionDict.TryGetValue(nodeTo, out node2Pos);
                    if(preVisitArr[node1Pos]>preVisitArr[node2Pos] && postVisitArr[node1Pos]<postVisitArr[node2Pos]){ 
                        //if the previsit value of the from node is greater than the to node, we have a backedge.
                        // Console.WriteLine("     BACKEDGE FOUND from node: " + nodeFrom + " to node: "+nodeTo);
                        backEdges.Add(e);
                    } 
                }
                catch(KeyNotFoundException k){
                    Console.WriteLine("Key not found "+k.StackTrace);
                }
            }
        }
        else{
            Console.Write("NodeList is empty, cannot DFS");
        }
        return backEdges; //Returns a list of all the backEdges in the graph. 
    }


/// <summary>
/// Utility used by DFS() to traverse graph. 
/// </summary>
/// <param name="currentNode"></param>
/// <param name="visited"></param>
/// <param name="preVisitArr"></param>
/// <param name="postVisitArr"></param>
/// <param name="nodePositionDict"></param>
/// <param name="nodePreDict"></param>
/// <param name="nodePostDict"></param>
private void explore(Node currentNode,bool[] visited,int[] preVisitArr,int[] postVisitArr,Dictionary<string,int> nodePositionDict,Dictionary<string,int> nodePreDict,Dictionary<string,int> nodePostDict){
    int currPos; //our current node virtual array index.
    nodePositionDict.TryGetValue(currentNode.name,out currPos); //grabs the array position of the current node. 
    lazyCounter++;
     
    if(!visited[currPos]){
        visited[currPos] = true; //sets this node as visited.
        preVisitArr[currPos] = lazyCounter; //sets this Node's previsit value to our counter. 
    }  
    
    List<KeyValuePair<string,Node>> adjKVPList; //list of adjacent nodes.
    try{
        adjKVPList = _adjacencyMatrix[currentNode.name];
    
        // Console.WriteLine("Current Node: {0} ",currentNode.name );

        //O(1) but approaches O(n) the more connected the graph is
        foreach(KeyValuePair<string,Node> kvp in adjKVPList ){ //search the adjacent edges to this one
            int position; //position of adjacent node.
            String nextNodeName = kvp.Key;
            position = nodePositionDict[nextNodeName];
            //nodePositionDict.TryGetValue(nextNodeName,out position); //get the position associated with the name
            bool nodeIsVisited = visited[position]; //has this node been visited?
            if(nodeIsVisited){ //if a node has been visited then this graph contains a cycle. 
                // Console.WriteLine("Node: "+nextNodeName + " has already been visited. CYCLE FOUND!");
                // hasCycle = true; 
            }
            else{ //since this node in the adjacency list isn't visited, visit it. 
                currentNode = _nodeDict[nextNodeName];
                //_nodeDict.TryGetValue(nextNodeName,out currentNode); //sets the next node to currentNode. 
                explore(currentNode,visited,preVisitArr,postVisitArr,nodePositionDict,nodePreDict,nodePostDict);
            }
        }
        lazyCounter++;
        postVisitArr[currPos] = lazyCounter; //if we are at the bottom of the search then set our current node postVisit to our counter.
    }
    catch(KeyNotFoundException k){
        Console.WriteLine(k.StackTrace);
    }
}

///<summary> Adds the passed in edge to the graph </summary>
///<param name = "edge"> Edge to add </param>
    public void addEdge(KeyValuePair<string,string> edge){
        Node newNode1 = new Node(edge.Key);
        Node newNode2 = new Node(edge.Value);
        Edge newEdge = new Edge(newNode1,newNode2);
        this._edgeList.Add(newEdge);
        this._edgeList.Sort();
        generateAdjacencyMatrix();

    }

    ///<summary>
    /// Removes the edge from any data structures in the graph that regerence it.
    ///</summary>
    public void removeEdge(KeyValuePair<string,string> edge){
        string edgeKey = edge.Key;
        string edgeValue = edge.Value;
        List<Edge> foundEdgeList = new List<Edge>();
        
        foreach(Edge e in this._edgeList){ //O(n) search operation. This is due to not having a data structure that maps edge names to edges. 
            if(e.source.name.Equals(edgeKey) && e.target.name.Equals(edgeValue)){
                foundEdgeList.Add(e);
            }
        }
        foreach(Edge e in foundEdgeList){
            this._edgeList.Remove(e);
        }
        generateAdjacencyMatrix();
    }



///<summary>
/// Our DFS is able to return a bunch of information, so this gives a simple answer for verifiers to call, and then we can use the DFS to check for backedges specifically.
///</summary>
  public bool isCyclical(int start = 0){
      
      List<Edge> backEdgeList = new List<Edge>();
      backEdgeList = DFS(start);
      if (backEdgeList.Count==0){
          return false; //if no backedges, no cycles.
      }
      else{
          return true; //backedges mean cycles.
      }

  }

    /// <summary>
    ///  Returns a Jsoned Dot representation (jsoned list of strings) that is compliant with the graphvis DOT format. 
    /// </summary>
    /// <returns></returns>
    public override String toDotJson(){
        string totalString = $"";
        string preStr = @"digraph ARCSET{";
        totalString = totalString + preStr;

        //string preStr2 = @"node[style = ""filled""]";
        //totalString = totalString+preStr2;
        
        string dotNode = ""; 
       // string colorRed = "#d62728";
        foreach(Node n in _nodeList){
            dotNode=$"{n.name}";
            //dotNode=$"{n.name} [{colorRed}]";
            totalString = totalString+ dotNode + ";";
        }
        //totalString = totalString.TrimEnd(',');

        foreach(Edge e in _edgeList){
            KeyValuePair<string,string> eKVP = e.toKVP();
            string edgeStr = $" {eKVP.Key} -> {eKVP.Value};";
            totalString = totalString + edgeStr;
        }

        totalString = totalString+ "}";
        
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(totalString, options);
        return jsonString;
    }


}