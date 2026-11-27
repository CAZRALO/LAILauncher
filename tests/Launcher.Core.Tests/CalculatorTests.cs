using Launcher.Core.Utilities;
using Xunit;

namespace Launcher.Core.Tests;

public class CalculatorTests
{
    [Theory]
    [InlineData("2 + 2", 4.0)]
    [InlineData("10 - 3 * 2", 4.0)]
    [InlineData("(5 + 5) * 3", 30.0)]
    [InlineData("100 / 4", 25.0)]
    [InlineData("5 / 2", 2.5)]
    [InlineData("10 / 2 / 5", 1.0)]
    [InlineData("2 ^ 8", 256.0)]
    [InlineData("sqrt(144)", 12.0)]
    [InlineData("50 * 20%", 10.0)]
    [InlineData("20%", 0.2)]
    [InlineData("20% * 50", 10.0)]
    [InlineData("(10 + 20)%", 0.3)]
    [InlineData("100 * (1 - 20%)", 80.0)]
    [InlineData("20% of 500", 100.0)]
    [InlineData("calc 50 * 20%", 10.0)]
    [InlineData("= 50 * 20%", 10.0)]
    [InlineData("10 mod 3", 1.0)]
    [InlineData("10 % 3", 1.0)]
    public void TryEvaluate_ValidMath_CalculatesCorrectly(string expression, double expected)
    {
        var success = CalculatorEvaluator.TryEvaluate(expression, out var result, out var formatted);
        Assert.True(success);
        Assert.Equal(expected, result, 3);
        Assert.False(string.IsNullOrWhiteSpace(formatted));
    }

    [Theory]
    [InlineData("0xFF to dec", 255.0, "255")]
    [InlineData("255 to hex", 255.0, "0xFF")]
    [InlineData("0b1010 to dec", 10.0, "10")]
    [InlineData("10 to bin", 10.0, "0b1010")]
    public void TryEvaluate_BaseConversion_CalculatesCorrectly(string expression, double expectedVal, string expectedFormatted)
    {
        var success = CalculatorEvaluator.TryEvaluate(expression, out var result, out var formatted);
        Assert.True(success);
        Assert.Equal(expectedVal, result, 3);
        Assert.Equal(expectedFormatted, formatted);
    }

    [Theory]
    [InlineData("1000 m to km", 1.0)]
    [InlineData("1024 mb to gb", 1.0)]
    [InlineData("1 gb to mb", 1024.0)]
    [InlineData("0 c to f", 32.0)]
    [InlineData("100 c to f", 212.0)]
    [InlineData("2 kg to g", 2000.0)]
    [InlineData("60 s to min", 1.0)]
    public void TryEvaluate_UnitConversion_CalculatesCorrectly(string expression, double expected)
    {
        var success = CalculatorEvaluator.TryEvaluate(expression, out var result, out var formatted);
        Assert.True(success);
        Assert.Equal(expected, result, 2);
        Assert.False(string.IsNullOrWhiteSpace(formatted));
    }

    [Theory]
    [InlineData("notepad")]
    [InlineData("hello world")]
    [InlineData("just a word")]
    public void TryEvaluate_NonMath_ReturnsFalse(string text)
    {
        var success = CalculatorEvaluator.TryEvaluate(text, out _, out _);
        Assert.False(success);
    }
}
