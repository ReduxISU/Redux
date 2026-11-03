using API.Interfaces;
using SPADE;
using System.Text.RegularExpressions;

namespace API.Problems.NPComplete.NPC_CSP;

class CSP : IProblem {
    public string problemName { get; } = "Core Shuffling";
    public string problemLink { get; } = "";
    public string formalDefinition { get; } =
        "CSP = {<C0,C1,k> | a crane can transform C0 into C1 with total travel <= k}";
    public string problemDefinition { get; } =
        "Decide whether a crane can rearrange reactor cores within a distance limit.";
    public string inputDescription { get; } =
        "C0 and C1 contain (cn,x,y) tuples. C0 ends with (P,x,y,{cores}), describing the pool. K is the distance limit.";
    public string outputDescription { get; } = "True if such a shuffle exists, False otherwise";
    public string source { get; } = "Antonio Tahhan, Proof Paper #2";
    public string sourceFile { get; } = SourceFile.Path();
    public string wikiName { get; } = "";
    public string[] contributors { get; } = { "Quinton Smith" };
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPComplete;
    public ProblemType problemType { get; } = ProblemType.SequencingAndScheduling;
    public ProblemVariant problemVariant { get; } = ProblemVariant.Decision;

    public const string InstanceGrammar = "{(C0,C1,K) | C0 is list, C1 is list, K is int}";

    public const string DefaultInstance =
        "(((A,1,2),(B,2,2),(C,1,1),(D,2,1),(P,0,2,{E})),((B,1,2),(E,2,2),(C,1,1),(D,2,1)),20)";

    public string defaultInstance { get; } = DefaultInstance;
    public string instanceFormat { get; } =
        $"Format: {InstanceGrammar}. Use (cn,x,y) for cores and a final (P,x,y,{{cores}}) in C0. All coordinates share the same coordinate system. Example: {DefaultInstance}";
    public string certificateFormat { get; } =
        "An ordered list of (cn,fromX,fromY,toX,toY) transfers. Example: ((B,2,2,1,2),(E,0,2,2,2)). Verification is not implemented yet.";

    public string instance { get; }
    public List<(string cn, int x, int y)> C0 { get; }
    public List<(string cn, int x, int y)> C1 { get; }
    public (int x, int y) poolPosition { get; }
    public HashSet<string> poolCores { get; }
    public int K { get; }

    public CSP()
        : this(DefaultInstance) { }

    public CSP(string input) {
        instance = input;

        try {
            if (string.IsNullOrWhiteSpace(input))
                throw new FormatException("An instance is required.");

            StringParser parser = new(InstanceGrammar);
            // SPADE requires delimiters to touch their contents. Preserve the original
            // instance and whitespace inside names while normalizing token boundaries.
            parser.parse(Regex.Replace(input.Trim(), @"\s*([(){},])\s*", "$1"));

            var initial = parser["C0"].ToList();
            var pool = initial[^1].ToList();
            if (pool.Count != 4 || pool[0].ToString() != "P")
                throw new FormatException("C0 must end with (P,x,y,{cores}).");
            initial.RemoveAt(initial.Count - 1);

            C0 = ReadLayout(initial);
            C1 = ReadLayout(parser["C1"].ToList());
            poolPosition = (int.Parse(pool[1].ToString()), int.Parse(pool[2].ToString()));
            poolCores = pool[3].ToList().Select(ReadCoreName).ToHashSet();
            K = int.Parse(parser["K"].ToString());
            if (K < 0)
                throw new FormatException("K must be a nonnegative integer.");
        } catch (Exception ex) when (ParseGuard.IsCertificateParseFailure(ex)) {
            throw new ProblemParseException(problemName, input, ex.Message, ex);
        }
    }

    private static List<(string cn, int x, int y)> ReadLayout(IEnumerable<UtilCollection> entries) {
        return entries
            .Select(entry => {
                var fields = entry.ToList();
                if (fields.Count != 3)
                    throw new FormatException("Each core must be a (cn,x,y) tuple.");

                return (
                    ReadCoreName(fields[0]),
                    int.Parse(fields[1].ToString()),
                    int.Parse(fields[2].ToString())
                );
            })
            .ToList();
    }

    private static string ReadCoreName(UtilCollection core) {
        string name = core.ToString();
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(new[] { '(', ')', '{', '}', ',' }) >= 0)
            throw new FormatException("Core names must be nonempty scalar values.");
        return name;
    }
}
