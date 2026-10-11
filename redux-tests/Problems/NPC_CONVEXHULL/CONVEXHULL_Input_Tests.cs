using API.Interfaces;
using API.Problems.NPComplete.NPC_CONVEXHULL;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// #806: every coordinate must be a finite number. double.Parse also reads NaN, Infinity and numbers too large for a double
// (as infinity), which gave hulls that aren't hulls and answers with an infinity sign in them.
public class CONVEXHULL_Input_Tests {
    [Theory]
    [InlineData("{(0,0),(1,0),(NaN,1),(0,1)}")]
    [InlineData("{(0,0),(1,0),(1,NaN),(0,1)}")]
    [InlineData("{(0,0),(1,0),(Infinity,1),(0,1)}")]
    [InlineData("{(0,0),(1,0),(-Infinity,1),(0,1)}")]
    [InlineData("{(0,0),(1,0),(1e400,1),(0,1)}")]     // too large for a double: read as infinity
    [InlineData("{(0,0),(1,0),(1,-1e309),(0,1)}")]
    public void CONVEXHULL_NonFinite_Coordinate_Is_A_Parse_Error(string instance) {
        Assert.Throws<ProblemParseException>(() => new CONVEXHULL(instance));
    }

    [Theory]
    [InlineData("{(0,0),(1,0),(1e300,1),(0,1)}")]
    [InlineData("{(-0.5,2.25),(1.5e-3,-4),(3,3)}")]
    [InlineData("{}")]
    public void CONVEXHULL_Finite_Coordinates_Still_Parse(string instance) {
        Assert.All(new CONVEXHULL(instance).points, p => Assert.True(double.IsFinite(p.x) && double.IsFinite(p.y)));
    }
}
