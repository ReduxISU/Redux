namespace API.Interfaces;

// Thrown when a problem's instance constructor cannot parse the supplied
// instance string. Controllers translate this to HTTP 400 with the
// problem's `instanceFormat` as the structured hint.
internal class ProblemParseException : Exception {
    public string ProblemName { get; }
    public string Received { get; }
    public ProblemParseException(string problemName, string received, string? detail = null, Exception? innerException = null)
        : base(detail ?? $"could not parse {problemName} instance", innerException) {
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
    public CertificateParseException(IProblem problem, string received, string? detail = null, Exception? innerException = null)
        : base(detail ?? $"could not parse {problem.problemName} certificate", innerException) {
        Problem = problem;
        Received = received;
    }
}

// Thrown when a reduction cannot parse the supplied input (either the
// source-problem instance during construction, or the source-problem
// certificate during mapSolutions). Controllers translate this to HTTP
// 400. The `ExpectedFormat` field is passed in at the throw site so the
// caller can choose between `reductionFrom.instanceFormat` and
// `reductionFrom.certificateFormat` depending on which input failed.
internal class ReductionInputException : Exception {
    public IReduction Reduction { get; }
    public string Received { get; }
    public string ExpectedFormat { get; }
    /// <summary>The problem whose text was malformed, when that is not the reduction's source problem (a backward map reads the target's answer).</summary>
    public string? Problem { get; }
    public ReductionInputException(IReduction reduction, string received, string expectedFormat, string? detail = null, Exception? innerException = null, string? problem = null)
        : base(detail ?? $"could not parse input to {reduction.reductionName}", innerException) {
        Problem = problem;
        Reduction = reduction;
        Received = received;
        ExpectedFormat = expectedFormat;
    }
}

// Shared chokepoints that turn failures while parsing caller-supplied input into the parse
// exceptions above, so the controller answers with a structured 400 instead of a raw 500 (#577).
// Only the parse/construction step is guarded: the algorithm that runs afterwards (solve, reduce,
// a verifier's checking logic) is never wrapped, so a genuine bug there still surfaces as a 500.
internal static class ParseGuard {
    /// <summary>
    /// Constructs <paramref name="problemType"/> from an instance string. Any exception thrown by
    /// the constructor is a failure to parse that instance; a <see cref="ProblemParseException"/>
    /// is passed through as-is, anything else is wrapped in one (original kept as InnerException).
    /// </summary>
    public static IProblem CreateProblem(Type problemType, string instance) {
        try {
            return (IProblem)Activator.CreateInstance(problemType, instance)!;
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Exception real = ex is System.Reflection.TargetInvocationException { InnerException: { } inner } ? inner : ex;
            if (real is ProblemParseException)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(real).Throw();
            throw new ProblemParseException(ProblemName(problemType), instance, real.Message, real);
        }
    }

    public static T CreateProblem<T>(string instance) where T : IProblem =>
        (T)CreateProblem(typeof(T), instance);

    // The friendly problemName property when the type default-constructs, else the class name.
    // ProblemProvider.LookupInstanceFormat resolves either spelling.
    private static string ProblemName(Type problemType) {
        try {
            if (Activator.CreateInstance(problemType) is IProblem p && !string.IsNullOrEmpty(p.problemName))
                return p.problemName;
        } catch { /* fall through to the class name */ }
        return problemType.Name;
    }

    /// <summary>
    /// Runs the certificate-parsing part of a verifier. verify() parses the certificate and checks
    /// it in one method, so parsing cannot be guarded on its own; instead only the exception types
    /// that malformed text produces from Parse/indexing/lookup code are translated. Anything else
    /// (and any CertificateParseException the verifier already threw) propagates unchanged.
    /// </summary>
    public static bool VerifyCertificate(IProblem problem, string certificate, Func<bool> verify) {
        try {
            return verify();
        } catch (Exception ex) when (IsCertificateParseFailure(ex)) {
            throw new CertificateParseException(problem, certificate, ex.Message, ex);
        }
    }

    /// <summary>
    /// True for the exception types that text parsing produces: Parse/indexing/lookup failures from
    /// the BCL, plus the bare System.Exception that SPADE (and some hand-written parsers) throw for
    /// malformed input, e.g. "Braces not matched". Matching the bare type exactly keeps derived
    /// exception types (and our own parse exceptions) out of the net.
    /// </summary>
    public static bool IsCertificateParseFailure(Exception ex) =>
        ex is FormatException or InvalidOperationException or IndexOutOfRangeException or ArgumentException
            or KeyNotFoundException or OverflowException or NullReferenceException or InvalidCastException
        || ex.GetType() == typeof(Exception);
}
