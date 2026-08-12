using Xunit;
using API.Interfaces;
using API.Problems.NPComplete.NPC_SUBSETSUM;
using API.Problems.NPComplete.NPC_SUBSETSUM.Solvers;
using API.Problems.NPComplete.NPC_SUBSETSUM.Verifiers;
using API.Problems.NPComplete.NPC_SUBSETSUM.ReduceTo.NPC_KNAPSACK;
using API.Problems.NPComplete.NPC_SUBSETSUM.ReduceTo.NPC_PARTITION;
using API.Problems.NPComplete.NPC_EXACTCOVER.ReduceTo.NPC_SUBSETSUM;

namespace redux_tests;
#pragma warning disable CS1591

public class SUBSETSUM_Tests {
        // -------------------------------------------------------------------------
        // Instantiation
        // -------------------------------------------------------------------------

        [Fact]
        public void SUBSETSUM_Default_Instantiation() {
                SUBSETSUM problem = new SUBSETSUM();
                Assert.Equal("({1,7,12,15},28)", problem.instance);
                Assert.Equal("({1,7,12,15},28)", problem.defaultInstance);
                Assert.Equal(4, problem.S.Count);
                Assert.Equal(28, problem.T);
        }

        [Fact]
        public void SUBSETSUM_Custom_Instantiation() {
                SUBSETSUM problem = new SUBSETSUM("({3,5,9},14)");
                Assert.Equal("({3,5,9},14)", problem.instance);
                Assert.Equal(3, problem.S.Count);
                Assert.Equal(14, problem.T);
                Assert.Contains("3", problem.S);
                Assert.Contains("5", problem.S);
                Assert.Contains("9", problem.S);
        }

        // -------------------------------------------------------------------------
        // Constructor — invalid instances (all must throw ProblemParseException)
        // -------------------------------------------------------------------------

        [Theory]
        [InlineData("{{1,7,12,15} : 28}")] // old colon format
        [InlineData("")]                    // empty
        [InlineData("101")]                 // bare string
        [InlineData("({1,x,3},5)")]         // non-integer element in S
        [InlineData("({1,2,3},y)")]         // non-integer T
        public void SUBSETSUM_Constructor_Throws_On_Invalid_Instance(string instance) {
                Assert.Throws<ProblemParseException>(() => new SUBSETSUM(instance));
        }

        // -------------------------------------------------------------------------
        // Verifier — valid certificates
        // -------------------------------------------------------------------------

        [Theory]
        [InlineData("({1,7,12,15},28)", "{1,12,15}")] // 1 + 12 + 15 = 28
        [InlineData("({5,1,2},5)", "{5}")]             // singleton {S[0]} is a valid witness
        [InlineData("({3,5,9},8)", "{3,5}")]           // 3 + 5 = 8
        public void SUBSETSUM_Verifier_Accepts_Valid_Certificate(string instance, string certificate) {
                SUBSETSUM problem = new SUBSETSUM(instance);
                SubsetSumVerifier verifier = new SubsetSumVerifier();
                Assert.True(verifier.verify(problem, certificate));
        }

        // -------------------------------------------------------------------------
        // Verifier — well-formed but wrong certificates (must return false)
        // -------------------------------------------------------------------------

        [Theory]
        [InlineData("({1,7,12,15},28)", "{1,7}")]   // sum 8 != 28
        [InlineData("({1,7,12,15},28)", "{1,99}")]  // 99 is not a member of S
        [InlineData("({1,7,12,15},28)", "101")]     // SPADE reads a bare scalar as {101}; 101 is not in S
        [InlineData("({7,1},14)", "{7,7}")]         // reuses 7 beyond its multiplicity in S (#259)
        public void SUBSETSUM_Verifier_Rejects_Invalid_Certificate(string instance, string certificate) {
                SUBSETSUM problem = new SUBSETSUM(instance);
                SubsetSumVerifier verifier = new SubsetSumVerifier();
                Assert.False(verifier.verify(problem, certificate));
        }

        // -------------------------------------------------------------------------
        // Verifier — malformed certificates (must throw CertificateParseException)
        // -------------------------------------------------------------------------

        [Theory]
        [InlineData("")]          // empty — SPADE parse failure
        [InlineData("{1,x,3}")]   // non-integer token
        public void SUBSETSUM_Verifier_Throws_On_Malformed_Certificate(string certificate) {
                SUBSETSUM problem = new SUBSETSUM("({1,7,12,15},28)");
                SubsetSumVerifier verifier = new SubsetSumVerifier();
                Assert.Throws<CertificateParseException>(() => verifier.verify(problem, certificate));
        }

        // -------------------------------------------------------------------------
        // Solver
        // -------------------------------------------------------------------------

        [Theory]
        [InlineData("({1,7,12,15},28)")]
        [InlineData("({3,5,9},14)")] // 5 + 9
        [InlineData("({2,4,6},10)")] // 4 + 6
        public void SUBSETSUM_Solver_ProducesVerifiableCertificate(string instance) {
                SUBSETSUM problem = new SUBSETSUM(instance);
                SubsetSumBruteForce solver = new SubsetSumBruteForce();
                SubsetSumVerifier verifier = new SubsetSumVerifier();
                string solution = solver.solve(problem);
                Assert.True(verifier.verify(problem, solution));
        }

        [Fact]
        public void SUBSETSUM_Solver_Finds_FirstElement_Only_Solution() {
                // Regression for the enumeration bug: the only subset summing to 5 is {5},
                // the singleton of the first element, which the old solver never tested.
                SUBSETSUM problem = new SUBSETSUM("({5,1,2},5)");
                SubsetSumBruteForce solver = new SubsetSumBruteForce();
                string solution = solver.solve(problem);
                Assert.Equal("{5}", solution);
        }

        [Fact]
        public void SUBSETSUM_Solver_Returns_Empty_When_No_Solution() {
                SUBSETSUM problem = new SUBSETSUM("({2,4,6},5)");
                SubsetSumBruteForce solver = new SubsetSumBruteForce();
                Assert.Equal("{}", solver.solve(problem));
        }

        // -------------------------------------------------------------------------
        // Reductions — must accept / produce the SPADE instance format
        // -------------------------------------------------------------------------

        [Fact]
        public void SUBSETSUM_FengReduction_Accepts_New_Instance_Format() {
                FengReduction reduction = new FengReduction("({3,5,7},8)");
                Assert.Equal(3, reduction.reductionFrom.S.Count);
                Assert.Equal(8, reduction.reductionFrom.T);
                Assert.NotNull(reduction.reductionTo);
        }

        [Fact]
        public void SUBSETSUM_PartitionReduction_Accepts_New_Instance_Format() {
                SubsetSumToPartitionReduction reduction = new SubsetSumToPartitionReduction("({3,5,7},8)");
                Assert.Equal(3, reduction.reductionFrom.S.Count);
                Assert.NotNull(reduction.reductionTo);
        }

        [Fact]
        public void SUBSETSUM_KarpExactCoverReduction_Produces_Parseable_Instance() {
                KarpExactCoverToSubsetSum reduction = new KarpExactCoverToSubsetSum();
                string producedInstance = reduction.reductionTo.instance;
                Assert.StartsWith("({", producedInstance);

                // The produced instance must round-trip through the SPADE constructor.
                SUBSETSUM roundTrip = new SUBSETSUM(producedInstance);
                Assert.Equal(reduction.reductionTo.S.Count, roundTrip.S.Count);
                Assert.Equal(reduction.reductionTo.T, roundTrip.T);
        }
}
