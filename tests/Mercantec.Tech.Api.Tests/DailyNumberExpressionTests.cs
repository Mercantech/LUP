using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Tests;

public sealed class DailyNumberExpressionTests
{
    private static readonly int[] Tiles = [12, 3, 4, 3, 5, 0];

    [Theory]
    [InlineData("0 + 1 * 2", 24, "12 + 3 * 4")]
    [InlineData("( 0 + 1 ) * 2", 60, "( 12 + 3 ) * 4")]
    [InlineData("0 / 1 + 3", 7, "12 / 3 + 3")]
    [InlineData("1 - 3", 0, "3 - 3")]
    [InlineData("0 / ( 2 - 1 )", 12, "12 / ( 4 - 3 )")]
    public void EvaluatesBricksByIndexWithStandardOperatorPrecedence(string input, int expected, string display)
    {
        Assert.True(DailyNumberExpression.TryEvaluate(Tiles, input.Split(' '), out var value, out var expression));
        Assert.Equal(expected, value);
        Assert.Equal(display, expression);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("( 0 )")]
    [InlineData("0 + 0")]
    [InlineData("1 - 2 + 0")]
    [InlineData("0 / 5")]
    [InlineData("0 / 4")]
    [InlineData("0 ( 1 )")]
    [InlineData("( 0 + 1")]
    [InlineData("0 + )")]
    [InlineData("0 + 6")]
    [InlineData("0 + 12")]
    [InlineData("0 ** 1")]
    public void RejectsMalformedAndNonIntegerExpressions(string input)
    {
        Assert.False(DailyNumberExpression.TryEvaluate(Tiles, input.Split(' '), out _, out _));
    }

    [Fact]
    public void RejectsOversizedResultsAndTokens()
    {
        Assert.False(DailyNumberExpression.TryEvaluate([1000, 1001, 4, 5, 6, 7], ["0", "*", "1"], out _, out _));
        var tooMany = Enumerable.Repeat("(", 16).Concat(["0", "+", "1"]).Concat(Enumerable.Repeat(")", 16)).ToArray();
        Assert.False(DailyNumberExpression.TryEvaluate(Tiles, tooMany, out _, out _));
    }
}
