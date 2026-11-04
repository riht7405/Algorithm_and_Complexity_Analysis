using LabApp.Core;
using Xunit;

public class ExpressionTests
{
    [Fact] public void Postfix_Sample_Returns3()
    {
        var tokens = Tokenizer.Tokenize("7 8 + 3 2 + /");
        Assert.True(tokens.IsSuccess, tokens.Error);
        var value = PostfixEvaluator.Evaluate(tokens.Value);
        Assert.True(value.IsSuccess, value.Error);
        Assert.Equal(3, value.Value, 3);
    }

    [Fact] public void Postfix_Add_Returns4()
    {
        var value = PostfixEvaluator.Evaluate(Tokenizer.Tokenize("2 2 +").Value);
        Assert.True(value.IsSuccess);
        Assert.Equal(4, value.Value, 3);
    }

    [Fact] public void Postfix_DivByZero_Fails()
    {
        var value = PostfixEvaluator.Evaluate(Tokenizer.Tokenize("1 0 /").Value);
        Assert.False(value.IsSuccess);
    }

    [Fact] public void Postfix_SqrtOfNegative_Fails()
    {
        var value = PostfixEvaluator.Evaluate(Tokenizer.Tokenize("-1 sqrt").Value);
        Assert.False(value.IsSuccess);
    }

    [Fact] public void ShuntingYard_Simple()
    {
        var postfix = ShuntingYard.ToPostfix(Tokenizer.Tokenize("2+2").Value);
        Assert.True(postfix.IsSuccess);
        Assert.Equal(new[] { "2", "2", "+" }, postfix.Value.Select(t => t.Lexeme).ToArray());
    }

    [Fact] public void ShuntingYard_Precedence()
    {
        var postfix = ShuntingYard.ToPostfix(Tokenizer.Tokenize("7+8*3").Value);
        Assert.Equal(new[] { "7", "8", "3", "*", "+" }, postfix.Value.Select(t => t.Lexeme).ToArray());
    }

    [Fact] public void ShuntingYard_Parens()
    {
        var postfix = ShuntingYard.ToPostfix(Tokenizer.Tokenize("(2+2)*3").Value);
        Assert.Equal(new[] { "2", "2", "+", "3", "*" }, postfix.Value.Select(t => t.Lexeme).ToArray());
    }

    [Fact] public void ShuntingYard_RightAssocPow()
    {
        var postfix = ShuntingYard.ToPostfix(Tokenizer.Tokenize("2^3^2").Value);
        Assert.Equal(new[] { "2", "3", "2", "^", "^" }, postfix.Value.Select(t => t.Lexeme).ToArray());
    }

    [Fact] public void ShuntingYard_UnbalancedParens_Fails()
    {
        var postfix = ShuntingYard.ToPostfix(Tokenizer.Tokenize("(2+2").Value);
        Assert.False(postfix.IsSuccess);
    }

    [Fact] public void Roundtrip_Infix_Postfix_Evaluates()
    {
        var infix = Tokenizer.Tokenize("(7+8)/(3+2)").Value;
        var postfix = ShuntingYard.ToPostfix(infix).Value;
        var value = PostfixEvaluator.Evaluate(postfix);
        Assert.True(value.IsSuccess, value.Error);
        Assert.Equal(3, value.Value, 3);
    }
}