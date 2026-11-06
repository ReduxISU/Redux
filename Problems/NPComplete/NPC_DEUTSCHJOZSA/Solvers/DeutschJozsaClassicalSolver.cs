using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;
using System;
using System.Collections.Generic;

namespace API.Problems.NPComplete.NPC_DEUTSCHJOZSA.Solvers;

// The steps Deutsch-Jozsa Classical records, for the Quantum Circuit picture (which this solver never runs, so only its
// oracle is drawn as the focus):
//   - a Reject when f breaks the promise (neither constant nor balanced): there is no answer to find;
//   - otherwise an Update per oracle call whose value matches the first: asking the oracle has no verdict;
//   - an Accept with the answer: balanced at the first call that differs, or constant once more than half agree;
//   - the Done, with the verifier's verdict.
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

    // How the steps show the run: the input the step calls the oracle with (null: a step that calls nothing), and the
    // answer once there is one.
    private static RunSteps<(int? Call, string? Answer), QuantumProgress> NewSteps(StepRecorder<QuantumProgress> rec,
        List<OracleQuery> queries, string? oracle) => new(rec,
        // What every picture shows: the oracle calls made so far, and the answer.
        partial: s => new QuantumProgress(queries: queries, result: s.Answer),
        // The oracle gate, for a step that calls it.
        focus: s => s.Call != null && oracle != null ? [oracle] : []);

    public string solve(DEUTSCHJOZSA problem) => Solve(problem, StepRecorder<QuantumProgress>.Off);

    // f is promised to be constant or balanced, so calling it on one more than half of its inputs settles which: the first
    // value that differs from f's first value proves balanced, and if more than half agree f cannot be balanced.
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
        int n = problem.n;
        // The oracle gate is looked up only for the steps: a plain solve never builds the circuit.
        string? gate = QuantumSteps.Recording(rec) ? QuantumCircuits.DeutschJozsa(oracle).OracleGateId : null;
        var queries = new List<OracleQuery>();
        var steps = NewSteps(rec, queries, gate);

        // get output for the first input
        int first_value = oracle[0];

        // determine total number of queries
        int total_inputs = (int)Math.Pow(2, problem.n);
        int queries_to_be_certain = (total_inputs / 2) + 1;

        string answer;
        // Check DJ promise
        if (!(oracle.All(v => v == 0) || oracle.All(v => v == 1) || oracle.Count(v => v == 0) * 2 == oracle.Count)) {
            steps.Reject((null, null), $"f gives 1 for {oracle.Count(v => v == 1)} of its {QuantumCircuits.Count(oracle.Count)} inputs: not none, not all and not exactly half. It breaks the promise, so there is no answer to find.");
            answer = "Breaks DJ Promise";
        } else {
            queries.Add(new OracleQuery(0, first_value));
            steps.Update((0, null), $"Call the oracle with x = {QuantumSteps.Input(0, n)}: f({QuantumSteps.Input(0, n)}) = {first_value}.");

            // check the inputs, until one differs from the first
            bool balanced = false;
            for (int i = 1; i < queries_to_be_certain && !balanced; i++) {
                if (timerHasExpired) return SolveRun.TimedOutAnswer;
                // query the oracle
                int current_value = oracle[i];
                queries.Add(new OracleQuery(i, current_value));

                // check if different than first
                balanced = current_value != first_value;
                if (balanced)
                    steps.Accept((i, "balanced"), $"Call the oracle with x = {QuantumSteps.Input(i, n)}: f({QuantumSteps.Input(i, n)}) = {current_value}, unlike f({QuantumSteps.Input(0, n)}) = {first_value}. f is not constant, so it is balanced, after {i + 1} calls.");
                else
                    steps.Update((i, null), $"Call the oracle with x = {QuantumSteps.Input(i, n)}: f({QuantumSteps.Input(i, n)}) = {current_value}, the same as f({QuantumSteps.Input(0, n)}).");
            }

            // if no input differed, all the inputs were the same
            answer = balanced ? "balanced" : "constant";
            if (!balanced && total_inputs == 1)
                steps.Accept((null, answer), $"f has a single input, so it is constant.");
            else if (!balanced)
                steps.Accept((null, answer), $"The first {queries_to_be_certain} of {QuantumCircuits.Count(total_inputs)} inputs all give {first_value}: more than half, so f cannot be balanced. It is constant. That took {queries_to_be_certain} oracle calls; the Deutsch-Jozsa algorithm needs 1.");
        }

        // The verdict is only for the steps: a plain solve never runs the verifier.
        if (QuantumSteps.Recording(rec)) {
            var final = new QuantumProgress(queries: queries, result: answer);
            if (QuantumSteps.Verdict(() => problem.defaultVerifier.verify(problem, answer)))
                steps.Done(final, true, $"f is {answer}.");
            else if (answer == "Breaks DJ Promise")
                steps.Done(final, false, $"No answer: f breaks the promise.");
            else
                steps.Done(final, false, $"The answer {answer} is not correct.");
        }
        return answer;
    }
}
