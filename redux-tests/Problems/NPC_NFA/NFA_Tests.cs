using Xunit;
using API.Problems.P.P_NFA;
using API.Problems.P.P_NFA.Solvers;
using API.Problems.P.P_NFA.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class NFA_Tests
{
    private const string DefaultInstance =
        "(({1,2,3},{a,b},{(1,a,2),(1,ε,2),(1,b,3),(2,a,2),(2,ε,3),(2,b,2),(3,a,2),(3,b,3)},1,{2}),a)";

    private const char Epsilon = 'ε';

    // -------------------------------------------------------------------------
    // Instantiation
    // -------------------------------------------------------------------------

    [Fact]
    public void NFA_Default_Instantiation()
    {
        NFA nfa = new NFA();
        Assert.Equal(DefaultInstance, nfa.defaultInstance);
        Assert.Equal(DefaultInstance, nfa.instance);
    }

    [Fact]
    public void NFA_Custom_Instantiation_Without_Epsilon()
    {
        // A DFA-like NFA (no ε transitions) is a valid NFA
        string instance = "(({p,q},{a,b},{(p,a,q),(p,b,p),(q,a,q),(q,b,q)},p,{q}),ab)";
        NFA nfa = new NFA(instance);
        Assert.Equal(instance, nfa.instance);
        Assert.Equal(2, nfa.nodes.Count);
        Assert.Equal(4, nfa.edges.Count);
        Assert.DoesNotContain(nfa.edges, e => e.Symbol == Epsilon);
    }

    // -------------------------------------------------------------------------
    // Parsing
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("(({1,2,3},{a,b},{(1,a,2),(1,ε,2),(1,b,3),(2,a,2),(2,ε,3),(2,b,2),(3,a,2),(3,b,3)},1,{2}),a)", 3)]
    [InlineData("(({p,q},{a,b},{(p,a,q),(p,b,p),(q,a,q),(q,b,q)},p,{q}),ab)", 2)]
    public void NFA_Parses_Correct_Node_Count(string instance, int expectedCount)
    {
        NFA nfa = new NFA(instance);
        Assert.Equal(expectedCount, nfa.nodes.Count);
    }

    [Fact]
    public void NFA_Parses_Epsilon_Transitions()
    {
        NFA nfa = new NFA(DefaultInstance);
        // Default instance has 8 edges: 6 regular + 2 ε
        Assert.Equal(8, nfa.edges.Count);
        Assert.Equal(2, nfa.edges.Count(e => e.Symbol == Epsilon));
        Assert.Contains(nfa.edges, e => e.From == "1" && e.Symbol == Epsilon && e.To == "2");
        Assert.Contains(nfa.edges, e => e.From == "2" && e.Symbol == Epsilon && e.To == "3");
    }

    [Fact]
    public void NFA_Allows_Epsilon_Edge_Even_When_Epsilon_Not_In_Declared_Alphabet()
    {
        // ε is always a valid edge label in NFA regardless of the declared alphabet
        string instance = "(({s,t},{a},{(s,ε,t)},s,{t}),ε)";
        NFA nfa = new NFA(instance);
        Assert.Single(nfa.edges);
        Assert.Equal(Epsilon, nfa.edges[0].Symbol);
    }

    [Fact]
    public void NFA_Accepts_DFA_Instance_As_Valid_NFA()
    {
        // Every DFA is a valid NFA; constructing NFA from DFA's default instance must not throw
        string dfaDefault =
            "(({1,2,3},{a,b},{(1,a,2),(1,b,3),(2,a,2),(2,b,2),(3,a,2),(3,b,3)},1,{2}),a)";
        NFA nfa = new NFA(dfaDefault);
        Assert.Equal(3, nfa.nodes.Count);
        Assert.Equal(6, nfa.edges.Count);
        Assert.DoesNotContain(nfa.edges, e => e.Symbol == Epsilon);
    }

    // -------------------------------------------------------------------------
    // Parsing — error cases
    // -------------------------------------------------------------------------

    [Fact]
    public void NFA_Rejects_Edge_With_Symbol_Not_In_Alphabet_And_Not_Epsilon()
    {
        string instance = "(({s,t},{a},{(s,c,t)},s,{t}),a)";
        Assert.Throws<InvalidOperationException>(() => new NFA(instance));
    }

    [Fact]
    public void NFA_Rejects_Unknown_Start_State()
    {
        string instance = "(({1,2},{a},{(1,a,2)},9,{2}),a)";
        Assert.Throws<InvalidOperationException>(() => new NFA(instance));
    }

    [Fact]
    public void NFA_Rejects_Unknown_Accept_State()
    {
        string instance = "(({1,2},{a},{(1,a,2)},1,{9}),a)";
        Assert.Throws<InvalidOperationException>(() => new NFA(instance));
    }

    // -------------------------------------------------------------------------
    // DFA vs NFA — structural asymmetry
    // -------------------------------------------------------------------------

    [Fact]
    public void NFA_Accepts_DFA_Default_Instance_And_Solver_Finds_Solution()
    {
        // DFA instance has no ε edges, so NFA accepts it and the solver runs normally.
        // This is the backend root cause of the DFA→NFA visualization bug: the first
        // visualization request uses the DFA instance, which NFA silently accepts,
        // returning a DFA-looking graph before the correct NFA instance arrives.
        string dfaDefault =
            "(({1,2,3},{a,b},{(1,a,2),(1,b,3),(2,a,2),(2,b,2),(3,a,2),(3,b,3)},1,{2}),a)";
        NFA nfa = new NFA(dfaDefault);
        NFASolver solver = new NFASolver();
        string result = solver.solve(nfa);
        Assert.Contains("sequence of states", result);
    }

    [Fact]
    public void DFA_Rejects_NFA_Default_Instance_With_Epsilon()
    {
        // NFA instance has ε edges; the DFA constructor rejects them because ε ∉ {a,b}.
        // This asymmetry is why NFA→DFA clears the visualization but DFA→NFA does not.
        string nfaDefault =
            "(({1,2,3},{a,b},{(1,a,2),(1,ε,2),(1,b,3),(2,a,2),(2,ε,3),(2,b,2),(3,a,2),(3,b,3)},1,{2}),a)";
        Assert.Throws<InvalidOperationException>(
            () => new API.Problems.P.P_DFA.DFA(nfaDefault));
    }

    // -------------------------------------------------------------------------
    // Solver
    // -------------------------------------------------------------------------

    [Fact]
    public void NFA_Solver_Accepts_Input_Via_Direct_Path()
    {
        // Input "a": direct path 1 →a→ 2 (accept state)
        NFA nfa = new NFA(DefaultInstance);
        NFASolver solver = new NFASolver();
        string result = solver.solve(nfa);
        Assert.Contains("sequence of states", result);
        Assert.DoesNotContain("No Solution", result);
    }

    [Fact]
    public void NFA_Solver_Returns_Multiple_Accepting_Paths_For_Nondeterminism()
    {
        // Default instance + input "a" has three distinct accepting paths (direct,
        // via ε, and via ε-chain), so the solver output must contain multiple lines
        NFA nfa = new NFA(DefaultInstance);
        NFASolver solver = new NFASolver();
        string result = solver.solve(nfa);
        string[] lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 2, "Expected multiple accepting paths for a nondeterministic run");
    }

    [Fact]
    public void NFA_Solver_Accepts_Input_Via_Epsilon_Then_Regular_Transition()
    {
        // s →ε→ t →a→ u (accept); the only accepting run uses an ε transition
        string instance = "(({s,t,u},{a},{(s,ε,t),(t,a,u)},s,{u}),a)";
        NFA nfa = new NFA(instance);
        NFASolver solver = new NFASolver();
        string result = solver.solve(nfa);
        Assert.Contains("sequence of states", result);
        Assert.DoesNotContain("No Solution", result);
    }

    [Fact]
    public void NFA_Solver_Accepts_Empty_String_Via_Epsilon_To_Accept_State()
    {
        // s →ε→ t (accept); empty input is accepted through the ε-closure of s
        string instance = "(({s,t},{a},{(s,ε,t)},s,{t}),ε)";
        NFA nfa = new NFA(instance);
        NFASolver solver = new NFASolver();
        string result = solver.solve(nfa);
        Assert.Contains("sequence of states", result);
        Assert.DoesNotContain("No Solution", result);
    }

    [Fact]
    public void NFA_Solver_Rejects_Input_With_No_Accepting_Run()
    {
        // From s only 'a' transitions exist; input "b" has no valid run
        string instance = "(({s,t},{a,b},{(s,a,t)},s,{t}),b)";
        NFA nfa = new NFA(instance);
        NFASolver solver = new NFASolver();
        string result = solver.solve(nfa);
        Assert.Contains("No Solution", result);
    }

    [Fact]
    public void NFA_Solver_Rejects_Character_Not_In_Alphabet()
    {
        NFA nfa = new NFA("(({s,t},{a},{(s,a,t)},s,{t}),c)");
        NFASolver solver = new NFASolver();
        string result = solver.solve(nfa);
        Assert.Contains("No Solution", result);
        Assert.Contains("'c'", result);
    }

    [Fact]
    public void NFA_Solver_Handles_Epsilon_Cycle_Without_Infinite_Loop()
    {
        // s →ε→ t →ε→ s creates a cycle; the visited set must prevent infinite recursion.
        // The direct path s →a→ u (accept) must still be found.
        string instance = "(({s,t,u},{a},{(s,ε,t),(t,ε,s),(s,a,u)},s,{u}),a)";
        NFA nfa = new NFA(instance);
        NFASolver solver = new NFASolver();
        string result = solver.solve(nfa);
        Assert.Contains("sequence of states", result);
        Assert.DoesNotContain("No Solution", result);
    }

    // -------------------------------------------------------------------------
    // Verifier
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("1,2")]      // direct path: 1 →a→ 2
    [InlineData("1,2,2")]    // ε-then-regular: 1 →ε→ 2 →a→ 2
    [InlineData("1,2,3,2")]  // ε-chain: 1 →ε→ 2 →ε→ 3 →a→ 2
    public void NFA_Verifier_Accepts_Valid_Certificate(string certificate)
    {
        NFA nfa = new NFA(DefaultInstance);
        NFAVerifier verifier = new NFAVerifier();
        Assert.True(verifier.verify(nfa, certificate));
    }

    [Theory]
    [InlineData("1,3")]     // no valid run for input "a" starts 1→3 (that requires reading 'b')
    [InlineData("1,3,2")]   // would require reading 'b' then 'a', but input is only "a"
    [InlineData("2,2")]     // doesn't start at start state "1"
    public void NFA_Verifier_Rejects_Invalid_Certificate(string certificate)
    {
        NFA nfa = new NFA(DefaultInstance);
        NFAVerifier verifier = new NFAVerifier();
        Assert.False(verifier.verify(nfa, certificate));
    }

    [Fact]
    public void NFA_Verifier_Accepts_Epsilon_Path_Certificate()
    {
        // s →ε→ t (accept), empty input; certificate "s,t" traces the ε transition
        string instance = "(({s,t},{a},{(s,ε,t)},s,{t}),ε)";
        NFA nfa = new NFA(instance);
        NFAVerifier verifier = new NFAVerifier();
        Assert.True(verifier.verify(nfa, "s,t"));
    }

    [Fact]
    public void NFA_Verifier_Returns_False_When_No_Accepting_Run_Exists()
    {
        // Input "b" has no accepting run; the verifier must return false for any certificate
        string instance = "(({s,t},{a,b},{(s,a,t)},s,{t}),b)";
        NFA nfa = new NFA(instance);
        NFAVerifier verifier = new NFAVerifier();
        Assert.False(verifier.verify(nfa, "s,t"));
    }

    // -------------------------------------------------------------------------
    // Solver — output content
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("1, 2")]      // direct: 1 →a→ 2
    [InlineData("1, 2, 2")]   // ε-then-regular: 1 →ε→ 2 →a→ 2
    [InlineData("1, 2, 3, 2")] // ε-chain: 1 →ε→ 2 →ε→ 3 →a→ 2
    public void NFA_Solver_Output_Contains_Known_Accepting_Path(string expectedPath)
    {
        NFA nfa = new NFA(DefaultInstance);
        NFASolver solver = new NFASolver();
        string result = solver.solve(nfa);
        Assert.Contains(expectedPath, result);
    }

    [Fact]
    public void NFA_Solver_Returns_State_Path_For_Epsilon_Accepted_Input()
    {
        // s →ε→ t (accept), empty input; the only accepting path is "s, t"
        string instance = "(({s,t},{a},{(s,ε,t)},s,{t}),ε)";
        NFA nfa = new NFA(instance);
        NFASolver solver = new NFASolver();
        string result = solver.solve(nfa);
        Assert.Contains("s, t", result);
    }

    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void NFA_Instance_Format_Described()
    {
        NFA nfa = new NFA();
        Assert.NotNull(nfa.instanceFormat);
        Assert.NotEmpty(nfa.instanceFormat);
        Assert.Contains("N,A,E,S,F", nfa.instanceFormat);
        Assert.Contains("epsilon", nfa.instanceFormat, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NFA_Certificate_Format_Described()
    {
        NFA nfa = new NFA();
        Assert.NotNull(nfa.certificateFormat);
        Assert.NotEmpty(nfa.certificateFormat);
        Assert.Contains("node names", nfa.certificateFormat);
    }

    [Fact]
    public void NFA_Certificate_Format_Example_Is_Actually_Valid()
    {
        // The "Example: 1,2" quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        NFA nfa = new NFA();
        NFAVerifier verifier = new NFAVerifier();
        Assert.True(verifier.verify(nfa, "1,2"));
    }
}
