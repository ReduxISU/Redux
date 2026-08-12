using API.Interfaces;
using API.Problems.P.P_NFA;
using System;
using System.Text;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text.RegularExpressions;

namespace API.Problems.P.P_NFA.Verifiers;

class NFAVerifier : IVerifier<NFA> {
        public string verifierName { get; } = "NFA Verifier";
        public string verifierDefinition { get; } =
            "Verifies one (or many) NFA run certificates against the input string, including ε-transitions, matching solver semantics.";
        public string source { get; } = "";
        public string[] contributors { get; } = { "Michael Trosper" };

        private string _certificate = "";
        public string certificate => _certificate;

        private const char EPS = '\u03B5'; // U+03B5 (ε), matches NFAEdge normalization

        public NFAVerifier() { }

        public bool verify(NFA problem, string certificate) {
                // Normalize empty-input representation "ε"
                string rawInput = problem.inputString ?? "";
                string input = rawInput == "ε" ? "" : rawInput;
                string result = "";

                // Validate characters
                foreach (char c in input) {
                        if (!problem.alphabet.Contains(c))
                                return false; // Input contains character not in NFA alphabet
                }

                var edges = problem.edges; // List<NFAEdge>
                var acceptPaths = new List<List<string>>();

                // DFS exploring nondeterministic runs; visitedPerPath prevents infinite loops for epsilon cycles
                void DFS(string state, int pos, List<string> path, HashSet<(string, int)> visitedPerPath) {
                        // If consumed all input and in accept state, record a copy of the path
                        if (pos >= input.Length && problem.acceptStates.Contains(state)) {
                                acceptPaths.Add(new List<string>(path));
                                // Do not return: still allow further epsilon transitions that may produce other accept runs
                        }

                        // Explore epsilon transitions (do not advance position)
                        foreach (var e in edges.Where(x => x.From == state && x.Symbol == 'ε')) {
                                var key = (e.To, pos);
                                if (visitedPerPath.Contains(key)) continue;
                                visitedPerPath.Add(key);
                                path.Add(e.To);
                                DFS(e.To, pos, path, visitedPerPath);
                                path.RemoveAt(path.Count - 1);
                                visitedPerPath.Remove(key);
                        }

                        // Explore regular symbol transitions (advance position)
                        if (pos < input.Length) {
                                char need = input[pos];
                                foreach (var e in edges.Where(x => x.From == state && x.Symbol == need)) {
                                        var key = (e.To, pos + 1);
                                        if (visitedPerPath.Contains(key)) continue;
                                        visitedPerPath.Add(key);
                                        path.Add(e.To);
                                        DFS(e.To, pos + 1, path, visitedPerPath);
                                        path.RemoveAt(path.Count - 1);
                                        visitedPerPath.Remove(key);
                                }
                        }
                }


                // Seed DFS with start state
                var startPath = new List<string> { problem.startState };
                var startVisited = new HashSet<(string, int)> { (problem.startState, 0) };
                DFS(problem.startState, 0, startPath, startVisited);

                // Build output
                if (acceptPaths.Count == 0) {
                        return false; // No Solution Exists: No run accepts the input
                }

                var sb = new StringBuilder();
                foreach (var p in acceptPaths) {
                        sb.AppendLine(string.Join(", ", p) + "\r\n");
                }

                result = sb.ToString().TrimEnd();

                string[] sequences = result.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                bool hasPath = false;
                foreach (string seq in sequences) {
                        if (seq.Replace(" ", "").Trim() == certificate.Replace(" ", "").Trim()) {
                                hasPath = true;
                                break;
                        }
                }

                return hasPath;
        }
}