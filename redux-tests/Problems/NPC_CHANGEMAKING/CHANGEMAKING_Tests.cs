using Xunit;
using API.Interfaces;
using API.Problems.NPComplete.NPC_CHANGEMAKING;
using API.Problems.NPComplete.NPC_CHANGEMAKING.Solvers;
using API.Problems.NPComplete.NPC_CHANGEMAKING.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class CHANGEMAKING_Tests {
    // -------------------------------------------------------------------------
    // Instantiation
    // -------------------------------------------------------------------------

    [Fact]
    public void CHANGEMAKING_Default_Instantiation() {
        CHANGEMAKING problem = new CHANGEMAKING();
        Assert.Equal("({1,7,10,25},39,3)", problem.instance);
        Assert.Equal("({1,7,10,25},39,3)", problem.defaultInstance);
        Assert.Equal(new List<int> { 1, 7, 10, 25 }, problem.C);
        Assert.Equal(39, problem.X);
        Assert.Equal(3, problem.K);
    }

    [Fact]
    public void CHANGEMAKING_Custom_Instantiation() {
        CHANGEMAKING problem = new CHANGEMAKING("({3,4},6,0)");
        Assert.Equal(new List<int> { 3, 4 }, problem.C);
        Assert.Equal(6, problem.X);
        Assert.Equal(0, problem.K);
    }

    // -------------------------------------------------------------------------
    // Constructor — invalid instances (all must throw ProblemParseException)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("")]                   // empty
    [InlineData("101")]                // bare string
    [InlineData("({1,5},10)")]         // missing K
    [InlineData("({1,x},10,3)")]       // non-integer coin
    [InlineData("({1,5},y,3)")]        // non-integer X
    [InlineData("({1,5},10,z)")]       // non-integer K
    [InlineData("({1,0},10,3)")]       // zero coin
    [InlineData("({1,-5},10,3)")]      // negative coin
    [InlineData("({1,5},0,3)")]        // X must be positive
    [InlineData("({1,5},10,-1)")]      // K must be nonnegative
    public void CHANGEMAKING_Constructor_Throws_On_Invalid_Instance(string instance) {
        Assert.Throws<ProblemParseException>(() => new CHANGEMAKING(instance));
    }

    // -------------------------------------------------------------------------
    // Verifier — valid certificates
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("({1,7,10,25},39,3)", "{(25,1),(7,2)}")]       // 25 + 7 + 7, three coins
    [InlineData("({1,7,10,25},39,6)", "{(25,1),(10,1),(1,4)}")] // the greedy answer, under a looser K
    [InlineData("({1,3,4},6,2)", "{(3,2)}")]                    // one coin value used twice
    [InlineData("({5},5,1)", "{(5,1)}")]                         // K exactly met
    [InlineData("({1,7,10,25},39,3)", "{(25,1),(7,2),(10,0)}")] // a zero count is harmless
    public void CHANGEMAKING_Verifier_Accepts_Valid_Certificate(string instance, string certificate) {
        CHANGEMAKING problem = new CHANGEMAKING(instance);
        Assert.True(new ChangeMakingVerifier().verify(problem, certificate));
    }

    // -------------------------------------------------------------------------
    // Verifier — well-formed but wrong certificates (must return false)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("({1,7,10,25},39,3)", "{(25,1),(10,1),(1,4)}")] // makes 39, but six coins > K = 3
    [InlineData("({1,7,10,25},39,3)", "{(25,1),(7,1)}")]        // makes 32, not 39
    [InlineData("({1,7,10,25},39,3)", "{(13,3)}")]              // 13 is not a coin
    [InlineData("({1,7,10,25},39,3)", "{(25,3),(7,-2)}")]       // negative count
    [InlineData("({1,7,10,25},39,3)", "{}")]                     // no coins make 0, not 39
    public void CHANGEMAKING_Verifier_Rejects_Invalid_Certificate(string instance, string certificate) {
        CHANGEMAKING problem = new CHANGEMAKING(instance);
        Assert.False(new ChangeMakingVerifier().verify(problem, certificate));
    }

    // -------------------------------------------------------------------------
    // Verifier — malformed certificates (must throw CertificateParseException)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("")]               // empty
    [InlineData("{(25,x)}")]       // non-integer count
    [InlineData("{(a,1)}")]        // non-integer coin
    public void CHANGEMAKING_Verifier_Throws_On_Malformed_Certificate(string certificate) {
        CHANGEMAKING problem = new CHANGEMAKING();
        Assert.Throws<CertificateParseException>(() => new ChangeMakingVerifier().verify(problem, certificate));
    }

    // -------------------------------------------------------------------------
    // Solver
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("({1,7,10,25},39,3)")] // greedy would need six coins
    [InlineData("({1,3,4},6,2)")]      // greedy would take 4+1+1
    [InlineData("({1,5,10,25},63,6)")] // canonical US coins: 25+25+10+1+1+1
    [InlineData("({2,3},7,3)")]        // 2+2+3, no 1-coin
    [InlineData("({7},49,7)")]         // a single coin value
    public void CHANGEMAKING_Solver_ProducesVerifiableCertificate(string instance) {
        CHANGEMAKING problem = new CHANGEMAKING(instance);
        string solution = new ChangeMakingDP().solve(problem);
        Assert.True(new ChangeMakingVerifier().verify(problem, solution),
            $"ChangeMakingDP returned unverifiable certificate {solution} for {instance}");
    }

    [Fact]
    public void CHANGEMAKING_Solver_Finds_Optimal_Default() {
        // The optimum is 25 + 7 + 7; greedy (25 + 10 + 1*4) would exceed K = 3.
        CHANGEMAKING problem = new CHANGEMAKING();
        Assert.Equal("{(7,2),(25,1)}", new ChangeMakingDP().solve(problem));
    }

    [Theory]
    [InlineData("({1,7,10,25},39,2)")] // three coins is the minimum
    [InlineData("({2,4},7,10)")]       // odd X from even coins: unreachable
    [InlineData("({3,4},6,1)")]        // 3+3 needs two
    [InlineData("({1},5,0)")]          // K = 0 with positive X
    [InlineData("({},5,3)")]           // no coins at all
    public void CHANGEMAKING_Solver_Returns_Empty_When_No_Solution(string instance) {
        CHANGEMAKING problem = new CHANGEMAKING(instance);
        Assert.Equal("{}", new ChangeMakingDP().solve(problem));
    }

    [Fact]
    public void CHANGEMAKING_Solver_Stops_When_Timer_Has_Expired() {
        CHANGEMAKING problem = new CHANGEMAKING();
        ChangeMakingDP solver = new ChangeMakingDP();
        solver.timerHasExpired = true;
        Assert.Equal(SolveRun.TimedOutAnswer, solver.solve(problem));
    }

    // -------------------------------------------------------------------------
    // Memoized solver
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("({1,7,10,25},39,3)")]
    [InlineData("({1,3,4},6,2)")]
    [InlineData("({1,5,10,25},63,6)")]
    [InlineData("({2,3},7,3)")]
    [InlineData("({7},49,7)")]
    public void CHANGEMAKING_MemoSolver_ProducesVerifiableCertificate(string instance) {
        CHANGEMAKING problem = new CHANGEMAKING(instance);
        string solution = new ChangeMakingMemo().solve(problem);
        Assert.True(new ChangeMakingVerifier().verify(problem, solution),
            $"ChangeMakingMemo returned unverifiable certificate {solution} for {instance}");
    }

    [Theory]
    [InlineData("({1,7,10,25},39,2)")]
    [InlineData("({2,4},7,10)")]
    [InlineData("({3,4},6,1)")]
    [InlineData("({1},5,0)")]
    [InlineData("({},5,3)")]
    public void CHANGEMAKING_MemoSolver_Returns_Empty_When_No_Solution(string instance) {
        CHANGEMAKING problem = new CHANGEMAKING(instance);
        Assert.Equal("{}", new ChangeMakingMemo().solve(problem));
    }

    [Theory]
    [InlineData("({1,7,10,25},39,3)")]
    [InlineData("({1,7,10,25},100,100)")]
    [InlineData("({3,5,11},97,97)")]
    [InlineData("({6,9,20},43,43)")]   // 43 is the largest amount these coins cannot make
    [InlineData("({6,9,20},44,44)")]
    [InlineData("({4,9,13},1000,1000)")]
    public void CHANGEMAKING_MemoSolver_Agrees_With_DP(string instance) {
        // Both compute the same recurrence and break ties the same way (first coin in C),
        // so they produce the same certificate, not just one of the same size.
        CHANGEMAKING problem = new CHANGEMAKING(instance);
        Assert.Equal(new ChangeMakingDP().solve(problem), new ChangeMakingMemo().solve(problem));
    }

    [Fact]
    public void CHANGEMAKING_MemoSolver_Handles_Deep_Chain_Without_Stack_Overflow() {
        // With only the 1-coin the subproblem chain is X deep; a recursive memo would overflow the
        // call stack (an uncatchable crash), so the solver must use its explicit stack.
        CHANGEMAKING problem = new CHANGEMAKING("({1},1000000,1000000)");
        Assert.Equal("{(1,1000000)}", new ChangeMakingMemo().solve(problem));
    }

    [Fact]
    public void CHANGEMAKING_MemoSolver_Stops_When_Timer_Has_Expired() {
        CHANGEMAKING problem = new CHANGEMAKING();
        ChangeMakingMemo solver = new ChangeMakingMemo();
        solver.timerHasExpired = true;
        Assert.Equal(SolveRun.TimedOutAnswer, solver.solve(problem));
    }

    // -------------------------------------------------------------------------
    // Size limits: refused with a 400, never an out-of-memory crash
    // -------------------------------------------------------------------------

    [Fact]
    public void CHANGEMAKING_DPSolver_Accepts_Largest_Allowed_X() {
        CHANGEMAKING problem = new CHANGEMAKING($"({{1,1000}},{ChangeMakingDP.MaxAmount},10000)");
        Assert.Equal("{(1000,10000)}", new ChangeMakingDP().solve(problem));
    }

    [Theory]
    [InlineData("({1},10000001,1)")]       // one past the limit
    [InlineData("({1},2000000000,1)")]     // about 8 GB per table if it were allocated
    public void CHANGEMAKING_DPSolver_Refuses_X_Above_Limit(string instance) {
        CHANGEMAKING problem = new CHANGEMAKING(instance);
        Assert.Throws<ExactSearchLimitExceededException>(() => new ChangeMakingDP().solve(problem));
    }

    [Fact]
    public void CHANGEMAKING_MemoSolver_Solves_Large_X_With_Few_Reachable_Amounts() {
        // X is far past ChangeMakingDP.MaxAmount, but only multiples of 10^8 can be reached from it.
        CHANGEMAKING problem = new CHANGEMAKING("({100000000,300000000},1000000000,4)");
        string solution = new ChangeMakingMemo().solve(problem);
        Assert.Equal("{(100000000,1),(300000000,3)}", solution);
        Assert.True(new ChangeMakingVerifier().verify(problem, solution));
        Assert.Throws<ExactSearchLimitExceededException>(() => new ChangeMakingDP().solve(problem));
    }

    [Fact]
    public void CHANGEMAKING_MemoSolver_Refuses_When_Too_Many_Amounts_Are_Reachable() {
        // With a 1-coin every amount up to X is reachable, so the memo would hold X + 1 entries.
        CHANGEMAKING problem = new CHANGEMAKING("({1},2000000000,1)");
        Assert.Throws<ExactSearchLimitExceededException>(() => new ChangeMakingMemo().solve(problem));
    }
}
