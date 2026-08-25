//Edge_Tests.cs
using Xunit;
using API.Interfaces.Graphs;

namespace redux_tests;
#pragma warning disable CS1591

public class Edge_Tests {
    [Fact]
    public void Edge_Constructor_Sets_Source_And_Target() {
        var source = new Node("A");
        var target = new Node("B");
        var edge = new Edge(source, target);

        Assert.Same(source, edge.source);
        Assert.Same(target, edge.target);
    }

    [Fact]
    public void Edge_Weight_Defaults_To_Zero() {
        var edge = new Edge(new Node("A"), new Node("B"));
        Assert.Equal(0, edge.weight);
    }

    [Fact]
    public void Edge_Weight_Setter_Is_Mutable() {
        var edge = new Edge(new Node("A"), new Node("B"));
        edge.weight = 5;
        Assert.Equal(5, edge.weight);
    }

    [Fact]
    public void Edge_ToString_Returns_Source_Comma_Target() {
        var edge = new Edge(new Node("A"), new Node("B"));
        Assert.Equal("A,B", edge.ToString());
    }

    [Fact]
    public void Edge_UndirectedString_Wraps_In_Braces() {
        var edge = new Edge(new Node("A"), new Node("B"));
        Assert.Equal("{A,B}", edge.undirectedString());
    }

    [Fact]
    public void Edge_DirectedString_Wraps_In_Parens() {
        var edge = new Edge(new Node("A"), new Node("B"));
        Assert.Equal("(A,B)", edge.directedString());
    }

    [Fact]
    public void Edge_ToKVP_Returns_Source_Key_Target_Value() {
        var edge = new Edge(new Node("A"), new Node("B"));
        var kvp = edge.toKVP();

        Assert.Equal("A", kvp.Key);
        Assert.Equal("B", kvp.Value);
    }

    [Fact]
    public void Edge_CompareTo_Null_Returns_One() {
        var edge = new Edge(new Node("A"), new Node("B"));
        Assert.Equal(1, edge.CompareTo(null));
    }

    [Fact]
    public void Edge_CompareTo_Identical_Source_And_Target_Names_Is_Zero() {
        var edge1 = new Edge(new Node("A"), new Node("B"));
        var edge2 = new Edge(new Node("A"), new Node("B"));

        Assert.Equal(0, edge1.CompareTo(edge2));
    }

    [Fact]
    public void Edge_CompareTo_Different_Source_Name_Matches_Key_Comparison_Sign() {
        var edge1 = new Edge(new Node("A"), new Node("B"));
        var edge2 = new Edge(new Node("C"), new Node("B"));

        int expectedSign = System.Math.Sign("A".CompareTo("C"));
        int actualSign = System.Math.Sign(edge1.CompareTo(edge2));

        Assert.NotEqual(0, actualSign);
        Assert.Equal(expectedSign, actualSign);
    }

    [Fact]
    public void Edge_CompareTo_Different_Target_Name_With_Same_Source_Matches_Value_Comparison_Sign() {
        var edge1 = new Edge(new Node("A"), new Node("B"));
        var edge2 = new Edge(new Node("A"), new Node("D"));

        int expectedSign = System.Math.Sign("B".CompareTo("D"));
        int actualSign = System.Math.Sign(edge1.CompareTo(edge2));

        Assert.NotEqual(0, actualSign);
        Assert.Equal(expectedSign, actualSign);
    }
}
