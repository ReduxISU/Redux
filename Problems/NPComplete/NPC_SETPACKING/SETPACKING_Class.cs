using API.Interfaces;
using API.Problems.NPComplete.NPC_SETPACKING.Solvers;
using API.Problems.NPComplete.NPC_SETPACKING.Verifiers;
using API.Problems.NPComplete.NPC_SETPACKING.Visualizations;
using SPADE;
using System.Collections.Generic;
using System.Linq;

namespace API.Problems.NPComplete.NPC_SETPACKING;

class SETPACKING : IGraphProblem<
    SetPackingBruteForce,
    SetPackingVerifier,
    SetPackingDefaultVisualization,
    UtilCollectionGraph>
{
    public string problemName { get; } = "Set Packing";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Set_packing";
    public string formalDefinition { get; } = "Given a collection of sets, determine whether there are K pairwise disjoint sets.";
    public string problemDefinition { get; } = "Set Packing asks whether K sets can be selected so that no two selected sets share an element.";
    public string source { get; } = "Wikipedia";
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Set_packing";
    public string wikiName { get; } = "Set_packing";
    public string[] contributors { get; } = { "Sansar Kharal" };

    private static string _defaultInstance =
        "({{S1:{a,b,c},S2:{b,d},S3:{c,e},S4:{d,e,f},S5:{f,g},S6:{a,g},S7:{h,i},S8:{i,j}}},3)";

    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;

    public Dictionary<string, List<string>> sets { get; set; } = new Dictionary<string, List<string>>();
    public List<string> setNames { get; set; } = new List<string>();

    private List<string> _nodes = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    private int _K;

    public SetPackingBruteForce defaultSolver { get; } = new SetPackingBruteForce();
    public SetPackingVerifier defaultVerifier { get; } = new SetPackingVerifier();
    public SetPackingDefaultVisualization defaultVisualization { get; } = new SetPackingDefaultVisualization();

    public UtilCollectionGraph graph { get; set; }

    public List<string> nodes
    {
        get { return _nodes; }
        set { _nodes = value; }
    }

    public List<KeyValuePair<string, string>> edges
    {
        get { return _edges; }
        set { _edges = value; }
    }

    public int K
    {
        get { return _K; }
        set { _K = value; }
    }

    public SETPACKING() : this(_defaultInstance)
    {
    }

    public SETPACKING(string input)
    {
        instance = input;
        parseSetPackingInstance(input);
        buildConflictGraph();
    }

    private void parseSetPackingInstance(string input)
    {
        sets.Clear();
        setNames.Clear();
        nodes.Clear();
        edges.Clear();

        input = input.Trim();

        if (input.StartsWith("("))
        {
            input = input.Substring(1);
        }

        if (input.EndsWith(")"))
        {
            input = input.Substring(0, input.Length - 1);
        }

        int lastComma = input.LastIndexOf(',');

        string setsPart = input.Substring(0, lastComma);
        string kPart = input.Substring(lastComma + 1);

        K = int.Parse(kPart.Trim());

        setsPart = setsPart.Trim();

        if (setsPart.StartsWith("{"))
        {
            setsPart = setsPart.Substring(1);
        }

        if (setsPart.EndsWith("}"))
        {
            setsPart = setsPart.Substring(0, setsPart.Length - 1);
        }

        string[] rawSets = setsPart.Split("},", System.StringSplitOptions.RemoveEmptyEntries);

        foreach (string rawSet in rawSets)
        {
            string cleaned = rawSet.Trim();
            cleaned = cleaned.Trim('{', '}');

            string[] parts = cleaned.Split(':');

            if (parts.Length != 2)
            {
                throw new System.Exception("Invalid Set Packing instance. Each set must use the format S1:{a,b,c}");
            }

            string setName = parts[0].Trim();

            List<string> elements = parts[1]
                .Trim('{', '}')
                .Split(',', System.StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .ToList();

            sets[setName] = elements;
            setNames.Add(setName);
            nodes.Add(setName);
        }
    }

    private void buildConflictGraph()
    {
        edges.Clear();

        for (int i = 0; i < setNames.Count; i++)
        {
            for (int j = i + 1; j < setNames.Count; j++)
            {
                string left = setNames[i];
                string right = setNames[j];

                if (sets[left].Intersect(sets[right]).Any())
                {
                    edges.Add(new KeyValuePair<string, string>(left, right));
                }
            }
        }

        string nodesString = "";

        foreach (string node in nodes)
        {
            nodesString += node + ",";
        }

        nodesString = nodesString.Trim(',');

        string edgesString = "";

        foreach (KeyValuePair<string, string> edge in edges)
        {
            edgesString += "{" + edge.Key + "," + edge.Value + "},";
        }

        edgesString = edgesString.Trim(',');

        string graphInput = "(({" + nodesString + "},{" + edgesString + "})," + K.ToString() + ")";

        StringParser graphParser = new StringParser("{((N,E),K) | N is set, E subset N unorderedcross N, K is int}");
        graphParser.parse(graphInput);

        graph = new UtilCollectionGraph(graphParser["N"], graphParser["E"]);
    }
}