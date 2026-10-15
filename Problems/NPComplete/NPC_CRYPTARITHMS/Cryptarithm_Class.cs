using API.Interfaces;
using API.Problems.NPComplete.NPC_CRYPTARITHMS.Solvers;
using API.Problems.NPComplete.NPC_CRYPTARITHMS.Verifiers;
using API.Problems.NPComplete.NPC_CRYPTARITHMS.Visualizations;

namespace API.Problems.NPComplete.NPC_CRYPTARITHMS;

class CRYPTARITHMS : IProblem<CryptarithmSolver, CryptarithmVerifier, CryptarithmVisualization> {

    // --- Fields ---
    public string problemName {get;} = "Cryptarithms";
    public string problemLink {get;} = "https://en.wikipedia.org/wiki/Verbal_arithmetic";
    public string formalDefinition {get;} = "TODO";
    public string problemDefinition {get;} = "A type of verbal arithmetic puzzle in which a set of words is written down in the form of a long addition sum or some other mathematical problem. The object is to replace the letters of the alphabet with decimal digits to make a valid arithmetic sum.";
    public string inputDescription {get;} = "Arithmetic equation made up of letters";
    public string outputDescription {get;} = "True or False";
    public string source {get;} = "Eppstein, D. On the np-completeness of cryptarithms. Computer Science Department of Columbia University, New York, NY.";
    public string sourceLink {get;} = "https://dl.acm.org/doi/pdf/10.1145/24658.2466";
    private static readonly string _defaultInstance = "SEND+MONEY=MONEY";
    public string defaultInstance {get;} = _defaultInstance;
    // Short descriptive sentence with a concrete example. Consumed by
    // /ProblemProvider/info and by parse-error responses; this is the
    // canonical place to tell an LLM (or a human, or the GUI) what
    // shape your instance/certificate strings take.
    public string instanceFormat {get;} = "TODO: describe the instance string format with an embedded example, e.g. 'Boolean formula in 3-CNF, clauses joined by `&`. Example: (x1 | !x2 | x3) & (!x1 | x3 | x1)'";
    public string certificateFormat {get;} = "TODO: describe the certificate string the verifier accepts with an embedded example, e.g. '(x1:True,x2:False,x3:True) -- one variable:Boolean pair per variable, comma-separated'";
    public string instance {get;set;} = string.Empty;
    public string wikiName {get;} = "";
    public CryptarithmSolver defaultSolver {get;} = new CryptarithmSolver();
    public CryptarithmVerifier defaultVerifier {get;} = new CryptarithmVerifier();
    public CryptarithmVisualization defaultVisualization {get;} = new CryptarithmVisualization();
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPComplete;
    public ProblemType problemType { get; } = ProblemType.SetsAndPartitions;
    public string[] contributors {get;} = { "Dani Sleight", "Brayden Weisbeck" };
    // Declared, not derived — do NOT infer this from which Problems/<Folder>/ this file
    // lives under. The folder is a filing convention only and is wrong for a dozen-plus
    // existing problems; declaring it explicitly here is the only source of truth the
    // API trusts. Leave as Unclassified (the default — this line can simply be deleted)
    // until someone has actually checked the literature/argued the class. Getting this
    // wrong is a public correctness claim the API will serve to callers.
    // public ComplexityClass complexityClass {get;} = ComplexityClass.TODO;

    // --- Properties ---

    // --- Methods and Constructors ---
    public CRYPTARITHMS() : this(_defaultInstance) {

    }

    public CRYPTARITHMS(string stringInstance) {
        instance = stringInstance;

        // On a malformed instance, throw `ProblemParseException` (see
        // Interfaces/ParseExceptions.cs). The controller layer catches it
        // and returns HTTP 400 with `instanceFormat` quoted back to the
        // caller. Example:
        //
        //   if (string.IsNullOrWhiteSpace(stringInstance)) {
        //       throw new ProblemParseException("C", stringInstance, "instance is empty");
        //   }

        // TODO: implement parsing of string instance of C. SPADE is a class meant to help with this step, see https://github.com/Jetison333/SPADE/blob/main/Documentation/index.md for more information.
        //
        // StringParser parser = new("{(i, w, v) | i subset int cross int, w is int, v is int}");
        // parser.parse(instance);
        // items = parser["i"];
        // W = int.Parse(parser["w"].ToString());
        // V = int.Parse(parser["v"].ToString());
        //
        // Or a unidirected unweighted graph example using SPADE
        // 
        // StringParser parser = new("{(N,E) | N is set, E subset N unorderdcross N}");
        // parser.parse(instance);
        // edges = parser["E"];
        // nodes = parser["N"];

    }
}
