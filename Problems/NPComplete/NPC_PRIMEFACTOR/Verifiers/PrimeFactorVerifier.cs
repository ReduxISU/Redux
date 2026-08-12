using System.Runtime.ConstrainedExecution;
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_PRIMEFACTOR.Verifiers;

class PrimeFactorVerifier : IVerifier<PRIMEFACTOR> {

        // --- Fields ---
        public string verifierName { get; } = "Prime Factor Verifier";
        public string verifierDefinition { get; } = "Verifies that the product of the proposed factors equals the original input number.";
        public string source { get; } = " ";
        public string[] contributors { get; } = { "Jason L. Wright", "Paul Gilbreath", "Alex Svancara", "Grant Gardner" };
        private readonly string _certificate = "";

        public string certificate {
                get {
                        return _certificate;
                }
        }

        // --- Methods Including Constructors ---
        public PrimeFactorVerifier() {

        }

        public bool verify(PRIMEFACTOR problem, string certificate) {
                // Parse the original number from the problem instance using SPADE parser
                SPADE.StringParser parser = new("{ i | i is int}");
                parser.parse(problem.instance);
                int originalNumber = int.Parse(parser["i"].ToString());

                // Parse the list of factors from the certificate using SPADE parser
                parser = new("{f | f is list}");
                parser.parse(certificate);
                SPADE.UtilCollection factorsList = parser["f"];
                int certProduct = 1;
                foreach (var factor in factorsList) {
                        var factorStr = factor.ToString();
                        if (factorStr is null) {
                                return false;
                        }
                        int factorInt = int.Parse(factorStr);
                        certProduct *= factorInt;
                }

                return certProduct == originalNumber;
        }
}
