using API.Interfaces;
using API.Interfaces.Logic;
using Xunit;
using API.Problems.NPComplete.NPC_CIRCUITSAT;
using API.Problems.NPComplete.NPC_CIRCUITSAT.Solvers;
using API.Problems.NPComplete.NPC_CIRCUITSAT.Verifiers;
using API.Problems.NPComplete.NPC_CIRCUITSAT.ReduceTo.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT.Solvers;
using API.Problems.NPComplete.NPC_SAT.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class CIRCUITSAT_Tests {

    // -------------------------------------------------------------------------
    // Instantiation
    // -------------------------------------------------------------------------

    [Fact]
    public void CIRCUITSAT_Default_Instantiation() {
        CIRCUITSAT circuitSat = new CIRCUITSAT();
        Assert.Equal("({x1,x2,x3},((g1,and,x1,x2),(g2,not,x3),(g3,or,g1,g2)),g3)", circuitSat.defaultInstance);
        Assert.Equal(circuitSat.defaultInstance, circuitSat.instance);
        Assert.Equal(new[] { "x1", "x2", "x3" }, circuitSat.inputs);
        Assert.Equal(new[] { "(g1,and,x1,x2)", "(g2,not,x3)", "(g3,or,g1,g2)" }, circuitSat.gates);
        Assert.Equal(new[] { "x1", "x2", "x3", "g1", "g2", "g3" }, circuitSat.wires);
        Assert.Equal("g3", circuitSat.output);
    }

    [Fact]
    public void CIRCUITSAT_Custom_Instance() {
        string instance = "({a,b},((c,or,a,b),(d,not,c)),d)";
        CIRCUITSAT circuitSat = new CIRCUITSAT(instance);
        Assert.Equal(instance, circuitSat.instance);
        Assert.Equal(new[] { "a", "b" }, circuitSat.inputs);
        Assert.Equal("d", circuitSat.output);
    }

    // The instance is the tuple (Inputs, Gates, Output) of the formal definition, so SPADE
    // reads its shape: a set of wires, a list of gates and one wire name.
    [Fact]
    public void CIRCUITSAT_Default_Instance_Matches_The_SPADE_Grammar() {
        var parser = new SPADE.StringParser(CIRCUITSAT.InstanceGrammar);
        parser.parse(CIRCUITSAT._defaultInstance);
        Assert.Equal("{x1,x2,x3}", parser["I"].ToString());
        Assert.Equal(3, parser["G"].Count());
        Assert.Equal("g3", parser["o"].ToString());
    }

    // The output wire may be a bare input: a gate-free circuit is still a circuit, and a
    // single-literal clause embeds as one.
    [Fact]
    public void CIRCUITSAT_Accepts_Gate_Free_Circuit_Outputting_An_Input() {
        CIRCUITSAT circuitSat = new CIRCUITSAT("({x1,x2},(),x2)");
        Assert.Empty(circuitSat.gates);
        Assert.True(new CircuitSatVerifier().verify(circuitSat, "(x2:True)"));
        Assert.False(new CircuitSatVerifier().verify(circuitSat, "(x1:True)"));
    }

    // The input wires are a set: a repeated name denotes the same wire once, exactly as in
    // every other SPADE-set instance in the repo.
    [Fact]
    public void CIRCUITSAT_Input_Set_Collapses_Duplicate_Wires() {
        CIRCUITSAT circuitSat = new CIRCUITSAT("({x1,x1,x2},((g1,and,x1,x2)),g1)");
        Assert.Equal(new[] { "x1", "x2" }, circuitSat.inputs);
    }

    [Fact]
    public void CIRCUITSAT_Instance_Format_Described() {
        CIRCUITSAT circuitSat = new CIRCUITSAT();
        Assert.NotNull(circuitSat.instanceFormat);
        Assert.Contains("gate", circuitSat.instanceFormat);
        Assert.Contains(circuitSat.defaultInstance, circuitSat.instanceFormat);
    }

    [Fact]
    public void CIRCUITSAT_Certificate_Format_Described() {
        CIRCUITSAT circuitSat = new CIRCUITSAT();
        Assert.NotNull(circuitSat.certificateFormat);
        Assert.Contains("True", circuitSat.certificateFormat);
        Assert.Contains("False", circuitSat.certificateFormat);
    }

    // Whitespace is free between tokens (it is stripped before SPADE sees the text).
    [Fact]
    public void CIRCUITSAT_Parses_With_Whitespace_Variation() {
        CIRCUITSAT circuitSat = new CIRCUITSAT("( {x1,\tx2}\n((g1 , and , x1,x2) ), g1 )");
        Assert.Equal(new[] { "(g1,and,x1,x2)" }, circuitSat.gates);
    }

    // -------------------------------------------------------------------------
    // Parsing — malformed instances
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("", "instance is empty")]
    [InlineData("x1", "an instance is a tuple of three components")]
    [InlineData("((x1,x2),(),x1)", "the input wires are a set in braces")]
    [InlineData("({x1},(g1,not,x1),g1)", "each gate is a tuple")]
    [InlineData("({x1},({g1,not,x1}),g1)", "each gate is a tuple")]
    [InlineData("({},(),x1)", "circuit has no input wires")]
    [InlineData("({x1},(),g9)", "output wire 'g9' is not declared")]
    [InlineData("({x1,x2},((g1,and,x1,x3)),g1)", "not a declared input or earlier gate output")]
    [InlineData("({x1,x2},((g1,and,x1,x2),(g1,or,x1,x2)),g1)", "is defined twice")]
    [InlineData("({x1},((g1,not,g1)),g1)", "not a declared input or earlier gate output")]
    [InlineData("({x1},((g2,not,g1),(g1,and,x1,x1)),g2)", "not a declared input or earlier gate output")]
    [InlineData("({x1,x2},((g1,and)),g1)", "is missing a part")]
    [InlineData("({x1,x2},((g1,xor,x1,x2)),g1)", "has operator 'xor'; expected 'and', 'or' or 'not'")]
    [InlineData("({x1,x2},((g1,not,x1,x2)),g1)", "gate 'g1' uses 'not', which takes 1 input wire")]
    [InlineData("({x1,x2},((g1,and,x1)),g1)", "gate 'g1' uses 'and', which takes 2 input wires")]
    [InlineData("({x1,x2},((g1,or,x1,x2,x1)),g1)", "gate 'g1' uses 'or', which takes 2 input wires")]
    [InlineData("({x1,x2},((g1,and,x1,(x2,x1))),g1)", "must start with a letter or '_'")]
    [InlineData("({1x},(),1x)", "must start with a letter or '_'")]
    [InlineData("({x-1},(),x-1)", "'-' is not allowed")]
    [InlineData("({x1},((g1,not,x1)),g1", "')' is missing")]
    [InlineData("({x1},((g1,not,x1)),g1) extra", "unexpected text after the instance tuple")]
    public void CIRCUITSAT_Constructor_Throws_On_Invalid_Instance(string instance, string messageFragment) {
        var ex = Assert.Throws<ProblemParseException>(() => new CIRCUITSAT(instance));
        Assert.Equal("CircuitSAT", ex.ProblemName);
        Assert.Contains(messageFragment, ex.Message);
    }

    // Operators are matched case-insensitively; the canonical text is lower case.
    [Fact]
    public void CIRCUITSAT_Accepts_Uppercase_Operators() {
        CIRCUITSAT circuitSat = new CIRCUITSAT("({x1},((g1,NOT,x1)),g1)");
        Assert.Equal(new[] { "(g1,not,x1)" }, circuitSat.gates);
    }

    // -------------------------------------------------------------------------
    // Verifier — valid certificates
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("({x1,x2,x3},((g1,and,x1,x2),(g2,not,x3),(g3,or,g1,g2)),g3)", "(x1:True,x2:True,x3:False)")]
    [InlineData("({x1,x2,x3},((g1,and,x1,x2),(g2,not,x3),(g3,or,g1,g2)),g3)", "(x1:False,x2:False,x3:False)")] // g2=not(False)=True
    [InlineData("({a,b},((c,or,a,b),(d,not,c)),d)", "(a:False,b:False)")]                                        // d = !(F|F) = True
    public void CIRCUITSAT_Verifier_Accepts_Valid_Certificate(string instance, string certificate) {
        CIRCUITSAT circuitSat = new CIRCUITSAT(instance);
        CircuitSatVerifier verifier = new CircuitSatVerifier();
        Assert.True(verifier.verify(circuitSat, certificate));
    }

    [Theory]
    // Accepted spellings, identical to the SAT family's: '=' separator and T/F values.
    [InlineData("({x1},((g1,not,x1)),g1)", "(x1=F)")]
    [InlineData("({x1},((g1,not,x1)),g1)", "(x1=False)")]
    // Inputs the certificate omits are read as False (SAT-family convention):
    [InlineData("({x1,x2,x3},((g1,and,x1,x2),(g2,not,x3),(g3,or,g1,g2)),g3)", "(x1:True,x2:True)")]
    // Names that are not wires of the circuit are ignored, as in SAT:
    [InlineData("({x1},((g1,not,x1)),g1)", "(x1:False,z9:True)")]
    public void CIRCUITSAT_Verifier_Accepts_SAT_Family_Certificate_Spellings(string instance, string certificate) {
        CIRCUITSAT circuitSat = new CIRCUITSAT(instance);
        Assert.True(new CircuitSatVerifier().verify(circuitSat, certificate));
    }

    // -------------------------------------------------------------------------
    // Verifier — invalid certificates
    // -------------------------------------------------------------------------

    [Theory]
    // x3=True kills g2=not x3, and x1=False kills g1=x1&x2, so g3=False.
    [InlineData("({x1,x2,x3},((g1,and,x1,x2),(g2,not,x3),(g3,or,g1,g2)),g3)", "(x1:False,x2:True,x3:True)")]
    // The output wire is the input x2, left unassigned (so False).
    [InlineData("({x1,x2},(),x2)", "(x1:True)")]
    public void CIRCUITSAT_Verifier_Rejects_Invalid_Certificate(string instance, string certificate) {
        CIRCUITSAT circuitSat = new CIRCUITSAT(instance);
        CircuitSatVerifier verifier = new CircuitSatVerifier();
        Assert.False(verifier.verify(circuitSat, certificate));
    }

    // An AND of a wire and its own negation is unsatisfiable — every well-formed
    // certificate is rejected, none of them throws.
    [Theory]
    [InlineData("(x1:True)")]
    [InlineData("(x1:False)")]
    public void CIRCUITSAT_Verifier_Rejects_Every_Certificate_For_UNSAT_Circuit(string certificate) {
        CIRCUITSAT circuitSat = new CIRCUITSAT("({x1},((g1,not,x1),(g2,and,x1,g1)),g2)");
        Assert.False(new CircuitSatVerifier().verify(circuitSat, certificate));
    }

    // -------------------------------------------------------------------------
    // Verifier — malformed certificates
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("")]          // empty
    [InlineData("()")]        // empty parentheses
    [InlineData("(x1)")]      // no ':' or '=' separator
    [InlineData("(x1:maybe)")] // value is not True/False/T/F
    [InlineData("(x1:True,x2)")] // second assignment malformed
    public void CIRCUITSAT_Verifier_Throws_On_Malformed_Certificate(string certificate) {
        CIRCUITSAT circuitSat = new CIRCUITSAT("({x1,x2},((g1,and,x1,x2)),g1)");
        CircuitSatVerifier verifier = new CircuitSatVerifier();
        Assert.Throws<CertificateParseException>(() => verifier.verify(circuitSat, certificate));
    }

    // -------------------------------------------------------------------------
    // Solver
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("({x1,x2,x3},((g1,and,x1,x2),(g2,not,x3),(g3,or,g1,g2)),g3)")]
    [InlineData("({a,b},((c,or,a,b),(d,not,c)),d)")]
    [InlineData("({x1,x2},((g1,and,x1,x2)),x1)")] // output is an input; satisfiable with x1=T
    public void CIRCUITSAT_Solver_Finds_Satisfying_Assignment(string instance) {
        CIRCUITSAT circuitSat = new CIRCUITSAT(instance);
        CircuitSatBruteForceSolver solver = new CircuitSatBruteForceSolver();
        CircuitSatVerifier verifier = new CircuitSatVerifier();
        string certificate = solver.solve(circuitSat);
        Assert.True(verifier.verify(circuitSat, certificate), certificate);
    }

    [Fact]
    public void CIRCUITSAT_Solver_Certificate_Contains_All_Input_Wires() {
        CIRCUITSAT circuitSat = new CIRCUITSAT("({x1,x2,x3},((g1,and,x1,x2),(g2,not,x3),(g3,or,g1,g2)),g3)");
        string certificate = new CircuitSatBruteForceSolver().solve(circuitSat);
        Assert.Contains("x1:", certificate);
        Assert.Contains("x2:", certificate);
        Assert.Contains("x3:", certificate);
    }

    // Counting starts at all-False with the last input as the low bit, so the single
    // NOT circuit must come back with x1=False — pins the enumeration order.
    [Fact]
    public void CIRCUITSAT_Solver_Returns_First_Assignment_In_Counting_Order() {
        CIRCUITSAT circuitSat = new CIRCUITSAT("({x1},((g1,not,x1)),g1)");
        Assert.Equal("(x1:False)", new CircuitSatBruteForceSolver().solve(circuitSat));
    }

    [Fact]
    public void CIRCUITSAT_Solver_Returns_NoSolution_Exist_For_UNSAT_Circuit() {
        CIRCUITSAT circuitSat = new CIRCUITSAT("({x1},((g1,not,x1),(g2,and,x1,g1)),g2)");
        Assert.Equal("No solution exists", new CircuitSatBruteForceSolver().solve(circuitSat));
    }

    [Fact]
    public void CIRCUITSAT_Solver_Expired_Timer_Returns_TimedOutAnswer_Immediately() {
        CIRCUITSAT circuitSat = new CIRCUITSAT();
        CircuitSatBruteForceSolver solver = new CircuitSatBruteForceSolver { timerHasExpired = true };
        Assert.Equal(SolveRun.TimedOutAnswer, solver.solve(circuitSat));
    }

    // -------------------------------------------------------------------------
    // Shared circuit model
    // -------------------------------------------------------------------------

    [Fact]
    public void BooleanCircuit_Canonical_Text_Round_Trips_Through_The_Parser() {
        string instance = "( {x1 ,x2 }, (( g1 , and , x1 , x2 ) ,(g2,not, x1)) , g2 )";
        BooleanCircuit parsed = CircuitParser.Parse(instance);
        BooleanCircuit reparsed = CircuitParser.Parse(parsed.ToString());
        Assert.Equal("({x1,x2},((g1,and,x1,x2),(g2,not,x1)),g2)", parsed.ToString());
        Assert.Equal(parsed.ToString(), reparsed.ToString());
    }

    // A gate-free circuit still has canonical text: the empty gate list is written ().
    [Fact]
    public void BooleanCircuit_Canonical_Text_Writes_Empty_Gate_List() {
        Assert.Equal("({x1,x2},(),x1)", CircuitParser.Parse("({x1,x2},(),x1)").ToString());
    }

    [Fact]
    public void BooleanCircuit_WireValues_Exposes_Every_Wire() {
        BooleanCircuit parsed = CircuitParser.Parse("({x1,x2,x3},((g1,and,x1,x2),(g2,not,x3),(g3,or,g1,g2)),g3)");
        Dictionary<string, bool> inputs = new() { ["x1"] = true, ["x2"] = true, ["x3"] = true };
        Dictionary<string, bool> wires = parsed.WireValues(inputs);
        Assert.True(wires["g1"]);
        Assert.False(wires["g2"]);
        Assert.True(wires["g3"]); // g1=True alone satisfies the OR
    }

    // -------------------------------------------------------------------------
    // CircuitSAT -> SAT reduction (CLRS Theorem 34.9 / Sipser Theorem 7.53)
    // -------------------------------------------------------------------------

    [Fact]
    public void CIRCUITSAT_To_SAT_Reduction_Structure() {
        CIRCUITSAT circuitSat = new CIRCUITSAT();
        CircuitSatToSatReduction reduction = new CircuitSatToSatReduction(circuitSat);
        SAT sat = reduction.reductionTo;

        // Three clauses per AND/OR gate, two per NOT gate, one unit clause for the output.
        Assert.Equal(new[] {
            new[] { "!x1", "!x2", "g1" },
            new[] { "x1", "!g1" },
            new[] { "x2", "!g1" },
            new[] { "x3", "g2" },
            new[] { "!x3", "!g2" },
            new[] { "g1", "g2", "!g3" },
            new[] { "!g1", "g3" },
            new[] { "!g2", "g3" },
            new[] { "g3" },
        }, sat.clauses.Select(c => c.ToArray()).ToArray());
    }

    [Theory]
    [InlineData("(x1:True,x2:True,x3:False)")]
    [InlineData("(x1:False,x2:False,x3:False)")]
    public void CIRCUITSAT_To_SAT_Maps_Valid_Certificate_To_Valid_SAT_Certificate(string circuitCertificate) {
        CIRCUITSAT circuitSat = new CIRCUITSAT();
        Assert.True(new CircuitSatVerifier().verify(circuitSat, circuitCertificate));

        CircuitSatToSatReduction reduction = new CircuitSatToSatReduction(circuitSat);
        string satCertificate = reduction.mapSolutions(circuitCertificate);

        // Every wire — inputs and gates — gets an assignment, so the SAT certificate
        // mentions all six.
        Assert.Equal(6, TruthAssignment.Parse(satCertificate).Count);
        Assert.True(new SATVerifier().verify(reduction.reductionTo, satCertificate));
    }

    [Fact]
    public void CIRCUITSAT_To_SAT_Solver_Certificate_Maps_To_Verifiable_SAT_Certificate() {
        CIRCUITSAT circuitSat = new CIRCUITSAT();
        string circuitCertificate = new CircuitSatBruteForceSolver().solve(circuitSat);
        CircuitSatToSatReduction reduction = new CircuitSatToSatReduction(circuitSat);
        Assert.True(new SATVerifier().verify(reduction.reductionTo, reduction.mapSolutions(circuitCertificate)));
    }

    [Fact]
    public void CIRCUITSAT_To_SAT_Preserves_Unsatisfiability() {
        // x1 & !x1 is constant-False; the reduced formula must be unsatisfiable too.
        CIRCUITSAT circuitSat = new CIRCUITSAT("({x1},((g1,not,x1),(g2,and,x1,g1)),g2)");
        CircuitSatToSatReduction reduction = new CircuitSatToSatReduction(circuitSat);
        Assert.Equal("No solution exists", new SATBruteForceSolver().solve(reduction.reductionTo.instance));
    }

    [Theory]
    [InlineData("")]
    [InlineData("(x1)")]           // missing separator
    [InlineData("(x1:perhaps)")]   // bad boolean
    public void CIRCUITSAT_To_SAT_MapSolutions_Throws_On_Malformed_Certificate(string certificate) {
        CircuitSatToSatReduction reduction = new CircuitSatToSatReduction(new CIRCUITSAT());
        Assert.Throws<ReductionInputException>(() => reduction.mapSolutions(certificate));
    }

    [Fact]
    public void CIRCUITSAT_To_SAT_Reduction_Is_Called_Twice_Safely() {
        CircuitSatToSatReduction reduction = new CircuitSatToSatReduction();
        string first = reduction.reduce().instance;
        string second = reduction.reduce().instance;
        Assert.Equal(first, second);
    }
}
