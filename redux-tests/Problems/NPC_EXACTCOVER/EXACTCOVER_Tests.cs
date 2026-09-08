using Xunit;
using API.Problems.NPComplete.NPC_EXACTCOVER;
using API.Problems.NPComplete.NPC_EXACTCOVER.Solvers;
using API.Problems.NPComplete.NPC_EXACTCOVER.Verifiers;

namespace redux_tests;

#pragma warning disable CS1591

public class EXACTCOVER_Tests {
    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void EXACTCOVER_Instance_Format_Described() {
        EXACTCOVER problem = new EXACTCOVER();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("(U,S)", problem.instanceFormat);
    }

    [Fact]
    public void EXACTCOVER_Certificate_Format_Described() {
        EXACTCOVER problem = new EXACTCOVER();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("pairwise disjoint", problem.certificateFormat);
    }

    [Fact]
    public void EXACTCOVER_Certificate_Format_Example_Is_Actually_Valid() {
        // The example quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        EXACTCOVER problem = new EXACTCOVER();
        ExactCoverVerifier verifier = new ExactCoverVerifier();
        Assert.True(verifier.verify(problem, "{{2,3},{4,1}}"));
    }

    // -------------------------------------------------------------------------
    // Shared solver test instances
    // -------------------------------------------------------------------------

    // Default instance: unique exact cover {{2,3},{4,1}}.
    private const string DefaultInstance = "({1,2,3,4},{{1,2,3},{2,3},{4,1}})";

    // Trivial: single element, single set matching it exactly.
    private const string TrivialInstance = "({1},{{1}})";

    // A set that exactly equals the universe, alongside a finer 2-set partition --
    // both {{1,2,3,4}} and {{1,2},{3,4}} are valid exact covers, but {{1,2},{3,4}}
    // is found first by both ExactCoverRecursive and ExactCoverBruteForce because it
    // is reachable via the earlier-indexed subsets.
    private const string MultipleCoversInstance = "({1,2,3,4},{{1,2},{3,4},{1,2,3,4}})";
    private const string MultipleCoversCertificate = "{{1,2},{3,4}}";

    // Knuth's classic Algorithm X example ("Dancing Links", 2000). Unique exact
    // cover {B,D,F} = {{1,4},{3,5,6},{2,7}}. Requires genuine backtracking: any
    // cover starting from A={1,4,7} is a dead end (only D={3,5,6} avoids
    // overlapping A, and A+D still leaves element 2 uncovered with nothing left
    // compatible), so a correct solver must explore and abandon that branch before
    // finding B,D,F. Since the exact cover is unique, every solver that finds one
    // at all must return this same combination of subsets.
    private const string KnuthInstance = "({1,2,3,4,5,6,7},{{1,4,7},{1,4},{4,5,7},{3,5,6},{2,3,6,7},{2,7}})";
    private const string KnuthUniqueCertificate = "{{1,4},{3,5,6},{2,7}}";

    // No exact cover: the two sets overlap on 2, and no combination covers {1,2,3} exactly.
    private const string OverlapOnlyInstance = "({1,2,3},{{1,2},{2,3}})";

    // No exact cover: nothing in S covers element 3 at all.
    private const string UncoverableInstance = "({1,2,3},{{1,2}})";

    private static int CountTopLevelSubsets(string certificate) {
        if (string.IsNullOrEmpty(certificate) || certificate == "{}") return 0;
        string inner = certificate.Substring(1, certificate.Length - 2); // strip outer { }
        return inner.Split(new[] { "},{" }, StringSplitOptions.None).Length;
    }

    // -------------------------------------------------------------------------
    // Solver: DancingLinks
    // -------------------------------------------------------------------------

    [Fact]
    public void DancingLinks_Solves_Default_Instance() {
        EXACTCOVER problem = new EXACTCOVER(DefaultInstance);
        string certificate = new DancingLinks().solve(problem);
        Assert.True(new ExactCoverVerifier().verify(problem, certificate), $"got: {certificate}");
        Assert.Equal(2, CountTopLevelSubsets(certificate));
    }

    [Fact]
    public void DancingLinks_Solves_Trivial_Single_Element() {
        EXACTCOVER problem = new EXACTCOVER(TrivialInstance);
        string certificate = new DancingLinks().solve(problem);
        Assert.Equal("{{1}}", certificate);
        Assert.True(new ExactCoverVerifier().verify(problem, certificate));
    }

    [Fact]
    public void DancingLinks_Accepts_Set_Equal_To_Universe() {
        EXACTCOVER problem = new EXACTCOVER(MultipleCoversInstance);
        string certificate = new DancingLinks().solve(problem);
        Assert.True(new ExactCoverVerifier().verify(problem, certificate), $"got: {certificate}");
    }

    [Fact]
    public void DancingLinks_Requires_Backtracking_On_Knuth_Instance() {
        EXACTCOVER problem = new EXACTCOVER(KnuthInstance);
        string certificate = new DancingLinks().solve(problem);
        Assert.True(new ExactCoverVerifier().verify(problem, certificate), $"got: {certificate}");
        Assert.Equal(3, CountTopLevelSubsets(certificate));
    }

    [Fact]
    public void DancingLinks_Returns_Empty_Braces_When_Sets_Only_Overlap() {
        EXACTCOVER problem = new EXACTCOVER(OverlapOnlyInstance);
        string certificate = new DancingLinks().solve(problem);
        Assert.Equal("{}", certificate);
    }

    [Fact]
    public void DancingLinks_Returns_Empty_Braces_When_Element_Is_Uncoverable() {
        EXACTCOVER problem = new EXACTCOVER(UncoverableInstance);
        string certificate = new DancingLinks().solve(problem);
        Assert.Equal("{}", certificate);
    }

    [Fact]
    public void DancingLinks_SolutionToCertificate_Formats_Selected_Sets() {
        // Exercise solutionToCertificate directly with a hand-built stack rather
        // than only indirectly through solve().
        EXACTCOVER problem = new EXACTCOVER(DefaultInstance);
        Stack<int> selected = new Stack<int>();
        selected.Push(1); // {2,3}
        selected.Push(2); // {4,1}
        string certificate = new DancingLinks().solutionToCertificate(selected, problem);
        Assert.True(new ExactCoverVerifier().verify(problem, certificate), $"got: {certificate}");
        Assert.Equal(2, CountTopLevelSubsets(certificate));
    }

    // -------------------------------------------------------------------------
    // Solver: ExactCoverRecursive
    // -------------------------------------------------------------------------

    [Fact]
    public void ExactCoverRecursive_Solves_Default_Instance_Exactly() {
        EXACTCOVER problem = new EXACTCOVER(DefaultInstance);
        string certificate = new ExactCoverRecursive().solve(problem);
        Assert.Equal("{{2,3},{4,1}}", certificate);
        Assert.True(new ExactCoverVerifier().verify(problem, certificate));
    }

    [Fact]
    public void ExactCoverRecursive_Solves_Trivial_Single_Element() {
        EXACTCOVER problem = new EXACTCOVER(TrivialInstance);
        string certificate = new ExactCoverRecursive().solve(problem);
        Assert.Equal("{{1}}", certificate);
        Assert.True(new ExactCoverVerifier().verify(problem, certificate));
    }

    [Fact]
    public void ExactCoverRecursive_Picks_Finer_Partition_Over_Full_Universe_Set() {
        EXACTCOVER problem = new EXACTCOVER(MultipleCoversInstance);
        string certificate = new ExactCoverRecursive().solve(problem);
        Assert.Equal(MultipleCoversCertificate, certificate);
        Assert.True(new ExactCoverVerifier().verify(problem, certificate));
    }

    [Fact]
    public void ExactCoverRecursive_Solves_Knuth_Instance_Exactly() {
        // Requires real backtracking: the A-branch is fully explored and abandoned
        // (see KnuthInstance comment) before B,D,F is found.
        EXACTCOVER problem = new EXACTCOVER(KnuthInstance);
        string certificate = new ExactCoverRecursive().solve(problem);
        Assert.Equal(KnuthUniqueCertificate, certificate);
        Assert.True(new ExactCoverVerifier().verify(problem, certificate));
    }

    [Fact]
    public void ExactCoverRecursive_Returns_Empty_String_When_Sets_Only_Overlap() {
        // Unlike DancingLinks/BruteForce (which explicitly return the literal
        // "{}"), ExactCoverRecursive's subsetsToCertificate short-circuits an
        // empty solution list to "". Still correctly rejected by the verifier --
        // just a different "no solution" sentinel than the other two solvers use.
        EXACTCOVER problem = new EXACTCOVER(OverlapOnlyInstance);
        string certificate = new ExactCoverRecursive().solve(problem);
        Assert.Equal("", certificate);
        Assert.False(new ExactCoverVerifier().verify(problem, certificate));
    }

    [Fact]
    public void ExactCoverRecursive_Returns_Empty_String_When_Element_Is_Uncoverable() {
        EXACTCOVER problem = new EXACTCOVER(UncoverableInstance);
        string certificate = new ExactCoverRecursive().solve(problem);
        Assert.Equal("", certificate);
        Assert.False(new ExactCoverVerifier().verify(problem, certificate));
    }

    // -------------------------------------------------------------------------
    // Solver: ExactCoverBruteForce
    // -------------------------------------------------------------------------

    [Fact]
    public void ExactCoverBruteForce_Solves_Default_Instance_Exactly() {
        EXACTCOVER problem = new EXACTCOVER(DefaultInstance);
        string certificate = new ExactCoverBruteForce().solve(problem);
        Assert.Equal("{{2,3},{4,1}}", certificate);
        Assert.True(new ExactCoverVerifier().verify(problem, certificate));
    }

    [Fact]
    public void ExactCoverBruteForce_Solves_Trivial_Single_Element() {
        EXACTCOVER problem = new EXACTCOVER(TrivialInstance);
        string certificate = new ExactCoverBruteForce().solve(problem);
        Assert.Equal("{{1}}", certificate);
        Assert.True(new ExactCoverVerifier().verify(problem, certificate));
    }

    [Fact]
    public void ExactCoverBruteForce_Picks_Lowest_Indexed_Combination() {
        // BruteForce enumerates candidate subset-combinations in ascending binary
        // order and returns the first one the verifier accepts, so among the two
        // valid exact covers it must return the lower-indexed {{1,2},{3,4}}
        // rather than the single-set {{1,2,3,4}}.
        EXACTCOVER problem = new EXACTCOVER(MultipleCoversInstance);
        string certificate = new ExactCoverBruteForce().solve(problem);
        Assert.Equal(MultipleCoversCertificate, certificate);
        Assert.True(new ExactCoverVerifier().verify(problem, certificate));
    }

    [Fact]
    public void ExactCoverBruteForce_Solves_Knuth_Instance_Exactly() {
        EXACTCOVER problem = new EXACTCOVER(KnuthInstance);
        string certificate = new ExactCoverBruteForce().solve(problem);
        Assert.Equal(KnuthUniqueCertificate, certificate);
        Assert.True(new ExactCoverVerifier().verify(problem, certificate));
    }

    [Fact]
    public void ExactCoverBruteForce_Returns_Empty_Braces_When_Sets_Only_Overlap() {
        EXACTCOVER problem = new EXACTCOVER(OverlapOnlyInstance);
        string certificate = new ExactCoverBruteForce().solve(problem);
        Assert.Equal("{}", certificate);
    }

    [Fact]
    public void ExactCoverBruteForce_Returns_Empty_Braces_When_Element_Is_Uncoverable() {
        EXACTCOVER problem = new EXACTCOVER(UncoverableInstance);
        string certificate = new ExactCoverBruteForce().solve(problem);
        Assert.Equal("{}", certificate);
    }
}
