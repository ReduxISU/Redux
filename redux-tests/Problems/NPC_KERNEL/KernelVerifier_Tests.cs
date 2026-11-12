// Contributors: Prashant Pant and Bektur Akkabakov. Tests for the LLM implementation.
using API.Interfaces;
using API.Problems.NPComplete.NPC_KERNEL;
using API.Problems.NPComplete.NPC_KERNEL.Verifiers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class KernelVerifier_Tests {
    [Theory]
    [InlineData("({},{})", "{}", true)]
    [InlineData("({7},{})", "{7}", true)]
    [InlineData("({7},{})", "{}", false)]
    [InlineData("({2,9},{(2,9)})", "{9}", true)]
    [InlineData("({2,9},{(2,9)})", "{2}", false)] // Wrong absorption direction.
    [InlineData("({2,9},{(2,9)})", "{2,9}", false)] // Independence violation.
    [InlineData("({2,9,42},{(2,9)})", "{9}", false)] // Isolated vertex omitted.
    [InlineData("({2,9,42},{(2,9)})", "{9,42}", true)]
    [InlineData("({0,1},{(0,0),(0,1)})", "{0}", false)]
    [InlineData("({0,1},{(0,0),(0,1)})", "{1}", true)]
    [InlineData("({0},{(0,0)})", "{}", false)]
    [InlineData("({0,1},{(0,1),(0,1)})", "{1}", true)]
    [InlineData("({0,1},{(0,1)})", "{999}", false)]
    [InlineData("({0,1},{(0,1)})", " \t{ 1 }\r\n", true)]
    [InlineData("({2147483647},{})", "{2147483647}", true)]
    public void Checks_kernel_conditions(string instance, string certificate, bool expected) {
        Assert.Equal(expected, new KernelVerifier().verify(new KERNEL(instance), certificate));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("0")]
    [InlineData("[0]")]
    [InlineData("{")]
    [InlineData("}")]
    [InlineData("{{0}}")]
    [InlineData("{0,}")]
    [InlineData("{,0}")]
    [InlineData("{0,,1}")]
    [InlineData("{0,0}")]
    [InlineData("{0,00}")]
    [InlineData("{-1}")]
    [InlineData("{+1}")]
    [InlineData("{1.0}")]
    [InlineData("{2147483648}")]
    [InlineData("{a}")]
    [InlineData("{1 0}")]
    [InlineData("{0}{1}")]
    [InlineData("{0};")]
    public void Malformed_certificate_throws_and_clears_previous_certificate(string? certificate) {
        var problem = new KERNEL("({0},{})");
        var verifier = new KernelVerifier();
        Assert.True(verifier.verify(problem, "{0}"));
        Assert.Equal("{0}", verifier.certificate);
        var error = Assert.Throws<CertificateParseException>(() => verifier.verify(problem, certificate!));
        Assert.Same(problem, error.Problem);
        Assert.Equal("", verifier.certificate);
    }

    [Fact]
    public void Matches_independent_oracle_for_every_subset_of_all_graphs_through_three_vertices() {
        var verifier = new KernelVerifier();
        foreach (var problem in KernelTestCases.SmallGraphs()) {
            foreach (var subset in KernelTestCases.Subsets(problem)) {
                string certificate = "{" + string.Join(",", subset) + "}";
                Assert.Equal(KernelTestCases.IsKernel(problem, subset), verifier.verify(problem, certificate));
            }
        }
    }

    [Fact]
    public void Verification_preserves_graph_and_tracks_latest_well_formed_candidate() {
        var problem = new KERNEL("({0,1},{(0,1)})");
        int[] verticesBefore = problem.vertices.ToArray();
        var edgesBefore = problem.edges.ToArray();
        var verifier = new KernelVerifier();
        Assert.True(verifier.verify(problem, "{1}"));
        Assert.False(verifier.verify(problem, "{0}"));
        Assert.Equal("{0}", verifier.certificate);
        Assert.Equal(verticesBefore, problem.vertices.ToArray());
        Assert.Equal(edgesBefore, problem.edges.ToArray());
    }
}
