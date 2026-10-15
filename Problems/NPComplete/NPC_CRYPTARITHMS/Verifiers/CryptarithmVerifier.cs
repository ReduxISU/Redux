using System.Text.Json.Serialization;
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CRYPTARITHMS.Verifiers;

class CryptarithmVerifier : IVerifier<CRYPTARITHMS> {

    // --- Fields ---
    public string verifierName {get;} = "Cryptarithm Verifier";
    public string verifierDefinition {get;} = "A type of verbal arithmetic puzzle in which a set of words is written down in the form of a long addition sum or some other mathematical problem. The object is to replace the letters of the alphabet with decimal digits to make a valid arithmetic sum.";
    public string source {get;} = "Eppstein, D. On the np-completeness of cryptarithms. Computer Science Department of Columbia University, New York, NY.";
    public string sourceLink {get;} = "https://dl.acm.org/doi/pdf/10.1145/24658.2466";
    public string[] contributors {get;} = { "Dani Sleight", "Brayden Weisbeck" };
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "NP-Complete";
    private string _certificate =  "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public CryptarithmVerifier() {
        
    }

    public bool verify(CRYPTARITHMS problem, string certificate){
        // On a malformed certificate, throw `CertificateParseException`
        // (see Interfaces/ParseExceptions.cs) rather than silently
        // returning false. The controller layer catches it and returns
        // HTTP 400 with `problem.certificateFormat` quoted back to the
        // caller. Example:
        //
        //   if (string.IsNullOrWhiteSpace(certificate)) {
        //       throw new CertificateParseException(problem, certificate, "certificate is empty");
        //   }
        //
        // Return false only when the certificate parses successfully but
        // fails the verification predicate; return true when it satisfies it.

        // TODO: implement C Verifier for C
        return true;
    }
}
