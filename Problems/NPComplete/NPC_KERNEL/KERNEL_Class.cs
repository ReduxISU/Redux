/*
* KERNEL_Class.cs
* @author Prashant Pant
* @author Bektur Akkabakov
*/


using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_KERNEL.Solvers;
using API.Problems.NPComplete.NPC_KERNEL.Verifiers;
using API.Problems.NPComplete.NPC_KERNEL.Visualizations;
using SPADE;
namespace API.Problems.NPComplete.NPC_KERNEL;

class KERNEL : IGraphProblem<KernelBruteForce, KernelVerifier, KernelDefaultVisualization, UtilCollectionGraph> {

    // --- Fields ---
    public string problemName { get; } = "Kernel";
    public string problemLink { get; } = "https://users.encs.concordia.ca/chvatal/kernel.html";
    public string formalDefinition { get; } = "KERNEL = {<G> | G is a directed graph that has a kernel}";
    public string problemDefinition { get; } = "The Kernel Problem asks whether a directed graph contains a kernel. A kernel is a subset of vertices where no two vertices are joined by an arc, while every vertex outside the set points to at least one vertex inside the set ";
    public string inputDescription { get; } = "G, a directed graph";
    public string outputDescription { get; } = "True or False, whether G contains a kernel";
    public string source { get; } = "V. Chv´atal, “On the computational complexity of finding a kernel,” Centre de recherches math´ematiques, Uni-versit´e de Montr´eal, Rep. CRM-300, 1973.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://users.encs.concordia.ca/chvatal/kernel.html";
    public const string InstanceGrammar = "(V,E) | V is a set of nonnegative integer vertices; E is a set of directed edges (u,v), where u,v belong to V";
    private static readonly string _defaultInstance = "({0,1,2,3},{(0,1),(1,2),(2,3),(3,0)})";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;
    public string instanceFormat { get; } = $"Format: {InstanceGrammar} Example: {_defaultInstance}";
    public string certificateFormat { get; } =
        $"Format: {KernelVerifier.CertificateGrammar} Example: {KernelVerifier.CertificateExample}";
    public string wikiName { get; } = "";
    public KernelBruteForce defaultSolver { get; } = new KernelBruteForce();
    public KernelVerifier defaultVerifier { get; } = new KernelVerifier();
    public KernelDefaultVisualization defaultVisualization { get; } = new KernelDefaultVisualization();
    public string[] contributors { get; } = {"Prashant Pant", "Bektur Akkabakov"};
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPComplete;
    public ProblemType problemType { get; } = ProblemType.GraphTheory;

    public HashSet<int> vertices { get; set; }
    public List<(int u, int v)> edges { get; set; }
    // --- Properties ---
    // --- Methods Including Constructors ---
    public KERNEL() : this(_defaultInstance) {
    }
    public KERNEL(string stringInstance) {
        if (string.IsNullOrWhiteSpace(stringInstance)) {
            throw new ProblemParseException("Kernel",stringInstance,"Instance is empty.");
        }
        instance = stringInstance;
        UtilCollection graph = new UtilCollection(stringInstance.Replace(" ", ""));
        graph.assertPair();

        // Parse vertices.
        UtilCollection vertexSet = graph[0];
        vertexSet.assertUnordered();

        vertices = vertexSet.ToList().Select(v => int.Parse(v.ToString())).ToHashSet();

        // Parse directed edges.
        UtilCollection edgeSet = graph[1];
        edgeSet.assertUnordered();

        edges = new List<(int u, int v)>();

        foreach (var edge in edgeSet.ToList()) {
            edge.assertPair();
            int u = int.Parse(edge[0].ToString());
            int v = int.Parse(edge[1].ToString());
            edges.Add((u, v));
        }

        // Validate edge endpoints.
        if (edges.Any(e => !vertices.Contains(e.u) || !vertices.Contains(e.v))) {
            throw new ProblemParseException("Kernel",stringInstance,"Every edge endpoint must belong to V.");
        }
    }
}
