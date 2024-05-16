using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_STEINERTREE.Verifiers;


class SteinerTreeVerifier : IVerifier
{

    // --- Fields ---
    private string _verifierName = "Steiner Tree Verifier";
    private string _verifierDefinition = "This is a verifier for Steiner Tree";
    private string _source = "Andrija Sevaljevic";
    private string[] _contributors = { "Andrija Sevaljevic" };


    private string _certificate = "";

    // --- Properties ---
    public string verifierName
    {
        get
        {
            return _verifierName;
        }
    }
    public string verifierDefinition
    {
        get
        {
            return _verifierDefinition;
        }
    }
    public string source
    {
        get
        {
            return _source;
        }
    }
    public string[] contributors
    {
        get
        {
            return _contributors;
        }
    }

    public string certificate
    {
        get
        {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public SteinerTreeVerifier()
    {

    }

    private Dictionary<string, List<string>> BuildAdjacencyList(List<KeyValuePair<string, string>> edges)
    {
        Dictionary<string, List<string>> adjacencyList = new Dictionary<string, List<string>>();

        foreach (var edge in edges)
        {
            if (!adjacencyList.ContainsKey(edge.Key))
                adjacencyList[edge.Key] = new List<string>();

            if (!adjacencyList.ContainsKey(edge.Value))
                adjacencyList[edge.Value] = new List<string>();

            adjacencyList[edge.Key].Add(edge.Value);
            adjacencyList[edge.Value].Add(edge.Key);
        }

        return adjacencyList;
    }

    private void DFS(string v, HashSet<string> visited, Dictionary<string, List<string>> adjacencyList)
    {
        visited.Add(v);

        foreach (string neighbor in adjacencyList[v])
        {
            if (!visited.Contains(neighbor))
                DFS(neighbor, visited, adjacencyList);
        }
    }

    public bool IsConnected(List<KeyValuePair<string, string>> edges)
    {
        Dictionary<string, List<string>> adjacencyList = BuildAdjacencyList(edges);
        HashSet<string> visited = new HashSet<string>();

        if (adjacencyList.Count == 0)
            return true; // Empty graph is considered connected

        string startVertex = adjacencyList.Keys.First();
        DFS(startVertex, visited, adjacencyList);

        // Check if all vertices were visited
        foreach (string vertex in adjacencyList.Keys)
        {
            if (!visited.Contains(vertex))
                return false; // Graph is not connected
        }

        return true;
    }


    public bool verify(STEINERTREE problem, string certificate)
    {
        List<string> order = certificate.Replace("{{", "").Replace("}}", "").Split("},{").ToList();
        List<string> check = new List<string>(problem.terminals);
        List<KeyValuePair<string, string>> edges = new List<KeyValuePair<string, string>>();

        for (int i = 0; i < order.Count; i++)
        {
            string edgeValues = order[i];
            string t = edgeValues.Split(',').ToList()[1];
            string s = edgeValues.Split(',').ToList()[0];
          
            KeyValuePair<string, string> edgePair = new KeyValuePair<string, string>(s, t);
            
            edges.Add(edgePair);
        }

        order = certificate.Replace("{","").Replace("}","").Split(',').ToList();

        foreach(var i in order) {
            if(check.Contains(i)) {
                check.Remove(i);
            }
        }


        if (IsConnected(edges) && !check.Any())
        {
            return true;
        }
        
        return false;
    }

}