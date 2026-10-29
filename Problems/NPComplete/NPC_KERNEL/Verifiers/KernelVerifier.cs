using System.Text.Json.Serialization;
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_KERNEL.Verifiers;

class KernelVerifier : IVerifier<KERNEL> {

    // --- Fields ---
    public const string CertificateGrammar = "TODO";
    public const string CertificateExample = "TODO";
    public string verifierName { get; } = "Kernel Verifier";
    public string verifierDefinition { get; } = "TODO";
    public string source { get; } = "TODO";
    public string sourceFile { get; } = SourceFile.Path();
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
    public KernelVerifier() {

    }

    public bool verify(KERNEL problem, string certificate) {
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

        // TODO: implement {VERIFIER} for {PROBLEM}
        // An unfinished verifier must fail loudly, not accept every certificate.
        throw new CertificateParseException(
            problem,
            certificate,
            "Kernel verifier is not implemented yet"
        );
    }
}