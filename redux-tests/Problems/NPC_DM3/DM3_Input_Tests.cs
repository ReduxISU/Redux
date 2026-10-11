using API.Interfaces;
using API.Problems.NPComplete.NPC_DM3;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// #805: every group after {X}{Y}{Z} is a triple of M, so it must name exactly three things, one from X, one from Y and one
// from Z, in that order; and X, Y and Z each need at least one name, none of them blank.
public class DM3_Input_Tests {
    [Theory]
    [InlineData("{a,d}{b,e}{c,f}{a,b}{c,d,e,f}")]   // a pair and a 4-tuple, which the solver used to glue into two "triples"
    [InlineData("{a}{b}{c}{a,b}")]                 // a pair
    [InlineData("{a}{b}{c}{a,b,c,a}")]             // four names
    [InlineData("{a}{b}{c}{q,r,s}")]               // names in none of the sets
    [InlineData("{a}{b}{c}{b,a,c}")]               // X and Y swapped
    [InlineData("{a}{b}{c}{a,b,c}{a,c,b}")]        // Y and Z swapped in the second triple
    [InlineData("{a}{b}{c}{a,,c}")]                // a blank name
    [InlineData("{}{}{}")]                         // empty sets
    [InlineData("{a,}{b}{c}{a,b,c}")]              // a blank name in X
    [InlineData("{a}{b}")]                         // no Z
    public void DM3_Triple_Outside_XYZ_Or_Blank_Name_Is_A_Parse_Error(string instance) {
        Assert.Throws<ProblemParseException>(() => new DM3(instance));
    }

    [Theory]
    [InlineData("{Paul,Sally,Dave}{Madison,Austin,Bob}{Chloe,Frank,Jake}{Paul,Madison,Chloe}{Paul,Austin,Jake}{Sally,Bob,Chloe}{Sally,Madison,Frank}{Dave,Austin,Chloe}{Dave,Bob,Chloe}")]
    [InlineData("{a}{b}{c}")]                       // no triples: valid, and there is no matching
    [InlineData("{1,2}{1,2}{1,2}{1,1,1}{2,2,2}")]   // a name may be in X, Y and Z at once
    [InlineData("{a-b}{c>d}{e\\f}{a-b,c>d,e\\f}")]   // separators in names are just characters
    [InlineData("{a,b}{c}{d}{a,c,d}")]              // sets of different sizes still parse (there is no matching)
    [InlineData("{a}{b}{c}{a,b,c}{a,b,c}")]         // a triple listed twice
    public void DM3_Triples_From_XYZ_Still_Parse(string instance) {
        var problem = new DM3(instance);
        Assert.All(problem.M, t => {
            Assert.Equal(3, t.Count);
            Assert.Contains(t[0], problem.X);
            Assert.Contains(t[1], problem.Y);
            Assert.Contains(t[2], problem.Z);
        });
    }
}
