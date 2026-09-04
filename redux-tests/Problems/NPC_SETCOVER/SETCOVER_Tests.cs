using Xunit;
using API.Problems.NPComplete.NPC_SETCOVER;
using API.Problems.NPComplete.NPC_SETCOVER.Solvers;
using API.Problems.NPComplete.NPC_SETCOVER.Verifiers;

namespace redux_tests;

#pragma warning disable CS1591

public class SETCOVER_Tests {
    // -------------------------------------------------------------------------
    // Shared solver test instances
    // -------------------------------------------------------------------------

    // Default instance: {1,2,3} + {4,5} (or {2,4}+{1,2,3}... ) covers the universe
    // with 2 subsets, well within K=3.
    private const string DefaultInstance = "({1,2,3,4,5},{{1,2,3},{2,4},{3,4},{4,5}},3)";

    // Trivial: single element, single set matching it exactly, K=1.
    private const string TrivialInstance = "({1},{{1}},1)";

    // Exactly two disjoint subsets are required and K=2 permits exactly that.
    private const string TwoSetInstance = "({1,2,3,4},{{1,2},{3,4}},2)";

    // Same subsets as DefaultInstance but K=1: no single subset covers all of
    // {1,2,3,4,5} (the largest subset only has 3 elements), so no cover of size
    // <= K exists.
    private const string KTooSmallInstance = "({1,2,3,4,5},{{1,2,3},{2,4},{3,4},{4,5}},1)";

    // K=0: even though sets exist, a nonempty universe can never be covered by
    // zero subsets.
    private const string KZeroInstance = "({1,2,3,4,5},{{1,2,3},{2,4},{3,4},{4,5}},0)";

    // Nothing in the subset collection covers element 3, so no cover exists
    // regardless of how large K is.
    private const string UncoverableInstance = "({1,2,3},{{1,2}},5)";

    // 5-cycle of overlapping pairs: {1,2},{2,3},{3,4},{4,5},{1,5}. Every element
    // appears in exactly two subsets, so picking any one subset first still
    // leaves a chain that needs at least two more subsets to close -- minimum
    // cover size is 3 (e.g. {1,2}+{3,4}+... no single pair-selection greedily
    // finishes without some amount of backtracking/tie-breaking across equally
    // "good" columns), exercising the OrderByDescending tie-break and the
    // select/deselect backtracking path with genuinely overlapping sets.
    private const string OverlappingCycleInstance = "({1,2,3,4,5},{{1,2},{2,3},{3,4},{4,5},{1,5}},3)";

    private static int CountTopLevelSubsets(string certificate) {
        if (string.IsNullOrEmpty(certificate) || certificate == "{}") return 0;
        string inner = certificate.Substring(1, certificate.Length - 2); // strip outer { }
        return inner.Split(new[] { "},{" }, StringSplitOptions.None).Length;
    }

    // -------------------------------------------------------------------------
    // Solver: HeuristicSolver
    // -------------------------------------------------------------------------

    [Fact]
    public void HeuristicSolver_Solves_Default_Instance_Within_K() {
        SETCOVER problem = new SETCOVER(DefaultInstance);
        string certificate = new HeuristicSolver().solve(problem);
        Assert.True(new SetCoverVerifier().verify(problem, certificate), $"got: {certificate}");
        Assert.True(CountTopLevelSubsets(certificate) <= problem.K);
        Assert.NotEqual("{}", certificate);
    }

    [Fact]
    public void HeuristicSolver_Solves_Trivial_Single_Element() {
        SETCOVER problem = new SETCOVER(TrivialInstance);
        string certificate = new HeuristicSolver().solve(problem);
        Assert.Equal("{{1}}", certificate);
        Assert.True(new SetCoverVerifier().verify(problem, certificate));
    }

    [Fact]
    public void HeuristicSolver_Solves_Two_Disjoint_Sets_At_Exact_K() {
        SETCOVER problem = new SETCOVER(TwoSetInstance);
        string certificate = new HeuristicSolver().solve(problem);
        Assert.True(new SetCoverVerifier().verify(problem, certificate), $"got: {certificate}");
        Assert.Equal(2, CountTopLevelSubsets(certificate));
    }

    [Fact]
    public void HeuristicSolver_Returns_Empty_Braces_When_K_Too_Small() {
        SETCOVER problem = new SETCOVER(KTooSmallInstance);
        string certificate = new HeuristicSolver().solve(problem);
        Assert.Equal("{}", certificate);
    }

    [Fact]
    public void HeuristicSolver_Returns_Empty_Braces_When_K_Is_Zero() {
        // Exercises the `solution.Count() > K` prune on the very first push,
        // since K=0 can never accommodate a nonempty universe.
        SETCOVER problem = new SETCOVER(KZeroInstance);
        string certificate = new HeuristicSolver().solve(problem);
        Assert.Equal("{}", certificate);
    }

    [Fact]
    public void HeuristicSolver_Returns_Empty_Braces_When_Element_Is_Uncoverable() {
        SETCOVER problem = new SETCOVER(UncoverableInstance);
        string certificate = new HeuristicSolver().solve(problem);
        Assert.Equal("{}", certificate);
    }

    [Fact]
    public void HeuristicSolver_Solves_Overlapping_Cycle_With_Backtracking() {
        SETCOVER problem = new SETCOVER(OverlappingCycleInstance);
        string certificate = new HeuristicSolver().solve(problem);
        Assert.True(new SetCoverVerifier().verify(problem, certificate), $"got: {certificate}");
        Assert.True(CountTopLevelSubsets(certificate) <= problem.K);
    }

    [Fact]
    public void HeuristicSolver_SolutionToCertificate_Formats_Selected_Sets() {
        // Exercise solutionToCertificate directly with a hand-built stack rather
        // than only indirectly through solve().
        SETCOVER problem = new SETCOVER(TwoSetInstance);
        Stack<string> selected = new Stack<string>();
        selected.Push("0"); // {1,2}
        selected.Push("1"); // {3,4}
        string certificate = new HeuristicSolver().solutionToCertificate(selected, problem);
        Assert.True(new SetCoverVerifier().verify(problem, certificate), $"got: {certificate}");
        Assert.Equal(2, CountTopLevelSubsets(certificate));
    }
}
