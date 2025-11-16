using API.Interfaces;
using System.Linq;
using API.Interfaces.Graphs.GraphParser;
using SPADE;
using System.Runtime.ConstrainedExecution;

namespace API.Problems.NPComplete.NPC_NODESET.Verifiers;

class NodeSetVerifier : IVerifier<NODESET>
{

    // --- Fields ---
    public string verifierName { get; } = "Node Set Verifier";
    public string verifierDefinition { get; } = "This is a verifier for the Node Set problem";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };


    private string _certificate = "";

    public string certificate
    {
        get
        {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public NodeSetVerifier()
    {

    }

    public UtilCollection toEdges(string certificate, NODESET problem)
    {
        UtilCollection edges = new("{}");
        UtilCollection cert = new(certificate);
        foreach (UtilCollection node in cert)
        {
            foreach (UtilCollection edge in problem.graph.Edges)
            {
                if (edge[0].Equals(node) || edge[1].Equals(node))
                {
                    edges.Add(edge);
                }
            }
        }
        return edges;
    }

    private bool isACyclical(UtilCollectionGraph graph)
    {
        Dictionary<UtilCollection, HashSet<UtilCollection>> reachability = new();
        foreach (UtilCollection node in graph.Nodes)
        {
            HashSet<UtilCollection> neighbour = new();
            foreach (UtilCollection edge in graph.Edges)
            {
                if (edge[0].Equals(node))
                {
                    neighbour.Add(edge[1]);
                }
            }
            reachability.Add(node, neighbour);
        }


        bool updated = true; //update reachable nodes until no changes happen

        while (updated)
        {
            Dictionary<UtilCollection, HashSet<UtilCollection>> oldReachability = reachability.ToDictionary(
                kvp => kvp.Key,
                kvp => new HashSet<UtilCollection>(kvp.Value)
            );
            updated = false;

            foreach (KeyValuePair<UtilCollection, HashSet<UtilCollection>> entry in oldReachability)
            {
                foreach (UtilCollection targetNode in entry.Value)
                {
                    foreach (UtilCollection newReachable in oldReachability[targetNode])
                    {
                        if (newReachable.Equals(entry.Key)) return false;

                        if (!entry.Value.Contains(newReachable))
                        {
                            updated = true;
                            reachability[entry.Key].Add(newReachable);
                        }
                    }
                }
            }

        }

        return true;
    }

    public bool verify(NODESET problem, string certificate)
    {
        UtilCollectionGraph graph = problem.graph;

        UtilCollection cert = new(certificate);

        UtilCollection edgesToRemove = toEdges(certificate, problem);

        //Checks if certificate matches k-value;
        if (cert.Count() > problem.K)
        {
            return false;
        }
        graph = graph.removeEdges(edgesToRemove);

        return isACyclical(graph);
    }
}