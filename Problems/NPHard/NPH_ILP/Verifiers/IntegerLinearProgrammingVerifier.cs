using System.Numerics;
using API.Interfaces;
using API.Problems.NPHard.NPH_ILP.Solvers;
using SPADE;

namespace API.Problems.NPHard.NPH_ILP.Verifiers;

class IntegerLinearProgrammingVerifier : IVerifier<INTEGERLINEARPROGRAMMING> {
    public string verifierName { get; } = "Integer Linear Programming Solution Verifier";
    public string verifierDefinition { get; } =
        "Checks that an integer vector satisfies Ax <= b and achieves the maximum objective value c^T x.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string certificate { get; } = "(2,2)";
    public string[] contributors { get; } = { "Prashant Pant" };

    public bool verify(INTEGERLINEARPROGRAMMING problem, string certificate) {
        var x = ParseAssignment(problem, certificate);

        // Step 1: Check feasibility.
        for (int i = 0; i < problem.A.Count; i++) {
            BigInteger total = BigInteger.Zero;

            for (int j = 0; j < x.Count; j++)
                total += problem.A[i][j] * x[j];

            if (total > problem.b[i])
                return false;
        }

        // Step 2: Calculate the certificate's objective value.
        BigInteger certificateValue = BigInteger.Zero;

        for (int j = 0; j < x.Count; j++)
            certificateValue += problem.c[j] * x[j];

        // Step 3: Solve the ILP independently of the certificate.
        var solver = new IntegerLinearProgrammingSolver();
        string optimalResult = solver.solve(problem);

        // Cannot certify optimality if the solver failed.
        if (optimalResult == "infeasible" ||
            optimalResult == "unbounded" ||
            optimalResult == "timeout")
            return false;

        // Step 4: Calculate the optimal objective value.
        var optimalX = ParseAssignment(problem, optimalResult);

        BigInteger optimalValue = BigInteger.Zero;

        for (int j = 0; j < optimalX.Count; j++)
            optimalValue += problem.c[j] * optimalX[j];

        // Step 5: Compare objective values.
        return certificateValue == optimalValue;
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