using API.Interfaces;
using API.Interfaces.Steps;

using API.Problems.NPComplete.NPC_SAT3;
using static API.Interfaces.Graphs.GroupedGraphFrames;
using static API.Interfaces.Steps.Words;

namespace API.Problems.NPComplete.NPC_SAT3.Solvers;

// The search records a step for each value it tries (a Try, then an Accept when the branch is queued or satisfies every
// clause, or a Reject when a clause is left with no true literal), a Backtrack when it takes a queued branch that is not one
// it just made, and a Done with the answer. SearchSteps builds the Search Tree picture's nodes from those steps.
class Sat3BacktrackingSolver : ISolver<SAT3, Assignment>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "3SAT Backtracking";
    public string solverDefinition { get; } = "This is a O(2^n) solution algorithm for the 3SAT problem which implements a back tracking algorithm to find an exact assignment boolean assignment of variables to satisfy the problem instance.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "David Lindeman", "Kaden Marchetti" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Exact constraint-backtracking search WITH pruning -- distinct from an
    // unpruned brute-force enumeration.
    public SolverType solverType { get; } = SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Priority-queue-guided backtracking: each state branches into
    // up to 2 children (true/false for the next variable), and building each child
    // re-scans all clauses -- worst case is still a full 2^n binary tree of assignments.
    public MathematicalFunction timeComplexity { get; } = new("2^n * |phi|", ", n = number of variables, |phi| = formula size");

    // --- Methods Including Constructors ---
    public Sat3BacktrackingSolver() {
    }

    // How the steps show a state: its assignment, the variable it decides, and its node in the Search Tree picture ("x2=F").
    // A rejected value's node names the clause it falsifies; the solution's node names the variables it never had to set.
    // Both run only when the node is built, since each is a lookup the search itself does not make.
    private static SearchSteps<SAT3PQObject, bool, Assignment> NewSteps(SAT3 sat3, StepRecorder<Assignment> rec) => new(rec,
        // What every picture shows: the assignment so far and the pieces the step is about.
        partial: s => ToAssignment(s.varStates),
        focus: s => [VariableId(s.nextVar)],
        childPartial: (s, value) => ToAssignment(WithValue(s.varStates, s.nextVar, value)),
        rejectFocus: (s, value) => FirstFalsifiedClause(sat3.clauses, WithValue(s.varStates, s.nextVar, value)) is int c and >= 0
            ? [VariableId(s.nextVar), ClauseId(c)]
            : [VariableId(s.nextVar)],
        // The Search Tree picture's node text only.
        childLabel: (s, value) => $"{s.nextVar}={(value ? "T" : "F")}",
        rejectNote: (s, value) => FirstFalsifiedClause(sat3.clauses, WithValue(s.varStates, s.nextVar, value)) is int c and >= 0 ? $"C{c + 1} false" : null,
        solvedNote: s => sat3.literals.Select(l => l.TrimStart('!')).Distinct().Where(v => !s.varStates.ContainsKey(v)).ToList() switch {
            { Count: 0 } => null,
            { Count: <= 2 } free => $"{string.Join(", ", free)} free",
            var free => $"{free.Count} free",
        });

    public string solve(SAT3 sat3) => Solve(sat3, StepRecorder<Assignment>.Off);

    public string Solve(SAT3 sat3, StepRecorder<Assignment> rec) {
        var steps = NewSteps(sat3, rec);
        Dictionary<string, bool>? solution = Search(sat3, steps);

        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        if (solution == null) {
            steps.Done(new Assignment([]), false, $"Every branch was rejected, so there is no solution.");
            return "No Solution";
        }

        var decided = new Dictionary<string, bool>(solution);
        List<string> free = [];
        // fill in any variables the backtracker left unassigned
        foreach (string literal in sat3.literals) {
            string varName = literal.TrimStart('!');
            if (!solution.ContainsKey(varName)) {
                // value doesn't matter, choose false
                solution[varName] = false;
                free.Add(varName);
            }
        }

        string solutionString = "(";
        foreach (KeyValuePair<string, bool> kvp in solution) {
            solutionString = solutionString + kvp.Key + ":" + kvp.Value.ToString() + ",";
        }
        solutionString = solutionString.TrimEnd(',');
        solutionString += ")";
        steps.Done(ToAssignment(solution), true, $"Every clause has a true literal under {Assigned(decided)}.{(free.Count switch {
            0 => "",
            1 => $" {free[0]} is free (either value works), so the answer sets it to False.",
            _ => $" {string.Join(", ", free.SkipLast(1))} and {free[^1]} are free (either value works), so the answer sets them to False.",
        })}");
        return solutionString;
    }

    // Return type varies
    public Dictionary<string, bool>? findSolution(SAT3 sat3) => findSolution(sat3, StepRecorder<Assignment>.Off);

    public Dictionary<string, bool>? findSolution(SAT3 sat3, StepRecorder<Assignment> rec) => Search(sat3, NewSteps(sat3, rec));

    // A best-first backtracking search: each state decides its next variable both ways, a value that leaves a clause with
    // no true literal is pruned, and the most promising state still queued is expanded next, until every clause has a true
    // literal.
    private Dictionary<string, bool>? Search(SAT3 sat3, SearchSteps<SAT3PQObject, bool, Assignment> steps) {
        // A single literal such as (x1) is still a satisfiable formula, so only an empty formula is rejected here.
        if (sat3.literals.Count < 1) {
            return null;
        }

        bool solutionFound = false;
        PriorityQueue<SAT3PQObject, int> satPQ = new PriorityQueue<SAT3PQObject, int>();
        Dictionary<string, bool>? solution = null;

        int totalNumberOfVariables = findVariables(sat3.literals);

        SAT3PQObject curSat;
        int eval;

        //add initial SAT3 to PQ
        curSat = new SAT3PQObject(sat3, 0, totalNumberOfVariables);
        curSat.initVarWeights();
        curSat.removeDuplicatesFromClauses();
        satPQ.Enqueue(curSat, curSat.getPQWeight());
        steps.Root(curSat, $"start");

        while (!solutionFound && satPQ.Count > 0) {
            if (timerHasExpired) return null; // stop at the time limit; Solve reports the stop
            curSat = satPQ.Dequeue();
            steps.Expand(curSat, $"{(steps.DeadEnd ? "Neither value of the last variable works. " : "")}Go back to {Assigned(curSat.varStates)}, the most promising branch still open: {curSat.nextVar} is next.");
            var (trueChild, falseChild) = curSat.createSATChildPair(curSat.depth, totalNumberOfVariables);
            foreach (var (value, childSAT) in new[] { (true, trueChild), (false, falseChild) }) {
                steps.Try(curSat, value, $"Try {curSat.nextVar} = {value}{(curSat.varStates.Count == 0 ? "" : $" after {Assigned(curSat.varStates)}")}.");
                //Invalid statements are evaluated upon creation and returned as null
                if (childSAT != null) {
                    eval = evaluateBooleanExpression(childSAT.SATState.clauses);
                    if (eval == 0) {
                        //undecided
                        satPQ.Enqueue(childSAT, childSAT.getPQWeight());
                        steps.Accept(curSat, value, $"Keep {curSat.nextVar} = {value}: no clause has every literal false yet, so this branch is queued.", queued: childSAT,
                            note: $"{Count(childSAT.SATState.clauses.Count, "clause")} left");
                    } else if (eval == 1) {
                        //satisfiable
                        solutionFound = true;
                        solution = childSAT.varStates;
                        steps.Accept(curSat, value, $"Keep {curSat.nextVar} = {value}: every clause now has a true literal.", solved: childSAT);
                        break; //SOLUTION FOUND EXIT FOR EACH
                    }
                } else {
                    //unsolvable therefore dont add
                    steps.Reject(curSat, value, $"{curSat.nextVar} = {value} fails: {(FirstFalsifiedClause(sat3.clauses, WithValue(curSat.varStates, curSat.nextVar, value)) is int c and >= 0 ? $"clause C{c + 1} {ClauseText(sat3.clauses[c])} has no true literal left." : "a clause is left with no true literal.")}");
                }
            }
        }
        return solution;
    }

    //Takes in a new SATState with boolean values written to the states and evaluates them
    //Invalid expressions are filtered at the creation of the expressions and do not make it to this point of the process
    //Returns 0 if undecided
    //Returns 1 if satisfiable
    private int evaluateBooleanExpression(List<List<string>> boolExp) {
        //Check for satisfiablility
        int retVal = 0;
        if (boolExp.Count == 1 && string.IsNullOrEmpty(boolExp[0][0])) {
            retVal = 1;
        }

        //variables for looping through boolExp
        int index = 0;
        string exp;
        //evaluates the string representation of each clause, if the expression is empty then it is unsatisfiable
        while (retVal == 0 && index < boolExp.Count) {
            exp = "";
            foreach (string clause in boolExp[index]) {
                exp += clause;
            }

            if (exp.Equals("()")) {
                retVal = -1;
            }
            index++;
        }

        //If it is not satisfiable or unsolvable it must be undecided
        return retVal;
    }


    //code that generates the variable priority queue
    //modifies the priority value so it sorts high to low
    private int findVariables(List<string> literals) {
        Dictionary<string, int> numbVars = new Dictionary<string, int>();
        int count = 0;
        foreach (string literal in literals) {
            if (!numbVars.ContainsKey(literal[literal.Length - 1].ToString())) {
                numbVars.Add(literal[literal.Length - 1].ToString(), 1);
                count++;
            }
        }

        return count;
    }
}
