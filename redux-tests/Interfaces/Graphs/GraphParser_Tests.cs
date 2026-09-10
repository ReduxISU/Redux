using System;
using System.Collections.Generic;
using Xunit;
using API.Interfaces.Graphs.GraphParser;

namespace redux_tests;
#pragma warning disable CS1591

public class GraphParser_Tests {

    [Theory]
    [InlineData("{a,b,c}", new string[] { "a", "b", "c" })]
    [InlineData("{a}", new string[] { "a" })]
    [InlineData("{node1,node2}", new string[] { "node1", "node2" })]
    public void GetNodesFromNodeListString_ParsesNodeList(string input, string[] expected) {
        var parser = new GraphParser();
        List<string> result = parser.getNodesFromNodeListString(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no braces here")]
    public void GetNodesFromNodeListString_ThrowsOnInvalidInput(string input) {
        var parser = new GraphParser();
        Assert.Throws<ArgumentException>(() => parser.getNodesFromNodeListString(input));
    }

    [Fact]
    public void ParseNodeListWithStringFunctions_ParsesSimpleNodeList() {
        List<string> result = GraphParser.parseNodeListWithStringFunctions("{a,b,c}");
        Assert.Equal(new List<string> { "a", "b", "c" }, result);
    }

    [Fact]
    public void ParseNodeListWithStringFunctions_FlattensFullGraphStringPerDocumentedGotcha() {
        // The method's own XML doc comment warns that this method does not validate structure -
        // it is a pure string replace/split, so feeding it a full graph string flattens everything.
        // Note: the doc comment's example input contains a stray unmatched '(' character
        // (the source string is: {{a,b,c},{(a,b},{b,c},0}) which this method does NOT strip
        // (only '{' and '}' are stripped), so the '(' stays attached to the following "a" token.
        // The actual output is therefore ["a","b","c","(a","b","b","c","0"], not the
        // ["a","b","c","a","b","b","c","0"] the doc comment prose claims - verified directly
        // against the method's Replace/Split implementation.
        List<string> result = GraphParser.parseNodeListWithStringFunctions("{{a,b,c},{(a,b},{b,c},0}");
        Assert.Equal(new List<string> { "a", "b", "c", "(a", "b", "b", "c", "0" }, result);
    }

    [Fact]
    public void ParseDirectedEdgeListWithStringFunctions_ParsesDirectedPairsInOrder() {
        List<KeyValuePair<string, string>> result = GraphParser.parseDirectedEdgeListWithStringFunctions("{(a,b),(c,d)}");
        Assert.Equal(
            new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>("a", "b"),
                new KeyValuePair<string, string>("c", "d")
            },
            result);
    }

    [Fact]
    public void ParseDirectedEdgeListWithStringFunctions_ThrowsWhenElementHasNoSecondPart() {
        Assert.Throws<ArgumentException>(() => GraphParser.parseDirectedEdgeListWithStringFunctions("{(a)}"));
    }

    [Fact]
    public void ParseUndirectedEdgeListWithStringFunctions_ParsesBothDirectionsPerEdge() {
        List<KeyValuePair<string, string>> result = GraphParser.parseUndirectedEdgeListWithStringFunctions("{{a,b},{c,d}}");
        Assert.Equal(
            new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>("a", "b"),
                new KeyValuePair<string, string>("b", "a"),
                new KeyValuePair<string, string>("c", "d"),
                new KeyValuePair<string, string>("d", "c")
            },
            result);
    }

    [Fact]
    public void ParseUndirectedEdgeListWithStringFunctions_ThrowsWhenElementHasNoSecondPart() {
        Assert.Throws<ArgumentException>(() => GraphParser.parseUndirectedEdgeListWithStringFunctions("{{a},{c,d}}"));
    }
}
