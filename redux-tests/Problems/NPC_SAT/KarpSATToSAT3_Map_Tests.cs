using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT.ReduceTo.NPC_SAT3;
using API.Problems.NPComplete.NPC_SAT.Verifiers;
using API.Problems.NPComplete.NPC_SAT3;
using API.Problems.NPComplete.NPC_SAT3.Verifiers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// #821: Karp's SAT -> 3SAT mapSolutions turns a SAT assignment into a 3SAT one. A clause longer than three is split into a
// chain with new variables, and the mapped answer must give each new variable the value that keeps the chain satisfied.
public class KarpSATToSAT3_Map_Tests {
    public static TheoryData<string, string> Satisfied() => new() {
        { "(a | b | c | d)", "(a:False,b:False,c:False,d:True)" },
        { "(a | b | c | d)", "(a:True,b:False,c:False,d:False)" },
        { "(a | b | c | d)", "(a:False,b:True,c:False,d:False)" },
        { "(x1 | x2 | x3 | x4 | x5 | x6 | x7)", "(x1:False,x2:False,x3:False,x4:False,x5:False,x6:False,x7:True)" },
        { "(x1 | x2 | x3 | x4 | x5 | x6 | x7)", "(x1:False,x2:False,x3:True,x4:False,x5:False,x6:False,x7:False)" },
        { "(x1 | !x2 | x3 | x4 | !x5) & (x2 | x3) & (!x1)", "(x1:False,x2:True,x3:False,x4:False,x5:False)" },
        { "(x1 | !x2 | x3 | x4 | !x5) & (x2 | x3) & (!x1)", "(x1:False,x2:False,x3:True,x4:False,x5:True)" },
        { "(x1)", "(x1:True)" },
    };

    [Theory]
    [MemberData(nameof(Satisfied))]
    public void ASatisfyingAssignment_MapsToOneThatSatisfiesThe3SatFormula(string formula, string assignment) {
        var sat = new SAT(formula);
        Assert.True(new SATVerifier().verify(sat, assignment));
        var red = new KarpSATToSAT3(sat);
        string mapped = red.mapSolutions(assignment);
        Assert.True(new SAT3Verifier().verify(new SAT3(red.reductionTo.instance), mapped), $"{mapped} does not satisfy {red.reductionTo.instance}");
    }

    [Fact]
    public void TheMappedAnswer_KeepsTheOriginalValues_AndSetsTheNewVariable() {
        var red = new KarpSATToSAT3(new SAT("(a | b | c | d)"));
        Assert.Equal("(a:False,b:False,c:False,d:True,x1:False)", red.mapSolutions("(a:False,b:False,c:False,d:True)"));
        Assert.Equal("(a:True,b:False,c:False,d:False,x1:True)", red.mapSolutions("(a:True,b:False,c:False,d:False)"));
    }
}
