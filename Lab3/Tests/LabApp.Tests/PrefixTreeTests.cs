using LabApp.Core;
using Xunit;

public class PrefixTreeTests
{
    [Fact] public void ParseSample_BuildsTree()
    {
        var tree = PrefixParser.ParsePrefix("ABD*G***CE**FH**J**");
        Assert.NotNull(tree.Root);
        Assert.Equal('A', tree.Root!.Value);
        Assert.Equal('B', tree.Root.Left!.Value);
        Assert.Equal('D', tree.Root.Left!.Left!.Value);
        Assert.Equal('G', tree.Root.Left!.Left!.Right!.Value);
        Assert.Equal('C', tree.Root.Right!.Value);
        Assert.Equal('E', tree.Root.Right!.Left!.Value);
        Assert.Equal('F', tree.Root.Right!.Right!.Value);
    }

    [Fact] public void Parse_NullTree()
    {
        var tree = PrefixParser.ParsePrefix("**");
        Assert.Null(tree.Root);
    }
}