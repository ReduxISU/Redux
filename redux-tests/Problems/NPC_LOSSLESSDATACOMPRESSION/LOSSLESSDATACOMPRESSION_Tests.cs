using Xunit;
using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION;
using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Verifiers;
using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Solvers;
using API.Interfaces;

namespace redux_tests;

public class LOSSLESSDATACOMPRESSION_Tests
{
    [Fact]
    public void LOSSLESS_Default_Instantiation()
    {
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION();
        string actual_result = problem.defaultSolver.solve(problem);
        Assert.Equal(problem.solution, actual_result);
    }

    [Fact]
    public void LOSSLESS_Custom_String_Input_Test()
    {
        string input = "banana";
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION(input);
        string actual_result = problem.defaultSolver.solve(problem);
        Assert.Equal(problem.solution, actual_result);
    }

    // verifier tests

    [Theory]
    [InlineData("aaaaaa", "000000", true)]
    [InlineData("Hello World!", "11010001010011110010011011111100", false)]
    [InlineData("abcdef", "1001011101110001", true)]
    [InlineData("Lossless data compression", "101101", false)]
    [InlineData("algorithm", "1", false)]
    public void LOSSLESS_Verifier(string instance, string certificate, bool expected)
    {
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION(instance);

        bool result = problem.defaultVerifier.verify(problem, certificate);

        Assert.Equal(expected, result);
    }

    // solver tests

    [Theory]
    [InlineData("Lossless data compression", "codes:{76=11000;97=1101;99=11001;100=11100;101=1111;105=11101;108=0000;109=0001;110=0010;111=011;112=0011;114=0100;115=10;116=0101}|encoded:110000111010000011111010111001101010111011100101100010011010011111010111010110010")]
    [InlineData("aa", "codes:{97=0}|encoded:00")]
    public void LOSSLESS_Solver_BasicCases(string instance, string expectedPrefix)
    {
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION(instance);

        string result = problem.defaultSolver.solve(problem);

        Assert.StartsWith("codes:{", result);
        Assert.Contains("encoded:", result);
    }

    [Fact]
    public void LOSSLESS_Empty_Input_Test()
    {
        LOSSLESSDATACOMPRESSION problem = new LOSSLESSDATACOMPRESSION("");

        string result = problem.defaultSolver.solve(problem);

        Assert.Equal("codes:{}|encoded:", result);
    }
}