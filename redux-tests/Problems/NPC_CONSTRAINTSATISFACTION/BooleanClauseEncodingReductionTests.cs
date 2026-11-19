using API.Interfaces;
using API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION;
using API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.ReduceTo.NPC_SAT3;
using API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Solvers;
using API.Problems.NPComplete.NPC_SAT3;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class BooleanClauseEncodingReductionTests {
    [Fact]
    public void Construction_RoundTripsAndIsDiscoveredInCorrectDirection() {
        IReduction reduction = new BooleanClauseEncodingReduction();
        Assert.IsType<SAT3>(reduction.reductionFrom);
        var target = Assert.IsType<CONSTRAINTSATISFACTION>(reduction.reduce());
        Assert.Equal(target.instance, new CONSTRAINTSATISFACTION(target.instance).instance);
        Assert.Equal(target.instance, reduction.reduce().instance);
        Assert.Equal(typeof(BooleanClauseEncodingReduction),
            ProblemProvider.Reductions["booleanclauseencodingreduction"]);
        Assert.All(target.D, domain => Assert.Equal(new decimal[] { 0, 1 }, domain));
    }

    [Theory]
    [InlineData("x", true)]
    [InlineData("!x", true)]
    [InlineData("x & !x", false)]
    [InlineData("(x | x | x) & (!x | !x | !x)", false)]
    [InlineData("(x | !x) & (!y | z) & y & !z", false)]
    [InlineData("(_a | !A | a) & !_a & A & a", true)]
    [InlineData("(x | !y | z) & (x | !y | z)", true)]
    public void Solvers_AgreeWithKnownSatisfiability(string input, bool expected) {
        var target = new BooleanClauseEncodingReduction(input).reductionTo;
        ISolver<CONSTRAINTSATISFACTION>[] solvers = [new ConstraintSatisfactionBruteForce(),
            new ConstraintSatisfactionBacktracking(), new ConstraintSatisfactionIntelligent()];
        foreach (var solver in solvers) {
            string answer = solver.solve(target);
            Assert.Equal(expected, answer != "{}");
            if (expected) Assert.True(target.defaultVerifier.verify(target, answer));
        }
    }

    [Fact]
    public void EveryOneAndTwoClauseFormulaOverTwoVariables_PreservesEveryAssignment() {
        // All 84 ordered clauses of length 1..3 over x,!x,y,!y, including
        // repetitions and tautologies. Check all singles and unordered clause pairs.
        string[] literals = ["x", "!x", "y", "!y"];
        var clauses = new List<string>();
        for (int length = 1; length <= 3; length++) {
            for (int code = 0; code < (1 << (2 * length)); code++) {
                int remaining = code;
                var terms = new List<string>();
                for (int i = 0; i < length; i++) { terms.Add(literals[remaining % 4]); remaining /= 4; }
                clauses.Add("(" + string.Join(" | ", terms) + ")");
            }
        }
        for (int i = 0; i < clauses.Count; i++) {
            CheckEveryAssignment(clauses[i]);
            for (int j = i; j < clauses.Count; j++) CheckEveryAssignment(clauses[i] + " & " + clauses[j]);
        }
    }

    [Fact]
    public void EveryNonemptySubsetOfTheEightThreeVariableSignClauses_PreservesEveryAssignment() {
        var clauses = Enumerable.Range(0, 8).Select(signs => "(" + string.Join(" | ",
            new[] { "x", "y", "z" }.Select((v, i) => (signs & (1 << i)) == 0 ? v : "!" + v)) + ")").ToArray();
        for (int subset = 1; subset < 256; subset++)
            CheckEveryAssignment(string.Join(" & ", clauses.Where((_, i) => (subset & (1 << i)) != 0)));
    }

    private static void CheckEveryAssignment(string input) {
        var reduction = new BooleanClauseEncodingReduction(input);
        var source = reduction.reductionFrom;
        var target = reduction.reductionTo;
        for (int mask = 0; mask < (1 << target.X.Count); mask++) {
            var values = target.X.Select((v, i) => (v, value: (mask & (1 << i)) != 0))
                .ToDictionary(p => p.v, p => p.value, StringComparer.Ordinal);
            // Independent Boolean oracle on the original literal strings. Do not
            // use generated inequalities or the reduction's certificate mapper.
            bool expected = source.clauses.All(c => c.Any(l =>
                l.StartsWith('!') ? !values[l[1..]] : values[l]));
            string numeric = "(" + string.Join(",", target.X.Select(v => $"{v}:{(values[v] ? 1 : 0)}")) + ")";
            Assert.Equal(expected, target.defaultVerifier.verify(target, numeric));
            if (expected) {
                string boolean = "(" + string.Join(",", target.X.Select(v => $"{v}:{values[v]}")) + ")";
                Assert.Equal(numeric, reduction.mapSolutions(boolean));
            }
        }
    }

    [Fact]
    public void Encoding_PreservesNamesAndNegationAndDeduplicatesRepeatedConstraints() {
        var target = new BooleanClauseEncodingReduction("(_a | !A | a) & (_a | !A | a)").reductionTo;
        Assert.Equal(new[] { "_a", "A", "a" }, target.X);
        Assert.Equal("_a+(1-A)+a>=1", Assert.Single(target.C));
    }

    [Fact]
    public void Reduce_UsesCurrentInstanceTextRatherThanStaleCachedLists() {
        var source = new SAT3("x");
        var reduction = new BooleanClauseEncodingReduction(source);
        source.instance = "!y";
        source.clauses.Clear();
        var target = reduction.reduce();
        Assert.Equal(new[] { "y" }, target.X);
        Assert.True(target.defaultVerifier.verify(target, "(y:0)"));
        Assert.Equal("(y:0)", reduction.mapSolutions("y=F"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("(x:True)")]
    [InlineData("(x:True,y:False,z:True)")]
    [InlineData("(x:True,x:False)")]
    [InlineData("(x:1,y:0)")]
    [InlineData("(x:False,y:True)")]
    [InlineData("(x:True,y:False) trailing")]
    public void ForwardMap_RejectsMalformedIncompleteOrUnsatisfyingCertificates(string? certificate) {
        var reduction = new BooleanClauseEncodingReduction("x & !y");
        Assert.Throws<CertificateParseException>(() => reduction.mapSolutions(certificate!));
    }

    [Fact]
    public void ForwardMap_AcceptsAlternativeSourceSyntaxAndOrdersByFirstAppearance() {
        var reduction = new BooleanClauseEncodingReduction("x & !y");
        Assert.Equal("(x:1,y:0)", reduction.mapSolutions(" ( y=F, x=T ) "));
    }

    [Theory]
    [InlineData("")]
    [InlineData("()")]
    [InlineData("(a | b | c | d)")]
    [InlineData("x + y")]
    public void InvalidSource_IsRejected(string input) {
        Assert.Throws<ProblemParseException>(() => new BooleanClauseEncodingReduction(input));
    }

    [Fact]
    public void LongIdentifiersAndManyClauses_HaveLinearOutputSize() {
        string name = "x" + new string('a', 4096);
        string input = string.Join(" & ", Enumerable.Repeat($"({name} | !{name} | {name})", 1000));
        var target = new BooleanClauseEncodingReduction(input).reductionTo;
        Assert.True(target.instance.Length <= 4 * input.Length + 64);
        Assert.True(target.defaultVerifier.verify(target, $"({name}:0)"));
    }
}
