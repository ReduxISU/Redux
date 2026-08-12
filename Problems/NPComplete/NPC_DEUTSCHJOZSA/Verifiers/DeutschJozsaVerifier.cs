using API.Interfaces;
using API.Problems.NPComplete.NPC_DEUTSCHJOZSA.Solvers;

namespace API.Problems.NPComplete.NPC_DEUTSCHJOZSA.Verifiers;

class DeutschJozsaVerifier : IVerifier<DEUTSCHJOZSA> {

        // --- Fields ---
        public string verifierName { get; } = "Deutsch Jozsa Verifier";
        public string verifierDefinition { get; } = "This verifier uses the classical solver to verify the solution to the Deutsch-Jozsa problem.";
        public string source { get; } = "Deutsch, David and Jozsa, Richard. 1992. Rapid solution of problems by quantum computation. Proc. R. Soc. Lond. A439553-558";
        public string[] contributors { get; } = { "Jason L. Wright", "Eric Hill", "Paul Gilbreath", "Max Gruenwoldt", "Alex Svancara" };
        private string _certificate = "";

        public string certificate {
                get {
                        return _certificate;
                }
        }

        // --- Methods Including Constructors ---
        public DeutschJozsaVerifier() {
        }

        public bool verify(DEUTSCHJOZSA problem, string certificate) {
                var solver = new DeutschJozsaClassicalSolver();
                return solver.solve(problem) == certificate && (certificate is "constant" or "balanced");
        }
}
