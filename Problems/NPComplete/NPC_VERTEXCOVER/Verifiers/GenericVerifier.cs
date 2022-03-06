using API.Interfaces;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.Verifiers;

class GenericVerifier : IVerifier {

    // --- Fields ---
    private string _verifierName = "Generic Verifier";
    private string _verifierDefinition = "This is a verifier for VERTEXCOVER";
    private string _source = " ";

    // --- Properties ---
    public string verifierName {
        get {
            return _verifierName;
        }
    }
    public string verifierDefinition {
        get {
            return _verifierDefinition;
        }
    }
    public string source {
        get {
            return _source;
        }
    }

    // --- Methods Including Constructors ---
    public GenericVerifier() {
        
    }
}