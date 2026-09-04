using Xunit;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01.Solvers;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01.Verifiers;

namespace redux_tests;

#pragma warning disable CS1591

public class INTPROGRAMMING01_Tests {
    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void INTPROGRAMMING01_Instance_Format_Described() {
        INTPROGRAMMING01 problem = new INTPROGRAMMING01();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("<=", problem.instanceFormat);
    }

    [Fact]
    public void INTPROGRAMMING01_Certificate_Format_Described() {
        INTPROGRAMMING01 problem = new INTPROGRAMMING01();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("bits", problem.certificateFormat);
    }

    [Fact]
    public void INTPROGRAMMING01_Certificate_Format_Example_Is_Actually_Valid() {
        // The "Example: (0 0 0)" quoted in certificateFormat must be a real,
        // verifiable certificate for defaultInstance — not just descriptive prose.
        INTPROGRAMMING01 problem = new INTPROGRAMMING01();
        GenericVerifier01INTP verifier = new GenericVerifier01INTP();
        Assert.True(verifier.verify(problem, "(0 0 0)"));
    }

    // -------------------------------------------------------------------------
    // IntegerProgrammingBruteForce
    // -------------------------------------------------------------------------

    [Fact]
    public void INTPROGRAMMING01_BruteForce_Default_Instance_Returns_AllZero_Assignment() {
        // solve() checks the all-zero assignment (the loop's initial binary vector) BEFORE
        // ever calling nextBinary, and for the default instance Cx = (0,0,0) <= d = (0,0,0)
        // is trivially satisfied, so it must return immediately without advancing.
        INTPROGRAMMING01 problem = new INTPROGRAMMING01();
        IntegerProgrammingBruteForce solver = new IntegerProgrammingBruteForce();

        string solution = solver.solve(problem);

        Assert.Equal("(0 0 0)", solution);
        Assert.True(new GenericVerifier01INTP().verify(problem, solution));
    }

    [Fact]
    public void INTPROGRAMMING01_BruteForce_Returns_Empty_When_No_Assignment_Satisfies() {
        // A single variable x in {0,1} with constraint x <= -1: neither x=0 (Cx=0) nor
        // x=1 (Cx=1) can satisfy <= -1, so the solver must exhaust both candidates
        // (including nextBinary's carry-rollover from [1] back to [0]) and return "()".
        INTPROGRAMMING01 problem = new INTPROGRAMMING01("(1)<=(-1)");
        IntegerProgrammingBruteForce solver = new IntegerProgrammingBruteForce();

        string solution = solver.solve(problem);

        Assert.Equal("()", solution);
    }

    [Fact]
    public void INTPROGRAMMING01_BruteForce_Finds_Assignment_Requiring_Multiple_Candidates() {
        // Two variables, x0 - x1 <= -1. x=(0,0) gives 0 (fails), x=(1,0) gives 1 (fails),
        // and only x=(0,1) gives -1 (satisfies). This forces the solver through both a
        // plain increment and a carry-rollover of nextBinary before succeeding.
        INTPROGRAMMING01 problem = new INTPROGRAMMING01("(1 -1)<=(-1)");
        IntegerProgrammingBruteForce solver = new IntegerProgrammingBruteForce();

        string solution = solver.solve(problem);

        Assert.Equal("(0 1)", solution);
        Assert.True(new GenericVerifier01INTP().verify(problem, solution));
    }

    [Fact]
    public void INTPROGRAMMING01_BruteForce_Single_Always_Satisfiable_Constraint() {
        // A single variable with a constraint that any assignment satisfies (x <= 5):
        // the very first candidate x=(0) already works.
        INTPROGRAMMING01 problem = new INTPROGRAMMING01("(1)<=(5)");
        IntegerProgrammingBruteForce solver = new IntegerProgrammingBruteForce();

        string solution = solver.solve(problem);

        Assert.Equal("(0)", solution);
        Assert.True(new GenericVerifier01INTP().verify(problem, solution));
    }

    [Fact]
    public void INTPROGRAMMING01_BruteForce_Multiple_Constraints_Output_Passes_Verifier() {
        string[] instances = {
            INTPROGRAMMING01._defaultInstance,
            "(1 -1)<=(-1)",
            "(1)<=(5)",
            "(-1 1 -1),(0 0 -1),(-1 -1 1)<=(0 0 0)",
        };
        foreach (string inst in instances) {
            INTPROGRAMMING01 problem = new INTPROGRAMMING01(inst);
            IntegerProgrammingBruteForce solver = new IntegerProgrammingBruteForce();
            string solution = solver.solve(problem);
            if (solution == "()") continue; // no satisfying assignment exists; nothing to verify
            Assert.True(new GenericVerifier01INTP().verify(problem, solution), $"Solver output failed verifier for: {inst}");
        }
    }
}
