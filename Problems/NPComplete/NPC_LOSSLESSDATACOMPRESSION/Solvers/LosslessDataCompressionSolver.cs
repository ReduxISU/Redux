using API.Interfaces;
using API.Interfaces.CodeTree;
using API.Interfaces.Steps;
using System.Text;

namespace API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Solvers;

// The steps Huffman's Algorithm records, for the Code Tree picture:
//   - an Update for the count: each character starts as its own one-node tree, weighted by how often it appears;
//   - an Update per merge of the two lightest trees into a new parent (the three nodes in focus);
//   - an Accept once the codes are read off the finished tree;
//   - the Done, with the verifier's verdict and how much smaller the bits are than 8 bits a character.
// Nothing is judged before the codes are read, so the count and the merges are Updates.
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

    // How the steps show the tree being built, given by how many merges are made so far.
    private static RunSteps<int, HuffmanForest> NewSteps(StepRecorder<HuffmanForest> rec, List<HuffmanMerge> merges) => new(rec,
        // What every picture shows: the merges made so far, and the two trees of the last one with their new parent.
        partial: made => new HuffmanForest(merges.Take(made), []),
        focus: made => made == 0 ? [] : [merges[made - 1].Left, merges[made - 1].Right, merges[made - 1].Node]);

    public string solve(LOSSLESSDATACOMPRESSION problem) => Solve(problem, StepRecorder<HuffmanForest>.Off);

    public string Solve(LOSSLESSDATACOMPRESSION problem, StepRecorder<HuffmanForest> rec) {

        // always check the timer first.
        if (timerHasExpired) {
            return SolveRun.TimedOutAnswer;
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
        var merges = new List<HuffmanMerge>();
        var steps = NewSteps(rec, merges);
        if (input.Length == 0) {
            problem.solution = "()";
            // The verifier's verdict, for the Done only (worked out only while recording).
            bool okEmpty = rec.Enabled && problem.defaultVerifier.verify(problem, problem.solution);
            steps.Done(new HuffmanForest([], []), okEmpty, $"The text is empty, so there is nothing to encode.");
            return problem.solution;
        }

        // first: count how many times each character appears.
        Dictionary<char, int> frequencies = BuildFrequencyTable(input);

        if (timerHasExpired) {
            return SolveRun.TimedOutAnswer;
        }

        // second: build the Huffman tree from the frequencies.
        if (frequencies.Count == 1)
            steps.Update(0, $"Count each character: {input.Length:N0} {(input.Length == 1 ? "character" : "characters")}, all {CodeTreeFrames.Label(input[0])}. A single tree needs no merging.");
        else
            steps.Update(0, $"Count each character: {input.Length:N0} {(input.Length == 1 ? "character" : "characters")}, {frequencies.Count} different. Each starts as its own one-node tree, weighted by how often it appears.");
        HuffmanNode root = BuildHuffmanTree(frequencies, merges, rec, steps);

        if (timerHasExpired) {
            return SolveRun.TimedOutAnswer;
        }

        // thurd: Walk the tree and create binary codes.
        Dictionary<char, string> codes = new Dictionary<char, string>();
        BuildCodeTable(root, "", codes);

        if (timerHasExpired) {
            return SolveRun.TimedOutAnswer;
        }

        // The codes by leaf id: characters in character order are c1, c2, ...
        HuffmanForest Forest() {
            var sorted = new List<char>(codes.Keys);
            sorted.Sort();
            return new HuffmanForest(merges, sorted.Select((c, i) => new KeyValuePair<string, string>(CodeTreeFrames.LeafId(i), codes[c])));
        }
        if (frequencies.Count == 1)
            steps.Accept(merges.Count, $"Only one character, so its code is just 0.", focus: () => [], partial: Forest);
        else
            steps.Accept(merges.Count, $"Read each code from the top: going left adds 0, going right adds 1. The most frequent characters sit near the top and get the shortest codes.", focus: () => [], partial: Forest);

        // fourth: encodes the original input using the code table.
        string encodedText = EncodeInput(input, codes);

        // store results on the problem object so the verifier/GUI can use them.
        problem.frequencies = frequencies;
        problem.codes = codes;
        problem.encodedText = encodedText;


        problem.solution = FormatSolution(codes, encodedText);

        long fixedBits = 8L * input.Length;
        // The verifier's verdict, for the Done only (worked out only while recording: a plain solve needs no second check).
        bool ok = rec.Enabled && problem.defaultVerifier.verify(problem, problem.solution);
        steps.Done(Forest(), ok, $"Encoded: {encodedText.Length:N0} {(encodedText.Length == 1 ? "bit" : "bits")}, versus {fixedBits:N0} bits at 8 bits per character, {Math.Round((1 - (double)encodedText.Length / fixedBits) * 100)}% smaller. No code is the start of another, so the bits decode back to exactly the text.");

        return problem.solution;
    }

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

    // Huffman's algorithm: every character starts as a one-node tree weighted by its count; while more than one tree is
    // left, the two lightest are merged under a new parent whose weight is their sum.
    private HuffmanNode BuildHuffmanTree(Dictionary<char, int> frequencies, List<HuffmanMerge> merges, StepRecorder<HuffmanForest> rec,
                                         RunSteps<int, HuffmanForest> steps) {
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
            if (timerHasExpired) break; // stop at the time limit; solve() reports it
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

            // A tie: a tree left weighs as much as the heavier of the two, and lost on the earliest character. The
            // algorithm never asks, so it is worked out only while recording.
            bool tie = rec.Enabled && nodes.Count > 0 && nodes.Min(n => n.Frequency) == right.Frequency;
            merges.Add(new HuffmanMerge(parent.Id, left.Id, right.Id, parent.Frequency));
            if (tie)
                steps.Update(merges.Count, $"Merge {Describe(left)} and {Describe(right)}, the two lightest trees left, into a node of weight {parent.Frequency}. (A tie: the tree holding the earliest character goes first.)");
            else
                steps.Update(merges.Count, $"Merge {Describe(left)} and {Describe(right)}, the two lightest trees left, into a node of weight {parent.Frequency}.");

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
        string which = chars.Count > 4 ? $"{node.Leaves} characters" : Words.List(chars);
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
