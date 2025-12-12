using API.Interfaces;

namespace API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Verifiers;

class UnstructuredSearchVerifier : IVerifier<UNSTRUCTUREDSEARCH> {

    // --- Fields ---
    public string verifierName { get; } = "Unstructured search verifier";
    public string verifierDefinition {get;} = "TODO";
    public string source {get;} = " ";
    public string[] contributors { get; } = { "Jason L. Wright", "Alex Svancara" };
    private string _certificate =  "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public UnstructuredSearchVerifier()
    {
    }

    public bool verify(UNSTRUCTUREDSEARCH problem, string certificate){
        int i = Convert.ToInt32(certificate);
        // All we need to do is see if funcValues[certificate] is non-zero
        return problem.funcValues[i] != 0;
    }
}
