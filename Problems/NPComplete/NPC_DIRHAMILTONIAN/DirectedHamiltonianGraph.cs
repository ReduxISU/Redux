using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using API.Interfaces.Graphs;
namespace API.Problems.NPComplete.NPC_DIRHAMILTONIAN;

class DirectedHamiltonianGraph : DirectedGraph{


    public DirectedHamiltonianGraph(string arcInput) : base (arcInput){
        
    }

    public DirectedHamiltonianGraph(string graphStr,bool decoy){

        _nodeDict = new Dictionary<string,Node>();
        _adjacencyMatrix = new Dictionary<string, List<KeyValuePair<string, Node>>>();

        string pattern;
        pattern = @"\({(([\w!]+)+(,([\w!]+))*)},{(\(([\w!]+),([\w!]+)\)(,\(([\w!]+),([\w!]+)\))*)*}\)"; //checks for directed graph format
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

    public override string toDotJson()
    {
        throw new NotImplementedException();
    }
}