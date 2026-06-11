using System;
using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM;

namespace API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Verifiers;

class PumpSchedulingCMVerifier : IVerifier<PUMPSCHEDULINGCM>
{
    // --- Fields ---
    public string verifierName { get; } = "Pump Scheduling CM Verifier";
    public string verifierDefinition { get; } =
        "Parses the pump schedule from the certificate, simulates the 24-hour tank trajectory " +
        "using exact arithmetic, and accepts if: (1) the tank stays within [0, capacity] at every hour, " +
        "(2) the reported cost matches the computed energy and startup costs within $0.01 tolerance.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "SARE 2026 Team" };
    public string certificate { get; private set; } = string.Empty;

    private const double CostTolerance = 0.01;

    public bool verify(PUMPSCHEDULINGCM problem, string cert)
    {
        certificate = cert ?? string.Empty;

        // Strip outer parens: (cost,{schedule})
        string s = certificate.Trim();
        if (s.StartsWith("(") && s.EndsWith(")"))
            s = s[1..^1].Trim();
        if (s == "{}" || s.Length == 0) return false;

        // Split into cost part and schedule part at the first top-level comma.
        int splitIdx = FindFirstTopLevelComma(s);
        if (splitIdx < 0) return false;

        string costStr = s[..splitIdx].Trim();
        string schedStr = s[(splitIdx + 1)..].Trim();

        if (!double.TryParse(costStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double reportedCost))
            return false;

        // Parse schedule: {(PumpName,h0,...,h23),...}
        schedStr = PUMPSCHEDULINGCM.StripBraces(schedStr);
        var pumpTuples = PUMPSCHEDULINGCM.SplitTopLevel(schedStr, ',');

        int n = problem.Pumps.Count;
        if (pumpTuples.Count != n) return false;

        // schedules[p][h] = true if pump p is on during hour h
        bool[,] on = new bool[n, 24];

        for (int p = 0; p < n; p++)
        {
            string inner = PUMPSCHEDULINGCM.StripParens(pumpTuples[p].Trim());
            var parts = PUMPSCHEDULINGCM.SplitTopLevel(inner, ',');
            if (parts.Count != 25) return false;  // name + 24 hours

            if (!string.Equals(parts[0].Trim(), problem.Pumps[p].Name, StringComparison.Ordinal))
                return false;

            for (int h = 0; h < 24; h++)
            {
                if (!int.TryParse(parts[h + 1].Trim(), out int v) || v is not (0 or 1))
                    return false;
                on[p, h] = v == 1;
            }
        }

        // Simulate the schedule exactly — no bucket discretization.
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

            // Energy cost for this hour.
            double rate = problem.PeakHours.Contains(h)
                ? problem.OnPeakCostPerKwh
                : problem.OffPeakCostPerKwh;
            totalCost += kw * rate;

            // Startup cost for pumps newly turned on this hour.
            int startups = (~prevMask) & mask & ((1 << n) - 1);
            for (int p = 0; p < n; p++)
                if ((startups & (1 << p)) != 0)
                    totalCost += problem.Pumps[p].StartupCostDollars;

            prevMask = mask;

            // Update tank level and check bounds.
            level = level + flowIn - problem.DemandGph[h];
            if (level < 0 || level > problem.TankCapacity)
                return false;
        }

        // Reported cost must match the simulated cost within tolerance.
        return Math.Abs(totalCost - reportedCost) <= CostTolerance;
    }

    // Returns the index of the first comma at bracket depth 0, or -1 if none.
    private static int FindFirstTopLevelComma(string s)
    {
        int depth = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c is '(' or '{' or '[') depth++;
            else if (c is ')' or '}' or ']') depth--;
            else if (c == ',' && depth == 0) return i;
        }
        return -1;
    }
}
