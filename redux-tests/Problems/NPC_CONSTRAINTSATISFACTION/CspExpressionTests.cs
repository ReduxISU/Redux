using System.Globalization;
using API.Interfaces;
using API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION;
using API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Solvers;
using API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Verifiers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class CONSTRAINTSATISFACTION_Expression_Tests {
    private static ISolver<CONSTRAINTSATISFACTION>[] Solvers() => [
        new ConstraintSatisfactionBruteForce(), new ConstraintSatisfactionBacktracking(),
        new ConstraintSatisfactionIntelligent(), new ConstraintSatisfactionIntelligent { ForwardChecking = false }
    ];
    private static CONSTRAINTSATISFACTION Problem(string expression) =>
        new($"((x,y,z),({{2}},{{5}},{{3}}),{{\"{expression}\"}})");

    [Theory]
    [InlineData("x*y=10", true)]
    [InlineData("y/x>=2", true)]
    [InlineData("x+y*2<=15", true)]
    [InlineData("(x+y)*z!=20", true)]
    [InlineData("x/2=1", true)]
    [InlineData("x=2.0", true)]
    [InlineData("x+y*z=17", true)]
    [InlineData("(x+y)*z=21", true)]
    [InlineData("y-x-z=0", true)]
    [InlineData("12/x/z=2", true)]
    [InlineData("y/(x*z)=1", false)]
    [InlineData("-x*-y=10", true)]
    [InlineData("--x++y=7", true)]
    [InlineData("-(x+y)*z=-21", true)]
    [InlineData("x*-(y+z)=-16", true)]
    [InlineData(".1+.2=.3", true)]
    [InlineData("9007199254740993+2=9007199254740995", true)]
    [InlineData("x in {1,2,3}", true)]
    [InlineData("x IN {1,y-x,z}", false)]
    [InlineData("x not in {1,3}", true)]
    [InlineData("x NoT IN {2}", false)]
    [InlineData("x in {}", false)]
    [InlineData("x not in {}", true)]
    [InlineData("x+y in {x*y,7,z}", true)]
    [InlineData("x/10 in [.2,.5]", true)]
    [InlineData("x/10 in (.2,.5)", false)]
    [InlineData("x/10 in [.2,.5)", true)]
    [InlineData("x/10 in (.2,.5]", false)]
    [InlineData("x/10 not in [.2,.5]", false)]
    [InlineData("x/10 not in (.2,.5)", true)]
    [InlineData("x/10 not in [.2,.5)", false)]
    [InlineData("x/10 not in (.2,.5]", true)]
    [InlineData("x in [2,5]", true)]
    [InlineData("x in (2,5)", false)]
    [InlineData("x in [2,5)", true)]
    [InlineData("x in (2,5]", false)]
    [InlineData("y in [2,5]", true)]
    [InlineData("y in (2,5)", false)]
    [InlineData("y in [2,5)", false)]
    [InlineData("y in (2,5]", true)]
    [InlineData("x not in [2,5]", false)]
    [InlineData("x not in (2,5)", true)]
    [InlineData("x not in [2,5)", false)]
    [InlineData("x not in (2,5]", true)]
    [InlineData("x in [2,2]", true)]
    [InlineData("x in [2,2)", false)]
    [InlineData("x in (1.9,2.1)", true)]
    [InlineData("x in [y-z,y/z+1)", true)]
    public void Expressions_AgreeAcrossAllSolversAndVerifier(string expression, bool expected) {
        var problem = Problem(expression);
        Assert.Equal(expected, new ConstraintSatisfactionVerifier().verify(problem, "(x:2,y:5,z:3)"));
        foreach (var solver in Solvers()) {
            string answer = solver.solve(problem);
            Assert.Equal(expected, answer != "{}");
            if (expected) Assert.True(new ConstraintSatisfactionVerifier().verify(problem, answer));
        }
    }

    [Theory]
    [InlineData("x in [1,2,3]")]
    [InlineData("x in [1,2")]
    [InlineData("x in {1,}")]
    [InlineData("x not {1}")]
    [InlineData("x in [1,zUnknown]")]
    [InlineData("x in {missing}")]
    [InlineData("x**y=1")]
    [InlineData("x//y=1")]
    [InlineData("x=1..2")]
    [InlineData("x=2.")]
    [InlineData("x=NaN")]
    [InlineData("x=1e2")]
    [InlineData("x in {1} garbage")]
    [InlineData("x in [1,2] != 0")]
    [InlineData("x=0.12345678901234567890123456789")]
    public void InvalidExpressions_ThrowEvenAfterFalseConstraint(string expression) {
        var problem = new CONSTRAINTSATISFACTION($"((x,y),({{1}},{{2}}),{{\"1=2\",\"{expression}\"}})");
        foreach (var solver in Solvers()) Assert.Throws<ProblemParseException>(() => solver.solve(problem));
        Assert.Throws<ProblemParseException>(() => new ConstraintSatisfactionVerifier().verify(problem, "(x:1,y:2)"));
    }

    [Theory]
    [InlineData("x/(y-y)=1")]
    [InlineData("x in {2,1/0}")]
    [InlineData("x not in {1/0}")]
    [InlineData("x in [0,1/0]")]
    public void DivisionByZero_PropagatesInEveryAlgorithm(string expression) {
        foreach (var solver in Solvers()) Assert.Throws<DivideByZeroException>(() => solver.solve(Problem(expression)));
        Assert.Throws<DivideByZeroException>(() => new ConstraintSatisfactionVerifier().verify(Problem(expression), "(x:2,y:5,z:3)"));
    }

    [Fact]
    public void InvalidArithmetic_IsNotHiddenByEarlierFalsePredicate() {
        var problem = new CONSTRAINTSATISFACTION("((x),({1}),{\"1=2\",\"1/0=0\"})");
        Assert.Throws<DivideByZeroException>(() => new ConstraintSatisfactionVerifier().verify(problem, "(x:1)"));
        foreach (var solver in Solvers()) Assert.Throws<DivideByZeroException>(() => solver.solve(problem));
    }

    [Theory]
    [InlineData("x in [5,2]")]
    [InlineData("x not in (5,2)")]
    public void ReversedRanges_ThrowEvaluationError(string expression) {
        foreach (var solver in Solvers()) Assert.Throws<InvalidOperationException>(() => solver.solve(Problem(expression)));
        Assert.Throws<InvalidOperationException>(() => new ConstraintSatisfactionVerifier().verify(Problem(expression), "(x:2,y:5,z:3)"));
    }

    [Fact]
    public void Overflow_PropagatesWithoutReturningFalse() {
        var problem = new CONSTRAINTSATISFACTION("((x),({79228162514264337593543950335}),{\"x+1>x\"})");
        Assert.Throws<OverflowException>(() => new ConstraintSatisfactionVerifier().verify(problem, "(x:79228162514264337593543950335)"));
        foreach (var solver in Solvers()) Assert.Throws<OverflowException>(() => solver.solve(problem));
    }

    [Fact]
    public void PartialAssignments_DeferWholeConstraintUntilAllDependenciesExist() {
        var model = new ConstraintSatisfactionModel(Problem("x/y=z"));
        Assert.True(model.Consistent([2m, null, 3m]));
        Assert.True(model.Consistent([null, 0m, 3m]));
        Assert.Throws<DivideByZeroException>(() => model.Consistent([2m, 0m, 3m]));
        Assert.False(model.Consistent([2m, 5m, 3m]));
        var membership = new ConstraintSatisfactionModel(Problem("x in {2,y}"));
        Assert.True(membership.Consistent([1m, null, null]));
        Assert.False(membership.Consistent([1m, 5m, null]));
    }

    [Theory]
    [InlineData("(x:1,y:2,)")]
    [InlineData("(,x:1,y:2)")]
    [InlineData("(x:1,,y:2)")]
    [InlineData("(x:1 y:2)")]
    [InlineData("(x:,y:2)")]
    [InlineData("(x:NaN,y:2)")]
    [InlineData("(x:Infinity,y:2)")]
    [InlineData("(x:1:2,y:2)")]
    [InlineData("(1x:1,y:2)")]
    [InlineData("(x:1,y:2))")]
    [InlineData("((x:1,y:2)")]
    [InlineData("(x:1,y:2)garbage")]
    [InlineData("(x:1e0,y:2)")]
    [InlineData("(x:1.,y:2)")]
    [InlineData("(x:0.12345678901234567890123456789,y:2)")]
    [InlineData("{}")]
    public void CertificateSyntax_IsStrict(string certificate) {
        Assert.Throws<CertificateParseException>(() => new ConstraintSatisfactionVerifier().verify(new CONSTRAINTSATISFACTION(), certificate));
    }

    [Theory]
    [InlineData("((x,y),({.1,.2},{.2,.3}),{\"x+y=.5\",\"x*y=.06\"})")]
    [InlineData("((x),({2147483648,9007199254740993}),{\"x=9007199254740993\"})")]
    [InlineData("((x),({0.0000000000000000000000000001}),{\"x>0\"})")]
    [InlineData("((x,y,z),({1,2},{2,5},{3,4}),{\"x*y=10\",\"(x+y)*z!=20\",\"y/x>=2\"})")]
    [InlineData("((x,y,z),({1,2},{1,2},{1,2}),{\"x in {y}\",\"y not in {z}\"})")]
    public void DecimalAndNonlinearSolutions_RoundTrip(string instance) {
        var problem = new CONSTRAINTSATISFACTION(instance);
        foreach (var solver in Solvers()) Assert.True(new ConstraintSatisfactionVerifier().verify(problem, solver.solve(problem)));
    }

    [Theory]
    [InlineData("1,1.0")]
    [InlineData(".1,0.10")]
    [InlineData("0.12345678901234567890123456789")]
    [InlineData("79228162514264337593543950336")]
    [InlineData("1e2")]
    [InlineData("NaN")]
    public void InvalidDomains_AreRejected(string domain) =>
        Assert.Throws<ProblemParseException>(() => new CONSTRAINTSATISFACTION($"((x),({{{domain}}}),{{}})"));

    [Fact]
    public void DecimalCertificates_UseInvariantCultureAndNumericDomainEquality() {
        var previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var problem = new CONSTRAINTSATISFACTION("((x,y),({.1},{2}),{\"x+y=2.1\"})");
            Assert.True(new ConstraintSatisfactionVerifier().verify(problem, "( y:+2.00, x:0.10 )"));
            foreach (var solver in Solvers()) Assert.True(new ConstraintSatisfactionVerifier().verify(problem, solver.solve(problem)));
        } finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void LargeDomains_AreSupported() {
        string domain = string.Join(",", Enumerable.Range(0, 10000));
        var problem = new CONSTRAINTSATISFACTION($"((x),({{{domain}}}),{{\"x=9999\"}})");
        foreach (var solver in Solvers()) Assert.True(new ConstraintSatisfactionVerifier().verify(problem, solver.solve(problem)));
    }

    [Fact]
    public void DeepExpressions_UseNoRecursiveParserOrEvaluator() {
        string expression = new string('(', 10000) + "x" + new string(')', 10000) + "=2";
        foreach (var solver in Solvers()) Assert.True(new ConstraintSatisfactionVerifier().verify(Problem(expression), solver.solve(Problem(expression))));
    }

    [Fact]
    public void TimeChecks_InterruptParsingAndEvaluation() {
        int checks = 0;
        Assert.Throws<OperationCanceledException>(() => new ConstraintSatisfactionModel(Problem(string.Join("+", Enumerable.Repeat("x", 1000)) + "=2"),
            () => { if (++checks == 50) throw new OperationCanceledException(); }));
        var model = new ConstraintSatisfactionModel(Problem(string.Join("+", Enumerable.Repeat("x", 1000)) + "=2"));
        checks = 0;
        Assert.Throws<OperationCanceledException>(() => model.Consistent([2m, 5m, 3m],
            () => { if (++checks == 50) throw new OperationCanceledException(); }));
    }

    [Fact]
    public void NonlinearSearch_AgreesWithIndependentDecimalOracle() {
        foreach (int target in Enumerable.Range(-4, 9)) {
            var problem = new CONSTRAINTSATISFACTION($"((x,y),({{-1,0,.5,1}},{{-1,.5,1}}),{{\"x*y={target}/2\",\"x not in (y,y+1)\"}})");
            bool exists = false;
            foreach (decimal x in new decimal[] { -1, 0, .5m, 1 }) foreach (decimal y in new decimal[] { -1, .5m, 1 }) {
                bool expected = x * y == target / 2m && !(x > y && x < y + 1);
                string certificate = "(x:" + x.ToString(CultureInfo.InvariantCulture) + ",y:" + y.ToString(CultureInfo.InvariantCulture) + ")";
                Assert.Equal(expected, new ConstraintSatisfactionVerifier().verify(problem, certificate));
                exists |= expected;
            }
            foreach (var solver in Solvers()) {
                string answer = solver.solve(problem);
                Assert.Equal(exists, answer != "{}");
                if (exists) Assert.True(new ConstraintSatisfactionVerifier().verify(problem, answer));
            }
        }
    }
}

public class CONSTRAINTSATISFACTION_Architecture_Tests {
    [Fact]
    public void Mrv_AndInputOrderRemainDistinctWithoutForwardChecking() {
        var problem = new CONSTRAINTSATISFACTION("((x,y),({1,2,3},{1,2}),{\"x+y=3\"})");
        string ordered = new ConstraintSatisfactionBacktracking().solve(problem);
        string mrv = new ConstraintSatisfactionIntelligent { ForwardChecking = false }.solve(problem);
        Assert.Equal("(x:1, y:2)", ordered);
        Assert.Equal("(x:2, y:1)", mrv);
        Assert.True(new ConstraintSatisfactionVerifier().verify(problem, ordered));
        Assert.True(new ConstraintSatisfactionVerifier().verify(problem, mrv));
    }

    [Fact]
    public void DecimalRounding_IsSharedAndExplicit() {
        var problem = new CONSTRAINTSATISFACTION("((x),({1}),{\"x/3=0.3333333333333333333333333333\",\"0.0000000000000000000000000001/2=0\"})");
        Assert.True(new ConstraintSatisfactionVerifier().verify(problem, "(x:1)"));
        Assert.True(new ConstraintSatisfactionVerifier().verify(problem, new ConstraintSatisfactionIntelligent().solve(problem)));
    }

    [Fact]
    public void DeepSearch_UsesExplicitFrames() {
        string[] names = Enumerable.Range(0, 2000).Select(i => $"x{i}").ToArray();
        var problem = new CONSTRAINTSATISFACTION($"(({string.Join(",", names)}),({string.Join(",", names.Select(_ => "{1}"))}),{{}})");
        var verifier = new ConstraintSatisfactionVerifier();
        Assert.True(verifier.verify(problem, new ConstraintSatisfactionBacktracking().solve(problem)));
        Assert.True(verifier.verify(problem, new ConstraintSatisfactionIntelligent().solve(problem)));
    }

    [Fact]
    public void NumericLiteralSizeLimit_IsValidatedInAllInputs() {
        string oversized = new string('0', 1024) + "1";
        Assert.Throws<ProblemParseException>(() => new CONSTRAINTSATISFACTION($"((x),({{{oversized}}}),{{}})"));
        var problem = new CONSTRAINTSATISFACTION($"((x),({{1}}),{{\"x={oversized}\"}})");
        Assert.Throws<ProblemParseException>(() => new ConstraintSatisfactionIntelligent().solve(problem));
        Assert.Throws<CertificateParseException>(() => new ConstraintSatisfactionVerifier().verify(new CONSTRAINTSATISFACTION(), $"(x:{oversized},y:2)"));
    }

    [Fact]
    public void SuccessfulEmptyCertificate_IsDistinctFromExhaustion() {
        var satisfiable = new CONSTRAINTSATISFACTION("((),(),{})");
        var unsatisfiable = new CONSTRAINTSATISFACTION("((),(),{\"1=2\"})");
        Assert.Equal("()", new ConstraintSatisfactionIntelligent().solve(satisfiable));
        Assert.Equal("{}", new ConstraintSatisfactionIntelligent().solve(unsatisfiable));
        Assert.True(new ConstraintSatisfactionVerifier().verify(satisfiable, "()"));
        Assert.False(new ConstraintSatisfactionVerifier().verify(unsatisfiable, "()"));
        Assert.Throws<CertificateParseException>(() => new ConstraintSatisfactionVerifier().verify(satisfiable, "{}"));
    }
}
