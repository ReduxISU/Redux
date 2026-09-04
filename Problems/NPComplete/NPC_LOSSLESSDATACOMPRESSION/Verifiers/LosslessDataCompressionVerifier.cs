using System;
using System.Collections.Generic;
using System.Linq;
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Verifiers {
    class LosslessDataCompressionVerifier : IVerifier<LOSSLESSDATACOMPRESSION> {
        public string verifierName { get; } = "Lossless Data Compression Verifier";
        public string verifierDefinition { get; } = "Verifies a proposed encoding by checking prefix-free property, decoding the bitstring, and comparing with original input.";
        public string source { get; } = "Sayood, K. (2018). Introduction to data compression (5th ed.). Morgan Kaufmann.";
        public string sourceLink { get; } = "https://www.vitalsource.com/products/introduction-to-data-compression-khalid-sayood-v9780128097052?srsltid=AfmBOoqEi_U3xj4PdBt2TaKZYgScGWnKA-v0OVyiworUKPYHJT0RWvPQ";
        public string[] contributors { get; } = { "Bektur Akkabakov", "Prem Shah" };

        private string _certificate = "";
        public string certificate {
            get { return _certificate; }
        }

        public LosslessDataCompressionVerifier() { }

        public bool verify(LOSSLESSDATACOMPRESSION problem, string certificate) {
            try {
                _certificate = certificate;

                var (codeTable, encodedText) = ParseCertificate(certificate);

                if (!IsPrefixFree(codeTable))
                    return false;

                string decoded = Decode(encodedText, codeTable);

                return decoded == problem.instance;
            } catch {
                return false;
            }
        }

        // parsing (UPDATED FORMAT)
        private (Dictionary<char, string>, string) ParseCertificate(string certificate) {
            int closeParen = certificate.IndexOf(')');
            if (closeParen == -1)
                throw new Exception("Invalid certificate format");

            string codesContent = certificate.Substring(1, closeParen - 1);
            string rest = certificate.Substring(closeParen + 1).Trim();

            if (!rest.StartsWith("encoded:"))
                throw new Exception("Missing encoded part");

            string encoded = rest.Substring("encoded:".Length);

            Dictionary<char, string> table = new Dictionary<char, string>();

            foreach (var pair in codesContent.Split(';')) {
                if (string.IsNullOrWhiteSpace(pair)) continue;

                string[] kv = pair.Split('=');
                if (kv.Length != 2)
                    throw new Exception("Invalid pair");

                int ascii = int.Parse(kv[0]);
                string code = kv[1];

                table[(char)ascii] = code;
            }

            if (table.Count == 0)
                throw new Exception("Empty code table");

            return (table, encoded);
        }

        // prefix-free check
        private bool IsPrefixFree(Dictionary<char, string> table) {
            var codes = table.Values.ToList();

            for (int i = 0; i < codes.Count; i++) {
                for (int j = 0; j < codes.Count; j++) {
                    if (i == j) continue;

                    if (codes[j].StartsWith(codes[i]))
                        return false;
                }
            }

            return true;
        }

        // decode
        private string Decode(string encoded, Dictionary<char, string> table) {
            Dictionary<string, char> reverse = table.ToDictionary(kv => kv.Value, kv => kv.Key);

            string current = "";
            var result = new List<char>();

            foreach (char bit in encoded) {
                current += bit;

                if (reverse.ContainsKey(current)) {
                    result.Add(reverse[current]);
                    current = "";
                }
            }

            if (current.Length != 0)
                throw new Exception("Invalid encoding");

            return new string(result.ToArray());
        }
    }
}