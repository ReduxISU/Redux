namespace API.Interfaces;

// Thrown when a problem's instance constructor cannot parse the supplied
// instance string. Controllers translate this to HTTP 400 with the
// problem's `instanceFormat` as the structured hint.
internal class ProblemParseException : Exception {
    public string ProblemName { get; }
    public string Received { get; }
    public ProblemParseException(string problemName, string received, string? detail = null)
        : base(detail ?? $"could not parse {problemName} instance") {
        ProblemName = problemName;
        Received = received;
    }
}

// Thrown when a verifier cannot parse the supplied certificate string.
// Controllers translate this to HTTP 400 with the problem's
// `certificateFormat` as the structured hint. Carries the problem
// instance (already constructed successfully) so the controller can
// read its certificateFormat directly.
internal class CertificateParseException : Exception {
    public IProblem Problem { get; }
    public string Received { get; }
    public CertificateParseException(IProblem problem, string received, string? detail = null)
        : base(detail ?? $"could not parse {problem.problemName} certificate") {
        Problem = problem;
        Received = received;
    }
}
