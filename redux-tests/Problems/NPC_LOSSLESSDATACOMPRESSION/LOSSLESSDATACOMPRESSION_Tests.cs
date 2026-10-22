#pragma warning disable CS1591
using Xunit;
using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION;
using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Verifiers;

namespace redux_tests;

public class LOSSLESSDATACOMPRESSION_Tests {
    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void LOSSLESSDATACOMPRESSION_Instance_Format_Described() {
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
    }

    [Fact]
    public void LOSSLESSDATACOMPRESSION_Certificate_Format_Described() {
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("bitstring", problem.certificateFormat);
    }

    [Fact]
    public void LOSSLESSDATACOMPRESSION_Certificate_Format_Example_Is_Actually_Valid() {
        // The example quoted in certificateFormat (LosslessDataCompressionVerifier.CertificateExample)
        // is illustrative on "abc", not on defaultInstance: defaultInstance is a full
        // sentence whose real Huffman certificate is a ~300-character code table +
        // bitstring, too unwieldy to serve as a readable format hint. "abc" keeps the
        // example short while still being a real, verifiable certificate.
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION("abc");
        LosslessDataCompressionVerifier verifier = new LosslessDataCompressionVerifier();
        Assert.True(verifier.verify(problem, LosslessDataCompressionVerifier.CertificateExample));
    }

    [Fact]
    public void LOSSLESS_Default_Instantiation() {
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION();
        string actual_result = problem.defaultSolver.solve(problem);
        Assert.True(problem.defaultVerifier.verify(problem, actual_result));
    }

    [Fact]
    public void LOSSLESS_Custom_String_Input_Test() {
        string input = "banana";
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION(input);

        string actual_result = problem.defaultSolver.solve(problem);

        Assert.True(problem.defaultVerifier.verify(problem, actual_result));
    }

    // verifier tests

    [Theory]
    [InlineData("aaaaaa", "({(97,0)},000000)", true)]
    [InlineData("aaaaaa", "({(97,1)},111111)", true)]
    [InlineData("abc", "({(97,0),(98,10),(99,11)},01011)", true)]
    [InlineData("abc", "({(97,0),(98,01),(99,1)},001)", false)] // not prefix-free
    [InlineData("abc", "({(97,0),(98,0),(99,1)},000)", false)]  // invalid encoding
    [InlineData("a", "({(97,0)},1)", false)]                    // wrong encoding
    public void LOSSLESS_Verifier(string instance, string certificate, bool expected) {
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION(instance);

        bool result = problem.defaultVerifier.verify(problem, certificate);

        Assert.Equal(expected, result);
    }

    // multiple valid encodings

    [Fact]
    public void LOSSLESS_Verifier_Allows_Different_Valid_Encodings() {
        string instance = "banana";
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION(instance);

        string cert1 = "({(97,0),(98,11),(110,10)},110100100)";
        string cert2 = "({(97,1),(98,00),(110,01)},001011011)";

        bool result1 = problem.defaultVerifier.verify(problem, cert1);
        bool result2 = problem.defaultVerifier.verify(problem, cert2);

        Assert.True(result1);
        Assert.True(result2);
    }

    // solver tests

    [Fact]
    public void LOSSLESS_Solver_Returns_Valid_Format() {
        string input = "Lossless data compression";
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION(input);
        string result = problem.defaultSolver.solve(problem);
        Assert.StartsWith("({", result);
        Assert.Contains("},", result);
        Assert.EndsWith(")", result);
        Assert.True(problem.defaultVerifier.verify(problem, result));
    }

    [Fact]
    public void LOSSLESS_Empty_Input_Test() {
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION("");

        string result = problem.defaultSolver.solve(problem);

        Assert.Equal("()", result);
        Assert.True(problem.defaultVerifier.verify(problem, result));
    }

    // optimality (#712): the verifier accepts only optimal-length prefix-free codes

    [Theory]
    // three equally frequent symbols: several optimal codes exist (any choice of which symbol gets the short code)
    [InlineData("aabbcc", "({(97,0),(98,10),(99,11)},0010101111)", true)]
    [InlineData("aabbcc", "({(99,0),(97,10),(98,11)},1010111100)", true)]
    [InlineData("aabbcc", "({(98,0),(99,10),(97,11)},1111001010)", true)]
    // prefix-free and decodes correctly, but 12 bits instead of the optimal 10
    [InlineData("aabbcc", "({(97,00),(98,01),(99,10)},000001011010)", false)]
    // banana: optimal is 9 bits; giving the frequent letter a long code costs more
    [InlineData("banana", "({(97,10),(98,0),(110,11)},011011011010)", false)]
    [InlineData("banana", "({(97,0),(98,10),(110,11)},100110110)", true)]  // a different optimal tie-break
    [InlineData("banana", "({(97,0),(98,10),(110,11)},0110110100)", false)] // decodes to a different string
    [InlineData("banana", "({(97,0),(98,11),(110,10)},110100100)", true)]
    // single symbol: one bit per symbol is optimal, two bits per symbol is not
    [InlineData("aaa", "({(97,0)},000)", true)]
    [InlineData("aaa", "({(97,00)},000000)", false)]
    // not prefix-free
    [InlineData("aabbcc", "({(97,0),(98,01),(99,1)},0001010111)", false)]
    // malformed certificates return false rather than throwing
    [InlineData("abc", "", false)]
    [InlineData("abc", "garbage", false)]
    [InlineData("abc", "({(97,x),(98,10),(99,11)},0x011)", false)]
    [InlineData("abc", "({(97,0),(98,10),(99,11)},01012)", false)]
    [InlineData("abc", "({},)", false)]
    [InlineData("abc", "()", false)]
    public void LOSSLESS_Verifier_Requires_Optimal_Length(string instance, string certificate, bool expected) {
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION(instance);
        Assert.Equal(expected, problem.defaultVerifier.verify(problem, certificate));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData("aaaabbbcc")]
    [InlineData("this is an example of lossless data compression using huffman encoding")]
    public void LOSSLESS_Verifier_Accepts_Solver_Output(string instance) {
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION(instance);
        string solution = problem.defaultSolver.solve(problem);
        Assert.True(problem.defaultVerifier.verify(problem, solution));
    }
}
