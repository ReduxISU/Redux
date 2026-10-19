using System.Globalization;
using System.Numerics;
using API.Problems.NPHard.NPH_ILP.Solvers;
using API.Problems.NPHard.NPH_ILP.Verifiers;
using API.Problems.NPHard.NPH_ILP.Visualizations;
using API.Interfaces;
using SPADE;

namespace API.Problems.NPHard.NPH_ILP;

class INTEGERLINEARPROGRAMMING : IProblem<IntegerLinearProgrammingSolver, IntegerLinearProgrammingVerifier, IntegerLinearProgrammingDefaultVisualization> {
    public string problemName { get; } = "Integer Linear Programming";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Integer_programming";
    public string formalDefinition { get; } = "Given an integer matrix A and integer vectors b and c, maximize c^T x subject to Ax <= b and x in Z^n.";
    public string inputDescription { get; } = "An objective coefficient vector c, a constraint matrix A, and a bound vector b.";
    public string outputDescription { get; } = "An optimal integer vector (x1,...,xn), or infeasible, unbounded, or timeout. The visualization displays the objective value.";
    public string problemDefinition { get; } = "Integer Linear Programming optimizes a linear objective subject to linear inequalities and integer-valued variables. This representation maximizes c^T x with Ax <= b. Variables may be any integers; express nonnegativity or other bounds as additional constraint rows. A minimum objective can be represented by negating its coefficients.";
    public string source { get; } = "Gomory, R. E. (1958). Outline of an Algorithm for Integer Solutions to Linear Programs. Bulletin of the American Mathematical Society, 64(5), 275–278.";
    public string sourceLink { get; } = "https://doi.org/10.1090/S0002-9904-1958-10224-4";
    public string[] contributors { get; } = { "Prashant Pant" };
    public string sourceFile { get; } = SourceFile.Path();
    public string wikiName { get; } = "Integer_programming";
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPHard;
    public ProblemType problemType { get; } = ProblemType.MathematicalProgramming;

    public const string DefaultInstance = "((3,2),((1,1),(1,0),(0,1),(-1,0),(0,-1)),(4,2,3,0,0))";
    public string defaultInstance { get; } = DefaultInstance;
    public string instanceFormat { get; } = "Tuple (c,A,b), with comma-separated vectors and a matrix of row tuples. A must have b.Count rows and c.Count columns. Maximize c^T x over integer x with Ax <= b. Example: " + DefaultInstance;
    public string certificateFormat { get; } = "An integer vector (x1,...,xn). The verifier checks that Ax <= b and that the solution achieves the maximum objective value c^T x. Example: (2,2).";
    public string instance { get; set; } = string.Empty;
    public List<BigInteger> c { get; private set; } = new();
    public List<List<BigInteger>> A { get; private set; } = new();
    public List<BigInteger> b { get; private set; } = new();

    public IntegerLinearProgrammingSolver defaultSolver { get; } = new();
    public IntegerLinearProgrammingVerifier defaultVerifier { get; } = new();
    public IntegerLinearProgrammingDefaultVisualization defaultVisualization { get; } = new();

    public INTEGERLINEARPROGRAMMING() : this(DefaultInstance) { }

    public INTEGERLINEARPROGRAMMING(string instanceInput) {
        if (string.IsNullOrWhiteSpace(instanceInput))
            throw new ProblemParseException("INTEGERLINEARPROGRAMMING", instanceInput, "Instance is empty.");
        try {
            string text = instanceInput.Trim();
            if (!text.StartsWith('(') || !text.EndsWith(')') || text.IndexOfAny(new[] { '{', '}' }) >= 0)
                throw new FormatException("Expected a tuple (c,A,b) containing row and vector tuples.");
            var sections = new UtilCollection(instanceInput).ToList();
            if (sections.Count != 3)
                throw new FormatException("Expected (c,A,b).");
            c = ParseVector(sections[0]);
            A = sections[1].ToList().Select(ParseVector).ToList();
            b = ParseVector(sections[2]);
            if (c.Count == 0)
                throw new FormatException("The objective must contain at least one coefficient.");
            if (A.Count != b.Count || A.Any(row => row.Count != c.Count))
                throw new FormatException("A must have one row per bound and one column per objective coefficient.");
            instance = instanceInput;
        } catch (Exception ex) when (ex is not ProblemParseException) {
            throw new ProblemParseException("INTEGERLINEARPROGRAMMING", instanceInput, ex.Message, ex);
        }
    }

    internal static List<BigInteger> ParseVector(UtilCollection vector) =>
        vector.ToList().Select(value => BigInteger.Parse(value.ToString().Trim(), CultureInfo.InvariantCulture)).ToList();
}
