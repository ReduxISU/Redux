using API.Interfaces;
using API.Interfaces.CodeTree;
using API.Interfaces.Steps;
using System.Text;

namespace API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Solvers;

// Incremental: Huffman's algorithm builds the tree one merge at a time and never undoes one.
class LosslessDataCompressionSolver : ISolver<LOSSLESSDATACOMPRESSION, HuffmanForest>, IIncrementalSolver {

    // --- Fields ---

    public string solverName { get; } = "Huffman's Algorithm";

    public string solverDefinition { get; } =
        "Solves the Lossless Data Compression problem using Huffman Encoding. " +
        "The solver counts character frequencies, builds a Huffman tree, creates prefix-free binary codes, " +
        "and then encodes the original input string.";

    public string source { get; } =
        "https://www.ias.ac.in/article/fulltext/reso/011/02/0091-0099";
    public string sourceFile { get; } = SourceFile.Path();

    public string[] contributors { get; } = { "Pramesh Shah", "Bektur Akkabakov" };

    public bool timerHasExpired { get; set; }
    // Declared, not derived. Huffman coding: irrevocable locally-optimal choice each step.
    public SolverType solverType { get; } = SolverType.Greedy;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // Declared, not derived. This is Huffman coding, but BuildHuffmanTree fully
    // re-sorts the remaining node list on every merge instead of using a heap, so tree
    // construction is O(k^2 log k) rather than the classical O(n log n) heap-based bound
    // (k = distinct characters, k <= n). EncodeInput adds O(n * k) (code length <= k-1).
    public MathematicalFunction timeComplexity { get; } = new("k^2 log k + n*k", ", n = input length, k <= n = distinct characters");

    // Constructors

    public LosslessDataCompressionSolver() {

    }

    //  Main solver method 

    public string solve(LOSSLESSDATACOMPRESSION problem) => Solve(problem, StepRecorder<HuffmanForest>.Off);

    // Steps: a Try for the count, a Try per merge (the two lightest trees and their new parent as the focus), an Accept once
    // the codes are read off the tree, and Done with the verifier's verdict.
    public string Solve(LOSSLESSDATACOMPRESSION problem, StepRecorder<HuffmanForest> rec) {

        // always check the timer first.
        if (timerHasExpired) {
            return "timeout";
        }

        // the problem instance is the raw text we want to compress.
        string input = problem.instance ?? string.Empty;

        // resets problem fields before solving.
        problem.frequencies = new Dictionary<char, int>();
        problem.codes = new Dictionary<char, string>();
        problem.encodedText = string.Empty;
        problem.solution = string.Empty;

        // empty input is still valid, it just compresses to nothing.
        // "()" is the special-cased certificate for this case -- there's no
        // character to build a (asciiCode,code) pair from.
        if (input.Length == 0) {
            problem.solution = "()";
            Done(problem, rec, new HuffmanForest([], []), "The text is empty, so there is nothing to encode.");
            return problem.solution;
        }

        // first: count how many times each character appears.
        Dictionary<char, int> frequencies = BuildFrequencyTable(input);

        if (timerHasExpired) {
            return "timeout";
        }

        // second: build the Huffman tree from the frequencies.
        var merges = new List<HuffmanMerge>();
        rec.Try(() => new HuffmanForest([], []), () => frequencies.Count == 1
            ? $"Count each character: {Plural(input.Length, "character")}, all {CodeTreeFrames.Label(input[0])}. A single tree needs no merging."
            : $"Count each character: {Plural(input.Length, "character")}, {frequencies.Count} different. Each starts as its own one-node tree, weighted by how often it appears.");
        HuffmanNode root = BuildHuffmanTree(frequencies, merges, rec);

        if (timerHasExpired) {
            return "timeout";
        }

        // thurd: Walk the tree and create binary codes.
        Dictionary<char, string> codes = new Dictionary<char, string>();
        BuildCodeTable(root, "", codes);

        if (timerHasExpired) {
            return "timeout";
        }

        // The codes by leaf id: characters in character order are c1, c2, ...
        HuffmanForest Forest() {
            var sorted = new List<char>(codes.Keys);
            sorted.Sort();
            return new HuffmanForest(merges, sorted.Select((c, i) => new KeyValuePair<string, string>(CodeTreeFrames.LeafId(i), codes[c])));
        }
        rec.Accept(Forest, () => frequencies.Count == 1
            ? "Only one character, so its code is just 0."
            : "Read each code from the top: going left adds 0, going right adds 1. The most frequent characters sit near the top and get the shortest codes.");

        // fourth: encodes the original input using the code table.
        string encodedText = EncodeInput(input, codes);

        // store results on the problem object so the verifier/GUI can use them.
        problem.frequencies = frequencies;
        problem.codes = codes;
        problem.encodedText = encodedText;


        problem.solution = FormatSolution(codes, encodedText);

        long fixedBits = 8L * input.Length;
        Done(problem, rec, Forest(), $"Encoded: {Plural(encodedText.Length, "bit")}, versus {fixedBits:N0} bits at 8 bits per character, "
            + $"{Math.Round((1 - (double)encodedText.Length / fixedBits) * 100)}% smaller. No code is the start of another, so the bits decode back to exactly the text.");

        return problem.solution;
    }

    // The Done step, with the verifier's verdict, only when recording: a plain solve has no steps, and needs no second check.
    private static void Done(LOSSLESSDATACOMPRESSION problem, StepRecorder<HuffmanForest> rec, HuffmanForest answer, string caption) {
        if (ReferenceEquals(rec, StepRecorder<HuffmanForest>.Off)) return;
        rec.Done(answer, problem.defaultVerifier.verify(problem, problem.solution), caption);
    }

    private static string Plural(long n, string one) => $"{n:N0} {(n == 1 ? one : one + "s")}";

    //  Huffman helper methods :)

    private Dictionary<char, int> BuildFrequencyTable(string input) {
        Dictionary<char, int> frequencies = new Dictionary<char, int>();

        foreach (char currentCharacter in input) {
            if (!frequencies.ContainsKey(currentCharacter)) {
                frequencies[currentCharacter] = 0;
            }

            frequencies[currentCharacter]++;
        }

        return frequencies;
    }

    private HuffmanNode BuildHuffmanTree(Dictionary<char, int> frequencies, List<HuffmanMerge> merges, StepRecorder<HuffmanForest> rec) {
        List<HuffmanNode> nodes = new List<HuffmanNode>();

        // sorting the characters makes the result deterministics.
        // that means our tests should get the same answer every time??
        List<char> characters = new List<char>(frequencies.Keys);
        characters.Sort();

        int serialNumber = 0;

        foreach (char currentCharacter in characters) {
            HuffmanNode node = new HuffmanNode();
            node.Character = currentCharacter;
            node.Frequency = frequencies[currentCharacter];
            node.LowestCharacterValue = (int)currentCharacter;
            node.SerialNumber = serialNumber++;
            node.Id = CodeTreeFrames.LeafId(node.SerialNumber);

            nodes.Add(node);
        }

        // keeps merging the two lowest frequency nodes until one tree remains.
        while (nodes.Count > 1) {
            SortNodes(nodes);

            HuffmanNode left = nodes[0];
            HuffmanNode right = nodes[1];

            nodes.RemoveAt(0);
            nodes.RemoveAt(0);

            HuffmanNode parent = new HuffmanNode();
            parent.Character = null;
            parent.Frequency = left.Frequency + right.Frequency;
            parent.Left = left;
            parent.Right = right;
            parent.LowestCharacterValue = Math.Min(left.LowestCharacterValue, right.LowestCharacterValue);
            parent.SerialNumber = serialNumber++;
            parent.Id = CodeTreeFrames.NodeId(merges.Count + 1);

            // A tie: a tree left weighs as much as the heavier of the two, and lost on the earliest character.
            bool tie = nodes.Count > 0 && nodes.Min(n => n.Frequency) == right.Frequency;
            merges.Add(new HuffmanMerge(parent.Id, left.Id, right.Id, parent.Frequency));
            int made = merges.Count;
            rec.Try(() => new HuffmanForest(merges.Take(made), []),
                () => $"Merge {Describe(left)} and {Describe(right)}, the two lightest trees left, into a node of weight {parent.Frequency}."
                    + (tie ? " (A tie: the tree holding the earliest character goes first.)" : ""),
                () => [left.Id, right.Id, parent.Id]);

            nodes.Add(parent);
        }

        return nodes[0];
    }

    // "e (5)" for a character, "the tree of weight 14 (e and f)" for a merged tree.
    private static string Describe(HuffmanNode node) {
        if (node.Character is char c) return $"{CodeTreeFrames.Label(c)} ({node.Frequency})";
        var chars = new List<string>();
        void Walk(HuffmanNode? n) {
            if (n == null || chars.Count > 4) return;
            if (n.Character is char leaf) chars.Add(CodeTreeFrames.Label(leaf));
            Walk(n.Left);
            Walk(n.Right);
        }
        Walk(node);
        string which = chars.Count > 4 ? $"{node.Leaves} characters"
            : chars.Count == 2 ? $"{chars[0]} and {chars[1]}"
            : string.Join(", ", chars.Take(chars.Count - 1)) + " and " + chars[^1];
        return $"the tree of weight {node.Frequency} ({which})";
    }

    private void SortNodes(List<HuffmanNode> nodes) {
        nodes.Sort((a, b) => {
            int frequencyCompare = a.Frequency.CompareTo(b.Frequency);

            if (frequencyCompare != 0) {
                return frequencyCompare;
            }

            int characterCompare = a.LowestCharacterValue.CompareTo(b.LowestCharacterValue);

            if (characterCompare != 0) {
                return characterCompare;
            }

            return a.SerialNumber.CompareTo(b.SerialNumber);
        });
    }

    private void BuildCodeTable(HuffmanNode? node, string currentCode, Dictionary<char, string> codes) {
        if (node == null) {
            return;
        }

        // leaf node means we found an actual character.
        if (node.IsLeaf()) {

            // special case: if the input only has one unique character,
            // give it the code "0" instead of an empty code.
            if (currentCode.Length == 0) {
                currentCode = "0";
            }

            if (node.Character.HasValue) {
                codes[node.Character.Value] = currentCode;
            }

            return;
        }

        // left branch gets 0, right branch gets 1. DUH
        BuildCodeTable(node.Left, currentCode + "0", codes);
        BuildCodeTable(node.Right, currentCode + "1", codes);
    }

    private string EncodeInput(string input, Dictionary<char, string> codes) {
        StringBuilder encodedBuilder = new StringBuilder();

        foreach (char currentCharacter in input) {
            encodedBuilder.Append(codes[currentCharacter]);
        }

        return encodedBuilder.ToString();
    }

    // Builds a SPADE-parseable certificate: a set of (asciiCode,code) tuples
    // (the code table), paired with the resulting bitstring -- e.g.
    // "({(97,0),(98,10),(99,11)},01011)".
    private string FormatSolution(Dictionary<char, string> codes, string encodedText) {
        StringBuilder solutionBuilder = new StringBuilder();

        solutionBuilder.Append("({");

        List<char> characters = new List<char>(codes.Keys);
        characters.Sort();

        for (int i = 0; i < characters.Count; i++) {
            char currentCharacter = characters[i];

            if (i > 0) {
                solutionBuilder.Append(",");
            }

            solutionBuilder.Append("(");
            solutionBuilder.Append((int)currentCharacter);
            solutionBuilder.Append(",");
            solutionBuilder.Append(codes[currentCharacter]);
            solutionBuilder.Append(")");
        }

        solutionBuilder.Append("},");
        solutionBuilder.Append(encodedText);
        solutionBuilder.Append(")");

        return solutionBuilder.ToString();
    }

    //  small helper class for the Huffman tree 

    private class HuffmanNode {
        // The node's id in the Code Tree picture: c1, c2, ... for characters in character order, n1, n2, ... for merges.
        public string Id { get; set; } = "";
        public char? Character { get; set; }
        public int Frequency { get; set; }
        public int LowestCharacterValue { get; set; }
        public int SerialNumber { get; set; }
        public HuffmanNode? Left { get; set; }
        public HuffmanNode? Right { get; set; }

        public int Leaves => Character.HasValue ? 1 : (Left?.Leaves ?? 0) + (Right?.Leaves ?? 0);

        public bool IsLeaf() {
            return Left == null && Right == null;
        }
    }
}
