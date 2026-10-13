using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01.Solvers;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01.Verifiers;
using SPADE;

namespace API.Problems.NPComplete.NPC_INTPROGRAMMING01;

class INTPROGRAMMING01 : IProblem<IntegerProgrammingBruteForce, GenericVerifier01INTP, DummyVisualization> {

    // --- Fields ---
    public string problemName { get; } = "0-1 Integer Linear Programming";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Integer_programming";
    public string formalDefinition { get; } = "0-1 ILP = {<C,d> | C is an m×n integer matrix, d is an integer m-vector, and there is an x ∈ {0,1}ⁿ such that Cx ≤ d}";
    public string problemDefinition { get; } = "0-1 Integer Linear Programming asks whether a system of linear inequalities has a solution in which every variable is either 0 or 1. The integer coefficient matrix C has one column per variable and one row per inequality, and this implementation writes every constraint as Cx <= d. It is the binary special case of general Integer Linear Programming, whose variables may be any integers.";
    public string inputDescription { get; } = "C, an integer matrix of coefficients, and d, an integer vector of bounds";
    public string outputDescription { get; } = "True or False, whether a 0-1 assignment x exists such that Cx <= d";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public const string InstanceGrammar = "(row1),...,(rowM)<=(d1 ... dM) | each row is n space-separated int coefficients (one row of C), d is the m-vector right-hand side";
    public static string _defaultInstance { get; } = "(-1 -1 0),(0 -1 -1),(1 1 1)<=(-1 -1 2)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instanceFormat { get; } = $"Format: {InstanceGrammar} Example: {_defaultInstance}";
    public string certificateFormat { get; } =
        $"Format: {GenericVerifier01INTP.CertificateGrammar} Example: {GenericVerifier01INTP.CertificateExample}";
    private List<List<int>> _C = new List<List<int>>();
    private List<int> _d = new List<int>();
    public string wikiName { get; } = "";
    public IntegerProgrammingBruteForce defaultSolver { get; } = new IntegerProgrammingBruteForce();
    public GenericVerifier01INTP defaultVerifier { get; } = new GenericVerifier01INTP();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();
    public string instance { get; set; } = string.Empty;
    public string[] contributors { get; } = { "Caleb Eardley", "Michael Trosper" };
    // Declared, not derived. INTPROGRAMMING01 (0-1 Integer Linear Programming) is NP-complete (Karp, 1972).
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPComplete;
    public ProblemType problemType { get; } = ProblemType.MathematicalProgramming;

    // --- Properties ---
    public List<List<int>> C {
        get {
            return _C;
        }
        set {
            _C = value;
        }
    }
    public List<int> d {
        get {
            return _d;
        }
        set {
            _d = value;
        }
    }

    // --- Methods Including Constructors ---
    public INTPROGRAMMING01() : this(_defaultInstance) {

    }
    public INTPROGRAMMING01(string instanceInput) {
        instance = instanceInput;
        C = getMatrixC(instance);
        d = getVectorD(instance);

        // Every row of C needs the same number of columns, and d needs one bound per row.
        if (C.Count != d.Count)
            throw new ProblemParseException("0-1 Integer Linear Programming", instanceInput,
                $"d has {d.Count} entries but C has {C.Count} rows; they must match");
        if (C.Any(row => row.Count != C[0].Count))
            throw new ProblemParseException("0-1 Integer Linear Programming", instanceInput,
                "every row of C must have the same number of coefficients");
    }

    private static string[] splitConstraintSides(string instance) {
        string[] sides = instance.Split("<=");
        if (sides.Length != 2)
            throw new ProblemParseException("0-1 Integer Linear Programming", instance,
                "expected exactly one '<=' separating C from d");
        return sides;
    }

    // Splits on any run of whitespace, so repeated spaces are tolerated.
    private static List<int> parseInts(string text, string instance) {
        List<int> values = new List<int>();
        foreach (string token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) {
            if (!int.TryParse(token, out int value))
                throw new ProblemParseException("0-1 Integer Linear Programming", instance, $"'{token}' is not an integer");
            values.Add(value);
        }
        if (values.Count == 0)
            throw new ProblemParseException("0-1 Integer Linear Programming", instance, "found an empty row or vector");
        return values;
    }

    private static string stripParens(string text) => text.Replace("(", " ").Replace(")", " ");

    public List<List<int>> getMatrixC(string G) {
        string[] rows = stripParens(splitConstraintSides(G)[0]).Split(",");
        List<List<int>> C = new List<List<int>>();
        foreach (string row in rows) {
            C.Add(parseInts(row, G));
        }
        return C;
    }

    public List<int> getVectorD(string G) {
        return parseInts(stripParens(splitConstraintSides(G)[1]), G);
    }
}
