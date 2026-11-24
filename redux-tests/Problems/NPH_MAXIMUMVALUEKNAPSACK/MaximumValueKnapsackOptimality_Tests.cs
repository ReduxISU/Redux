using API.Interfaces;
using API.Problems.NPHard.NPH_MAXIMUMVALUEKNAPSACK;
using API.Problems.NPHard.NPH_MAXIMUMVALUEKNAPSACK.Verifiers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class MaximumValueKnapsackOptimality_Tests {
    [Theory]
    [InlineData(MAXIMUMVALUEKNAPSACK.DefaultInstance, "({2,3},220)", true)]
    [InlineData(MAXIMUMVALUEKNAPSACK.DefaultInstance, "({3,2},220)", true)]
    [InlineData(MAXIMUMVALUEKNAPSACK.DefaultInstance, "({1},60)", false)]
    [InlineData(MAXIMUMVALUEKNAPSACK.DefaultInstance, "({},0)", false)]
    [InlineData(MAXIMUMVALUEKNAPSACK.DefaultInstance, "({1,2,3},280)", false)]
    [InlineData(MAXIMUMVALUEKNAPSACK.DefaultInstance, "({2,3},219)", false)]
    [InlineData(MAXIMUMVALUEKNAPSACK.DefaultInstance, "({9223372036854775807},0)", false)]
    [InlineData("((),0)", "({},0)", true)]
    [InlineData("(((1,3),(1,3),(2,6)),2)", "({1,2},6)", true)]
    [InlineData("(((1,3),(1,3),(2,6)),2)", "({3},6)", true)]
    [InlineData("(((0,0),(1,0)),1)", "({},0)", true)]
    [InlineData("(((0,0),(1,0)),1)", "({1,2},0)", true)]
    [InlineData("(((0,7),(0,0)),0)", "({1,2},7)", true)]
    [InlineData("(((0,7),(0,0)),0)", "({2},0)", false)]
    [InlineData("(((0,9223372036854775806),(0,1)),0)", "({1,2},9223372036854775807)", true)]
    public void Verifier_ConfirmsValuesAndAcceptsEveryFeasibleTiedOptimum(string input, string certificate, bool expected) {
        var verifier = new MaximumValueKnapsackOptimalityVerifier();
        Assert.Equal(expected, verifier.verify(new MAXIMUMVALUEKNAPSACK(input), certificate));
        Assert.Equal(expected, ((IVerifier)verifier).verify(input, certificate));
    }

    [Theory]
    [MemberData(nameof(MaximumValueKnapsackVerifier_Tests.MalformedCertificates), MemberType = typeof(MaximumValueKnapsackVerifier_Tests))]
    public void MalformedCertificates_KeepB2TypedErrors(string? certificate) {
        var problem = new MAXIMUMVALUEKNAPSACK();
        var verifier = new MaximumValueKnapsackOptimalityVerifier();
        var error = Assert.Throws<CertificateParseException>(() => verifier.verify(problem, certificate!));
        Assert.Same(problem, error.Problem);
        Assert.Equal(certificate, error.Received);
        Assert.Throws<CertificateParseException>(() => ((IVerifier)verifier).verify(problem.instance, certificate!));
    }

    [Theory]
    [InlineData("({9223372036854775808},0)")]
    [InlineData("({},9223372036854775808)")]
    [InlineData("({},-1)")]
    [InlineData("({1},60\0)")]
    public void InvalidNumbers_ThrowCertificateParseException(string certificate) {
        Assert.Throws<CertificateParseException>(() => new MaximumValueKnapsackOptimalityVerifier().verify(new MAXIMUMVALUEKNAPSACK(), certificate));
    }

    [Fact]
    public void CertificateValidation_PrecedesSearch() {
        var problem = new MAXIMUMVALUEKNAPSACK(MaximumValueKnapsackBruteForce_Tests.UnitItems(21));
        var verifier = new MaximumValueKnapsackOptimalityVerifier();
        Assert.False(verifier.verify(problem, "({0},0)"));
        Assert.False(verifier.verify(problem, "({},1)"));
        Assert.Throws<CertificateParseException>(() => verifier.verify(problem, "({1,1},2)"));
        Assert.Throws<ProblemParseException>(() => ((IVerifier)verifier).verify("garbage", "({},0)"));
    }

    [Theory]
    [InlineData("({23,24},8)", true)]
    [InlineData("({22,23},7)", false)]
    [InlineData("({22,23,24},11)", false)]
    [InlineData("({25},0)", false)]
    public void MoreThanTwentyItems_VerifiesExactOptimumBeyondGreedySeed(string certificate, bool expected) {
        string padding = string.Join(",", Enumerable.Repeat("(0,0)", 21));
        var problem = new MAXIMUMVALUEKNAPSACK($"(({padding},(2,3),(3,4),(3,4)),6)");
        var verifier = new MaximumValueKnapsackOptimalityVerifier();
        Assert.Equal(expected, verifier.verify(problem, certificate));
        Assert.Equal(expected, ((IVerifier)verifier).verify(problem.instance, certificate));
    }

    [Fact]
    public void MetadataAndDiscovery_KeepSolutionVerifierAsDefault() {
        var verifier = new MaximumValueKnapsackOptimalityVerifier();
        Assert.Equal(typeof(MaximumValueKnapsackOptimalityVerifier), ProblemProvider.Verifiers["maximumvalueknapsackoptimalityverifier"]);
        Assert.Equal(typeof(MAXIMUMVALUEKNAPSACK), ProblemProvider.ProblemTypeOf(verifier.GetType(), typeof(IVerifier<>)));
        IProblem problem = new MAXIMUMVALUEKNAPSACK();
        Assert.IsType<MaximumValueKnapsackSolutionVerifier>(problem.defaultVerifier);
        Assert.Equal("Maximum-Value 0-1 Knapsack Optimality Verifier", verifier.verifierName);
        Assert.Contains("exact branch-and-bound", verifier.verifierDefinition);
        Assert.Contains("no item-count limit", verifier.verifierDefinition);
        Assert.NotEmpty(verifier.source);
        Assert.NotEmpty(verifier.sourceLink);
        Assert.NotEmpty(verifier.contributors);
        Assert.EndsWith("MaximumValueKnapsackOptimalityVerifier.cs", verifier.sourceFile);
        Assert.True(verifier.verify(new MAXIMUMVALUEKNAPSACK(), verifier.certificate));
    }
}
