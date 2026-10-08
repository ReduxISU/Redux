using System.Text.Json.Serialization;
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_LONGESTPATH.Verifiers;

class LongestPathVerifier : IVerifier<LONGESTPATH> {

    // --- Fields ---
    public string verifierName { get; } = "Default Longest Path Verifier";
    public string verifierDefinition { get; } = "TODO";
    public string source { get; } = "TODO";
    public string sourceLink { get; } = "TODO";
    public string[] contributors { get; } = { "TODO" };
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = null;
    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public LongestPathVerifier() {

    }

    public bool verify(LONGESTPATH problem, string certificate) {
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

        // TODO: implement Default Longest Path Verifier for Longest Path
        // An unfinished verifier must fail loudly, not accept every certificate.
        throw new NotImplementedException("TODO: implement Default Longest Path Verifier for Longest Path");
    }
}
