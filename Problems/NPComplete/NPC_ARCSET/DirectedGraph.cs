//DirectedGraph.cs
//Can take a string representation of a directed graph and turn it into a directed graph object.
using System;
using System.Collections.Generic;

namespace API.Problems.NPComplete.NPC_ARCSET;

class DirectedGraph:Graph{


    // --- Fields ---

    //private list nodeList // Node obj
   // protected List<Node> _nodeList;
    //private list edge list // edge obj
    //protected List<Edge> _edgeList;

    Dictionary<string,Node> _nodeDict;
  

    protected int _K;
    private int lazyCounter;
    private Dictionary<string,List<KeyValuePair<string,Node>>> _adjacencyMatrix; //Dictionary of Key Value Pairs where keys are node names and values are lists of all the node names that they are connected to.

    //Constructor
    public DirectedGraph(){

        _nodeList = new List<Node>();
         _edgeList = new List<Edge>();
        _nodeDict = new Dictionary<string, Node>();
      
       
        _K=0;
        _adjacencyMatrix = new Dictionary<string,List<KeyValuePair<string,Node>>>();
   
    generateAdjacencyMatrix();
    }

/**
*Note: this constructor DOES NOT WORK PROPERLY, REFACTOR THIS.
*
**/
    public DirectedGraph(List<Node> nl, List<Edge> el, int kVal){

        _nodeList = nl;
        _edgeList = el; 
        _K = kVal; 
        _nodeDict = new Dictionary<string, Node>();
        foreach(Node n in _nodeList){
            _nodeDict.Add(n.name,n);
        }
        _adjacencyMatrix = new Dictionary<string, List<KeyValuePair<string, Node>>>();
        generateAdjacencyMatrix();

    }

    public DirectedGraph(List<String> nl, List<KeyValuePair<string,string>> el, int kVal){
        
        _nodeList = new List<Node>();
        _K = kVal;
        _nodeDict = new Dictionary<string, Node>();
        _adjacencyMatrix = new Dictionary<string, List<KeyValuePair<string, Node>>>();
        _edgeList = new List<Edge>();

        foreach (string nodeStr in nl){
            Node node = new Node(nodeStr);
            _nodeList.Add(node);
            if(_nodeDict is not null){
            _nodeDict.Add(nodeStr,node);
            }
        }
        //Note that this is initializing unique node instances. May want to compose edges of already existing nodes instead. 
        foreach(KeyValuePair<string,string> edgeKV in el){
            string eStr1= edgeKV.Key;
            string eStr2 = edgeKV.Value;
            Node n1 = new Node(eStr1);
            Node n2 = new Node(eStr2);
            Edge edge = new Edge(n1,n2);
            _edgeList.Add(edge);
        }
        
        generateAdjacencyMatrix();
       
    }
    /**
    * takes a string directed graph input and constructs a directed graph
    **/
    public DirectedGraph(String graphStr){     
        List<string> nl = getNodes(graphStr);
        List<KeyValuePair<string,string>> el = getEdges(graphStr);
        
         //The following generates the dictionaries for our Nodes and Edges
         _nodeDict = new Dictionary<string,Node>();
        int k = getK(graphStr);

         _nodeList = new List<Node>();
        foreach (string nodeStr in nl){
            Node node = new Node(nodeStr);
            _nodeList.Add(node); //adds node to nodeList
            _nodeDict.Add(nodeStr,node); //adds node to nodeDIct
        }
        //Note that this is initializing unique node instances. May want to compose edges of already existing nodes instead. 
        _edgeList = new List<Edge>();
        foreach(KeyValuePair<string,string> edgeKV in el){
            string eStr1= edgeKV.Key;
            string eStr2 = edgeKV.Value;
            Node n1 = new Node(eStr1);
            Node n2 = new Node(eStr2);
            Edge edge = new Edge(n1,n2);
            _edgeList.Add(edge); //adds edge to edgeList
        }

        _K = k;
        _adjacencyMatrix = new Dictionary<string,List<KeyValuePair<string,Node>>>();
        generateAdjacencyMatrix();
 
    }

    public override string ToString(){
        string nodeListStr = "";
        foreach(Node node in _nodeList){
            nodeListStr= nodeListStr+ node.name +",";
        }
        nodeListStr = nodeListStr.TrimEnd(',');

        string edgeListStr = "";
        foreach(Edge edge in _edgeList){
           string edgeStr = edge.directedString() +" & "; //this line is what makes this class distinct from Undirected Graph
           //Console.WriteLine("Edge: "+ edge.directedString());
            edgeListStr = edgeListStr+ edgeStr+""; 
        }
        edgeListStr = edgeListStr.TrimEnd('&',' ');
        //edgeListStr = edgeListStr.TrimEnd(' ');
        string toStr = "{{"+nodeListStr+"}"+ " : {" + edgeListStr+"}"+" : "+_K+"}";
        return toStr;
    }  


    //ALEX NOTE: Taken from Kaden's Clique class. May refactor to directly return a node dictionary.
/**
  * Takes a string representation of a directed graph and returns its Nodes as a list of strings.
**/
    protected override List<string> getNodes(string Ginput) {

        List<string> allGNodes = new List<string>();
        string strippedInput = Ginput.Replace("{", "").Replace("}", "").Replace(" ", "").Replace("(", "").Replace(")",""); //Looks for ( and ) as delimiters for edge pairs
        
        // [0] is nodes,  [1] is edges,  [2] is k.
        string[] Gsections = strippedInput.Split(':');
        string[] Gnodes = Gsections[0].Split(',');
        
        foreach(string node in Gnodes) {
            allGNodes.Add(node);
        }

        return allGNodes;
    }


        //ALEX NOTE: Taken from Kaden's Clique class. May Refactor to directly return an Edge dictionary.
  /**
  * Takes a string representation of a directed graph and returns its edges as a list of strings.
  **/

    protected override List<KeyValuePair<string, string>> getEdges(string Ginput) {

        List<KeyValuePair<string, string>> allGEdges = new List<KeyValuePair<string, string>>();

        string strippedInput = Ginput.Replace("{", "").Replace("}", "").Replace(" ", "").Replace("(", "").Replace(")","");
        
        // [0] is nodes,  [1] is edges,  [2] is k.
        string[] Gsections = strippedInput.Split(':');
        string[] Gedges = Gsections[1].Split('&');
        
        foreach (string edge in Gedges) {
            string[] fromTo = edge.Split(',');
            string nodeFrom = fromTo[0];
            string nodeTo = fromTo[1];
            
            KeyValuePair<string,string> fullEdge = new KeyValuePair<string,string>(nodeFrom, nodeTo);
            allGEdges.Add(fullEdge);
        }

        return allGEdges;
    }

        //ALEX NOTE: Taken from Kaden's Clique class
  /**
  * Takes a string representation of a directed graph and returns its k value as a list of strings.
  **/
    protected override int getK(string Ginput) {
            string strippedInput = Ginput.Replace("{", "").Replace("}", "").Replace(" ", "").Replace("(", "").Replace(")","");
            
            // [0] is nodes,  [1] is edges,  [2] is k.
            string[] Gsections = strippedInput.Split(':');
            return Int32.Parse(Gsections[2]);
        }



/**
* This method generates a Dictionary of key value pairs to represent the graph so that the explore() function can explore the graph. 
**/
  private void generateAdjacencyMatrix(){

            

             _adjacencyMatrix =  new Dictionary<string,List<KeyValuePair<string,Node>>>();
            foreach(Node n in _nodeList){ //creates the x row
                _adjacencyMatrix.Add(n.name,new List<KeyValuePair<string,Node>>()); //initializes a list of KVP's per node. 
                    //Console.Write(n.name);    
            }
            List<KeyValuePair<string,Node>> posList;
            KeyValuePair<string,Node>kvp;
            foreach(Edge e in _edgeList){ //creates the y columns. 
                
                bool hasKey =_adjacencyMatrix.ContainsKey(e.node1.name);
                if (!hasKey){
                posList = new List<KeyValuePair<string, Node>>();
                kvp = new KeyValuePair<string, Node>(e.node2.name,e.node2);

                }
                else{
                    try{
                   posList = _adjacencyMatrix[e.node1.name];
                    kvp = new KeyValuePair<string, Node>(e.node2.name,e.node2);
                    posList.Add(kvp); //adds the node kvp to the list of kvps associated with "e1" (current node).

                    }
                    catch(KeyNotFoundException k){
                        Console.WriteLine("Key not found");
                        Console.WriteLine(k.StackTrace);
                    }
                 //_adjacencyMatrix.TryGetValue(e.node1.name,out posList); //given name of node 1 in edge, add the KVP (node2.name, node2) to list. 

                }
                  

            }

    }
    

    public string adjToString(){
        String toString = "";
        foreach(Node n in _nodeList){
            toString= toString+" Node: "+ n.name +" ";
            List<KeyValuePair<string,Node>> adjList = new List<KeyValuePair<string, Node>>();
            try{
                adjList =_adjacencyMatrix[n.name];
                      
            
             //bool testCreation = _adjacencyMatrix.TryGetValue(n.name,out adjList);
              //Console.Write(testStackCreation);
              toString = toString +"Edges: (";
              foreach(KeyValuePair<string,Node> kvp in adjList){
                
                  toString= toString+ "["+ n.name + ","+kvp.Value.name +"]";
              }
              toString= toString+")";
            }
            catch(KeyNotFoundException k){
                Console.WriteLine("KEY NOT FOUND");
                Console.WriteLine(k.StackTrace);
            }
        }
        return toString;
    }



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
    
    //Console.Write("Current Node: "+currentNode.name );

    foreach(KeyValuePair<string,Node> kvp in adjKVPList ){ //search the adjacent edges to this one
        int position; //position of adjacent node.
        String nextNodeName = kvp.Key;
        position = nodePositionDict[nextNodeName];
        //nodePositionDict.TryGetValue(nextNodeName,out position); //get the position associated with the name
        bool nodeIsVisited = visited[position]; //has this node been visited?
        if(nodeIsVisited){ //if a node has been visited then this graph contains a cycle. 
             //Console.WriteLine("Node: "+nextNodeName + "has already been visited. CYCLE FOUND!");
           // hasCycle = true; 
        }
        else{ //since this node in the adjacency list isn't visited, visit it. 
            currentNode = _nodeDict[nextNodeName];
            //_nodeDict.TryGetValue(nextNodeName,out currentNode); //sets the next node to currentNode. 
            explore(currentNode,visited,preVisitArr,postVisitArr,nodePositionDict,nodePreDict,nodePostDict);
        }
        // foreach(int pre in preVisitArr){
        //     Console.Write(pre +" ");
        // }
    }
    lazyCounter++;
    postVisitArr[currPos] = lazyCounter; //if we are at the bottom of the search then set our current node postVisit to our counter.
    }
    catch(KeyNotFoundException k){
        Console.WriteLine(k.StackTrace);
    }
}

/**
* This method uses a DFS to check a graph for cycles, returning true if any cycles have been found. 
**/
  public bool DFS(){
      //bool hasCycle = false;
      //while member, no need for static. 
    bool[] visited = new bool[_nodeList.Count]; //makes array equal entry for entry to nodeList
   // bool[] mapNodeNum = new bool[nodeList.Count];
    int[] preVisitArr = new int[_nodeList.Count];
    int[] postVisitArr = new int[_nodeList.Count];
    int i = 0;
    string nameNodeInit = "";
    if(_nodeList.Count!=0)
    {

        nameNodeInit = _nodeList[0].name; //This will start the DFS using the 

        Node currentNode = new Node(); //Instantiates Object. This is messy solution, but avoids a O(n) search of nodeList. 
        try{
            currentNode = _nodeDict[nameNodeInit];
        }
        catch(KeyNotFoundException k){
            Console.WriteLine("Key not found "+k.StackTrace);
        }
       // _nodeDict.TryGetValue(nameNodeInit, out currentNode); 

        //KeyValuePair<string,int> mapNodePos; //we want to map our node name to a position int
        Dictionary<string,int> nodePositionDict = new Dictionary<string,int>(); //creates a dictionary of KVPs

        //KeyValuePair<string,int> mapNodePreVis; //we want to map our node name to a previsit int
        Dictionary<string,int> nodePreDict = new Dictionary<string,int>(); //creates a dictionary of KVPs

       // KeyValuePair<string,int> mapNodePosVis; //we want to map our node name to a previsit int
        Dictionary<string,int> nodePostDict = new Dictionary<string,int>(); //creates a dictionary of KVPs

        foreach(var nodeKVP in _nodeDict){
        visited[i] = false; //sets initial visit value of every node to false
        string nodeName = nodeKVP.Key;
        //mapNodePos = new KeyValuePair<string, int>(nodeName,i); //maps name of node to position
        nodePositionDict.Add(nodeName,i); //now nodeNumDict will be able to find a position given a name.

        i++;
        }

        //int counter = 0;
        
        foreach(var entry in _nodeDict){
        int mappedPos = -1;
        try{
        mappedPos = nodePositionDict[entry.Key];
        }
        catch(KeyNotFoundException k ){
            Console.WriteLine("Key not found"+k.StackTrace);
        }
        //nodePositionDict.TryGetValue(entry.Key,out mappedPos); //looks for a position given name.
        if(!visited[mappedPos])
            { //if the boolean visit array sees the position isn't visited
        explore(currentNode,visited,preVisitArr,postVisitArr,nodePositionDict,nodePreDict,nodePostDict); //explore the position (start recursion).
            }

        }
            //checks for backedges.        
            foreach(Edge e in _edgeList){
           // Console.WriteLine(e.ToString());
            String nodeFrom = e.node1.name;
            String nodeTo = e.node2.name;

            int node1Pos;
            int node2Pos;
            try{
            nodePositionDict.TryGetValue(nodeFrom, out node1Pos);
            nodePositionDict.TryGetValue(nodeTo, out node2Pos);
            if(preVisitArr[node1Pos]>preVisitArr[node2Pos]){ //if the previsit value of the from node is greater than the to node, we have a backedge.
                return true;
            } 
            }
            catch(KeyNotFoundException k){
                Console.WriteLine("Key not found "+k.StackTrace);
            }
            
           // return false;
        }
   
    // Console.Write("Previsit Values:  {");
    // foreach(int preVis in preVisitArr){
    //     Console.Write(preVis + ",");
    // }
    // Console.WriteLine("}");
    //  Console.Write("Postvisit Values:  {");
    // foreach(int postVis in postVisitArr){
    //     Console.Write(postVis + ",");
    // }
    // Console.WriteLine("}");

    // Console.Write("Node Names:  {");
    // foreach(KeyValuePair<string,Node> nodeKVP in nodeDict){

    //     Console.Write(nodeKVP.Key + ",");
    // }
    // Console.WriteLine("}");

    }
    else{
        Console.Write("NodeList is empty, cannot DFS");
        }
    
    return false; //no cycle was found. 

}


    public void addEdge(KeyValuePair<string,string> edge){
        Node newNode1 = new Node(edge.Key);
        Node newNode2 = new Node(edge.Value);
        Edge newEdge = new Edge(newNode1,newNode2);
        this._edgeList.Add(newEdge);
        generateAdjacencyMatrix();

    }
    /**
    * Removes the edge from any data structures in the graph that regerence it.
    **/
    public void removeEdge(KeyValuePair<string,string> edge){
        string edgeKey = edge.Key;
        string edgeValue = edge.Value;
        List<Edge> foundEdgeList = new List<Edge>();
        
        foreach(Edge e in this._edgeList){ //O(n) search operation. This is due to not having a data structure that maps edge names to edges. 
            if(e.node1.name.Equals(edgeKey) && e.node2.name.Equals(edgeValue)){
                foundEdgeList.Add(e);
            }
        }
        foreach(Edge e in foundEdgeList){
            this._edgeList.Remove(e);
        }
        generateAdjacencyMatrix();
    }

    /**
    * Processes a user String input of edges and removes all of the input edges from the graph.
    **/
    public void processCertificate(String certificate){
        

        if (!certificate.Equals(String.Empty)){
        List<KeyValuePair<string,string>> certEdges;
        certEdges = getEdges(certificate);
        foreach(KeyValuePair<string, string> e in certEdges){
            removeEdge(e);
        }
    }

    }
  
    public List<Node> getNodeList{
        get{
            return base._nodeList;
        }
    }
    public List<Edge> getEdgeList{
        get{
            return base._edgeList;
        }
    }

   public Dictionary<string,Node> nodeDict{
       get{
           return _nodeDict;
       }
       set{
           _nodeDict = value;
       }
   }
   public Dictionary<string,List<KeyValuePair<string,Node>>> adjacencyMatrix{ 
       get{
           return _adjacencyMatrix;
       }
   }


}