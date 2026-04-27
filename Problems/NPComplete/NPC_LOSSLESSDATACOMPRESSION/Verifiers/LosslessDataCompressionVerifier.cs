using API.Interfaces;

namespace API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Verifiers;

class LosslessDataCompressionVerifier : IVerifier<LOSSLESSDATACOMPRESSION> {

    // --- Fields ---
    public string verifierName {get;} = "Lossless Data Compression Verifier";
    public string verifierDefinition {get;} = "Verifies a proposed Huffman encoding by recomputing the frequency table from the input string, reconstructing the Huffman tree using the default solver, regenerating the prefix-free code table, and checking that the provided encoded bitstring matches the recomputed encoding exactly.";
    public string source {get;} = " ";
    public string sourceLink {get;} = "";
    public string[] contributors {get;} = { "Bektur Akkabakov", "Prem Shah" };
    private string _certificate =  "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public LosslessDataCompressionVerifier() {}

    public bool verify(LOSSLESSDATACOMPRESSION problem, string certificate){
        problem.defaultSolver.solve(problem);
        if(problem.encodedText == certificate){
            return true;
        }
        return false;
    }

    public string parseAfterColon(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        int colonIndex = input.LastIndexOf(':');

        if (colonIndex == -1 || colonIndex == input.Length - 1)
        {
            return string.Empty;
        }

        return input.Substring(colonIndex + 1);
    }
}
