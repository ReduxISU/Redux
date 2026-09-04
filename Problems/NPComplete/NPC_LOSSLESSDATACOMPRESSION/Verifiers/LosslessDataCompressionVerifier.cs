using System;
using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using SPADE;

namespace API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Verifiers {
    class LosslessDataCompressionVerifier : IVerifier<LOSSLESSDATACOMPRESSION> {
        // A SPADE-parseable pair: a set of (asciiCode,code) tuples (the prefix-free
        // code table), paired with the resulting bitstring. The previous format --
        // "(97=0;98=10;99=11) encoded:01011" -- used "=" key/value pairs and a bare
        // "encoded:" suffix glued on outside any bracket, neither of which SPADE's
        // set/list grammar can express, so it was hand-parsed with Substring/Split.
        // "()" (an empty pair) is a special-cased certificate for an empty instance.
        public const string CertificateGrammar = "({(asciiCode1,code1),(asciiCode2,code2),...},bitstring) | prefix-free code table paired with S encoded using it; () if S is empty";
        public const string CertificateExample = "({(97,0),(98,10),(99,11)},01011)";

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

        // parsing (SPADE-based)
        private (Dictionary<char, string>, string) ParseCertificate(string certificate) {
            if (certificate.Trim() == "()")
                return (new Dictionary<char, string>(), string.Empty);

            UtilCollection cert = new UtilCollection(certificate);
            cert.assertPair();

            UtilCollection codeTable = cert[0];
            codeTable.assertUnordered();

            Dictionary<char, string> table = new Dictionary<char, string>();
            foreach (UtilCollection entry in codeTable) {
                entry.assertPair();
                int ascii = entry[0].parseInt();
                string code = entry[1].ToString();
                table[(char)ascii] = code;
            }

            if (table.Count == 0)
                throw new Exception("Empty code table");

            string encoded = cert[1].ToString();
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