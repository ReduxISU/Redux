using API.Interfaces;
using System;
using System.Collections.Generic;

namespace API.Problems.NPComplete.NPC_DEUTSCHJOZSA.Solvers;
class DeutschJozsaClassicalSolver : ISolver<DEUTSCHJOZSA> {

    // --- Fields ---
    public string solverName {get;} = "Deutsch Jozsa Problem - Classical Solver";
    public string solverDefinition { get; } = "This solver classically solves the Deutsch-Jozsa problem by querying the oracle up to (n/2) + 1 times to determine if the function is constant or balanced.";
    public string source { get; } = "Deutsch, David and Jozsa, Richard. 1992. Rapid solution of problems by quantum computation. Proc. R. Soc. Lond. A439553-558";
    public string[] contributors {get;} = { "George Lake", "Eric Hill", "Paul Gilbreath", "Max Gruenwoldt", "Alex Svancara" };

    // --- Methods Including Constructors ---
    public DeutschJozsaClassicalSolver() {}

    public string solve(DEUTSCHJOZSA problem){
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

        // get output for the first input
        int first_value = oracle[0];

        // determine total number of queries
        int total_inputs = (int)Math.Pow(2, problem.n);
        int queries_to_be_certain = (total_inputs / 2) + 1;

        // Check DJ promise
        if (!(oracle.All(v => v == 0) || oracle.All(v => v == 1) || oracle.Count(v => v == 0) * 2 == oracle.Count))
            return "Breaks DJ Promise";

        // check the inputs
        for (int i = 1; i <queries_to_be_certain; i++)
        {
            // query the oracle
            int current_value = oracle[i];

            // check if different than first
            if (current_value != first_value)
            {
                return "balanced";
            }
        }

        // if we reach this point, all the inputs were the same
        return "constant";
    }
}
