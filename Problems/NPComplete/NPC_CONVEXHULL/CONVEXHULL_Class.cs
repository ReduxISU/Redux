using System.Globalization;
using API.Interfaces;
using API.Problems.NPComplete.NPC_CONVEXHULL.Solvers;
using API.Problems.NPComplete.NPC_CONVEXHULL.Verifiers;
using API.Problems.NPComplete.NPC_CONVEXHULL.Visualizations;
using SPADE;

namespace API.Problems.NPComplete.NPC_CONVEXHULL;

class CONVEXHULL : IProblem<ConvexHullSolver, ConvexHullVerifier, ConvexHullVisualization> {

    // --- Fields ---
    public string problemName { get; } = "Convex Hull";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Convex_hull#Definitions";
    public string formalDefinition { get; } = "{X ⊆ ℝ² | output the extreme points of X forming the convex hull in counterclockwise order}";
    public string problemDefinition { get; } = "Given a set of points in the plane, compute the smallest convex polygon that contains all the points. The output is typically given as the vertices of the polygon in counterclockwise order. Points that lie on an edge of the hull but are not corners are not vertices and are left out.";
    public string inputDescription { get; } = "P, a set of points in the plane";
    public string outputDescription { get; } = "The vertices of the convex hull enclosing P, in counterclockwise order (any starting vertex is accepted by the verifier)";
    public string source { get; } = "de Berg, M.; van Kreveld, M.; Overmars, Mark; Schwarzkopf, O. (2008), Computational Geometry: Algorithms and Applications (3rd ed.), Springer";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://doi.org/10.1007/978-3-540-77974-2_1";
    public const string InstanceGrammar = "{(x,y)} | (x,y) are real-valued 2D points";
    private static readonly string _defaultInstance = "{(0,0),(4,0),(6,3),(4,6),(1,5),(-1,3),(2,2),(3,4),(1,1),(4,3)}";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;
    public string instanceFormat { get; } = $"Format: {InstanceGrammar} Example: {_defaultInstance}";
    public string certificateFormat { get; } =
        $"Format: {ConvexHullVerifier.CertificateGrammar} Example: {ConvexHullVerifier.CertificateExample}";
    public string wikiName { get; } = "";
    public ConvexHullSolver defaultSolver { get; } = new ConvexHullSolver();
    public ConvexHullVerifier defaultVerifier { get; } = new ConvexHullVerifier();
    public ConvexHullVisualization defaultVisualization { get; } = new ConvexHullVisualization();
    public string[] contributors { get; } = { "Bektur Akkabakov" };
    // Declared, not derived from the Problems/NPComplete/ folder — solvable in
    // polynomial time (e.g. Graham scan / gift wrapping); it just lives under
    // NPComplete/ for filing reasons.
    public SolverType solverType { get; } = SolverType.DivideAndConquer;
    public ComplexityClass complexityClass { get; } = ComplexityClass.P;
    public ProblemType problemType { get; } = ProblemType.ComputationalGeometry;
    // Variant (#616): Judgement call: "the smallest convex polygon" containing the points, though the hull is unique.
    public ProblemVariant problemVariant { get; } = ProblemVariant.Optimization;
    public List<(double x, double y)> points { get; set; }
    public List<(double x, double y)> convexHull { get; set; }
    public string solution { get; set; }

    // --- Properties ---
    // --- Methods and Constructors ---
    public CONVEXHULL() : this(_defaultInstance) {
    }

    public CONVEXHULL(string stringInstance) {
        points = ParsePoints(stringInstance);
        convexHull = new List<(double x, double y)>();
        solution = string.Empty;
    }

    public List<(double x, double y)> ParsePoints(string s) {
        // SPADE's tokenizer doesn't tolerate whitespace between elements, so
        // insignificant spacing is stripped before handing the structure
        // (the set of (x,y) tuples) to UtilCollection to parse.
        string compact = s.Replace(" ", "");

        List<(double x, double y)> pointsList = (ScanPointSet(compact) ?? SpadePoints(compact)).Select(point => {
            double x = double.Parse(point.x, CultureInfo.InvariantCulture);
            double y = double.Parse(point.y, CultureInfo.InvariantCulture);
            // double.Parse also reads NaN, Infinity and numbers too large for a double (as infinity); none is a point (#806).
            if (!double.IsFinite(x) || !double.IsFinite(y))
                throw new ProblemParseException("CONVEXHULL", s, $"point ({point.x},{point.y}) must have finite numbers for x and y");
            return (x, y);
        }).ToList();

        this.points = pointsList;
        return pointsList;
    }

    // The points as SPADE reads them: the set's elements in first-seen order, a repeated one dropped, each checked to be a
    // pair just before its numbers are read. SPADE takes time quadratic in the length of the instance (#815), so this is
    // only the fallback for what ScanPointSet does not read itself.
    private static IEnumerable<(string x, string y)> SpadePoints(string compact) {
        UtilCollection collection = new UtilCollection(compact);
        collection.assertUnordered();
        return collection.ToList().Select(point => {
            point.assertPair();
            return (point[0].ToString(), point[1].ToString());
        });
    }

    // The plain shape of an instance, {(x,y),(x,y),...} with every x and y made only of digits, signs, dots and exponent
    // marks, read in one pass (#815). It gives exactly what SPADE gives for that shape: the pairs' texts in first-seen
    // order, a pair whose two texts repeat an earlier one dropped. For anything else (other characters, nesting, a missing
    // or extra separator) it answers null, and SpadePoints reads the instance as before, errors included.
    internal static List<(string x, string y)>? ScanPointSet(string compact) {
        if (compact.Length < 2 || compact[0] != '{' || compact[^1] != '}') return null;
        var pairs = new List<(string x, string y)>();
        if (compact.Length == 2) return pairs;
        var seen = new HashSet<(string x, string y)>();
        int i = 1, end = compact.Length - 1;
        while (true) {
            if (compact[i] != '(') return null;
            string? x = ScanNumber(compact, i + 1, end, out i);
            if (x == null || i >= end || compact[i] != ',') return null;
            string? y = ScanNumber(compact, i + 1, end, out i);
            if (y == null || i >= end || compact[i] != ')') return null;
            if (seen.Add((x, y))) pairs.Add((x, y));
            i++;
            if (i == end) return pairs;
            if (compact[i] != ',' || ++i >= end) return null;
        }
    }

    // A non-empty run of digits, signs, dots and exponent marks starting at start, or null; next is where the run stops.
    private static string? ScanNumber(string s, int start, int end, out int next) {
        int i = start;
        while (i < end && (char.IsAsciiDigit(s[i]) || s[i] is '+' or '-' or '.' or 'e' or 'E')) i++;
        next = i;
        return i > start ? s[start..i] : null;
    }
}
