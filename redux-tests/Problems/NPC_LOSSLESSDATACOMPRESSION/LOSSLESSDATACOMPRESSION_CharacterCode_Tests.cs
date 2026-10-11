using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// #808: a code table entry names its character by its UTF-16 code, 0 to 65,535. Codes outside that range used to wrap round to
// some character (65,633 to a), so a certificate naming a character that doesn't exist could pass.
public class LOSSLESSDATACOMPRESSION_CharacterCode_Tests {
    [Theory]
    [InlineData("aaa", "({(65633,0)},000)")]   // wraps to 97, a
    [InlineData("aaa", "({(-65439,0)},000)")]  // wraps to 97 too
    [InlineData("ab", "({(97,0),(65634,1)},01)")]
    public void Verifier_CharacterCodeOutOfRange_IsFalse(string text, string certificate) {
        var problem = new LOSSLESSDATACOMPRESSION(text);
        Assert.False(problem.defaultVerifier.verify(problem, certificate));
    }

    [Theory]
    [InlineData("aaa", "({(97,0)},000)")]
    [InlineData("￿\u0000", "({(0,0),(65535,1)},10)")]   // the ends of the range
    public void Verifier_CharacterCodeInRange_StillPasses(string text, string certificate) {
        var problem = new LOSSLESSDATACOMPRESSION(text);
        Assert.True(problem.defaultVerifier.verify(problem, certificate));
    }
}
