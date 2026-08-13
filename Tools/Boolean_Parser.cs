using System.Text.RegularExpressions;

namespace API.Tools.Boolean_Parser;

//Simple class to parse a Boolean string. 
//Use is to create an Boolean_Parser with you Boolean string and call the functions on it.
#pragma warning disable CS1591

public class Boolean_Parser {

    //Constructor
    public Boolean_Parser(string booleanString) {
        this.booleanString = booleanString;
    }

    public string booleanString;

    // Returns a list that contains lists of each individual clause
    public List<List<string>> getClause() {
        List<List<string>> clauses = new List<List<string>>();

        // Removing unnessassary characters
        booleanString = booleanString.Replace("(", "").Replace(")", "").Replace(" ", "");

        string[] splitClauses = booleanString.Split('&');

        foreach (string singleClause in splitClauses) {
            List<String> currentClause = new List<string>();
            string[] literals = singleClause.Split("|");
            foreach (string currentLiteral in literals) {
                currentClause.Add(currentLiteral);
            }
            clauses.Add(currentClause);
        }

        int i = 0;
        foreach (List<string> curList in clauses) {
            i++;
        }
        return clauses;

    }


    // Gets all the diffrent literals (A, B, C etc.)
    // This will count the literals twice(e.g A, B, C, A) could be returned since it doesn't remove duplicates.
    public List<string> getLiterals() {
        booleanString = booleanString.Replace("(", "").Replace(")", "").Replace(" ", "");
        string[] split1 = booleanString.Split(new string[] { "|&" }, StringSplitOptions.None);
        string[] split2 = Regex.Split(booleanString, "[|]|[&]+");

        List<string> literalList = new List<string>();
        foreach (string literal in split2) {
            literalList.Add(literal);
        }

        return literalList;
    }

    // Setter to change the input string.
    public void setBooleanString(string boolString) {
        booleanString = boolString;
    }

}