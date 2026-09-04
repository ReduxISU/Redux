using System.Reflection;
using System.Text.RegularExpressions;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Naming-convention guards for Verifier/Solver/Reduction display names
// (branch: rename/solver-reduction-verifier-names).
//
// Verifiers: every verifierName must be exactly "{problem.problemName} Verifier" — this
// is the regression guard for the "verifier display names to [Problem Name] Verifier
// instead of Default Verifier" fix on this branch.
//
// Solvers/Reductions: every solverName/reductionName must match one of two shapes:
//   (a) "[Problem Name] [Approach]"        -- e.g. "Clique Brute Force", "DFA Simulation"
//   (b) "[Person(s) Who Created It] Algorithm" (solvers) or "... Reduction" (reductions)
//       -- e.g. "Dijkstra's Algorithm", "Karp's 3SAT Reduction", "Garey-Johnson Reduction"
// Shape (a) is checked structurally (title-case, 2+ words) rather than by cross-referencing
// the exact problemName string, because the free-form "approach" half of these names
// legitimately diverges from the formal problemName (e.g. solver "DFA Simulation" vs.
// problem "DFA Acceptance") — an exact-prefix check would fail perfectly good names.
// What IS enforced structurally: the name isn't empty, isn't a raw class-name leak
// (PascalCase-no-spaces, lowercase), and isn't the literal "Default ..." placeholder this
// test exists to catch.
//
// *** A failing case here means a real display name needs fixing, not that this test
// should grow an exception list. Report it. ***
public class NamingConvention_Tests {

    // Person token: one or more hyphen-joined Capitalized words (covers co-author names
    // like "Garey-Johnson", "Lawler-Karp", "Bernstein-Vazirani"), optional possessive 's.
    private const string PersonToken = @"[A-Z][A-Za-z]*(?:-[A-Z][A-Za-z]*)*'?s?";
    private static readonly Regex PersonAlgorithmShape = new($@"^{PersonToken}\s+Algorithm\b");
    private static readonly Regex PersonReductionShape = new($@"^{PersonToken}\s+Reduction\b");

    // Problem+Approach shape: 2+ words, each either Title-Case/ALLCAPS/digit-leading
    // (hyphens/apostrophes allowed within a word) or a lowercase connector ("and"/"of").
    private static readonly Regex ProblemPlusApproachShape = new(
        @"^(?:[A-Z0-9][\w'-]*|and|of)(?:\s+(?:[A-Z0-9][\w'-]*|and|of))+$");

    private static Exception Unwrap(Exception ex) =>
        ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;

    // ── Verifiers ────────────────────────────────────────────────────────────

    public static IEnumerable<object[]> AllVerifiers() {
        foreach (var (_, type) in ProblemProvider.Verifiers)
            yield return new object[] { type };
    }

    [Theory]
    [MemberData(nameof(AllVerifiers))]
    public void VerifierName_Equals_ProblemName_Plus_Verifier(Type verifierType) {
        var generic = verifierType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IVerifier<>));
        Assert.True(generic != null, $"{verifierType.Name}: does not implement IVerifier<>.");
        Type problemType = generic!.GetGenericArguments()[0];

        IProblem? problem;
        try {
            problem = Activator.CreateInstance(problemType) as IProblem;
        } catch (Exception ex) {
            var real = Unwrap(ex);
            Assert.Fail($"{verifierType.Name}: {problemType.Name} did not default-construct " +
                $"({real.GetType().Name}: {real.Message}).");
            return;
        }
        Assert.True(problem != null, $"{verifierType.Name}: {problemType.Name} default-constructed but wasn't an IProblem.");

        IVerifier? verifier;
        try {
            verifier = Activator.CreateInstance(verifierType) as IVerifier;
        } catch (Exception ex) {
            var real = Unwrap(ex);
            Assert.Fail($"{verifierType.Name}: did not default-construct ({real.GetType().Name}: {real.Message}).");
            return;
        }
        Assert.True(verifier != null, $"{verifierType.Name}: default-constructed but wasn't an IVerifier.");

        string expected = $"{problem!.problemName} Verifier";
        Assert.True(verifier!.verifierName == expected,
            $"{verifierType.Name}.verifierName is \"{verifier.verifierName}\" — expected \"{expected}\" " +
            $"(\"[Problem Name] Verifier\", matching {problemType.Name}.problemName).");
    }

    // ── Solvers ──────────────────────────────────────────────────────────────

    public static IEnumerable<object[]> AllSolvers() {
        foreach (var (_, type) in ProblemProvider.Solvers) {
            // Placeholder/test-scaffolding class (Interfaces/DummyClasses/DummySolver.cs),
            // not one of the real solvers in scope for a display-name convention — same
            // precedent as SolverType_Tests.cs's SolverTypeUnclassifiedAllowlist.
            if (type.Name == "DummySolver") continue;
            yield return new object[] { type };
        }
    }

    [Theory]
    [MemberData(nameof(AllSolvers))]
    public void SolverName_MatchesNamingConvention(Type solverType) {
        ISolver? solver;
        try {
            solver = Activator.CreateInstance(solverType) as ISolver;
        } catch (Exception ex) {
            var real = Unwrap(ex);
            Assert.Fail($"{solverType.Name}: did not default-construct ({real.GetType().Name}: {real.Message}).");
            return;
        }
        Assert.True(solver != null, $"{solverType.Name}: default-constructed but wasn't an ISolver.");

        AssertMatchesConvention(solverType.Name, solver!.solverName, PersonAlgorithmShape, "Algorithm");
    }

    // ── Reductions ───────────────────────────────────────────────────────────

    // Driven off ReductionGraphData.Graph rather than ProblemProvider.Reductions directly —
    // same choice, and same reasoning, as ReductionSmoke_Tests.AllReductionEdges(): it keeps
    // this exercising exactly what /Navigation/Reductions advertises to callers, rather than
    // every IReduction<,> implementation the raw type scan happens to find (e.g.
    // SipserReduceToSAT3 is a solution-mapping companion class that isn't a graph edge and
    // can't be constructed from a plain default instance — see
    // ProblemProvider_Endpoint_Tests.MapSolution_NonSipserInstance_Returns400WithParseError).
    public static IEnumerable<object[]> AllReductions() {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, tos) in ReductionGraphData.Graph)
            foreach (var (_, edges) in tos)
                foreach (var edge in edges) {
                    if (!ProblemProvider.Reductions.TryGetValue(edge.className.ToLower(), out var type))
                        continue;
                    if (seen.Add(edge.className))
                        yield return new object[] { type! };
                }
    }

    [Theory]
    [MemberData(nameof(AllReductions))]
    public void ReductionName_MatchesNamingConvention(Type reductionType) {
        var generic = reductionType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReduction<,>));
        Assert.True(generic != null, $"{reductionType.Name}: does not implement IReduction<,>.");
        Type fromType = generic!.GetGenericArguments()[0];

        IProblem? fromDefault;
        try {
            fromDefault = Activator.CreateInstance(fromType) as IProblem;
        } catch (Exception ex) {
            var real = Unwrap(ex);
            Assert.Fail($"{reductionType.Name}: {fromType.Name} did not default-construct " +
                $"({real.GetType().Name}: {real.Message}).");
            return;
        }
        Assert.True(fromDefault != null, $"{reductionType.Name}: {fromType.Name} default-constructed but wasn't an IProblem.");

        IReduction? reduction;
        try {
            reduction = Activator.CreateInstance(reductionType, fromDefault!.defaultInstance) as IReduction;
        } catch (Exception ex) {
            var real = Unwrap(ex);
            Assert.Fail($"{reductionType.Name}: constructing from {fromType.Name}.defaultInstance threw " +
                $"{real.GetType().Name}: {real.Message}.");
            return;
        }
        Assert.True(reduction != null, $"{reductionType.Name}: constructed instance was not an IReduction.");

        AssertMatchesConvention(reductionType.Name, reduction!.reductionName, PersonReductionShape, "Reduction");
    }

    // ── Shared assertion ─────────────────────────────────────────────────────

    private static void AssertMatchesConvention(string typeName, string name, Regex personShape, string suffixWord) {
        Assert.False(string.IsNullOrWhiteSpace(name), $"{typeName}: display name is empty.");

        // The specific regression this whole test exists to catch: a generic placeholder
        // like "Default Solver"/"Default Reduction" is structurally two title-case words,
        // so it would otherwise slip through the Problem+Approach shape below.
        Assert.False(name.StartsWith("Default ", StringComparison.OrdinalIgnoreCase),
            $"{typeName}: display name \"{name}\" starts with the placeholder \"Default\" — give it a " +
            $"real \"[Problem Name] [Approach]\" or \"[Person] {suffixWord}\" name.");

        bool matchesPersonShape = personShape.IsMatch(name);
        bool matchesProblemShape = ProblemPlusApproachShape.IsMatch(name);
        Assert.True(matchesPersonShape || matchesProblemShape,
            $"{typeName}: display name \"{name}\" matches neither the \"[Problem Name] [Approach]\" shape " +
            $"nor the \"[Person] {suffixWord}\" shape (e.g. \"Dijkstra's Algorithm\" / \"Karp's 3SAT Reduction\").");
    }
}
