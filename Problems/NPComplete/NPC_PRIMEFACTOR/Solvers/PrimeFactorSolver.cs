using System.Numerics;
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_PRIMEFACTOR.Solvers;

class PrimeFactorSolver : ISolver<PRIMEFACTOR> {

        // --- Fields ---
        public string solverName { get; } = "PrimeFactor Trial Division Solver";
        public string solverDefinition { get; } = "A brute-force trial division algorithm for prime factorization. " +
            "This solver iteratively tests divisibility starting from the smallest prime (2) " +
            "and continues dividing the number by each prime factor found until the number is fully factored. " +
            "The algorithm runs in O(√n) time complexity in the worst case, making it suitable for small to moderate-sized integers.";
        public string source { get; } = "Pisano, Leonardo (1202), Incipit liber Abbaci compositus to Lionardo filio Bonaccii Pisano in year Mccij, Museo Galileo.";
        public string[] contributors { get; } = { "Jason L. Wright", "Paul Gilbreath", "Alex Svancara" };
        public bool timerHasExpired { get; set; }
        // Declared, not derived. Unpruned exhaustive trial division. Classic trap: looks polynomial-ish
        // (a loop to sqrt(n)) in the *value* of n, but is exponential in n's *bit-length* -- the actual
        // input size for a factoring problem -- which is why Shor's quantum algorithm (ShorsQuantumSolver)
        // is a genuine complexity-class improvement, not just a constant-factor speedup.
        public SolverType solverType { get; } = SolverType.BruteForce;
        public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
        // solve()'s trial-division loop runs while i * i <= numberToFactor, i.e. O(sqrt(n)) iterations
        // in the *value* of n -- but n's bit-length b is the real input size, and n = 2^b, so this is
        // O(2^(b/2)): exponential in input size, matching the Exponential bucket above.
        public string complexity { get; } = "O(sqrt(n)) in the value of n (exponential in n's bit-length)";
        // --- Methods Including Constructors ---
        public PrimeFactorSolver() { }

        public string solve(PRIMEFACTOR problem) {
                var numberToFactor = BigInteger.Parse(problem.instance);

                var factors = new List<BigInteger>();

                var i = new BigInteger(2);
                while (i * i <= numberToFactor) {
                        if (timerHasExpired) {
                                return "TIMEOUT";
                        }

                        while (numberToFactor % i == 0) {
                                factors.Add(i);
                                numberToFactor /= i;
                        }
                        i++;
                }

                if (numberToFactor > 1) {
                        factors.Add(numberToFactor);
                }

                return "(" + string.Join(",", factors) + ")";
        }
}
