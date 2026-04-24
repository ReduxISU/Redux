using API.Interfaces;

namespace API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Verifiers;

class LosslessDataCompressionVerifier : IVerifier<LOSSLESSDATACOMPRESSION> {

    // --- Fields ---
    public string verifierName {get;} = "lossless data compression Verifier";
    public string verifierDefinition {get;} = "TODO";
    public string source {get;} = " ";
    public string sourceLink {get;} = "TODO";
    public string[] contributors {get;} = { "TODO" };
    private string _certificate =  "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public LosslessDataCompressionVerifier() {
        
    }

    public bool verify(LOSSLESSDATACOMPRESSION problem, string certificate){
        // TODO: implement lossless data compression Verifier for LOSSLESSDATACOMPRESSION
        return true;
    }
}
