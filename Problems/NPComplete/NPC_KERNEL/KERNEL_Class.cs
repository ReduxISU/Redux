/*
* Kernel_Class.cs
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
    public string problemDefinition { get; } = "AThe Kernel Problem asks whether a directed graph contains a kernel. A kernel is a subset of vertices where no two verticesare joined by an arc, while every vertex outside the set points to at least one vertex inside the set ";
    public string inputDescription { get; } = "G, a directed graph";
    public string outputDescription { get; } = "True or False, whether G contains a kernel";
    public string source { get; } = "V. Chv´atal, “On the computational complexity of finding a kernel,” Centre de recherches math´ematiques, Uni-versit´e de Montr´eal, Rep. CRM-300, 1973.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://users.encs.concordia.ca/chvatal/kernel.html";
    private static string _defaultInstance = "({1,2,3,4},{(1,2),(2,3),(3,4),(4,1)})";
    

    // --- Properties ---
    // --- Methods Including Constructors ---
    public KERNEL() : this(_defaultInstance) {
    }

}
