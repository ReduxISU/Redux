
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace API.Interfaces.Graphs.GraphParser;

class GraphParser {
     

public GraphParser(){
}
 /**
 * Checks if an input string is a valid undirected graph. 
 **/
public bool isValidUndirectedGraph(string undirectedGraphStr){
    string pattern;
    pattern = @"{{((\w+)*(\w+,)*)+},{(({\w+,\w+})*({\w+,\w+},)*)*},\d+}"; //checks for undirected graph format
    Regex reg = new Regex(pattern);
    bool inputIsValid = reg.IsMatch(undirectedGraphStr);
        // Console.WriteLine(inputIsValid.ToString(), undirectedGraphStr);
        return inputIsValid;
}

 /**
 * Checks if an input string is a valid directed graph. 
 **/
public bool isValidDirectedGraph(string directedGraphStr){
 string pattern;
    pattern = @"{{((\w+)*(\w+,)*)+},{((\(\w+,\w+\))*(\(\w+,\w+\),)*)*},\d+}"; //checks for directed graph format
    Regex reg = new Regex(pattern);
    bool inputIsValid = reg.IsMatch(directedGraphStr);
    return inputIsValid;

}

 /**
 * Checks if input is a directed or undirected graph and then if it is, returns a list of edges that the graph contains.
 **/
public List<Edge> getGraphEdgeList(string graphString){
    List<Edge> edgeList;
    if(isValidUndirectedGraph(graphString)){
        string edgePattern = @"{(({\w+,\w+})*({\w+,\w+},)*)*}"; //outer edge pattern. from {{a,b,...,z},{{a,b},{c,d},...,{y,z}},k} --> {{a,b},{b,c},...,{y,z}}. Ie. removes nodes and k from a graph.
        edgeList =edgesGivenValidGraphAndPattern(graphString, edgePattern);
        }
    
    else if(isValidDirectedGraph(graphString)){

        string edgePattern = @"{((\(\w+,\w+\))*(\(\w+,\w+\),)*)*}";  //outer edge pattern. from {{a,b,...,z},{(a,b),(c,d),...,(y,z)},k} --> {(a,b),(b,c),...,(y,z)}
        edgeList = edgesGivenValidGraphAndPattern(graphString,edgePattern);
    }
    else{
        throw new ArgumentException("Invalid Input",graphString);
    }
    return edgeList;
}

public List<string> getNodeList(string graphString){
        List<string> nodeList = new List<string>();
        if(isValidUndirectedGraph(graphString)){
        string nodePatternOuter = @"{{((\w+)*(\w+,)*)+},{";
            nodeList = nodesGivenValidGraphAndPattern(graphString, nodePatternOuter);
        }
        return nodeList;
    }


/**
* Helper parser method for getGraphEdgeList();
**/
public List<Edge> edgesGivenValidGraphAndPattern(string validGraphStr,string edgePattern){
    List<Edge> edgeList = new List<Edge>();
    MatchCollection eMatches = Regex.Matches(validGraphStr,edgePattern);
    string edgeStr = eMatches[0].ToString();
    string edgePatternInner = @"\w+,\w+"; //inner edge patten. Spots any "a,b" pattern from {{a,b},{b,c},...,{y,z}} (directed or undirected)
    MatchCollection eMatches2 = Regex.Matches(edgeStr,edgePatternInner);
    foreach(Match medge in eMatches2){
        string[] edgeSplit = medge.ToString().Split(','); //splits "a,b" string literal into ["a","b"] array.
        Node n1 = new Node(edgeSplit[0]);
        Node n2 = new Node(edgeSplit[1]);
        edgeList.Add(new Edge(n1,n2)); //creates an edge from the array positions. 
    }
    return edgeList;

}

/// <summary>
/// Given a graph string, returns a string list of the nodes. Doesn't do graph initialization, this is pure regex.
/// Note that node names with trailing whitespace may be affected strangely. 
/// </summary>
/// <param name="validGraphStr"></param>
/// <param name="nodePattern"></param>
/// <returns></returns>
private List<string> nodesGivenValidGraphAndPattern(string validGraphStr,string nodePattern){
        List<string> retList = new List<string>();


        try
        {
            MatchCollection eMatches = Regex.Matches(validGraphStr, nodePattern);
            string matchedStr = eMatches[0].ToString();
            string parsedInput = matchedStr.Replace('{', ' ').Replace('}', ' ').TrimStart().TrimEnd().TrimEnd(',').TrimEnd(); //takes out starting {{ and ending },}
            string[] splitArr = parsedInput.Split(',');
            foreach (string nStr in splitArr)
            {
                retList.Add(nStr);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Invalid input GraphParser nodesGivenValidGraphAndPattern");
        }
        return retList;
    }


/// <summary>
/// Given a list of nodes in the string format {a,b,c} 
/// returns a list of strings ["a","b","c"]
/// </summary>
/// <returns></returns>
/// <remarks>
/// only supports word characters  (multicharacter supported) currently, not special characters or ! symbols.
/// </remarks>
public List<string> getNodesFromNodeListString(string input){
        List<string> retList = new List<string>();

        try{
        string pattern = @"{(\w+)(,\w+)*}";
        MatchCollection matches = Regex.Matches(input,pattern);
        string foundString = matches[0].ToString();
        string innerPattern = @"(\w+)(,\w+)*";
        MatchCollection matchesInner = Regex.Matches(input,innerPattern);
        string foundString2 = matchesInner[0].ToString();
            string[] splitStr = foundString2.Split(',');
            foreach(string n in splitStr){
                retList.Add(n);
            }


        }
        catch(Exception e){
            Console.WriteLine("Invalid input GraphParser getNodesFromNodeListString");
        }
        

        return retList;
    }
    
    /// <summary>
    /// Despite the complex name, all this does is take a string input in list format eg. "{a,b,c}" and turns it into a list of strings. 
    /// Be careful using this because it will also turn a graph string {{a,b,c},{(a,b},{b,c},0} into a list [a,b,c,a,b,b,c,0]
    /// </summary>
    /// <param name="input"></param>
    /// <returns> A list of strings</returns>
    public static List<string> parseNodeListWithStringFunctions(string input){
        List<string> retList = new List<string>();
        try{
            retList = input.Replace("{","").Replace("}","").Split(",").ToList();
            return retList;
        }
        catch(Exception e){
            Console.WriteLine("Invalid input GraphParser getNodesFromNodeListString");
        }
        return retList;
}

    public static List<KeyValuePair<string, string>> parseDirectedEdgeListWithStringFunctions(string input){
            List<KeyValuePair<string, string>> retList = new List<KeyValuePair<string, string>>();
            try{
                List<string> sList = input.Replace("{","").Replace("}","").Replace(" ","").Replace("),(","|").Split("|").ToList();
                foreach(string s in sList){
                    string k = s.Split(",")[0].Replace("(","").Replace(")","");
                    string v = s.Split(",")[1].Replace("(","").Replace(")","");
                    retList.Add( new KeyValuePair<string, string>(k,v));
                }
                return retList;
            }
            catch(Exception e){
                Console.WriteLine("Invalid input GraphParser getNodesFromNodeListString");
            }
            return retList;
    }

    
    public static List<KeyValuePair<string, string>> parseUndirectedEdgeListWithStringFunctions(string input){
            List<KeyValuePair<string, string>> retList = new List<KeyValuePair<string, string>>();
            try{
                List<string> sList = input.Replace("{{", "").Replace("}}","").Split("},{").ToList();
                foreach(string s in sList){
                    List<string> currentEdge = s.Split(",").ToList();
                    string k = currentEdge[0];
                    string v = currentEdge[1];
                    retList.Add( new KeyValuePair<string, string>(k,v));
                    retList.Add( new KeyValuePair<string, string>(v,k));
                }
                return retList;
            }
            catch(Exception e){
                Console.WriteLine("Invalid input GraphParser getNodesFromNodeListString");
            }
            return retList;
    }

}