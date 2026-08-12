using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_PARTITION.Verifiers;

class PartitionVerifier : IVerifier<PARTITION> {

        // --- Fields ---
        public string verifierName { get; } = "Partition Verifier";
        public string verifierDefinition { get; } = "This is a verifier for the Partition problem";
        public string source { get; } = "";
        public string[] contributors { get; } = { "Andrija Sevaljevic" };

        private string _certificate = "";

        public string certificate {
                get {
                        return _certificate;
                }
        }


        // --- Methods Including Constructors ---
        public PartitionVerifier() {

        }

        public bool verify(PARTITION problem, string certificate) {

                certificate = certificate.Replace("{", "").Replace("}", "").Replace(" ", "");

                string[] pairs = certificate.Split("),(");
                string firstPair = pairs[0];
                string secondPair = pairs[1];

                List<string> c = firstPair.Replace("(", "").Replace(")", "").Replace(" ", "").Split(",").ToList();
                List<string> c2 = secondPair.Replace("(", "").Replace(")", "").Replace(" ", "").Split(",").ToList();

                foreach (var a in problem.S) {
                        if (problem.S.Count(n => n == a) != (c.Count(n => n == a) + c2.Count(n => n == a))) {
                                return false;
                        }
                }

                int sum = 0;
                int sum2 = 0;

                foreach (string a in c) {
                        if (problem.S.Contains(a)) {
                                sum += int.Parse(a);
                        }
                        else {
                                return false;
                        }
                }
                foreach (string a in c2) {
                        if (problem.S.Contains(a)) {
                                sum2 += int.Parse(a);
                        }
                        else {
                                return false;
                        }
                }

                if (sum == sum2 && (c.Count() + c2.Count()) == problem.S.Count()) {
                        return true;
                }

                return false;
        }


}