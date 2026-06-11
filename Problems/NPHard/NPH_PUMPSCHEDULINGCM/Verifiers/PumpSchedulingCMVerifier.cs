using System;
using System.Collections.Generic;
using API.Interfaces;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM;
using SPADE;

namespace API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Verifiers;

class PumpSchedulingCMVerifier : IVerifier<PUMPSCHEDULINGCM>
{
    public const string CertificateGrammar = "{(cost,S) | cost is string, S is list}";
    public const string CertificateExample =
        "(29.72,((PumpA,0,1,1,0,0,0,0,0,1,1,1,1,0,0,0,0,0,1,1,1,1,1,0,0)," +
        "(PumpB,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1)," +
        "(PumpC,1,1,1,1,1,1,1,1,0,0,0,0,1,1,1,1,1,0,0,0,0,0,1,1)))";

    public string verifierName { get; } = "Pump Scheduling CM Verifier";
    public string certificate { get; private set; } = string.Empty;
    public string verifierDefinition { get; } =
        "Parses the pump schedule from the certificate, simulates the 24-hour tank trajectory " +
        "using exact arithmetic, and accepts if: (1) the tank stays within [0, capacity] at every hour, " +
        "(2) the reported cost matches the computed energy and startup costs within $0.01 tolerance.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "SARE 2026 Team" };

    private const double CostTolerance = 0.01;

    public bool verify(PUMPSCHEDULINGCM problem, string certificate)
    {
        this.certificate = certificate ?? string.Empty;

        UtilCollection parsed;
        try {
            parsed = new UtilCollection(this.certificate);
        } catch (Exception ex) {
            throw new CertificateParseException(problem, certificate, ex.Message);
        }

        // Certificate is (cost, ((PumpA,h0,...,h23),...))
        var topLevel = parsed.ToList();
        if (topLevel.Count != 2)
            throw new CertificateParseException(problem, certificate,
                "Certificate must be (cost,schedule).");

        if (!double.TryParse(topLevel[0].ToString().Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double reportedCost))
            throw new CertificateParseException(problem, certificate, "cost field is not a valid number");

        var pumpTuples = ((UtilCollection)topLevel[1]).ToList();
        int n = problem.Pumps.Count;
        if (pumpTuples.Count != n) return false;

        bool[,] on = new bool[n, 24];

        for (int p = 0; p < n; p++)
        {
            var parts = ((UtilCollection)pumpTuples[p]).ToList();
            if (parts.Count != 25) return false;

            if (!string.Equals(parts[0].ToString().Trim(), problem.Pumps[p].Name, StringComparison.Ordinal))
                return false;

            for (int h = 0; h < 24; h++)
            {
                string v = parts[h + 1].ToString().Trim();
                if (v != "0" && v != "1") return false;
                on[p, h] = v == "1";
            }
        }

        double level = problem.TankCurrentLevel;
        double totalCost = 0.0;
        int prevMask = 0;

        for (int h = 0; h < 24; h++)
        {
            int mask = 0;
            double flowIn = 0.0;
            double kw = 0.0;

            for (int p = 0; p < n; p++)
            {
                if (!on[p, h]) continue;
                mask |= (1 << p);
                flowIn += problem.Pumps[p].FlowRateGph;
                kw += problem.Pumps[p].PowerKw;
            }

            double rate = problem.PeakHours.Contains(h)
                ? problem.OnPeakCostPerKwh
                : problem.OffPeakCostPerKwh;
            totalCost += kw * rate;

            int startups = (~prevMask) & mask & ((1 << n) - 1);
            for (int p = 0; p < n; p++)
                if ((startups & (1 << p)) != 0)
                    totalCost += problem.Pumps[p].StartupCostDollars;

            prevMask = mask;
            level = level + flowIn - problem.DemandGph[h];
            if (level < 0 || level > problem.TankCapacity)
                return false;
        }

        return Math.Abs(totalCost - reportedCost) <= CostTolerance;
    }
}
