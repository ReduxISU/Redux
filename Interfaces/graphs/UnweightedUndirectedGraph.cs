
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace API.Interfaces.Graphs;

abstract class UnweightedUndirectedGraph : Graph
{


    // --- Fields ---
    //Since we are inheriting from the graph abstract class, fields are blank. There is probably a better way to do this though.
    // protected List<Node> _nodeList;

    // protected List<Edge> _edgeList;


    protected int _K;
    protected List<string> _nodeStringList = new List<string>();
    protected List<KeyValuePair<string, string>> _edgesKVP = new List<KeyValuePair<string, string>>();


    //Constructor
    public UnweightedUndirectedGraph()
    {

        _nodeList = new List<Node>();
        _edgeList = new List<Edge>();
        _K = 0;

    }


    public UnweightedUndirectedGraph(List<Node> nl, List<Edge> el, int kVal)
    {

        this._nodeList = nl;
        this._edgeList = el;
        _K = kVal;
    }

    //This constructors takes in a list of nodes (in string format) and a list of edges (in string format) and creates a graph
    public UnweightedUndirectedGraph(List<String> nl, List<KeyValuePair<string, string>> el, int kVal)
    {

        this._nodeList = new List<Node>();
        foreach (string nodeStr in nl)
        {
            Node node = new Node(nodeStr);
            _nodeList.Add(node);
        }
        //Note that this is initializing unique node instances. May want to compose edges of already existing nodes instead. 
        this._edgeList = new List<Edge>();
        foreach (KeyValuePair<string, string> edgeKV in el)
        {
            string eStr1 = edgeKV.Key;
            string eStr2 = edgeKV.Value;
            Node n1 = new Node(eStr1);
            Node n2 = new Node(eStr2);
            Edge edge = new Edge(n1, n2);
            this._edgeList.Add(edge);
        }
        _K = kVal;
    }

    /// <summary>
    /// Takes a String and creates a VertexCoverGraph from it
    /// NOTE: DEPRECATED format, ex: {{a,b,c} : {{a,b} &amp; {b,c}} : 1}
    /// </summary>
    /// <param name="graphStr"> string input</param>
    public UnweightedUndirectedGraph(String graphStr)
    {


        List<string> nl = getNodes(graphStr);
        List<KeyValuePair<string, string>> el = getEdges(graphStr);
        int k = getK(graphStr);

        this._nodeList = new List<Node>();
        foreach (string nodeStr in nl)
        {
            Node node = new Node(nodeStr);
            _nodeList.Add(node);
        }
        //Note that this is initializing unique node instances. May want to compose edges of already existing nodes instead. 
        this._edgeList = new List<Edge>();
        foreach (KeyValuePair<string, string> edgeKV in el)
        {
            string eStr1 = edgeKV.Key;
            string eStr2 = edgeKV.Value;
            Node n1 = new Node(eStr1);
            Node n2 = new Node(eStr2);
            Edge edge = new Edge(n1, n2);
            this._edgeList.Add(edge);
        }
        foreach (Edge e in _edgeList)
        {
            //Console.Write(e.undirectedString()+ " ");
        }

        _K = k;
    }



    //Constructor for standard graph formatted string input.
    /// <summary>
    /// 
    /// </summary>
    /// <param name="graphStr"> Undirected Graph string input
    /// ex. {{1,2,3},{{1,2},{2,3}},0}
    /// </param>
    /// <param name="decoy"></param>
    public UnweightedUndirectedGraph(String graphStr, bool decoy)
    {
        string pattern;
        string patternWithTerminals;
        pattern = @"\(\({([\w!]+)(,([\w!]+))*},{\{([\w!]+),([\w!]+)\}(,\{([\w!]+),([\w!]+)\})*}\),\d+\)"; //checks for undirected graph format
        patternWithTerminals = @"{{(([\w!]+)(,([\w!]+))*)+},{(\{([\w!]+),([\w!]+)\}(,\{([\w!]+),([\w!]+)\})*)*},{(([\w!]+)(,([\w!]+))*)+},\d+}"; //checks for undirected graph format with terminals
        Regex reg = new Regex(pattern);
        Regex regTerminal = new Regex(patternWithTerminals);
        bool inputIsValid = reg.IsMatch(graphStr);
        bool terminalInputIsValid = regTerminal.IsMatch(graphStr);
        if (inputIsValid || terminalInputIsValid)
        {

            //nodes
            string nodePattern = @"{[\w!]+(,[\w!]+)*}";
            MatchCollection nMatches = Regex.Matches(graphStr, nodePattern);
            string nodeStr = nMatches[0].ToString();
            nodeStr = nodeStr.TrimStart('{');
            nodeStr = nodeStr.TrimEnd('}');
            string[] nodeStringList = nodeStr.Split(',');
            foreach (string nodeName in nodeStringList)
            {
                _nodeList.Add(new Node(nodeName));
            }
            //Console.WriteLine(nMatches[0]);

            //edges
            string edgePattern = @"{(\{([\w!]+),([\w!]+)\}(,\{([\w!]+),([\w!]+)\})*)*}";
            MatchCollection eMatches = Regex.Matches(graphStr, edgePattern);
            string edgeStr = eMatches[0].ToString();
            //Console.WriteLine(edgeStr);
            string edgePatternInner = @"([\w!]+),([\w!]+)";
            MatchCollection eMatches2 = Regex.Matches(edgeStr, edgePatternInner);
            foreach (Match medge in eMatches2)
            {
                string[] edgeSplit = medge.ToString().Split(',');
                Node n1 = new Node(edgeSplit[0]);
                Node n2 = new Node(edgeSplit[1]);
                _edgeList.Add(new Edge(n1, n2));
            }

            //end num
            string endNumPatternOuter = @"\),\d+\)"; //gets the end section of the graph string
            MatchCollection numMatches = Regex.Matches(graphStr, endNumPatternOuter);
            string outerString = numMatches[0].ToString();
            string endNumPatternInner = @"\d+"; //parses out number from end section.
            MatchCollection numMatches2 = Regex.Matches(outerString, endNumPatternInner);
            string innerString = numMatches2[0].ToString();

            int convNum = Int32.Parse(innerString);

            _K = convNum;


            foreach (Node n in _nodeList)
            {
                _nodeStringList.Add(n.name);
            }
            foreach (Edge e in _edgeList)
            {
                KeyValuePair<string, string> tempKVP = new KeyValuePair<string, string>(e.source.name, e.target.name);
                _edgesKVP.Add(tempKVP);
            }

        }
        else
        {
            Console.WriteLine("NOT VALID INPUT for Regex evaluation! INITIALIZATION FAILED");
        }

    }


    /// <summary>
    /// The toString method used to use an old graph format, now it an alias for
    /// formalString
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {

        return formalString();
    }

    public string formalString()
    {

        string nodeListStr = "";
        foreach (Node node in _nodeList)
        {

            nodeListStr = nodeListStr + node.name + ",";
        }
        nodeListStr = nodeListStr.TrimEnd(',');

        string edgeListStr = "";
        foreach (Edge edge in _edgeList)
        {
            string edgeStr = edge.undirectedString() + ","; //This line makes this distinct from DirectedGraph
            edgeListStr = edgeListStr + edgeStr + "";
        }
        edgeListStr = edgeListStr.TrimEnd(',', ' ');
        //edgeListStr = edgeListStr.TrimEnd(' ');
        string toStr = "(({" + nodeListStr + "}" + ",{" + edgeListStr + "})" + "," + _K + ")";
        return toStr;

    }

    //ALEX NOTE: Taken from Kaden's Clique class
    /**
      * Takes a string representation of a directed graph and returns its Nodes as a list of strings.
    **/
    protected override List<string> getNodes(string Ginput)
    {

        List<string> allGNodes = new List<string>();
        string strippedInput = Ginput.Replace("{", "").Replace("}", "").Replace(" ", "").Replace("(", "").Replace(")", ""); //uses [ ] as delimiters for edge pairs

        // [0] is nodes,  [1] is edges,  [2] is k.
        string[] Gsections = strippedInput.Split(':');
        string[] Gnodes = Gsections[0].Split(',');

        foreach (string node in Gnodes)
        {
            allGNodes.Add(node);
        }

        return allGNodes;
    }

    //ALEX NOTE: Taken from Kaden's Clique class
    /**
    * Takes a string representation of a directed graph and returns its edges as a list of strings.
    **/

    protected override List<KeyValuePair<string, string>> getEdges(string Ginput)
    {

        List<KeyValuePair<string, string>> allGEdges = new List<KeyValuePair<string, string>>();

        string strippedInput = Ginput.Replace("{", "").Replace("}", "").Replace(" ", "").Replace("(", "").Replace(")", "");

        // [0] is nodes,  [1] is edges,  [2] is k.
        string[] Gsections = strippedInput.Split(':');
        string[] Gedges = Gsections[1].Split('&');

        foreach (string edge in Gedges)
        {
            if (edge.Replace(" ", "") != "")
            { // Checks that edge isn't empty string, which can happens if there are no edges to begin with
                string[] fromTo = edge.Split(',');
                string nodeFrom = fromTo[0];
                string nodeTo = fromTo[1];

                KeyValuePair<string, string> fullEdge = new KeyValuePair<string, string>(nodeFrom, nodeTo);
                allGEdges.Add(fullEdge);
            }
        }

        return allGEdges;
    }

    //ALEX NOTE: Taken from Kaden's Clique class

    /// <summary>
    ///  Takes a string representation of a directed graph and returns its k value as a list of strings.
    /// </summary>
    /// <param name="Ginput"></param>
    /// <returns></returns>
    protected override int getK(string Ginput)
    {
        string strippedInput = Ginput.Replace("{", "").Replace("}", "").Replace(" ", "").Replace("(", "").Replace(")", "");

        // [0] is nodes,  [1] is edges,  [2] is k.
        string[] Gsections = strippedInput.Split(':');
        return Int32.Parse(Gsections[2]);
    }




    //Getters
    public List<Node> getNodeList
    {
        get
        {
            return base._nodeList;
        }
    }
    public List<Edge> getEdgeList
    {
        get
        {
            return base._edgeList;
        }
    }

    public List<string> nodesStringList
    {
        get
        {
            return _nodeStringList;
        }
    }
    public List<KeyValuePair<string, string>> edgesKVP
    {
        get
        {
            return _edgesKVP;
        }
    }

    public int K
    {
        get
        {
            return _K;
        }
    }


}