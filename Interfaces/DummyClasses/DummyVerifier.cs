using API.Interfaces;

namespace API.DummyClasses;

// Default verifier of a problem with no [DefaultFor] verifier yet (see Interfaces/DefaultFor.cs).
// It can't honestly answer true or false, so verify reports that there is no verifier, which
// /ProblemProvider/verify turns into a 400.
class DummyVerifier : IVerifier<IProblem> {

    // --- Fields ---
    public string verifierName { get; } = "";
    public string verifierDefinition { get; } = "This is a placeholder verifier class for problems with no verifier";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "" };
    public string certificate { get; } = "";

    // --- Methods Including Constructors ---
    public DummyVerifier() {

    }
    public bool verify(IProblem problem, string certificate) {
        throw new NoVerifierException();
    }

    // Stands in for any problem, so there is nothing to parse the instance into (IProblem has no constructor).
    bool IVerifier.verify(string problem, string certificate) {
        throw new NoVerifierException();
    }
}

internal class NoVerifierException : Exception {
    public NoVerifierException()
        : base("This problem has no verifier yet. DummyVerifier is a placeholder and cannot check certificates.") { }
}
