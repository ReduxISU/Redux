using API.Problems.NPComplete.NPC_DM3;
using API.Problems.NPComplete.NPC_DM3.Solvers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// #745: a matching needs |X| triples, so with fewer triples in M there is none, and the brute force answers {} at once
// instead of building a combination of triples M doesn't have.
public class DM3_FewerTriples_Tests {
    [Theory]
    [InlineData("{a}{b}{c}")]                          // no triples at all: used to throw (500)
    [InlineData("{a,b}{c,d}{e,f}{a,c,e}")]             // one triple for two elements
    [InlineData("{a,b,g}{c,d,h}{e,f,i}{a,c,e}{b,d,f}")] // two triples for three elements
    public void BruteForce_FewerTriplesThanX_AnswersNoMatching(string instance) {
        Assert.Equal("{}", new ThreeDimensionalMatchingBruteForce().solve(new DM3(instance)));
    }

    [Fact]
    public void BruteForce_JustEnoughTriples_StillFindsTheMatching() {
        Assert.Equal("{{a,c,e},{b,d,f}}", new ThreeDimensionalMatchingBruteForce().solve(new DM3("{a,b}{c,d}{e,f}{a,c,e}{b,d,f}")));
    }
}
