using Nexus.Gateway.Common;
using Xunit;

namespace Nexus.Gateway.Tests;

public sealed class MoneyPrecisionHttpTests
{
    [Theory]
    [InlineData("100.00", 100.00)]
    [InlineData("0.50", 0.50)]
    [InlineData("1250000", 1250000.00)]
    [InlineData("99.99", 99.99)]
    public void Valid_positive_money_with_two_decimals_succeeds(string text, decimal expected)
    {
        Assert.True(MoneyPrecision.TryParsePositive(text, out var amount));
        Assert.Equal(expected, amount);
        Assert.Equal(expected, MoneyPrecision.EnsurePositive(text));
    }

    [Theory]
    [InlineData("0.001")]
    [InlineData("1.005")]
    [InlineData("10.1234")]
    [InlineData("0")]
    [InlineData("-50.00")]
    [InlineData("not-a-number")]
    [InlineData(null)]
    public void Invalid_money_precision_or_negative_fails(string? text)
    {
        Assert.False(MoneyPrecision.TryParsePositive(text, out _));
        var ex = Assert.Throws<GatewayInputException>(() => MoneyPrecision.EnsurePositive(text));
        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
    }
}
