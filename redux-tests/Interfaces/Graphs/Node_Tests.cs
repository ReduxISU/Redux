//Node_Tests.cs
using Xunit;
using API.Interfaces.Graphs;

namespace redux_tests;
#pragma warning disable CS1591

public class Node_Tests {
    [Fact]
    public void Node_Default_Constructor_Sets_Default_Name() {
        var node = new Node();
        Assert.Equal("DEFAULT", node.name);
    }

    [Fact]
    public void Node_String_Constructor_Sets_Given_Name() {
        var node = new Node("A");
        Assert.Equal("A", node.name);
    }

    [Fact]
    public void Node_Name_Setter_Mutates_Value() {
        var node = new Node("A");
        node.name = "B";
        Assert.Equal("B", node.name);
    }

    [Fact]
    public void Node_ToString_Returns_Current_Name() {
        var node = new Node("A");
        Assert.Equal("A", node.ToString());
        node.name = "B";
        Assert.Equal("B", node.ToString());
    }

    [Fact]
    public void Node_Clone_Returns_Distinct_Object_With_Same_Name() {
        var original = new Node("A");
        var clone = (Node)original.Clone();

        Assert.False(ReferenceEquals(original, clone));
        Assert.Equal(original.name, clone.name);
    }

    [Fact]
    public void Node_Clone_Mutation_Does_Not_Affect_Original() {
        var original = new Node("A");
        var clone = (Node)original.Clone();

        clone.name = "B";

        Assert.Equal("A", original.name);
        Assert.Equal("B", clone.name);
    }
}
