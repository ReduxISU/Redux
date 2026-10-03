using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Whitespace in a user-supplied instance or certificate must never change a verify() result
// (Redux_GUI#327). For every top-level problem: solve the default instance with the default solver,
// then check that inserting whitespace into the instance or the certificate gives the same verdict
// as the original.
//
// Runs in its own non-parallel collection: it solves ~50 problems (some CPU-heavy), and running that
// alongside the suite starves the wall-clock-sensitive tests (e.g. SAT3 Schoning's 10 s bound).
[Collection("WhitespaceTolerance"), CollectionDefinition("WhitespaceTolerance", DisableParallelization = true)]
public class WhitespaceTolerance_Tests {
    private static readonly (string Name, Func<string, string> Apply)[] Variants = {
        ("comma+space", s => s.Replace(",", ", ")),
        ("comma+LF", s => s.Replace(",", ",\n")),
        ("comma+CRLF", s => s.Replace(",", ",\r\n")),
        ("comma+tab", s => s.Replace(",", ",\t")),
        ("trailing LF", s => s + "\n"),
        ("leading LF", s => "\n" + s),
    };

    // Whitespace next to a delimiter is data in an opted-out instance (preserveInstanceWhitespace), so
    // only the leading/trailing variants, which are trimmed, apply to such an instance.
    private static readonly HashSet<string> EdgeVariants = new() { "trailing LF", "leading LF" };

    public static IEnumerable<TheoryDataRow<string>> ProblemNames() =>
        MetadataReflection.Instances.Keys
            .Where(MetadataReflection.TopLevelClassNames.Contains)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Select(n => new TheoryDataRow<string>(n));

    [Theory]
    [MemberData(nameof(ProblemNames))]
    public async Task WhitespaceVariants_DoNotChangeVerifyResult(string name) {
        IProblem problem = MetadataReflection.Instances[name];
        string instance = problem.defaultInstance;
        ISolver solver = problem.defaultSolver;
        IVerifier verifier = problem.defaultVerifier;

        Task<string> solve = Task.Run(() => solver.solve(instance), TestContext.Current.CancellationToken);
        Task finished = await Task.WhenAny(solve, Task.Delay(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));
        Assert.True(finished == solve, $"{name}: default solve timed out");
        string certificate = await solve;

        Assert.True(verifier.verify(instance, certificate), $"{name}: default solution does not verify as given");

        var failures = new List<string>();
        foreach (var (variant, apply) in Variants) {
            if (!problem.preserveInstanceWhitespace || EdgeVariants.Contains(variant))
                Check(failures, $"instance, {variant}", () => verifier.verify(apply(instance), certificate));
            Check(failures, $"certificate, {variant}", () => verifier.verify(instance, apply(certificate)));
        }
        Assert.True(failures.Count == 0, $"{name}: {string.Join("; ", failures)}");
    }

    private static void Check(List<string> failures, string what, Func<bool> verify) {
        try {
            if (!verify()) failures.Add($"{what} verified False");
        } catch (Exception ex) {
            failures.Add($"{what} threw {(ex.InnerException ?? ex).GetType().Name}");
        }
    }
}
