using API.Interfaces;
using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Solvers;
using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Verifiers;
using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Visualizations;

namespace API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION;

class LOSSLESSDATACOMPRESSION : IProblem<LosslessDataCompressionSolver, LosslessDataCompressionVerifier, LosslessDataCompressionVisualization> {

    // --- Fields ---

    public string problemName { get; } = "Lossless Data Compression";

    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Lossless_compression";

    public string formalDefinition { get; } =
        "Given an input string S over some alphabet, find a binary encoding of S that uses a prefix-free code so that S can be decoded exactly back into the original string.";

    public string problemDefinition { get; } =
        "Lossless Data Compression is the problem of reducing the size of data while still allowing the original data to be perfectly reconstructed. " +
        "For this Redux contribution, the selected algorithm is Huffman Encoding. Huffman Encoding builds a prefix-free binary code where characters that appear more often usually receive shorter codes, " +
        "and characters that appear less often usually receive longer codes.";

    public string source { get; } =
        "David A. Huffman, A Method for the Construction of Minimum-Redundancy Codes, Proceedings of the IRE, 1952.";

    public string sourceLink { get; } =
        "https://doi.org/10.1109/JRPROC.1952.273898";

    private static readonly string _defaultInstance =
        "this is an example of lossless data compression using huffman encoding";

    public string defaultInstance { get; } = _defaultInstance;

    public string instance { get; set; } = string.Empty;

    public string wikiName { get; } = "";

    public LosslessDataCompressionSolver defaultSolver { get; } = new LosslessDataCompressionSolver();

    public LosslessDataCompressionVerifier defaultVerifier { get; } = new LosslessDataCompressionVerifier();

    public LosslessDataCompressionVisualization defaultVisualization { get; } = new LosslessDataCompressionVisualization();

    public string[] contributors { get; } = { "Prem Shah", "Bektur Akkabakov" };


    // this is the raw text we will wanna compress.
    public string inputText { get; set; } = string.Empty;

    // this stores the number of times each character appears.
    // examples: "banana" gives b:1, a:3, n:2.
    public Dictionary<char, int> frequencies { get; set; } = new Dictionary<char, int>();

    // this stores the Huffman code for each character.
    // examples: a -> 0, b -> 10, n -> 11.
    public Dictionary<char, string> codes { get; set; } = new Dictionary<char, string>();

    // this stores the final encoded binary string.
    public string encodedText { get; set; } = string.Empty;

    // this stores the final formatted answer returned by the solver.
    public string solution { get; set; } = string.Empty;

    // methods and constructors. 

    public LOSSLESSDATACOMPRESSION() : this(_defaultInstance) {

    }

    public LOSSLESSDATACOMPRESSION(string stringInstance) {
        instance = stringInstance ?? string.Empty;
        ParseInstance(instance);
    }

    private void ParseInstance(string stringInstance) {
        // for this problem, the instance is just the raw text that we want to compress (I think?).
        // ee are not using SPADE here because a plain string is easier for users to type in the GUI.
        inputText = stringInstance ?? string.Empty;

        // cleares these whenever a new instance is created.
        frequencies = new Dictionary<char, int>();
        codes = new Dictionary<char, string>();
        encodedText = string.Empty;
        solution = string.Empty;
    }
}