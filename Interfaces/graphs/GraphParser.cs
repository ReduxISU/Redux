
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace API.Interfaces.Graphs.GraphParser;

class GraphParser {

    // Node-list patterns are linear (no nested or overlapping quantifiers), but the input is
    // caller-supplied, so every match also runs under an explicit timeout as defense in depth.
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static readonly Regex NodeListPattern = new Regex(@"{(\w+)(,\w+)*}", RegexOptions.None, RegexTimeout);
    private static readonly Regex NodeRunPattern = new Regex(@"(\w+)(,\w+)*", RegexOptions.None, RegexTimeout);

    public GraphParser() {
    }

    /// <summary>
    /// Given a list of nodes in the string format {a,b,c} 
    /// returns a list of strings ["a","b","c"]
    /// </summary>
    /// <returns></returns>
    /// <remarks>
    /// only supports word characters  (multicharacter supported) currently, not special characters or ! symbols.
    /// </remarks>
    public List<string> getNodesFromNodeListString(string input) {
        MatchCollection matches;
        MatchCollection matchesInner;
        try {
            matches = NodeListPattern.Matches(input);
            if (matches.Count == 0)
                throw new ArgumentException("Input does not match expected node list format, e.g. {a,b,c}.", nameof(input));
            matchesInner = NodeRunPattern.Matches(input);
            if (matchesInner.Count == 0)
                throw new ArgumentException("Input does not contain valid node identifiers.", nameof(input));
        } catch (RegexMatchTimeoutException e) {
            // Treat input that takes too long to match as invalid rather than tying up the request.
            throw new ArgumentException("Input took too long to parse as a node list.", nameof(input), e);
        }
        List<string> retList = new List<string>();
        foreach (string n in matchesInner[0].ToString().Split(','))
            retList.Add(n);
        return retList;
    }

    /// <summary>
    /// Despite the complex name, all this does is take a string input in list format eg. "{a,b,c}" and turns it into a list of strings. 
    /// Be careful using this because it will also turn a graph string {{a,b,c},{(a,b},{b,c},0} into a list [a,b,c,a,b,b,c,0]
    /// </summary>
    /// <param name="input"></param>
    /// <returns> A list of strings</returns>
    public static List<string> parseNodeListWithStringFunctions(string input) {
        return input.Replace("{", "").Replace("}", "").Split(",").ToList();
    }

    public static List<KeyValuePair<string, string>> parseDirectedEdgeListWithStringFunctions(string input) {
        List<KeyValuePair<string, string>> retList = new List<KeyValuePair<string, string>>();
        List<string> sList = input.Replace("{", "").Replace("}", "").Replace(" ", "").Replace("),(", "|").Split("|").ToList();
        foreach (string s in sList) {
            string[] parts = s.Split(",");
            if (parts.Length < 2)
                throw new ArgumentException($"Expected directed edge pair '(a,b)', got '{s}'.", nameof(input));
            string k = parts[0].Replace("(", "").Replace(")", "");
            string v = parts[1].Replace("(", "").Replace(")", "");
            retList.Add(new KeyValuePair<string, string>(k, v));
        }
        return retList;
    }


    public static List<KeyValuePair<string, string>> parseUndirectedEdgeListWithStringFunctions(string input) {
        List<KeyValuePair<string, string>> retList = new List<KeyValuePair<string, string>>();
        List<string> sList = input.Replace("{{", "").Replace("}}", "").Split("},{").ToList();
        foreach (string s in sList) {
            string[] currentEdge = s.Split(",");
            if (currentEdge.Length < 2)
                throw new ArgumentException($"Expected undirected edge pair 'a,b', got '{s}'.", nameof(input));
            retList.Add(new KeyValuePair<string, string>(currentEdge[0], currentEdge[1]));
            retList.Add(new KeyValuePair<string, string>(currentEdge[1], currentEdge[0]));
        }
        return retList;
    }

}