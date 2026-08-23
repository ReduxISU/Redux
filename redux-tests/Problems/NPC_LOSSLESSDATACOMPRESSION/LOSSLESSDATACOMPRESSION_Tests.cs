#pragma warning disable CS1591
using Xunit;
using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION;
using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Verifiers;

namespace redux_tests;

public class LOSSLESSDATACOMPRESSION_Tests
{
    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void LOSSLESSDATACOMPRESSION_Instance_Format_Described()
    {
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
    }

    [Fact]
    public void LOSSLESSDATACOMPRESSION_Certificate_Format_Described()
    {
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("encoded:", problem.certificateFormat);
    }

    [Fact]
    public void LOSSLESSDATACOMPRESSION_Certificate_Format_Example_Is_Actually_Valid()
    {
        // The example quoted in certificateFormat ("(97=0;98=10;99=11) encoded:01011")
        // is illustrative on "abc", not on defaultInstance: defaultInstance is a full
        // sentence whose real Huffman certificate is a ~300-character code table +
        // bitstring, too unwieldy to serve as a readable format hint. "abc" keeps the
        // example short while still being a real, verifiable certificate.
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION("abc");
        LosslessDataCompressionVerifier verifier = new LosslessDataCompressionVerifier();
        Assert.True(verifier.verify(problem, "(97=0;98=10;99=11) encoded:01011"));
    }

    [Fact]
    public void LOSSLESS_Default_Instantiation()
    {
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION();
        string actual_result = problem.defaultSolver.solve(problem);
        Assert.Contains("encoded:", actual_result);
    }

    [Fact]
    public void LOSSLESS_Custom_String_Input_Test()
    {
        string input = "banana";
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION(input);

        string actual_result = problem.defaultSolver.solve(problem);

        Assert.Contains("encoded:", actual_result);
    }

    // verifier tests

    [Theory]
    [InlineData("aaaaaa", "(97=0) encoded:000000", true)]
    [InlineData("aaaaaa", "(97=1) encoded:111111", true)]
    [InlineData("abc", "(97=0;98=10;99=11) encoded:01011", true)]
    [InlineData("abc", "(97=0;98=01;99=1) encoded:001", false)] // not prefix-free
    [InlineData("abc", "(97=0;98=0;99=1) encoded:000", false)]  // invalid encoding
    [InlineData("a", "(97=0) encoded:1", false)]                // wrong encoding
    public void LOSSLESS_Verifier(string instance, string certificate, bool expected)
    {
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION(instance);

        bool result = problem.defaultVerifier.verify(problem, certificate);

        Assert.Equal(expected, result);
    }

    // multiple valid encodings

    [Fact]
    public void LOSSLESS_Verifier_Allows_Different_Valid_Encodings()
    {
        string instance = "banana";
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION(instance);

        string cert1 = "(97=0;98=11;110=10) encoded:110100100";
        string cert2 = "(97=1;98=00;110=01) encoded:001011011";

        bool result1 = problem.defaultVerifier.verify(problem, cert1);
        bool result2 = problem.defaultVerifier.verify(problem, cert2);

        Assert.True(result1);
        Assert.True(result2);
    }

    // solver tests

    [Fact]
    public void LOSSLESS_Solver_Returns_Valid_Format()
    {
        string input = "Lossless data compression";
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION(input);
        string result = problem.defaultSolver.solve(problem);
        Assert.Contains("encoded:", result);
        Assert.Contains("(", result);
        Assert.Contains(")", result);
    }

    [Fact]
    public void LOSSLESS_Empty_Input_Test()
    {
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION("");

        string result = problem.defaultSolver.solve(problem);

        Assert.Equal("( ) encoded:", result);
    }
}