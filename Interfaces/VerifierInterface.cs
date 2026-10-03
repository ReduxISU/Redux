namespace API.Interfaces;

interface IVerifier {
    string verifierName { get; }
    string verifierDefinition { get; }
    string source { get; }
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
        return ParseGuard.VerifyCertificate(problemInstance, certificate, c => verify(problemInstance, c));
    }
    bool verify(T problem, string certificate);
}
