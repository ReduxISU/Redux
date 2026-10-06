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
        Assert.Contains("3-tuples", problem.certificateFormat);
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
    // HurkensShrijver
    //
    // Note: HurkensShrijver does not implement ISolver<DM3> (the interface is commented
    // out in source: "class HurkensShrijver /*: ISolver*/") and its solve() returns
    // List<List<string>> rather than a certificate string, so it is exercised directly
    // (not through the ISolver contract) and its output is converted to the
    // GenericVerifierDM3 certificate format by the helper below.
    // -------------------------------------------------------------------------

    [Fact]
    public void HurkensShrijver_Minimal_Instance_No_Swap_Available_Keeps_Single_Triple() {
        // X = {A}, Y = {B}, Z = {C}, M = {{A,B,C}}. S seeds with M[0] = {A,B,C}; after
        // RemoveAt(0), M is empty, so the inner search never finds a swap and the
        // S.Count == currentCount fallback branch is taken every pass, leaving S unchanged
        // at a single triple.
        string instance = "{A}{B}{C}{A,B,C}";
        DM3 problem = new DM3(instance);
        HurkensShrijver solver = new HurkensShrijver();

        List<List<string>> result = solver.solve(problem);

        Assert.Single(result);
        Assert.Equal(new List<string> { "A", "B", "C" }, result[0]);

        string certificate = CertificateFromTriples(result);
        Assert.True(new GenericVerifierDM3().verify(problem, certificate));
    }

    [Fact]
    public void HurkensShrijver_Default_Instance_Swaps_Seed_For_Two_Compatible_Triples() {
        // On the default instance (M seeded from the 6 real candidate triples -- see
        // DM3_ParseM_Only_Contains_M_Triples above), the solver's swap search settles on
        // 2 mutually-disjoint triples covering all 6 elements without overlap.
        DM3 problem = new DM3();
        HurkensShrijver solver = new HurkensShrijver();

        List<List<string>> result = solver.solve(problem);

        Assert.Equal(2, result.Count);
        foreach (var triple in result) {
            Assert.Equal(3, triple.Count);
        }
        // The two retained triples together must use six distinct elements (no coordinate
        // reused), matching the "works" check in the source.
        var flattened = result.SelectMany(t => t).ToList();
        Assert.Equal(flattened.Count, flattened.Distinct().Count());

        string certificate = CertificateFromTriples(result);
        Assert.True(new GenericVerifierDM3().verify(problem, certificate));
    }

    [Fact]
    public void HurkensShrijver_Solve_Mutates_Input_Problem_M() {
        // solve() aliases problem.M directly (List<List<string>> M = problem.M;) rather
        // than copying it, so RemoveAt(0) on the local variable mutates the caller's
        // problem.M as a side effect of calling solve().
        DM3 problem = new DM3();
        int originalCount = problem.M.Count;
        HurkensShrijver solver = new HurkensShrijver();

        solver.solve(problem);

        Assert.Equal(originalCount - 1, problem.M.Count);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string CertificateFromTriples(List<List<string>> triples) {
        var sb = new System.Text.StringBuilder("{");
        for (int t = 0; t < triples.Count; t++) {
            if (t > 0) sb.Append(',');
            sb.Append('{').Append(string.Join(",", triples[t])).Append('}');
        }
        sb.Append('}');
        return sb.ToString();
    }
}
