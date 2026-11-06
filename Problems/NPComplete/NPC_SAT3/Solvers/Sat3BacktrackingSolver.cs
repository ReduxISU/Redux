using API.Interfaces;
using API.Interfaces.Steps;

using API.Problems.NPComplete.NPC_SAT3;
using static API.Interfaces.Graphs.GroupedGraphFrames;

namespace API.Problems.NPComplete.NPC_SAT3.Solvers;

// The search records a step for each value it tries (a Try, then an Accept when the branch is queued or satisfies every
// clause, or a Reject when a clause is left with no true literal), a Backtrack when it takes a queued branch that is not one
// it just made, and a Done with the answer. The SearchTree builds the Search Tree picture's nodes from those steps.
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



    public string solve(SAT3 sat3) => Solve(sat3, StepRecorder<Assignment>.Off);

    public string Solve(SAT3 sat3, StepRecorder<Assignment> rec) {
        var tree = NewTree(sat3, rec);
        Dictionary<string, bool>? solution = Search(sat3, tree);

        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        if (solution == null) {
            tree.Done(new Assignment([]), false, $"Every branch was rejected, so there is no solution.");
            return "No Solution";
        }

        var decided = new Dictionary<string, bool>(solution);
        // fill in any variables the backtracker left unassigned
        foreach (string literal in sat3.literals) {
            string varName = literal.TrimStart('!');
            if (!solution.ContainsKey(varName)) {
                // value doesn't matter, choose false
                solution[varName] = false;
            }
        }

        string solutionString = "(";
        foreach (KeyValuePair<string, bool> kvp in solution) {
            solutionString = solutionString + kvp.Key + ":" + kvp.Value.ToString() + ",";
        }
        solutionString = solutionString.TrimEnd(',');
        solutionString += ")";
        // The variables the search never had to set are free: the answer sets them to False.
        tree.Done(ToAssignment(solution), true, $"Every clause has a true literal under {Assigned(decided)}.{FreeText(FreeVariables(sat3, decided))}");
        return solutionString;
    }

    // Return type varies
    public Dictionary<string, bool>? findSolution(SAT3 sat3) => findSolution(sat3, StepRecorder<Assignment>.Off);

    public Dictionary<string, bool>? findSolution(SAT3 sat3, StepRecorder<Assignment> rec) => Search(sat3, NewTree(sat3, rec));

    // A best-first backtracking search: each state decides its next variable both ways, a value that leaves a clause with
    // no true literal is pruned, and the most promising state still queued is expanded next, until every clause has a true
    // literal.
    private Dictionary<string, bool>? Search(SAT3 sat3, SearchTree<SAT3PQObject, bool, Assignment> tree) {
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
        tree.Root(curSat, $"start");

        while (!solutionFound && satPQ.Count > 0) {
            if (timerHasExpired) return null; // stop at the time limit; Solve reports the stop
            curSat = satPQ.Dequeue();
            tree.Expand(curSat, $"{(tree.DeadEnd ? "Neither value of the last variable works. " : "")}Go back to {Assigned(curSat.varStates)}, the most promising branch still open: {curSat.nextVar} is next.");
            var (trueChild, falseChild) = curSat.createSATChildPair(curSat.depth, totalNumberOfVariables);
            foreach (var (value, childSAT) in new[] { (true, trueChild), (false, falseChild) }) {
                tree.Try(curSat, value, $"Try {curSat.nextVar} = {value}{After(curSat)}.");
                //Invalid statements are evaluated upon creation and returned as null
                if (childSAT != null) {
                    eval = evaluateBooleanExpression(childSAT.SATState.clauses);
                    if (eval == 0) {
                        //undecided
                        satPQ.Enqueue(childSAT, childSAT.getPQWeight());
                        tree.Accept(curSat, value, $"Keep {curSat.nextVar} = {value}: no clause has every literal false yet, so this branch is queued.", queued: childSAT);
                    } else if (eval == 1) {
                        //satisfiable
                        solutionFound = true;
                        solution = childSAT.varStates;
                        tree.Accept(curSat, value, $"Keep {curSat.nextVar} = {value}: every clause now has a true literal.", solved: childSAT);
                        break; //SOLUTION FOUND EXIT FOR EACH
                    }
                } else {
                    //unsolvable therefore dont add
                    tree.Reject(curSat, value, $"{curSat.nextVar} = {value} fails: {WhyFails(sat3, curSat, value)}");
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

    // --- What the steps say ---

    // How the steps show a state: its assignment, the variable it decides, and its node in the Search Tree picture ("x2=F",
    // with how many clauses are left once queued, the clause a value falsifies, or the variables the solution never set).
    private static SearchTree<SAT3PQObject, bool, Assignment> NewTree(SAT3 sat3, StepRecorder<Assignment> rec) => new(rec,
        partial: s => ToAssignment(s.varStates),
        focus: VariableFocus,
        childPartial: Partial,
        childLabel: (s, value) => $"{s.nextVar}={(value ? "T" : "F")}",
        rejectFocus: (s, value) => FailFocus(sat3, s, value),
        rejectNote: (s, value) => FailNote(sat3, s, value),
        queuedNote: ClausesLeft,
        solvedNote: s => FreeNote(FreeVariables(sat3, s.varStates)));

    // The assignment so far with the value being tried added.
    private static Assignment Partial(SAT3PQObject state, bool value) =>
        ToAssignment(new Dictionary<string, bool>(state.varStates) { [state.nextVar] = value });

    private static Assignment ToAssignment(Dictionary<string, bool> values) =>
        new(values.Select(p => new KeyValuePair<string, string>(p.Key, p.Value.ToString())));

    private static string[] VariableFocus(SAT3PQObject state) => [VariableId(state.nextVar)];

    // " after x1 = True, x2 = False", or nothing for the first variable.
    private static string After(SAT3PQObject state) => state.varStates.Count == 0 ? "" : $" after {Assigned(state.varStates)}";

    private static string ClausesLeft(SAT3PQObject queued) {
        int left = queued.SATState.clauses.Count;
        return $"{left} {(left == 1 ? "clause" : "clauses")} left";
    }

    // The first clause with every literal false once the state's variable takes the value, or -1.
    private static int FalsifiedClause(SAT3 sat3, SAT3PQObject state, bool value) =>
        FirstFalsifiedClause(sat3.clauses, new Dictionary<string, bool>(state.varStates) { [state.nextVar] = value });

    private static string WhyFails(SAT3 sat3, SAT3PQObject state, bool value) => FalsifiedClause(sat3, state, value) is int c and >= 0
        ? $"clause C{c + 1} {ClauseText(sat3.clauses[c])} has no true literal left."
        : "a clause is left with no true literal.";

    private static string[] FailFocus(SAT3 sat3, SAT3PQObject state, bool value) => FalsifiedClause(sat3, state, value) is int c and >= 0
        ? [VariableId(state.nextVar), ClauseId(c)]
        : VariableFocus(state);

    private static string? FailNote(SAT3 sat3, SAT3PQObject state, bool value) =>
        FalsifiedClause(sat3, state, value) is int c and >= 0 ? $"C{c + 1} false" : null;

    // The variables of the formula the search never had to set, in the order they first appear.
    private static List<string> FreeVariables(SAT3 sat3, IReadOnlyDictionary<string, bool> decided) =>
        sat3.literals.Select(l => l.TrimStart('!')).Distinct().Where(v => !decided.ContainsKey(v)).ToList();

    // What the Done says about the free variables: either value works, and the answer sets them to False.
    private static string FreeText(List<string> free) => free.Count switch {
        0 => "",
        1 => $" {free[0]} is free (either value works), so the answer sets it to False.",
        _ => $" {string.Join(", ", free.SkipLast(1))} and {free[^1]} are free (either value works), so the answer sets them to False.",
    };

    // The solution node's note: the variables it never had to set ("x3 free"), or how many when there are more than two.
    private static string? FreeNote(List<string> free) => free.Count switch {
        0 => null,
        <= 2 => $"{string.Join(", ", free)} free",
        _ => $"{free.Count} free",
    };
}
