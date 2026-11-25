using System.Globalization;
using System.Numerics;
using API.Interfaces;
using API.Problems.NPHard.NPH_MAXIMUMVALUEKNAPSACK;
using API.Problems.NPHard.NPH_MAXIMUMVALUEKNAPSACK.Verifiers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class MaximumValueKnapsackVerifier_Tests {
    [Theory]
    [InlineData("({2,3},220)", true)]
    [InlineData("({3,2},220)", true)]
    [InlineData(" \t( { 03 , 002 } , 0220 )\r\n", true)]
    [InlineData("({1},60)", true)]
    [InlineData("({1,2},160)", true)]
    [InlineData("({},0)", true)]
    [InlineData("({},1)", false)]
    [InlineData("({1,2,3},280)", false)]
    [InlineData("({2,3},219)", false)]
    [InlineData("({2,3},9223372036854775807)", false)]
    [InlineData("({0},0)", false)]
    [InlineData("({4},0)", false)]
    [InlineData("({2147483648},0)", false)]
    [InlineData("({9223372036854775807},0)", false)]
    public void Verifier_ChecksFeasibilityAndValueWithoutRequiringOptimality(string certificate, bool expected) {
        var problem = new MAXIMUMVALUEKNAPSACK();
        var verifier = new MaximumValueKnapsackSolutionVerifier();
        Assert.Equal(expected, verifier.verify(problem, certificate));
        Assert.Equal(expected, ((IVerifier)verifier).verify(problem.instance, certificate));
    }

    public static TheoryData<string?> MalformedCertificates => new() {
        (string?)null, "", " ", "garbage", "{}", "()", "({})", "{1},60", "((1),60)",
        "({1},60,0)", "({1},)", "(,0)", "({},,0)", "({},0,)", "({,},0)",
        "({1,},60)", "({,1},60)", "({1,,2},160)", "({1}{2},160)",
        "({(1)},60)", "({{1}},60)", "({1},(60))", "({1},())",
        "({1],60)", "([{1}],60)", "({1},60", "({1},60))", "({1}},60)",
        "({1},60)trailing", "({1},60)()", "({1},60) ({},0)",
        "({1 2},160)", "({1},6 0)", "({1},60\0)",
        "({1,1},120)", "({01,1},120)", "({4,4},0)", "({0,00},0)",
        "({9223372036854775807,09223372036854775807},0)"
    };

    [Theory]
    [MemberData(nameof(MalformedCertificates))]
    public void MalformedCertificate_ThrowsTypedErrorThroughBothEntryPoints(string? certificate) {
        var problem = new MAXIMUMVALUEKNAPSACK();
        var verifier = new MaximumValueKnapsackSolutionVerifier();
        var error = Assert.Throws<CertificateParseException>(() => verifier.verify(problem, certificate!));
        Assert.Same(problem, error.Problem);
        Assert.Equal(certificate, error.Received);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        Assert.Throws<CertificateParseException>(() => ((IVerifier)verifier).verify(problem.instance, certificate!));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("+0")]
    [InlineData("1.0")]
    [InlineData("1e2")]
    [InlineData("0x10")]
    [InlineData("NaN")]
    [InlineData("one")]
    [InlineData("٤")]
    [InlineData("４")]
    [InlineData("9223372036854775808")]
    [InlineData("18446744073709551615")]
    [InlineData("9999999999999999999999999999999999")]
    [InlineData("1\0")]
    [InlineData("1\t2")]
    public void InvalidNumericTokens_AreMalformedInBothPositions(string token) {
        var verifier = new MaximumValueKnapsackSolutionVerifier();
        var problem = new MAXIMUMVALUEKNAPSACK();
        foreach (string certificate in new[] { $"({{{token}}},0)", $"({{}},{token})", $"({{4,{token}}},0)" }) {
            Assert.Throws<CertificateParseException>(() => verifier.verify(problem, certificate));
            Assert.Throws<CertificateParseException>(() => ((IVerifier)verifier).verify(problem.instance, certificate));
        }
    }

    [Fact]
    public void EmbeddedNul_IsRejectedAtEveryPosition() {
        const string certificate = MaximumValueKnapsackSolutionVerifier.CertificateExample;
        var verifier = new MaximumValueKnapsackSolutionVerifier();
        var problem = new MAXIMUMVALUEKNAPSACK();
        for (int i = 0; i <= certificate.Length; i++)
            Assert.Throws<CertificateParseException>(() => verifier.verify(problem, certificate.Insert(i, "\0")));
    }

    [Theory]
    [InlineData("((),0)", "({},0)", true)]
    [InlineData("((),0)", "({1},0)", false)]
    [InlineData("(((0,7),(0,0),(1,5)),0)", "({1,2},7)", true)]
    [InlineData("(((0,7),(0,0),(1,5)),0)", "({2},0)", true)]
    [InlineData("(((0,7),(0,0),(1,5)),0)", "({3},5)", false)]
    [InlineData("(((2,3),(2,3)),4)", "({1,2},6)", true)]
    [InlineData("(((2,3),(2,3)),2)", "({2},3)", true)]
    [InlineData("(((2,3),(2,3)),2)", "({1,2},6)", false)]
    [InlineData("(((0,9223372036854775806),(0,1)),0)", "({2,1},9223372036854775807)", true)]
    [InlineData("(((9223372036854775807,1),(9223372036854775807,2)),9223372036854775807)", "({1,2},3)", false)]
    [InlineData("(((9223372036854775807,1),(0,2)),9223372036854775807)", "({1,2},3)", true)]
    public void BoundaryInstances_AreVerifiedWithoutOverflow(string instance, string certificate, bool expected) {
        var verifier = new MaximumValueKnapsackSolutionVerifier();
        Assert.Equal(expected, verifier.verify(new MAXIMUMVALUEKNAPSACK(instance), certificate));
        Assert.Equal(expected, ((IVerifier)verifier).verify(instance, certificate));
    }

    [Fact]
    public void Verifier_HasNoExactSearchLimit() {
        string items = string.Join(",", Enumerable.Repeat("(0,1)", 100));
        var problem = new MAXIMUMVALUEKNAPSACK($"(({items}),0)");
        Assert.True(new MaximumValueKnapsackSolutionVerifier().verify(problem, "({100},1)"));
    }

    [Fact]
    public void DiscoveryAndMetadata_RegisterOnlyTheSolutionVerifierAsDefault() {
        IProblem problem = new MAXIMUMVALUEKNAPSACK();
        var verifier = Assert.IsType<MaximumValueKnapsackSolutionVerifier>(problem.defaultVerifier);
        Assert.Same(verifier, problem.defaultVerifier);
        Assert.Equal(typeof(MaximumValueKnapsackSolutionVerifier), ProblemProvider.Verifiers["maximumvalueknapsacksolutionverifier"]);
        Assert.Equal(typeof(MAXIMUMVALUEKNAPSACK), ProblemProvider.ProblemTypeOf(typeof(MaximumValueKnapsackSolutionVerifier), typeof(IVerifier<>)));
        Assert.Equal(typeof(MaximumValueKnapsackSolutionVerifier), ProblemDefaults.Find(typeof(MAXIMUMVALUEKNAPSACK), typeof(IVerifier)));
        Assert.Equal("Maximum-Value 0-1 Knapsack Solution Verifier", verifier.verifierName);
        Assert.Contains("does not check optimality", verifier.verifierDefinition);
        Assert.NotEmpty(verifier.source);
        Assert.NotEmpty(verifier.sourceLink);
        Assert.NotEmpty(verifier.contributors);
        Assert.EndsWith("MaximumValueKnapsackSolutionVerifier.cs", verifier.sourceFile);
        Assert.True(verifier.verify(new MAXIMUMVALUEKNAPSACK(), verifier.certificate));
        Assert.Throws<ProblemParseException>(() => ((IVerifier)verifier).verify("garbage", "({},0)"));
    }

    [Fact]
    public void NumericParsing_IsCultureIndependent() {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            Assert.True(new MaximumValueKnapsackSolutionVerifier().verify(new MAXIMUMVALUEKNAPSACK(), "({02,3},0220)"));
        } finally {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void EverySmallSubset_AgreesWithIndependentArithmeticAndRejectsWrongValues() {
        var random = new Random(8742);
        var verifier = new MaximumValueKnapsackSolutionVerifier();
        for (int trial = 0; trial < 40; trial++) {
            int n = trial % 9;
            long[] weights = Enumerable.Range(0, n).Select(_ => random.Next(4) == 0 ? long.MaxValue : random.NextInt64(0, 20)).ToArray();
            long[] values = Enumerable.Range(0, n).Select(_ => random.NextInt64(0, 100)).ToArray();
            long capacity = trial % 2 == 0 ? long.MaxValue : random.NextInt64(0, 40);
            string pairs = string.Join(",", weights.Select((weight, i) => $"({weight.ToString(CultureInfo.InvariantCulture)},{values[i].ToString(CultureInfo.InvariantCulture)})"));
            var problem = new MAXIMUMVALUEKNAPSACK($"(({pairs}),{capacity.ToString(CultureInfo.InvariantCulture)})");
            for (int mask = 0; mask < (1 << n); mask++) {
                int[] selected = Enumerable.Range(0, n).Where(i => (mask & (1 << i)) != 0).ToArray();
                BigInteger weight = selected.Aggregate(BigInteger.Zero, (sum, i) => sum + weights[i]);
                long value = selected.Sum(i => values[i]);
                string indices = string.Join(",", selected.Reverse().Select(i => (i + 1).ToString(CultureInfo.InvariantCulture)));
                Assert.Equal(weight <= capacity, verifier.verify(problem, $"({{{indices}}},{value.ToString(CultureInfo.InvariantCulture)})"));
                Assert.False(verifier.verify(problem, $"({{{indices}}},{(value + 1).ToString(CultureInfo.InvariantCulture)})"));
            }
        }
    }
}
