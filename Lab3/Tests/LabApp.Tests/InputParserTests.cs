using LabApp.Core;
using Xunit;

public class InputParserTests
{
    [Fact] public void Parse_SampleInput_ReturnsCommands()
    {
        var result = InputParser.Parse("3 4 1,56 1,7 1,cat 2 5 4");
        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(8, result.Value.Count);
        Assert.Equal(CommandKind.TopOrPeek, result.Value[0].Kind);
        Assert.Equal(CommandKind.IsEmpty, result.Value[1].Kind);
        Assert.Equal(CommandKind.PushOrEnqueue, result.Value[2].Kind);
        Assert.Equal("56", result.Value[2].Argument!.Raw);
        Assert.Equal("cat", result.Value[4].Argument!.Raw);
    }

    [Fact] public void Parse_Empty_ReturnsEmpty()
    {
        var result = InputParser.Parse("");
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact] public void Parse_InvalidCommand_Fails()
    {
        var result = InputParser.Parse("9");
        Assert.False(result.IsSuccess);
        Assert.Contains("неизвестная команда", result.Error);
    }

    [Fact] public void Parse_NonOneWithArgument_Fails()
    {
        var result = InputParser.Parse("2,foo");
        Assert.False(result.IsSuccess);
    }
}