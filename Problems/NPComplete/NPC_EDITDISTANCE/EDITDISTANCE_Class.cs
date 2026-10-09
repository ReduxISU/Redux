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
    public string inputDescription { get; } = "x and y, two strings";
    public string outputDescription { get; } = "The minimum number of single-character edit operations required to transform x into y";
    public string source { get; } = "Arturs Backurs and P. Indyk, “Edit Distance Cannot Be Computed in Strongly Subquadratic Time (unless SETH is false),” DSpace@MIT (Massachusetts Institute of Technology), Jun. 2015";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://dl.acm.org/doi/10.1145/2746539.2746612";
    public string wikiName { get; } = "";
    public static string _defaultInstance { get; } = "(horse, ros)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;
    public const string InstanceGrammar = "(x, y) | x,y are strings; a string containing a comma, quote, or leading/trailing space is written in double quotes with \\\" for a quote and \\ for a backslash, e.g. (\"a,b\", c); an optional third field k (an integer) is accepted and ignored";
    public string instanceFormat { get; } = $"Format: {InstanceGrammar} Example: {_defaultInstance}";
    public string certificateFormat { get; } =
        $"Format: {EditDistanceVerifier.CertificateGrammar} Example: {EditDistanceVerifier.CertificateExample}";

    public EditDistanceDPSolver defaultSolver { get; } = new EditDistanceDPSolver();
    public EditDistanceVerifier defaultVerifier { get; } = new EditDistanceVerifier();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();
    public string[] contributors { get; } = { "Kaosi Ibeabuchi", "Diya Pandey", "Srijan Pant" };
    // Declared, not derived from the Problems/NPComplete/ folder. The comment above
    // (this problem is solved with DP in polynomial time) already said as much; this
    // makes it a machine-checkable fact instead of a comment nobody reads.
    public ComplexityClass complexityClass { get; } = ComplexityClass.P;
    public ProblemType problemType { get; } = ProblemType.StorageAndRetrieval;

    public string sourceString = "";
    public string targetString = "";

    public EDITDISTANCE() : this(_defaultInstance) { }

    public EDITDISTANCE(string instanceString) {
        instance = instanceString;

        List<string> fields = splitFields(instanceString);
        if (fields.Count is not (2 or 3))
            throw new ProblemParseException("Edit Distance", instanceString,
                $"expected (x, y) but found {fields.Count} comma-separated field(s)");
        if (fields.Count == 3 && !int.TryParse(fields[2].Trim(), out _))
            throw new ProblemParseException("Edit Distance", instanceString,
                "a third field must be an integer k; put strings containing commas in double quotes");

        sourceString = fields[0];
        targetString = fields[1];
    }

    // Splits "(x, y)" into its fields on commas that are outside double quotes. A quoted field keeps
    // its contents verbatim (\" is a quote, \\ a backslash); an unquoted field is trimmed.
    private static List<string> splitFields(string instanceString) {
        string text = instanceString.Trim();
        if (text.StartsWith('(') && text.EndsWith(')'))
            text = text.Substring(1, text.Length - 2);

        List<string> fields = new List<string>();
        int pos = 0;
        while (true) {
            while (pos < text.Length && text[pos] == ' ') pos++;
            string field;
            if (pos < text.Length && text[pos] == '"') {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                pos++;
                bool closed = false;
                while (pos < text.Length) {
                    char c = text[pos++];
                    if (c == '\\' && pos < text.Length && (text[pos] == '"' || text[pos] == '\\')) {
                        sb.Append(text[pos++]);
                    } else if (c == '"') {
                        closed = true;
                        break;
                    } else {
                        sb.Append(c);
                    }
                }
                if (!closed)
                    throw new ProblemParseException("Edit Distance", instanceString, "unterminated double quote");
                while (pos < text.Length && text[pos] == ' ') pos++;
                if (pos < text.Length && text[pos] != ',')
                    throw new ProblemParseException("Edit Distance", instanceString, "unexpected text after a closing quote");
                field = sb.ToString();
            } else {
                int end = text.IndexOf(',', pos);
                if (end < 0) end = text.Length;
                field = text.Substring(pos, end - pos).Trim();
                if (field.Contains('"'))
                    throw new ProblemParseException("Edit Distance", instanceString, "a double quote must start a quoted string");
                pos = end;
            }
            fields.Add(field);
            if (pos >= text.Length) break;
            pos++; // the comma
        }
        return fields;
    }
}
