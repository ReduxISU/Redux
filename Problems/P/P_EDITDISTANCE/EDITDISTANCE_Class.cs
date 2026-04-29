using API.Interfaces;
using API.Problems.P.P_EDITDISTANCE.Solvers;
using API.Problems.P.P_EDITDISTANCE.Verifiers;
using API.DummyClasses;
using SPADE;


namespace API.Problems.P.P_EDITDISTANCE;

class EDITDISTANCE : IProblem<EditDistanceDPSolver, EditDistanceVerifier, DummyVisualization>
{
    public string problemName { get; } = "Edit Distance";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Edit_distance";
    public string formalDefinition { get; } = "{(x, y, k) | x and y are strings, k is int, and there exists a sequence of k operations to transform x into y}";
    public string problemDefinition { get; } = "Find the minimum number of operations (insertion, deletion, substitution) required to transform one string into another.";
    public string source { get; } = "N/A";
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Edit_distance";
    public string wikiName { get; } = "";
    public static string _defaultInstance { get; } = "(\"horse\", \"ros\", 3)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;

    public EditDistanceDPSolver defaultSolver {get;} = new EditDistanceDPSolver();
    public EditDistanceVerifier defaultVerifier { get; } = new EditDistanceVerifier();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();
    public string[] contributors { get; } = { "Kaosi Ibeabuchi", "Diya Pandey", "Srijan Pant" };

    public string sourceString = "";
    public string targetString = "";

    public EDITDISTANCE() : this(_defaultInstance) { }

    public EDITDISTANCE(string instanceString)
    {
        instance = instanceString;

        StringParser parser = new("{(x, y, k) | x is string, y is string, k is int}");
        parser.parse(instanceString);

        sourceString = parser["x"].ToString();
        targetString = parser["y"].ToString();
    }
}
