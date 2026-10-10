using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_CLIQUECOVER.Solvers;

class CliqueCoverBruteForce : ISolver<CLIQUECOVER, Groups> {

    // --- Fields ---
    public string solverName { get; } = "Clique Cover Brute Force";
    public string solverDefinition { get; } = "This is a brute force solver for the NP-Complete Clique Cover problem";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. SURPRISING: this is not a plain 2^n subset search — `binary`
    // assigns each of the n nodes a label via nextBinary's odometer, and that odometer's
    // carry check ("binary[i] != K") lets each digit range over K+1 values (0..K) before
    // carrying, so the loop runs until every node reaches label K-1, a state reached after
    // Theta((K+1)^n) steps. Each candidate costs O(n^2 * m) to verify (CliqueCoverVerifier's
    // worst case is one group holding all n nodes: O(n^2) pairs x O(m) edge lookup each).
    // With K allowed up to n (see the K > nodes.Count guard in solve()), worst case is
    // Theta((n+1)^n) -- asymptotically worse than n! (n^n / n! -> e^n), i.e. worse than the
    // existing SolverComplexityBucket.Factorial tier; Exponential is kept only because no
    // stronger bucket value exists.
    public string complexity { get; } = "O((K+1)^n * n^2 * m), n = |nodes|, m = |edges|, K = target clique count";

    // --- Methods Including Constructors ---
    public CliqueCoverBruteForce() {

    }


    private string BinaryToCertificate(List<int> binary, List<string> S, int K) {
        string certificate = "{";

        for (int j = 0; j < K; j++) {
            for (int i = 0; i < binary.Count; i++) {
                if (binary[i] == j) {
                    certificate += S[i] + ",";
                }
            }
            certificate = certificate.TrimEnd(',');
            certificate += "},{";
        }

        certificate = certificate.TrimEnd('{');

        return "{" + certificate.TrimEnd(',') + "}";

    }


    private void nextBinary(List<int> binary, int K) {
        for (int i = 0; i < binary.Count; i++) {
            if (binary[i] != K) {
                binary[i] += 1;
                return;
            } else if (binary[i] == K) {
                binary[i] = 0;
            }
        }
    }


    public string solve(CLIQUECOVER clique) => Solve(clique, StepRecorder<Groups>.Off);

    // Steps: one per candidate grouping tried, in the order the solver tries them. A node whose digit is K (the odometer's
    // extra value) is in no group in that candidate, so it is left out of the partial answer. Recorder state lives only
    // in this call.
    public string Solve(CLIQUECOVER clique, StepRecorder<Groups> rec) {

        // When this solver gives up, "{}" is its answer: the Done step says what the verifier says of it (an empty graph is
        // covered by no cliques at all).
        bool EmptyAnswerOk() {
            try { return clique.defaultVerifier.verify(clique, "{}"); } catch { return false; }
        }

        if (clique.K > clique.nodes.Count) {
            rec.Done(new Groups([]), EmptyAnswerOk(), $"K = {clique.K} is more than the {clique.nodes.Count} nodes, and this solver only tries up to one group per node.");
            return "{}";
        }

        List<int> binary = new List<int>();
        foreach (var i in clique.nodes) {
            binary.Add(0);
        }

        int tried = 0;
        Dictionary<string, int> Candidate() {
            var groups = new Dictionary<string, int>();
            for (int i = 0; i < binary.Count; i++)
                if (binary[i] < clique.K) groups[clique.nodes[i]] = binary[i];
            return groups;
        }

        while (binary.Count(n => n == (clique.K - 1)) < clique.nodes.Count) {
            string certificate = BinaryToCertificate(binary, clique.nodes, clique.K);
            tried++;
            if (clique.defaultVerifier.verify(clique, certificate)) {
                rec.Accept(() => new Groups(Candidate()), () => $"Try {GraphGroupFrames.Classes(Candidate(), clique.K)}. Every group is a clique.", Array.Empty<string>());
                rec.Done(new Groups(Candidate()), true, $"{GraphGroupFrames.Classes(Candidate(), clique.K)} covers the nodes with cliques, found after {tried} tries.");
                return certificate;
            }
            rec.Reject(() => new Groups(Candidate()),
                () => $"Try {GraphGroupFrames.Classes(Candidate(), clique.K)}: {GraphGroupFrames.Why(GroupRule.CliqueCover, clique.nodes, clique.edges, Candidate())?.Reason ?? "the verifier rejects it"}.",
                () => GraphGroupFrames.Why(GroupRule.CliqueCover, clique.nodes, clique.edges, Candidate())?.Focus ?? Array.Empty<string>());
            nextBinary(binary, clique.K);

        }

        rec.Done(new Groups([]), EmptyAnswerOk(), tried == 0
            ? "No grouping was checked."
            : $"Checked {tried} candidate groupings into at most {clique.K} groups. None is a cover by cliques.");
        return "{}";
    }
}