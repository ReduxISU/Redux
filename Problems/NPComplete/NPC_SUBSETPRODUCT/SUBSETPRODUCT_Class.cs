using API.Interfaces;
using API.Problems.NPComplete.NPC_SUBSETPRODUCT.Solvers;
using API.Problems.NPComplete.NPC_SUBSETPRODUCT.Verifiers;
using API.Problems.NPComplete.NPC_SUBSETPRODUCT.Visualizations;
using SPADE;

namespace API.Problems.NPComplete.NPC_SUBSETPRODUCT;

class SUBSETPRODUCT : IProblem<SubsetProductSolver, SubsetProductVerifier, SubsetProductVisualization> {

    // --- Fields ---
    public string problemName { get; } = "Subset Product";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Subset_sum_problem#Subset_product";
    public string formalDefinition { get; } = "Subset Product = <S, T> | S is a set of positive integers and there exists a subset K of S whose elements multiply to T";
    public string problemDefinition { get; } = "The problem is to determine whether some of the numbers multiply together to exactly the target T.";
    public string inputDescription { get; } = "S, a set of positive integers, and T, a target product";
    public string outputDescription { get; } = "True or False, whether some subset of S multiplies to T";
    public string source { get; } = "Garey, Michael R., and David S. Johnson. Computers and Intractability: A Guide to the Theory of NP-Completeness. W. H. Freeman, 1979. Problem SP14.";
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Computers_and_Intractability";
    public string[] contributors { get; } = { "Demo Student" };
    public const string InstanceGrammar = "{(S,T) | S subset int, T is int}";
    public static string _defaultInstance { get; } = "({2,3,5,7},30)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;
    public string instanceFormat { get; } = $"Format: {InstanceGrammar} Example: {_defaultInstance}";
    public string certificateFormat { get; } =
        $"Format: {SubsetProductVerifier.CertificateGrammar} Example: {SubsetProductVerifier.CertificateExample}";
    public string wikiName { get; } = "";
    public SubsetProductSolver defaultSolver { get; } = new SubsetProductSolver();
    public SubsetProductVerifier defaultVerifier { get; } = new SubsetProductVerifier();
    public SubsetProductVisualization defaultVisualization { get; } = new SubsetProductVisualization();
    // TODO: replace Unclassified. A test in redux-tests/Metadata fails until you do, on purpose. See Interfaces/ComplexityClass.cs for what each value means.
    public ComplexityClass complexityClass { get; } = ComplexityClass.Unclassified;
    // TODO: replace Unclassified. A test in redux-tests/Metadata fails until you do, on purpose. See Interfaces/ProblemType.cs for what each value means.
    public ProblemType problemType { get; } = ProblemType.Unclassified;

    // --- Properties ---
    public List<string> S { get; set; } = new List<string>();
    public int T { get; set; }

    // --- Methods and Constructors ---
    public SUBSETPRODUCT() : this(_defaultInstance) {

    }

    public SUBSETPRODUCT(string instance)
    {
            this.instance=instance;

        StringParser parser = new(InstanceGrammar);
        parser.parse(instance);
        S = parser["S"].ToList().Select(x => x.ToString()).ToList();
        T = int.Parse(parser["T"].ToString());
    }
}
