using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

using API.Problems.NPComplete.NPC_SAT3;


namespace API.Problems.NPComplete.NPC_SAT3.Solvers;

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

    // Steps: for each value of the variable being decided, a Try, then an Accept (no clause is falsified: the branch is
    // queued) or a Reject (a clause has no true literal left). The search takes the most promising queued branch next; when
    // that is not a branch it just made, a Backtrack says where it went back to. Recorder state lives only in this call.
    // Each step also names its node of the search tree (SolverStep.Tree, for the Search Tree picture): the root "start" is
    // queued, each value tried is a node ("x2=F") under the state it decides, queued (with its priority), pruned (with the
    // clause it makes false) or the solution (with the variables it never had to set, "x3 free"), and a Backtrack takes the
    // node it goes back to off the queue. The tree stops where every clause has a true literal, so the Done says which
    // variables are free and that the answer sets them to False (#899).
    public string Solve(SAT3 sat3, StepRecorder<Assignment> rec) {
        Dictionary<string, bool>? solution = Search(sat3, rec, out SearchNode[]? answerPath);

        if (solution == null) {
            rec.Done(new Assignment([]), false, "Every branch was rejected, so there is no solution.");
            return "No Solution";
        }

        var decided = new Dictionary<string, bool>(solution);
        List<string> free = FreeVariables(sat3, decided);
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
        // The answer's path in the search tree, root first: past the cap, its ancestors' steps may not have been kept.
        rec.Done(ToAssignment(solution), true, $"Every clause has a true literal under {GroupedGraphFrames.Assigned(decided)}.{FreeText(free)}",
            answerPath is null ? null : () => answerPath);
        return solutionString;
    }

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

    private static Assignment ToAssignment(Dictionary<string, bool> values) =>
        new(values.Select(p => new KeyValuePair<string, string>(p.Key, p.Value.ToString())));

    // Return type varies
    public Dictionary<string, bool>? findSolution(SAT3 sat3) => findSolution(sat3, StepRecorder<Assignment>.Off);

    public Dictionary<string, bool>? findSolution(SAT3 sat3, StepRecorder<Assignment> rec) => Search(sat3, rec, out _);

    // The search; answerPath is the solution's nodes in the search tree, root first (null when there is no solution).
    private Dictionary<string, bool>? Search(SAT3 sat3, StepRecorder<Assignment> rec, out SearchNode[]? answerPath) {
        answerPath = null;
        ////O(n!)
        // while(!solutionFound && !satQueue.isEmpty()):
        // 	var = varQueue.pop() //O(1)
        // 	if(var != null):
        // 		//O(1+n*2)
        // 		//create new satNode with var.truthVal = true, evaluate the statement and update the neccisary variables then if the solution is still valid push it to satQueue
        // 		//create new satNode with var.truthVal = false, evaluate the statement and update the neccisary variables then if the solution is still valid push it to satQueue
        // 		//prioritize evaluating depth over breadth
        // 		//when evaluating statements if any evaluate to true set solution equal to the satNode and set solutionFound to True (exiting the loop)
        // 		//a potential pruning function would be to prioritize any variable that is alone in a statement and evaluating that to its required value (ex (y) must evaluate to true)
        // 		//to resolve cases where there are two contradicting statements ex (y), (!y) always choose the first statment to satisfy before evaluating the whole expression
        // 		//this pruning function would attempt to imediatly evaluate the first standalone expression as the next node (after current processing is done)

        //CATCHES INVALID INPUTS
        // A single literal such as (x1) is still a satisfiable formula (#623), so only an
        // empty formula is rejected here.
        if (sat3.literals.Count < 1) {
            // Console.WriteLine("No literals provided");
            return null;
        }

        bool solutionFound = false;
        PriorityQueue<SAT3PQObject, int> satPQ = new PriorityQueue<SAT3PQObject, int>();
        Dictionary<string, bool>? solution = null;

        int totalNumberOfVariables = findVariables(sat3.literals);


        // string var;
        SAT3PQObject curSat;
        int eval;

        //add initial SAT3 to PQ
        curSat = new SAT3PQObject(sat3, 0, totalNumberOfVariables);
        // Console.WriteLine("curSat's nextVar after init : " + curSat.nextVar);
        curSat.initVarWeights();
        curSat.removeDuplicatesFromClauses();
        satPQ.Enqueue(curSat, curSat.getPQWeight());

        //O(n!)
        bool firstState = true;
        var justQueued = new List<SAT3PQObject>();   // the children of the last state that were queued (read only to word a Backtrack)
        // The search tree (read only to record steps): each state's node id, parent and label. The root is node 0, "start";
        // each value tried makes a node, numbered in the order tried.
        var nodes = new Dictionary<SAT3PQObject, SearchNode>(ReferenceEqualityComparer.Instance) {
            [curSat] = new SearchNode(0, null, "start", SearchNodeState.Queued),
        };
        int nextNode = 1;
        var byId = new Dictionary<int, SearchNode> { [0] = nodes[curSat] };   // the root and every queued node, to walk up from the answer
        while (!solutionFound && satPQ.Count > 0) {
            curSat = satPQ.Dequeue();
            // Console.WriteLine("curSat's nextVar is : " + curSat.nextVar);
            // The search took a branch it did not make a moment ago: say where it went back to.
            if (!firstState && !justQueued.Contains(curSat)) {
                SAT3PQObject backTo = curSat;
                bool deadEnd = justQueued.Count == 0;
                SearchNode backNode = nodes[backTo] with { State = SearchNodeState.Explored, Bound = null };
                rec.Backtrack(() => ToAssignment(backTo.varStates),
                    () => (deadEnd ? "Neither value of the last variable works. " : "")
                        + $"Go back to {GroupedGraphFrames.Assigned(backTo.varStates)}, the most promising branch still open: {backTo.nextVar} is next.",
                    () => [GroupedGraphFrames.VariableId(backTo.nextVar)],
                    () => [backNode]);
            }
            firstState = false;
            justQueued.Clear();
            var (trueChild, falseChild) = curSat.createSATChildPair(curSat.depth, totalNumberOfVariables); //ADD VARIABLE TO INPUT
            SAT3PQObject decided = curSat;
            SearchNode parentNode = nodes[decided];
            foreach (var (value, childSAT) in new[] { (true, trueChild), (false, falseChild) }) {
                // Try the value, then say what became of it. The partial answer is the assignment with this value added.
                bool tried = value;
                Assignment Partial() => ToAssignment(new Dictionary<string, bool>(decided.varStates) { [decided.nextVar] = tried });
                string[] focus = [GroupedGraphFrames.VariableId(decided.nextVar)];
                // The tree: the node for this value, under the state being decided (the root comes with the very first Try).
                var child = new SearchNode(nextNode++, parentNode.NodeId, $"{decided.nextVar}={(tried ? "T" : "F")}", SearchNodeState.Explored);
                SearchNode[] Node(SearchNodeState state, string? bound = null) => [child with { State = state, Bound = bound }];
                bool withRoot = nextNode == 2;
                rec.Try(Partial,
                    () => decided.varStates.Count == 0
                        ? $"Try {decided.nextVar} = {tried}."
                        : $"Try {decided.nextVar} = {tried} after {GroupedGraphFrames.Assigned(decided.varStates)}.",
                    () => focus,
                    () => withRoot ? [parentNode, child] : [child]);

                //Invalid statements are evaluated upon creation and returned as null
                if (childSAT != null) {
                    eval = evaluateBooleanExpression(childSAT.SATState.clauses);
                    if (eval == 0) {
                        //undecided
                        satPQ.Enqueue(childSAT, childSAT.getPQWeight());
                        justQueued.Add(childSAT);
                        nodes[childSAT] = child with { State = SearchNodeState.Queued };
                        byId[child.NodeId] = child;
                        SAT3PQObject queued = childSAT;
                        rec.Accept(Partial, () => $"Keep {decided.nextVar} = {tried}: no clause has every literal false yet, so this branch is queued.", () => focus,
                            () => Node(SearchNodeState.Queued, $"priority {queued.getPQWeight()}"));
                    } else if (eval == 1) {
                        //satisfiable
                        //WRITE ASSIGNMENTS OF VARIABLES
                        solutionFound = true;
                        solution = childSAT.varStates;
                        var solved = child with { State = SearchNodeState.Solution, Bound = FreeNote(FreeVariables(sat3, childSAT.varStates)) };
                        var path = new List<SearchNode> { solved };
                        for (int? up = child.ParentId; up is int id; up = byId[id].ParentId)
                            path.Insert(0, byId[id] with { State = SearchNodeState.Explored, Bound = null });
                        answerPath = [.. path];
                        rec.Accept(Partial, () => $"Keep {decided.nextVar} = {tried}: every clause now has a true literal.", () => focus,
                            () => [solved]);
                        break; //SOLUTION FOUND EXIT FOR EACH
                    }
                } else {
                    //else unsolvable therefore dont add
                    rec.Reject(Partial,
                        () => {
                            int clause = FalsifiedClause(sat3, decided, tried);
                            return clause < 0
                                ? $"{decided.nextVar} = {tried} fails: a clause is left with no true literal."
                                : $"{decided.nextVar} = {tried} fails: clause C{clause + 1} {GroupedGraphFrames.ClauseText(sat3.clauses[clause])} has no true literal left.";
                        },
                        () => {
                            int clause = FalsifiedClause(sat3, decided, tried);
                            return clause < 0 ? focus : [focus[0], GroupedGraphFrames.ClauseId(clause)];
                        },
                        () => {
                            int clause = FalsifiedClause(sat3, decided, tried);
                            return Node(SearchNodeState.Pruned, clause < 0 ? null : $"C{clause + 1} false");
                        });
                }
            }
        }
        return solution;
    }

    // The first clause with every literal false once the state's variable takes the value (for the Reject steps; only called when recording).
    private static int FalsifiedClause(SAT3 sat3, SAT3PQObject state, bool value) =>
        GroupedGraphFrames.FirstFalsifiedClause(sat3.clauses, new Dictionary<string, bool>(state.varStates) { [state.nextVar] = value });

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

            // Console.WriteLine(exp);
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
