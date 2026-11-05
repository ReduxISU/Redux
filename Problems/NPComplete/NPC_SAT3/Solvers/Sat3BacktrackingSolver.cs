using API.Interfaces;
using API.Interfaces.Steps;

using API.Problems.NPComplete.NPC_SAT3;


namespace API.Problems.NPComplete.NPC_SAT3.Solvers;

partial class Sat3BacktrackingSolver : ISolver<SAT3, Assignment>, IIncrementalSolver {

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
        var steps = new Steps(sat3, rec);
        Dictionary<string, bool>? solution = Search(sat3, steps);

        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        if (solution == null) {
            steps.NoSolution();
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
        steps.Solved(decided, solution);
        return solutionString;
    }

    // Return type varies
    public Dictionary<string, bool>? findSolution(SAT3 sat3) => findSolution(sat3, StepRecorder<Assignment>.Off);

    public Dictionary<string, bool>? findSolution(SAT3 sat3, StepRecorder<Assignment> rec) => Search(sat3, new Steps(sat3, rec));

    // A best-first backtracking search: each state decides its next variable both ways, a value that leaves a clause with
    // no true literal is pruned, and the most promising state still queued is expanded next, until every clause has a true
    // literal.
    private Dictionary<string, bool>? Search(SAT3 sat3, Steps steps) {
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
        steps.Root(curSat);

        while (!solutionFound && satPQ.Count > 0) {
            if (timerHasExpired) return null; // stop at the time limit; Solve reports the stop
            curSat = satPQ.Dequeue();
            steps.Expanding(curSat);
            var (trueChild, falseChild) = curSat.createSATChildPair(curSat.depth, totalNumberOfVariables);
            foreach (var (value, childSAT) in new[] { (true, trueChild), (false, falseChild) }) {
                steps.Tried(curSat, value);
                //Invalid statements are evaluated upon creation and returned as null
                if (childSAT != null) {
                    eval = evaluateBooleanExpression(childSAT.SATState.clauses);
                    if (eval == 0) {
                        //undecided
                        satPQ.Enqueue(childSAT, childSAT.getPQWeight());
                        steps.Queued(curSat, value, childSAT);
                    } else if (eval == 1) {
                        //satisfiable
                        solutionFound = true;
                        solution = childSAT.varStates;
                        steps.Satisfied(curSat, value, childSAT);
                        break; //SOLUTION FOUND EXIT FOR EACH
                    }
                } else {
                    //unsolvable therefore dont add
                    steps.Pruned(curSat, value);
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
