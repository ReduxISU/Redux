using System.Text.Json.Serialization;
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Verifiers;

class ConstraintSatisfactionVerifier : IVerifier<CONSTRAINTSATISFACTION> {

    // --- Fields ---
    public string verifierName {get;} = "Constraint Satisfaction Verifier";
    public string verifierDefinition {get;} = "TODO";
    public string source {get;} = " ";
    public string sourceLink {get;} = "TODO";
    public string[] contributors {get;} = { "TODO" };
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = null;
    private string _certificate =  "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public ConstraintSatisfactionVerifier() {
        
    }

    public bool verify(CONSTRAINTSATISFACTION problem, string certificate){
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

        // TODO: implement constraint satisfaction verifier for CONSTRAINTSATISFACTION
        return true;
    }
}
