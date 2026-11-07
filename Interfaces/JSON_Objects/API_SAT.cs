using API.Interfaces.Logic;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography.Xml;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_SAT3;
using API.Problems.NPComplete.NPC_SAT;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.VisualBasic;

namespace API.Interfaces.JSON_Objects;

class API_SAT : API_JSON {
    /// <summary>Discriminator so a client can tell payload shapes apart without duck-typing. See #524.</summary>
    public string kind { get; } = "sat";

    /// <summary>
    /// The clauses in formula order. Clause i has id <c>c{i}</c> and its j-th literal <c>c{i}-{j}</c>, the ids the Grouped Graph
    /// picture and every gadget out of a SAT or 3SAT formula use, so a gadget's ids name pieces of this picture too.
    /// </summary>
    public List<Clause> clauses { get; set; }
    public API_SAT(List<Clause> _clauses) {
        clauses = _clauses;
    }
    public API_SAT(SAT3 instance) {
        clauses = instance.clauses.Select((c, i) => new Clause(c, i)).ToList();
    }

    public API_SAT(SAT instance) {
        clauses = instance.clauses.Select((c, i) => new Clause(c, i)).ToList();
    }

    public class Clause {
        public string id { get; set; }
        public List<Literal> literals { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public long? weight { get; set; }

        public Clause(List<string> _literals, int index, long? _weight = null) {
            id = CnfPieces.ClauseId(index);
            literals = _literals.Select((s, j) => new Literal(s, CnfPieces.LiteralId(index, j))).ToList();
            weight = _weight;
        }
    }

    public class Literal {
        public string id { get; set; }
        public string literal { get; set; }
        public string color { get; set; }

        public Literal(string _literal, string _id) {
            id = _id;
            literal = _literal;
            color = "Background";
        }
    }
}

