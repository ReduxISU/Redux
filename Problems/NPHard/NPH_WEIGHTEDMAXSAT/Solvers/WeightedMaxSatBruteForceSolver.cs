using API.Interfaces;
using API.Interfaces.Steps;
using static API.Interfaces.Graphs.GroupedGraphFrames;
using static API.Interfaces.Logic.CnfPieces;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Solvers;

class WeightedMaxSatBruteForceSolver : ISolver<WEIGHTEDMAXSAT, Assignment> {
    // --- Fields ---
    public string solverName { get; } = "Weighted Max-SAT Exact Brute Force";
    public string solverDefinition { get; } = "Enumerates every truth assignment and returns one with maximum satisfied clause weight. This exact baseline guarantees an optimal solution.";
    public string source { get; } = "Exhaustive search.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.BruteForce;
    public MathematicalFunction timeComplexity { get; } = new("2^n * |phi|", ", n = variables, |Φ| = weighted formula size");
    public MathematicalFunction spaceComplexity { get; } = new("n", ", n = variables");
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;

    // --- Methods Including Constructors ---
    public WeightedMaxSatBruteForceSolver() { }

    // How the steps show a candidate: its assignment, with the focus on the clauses it leaves unsatisfied.
    private static RunSteps<(WEIGHTEDMAXSAT Problem, Dictionary<string, bool> Assignment), Assignment> NewSteps(StepRecorder<Assignment> rec) => new(rec,
        partial: s => ToAssignment(s.Assignment),
        focus: s => Unsatisfied(s.Problem, s.Assignment).Select(ClauseId).ToArray());

    public string solve(WEIGHTEDMAXSAT problem) => Solve(problem, StepRecorder<Assignment>.Off);

    public string Solve(WEIGHTEDMAXSAT problem, StepRecorder<Assignment> rec) {
        var steps = NewSteps(rec);
        var result = Optimize(problem, () => timerHasExpired, (assignment, value, best) => {
            if (best < 0)
                steps.Accept((problem, assignment), $"Try {Assigned(assignment)}: it satisfies weight {value}, the first to compare against.");
            else if (value > best)
                steps.Accept((problem, assignment), $"Try {Assigned(assignment)}: it satisfies weight {value}, more than the best so far, {best}.");
            else
                steps.Reject((problem, assignment), $"Try {Assigned(assignment)}: it satisfies weight {value}, not more than the best, {best}.");
        });
        if (timerHasExpired) {
            steps.Done(new Assignment([]), false, $"{SolverTimeoutCaption}");
            return SolveRun.TimedOutAnswer;
        }
        steps.Done(ToAssignment(result.Assignment), true,
            $"The best assignment is {Assigned(result.Assignment)}, satisfying weight {result.Value} of {problem.weightedClauses.Sum(w => w.Weight)}.");
        return FormatCertificate(problem.variables, result.Assignment, result.Value);
    }

    // The clauses (0-based) the assignment leaves with no true literal: the weight it gives up.
    private static IEnumerable<int> Unsatisfied(WEIGHTEDMAXSAT problem, IReadOnlyDictionary<string, bool> assignment) =>
        Enumerable.Range(0, problem.weightedClauses.Count)
            .Where(c => !problem.weightedClauses[c].Clause.Literals.Any(l => assignment[l.Variable] != l.Negated));

    internal static string FormatCertificate(
        IReadOnlyList<string> variables, IReadOnlyDictionary<string, bool> assignment, long value) =>
        "((" + string.Join(",", variables.Select(v => $"{v}:{assignment[v]}")) + $"),{value})";

    internal static (Dictionary<string, bool> Assignment, long Value) Optimize(
        WEIGHTEDMAXSAT problem, Func<bool>? expired = null,
        Action<Dictionary<string, bool>, long, long>? onCandidate = null) {
        WeightedMaxSatExactSearchLimit.ThrowIfExceeded(problem.variables.Count);
        var assignment = new Dictionary<string, bool>(StringComparer.Ordinal);
        var best = new Dictionary<string, bool>(StringComparer.Ordinal);
        long bestValue = -1;

        void Search(int index) {
            if (expired?.Invoke() == true) return;
            if (index == problem.variables.Count) {
                long value = problem.Value(assignment);
                // Tells the caller each whole candidate, with its value and the best before it (-1 before the first).
                onCandidate?.Invoke(assignment, value, bestValue);
                if (value > bestValue) {
                    bestValue = value;
                    best = new Dictionary<string, bool>(assignment, StringComparer.Ordinal);
                }
                return;
            }

            string variable = problem.variables[index];
            assignment[variable] = false;
            Search(index + 1);
            assignment[variable] = true;
            Search(index + 1);
            assignment.Remove(variable);
        }

        Search(0);
        return (best, bestValue);
    }
}
