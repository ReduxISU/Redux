// BINPACKING_Tests.cs
// Unit tests for the Bin Packing problem implementation.
//
// Test coverage includes:
//   - Problem class instantiation (default and custom instances)
//   - Verifier: valid certificates that should return true
//   - Verifier: invalid certificates that should return false
//   - BruteForce solver: feasible instances (checks exact certificate output)
//   - BruteForce solver: infeasible instances (must return empty string)
//   - FFD solver: feasible instances (checks exact certificate output)
//   - FFD solver: infeasible/over-K instances (must return empty string)
//   - Round-trip: solver output fed directly into verifier must always pass
//
// All expected certificate strings were hand-traced before writing the tests.

using Xunit;
using API.Problems.NPComplete.NPC_BINPACKING;
using API.Problems.NPComplete.NPC_BINPACKING.Verifiers;
using API.Problems.NPComplete.NPC_BINPACKING.Solvers;

namespace redux_tests;
#pragma warning disable CS1591

public class BINPACKING_Tests {

    // Instantiation
    // Make sure the constructor correctly parses the instance string into
    // the S (item sizes), C (bin capacity), and K (bin limit) fields.

    [Fact]
    public void BINPACKING_Default_Instantiation() {
        // Checking default constructor loading and parsing the example instance correctly.
        BINPACKING bp = new BINPACKING();
        Assert.Equal("((4,7,3,6,2,8),10,3)", bp.instance);
        Assert.Equal("((4,7,3,6,2,8),10,3)", bp.defaultInstance);
        Assert.Equal(new List<int> { 4, 7, 3, 6, 2, 8 }, bp.S);
        Assert.Equal(10, bp.C);
        Assert.Equal(3, bp.K);
    }

    [Fact]
    public void BINPACKING_Custom_Instantiation() {
        // Checking custom instance parsing and confirming values loading correctly.
        string input = "((1,2,3),6,2)";
        BINPACKING bp = new BINPACKING(input);
        Assert.Equal(input, bp.instance);
        Assert.Equal(new List<int> { 1, 2, 3 }, bp.S);
        Assert.Equal(6, bp.C);
        Assert.Equal(2, bp.K);
    }

    //  Verifier — valid certificates (should return true) 
    // Each case is a YES-instance with a correctly formed certificate.
    // We test two different valid packings of the same default instance
    // to confirm the verifier accepts any correct packing, not just one specific one.

    [Theory]
    [InlineData("((4,7,3,6,2,8),10,3)", "((4,6),(7,3),(2,8))", true)]
    // brute force output for the default instance (traced by hand)
    [InlineData("((4,7,3,6,2,8),10,3)", "((8,2),(7,3),(6,4))", true)]
    // FFD output for the default instance (sorted descending placement)
    [InlineData("((1,2,3),6,2)", "((3,2),(1))", true)]
    // simple 3-item instance: bin 0 sum=5 ≤ 6, bin 1 sum=1 ≤ 6, 2 bins ≤ K=2
    [InlineData("((5),10,1)", "((5))", true)]
    // single item that fits exactly — minimum possible case
    public void BINPACKING_Verifier_True(string instance, string certificate, bool expected) {
        BINPACKING bp = new BINPACKING(instance);
        BinPackingVerifier verifier = new BinPackingVerifier();
        Assert.Equal(expected, verifier.verify(bp, certificate));
    }

    //  Verifier — invalid certificates (should return false) 
    // Each case targets a different failure mode so we know every check works.

    [Theory]
    [InlineData("((1,2,3),3,2)", "((3,1),(2))", false)]
    // Checking capacity violation handling.
    [InlineData("((1,2,3),6,1)", "((1,2),(3))", false)]
    // Verifying bin limit validation.
    [InlineData("((4,7,3,6,2,8),10,3)", "((10),(10),(10))", false)]
    // multiset check fails: certificate items {10,10,10} ≠ S {4,7,3,6,2,8}
    [InlineData("((4,7,3,6,2,8),10,3)", "", false)]
    // malformed input: empty string has no outer parentheses
    [InlineData("((4,7,3,6,2,8),10,3)", "not-a-cert", false)]
    // malformed input: plain text doesn't match the expected format at all
    public void BINPACKING_Verifier_False(string instance, string certificate, bool expected) {
        BINPACKING bp = new BINPACKING(instance);
        BinPackingVerifier verifier = new BinPackingVerifier();
        Assert.Equal(expected, verifier.verify(bp, certificate));
    }

    //  Brute Force Solver — feasible instances 
    // These are YES-instances. The brute force solver must find a valid packing
    // and return the exact certificate we traced by hand.
    //
    // Default instance trace (items [4,7,3,6,2,8], C=10, K=3):
    //   4→bin0, 7→bin1, 3→bin0 (sum=7), 6→bin0(13>10)→bin1(13>10)→bin2,
    //   2→bin0(9), 8→bin0(17>10)→bin1(15>10)→bin2(14>10) → backtrack to 3 in bin1,
    //   then 6→bin0(10), 2→bin2, 8→bin2(10) → "((4,6),(7,3),(2,8))"

    [Theory]
    [InlineData("((4,7,3,6,2,8),10,3)", "((4,6),(7,3),(2,8))")]
    // default instance — certificate traced step by step above
    [InlineData("((1,1,1),2,2)", "((1,1),(1))")]
    // 3 items of size 1, capacity 2, 2 bins: first two items share bin 0
    [InlineData("((5,5,5),10,2)", "((5,5),(5))")]
    // two items of size 5 fit together (sum=10=C), the third goes alone
    public void BINPACKING_BruteForce_Feasible(string instance, string expectedCert) {
        BINPACKING bp = new BINPACKING(instance);
        BinPackingBruteForce solver = new BinPackingBruteForce();
        string result = solver.solve(bp);
        Assert.Equal(expectedCert, result);
    }

    //  Brute Force Solver — infeasible instances
    // These are NO-instances. The solver must exhaustively confirm no packing
    // exists and return an empty string "".

    [Theory]
    [InlineData("((6,6,6),10,1)")]
    // three items of size 6 can't all fit in one bin of capacity 10 (6+6=12>10)
    [InlineData("((11,5),10,3)")]
    // item size 11 exceeds the bin capacity C=10 — impossible regardless of K
    public void BINPACKING_BruteForce_Infeasible_Returns_Empty(string instance) {
        BINPACKING bp = new BINPACKING(instance);
        BinPackingBruteForce solver = new BinPackingBruteForce();
        Assert.Equal("", solver.solve(bp));
    }

    // FFD Solver — feasible instances 
    // FFD sorts items largest-first, then places each item in the first bin
    // that still has room. We verify the exact output of that greedy process.
    //
    // Default instance trace (sorted [8,7,6,4,3,2], C=10, K=3):
    //   8→new bin0, 7→new bin1, 6→new bin2, 4→bin2(10), 3→bin1(10), 2→bin0(10)
    //   → "((8,2),(7,3),(6,4))"

    [Theory]
    [InlineData("((4,7,3,6,2,8),10,3)", "((8,2),(7,3),(6,4))")]
    // default instance — FFD sorts to [8,7,6,4,3,2] and fills bins perfectly
    [InlineData("((1,2,3,4),5,2)", "((4,1),(3,2))")]
    // sorted [4,3,2,1]: 4→bin0, 3→bin1, 2→bin1(5=C), 1→bin0(5=C) → "((4,1),(3,2))"
    public void BINPACKING_FFD_Feasible(string instance, string expectedCert) {
        BINPACKING bp = new BINPACKING(instance);
        BinPackingFFD solver = new BinPackingFFD();
        string result = solver.solve(bp);
        Assert.Equal(expectedCert, result);
    }

    //  FFD Solver — over-K or impossible instances 
    // When FFD would need more than K bins, it returns "" rather than exceeding
    // the limit. Note: "" from FFD does NOT prove infeasibility — use brute force.

    [Theory]
    [InlineData("((6,6,6),10,1)")]
    // FFD opens bin0 for first 6, then can't place second 6 (12>10) and
    //   opening bin1 would exceed K=1 — so it gives up and returns ""
    [InlineData("((11,5),10,3)")]
    // item size 11 exceeds C=10 — caught by the pre-check, returns "" immediately
    public void BINPACKING_FFD_Infeasible_Returns_Empty(string instance) {
        BINPACKING bp = new BINPACKING(instance);
        BinPackingFFD solver = new BinPackingFFD();
        Assert.Equal("", solver.solve(bp));
    }

    //  Round-trip: Solver output → Verifier 
    // Any certificate produced by a solver must be accepted by the verifier.
    // This catches any mismatch between the certificate format the solver
    // outputs and the format the verifier expects to parse.

    [Theory]
    [InlineData("((4,7,3,6,2,8),10,3)")]
    [InlineData("((1,2,3),6,2)")]
    [InlineData("((5,5,5),10,2)")]
    public void BINPACKING_BruteForce_Output_Passes_Verifier(string instance) {
        BINPACKING bp = new BINPACKING(instance);
        BinPackingBruteForce solver = new BinPackingBruteForce();
        BinPackingVerifier verifier = new BinPackingVerifier();
        string cert = solver.solve(bp);
        // The brute force only returns a non-empty cert on YES-instances,
        // so the verifier must always accept it.
        Assert.True(verifier.verify(bp, cert));
    }

    [Theory]
    [InlineData("((4,7,3,6,2,8),10,3)")]
    [InlineData("((1,2,3,4),5,2)")]
    public void BINPACKING_FFD_Output_Passes_Verifier(string instance) {
        BINPACKING bp = new BINPACKING(instance);
        BinPackingFFD solver = new BinPackingFFD();
        BinPackingVerifier verifier = new BinPackingVerifier();
        string cert = solver.solve(bp);
        // FFD found a valid packing for these instances, so the verifier must accept it.
        Assert.True(verifier.verify(bp, cert));
    }
}
