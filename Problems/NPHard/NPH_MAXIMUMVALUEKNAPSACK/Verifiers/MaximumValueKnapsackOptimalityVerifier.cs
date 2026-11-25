using API.Interfaces;
using API.Problems.NPHard.NPH_MAXIMUMVALUEKNAPSACK.Solvers;

namespace API.Problems.NPHard.NPH_MAXIMUMVALUEKNAPSACK.Verifiers;

internal sealed class MaximumValueKnapsackOptimalityVerifier : IVerifier<MAXIMUMVALUEKNAPSACK> {
    public const string CertificateExample = MaximumValueKnapsackSolutionVerifier.CertificateExample;
    public string verifierName { get; } = "Maximum-Value 0-1 Knapsack Optimality Verifier";
    public string verifierDefinition { get; } = "Checks selection feasibility and reported value, then compares the value to a completed exact branch-and-bound search. Accepts any tied optimum. Verification takes O(n * 2^n) worst-case time and O(n) auxiliary search space for n item occurrences, with no item-count limit; certificate parsing additionally uses linear time and space in its length. An incomplete search cannot prove optimality.";
    public string source { get; } = "Exact optimality verification using the 0-1 knapsack branch-and-bound solver.";
    public string sourceLink { get; } = "https://doi.org/10.1007/978-3-540-24777-7";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public string certificate { get; } = CertificateExample;

    public bool verify(MAXIMUMVALUEKNAPSACK problem, string certificateText) {
        if (!new MaximumValueKnapsackSolutionVerifier().verify(problem, certificateText)) return false;
        var parsed = MaximumValueKnapsackSolutionVerifier.ParseCertificate(problem, certificateText);
        // IVerifier has no timer flag. Request deadlines are enforced by Redux's
        // worker process or endpoint backstop; this local search runs to completion.
        var solver = new MaximumValueKnapsackBranchAndBoundSolver();
        string optimum = solver.solve(problem);
        return MaximumValueKnapsackSolutionVerifier.ParseCertificate(problem, optimum).Value == parsed.Value;
    }
}
