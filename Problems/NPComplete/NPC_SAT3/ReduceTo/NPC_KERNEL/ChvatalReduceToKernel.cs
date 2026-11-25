using API.Interfaces;
using API.Problems.NPComplete.NPC_KERNEL;

namespace API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_KERNEL;

// Chvatal's SAT-to-kernel construction. See README.md for the proof and source.
class ChvatalReduceToKernel : IReduction<SAT3, KERNEL> {
    public string reductionName { get; } = "Chvatal's Kernel Reduction";
    public string reductionDefinition { get; } = "Maps 3SAT to directed kernel using a pair of literal vertices per variable and a directed triangle per clause.";
    public string source { get; } = "Vasek Chvatal, On the computational complexity of finding a kernel, CRM-300, 1973; author's reconstruction.";
    public string sourceLink { get; } = "https://users.encs.concordia.ca/~chvatal/kernel.html";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Prashant Pant" };
    public ReductionCost cost { get; } = ReductionCost.Linear;
    public ReductionType reductionType { get; } = ReductionType.ComponentDesign;
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Polynomial;
    public string? complexity { get; } = "Polynomial in the input and output encoding lengths; O(n + m + L) vertices and arcs for n variables, m clauses, and L literal occurrences.";
    public SAT3 reductionFrom { get; }
    public KERNEL reductionTo { get; private set; }

    public ChvatalReduceToKernel() : this(new SAT3()) { }
    public ChvatalReduceToKernel(string instance) : this(new SAT3(instance)) { }
    public ChvatalReduceToKernel(SAT3 from) {
        reductionFrom = from;
        reductionTo = reduce();
    }

    private static string Variable(string literal) => literal.StartsWith("!") ? literal.Substring(1) : literal;

    public KERNEL reduce() {
        var literals = new Dictionary<string, (int positive, int negative)>();
        var vertices = new List<int>();
        var arcs = new HashSet<(int, int)>();
        int AddVertex() { int id = vertices.Count; vertices.Add(id); return id; }
        foreach (var clause in reductionFrom.clauses) {
            foreach (string literal in clause) {
                string variable = Variable(literal);
                if (literals.ContainsKey(variable)) continue;
                int positive = AddVertex(), negative = AddVertex();
                literals.Add(variable, (positive, negative));
                arcs.Add((positive, negative));
                arcs.Add((negative, positive));
            }
        }
        foreach (var clause in reductionFrom.clauses) {
            int a = AddVertex(), b = AddVertex(), c = AddVertex();
            arcs.Add((a, b)); arcs.Add((b, c)); arcs.Add((c, a));
            foreach (string literal in clause) {
                var pair = literals[Variable(literal)];
                int target = literal.StartsWith("!") ? pair.negative : pair.positive;
                arcs.Add((a, target)); arcs.Add((b, target)); arcs.Add((c, target));
            }
        }
        string graph = "({" + string.Join(",", vertices) + "},{" +
            string.Join(",", arcs.Select(e => $"({e.Item1},{e.Item2})")) + "})";
        reductionTo = new KERNEL(graph);
        return reductionTo;
    }

    public string mapSolutions(string problemFromSolution) {
        if (!reductionFrom.defaultVerifier.verify(reductionFrom, problemFromSolution))
            throw new ReductionInputException(this, problemFromSolution, reductionFrom.certificateFormat, "assignment does not satisfy the formula");
        var values = new Dictionary<string, bool>();
        foreach (string assignment in problemFromSolution.Trim().Trim('(', ')').Split(',')) {
            string[] parts = assignment.Trim().Split(new[] { ':', '=' }, 2);
            values[parts[0].Trim()] = parts[1].Trim() is "True" or "T";
        }
        var selected = new List<int>();
        var seen = new HashSet<string>();
        foreach (var clause in reductionFrom.clauses) {
            foreach (string literal in clause) {
                string variable = Variable(literal);
                if (!seen.Add(variable)) continue;
                int positive = (seen.Count - 1) * 2;
                selected.Add(values.TryGetValue(variable, out bool value) && value ? positive : positive + 1);
            }
        }
        string result = "{" + string.Join(",", selected) + "}";
        if (!reductionTo.defaultVerifier.verify(reductionTo, result))
            throw new ReductionInputException(this, problemFromSolution, reductionFrom.certificateFormat, "assignment is inconsistent or incomplete");
        return result;
    }
}

