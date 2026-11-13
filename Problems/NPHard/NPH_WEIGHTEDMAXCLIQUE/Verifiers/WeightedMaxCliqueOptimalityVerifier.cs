using API.Interfaces;
using API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Solvers;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Verifiers;

internal sealed class WeightedMaxCliqueOptimalityVerifier : IVerifier<WEIGHTEDMAXCLIQUE> {
    public const string CertificateExample = WeightedMaxCliqueSolutionVerifier.CertificateExample;
    public string verifierName { get; } = "Weighted Maximum Clique Optimality Verifier";
    public string verifierDefinition { get; } = "Checks clique feasibility and its reported weight, then uses exhaustive solving to prove no strictly heavier clique exists. Worst-case verification takes O(2^n * n^2) time and supports at most 20 vertices.";
    public string source { get; } = "Exhaustive optimality verification using the weighted maximum clique brute-force solver.";
    public string sourceFile { get; } = SourceFile.Path();
    public string certificate { get; } = CertificateExample;
    public string[] contributors { get; } = { "Corbin Hay" };

    public bool verify(WEIGHTEDMAXCLIQUE problem, string certificateText) {
        var solutionVerifier = new WeightedMaxCliqueSolutionVerifier();
        if (!solutionVerifier.verify(problem, certificateText)) return false;
        var parsed = WeightedMaxCliqueSolutionVerifier.ParseCertificate(problem, certificateText);

        // Use the existing exact algorithm and its guard; no second search or new
        // solver infrastructure. Only a completed search can establish optimality.
        var solver = new WeightedMaxCliqueBruteForceSolver();
        string optimum = solver.solve(problem);
        if (solver.timerHasExpired || optimum == SolveRun.TimedOutAnswer)
            throw new TimeoutException("Weighted Maximum Clique optimality verification did not complete; optimality is unknown.");
        return WeightedMaxCliqueSolutionVerifier.ParseCertificate(problem, optimum).Value == parsed.Value;
    }
}
