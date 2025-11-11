using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography.Xml;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_CLIQUE.Inherited;
using API.Problems.NPComplete.NPC_SAT3;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.VisualBasic;

namespace API.Interfaces.JSON_Objects;

class API_SAT : API_JSON
{
    public List<Clause> clauses { get; set; }
    public API_SAT(SAT3 instance)
    {
        clauses = instance.clauses.Select((c, i) => new Clause(c, i.ToString())).ToList();
    }

    public class Clause
    {
        public string id { get; set; }
        public List<Literal> literals { get; set; }

        public Clause(List<string> _literals, string _id)
        {
            id = _id;
            literals = _literals.Select((s, i) => new Literal(s, id.ToString() + "-" + i.ToString())).ToList();
        }
    }

    public class Literal
    {
        public string id { get; set; }
        public string literal { get; set; }
        public string color { get; set; }

        public Literal(string _literal, string _id)
        {
            id = _id;
            literal = _literal;
            color = "Background";
        }
    }
}

