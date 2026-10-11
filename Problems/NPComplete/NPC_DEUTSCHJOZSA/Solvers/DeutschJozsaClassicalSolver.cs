using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;
using System;
using System.Collections.Generic;

namespace API.Problems.NPComplete.NPC_DEUTSCHJOZSA.Solvers;

// Incremental: each oracle call adds to what the solver knows about f, and none is taken back.
class DeutschJozsaClassicalSolver : ISolver<DEUTSCHJOZSA, QuantumProgress>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "Deutsch-Jozsa Classical";
    public string solverDefinition { get; } = "This solver classically solves the Deutsch-Jozsa problem by querying the oracle up to (n/2) + 1 times to determine if the function is constant or balanced.";
    public string source { get; } = "Deutsch, David and Jozsa, Richard. 1992. Rapid solution of problems by quantum computation. Proc. R. Soc. Lond. A439553-558";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "George Lake", "Eric Hill", "Paul Gilbreath", "Max Grünwoldt", "Alex Svancara" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Classical baseline paired with DeutschJozsaQuantumSolver; no SolverType
    // bucket fits so solverType stays Unclassified per user decision. Natural parameter is
    // oracle-bit-count n (matches the paired quantum solver), not the 2^n-length array scanned --
    // classically needs exponentially many oracle queries in n, the entire pedagogical point of this
    // problem.
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // solve() queries the oracle array up to (2^n / 2) + 1 times (queries_to_be_certain,
    // computed from total_inputs = 2^n) before it can be certain of constant vs. balanced.
    public MathematicalFunction timeComplexity { get; } = new("2^n", " queries");

    // --- Methods Including Constructors ---
    public DeutschJozsaClassicalSolver() { }

    public string solve(DEUTSCHJOZSA problem) => Solve(problem, StepRecorder<QuantumProgress>.Off);

    // Steps (the Quantum Circuit picture, which this solver never runs): a Reject when f breaks the promise; otherwise a Try
    // per oracle call with the oracle as the focus, an Accept for the call that settles it (or once more than half the
    // inputs agree), and Done with the verifier's verdict.
    public string Solve(DEUTSCHJOZSA problem, StepRecorder<QuantumProgress> rec) {
        //
        // Solves the Deutsch-Jozsa problem by classically querying the oracle.
        // Queries up to (n/2) + 1 times.
        //
        // Input: Problem
        // {(n, S) | n is int, S is list}")
        //
        // Output - string
        // ("Balanced" or "Constant")
        //
        // NOTE: This solver returns "Breaks DJ Promise" in the event that the input does.
        // Technically this breaks the idea of the input being a black box.
        // I kept the idea for education purposes

        List<int> oracle = problem.w;
        string? gate = QuantumSteps.Recording(rec) ? QuantumCircuits.DeutschJozsa(oracle).OracleGateId : null;
        string[] Focus() => gate == null ? [] : [gate];
        string In(int x) => QuantumSteps.Input(x, problem.n);
        var queries = new List<OracleQuery>();
        string Finish(string answer) {
            QuantumSteps.Done(rec, () => problem.defaultVerifier.verify(problem, answer), new QuantumProgress(queries: queries, result: answer),
                ok => ok ? $"f is {answer}." : answer == "Breaks DJ Promise" ? "No answer: f breaks the promise." : $"The answer {answer} is not correct.");
            return answer;
        }

        // get output for the first input
        int first_value = oracle[0];

        // determine total number of queries
        int total_inputs = (int)Math.Pow(2, problem.n);
        int queries_to_be_certain = (total_inputs / 2) + 1;

        // Check DJ promise
        if (!(oracle.All(v => v == 0) || oracle.All(v => v == 1) || oracle.Count(v => v == 0) * 2 == oracle.Count)) {
            rec.Reject(() => new QuantumProgress(), () => $"f gives 1 for {oracle.Count(v => v == 1)} of its {QuantumCircuits.Count(oracle.Count)} inputs: "
                + "not none, not all and not exactly half. It breaks the promise, so there is no answer to find.");
            return Finish("Breaks DJ Promise");
        }

        queries.Add(new OracleQuery(0, first_value));
        rec.Try(() => new QuantumProgress(queries: queries), () => $"Call the oracle with x = {In(0)}: f({In(0)}) = {first_value}.", Focus);

        // check the inputs
        for (int i = 1; i < queries_to_be_certain; i++) {
            // query the oracle
            int current_value = oracle[i];
            queries.Add(new OracleQuery(i, current_value));
            int x = i;

            // check if different than first
            if (current_value != first_value) {
                rec.Accept(() => new QuantumProgress(queries: queries, result: "balanced"),
                    () => $"Call the oracle with x = {In(x)}: f({In(x)}) = {current_value}, unlike f({In(0)}) = {first_value}. f is not constant, so it is balanced, after {x + 1} calls.",
                    Focus);
                return Finish("balanced");
            }
            rec.Try(() => new QuantumProgress(queries: queries), () => $"Call the oracle with x = {In(x)}: f({In(x)}) = {current_value}, the same as f({In(0)}).", Focus);
        }

        // if we reach this point, all the inputs were the same
        rec.Accept(() => new QuantumProgress(queries: queries, result: "constant"), () => total_inputs == 1
            ? "f has a single input, so it is constant."
            : $"The first {queries_to_be_certain} of {QuantumCircuits.Count(total_inputs)} inputs all give {first_value}: more than half, so f cannot be balanced. It is constant. "
                + $"That took {queries_to_be_certain} oracle calls; the Deutsch-Jozsa algorithm needs 1.");
        return Finish("constant");
    }
}
