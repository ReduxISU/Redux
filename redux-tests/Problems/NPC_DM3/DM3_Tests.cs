using Xunit;
using API.Problems.NPComplete.NPC_DM3;
using API.Problems.NPComplete.NPC_DM3.Solvers;
using API.Problems.NPComplete.NPC_DM3.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class DM3_Tests {
    // -------------------------------------------------------------------------
    // ThreeDimensionalMatchingBruteForce
    //
    // Note on DM3's instance parsing: DM3.ParseProblem(instanceInput, "X"/"Y"/"Z") walks
    // the ENTIRE flattened token stream (every element from every brace group, including
    // the M triples) with a stride of 3, rather than restricting to just the X/Y/Z header
    // groups. Likewise DM3.ParseM chunks the entire flattened stream into groups of 3
    // regardless of brace boundaries, so the X/Y/Z header groups themselves end up as
    // extra (bogus) entries at the front of problem.M. See the BUG test at the bottom of
    // this file for a concrete demonstration using the documented default instance. The
    // instances below are deliberately built with single-element X/Y/Z header groups
    // (which happen to parse cleanly despite this bug) so their solver output can be
    // hand-verified.
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
    public void DM3_BruteForce_Default_Instance_Exhausts_Search_Without_A_Match() {
        // The default instance's header groups (X={Paul,Sally,Dave}, Y={Madison,Austin,Bob},
        // Z={Chloe,Frank,Jake}) get folded into problem.M as three extra pseudo-triples by
        // the parsing bug documented above, inflating |M| to 9 and -- more importantly --
        // inflating problem.X/Y/Z themselves to 5 elements each (cross-contaminated with
        // each other's first/second/third coordinates). A full matching would need 5
        // mutually-disjoint triples, which this instance does not have, so the brute force
        // solver exhausts all C(9,5) = 126 combinations without success. This still gives
        // solid coverage of the solver's own logic (factorial/reps computation, repeated
        // nextComb advancement, and indexListToCertificate building 5-triple certificates
        // on every failed attempt) even though the final answer is empty.
        DM3 problem = new DM3();
        ThreeDimensionalMatchingBruteForce solver = new ThreeDimensionalMatchingBruteForce();

        string solution = solver.solve(problem);

        Assert.Equal("{}", solution);
        Assert.Equal(5, problem.X.Count);
        Assert.Equal(9, problem.M.Count);
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
        // X = {A}, Y = {B}, Z = {C}. S seeds with M[0] = {A,B,C}; after RemoveAt(0), the
        // only remaining candidate in M is another {A,B,C}, which cannot pair with itself
        // (setM1 == setM2 is always skipped), so the inner search never finds a swap and
        // the S.Count == currentCount fallback branch is taken every pass, leaving S
        // unchanged at a single triple.
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
        // On the default instance, the seed M[0] (the header-derived pseudo-triple
        // {Paul,Sally,Dave} -- see the parsing-bug note above) gets successfully replaced:
        // the "works" branch fires and S grows from 1 triple to 2 (net +1: -1 removed,
        // +2 added), landing on {Madison,Austin,Bob} and {Chloe,Frank,Jake}. A further
        // while-loop pass then fails to extend further and the loop terminates. This
        // exercises the seed setup, SHash bookkeeping, the successful "works == true"
        // swap-in-two branch, and the terminating fallback branch all in one call.
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
    // Documented bug: DM3's instance parsing cross-contaminates X/Y/Z with each other
    // and with M.
    // -------------------------------------------------------------------------

    [Fact(Skip = "BUG: DM3.ParseProblem strides by 3 over the ENTIRE flattened instance " +
        "(header groups AND every M triple), instead of stopping at the end of the " +
        "relevant header group, so problem.X/Y/Z each pick up spurious elements from the " +
        "other two header groups. DM3.ParseM independently chunks the entire flattened " +
        "stream into groups of 3, so the X/Y/Z header groups themselves are added to " +
        "problem.M as three extra pseudo-triples. For the documented default instance " +
        "(intended X={Paul,Sally,Dave}, Y={Madison,Austin,Bob}, Z={Chloe,Frank,Jake}, a " +
        "solvable 3-matching), the parsed problem.X actually comes out as " +
        "{Paul,Madison,Chloe,Sally,Dave} (5 elements) and problem.M.Count is 9 instead of " +
        "6 -- as a direct consequence, ThreeDimensionalMatchingBruteForce.solve() on the " +
        "default instance returns \"{}\" (no solution found) even though the problem's " +
        "own documented default instance is supposed to have a valid matching.")]
    public void DM3_ParseProblem_Cross_Contaminates_Header_Sets_And_M() {
        DM3 problem = new DM3();

        // Intended per the documented default instance: X = {Paul, Sally, Dave}.
        Assert.Equal(new List<string> { "Paul", "Sally", "Dave" }, problem.X);
        // Intended: M has exactly the 6 listed candidate triples.
        Assert.Equal(6, problem.M.Count);

        // As a consequence, the brute-force solver -- whose own search logic is
        // otherwise correct -- fails to find the matching the default instance was
        // written to demonstrate.
        string solution = new ThreeDimensionalMatchingBruteForce().solve(problem);
        Assert.NotEqual("{}", solution);
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
