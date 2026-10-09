using Xunit;
using API.Problems.NPComplete.NPC_DM3;
using API.Problems.NPComplete.NPC_DM3.Solvers;
using API.Problems.NPComplete.NPC_DM3.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class DM3_Tests {

    // -------------------------------------------------------------------------
    // Instantiation
    // -------------------------------------------------------------------------

    [Fact]
    public void DM3_Default_Instantiation() {
        DM3 problem = new DM3();
        Assert.Equal(DM3._defaultInstance, problem.defaultInstance);
        Assert.Equal(problem.defaultInstance, problem.instance);
    }

    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void DM3_Instance_Format_Described() {
        DM3 problem = new DM3();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("3-tuple", problem.instanceFormat);
    }

    [Fact]
    public void DM3_Certificate_Format_Described() {
        DM3 problem = new DM3();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("{x,y,z}", problem.certificateFormat);
    }

    [Fact]
    public void DM3_Certificate_Format_Example_Is_Actually_Valid() {
        // The example quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        DM3 problem = new DM3();
        GenericVerifierDM3 verifier = new GenericVerifierDM3();
        Assert.True(verifier.verify(problem, DM3.CertificateExample));
    }

    // -------------------------------------------------------------------------
    // Parsing
    // -------------------------------------------------------------------------

    // Regression test for https://github.com/ReduxISU/Redux/issues/537 — ParseProblem used to
    // stride by 3 over the entire flattened instance string (header groups and M triples alike)
    // instead of stopping at each header group's own boundary, and ParseM independently chunked
    // the whole flattened stream into groups of 3, pulling the X/Y/Z header groups themselves into
    // M as spurious pseudo-triples. Both bugs corrupted the default instance: X came out as
    // {Paul,Madison,Chloe,Sally,Dave} (contaminated with Y/Z entries) and M.Count came out as 9
    // instead of 6.
    [Fact]
    public void DM3_ParseProblem_Cross_Contaminates_Header_Sets_And_M() {
        DM3 problem = new DM3();

        Assert.Equal(new List<string> { "Paul", "Sally", "Dave" }, problem.X);
        Assert.Equal(new List<string> { "Madison", "Austin", "Bob" }, problem.Y);
        Assert.Equal(new List<string> { "Chloe", "Frank", "Jake" }, problem.Z);
        Assert.Equal(6, problem.M.Count);
    }

    [Fact]
    public void DM3_ParseM_Only_Contains_M_Triples() {
        DM3 problem = new DM3();

        Assert.Equal(6, problem.M.Count);
        Assert.All(problem.M, triple => Assert.Equal(3, triple.Count));

        List<List<string>> expected = new List<List<string>> {
            new List<string> { "Paul", "Madison", "Chloe" },
            new List<string> { "Paul", "Austin", "Jake" },
            new List<string> { "Sally", "Bob", "Chloe" },
            new List<string> { "Sally", "Madison", "Frank" },
            new List<string> { "Dave", "Austin", "Chloe" },
            new List<string> { "Dave", "Bob", "Chloe" },
        };
        Assert.Equal(expected, problem.M);
    }

    // -------------------------------------------------------------------------
    // GenericVerifierDM3 (#710): a certificate must be a perfect matching
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("{Paul,Austin,Jake}{Sally,Madison,Frank}{Dave,Bob,Chloe}", true)]
    [InlineData("{{Paul,Austin,Jake},{Sally,Madison,Frank},{Dave,Bob,Chloe}}", true)] // the solver's output shape
    [InlineData("{Dave,Bob,Chloe}{Paul,Austin,Jake}{Sally,Madison,Frank}", true)]     // order does not matter
    [InlineData("{Paul,Austin,Jake}", false)]                                         // a single triple does not cover X, Y and Z
    [InlineData("{Paul,Austin,Jake}{Sally,Madison,Frank}", false)]                    // Dave, Bob, Chloe uncovered
    [InlineData("{Paul,Austin,Jake}{Sally,Madison,Chloe}{Dave,Bob,Frank}", false)]    // all elements covered, but triples not in M
    [InlineData("{Paul,Madison,Chloe}{Paul,Austin,Jake}{Sally,Madison,Frank}", false)] // Paul used twice
    [InlineData("{Paul,Austin,Jake}{Sally,Madison,Frank}{Dave,Austin,Chloe}", false)] // Austin used twice
    [InlineData("{Paul,Austin,Jake}{Sally,Madison,Frank}{Dave,Bob,Chloe}{Dave,Bob,Chloe}", false)] // repeated triple
    [InlineData("{Paul,Austin}", false)]                                              // not a triple
    [InlineData("{Nobody,Austin,Jake}", false)]                                       // unknown element
    [InlineData("{}", false)]
    [InlineData("", false)]
    public void DM3_Verifier_Requires_Perfect_Matching_From_M(string certificate, bool expected) {
        DM3 problem = new DM3();
        Assert.Equal(expected, new GenericVerifierDM3().verify(problem, certificate));
    }

    [Fact]
    public void DM3_Verifier_Rejects_Triple_Not_In_M_Even_When_Elements_Are_Valid() {
        // The only constraint is {A,B,C}; {A,B,D} uses valid elements but is not in M.
        DM3 problem = new DM3("{A,E}{B,F}{C,D}{A,B,C}{E,F,D}");
        Assert.True(new GenericVerifierDM3().verify(problem, "{A,B,C}{E,F,D}"));
        Assert.False(new GenericVerifierDM3().verify(problem, "{A,B,D}{E,F,C}"));
    }

    // -------------------------------------------------------------------------
    // ThreeDimensionalMatchingBruteForce
    //
    // The instance below is deliberately built with single-element X/Y/Z header groups so
    // its solver output can be hand-verified independent of the default instance's larger
    // search space.
    // -------------------------------------------------------------------------

    [Fact]
    public void DM3_BruteForce_Minimal_Instance_Finds_Immediate_Match() {
        // X = {A}, Y = {B}, Z = {C} (single-element sets), and M contains the matching
        // triple {A,B,C} -- the very first candidate combination succeeds immediately.
        string instance = "{A}{B}{C}{A,B,C}";
        DM3 problem = new DM3(instance);
        ThreeDimensionalMatchingBruteForce solver = new ThreeDimensionalMatchingBruteForce();

        string solution = solver.solve(problem);

        Assert.Equal("{{A,B,C}}", solution);
        Assert.True(new GenericVerifierDM3().verify(problem, solution));
    }

    [Fact]
    public void DM3_BruteForce_Solver_Finds_A_Matching_On_Default_Instance() {
        DM3 problem = new DM3();
        ThreeDimensionalMatchingBruteForce solver = new ThreeDimensionalMatchingBruteForce();

        string certificate = solver.solve(problem);

        Assert.NotEqual("{}", certificate);
        Assert.True(problem.defaultVerifier.verify(problem, certificate));
    }

    // -------------------------------------------------------------------------
    // HurkensSchrijver
    //
    // Note: HurkensSchrijver does not implement ISolver<DM3> (the interface is commented
    // out in source: "class HurkensSchrijver /*: ISolver*/") and its solve() returns
    // List<List<string>> rather than a certificate string, so it is exercised directly
    // (not through the ISolver contract) and its output is converted to the
    // GenericVerifierDM3 certificate format by the helper below.
    // -------------------------------------------------------------------------

    [Fact]
    public void HurkensSchrijver_Minimal_Instance_No_Swap_Available_Keeps_Single_Triple() {
        // X = {A}, Y = {B}, Z = {C}, M = {{A,B,C}}. S seeds with M[0] = {A,B,C}; after
        // RemoveAt(0), M is empty, so the inner search never finds a swap and the
        // S.Count == currentCount fallback branch is taken every pass, leaving S unchanged
        // at a single triple.
        string instance = "{A}{B}{C}{A,B,C}";
        DM3 problem = new DM3(instance);
        List<List<string>> originalM = problem.M.Select(t => t.ToList()).ToList();
        HurkensSchrijver solver = new HurkensSchrijver();

        List<List<string>> result = solver.solve(problem);

        Assert.Single(result);
        Assert.Equal(new List<string> { "A", "B", "C" }, result[0]);

        // solve() mutates problem.M (see below), so check against the copy taken beforehand.
        // The verifier requires a perfect matching, so only the packing property is checked here.
        Assert.True(IsPacking(originalM, result));
    }

    [Fact]
    public void HurkensSchrijver_Default_Instance_Swaps_Seed_For_Two_Compatible_Triples() {
        // On the default instance (M seeded from the 6 real candidate triples -- see
        // DM3_ParseM_Only_Contains_M_Triples above), the solver's swap search settles on
        // 2 mutually-disjoint triples covering all 6 elements without overlap.
        DM3 problem = new DM3();
        List<List<string>> originalM = problem.M.Select(t => t.ToList()).ToList();
        HurkensSchrijver solver = new HurkensSchrijver();

        List<List<string>> result = solver.solve(problem);

        Assert.Equal(2, result.Count);
        foreach (var triple in result) {
            Assert.Equal(3, triple.Count);
        }
        // The two retained triples together must use six distinct elements (no coordinate
        // reused), matching the "works" check in the source.
        var flattened = result.SelectMany(t => t).ToList();
        Assert.Equal(flattened.Count, flattened.Distinct().Count());

        // A local-search packing of 2 triples, not a perfect matching (that needs 3).
        Assert.True(IsPacking(originalM, result));
    }

    [Fact]
    public void HurkensSchrijver_Solve_Mutates_Input_Problem_M() {
        // solve() aliases problem.M directly (List<List<string>> M = problem.M;) rather
        // than copying it, so RemoveAt(0) on the local variable mutates the caller's
        // problem.M as a side effect of calling solve().
        DM3 problem = new DM3();
        int originalCount = problem.M.Count;
        HurkensSchrijver solver = new HurkensSchrijver();

        solver.solve(problem);

        Assert.Equal(originalCount - 1, problem.M.Count);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    // True when every triple is in M and no two triples share an element in any coordinate.
    private static bool IsPacking(List<List<string>> m, List<List<string>> triples) {
        for (int c = 0; c < 3; c++) {
            if (triples.Select(t => t[c]).Distinct().Count() != triples.Count) return false;
        }
        return triples.All(t => m.Any(mt => mt.SequenceEqual(t)));
    }
}
