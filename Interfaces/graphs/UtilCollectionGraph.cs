using SPADE;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.Graphs;

namespace API.Interfaces;

/// <summary>
/// Help class from graph operations, and for converting to API ready graph for visualizations on the front end
/// </summary>
/// Expects graphs in the following formats:
/// unweighted undirected: (N,E) where N is set of nodes, E is a set of edges represented as {a,b}
/// unweighted directed  : (N,E) where N is set of nodes, E is a set of edges represented as (a,b)
/// weighted undirected  : (N,E) where N is set of nodes, E is a set of edges represented as ({a,b}, w) where w is the weight
/// weighted directed    : (N,E) where N is set of nodes, E is a set of edges represented as ((a,b), w) where w is the weight
/// Child of Graph class to fix some typing issues while codebase is converted. Expected to be removed
class UtilCollectionGraph : Graph {
    public UtilCollection Nodes;

    public UtilCollection Edges;

    bool IsDirected;
    bool IsWeighted;

    public UtilCollectionGraph(UtilCollection n, UtilCollection e) {
        Nodes = n;
        Edges = e;

        if (Edges.Count() == 0) {
            IsWeighted = false;
            IsDirected = false;
            return;
        }

        UtilCollection EdgeExample = Edges.ToList()[0];

        if (!EdgeExample.IsOrdered() || EdgeExample[0].IsValue()) {
            IsWeighted = false;
            IsDirected = EdgeExample.IsOrdered();
        } else {
            IsWeighted = true;
            IsDirected = EdgeExample[0].IsOrdered();
        }
    }

    // This transitional class represents its data as UtilCollections and overrides
    // ToAPIGraph directly, so it never builds the Node/Edge view of the base class.
    // These getters intentionally return null; null-forgiving (null!) keeps the
    // non-nullable base contract while preserving the existing serialized output,
    // which emits these as null (e.g. the /info endpoint reflects over them).
    public override List<Node> nodes => null!;

    public override List<Edge> edges => null!;

    public override API_GraphJSON ToAPIGraph() {
        //nodes are always the same
        List<string> nodes = Nodes.ToList().Select(node => node.ToString()).ToList();
        //edges need special handling based on case
        List<KeyValuePair<string, string>> edges;
        List<UtilCollection> EdgeList = Edges.ToList();

        API_GraphJSON graph;

        if (IsDirected) {
            if (IsWeighted) {
                edges = EdgeList.Select(edge => {
                    return new KeyValuePair<string, string>(edge[0][0].ToString(), edge[0][1].ToString());
                }).ToList();

                graph = new API_GraphJSON(nodes, edges);

                for (int i = 0; i < graph.links.Count; i++) {
                    graph.links[i].weight = EdgeList[i][1].ToString();
                }

                foreach (var link in graph.links) {
                    link.directed = true;
                    link.weighted = true;
                }

            } else {
                edges = EdgeList.Select(edge => {
                    return new KeyValuePair<string, string>(edge[0].ToString(), edge[1].ToString());
                }).ToList();

                graph = new API_GraphJSON(nodes, edges);

                foreach (var link in graph.links) {
                    link.directed = true;
                    link.weighted = false;
                }
            }
        } else {
            if (IsWeighted) {
                edges = EdgeList.Select(edge => {
                    List<UtilCollection> cast = edge[0].ToList();
                    if (cast.Count == 1) // self edge is a set with only one element, since {1,1} = {1}
                    {
                        return new KeyValuePair<string, string>(cast[0].ToString(), cast[0].ToString());
                    }
                    return new KeyValuePair<string, string>(cast[0].ToString(), cast[1].ToString());
                }).ToList();

                graph = new API_GraphJSON(nodes, edges);

                for (int i = 0; i < graph.links.Count; i++) {
                    graph.links[i].weight = EdgeList[i][1].ToString();
                    graph.links[i].weighted = true;
                }

            } else //default case
              {

                edges = EdgeList.Select(edge => {
                    List<UtilCollection> cast = edge.ToList();
                    if (cast.Count == 1) // self edge is a set with only one element, since {1,1} = {1}
                    {
                        return new KeyValuePair<string, string>(cast[0].ToString(), cast[0].ToString());
                    }
                    return new KeyValuePair<string, string>(cast[0].ToString(), cast[1].ToString());
                }).ToList();

                graph = new API_GraphJSON(nodes, edges);
            }
        }
        return graph;
    }

    // Refuses an undirected edge from a node to itself (#836, #882). SPADE reads the loop {a,a} as the
    // one-node set {a}, and ({a,a},w) as ({a},w), so reading the edge's second end failed with an index
    // error. Call it on E before reading the edges.
    public static void RefuseLoops(UtilCollection edges, string problemName, string received) {
        foreach (UtilCollection edge in edges.ToList()) {
            // A weighted edge is the pair (ends, weight); an unweighted one is the set of its ends.
            List<UtilCollection> ends = (edge.IsOrdered() ? edge[0] : edge).ToList();
            if (ends.Count == 1)
                throw new ProblemParseException(problemName, received, $"edge {{{ends[0]},{ends[0]}}} joins a node to itself; {problemName} has no loops");
        }
    }

    public UtilCollectionGraph removeEdges(UtilCollection removedEdges) {
        HashSet<UtilCollection> edgeset = Edges.ToList().ToHashSet();
        edgeset.ExceptWith(removedEdges.ToList().ToHashSet());
        return new(Nodes, new UtilCollection(edgeset));
    }

    public override string ToString() {
        return $"({Nodes},{Edges})";
    }

}