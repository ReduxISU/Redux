namespace API.Interfaces;

interface IVerifier {
    string verifierName { get; }
    string verifierDefinition { get; }
    string source { get; }
    // Repo-relative path of the file declaring this class; see SourceFile.
    string sourceFile { get; }
    // Link to the formal citation in `source`. Optional, so it defaults to "". [JsonIgnore] keeps it out
    // of JSON written through an interface-typed member (e.g. /ProblemProvider/reduce), which never had
    // it; a class that declares its own sourceLink still shows it in /info.
    [System.Text.Json.Serialization.JsonIgnore]
    string sourceLink { get => ""; }
    string certificate { get; }
    string[] contributors { get; }

    bool verify(string problem, string certificate);
}

interface IVerifier<T> : IVerifier where T : IProblem {
    bool IVerifier.verify(string problem, string certificate) {
        // Should there be some sort of contraint that assures there is a constructor
        // that matches the signature of a single `string` argument?
        // Perhaps a static `FromInstance(string instance)` method for `IProblem` will work.
        // Constructor failures surface as ProblemParseException, certificate-parsing failures inside
        // verify(T, string) as CertificateParseException (see ParseGuard); both become a 400.
        T problemInstance = ParseGuard.CreateProblem<T>(problem);
        return ParseGuard.VerifyCertificate(problemInstance, certificate, () => verify(problemInstance, certificate));
    }
    bool verify(T problem, string certificate);
}
