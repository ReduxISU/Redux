using API.Interfaces;
using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Solvers;
using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Verifiers;
using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Visualizations;

namespace API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION;

class LOSSLESSDATACOMPRESSION : IProblem<LosslessDataCompressionSolver, LosslessDataCompressionVerifier, LosslessDataCompressionVisualization> {

    // --- Fields ---
    public string problemName {get;} = "lossless data compression";
    public string problemLink {get;} = "TODO";
    public string formalDefinition {get;} = "TODO";
    public string problemDefinition {get;} = "TODO";
    public string source {get;} = "TODO";
    public string sourceLink {get;} = "TODO";
    private static readonly string _defaultInstance = "TODO";
    public string defaultInstance {get;} = _defaultInstance;
    public string instance {get;set;} = string.Empty;
    public string wikiName {get;} = "";
    public LosslessDataCompressionSolver defaultSolver {get;} = new LosslessDataCompressionSolver();
    public LosslessDataCompressionVerifier defaultVerifier {get;} = new LosslessDataCompressionVerifier();
    public LosslessDataCompressionVisualization defaultVisualization {get;} = new LosslessDataCompressionVisualization();
    public string[] contributors {get;} = { "TODO" };

    // --- Properties ---

    // --- Methods and Constructors ---
    public LOSSLESSDATACOMPRESSION() : this(_defaultInstance) {

    }

    public LOSSLESSDATACOMPRESSION(string stringInstance) {
        instance = stringInstance;



        // TODO: implement parsing of string instance of lossless data compression. SPADE is a class meant to help with this step, see https://github.com/Jetison333/SPADE/blob/main/Documentation/index.md for more information.
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
