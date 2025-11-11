using API.Interfaces;
using API.Interfaces.Graphs;
using SPADE;
namespace API.Problems.NPComplete.NPC_ARCSET.Verifiers;


class ArcSetVerifier : IVerifier<ARCSET> {
    public string verifierDefinition {get;} =  @"This Verifier takes in an arcset problem and a list of edges to remove from that problem. It removes those edges and then checks if the problem is still an instance of ARCSET
                                            ie. Does this input graph no longer have cycles after these input edges are removed? Returns true or false ";
    
    public string source {get;} = "";

    private string _certificate = "{(2,4)}"; //The certificate should be in the form of a set of directed edges
    public string[] contributors {get;} = {"Alex Diviney","Caleb Eardley","Russell Phillips"};

    // --- Properties ---
    public string verifierName { get; } = "Arc Set Verifier";
      public string certificate {
        get {
            return _certificate;
        }
    }



    public ArcSetVerifier()
    {

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

    /**
    * This method should take in an arcset problem and a list of edges to remove from that problem. It removes those edges and then checks if the problem is still an instance of ARCSET
    * ie. Does this input graph continue to have cycles after these input edges are removed? 
    **/
    public bool verify(ARCSET problem, string certificate){

        UtilCollectionGraph graph = problem.graph;

        UtilCollection cert = new(certificate);

        //Checks if certificate matches k-value;
        if(cert.Count() > problem.K){
            return false;
        }
        graph = graph.removeEdges(cert);

        return isACyclical(graph);
    }
}