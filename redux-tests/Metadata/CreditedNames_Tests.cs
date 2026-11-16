using Xunit;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace redux_tests;
#pragma warning disable CS1591

// The weekly contributor scan (Tools/ContributorStatsSync) adds an entry for every name credited in
// a `contributors` list under Problems/. A list written in a form its pattern doesn't read is
// skipped without a word, so these tests pin every form the codebase uses.
[Trait("Category", "Metadata")]
public class CreditedNames_Tests {

    [Theory]
    [InlineData("public string[] contributors { get; } = { \"Ada Lovelace\", \"Alan Turing\" };")]
    [InlineData("public string[] contributors { get; set; } = new string[] { \"Ada Lovelace\", \"Alan Turing\" };")]
    [InlineData("public List<string> contributors { get; } = new List<string> { \"Ada Lovelace\", \"Alan Turing\" };")]
    [InlineData("public string[] contributors => new[] { \"Ada Lovelace\", \"Alan Turing\" };")]
    [InlineData("public string[] contributors => [\"Ada Lovelace\", \"Alan Turing\"];")]
    [InlineData("public string[] contributors { get; } = [\"Ada Lovelace\", \"Alan Turing\"];")]
    [InlineData("public string[] contributors { get; } =\n    [\n        \"Ada Lovelace\",\n        \"Alan Turing\",\n    ];")]
    public void CreditedNames_ReadsEveryListForm(string source) {
        Assert.Equal(new[] { "Ada Lovelace", "Alan Turing" }, ContributorStatsSync.Program.CreditedNames(source));
    }

    [Fact]
    public void CreditedNames_SkipsPlaceholdersAndNonNames() {
        var source = "public string[] contributors => [\"TODO\", \"Author Unknown\", \"A, B\", \"A & B\", \"Ada Lovelace\"];";
        Assert.Equal(new[] { "Ada Lovelace" }, ContributorStatsSync.Program.CreditedNames(source));
    }

    // Every `contributors` declaration under Problems/ that credits anyone must yield at least one
    // name. Lists holding only placeholders ("TODO", "Author Unknown") credit nobody, so they're left out.
    [Fact]
    public void CreditedNames_ReadsEveryDeclarationUnderProblems() {
        var declaration = new Regex(@"\bcontributors\s*(?:\{[^}]*\}\s*)?=>?[^;]*;", RegexOptions.Singleline);
        var unread = Directory.EnumerateFiles(Path.Combine(ProjectSourcePath.Value, "Problems"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(file => declaration.Matches(File.ReadAllText(file))
                .Where(m => Regex.Matches(m.Value, "\"([^\"]*)\"").Any(q => q.Groups[1].Value is not ("TODO" or "Author Unknown")))
                .Where(m => !ContributorStatsSync.Program.CreditedNames(m.Value).Any())
                .Select(m => $"{Path.GetRelativePath(ProjectSourcePath.Value, file)}: {m.Value.Split('\n')[0].Trim()}"))
            .ToList();

        Assert.True(unread.Count == 0,
            "The contributor scan reads no names from these contributors lists:\n" + string.Join("\n", unread));
    }
}
