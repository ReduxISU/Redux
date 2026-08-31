using System;
using System.Collections.Generic;
using API.Interfaces;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGEM;
using SPADE;

namespace API.Problems.NPHard.NPH_PUMPSCHEDULINGEM.Verifiers;

class PumpSchedulingEMVerifier : IVerifier<PUMPSCHEDULINGEM> {
    public const string CertificateGrammar = "{(budget,cost,S) | budget and cost are numbers, S is list}";
    public const string CertificateExample =
        "(45.72,38.15,((PumpA,0,1,1,0,0,0,0,0,1,1,1,1,0,0,0,0,0,1,1,1,1,1,0,0)," +
        "(PumpB,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1)," +
        "(PumpC,1,1,1,1,1,1,1,1,0,0,0,0,1,1,1,1,1,0,0,0,0,0,1,1)))";

    public string verifierName { get; } = "Default Verifier";
    public string certificate { get; private set; } = string.Empty;
    public string verifierDefinition { get; } =
        "Parses the effective budget and pump schedule from the certificate, simulates the " +
        "24-hour tank trajectory, and accepts if: " +
        "(1) the tank stays within [minLevel, capacity] at every hour, " +
        "(2) the reported total cost matches the computed energy and startup costs within $0.01, " +
        "(3) the actual cost does not exceed the effective budget in the certificate, and " +
        "(4) if the instance specifies a positive budget, the certificate's effective budget " +
        "does not exceed that bound.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };

    private const double CostTolerance = 0.01;

    public bool verify(PUMPSCHEDULINGEM problem, string certificate) {
        this.certificate = certificate ?? string.Empty;

        UtilCollection parsed;
        try {
            parsed = new UtilCollection(this.certificate);
        } catch (Exception ex) {
            throw new CertificateParseException(problem, this.certificate, ex.Message);
        }

        // Certificate is (effectiveBudget, totalCost, ((PumpA,h0,...,h23),...))
        var topLevel = parsed.ToList();
        if (topLevel.Count != 3)
            throw new CertificateParseException(problem, this.certificate,
                "Certificate must be (effectiveBudget,totalCost,schedule).");

        if (!TryParseDouble(topLevel[0].ToString(), out double certBudget))
            throw new CertificateParseException(problem, this.certificate,
                "effectiveBudget field is not a valid number.");

        if (!TryParseDouble(topLevel[1].ToString(), out double reportedCost))
            throw new CertificateParseException(problem, this.certificate,
                "totalCost field is not a valid number.");

        // If the instance has an explicit positive budget, the certificate's effective
        // budget must not exceed it (within tolerance).
        if (problem.BudgetLimitDollars > 0 &&
            certBudget > problem.BudgetLimitDollars + CostTolerance)
            return false;

        var pumpTuples = ((UtilCollection)topLevel[2]).ToList();
        int n = problem.Pumps.Count;
        if (pumpTuples.Count != n) return false;

        bool[,] on = new bool[n, 24];
        for (int p = 0; p < n; p++) {
            var parts = ((UtilCollection)pumpTuples[p]).ToList();
            if (parts.Count != 25) return false;

            if (!string.Equals(parts[0].ToString().Trim(), problem.Pumps[p].Name,
                    StringComparison.Ordinal))
                return false;

            for (int h = 0; h < 24; h++) {
                string v = parts[h + 1].ToString().Trim();
                if (v != "0" && v != "1") return false;
                on[p, h] = v == "1";
            }
        }

        // Simulate the 24-hour schedule.
        double level = problem.TankCurrentLevel;
        double computed = 0.0;
        int prevMask = 0;
        int nMasks = 1 << n;

        for (int h = 0; h < 24; h++) {
            int mask = 0;
            double flowIn = 0.0;
            double kw = 0.0;

            for (int p = 0; p < n; p++) {
                if (!on[p, h]) continue;
                mask |= (1 << p);
                flowIn += problem.Pumps[p].FlowRateGph;
                kw += problem.Pumps[p].PowerKw;
            }

            double rate = problem.PeakHours.Contains(h)
                ? problem.OnPeakCostPerKwh
                : problem.OffPeakCostPerKwh;
            computed += kw * rate;

            int startups = (~prevMask) & mask & (nMasks - 1);
            for (int p = 0; p < n; p++)
                if ((startups & (1 << p)) != 0)
                    computed += problem.Pumps[p].StartupCostDollars;

            prevMask = mask;
            level = level + flowIn - problem.DemandGph[h];

            // Tank must stay within [minLevel, capacity].
            if (level < problem.TankMinLevel || level > problem.TankCapacity)
                return false;
        }

        // Reported cost must match simulation.
        if (Math.Abs(computed - reportedCost) > CostTolerance) return false;

        // Actual cost must not exceed the certificate's effective budget.
        if (computed > certBudget + CostTolerance) return false;

        return true;
    }

    private static bool TryParseDouble(string s, out double value) =>
        double.TryParse(s.Trim(),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out value);
}
