//DirectedGraph.cs
//Can take a string representation of a directed graph and turn it into a directed graph object.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using API.Interfaces.Graphs;


namespace API.Interfaces.Graphs;

abstract class DirectedGraph:Graph{


    // --- Fields ---

    //private list nodeList // Node obj
    //protected List<Node> _nodeList;
    //private list edge list // edge obj
    //protected List<Edge> _edgeList;

    protected Dictionary<string,Node> _nodeDict;
    protected List<string> _nodeStringList = new List<string>();
    protected List<KeyValuePair<string, string>> _edgesKVP = new List<KeyValuePair<string, string>>();

  

    protected int _K;
    protected int lazyCounter;
    protected Dictionary<string,List<KeyValuePair<string,Node>>> _adjacencyMatrix; //Dictionary of Key Value Pairs where keys are node names and values are lists of all the node names that they are connected to.

    ///<summary> Default Constructor </summary>
    public DirectedGraph(){

        _nodeList = new List<Node>();
        _edgeList = new List<Edge>();
        _nodeDict = new Dictionary<string, Node>();
      
       
        _K=0;
        _adjacencyMatrix = new Dictionary<string,List<KeyValuePair<string,Node>>>();
   
    generateAdjacencyMatrix();
    }


    
    ///<summary>
    ///
    /// Constructor for standard graph formatted string input. Takes a string in directed graph format.
    ///ex {{a,b,c},{(a,b),(b,c)},0}
    ///</summary>
    ///<param name="graphStr">A Directed Graph string</param>
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



    ///<summary>
    /// Constructor for standard graph formatted string input. Takes a string in directed graph format
    ///ex. {{a,b,c},{(a,b),(b,c)},0}
    ///</summary>
    ///<param name="graphStr">A Directed Graph string</param>
    ///<param name = "decoy"> This is a temp variable for constructor overloading while deprecating the original format </param>
    public DirectedGraph(String graphStr,bool decoy){

        _nodeDict = new Dictionary<string,Node>();
        _adjacencyMatrix = new Dictionary<string, List<KeyValuePair<string, Node>>>();

        string pattern;
        pattern = @"\(\({(([\w!]+)+(,([\w!]+))*)},{(\(([\w!]+),([\w!]+)\)(,\(([\w!]+),([\w!]+)\))*)*}\),\d+\)"; //checks for directed graph format
        Regex reg = new Regex(pattern);
        bool inputIsValid = reg.IsMatch(graphStr);
        if(inputIsValid){
            
            //nodes
            string nodePattern = @"{((([\w!]+))*(([\w!]+),)*)+}";
            MatchCollection nMatches =  Regex.Matches(graphStr,nodePattern);
            string nodeStr = nMatches[0].ToString();
            nodeStr = nodeStr.TrimStart('{');
            nodeStr = nodeStr.TrimEnd('}');
            string[] nodeStringList = nodeStr.Split(',');
            foreach(string nodeName in nodeStringList){
               _nodeList.Add(new Node(nodeName));
               _nodeDict.Add(nodeName, new Node(nodeName));
            }
           //Console.WriteLine(nMatches[0]);
            
            //edges
            string edgePattern = @"{(\(([\w!]+),([\w!]+)\)(,\(([\w!]+),([\w!]+)\))*)*}";
            MatchCollection eMatches = Regex.Matches(graphStr,edgePattern);
            string edgeStr = eMatches[0].ToString();
            string edgePatternInner = @"([\w!]+)+,([\w!]+)";
            MatchCollection eMatches2 = Regex.Matches(edgeStr,edgePatternInner);
            foreach(Match medge in eMatches2){
                string[] edgeSplit = medge.ToString().Split(',');
                Node n1 = new Node(edgeSplit[0]);
                Node n2 = new Node(edgeSplit[1]);
                _edgeList.Add(new Edge(n1,n2));
            }
            
            //end num
             string endNumPatternOuter = @"\),\d+\)"; //gets the end section of the graph string
            MatchCollection numMatches = Regex.Matches(graphStr,endNumPatternOuter);
            string outerString = numMatches[0].ToString();
            string endNumPatternInner = @"\d+"; //parses out number from end section.
            MatchCollection numMatches2 = Regex.Matches(outerString,endNumPatternInner);
            string innerString = numMatches2[0].ToString();
            int convNum = Int32.Parse(innerString);
            _K = convNum;
            _adjacencyMatrix = new Dictionary<string,List<KeyValuePair<string,Node>>>();
            generateAdjacencyMatrix();

            
          foreach(Node n in _nodeList){
            _nodeStringList.Add(n.name);
        }
        foreach(Edge e in _edgeList){
                KeyValuePair<string, string> tempKVP = new KeyValuePair<string, string>(e.source.name, e.target.name);
                _edgesKVP.Add(tempKVP);
            }
 
        }
        else
        {
           Console.WriteLine("NOT VALID INPUT for Regex evaluation! INITIALIZATION FAILED");
            
        }


    }

    public override string ToString(){
        string nodeListStr = "";
        foreach(Node node in _nodeList){
            nodeListStr= nodeListStr+ node.name +",";
        }
        nodeListStr = nodeListStr.TrimEnd(',');

        string edgeListStr = "";
        foreach(Edge edge in _edgeList){
           string edgeStr = edge.directedString() +","; //this line is what makes this class distinct from Undirected Graph
           //Console.WriteLine("Edge: "+ edge.directedString());
            edgeListStr = edgeListStr+ edgeStr+""; 
        }
        edgeListStr = edgeListStr.TrimEnd(',',' ');
        //edgeListStr = edgeListStr.TrimEnd(' ');
        string toStr = "(({"+nodeListStr+"}"+ ",{" + edgeListStr+"}"+"),"+_K+")";
        return toStr;
    }  


//ALEX NOTE: Taken from Kaden's Clique class. 
///<summary>
///Takes a string representation of a directed graph and returns its Nodes as a list of strings.
///NOTE: DEPRECATED
///</summary>
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


//ALEX NOTE: Taken from Kaden's Clique class. DEPRECATED
///<summary>
///Takes a string representation of a directed graph and returns its edges as a list of strings.
///NOTE: DEPRECATED
///</summary>
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
///<summary>
///Takes a string representation of a directed graph and returns its k value as a list of strings.
///NOTE: DEPRECATED
///</summary>
    protected override int getK(string Ginput) {
        string strippedInput = Ginput.Replace("{", "").Replace("}", "").Replace(" ", "").Replace("(", "").Replace(")","");
        
        // [0] is nodes,  [1] is edges,  [2] is k.
        string[] Gsections = strippedInput.Split(':');
        return Int32.Parse(Gsections[2]);
    }




///<summary>
/// This method generates a Dictionary of key value pairs to represent the graph
///</summary>
    protected void generateAdjacencyMatrix(){
        _adjacencyMatrix =  new Dictionary<string,List<KeyValuePair<string,Node>>>();
        foreach(Node n in _nodeList){ //creates the x row
            _adjacencyMatrix.Add(n.name,new List<KeyValuePair<string,Node>>()); //initializes a list of KVP's per node. 
                //Console.Write(n.name);    
        }
        List<KeyValuePair<string,Node>> posList;
        KeyValuePair<string,Node>kvp;
        foreach(Edge e in _edgeList){ //creates the y columns. 
            
            bool hasKey =_adjacencyMatrix.ContainsKey(e.source.name);
            if (!hasKey){
                posList = new List<KeyValuePair<string, Node>>();
                kvp = new KeyValuePair<string, Node>(e.target.name,e.target);
            }
            else{
                try{
                    posList = _adjacencyMatrix[e.source.name];
                    kvp = new KeyValuePair<string, Node>(e.target.name,e.target);
                    posList.Add(kvp); //adds the node kvp to the list of kvps associated with "e1" (current node).
                }
                catch(KeyNotFoundException k){
                    Console.WriteLine("Key not found");
                    Console.WriteLine(k.StackTrace);
                }
                //_adjacencyMatrix.TryGetValue(e.source.name,out posList); //given name of node 1 in edge, add the KVP (target.name, target) to list. 

            }
                

        }

    }
    
///<summary>
/// displays the adjacency matrix representation of the graph
///</summary>
    public string adjToString(){
        String toString = "";
        foreach(Node n in _nodeList){
            toString= toString+" Node: "+ n.name +" ";
            List<KeyValuePair<string,Node>> adjList = new List<KeyValuePair<string, Node>>();
            try{
                adjList =_adjacencyMatrix[n.name];
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


    ///<summary>
    ///Returns an ArrayList of Strings that is essentially a dot representation of the graph.
    ///</summary>
    public ArrayList toDotArrayList(){

        ArrayList dotList = new ArrayList();
  
        string preStr = @"digraph {";
        dotList.Add(preStr);

        //string preStr2 = @"node[style = ""filled""]";
        dotList.Add(preStr);

        
        string dotNode = ""; 
        //string colorRed = "#d62728";
        foreach(Node n in _nodeList){
        //dotNode=$"{n.name} [{colorRed}]";
        dotList.Add(dotNode);
        }
        foreach(Edge e in _edgeList){ 
            KeyValuePair<string,string> eKVP = e.toKVP();
            string edgeStr = $"{eKVP.Key} -> {eKVP.Value}";
            dotList.Add(edgeStr);
        }
        dotList.Add("}");
        return dotList;
       
    }

    /// <summary>
    /// Returns a Jsoned Dot representation (jsoned list of strings) that is compliant with the graphvis DOT format. 
    /// </summary>
    /// <returns></returns>
    public abstract String toDotJson();
    //{

    //     string totalString = $"";
    //     string preStr = @"digraph {";
    //     totalString = totalString + preStr;

    //     //string preStr2 = @"node[style = ""filled""]";
    //     //totalString = totalString+preStr2;

    //     string dotNode = ""; 
    //    // string colorRed = "#d62728";
    //     foreach(Node n in _nodeList){
    //     dotNode=$"{n.name}";
    //     //dotNode=$"{n.name} [{colorRed}]";
    //     totalString = totalString+ dotNode + ";";
    //     }
    //     totalString = totalString.TrimEnd(',');

    //     foreach(Edge e in _edgeList){
    //         KeyValuePair<string,string> eKVP = e.toKVP();
    //         string edgeStr = $" {eKVP.Key} -> {eKVP.Value}";
    //         totalString = totalString + edgeStr;
    //     }

    //     totalString = totalString}";

    //     var options = new JsonSerializerOptions { WriteIndented = true };
    //     string jsonString = JsonSerializer.Serialize(totalString, options);
    //     return jsonString;

    //}



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
    public int K{
        get{
            return _K;
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

       public List<string> nodesStringList{
        get{
            return _nodeStringList;
        }
    }
    public List<KeyValuePair<string,string>> edgesKVP{
        get{
            return _edgesKVP;
        }
    }


}