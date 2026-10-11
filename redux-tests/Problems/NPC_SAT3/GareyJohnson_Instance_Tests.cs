using API.Problems.NPComplete.NPC_DM3;
using API.Problems.NPComplete.NPC_SAT3;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_DM3;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// #807: the reduced 3-Dimensional Matching instance string must start with {X}{Y}{Z}, so that reading it back gives the
// instance the reduction built, not one whose X, Y and Z are the first three triples.
public class GareyJohnson_Instance_Tests {
    [Theory]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)")]
    [InlineData("(x1 | x2 | x3) & (!x1 | x2 | !x3)")]
    [InlineData("(x1) & (x2)")]
    public void Reduced_Instance_String_Parses_Back_To_The_Same_Instance(string sat3Instance) {
        DM3 reduced = new GareyJohnson(new SAT3(sat3Instance)).reductionTo;
        DM3 reread = new DM3(reduced.instance);
        Assert.Equal(reduced.X, reread.X);
        Assert.Equal(reduced.Y, reread.Y);
        Assert.Equal(reduced.Z, reread.Z);
        Assert.Equal(reduced.M, reread.M);
        Assert.StartsWith("{" + string.Join(",", reduced.X) + "}{" + string.Join(",", reduced.Y) + "}{" + string.Join(",", reduced.Z) + "}", reduced.instance);
    }
}
