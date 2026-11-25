using System.Globalization;
using API.Interfaces;
using SPADE;

namespace API.Problems.NPHard.NPH_MAXIMUMVALUEKNAPSACK;

internal sealed class MAXIMUMVALUEKNAPSACK : IProblem {
    public string problemName { get; } = "Maximum-Value 0-1 Knapsack";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Knapsack_problem";
    public string formalDefinition { get; } = "Given indexed items (w_i,v_i) and capacity W, maximize sum(v_i*x_i) subject to sum(w_i*x_i) <= W and x_i in {0,1}.";
    public string problemDefinition { get; } = "Choose each item at most once to maximize total value without exceeding capacity. Identical weight/value pairs are distinct item occurrences. The empty selection is allowed. Weights, values, and capacity are nonnegative integers; zero-weight and zero-value items are allowed.";
    public string inputDescription { get; } = "An ordered list of (weight,value) item pairs and a capacity; item indices are 1-based positions in that list.";
    public string outputDescription { get; } = "Selected item indices and their total value; an optimal solution has maximum value among all feasible selections.";
    public string source { get; } = "Kellerer, H., Pferschy, U., and Pisinger, D. Knapsack Problems. Springer, 2004.";
    public string sourceLink { get; } = "https://doi.org/10.1007/978-3-540-24777-7";
    public string sourceFile { get; } = SourceFile.Path();
    public string wikiName { get; } = "Knapsack_problem";
    public string[] contributors { get; } = { "Corbin Hay" };
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPHard;
    public ProblemVariant problemVariant { get; } = ProblemVariant.Optimization;
    public ProblemType problemType { get; } = ProblemType.MathematicalProgramming;

    public const string InstanceGrammar = "{(items,capacity) | items is list, capacity is int}";
    public const string DefaultInstance = "(((10,60),(20,100),(30,120)),50)";
    public string defaultInstance { get; } = DefaultInstance;
    public string instanceFormat { get; } = $"Format: {InstanceGrammar}. Each item is an ordered (weight,value) pair. Items retain input order and duplicates. All numbers use ASCII decimal digits without signs and must be between 0 and {long.MaxValue}; the sum of all values must also fit this range. Whitespace is allowed between tokens. Empty items: ((),0). Single item: (((10,60)),50). Example: {DefaultInstance}";
    public string certificateFormat { get; } = "({i1,i2,...},totalValue): distinct 1-based item indices and their nonnegative signed 64-bit total value. Solvers emit indices in ascending order. Feasibility requires total weight at most capacity; optimality is a separate check. The empty selection is ({},0). Example: ({2,3},220)";

    public string instance { get; }
    public IReadOnlyList<MaximumValueKnapsackItem> items { get; }
    public long capacity { get; }
    public long totalItemValue { get; }

    public MAXIMUMVALUEKNAPSACK() : this(DefaultInstance) { }

    /// <summary>The grammar's parts, for the Big-O precheck (#843): |i| items, counting duplicates. The same name as 0-1 Knapsack.</summary>
    public IReadOnlyList<SizeMeasure> SizeMeasures() => [SizeMeasure.Count("i", items.Count)];

    public MAXIMUMVALUEKNAPSACK(string input) {
        try {
            ValidateSyntax(input);
            // Read the whole collection tree, then enforce arity and Int64 domains ourselves.
            // SPADE alone can ignore trailing input and empty comma-separated entries.
            StringParser parser = new("{I | I is list}");
            parser.parse(string.Concat(input.Where(c => !char.IsWhiteSpace(c))));
            UtilCollection root = parser["I"];
            RequirePair(root, "instance (items,capacity)");
            if (!root[0].IsOrdered())
                throw new FormatException("items must be a parenthesized ordered list");
            capacity = NonnegativeInteger(root[1], "capacity");

            var parsedItems = new List<MaximumValueKnapsackItem>();
            long total = 0;
            foreach (UtilCollection entry in root[0].ToList()) {
                RequirePair(entry, "item (weight,value)");
                long weight = NonnegativeInteger(entry[0], "weight");
                long value = NonnegativeInteger(entry[1], "value");
                total = checked(total + value);
                parsedItems.Add(new(parsedItems.Count + 1, weight, value));
            }
            items = parsedItems.AsReadOnly();
            totalItemValue = total;
            instance = input;
        } catch (OverflowException ex) {
            throw new ProblemParseException(problemName, input, $"sum of item values must not exceed {long.MaxValue}", ex);
        } catch (Exception ex) when (ex is not OutOfMemoryException && ex is not ProblemParseException) {
            throw new ProblemParseException(problemName, input, ex.Message, ex);
        }
    }

    // All shared selection operations use the same public, 1-based item indices.
    internal bool IsFeasible(IReadOnlyList<int> selected) {
        var seen = new HashSet<int>();
        long remaining = capacity;
        foreach (int index in selected) {
            if (index < 1 || index > items.Count || !seen.Add(index)) return false;
            long weight = items[index - 1].Weight;
            if (weight > remaining) return false;
            remaining -= weight; // Never sum weights: even infeasible sums may exceed Int64.
        }
        return true;
    }

    // Evaluates a distinct in-range selection, whether or not it fits the capacity.
    internal long Value(IReadOnlyList<int> selected) {
        var seen = new HashSet<int>();
        long value = 0;
        foreach (int index in selected) {
            if (index < 1 || index > items.Count || !seen.Add(index))
                throw new ArgumentException("selected indices must be distinct and between 1 and the item count", nameof(selected));
            value = checked(value + items[index - 1].Value);
        }
        return value;
    }

    internal string FormatCertificate(IReadOnlyList<int> selected) {
        if (!IsFeasible(selected))
            throw new ArgumentException("cannot format an infeasible selection", nameof(selected));
        return "({" + string.Join(",", selected.Order().Select(i => i.ToString(CultureInfo.InvariantCulture)))
            + "}," + Value(selected).ToString(CultureInfo.InvariantCulture) + ")";
    }

    private static void RequirePair(UtilCollection value, string description) {
        if (!value.IsOrdered() || value.Count() != 2)
            throw new FormatException($"{description} must have exactly two components");
    }

    private static long NonnegativeInteger(UtilCollection entry, string description) {
        string text = entry.ToString();
        if (!entry.IsValue() || text.Length == 0 || text.Any(c => c is < '0' or > '9')
            || !long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long value))
            throw new FormatException($"{description} must be a nonnegative integer no greater than {long.MaxValue}");
        return value;
    }

    // A lexical/delimiter guard around SPADE, not a second item parser. Only round
    // collections and ASCII integer tokens belong to this instance grammar.
    private static void ValidateSyntax(string input) {
        if (string.IsNullOrWhiteSpace(input)) throw new FormatException("instance is empty");
        string text = input.Trim();
        if (text[0] != '(' || text[^1] != ')')
            throw new FormatException("instance must be an enclosing (items,capacity) tuple");
        int depth = 0;
        bool needsEntry = true;
        bool justOpened = false;
        for (int i = 0; i < text.Length; i++) {
            char c = text[i];
            if (char.IsWhiteSpace(c)) continue;
            if (c == '(') {
                if (!needsEntry) throw new FormatException("missing comma before collection");
                depth++;
                needsEntry = justOpened = true;
            } else if (c == ')') {
                if (--depth < 0) throw new FormatException("unbalanced parentheses");
                if (needsEntry && !justOpened) throw new FormatException("missing entry after comma");
                needsEntry = justOpened = false;
                if (depth == 0 && i != text.Length - 1)
                    throw new FormatException("unexpected text after instance tuple");
            } else if (c == ',') {
                if (needsEntry) throw new FormatException("missing entry before comma");
                needsEntry = true;
                justOpened = false;
            } else if (c is >= '0' and <= '9') {
                if (!needsEntry) throw new FormatException("missing comma between entries");
                while (i + 1 < text.Length && text[i + 1] is >= '0' and <= '9') i++;
                needsEntry = justOpened = false;
            } else {
                throw new FormatException("only parentheses, commas, whitespace, and ASCII decimal digits are allowed");
            }
        }
        if (depth != 0) throw new FormatException("unbalanced parentheses");
    }
}

internal sealed record MaximumValueKnapsackItem(int Index, long Weight, long Value);
