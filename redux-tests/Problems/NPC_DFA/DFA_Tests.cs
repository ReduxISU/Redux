using Xunit;
using API.Problems.P.P_DFA;
using API.Problems.P.P_DFA.Solvers;
using API.Problems.P.P_DFA.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class DFA_Tests
{
    private const string DefaultInstance =
        "(({1,2,3},{a,b},{(1,a,2),(1,b,3),(2,a,2),(2,b,2),(3,a,2),(3,b,3)},1,{2}),a)";

    // -------------------------------------------------------------------------
    // Instantiation
    // -------------------------------------------------------------------------

    [Fact]
    public void DFA_Default_Instantiation()
    {
        DFA dfa = new DFA();
        Assert.Equal(DefaultInstance, dfa.defaultInstance);
        Assert.Equal(DefaultInstance, dfa.instance);
    }

    [Fact]
    public void DFA_Custom_Instantiation()
    {
        // Even-number-of-b's machine over {a,b}
        string instance = "(({even,odd},{a,b},{(even,a,even),(even,b,odd),(odd,a,odd),(odd,b,even)},even,{even}),bb)";
        DFA dfa = new DFA(instance);
        Assert.Equal(instance, dfa.instance);
        Assert.Equal(2, dfa.nodes.Count);
        Assert.Equal("even", dfa.startState);
        Assert.Contains("even", dfa.acceptStates);
    }

    // -------------------------------------------------------------------------
    // Parsing
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(({1,2,3},{a,b},{(1,a,2),(1,b,3),(2,a,2),(2,b,2),(3,a,2),(3,b,3)},1,{2}),a)", 3)]
    [InlineData("(({even,odd},{a,b},{(even,a,even),(even,b,odd),(odd,a,odd),(odd,b,even)},even,{even}),bb)", 2)]
    public void DFA_Parses_Correct_Node_Count(string instance, int expectedCount)
    {
        DFA dfa = new DFA(instance);
        Assert.Equal(expectedCount, dfa.nodes.Count);
    }

    [Theory]
    [InlineData("(({1,2,3},{a,b},{(1,a,2),(1,b,3),(2,a,2),(2,b,2),(3,a,2),(3,b,3)},1,{2}),a)", 6)]
    [InlineData("(({even,odd},{a,b},{(even,a,even),(even,b,odd),(odd,a,odd),(odd,b,even)},even,{even}),bb)", 4)]
    public void DFA_Parses_Correct_Edge_Count(string instance, int expectedCount)
    {
        DFA dfa = new DFA(instance);
        Assert.Equal(expectedCount, dfa.edges.Count);
    }

    [Fact]
    public void DFA_Parses_Start_State()
    {
        DFA dfa = new DFA(DefaultInstance);
        Assert.Equal("1", dfa.startState);
    }

    [Fact]
    public void DFA_Parses_Accept_States()
    {
        DFA dfa = new DFA(DefaultInstance);
        Assert.Single(dfa.acceptStates);
        Assert.Contains("2", dfa.acceptStates);
    }

    [Fact]
    public void DFA_Parses_Alphabet()
    {
        DFA dfa = new DFA(DefaultInstance);
        Assert.Equal(2, dfa.alphabet.Count);
        Assert.Contains('a', dfa.alphabet);
        Assert.Contains('b', dfa.alphabet);
    }

    [Fact]
    public void DFA_Parses_Input_String()
    {
        DFA dfa = new DFA(DefaultInstance);
        Assert.Equal("a", dfa.inputString);
    }

    // -------------------------------------------------------------------------
    // Parsing — error cases
    // -------------------------------------------------------------------------

    [Fact]
    public void DFA_Rejects_NFA_Instance_With_Epsilon_Edge()
    {
        // ε is not in the DFA alphabet {a,b}, so parsing an NFA instance must throw
        string nfaInstance =
            "(({1,2,3},{a,b},{(1,a,2),(1,ε,2),(1,b,3),(2,a,2),(2,ε,3),(2,b,2),(3,a,2),(3,b,3)},1,{2}),a)";
        Assert.Throws<InvalidOperationException>(() => new DFA(nfaInstance));
    }

    [Fact]
    public void DFA_Rejects_Edge_With_Symbol_Not_In_Alphabet()
    {
        string instance = "(({1,2},{a},{(1,c,2)},1,{2}),a)";
        Assert.Throws<InvalidOperationException>(() => new DFA(instance));
    }

    [Fact]
    public void DFA_Rejects_Unknown_Start_State()
    {
        string instance = "(({1,2},{a},{(1,a,2)},9,{2}),a)";
        Assert.Throws<InvalidOperationException>(() => new DFA(instance));
    }

    [Fact]
    public void DFA_Rejects_Unknown_Accept_State()
    {
        string instance = "(({1,2},{a},{(1,a,2)},1,{9}),a)";
        Assert.Throws<InvalidOperationException>(() => new DFA(instance));
    }

    [Fact]
    public void DFA_Rejects_Edge_From_Unknown_Node()
    {
        string instance = "(({1,2},{a},{(9,a,2)},1,{2}),a)";
        Assert.Throws<InvalidOperationException>(() => new DFA(instance));
    }

    [Fact]
    public void DFA_Rejects_Edge_To_Unknown_Node()
    {
        string instance = "(({1,2},{a},{(1,a,9)},1,{2}),a)";
        Assert.Throws<InvalidOperationException>(() => new DFA(instance));
    }

    // -------------------------------------------------------------------------
    // Solver
    // -------------------------------------------------------------------------

    [Fact]
    public void DFA_Solver_Accepts_Valid_Input()
    {
        // Default instance: input "a" → 1 →a→ 2 (accept state)
        DFA dfa = new DFA(DefaultInstance);
        DFASolver solver = new DFASolver();
        string result = solver.solve(dfa);
        Assert.Contains("sequence of states", result);
        Assert.DoesNotContain("No Solution", result);
    }

    [Fact]
    public void DFA_Solver_Rejects_Input_Ending_In_Non_Accept_State()
    {
        // Input "b": 1 →b→ 3 (non-accept state)
        string instance = "(({1,2,3},{a,b},{(1,a,2),(1,b,3),(2,a,2),(2,b,2),(3,a,2),(3,b,3)},1,{2}),b)";
        DFA dfa = new DFA(instance);
        DFASolver solver = new DFASolver();
        string result = solver.solve(dfa);
        Assert.Contains("No Solution", result);
    }

    [Theory]
    [InlineData("(({1,2,3},{a,b},{(1,a,2),(1,b,3),(2,a,2),(2,b,2),(3,a,2),(3,b,3)},1,{2}),ba)")]
    [InlineData("(({even,odd},{a,b},{(even,a,even),(even,b,odd),(odd,a,odd),(odd,b,even)},even,{even}),bb)")]
    public void DFA_Solver_Accepts_Multi_Step_Input(string instance)
    {
        DFA dfa = new DFA(instance);
        DFASolver solver = new DFASolver();
        string result = solver.solve(dfa);
        Assert.Contains("sequence of states", result);
        Assert.DoesNotContain("No Solution", result);
    }

    [Fact]
    public void DFA_Solver_Rejects_Character_Not_In_Alphabet()
    {
        DFA dfa = new DFA("(({1,2},{a},{(1,a,2)},1,{2}),c)");
        DFASolver solver = new DFASolver();
        string result = solver.solve(dfa);
        Assert.Contains("No Solution", result);
        Assert.Contains("'c'", result);
    }

    [Fact]
    public void DFA_Solver_Accepts_Empty_String_When_Start_Is_Accept()
    {
        // Start state q0 is accept; ε input represents the empty string
        DFA dfa = new DFA("(({q0,q1},{a},{(q0,a,q1)},q0,{q0}),ε)");
        DFASolver solver = new DFASolver();
        string result = solver.solve(dfa);
        Assert.Contains("sequence of states", result);
    }

    [Fact]
    public void DFA_Solver_Rejects_Empty_String_When_Start_Is_Not_Accept()
    {
        // Start state q0 is NOT accept; ε input cannot reach q1
        DFA dfa = new DFA("(({q0,q1},{a},{(q0,a,q1)},q0,{q1}),ε)");
        DFASolver solver = new DFASolver();
        string result = solver.solve(dfa);
        Assert.Contains("No Solution", result);
    }

    [Fact]
    public void DFA_Solver_Output_Contains_Correct_State_Path()
    {
        // Input "ba": 1 →b→ 3 →a→ 2; all three state labels must appear in output
        DFA dfa = new DFA("(({1,2,3},{a,b},{(1,a,2),(1,b,3),(2,a,2),(2,b,2),(3,a,2),(3,b,3)},1,{2}),ba)");
        DFASolver solver = new DFASolver();
        string result = solver.solve(dfa);
        Assert.Contains("1", result);
        Assert.Contains("3", result);
        Assert.Contains("2", result);
    }

    // -------------------------------------------------------------------------
    // Verifier
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("1,2")]    // 1 →a→ 2 (accept)
    [InlineData("1,3,2")]  // 1 →b→ 3 →a→ 2 (accept)
    public void DFA_Verifier_Accepts_Valid_Path(string certificate)
    {
        DFA dfa = new DFA(DefaultInstance);
        DFAVerifier verifier = new DFAVerifier();
        Assert.True(verifier.verify(dfa, certificate));
    }

    [Theory]
    [InlineData("1,3")]    // ends in non-accept state 3
    [InlineData("1,3,3")]  // ends in non-accept state 3 after self-loop
    public void DFA_Verifier_Rejects_Path_Not_Ending_In_Accept_State(string certificate)
    {
        DFA dfa = new DFA(DefaultInstance);
        DFAVerifier verifier = new DFAVerifier();
        Assert.False(verifier.verify(dfa, certificate));
    }

    [Theory]
    [InlineData("2,2")]   // starts at "2", not the start state "1"
    [InlineData("3,2")]   // starts at "3", not the start state "1"
    public void DFA_Verifier_Rejects_Path_Not_Starting_At_Start_State(string certificate)
    {
        DFA dfa = new DFA(DefaultInstance);
        DFAVerifier verifier = new DFAVerifier();
        Assert.False(verifier.verify(dfa, certificate));
    }

    [Fact]
    public void DFA_Verifier_Accepts_Single_Node_When_Start_Is_Accept()
    {
        // q0 is both start and accept; a one-node certificate "q0" is valid
        DFA dfa = new DFA("(({q0,q1},{a},{(q0,a,q1)},q0,{q0}),ε)");
        DFAVerifier verifier = new DFAVerifier();
        Assert.True(verifier.verify(dfa, "q0"));
    }

    // -------------------------------------------------------------------------
    // Solver — output content
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(({1,2,3},{a,b},{(1,a,2),(1,b,3),(2,a,2),(2,b,2),(3,a,2),(3,b,3)},1,{2}),a)", "1, 2")]
    [InlineData("(({1,2,3},{a,b},{(1,a,2),(1,b,3),(2,a,2),(2,b,2),(3,a,2),(3,b,3)},1,{2}),ba)", "1, 3, 2")]
    [InlineData("(({even,odd},{a,b},{(even,a,even),(even,b,odd),(odd,a,odd),(odd,b,even)},even,{even}),bb)", "even, odd, even")]
    public void DFA_Solver_Returns_Correct_State_Path_For_Accepted_Input(string instance, string expectedPath)
    {
        DFA dfa = new DFA(instance);
        DFASolver solver = new DFASolver();
        string result = solver.solve(dfa);
        Assert.Contains(expectedPath, result);
    }

    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void DFA_Instance_Format_Described()
    {
        DFA dfa = new DFA();
        Assert.NotNull(dfa.instanceFormat);
        Assert.NotEmpty(dfa.instanceFormat);
        Assert.Contains("N,A,E,S,F", dfa.instanceFormat);
    }

    [Fact]
    public void DFA_Certificate_Format_Described()
    {
        DFA dfa = new DFA();
        Assert.NotNull(dfa.certificateFormat);
        Assert.NotEmpty(dfa.certificateFormat);
        Assert.Contains("node names", dfa.certificateFormat);
    }

    [Fact]
    public void DFA_Certificate_Format_Example_Is_Actually_Valid()
    {
        // The "Example: 1,2" quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        DFA dfa = new DFA();
        DFAVerifier verifier = new DFAVerifier();
        Assert.True(verifier.verify(dfa, "1,2"));
    }
}
