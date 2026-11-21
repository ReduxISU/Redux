using API.Interfaces;
using API.Problems.NPHard.NPH_WEIGHTEDMAXSAT;
using API.Problems.NPHard.NPH_WEIGHTEDMAX3SAT.Verifiers;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAX3SAT;

class WEIGHTEDMAX3SAT : IProblem {
    // --- Fields ---
    public string problemName { get; } = "Weighted Max-3SAT";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Maximum_satisfiability_problem";
    public string formalDefinition { get; } = "Given weighted CNF clauses (C_i, w_i), each with 1–3 literal occurrences, find an assignment A maximizing sum of w_i over the clauses satisfied by A.";
    public string problemDefinition { get; } = "Weighted Max-3SAT restricts Weighted Max-SAT to nonempty clauses containing at most three literal occurrences, including repeated literals. Weights are nonnegative integers. Unit weights recover Max-3SAT, an NP-hard optimization problem.";
    public string inputDescription { get; } = "A conjunction of CNF clauses with 1–3 literal occurrences each, followed by a list of respective nonnegative integer weights";
    public string outputDescription { get; } = "A complete truth assignment and the total weight of the clauses it satisfies";
    public string source { get; } = "Vijay V. Vazirani, Approximation Algorithms, Chapter 16: Maximum Satisfiability, Springer, 2001.";
    public string sourceFile { get; } = SourceFile.Path();
    public string wikiName { get; } = "";
    public string[] contributors { get; } = { "Corbin Hay" };

    public static string _defaultInstance { get; } = "(((!x1 | x2) & (x3 | x4)),(1,2))";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance => weightedMaxSat.instance;
    public string instanceFormat { get; } = $"Format: {WEIGHTEDMAXSAT.InstanceGrammar}. Each clause of Φ has 1–3 literal occurrences; repetitions count separately. W has one nonnegative integer per clause. Example: {_defaultInstance}";
    public string certificateFormat { get; } = $"A tuple containing a complete comma-separated assignment and its satisfied weight. Booleans are True/False (T/F also accepted). Example: {WeightedMax3SatSolutionVerifier.CertificateExample}";
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPHard;
    public ProblemType problemType { get; } = ProblemType.Logic;
    public ProblemVariant problemVariant { get; } = ProblemVariant.Optimization;

    internal WEIGHTEDMAXSAT weightedMaxSat { get; }

    // --- Methods Including Constructors ---
    public WEIGHTEDMAX3SAT() : this(_defaultInstance) { }

    public WEIGHTEDMAX3SAT(string input) {
        try {
            weightedMaxSat = new WEIGHTEDMAXSAT(input);
        } catch (ProblemParseException ex) {
            throw new ProblemParseException(problemName, input, ex.Message, ex);
        }

        for (int i = 0; i < weightedMaxSat.weightedClauses.Count; i++) {
            int count = weightedMaxSat.weightedClauses[i].Clause.Literals.Count;
            if (count > 3)
                throw new ProblemParseException(problemName, input,
                    $"clause {i + 1} has {count} literal occurrences; each clause must contain 1–3");
        }
    }
}
