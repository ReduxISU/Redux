using API.Interfaces;
using API.Problems.NPComplete.NPC_EDITDISTANCE.Solvers;
using API.Problems.NPComplete.NPC_EDITDISTANCE.Verifiers;
using API.DummyClasses;
using SPADE;


namespace API.Problems.NPComplete.NPC_EDITDISTANCE;

// --- Fields, Properties, and Constructors ---
// Note: Edit Distance is a P problem, but we are treating it as NP-Complete Temporarily
// bc frontend not yet set up to handle P problems. 
class EDITDISTANCE : IProblem<EditDistanceDPSolver, EditDistanceVerifier, DummyVisualization> {
        public string problemName { get; } = "Edit Distance";
        public string problemLink { get; } = "https://en.wikipedia.org/wiki/Edit_distance";
        public string formalDefinition { get; } = "{(x,y,k) | x and y are strings, k is int, and there exists a sequence of k operations to transform x into y}";
        public string problemDefinition { get; } = "Find the minimum number of operations (insertion, deletion, substitution) required to transform one string into another.";
        public string source { get; } = "Arturs Backurs and P. Indyk, “Edit Distance Cannot Be Computed in Strongly Subquadratic Time (unless SETH is false),” DSpace@MIT (Massachusetts Institute of Technology), Jun. 2015";
        public string sourceLink { get; } = "https://dl.acm.org/doi/10.1145/2746539.2746612";
        public string wikiName { get; } = "";
        public static string _defaultInstance { get; } = "(horse, ros)";
        public string defaultInstance { get; } = _defaultInstance;
        public string instance { get; set; } = string.Empty;

        public EditDistanceDPSolver defaultSolver { get; } = new EditDistanceDPSolver();
        public EditDistanceVerifier defaultVerifier { get; } = new EditDistanceVerifier();
        public DummyVisualization defaultVisualization { get; } = new DummyVisualization();
        public string[] contributors { get; } = { "Kaosi Ibeabuchi", "Diya Pandey", "Srijan Pant" };
        // Declared, not derived from the Problems/NPComplete/ folder. The comment above
        // (this problem is solved with DP in polynomial time) already said as much; this
        // makes it a machine-checkable fact instead of a comment nobody reads.
        public ComplexityClass complexityClass { get; } = ComplexityClass.P;

        public string sourceString = "";
        public string targetString = "";

        public EDITDISTANCE() : this(_defaultInstance) { }

        public EDITDISTANCE(string instanceString) {
                instance = instanceString;

                string trimmed = instanceString.Trim().TrimStart('(').TrimEnd(')');
                string[] parts = trimmed.Split(',');

                sourceString = parts[0].Trim();
                targetString = parts[1].Trim();
        }
}
