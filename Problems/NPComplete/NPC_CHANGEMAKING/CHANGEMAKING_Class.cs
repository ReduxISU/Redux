using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_CHANGEMAKING.Solvers;
using API.Problems.NPComplete.NPC_CHANGEMAKING.Verifiers;
using SPADE;

namespace API.Problems.NPComplete.NPC_CHANGEMAKING;

class CHANGEMAKING : IProblem<ChangeMakingDP, ChangeMakingVerifier, DummyVisualization> {

    // --- Fields ---
    public string problemName { get; } = "Change-Making";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Change-making_problem";
    public string formalDefinition { get; } = "Change-Making = {<C, X, K> | C is a set of positive integer coin values, X and K are integers, and there are nonnegative integers a_c for each c in C with sum(a_c * c) = X and sum(a_c) <= K}";
    public string problemDefinition { get; } = "Given coin values c_1, ..., c_m (each usable any number of times), a target amount X, and a number K, can you make exactly X using at most K coins?";
    public string inputDescription { get; } = "C, a set of positive coin values, X, a positive target amount, and K, the maximum number of coins";
    public string outputDescription { get; } = "True or False, whether X can be made exactly with at most K coins from C";
    public string source { get; } = "Lueker, George S. Two NP-complete problems in nonnegative integer programming. Technical Report 178, Computer Science Laboratory, Princeton University, 1975. As cited in Kozen, Dexter and Zaks, Shmuel. Optimal bounds for the change-making problem. Theoretical Computer Science 123(2), 1994. 377-388.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://www.cs.cornell.edu/~kozen/Papers/change";
    public string[] contributors { get; } = { "Jason L. Wright" };
    public const string InstanceGrammar = "{(C,X,K) | C subset int, X is int, K is int}";
    // Greedy picks 25+10+1+1+1+1 (six coins); the optimum is 25+7+7 (three).
    public static string _defaultInstance { get; } = "({1,7,10,25},39,3)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;
    public string instanceFormat { get; } = $"Format: {InstanceGrammar} (coins positive, X >= 1, K >= 0) Example: {_defaultInstance}";
    public string certificateFormat { get; } =
        $"Format: {ChangeMakingVerifier.CertificateGrammar} Example: {ChangeMakingVerifier.CertificateExample}";

    public string wikiName { get; } = "";
    public ChangeMakingDP defaultSolver { get; } = new ChangeMakingDP();
    public ChangeMakingVerifier defaultVerifier { get; } = new ChangeMakingVerifier();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();
    // Declared, not derived. NP-complete when the coin values are part of the input (Lueker, 1975);
    // in NP because a certificate of per-coin counts is polynomial in the bit length of X.
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPComplete;
    public ProblemType problemType { get; } = ProblemType.MathematicalProgramming;
    public ProblemVariant problemVariant { get; } = ProblemVariant.Decision;

    // --- Properties ---
    public List<int> C { get; set; } = new List<int>();
    public int X { get; set; }
    public int K { get; set; }

    // --- Methods Including Constructors ---
    public CHANGEMAKING() : this(_defaultInstance) {

    }

    public CHANGEMAKING(string instance) {
        this.instance = instance;

        StringParser parser = new(InstanceGrammar);
        List<string> parsedC;
        string xStr;
        string kStr;
        try {
            parser.parse(instance);
            // Materialize inside the try: SPADE may parse without error and only
            // fail when the result is enumerated or a missing key is accessed.
            parsedC = parser["C"].ToList().Select(x => x.ToString()).ToList();
            xStr = parser["X"].ToString();
            kStr = parser["K"].ToString();
        } catch (Exception ex) {
            throw new ProblemParseException(problemName, instance, ex.Message);
        }

        foreach (string element in parsedC) {
            if (!int.TryParse(element, out int coin))
                throw new ProblemParseException(problemName, instance,
                    $"'{element}' is not a valid integer in C");
            if (coin <= 0)
                throw new ProblemParseException(problemName, instance,
                    $"coin value {coin} must be positive");
            C.Add(coin);
        }

        if (!int.TryParse(xStr, out int x))
            throw new ProblemParseException(problemName, instance,
                $"'{xStr}' is not a valid integer for X");
        // X = 0 is trivially yes with no coins; requiring X >= 1 keeps "{}" free to mean "no solution".
        if (x <= 0)
            throw new ProblemParseException(problemName, instance,
                $"target X = {x} must be positive");
        X = x;

        if (!int.TryParse(kStr, out int k))
            throw new ProblemParseException(problemName, instance,
                $"'{kStr}' is not a valid integer for K");
        if (k < 0)
            throw new ProblemParseException(problemName, instance,
                $"coin limit K = {k} must be nonnegative");
        K = k;
    }
}
