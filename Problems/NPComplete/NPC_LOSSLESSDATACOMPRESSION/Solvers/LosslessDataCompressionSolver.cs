using API.Interfaces;
using System.Text;

namespace API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Solvers;

class LosslessDataCompressionSolver : ISolver<LOSSLESSDATACOMPRESSION> {

    // --- Fields ---

    public string solverName { get; } = "Huffman Encoding Solver";

    public string solverDefinition { get; } =
        "Solves the Lossless Data Compression problem using Huffman Encoding. " +
        "The solver counts character frequencies, builds a Huffman tree, creates prefix-free binary codes, " +
        "and then encodes the original input string.";

    public string source { get; } =
        "https://www.ias.ac.in/article/fulltext/reso/011/02/0091-0099";

    public string[] contributors { get; } = { "Prem Shah", "Bektur Akkabakov" };

    public bool timerHasExpired { get; set; }
    // Declared, not derived. Huffman coding: irrevocable locally-optimal choice each step.
    public SolverType solverType { get; } = SolverType.Greedy;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // Declared, not derived. This is Huffman coding, but BuildHuffmanTree fully
    // re-sorts the remaining node list on every merge instead of using a heap, so tree
    // construction is O(k^2 log k) rather than the classical O(n log n) heap-based bound
    // (k = distinct characters, k <= n). EncodeInput adds O(n * k) (code length <= k-1).
    public string complexity { get; } = "O(k^2 log k + n*k), n = input length, k <= n = distinct characters";

    // Constructors

    public LosslessDataCompressionSolver() {

    }

    //  Main solver method 

    public string solve(LOSSLESSDATACOMPRESSION problem) {

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
            return problem.solution;
        }

        // first: count how many times each character appears.
        Dictionary<char, int> frequencies = BuildFrequencyTable(input);

        if (timerHasExpired) {
            return "timeout";
        }

        // second: build the Huffman tree from the frequencies.
        HuffmanNode root = BuildHuffmanTree(frequencies);

        if (timerHasExpired) {
            return "timeout";
        }

        // thurd: Walk the tree and create binary codes.
        Dictionary<char, string> codes = new Dictionary<char, string>();
        BuildCodeTable(root, "", codes);

        if (timerHasExpired) {
            return "timeout";
        }

        // fourth: encodes the original input using the code table.
        string encodedText = EncodeInput(input, codes);

        // store results on the problem object so the verifier/GUI can use them.
        problem.frequencies = frequencies;
        problem.codes = codes;
        problem.encodedText = encodedText;


        problem.solution = FormatSolution(codes, encodedText);

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

    private HuffmanNode BuildHuffmanTree(Dictionary<char, int> frequencies) {
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

            nodes.Add(parent);
        }

        return nodes[0];
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
        public char? Character { get; set; }
        public int Frequency { get; set; }
        public int LowestCharacterValue { get; set; }
        public int SerialNumber { get; set; }
        public HuffmanNode? Left { get; set; }
        public HuffmanNode? Right { get; set; }

        public bool IsLeaf() {
            return Left == null && Right == null;
        }
    }
}
