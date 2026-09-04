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
    public string problemDefinition { get; } = "Given a set of points in the plane, compute the smallest convex polygon that contains all the points. The output is typically given as the vertices of the polygon in clockwise order.";
    public string source { get; } = "de Berg, M.; van Kreveld, M.; Overmars, Mark; Schwarzkopf, O. (2008), Computational Geometry: Algorithms and Applications (3rd ed.), Springer";
    public string sourceLink { get; } = "https://doi.org/10.1007/978-3-540-77974-2_1";
    public const string InstanceGrammar = "{(x,y)} | (x,y) are real-valued 2D points";
    private static readonly string _defaultInstance = "{(-0.49429993857731547,-0.14539088193288174),(0.7674622377407927,-0.21537444528240846),(-0.9308276577586132,-0.1423800479224624),(-0.6984449872706371,0.3857380723376367),(-0.27394905790800017,-0.7488048223660126),(0.6681904385614421,-0.11341930745315643),(0.6077591838324792,0.5288040272918157),(0.32371432910023556,0.12231914413229394),(-0.32705115386597394,0.6744065707101621),(0.2723211656942368,-0.8053758131859647)}";
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
        UtilCollection collection = new UtilCollection(s.Replace(" ", ""));
        collection.assertUnordered();

        List<(double x, double y)> pointsList = collection.ToList().Select(point => {
            point.assertPair();
            double x = double.Parse(point[0].ToString(), CultureInfo.InvariantCulture);
            double y = double.Parse(point[1].ToString(), CultureInfo.InvariantCulture);
            return (x, y);
        }).ToList();

        this.points = pointsList;
        return pointsList;
    }
}
