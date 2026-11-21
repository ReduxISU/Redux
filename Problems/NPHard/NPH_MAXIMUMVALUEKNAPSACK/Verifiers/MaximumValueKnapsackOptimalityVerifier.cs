using API.Interfaces;
using API.Problems.NPHard.NPH_MAXIMUMVALUEKNAPSACK.Solvers;

namespace API.Problems.NPHard.NPH_MAXIMUMVALUEKNAPSACK.Verifiers;

internal sealed class MaximumValueKnapsackOptimalityVerifier : IVerifier<MAXIMUMVALUEKNAPSACK> {
    public const string CertificateExample = MaximumValueKnapsackSolutionVerifier.CertificateExample;
    public string verifierName { get; } = "Maximum-Value 0-1 Knapsack Optimality Verifier";
    public string verifierDefinition { get; } = "Checks selection feasibility and reported value, then compares the value to a completed exhaustive search. Accepts any tied optimum. Exhaustive verification takes O(n * 2^n) time and O(n) auxiliary search space for n item occurrences, with a 20-item limit; certificate parsing additionally uses linear time and space in its length. An incomplete search cannot prove optimality.";
    public string source { get; } = "Exhaustive optimality verification using the 0-1 knapsack brute-force solver.";
    public string sourceLink { get; } = "https://doi.org/10.1007/978-3-540-24777-7";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public string certificate { get; } = CertificateExample;

    public bool verify(MAXIMUMVALUEKNAPSACK problem, string certificateText) {
        if (!new MaximumValueKnapsackSolutionVerifier().verify(problem, certificateText)) return false;
        var parsed = MaximumValueKnapsackSolutionVerifier.ParseCertificate(problem, certificateText);
        // Reuse the exact baseline and its size guard. Verifier request time limits
        // use Redux's existing endpoint backstop, since IVerifier has no timer flag.
        var solver = new MaximumValueKnapsackBruteForceSolver();
        string optimum = solver.solve(problem);
        if (solver.timerHasExpired || optimum == SolveRun.TimedOutAnswer)
            throw new TimeoutException("Maximum-Value 0-1 Knapsack optimality verification did not complete; optimality is unknown.");
        return MaximumValueKnapsackSolutionVerifier.ParseCertificate(problem, optimum).Value == parsed.Value;
    }
}
