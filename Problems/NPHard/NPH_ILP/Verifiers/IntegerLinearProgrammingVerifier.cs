using System.Numerics;
using API.Interfaces;
using SPADE;

namespace API.Problems.NPHard.NPH_ILP.Verifiers;

class IntegerLinearProgrammingVerifier : IVerifier<INTEGERLINEARPROGRAMMING> {
    public string verifierName { get; } = "Integer Linear Programming Solution Verifier";

    public string verifierDefinition { get; } =
        "Checks that an integer assignment has the correct number of variables and satisfies every constraint Ax <= b.";

    public string source { get; } =
        "Schrijver, A. (1986). Theory of Linear and Integer Programming. Wiley.";
    public string sourceFile { get; } = SourceFile.Path();
    public string certificate { get; } = "(2,2)";
    public string[] contributors { get; } = { "Prashant Pant" };

    public bool verify(INTEGERLINEARPROGRAMMING problem, string certificate) {
        var x = ParseAssignment(problem, certificate);

        // Check that every constraint is satisfied.
        for (int i = 0; i < problem.A.Count; i++) {
            BigInteger total = BigInteger.Zero;

            for (int j = 0; j < x.Count; j++)
                total += problem.A[i][j] * x[j];

            if (total > problem.b[i])
                return false;
        }

        return true;
    }

    internal static List<BigInteger> ParseAssignment(
        INTEGERLINEARPROGRAMMING problem,
        string certificate
    ) {
        try {
            if (string.IsNullOrWhiteSpace(certificate))
                throw new FormatException("Certificate is empty.");

            string text = certificate.Trim();

            if (!text.StartsWith('(') ||
                !text.EndsWith(')') ||
                text[1..^1].IndexOfAny(new[] { '(', ')', '{', '}' }) >= 0)
                throw new FormatException(
                    "Expected a tuple (x1,...,xn) of integers."
                );

            var x = INTEGERLINEARPROGRAMMING.ParseVector(
                new UtilCollection(certificate)
            );

            if (x.Count != problem.c.Count)
                throw new FormatException(
                    "Expected one integer per variable."
                );

            return x;
        } catch (Exception ex) {
            throw new CertificateParseException(
                problem,
                certificate,
                ex.Message,
                ex
            );
        }
    }
}