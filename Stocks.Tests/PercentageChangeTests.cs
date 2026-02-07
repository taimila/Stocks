using NUnit.Framework;
using Stocks.Model;

namespace Stocks.Tests;

public class PercentageChangeTests
{
    [Test]
    public void ChangeBetweenTwoPricesPercentageReturnsPositiveValue()
    {
        var sut = new ChangeBetweenTwoPrices(100, 125);

        var percentage = sut.Percentage;

        Assert.That(percentage, Is.EqualTo(25));
    }

    [Test]
    public void ChangeBetweenTwoPricesPercentageReturnsNegativeValue()
    {
        var sut = new ChangeBetweenTwoPrices(100, 80);

        var percentage = sut.Percentage;

        Assert.That(percentage, Is.EqualTo(-20));
    }

    [Test]
    public void ChangeBetweenTwoPricesPercentageReturnsZeroValue()
    {
        var sut = new ChangeBetweenTwoPrices(100, 100);

        var percentage = sut.Percentage;

        Assert.That(percentage, Is.EqualTo(0));
    }

    [Test]
    public void ChangeBetweenTwoPricesIsPositiveReturnsTrueWhenGreaterThanZero()
    {
        var sut = new ChangeBetweenTwoPrices(100, 110);

        var isPositive = sut.IsPositive;

        Assert.That(isPositive, Is.True);
    }

    [Test]
    public void ChangeBetweenTwoPricesIsPositiveReturnsTrueWhenZero()
    {
        var sut = new ChangeBetweenTwoPrices(100, 100);

        var isPositive = sut.IsPositive;

        Assert.That(isPositive, Is.True);
    }

    [Test]
    public void ChangeBetweenTwoPricesIsPositiveReturnsFalseWhenNegative()
    {
        var sut = new ChangeBetweenTwoPrices(100, 90);

        var isPositive = sut.IsPositive;

        Assert.That(isPositive, Is.False);
    }

    [Test]
    public void ChangeBetweenTwoPricesToStringFormatsTwoDecimals()
    {
        var sut = new ChangeBetweenTwoPrices(100, 110);

        var formatted = sut.ToString();

        Assert.That(formatted, Is.EqualTo("10.00\u202f%"));
    }

    [Test]
    public void ChangeBetweenTwoPricesPercentageReturnsInfinityWhenStartPriceIsZero()
    {
        var sut = new ChangeBetweenTwoPrices(0, 10);

        var percentage = sut.Percentage;

        Assert.That(double.IsInfinity(percentage), Is.True);
    }

    [Test]
    public void ChangeBetweenTwoPricesPercentageReturnsNaNWhenStartAndEndAreZero()
    {
        var sut = new ChangeBetweenTwoPrices(0, 0);

        var percentage = sut.Percentage;

        Assert.That(double.IsNaN(percentage), Is.True);
    }

    [Test]
    public void ChangeFromPreviousClosePercentageReturnsPositiveValue()
    {
        var sut = new ChangeFromPreviousClose(125, 100);

        var percentage = sut.Percentage;

        Assert.That(percentage, Is.EqualTo(25));
    }

    [Test]
    public void ChangeFromPreviousClosePercentageReturnsNegativeValue()
    {
        var sut = new ChangeFromPreviousClose(80, 100);

        var percentage = sut.Percentage;

        Assert.That(percentage, Is.EqualTo(-20));
    }

    [Test]
    public void ChangeFromPreviousClosePercentageReturnsZeroValue()
    {
        var sut = new ChangeFromPreviousClose(100, 100);

        var percentage = sut.Percentage;

        Assert.That(percentage, Is.EqualTo(0));
    }

    [Test]
    public void ChangeFromPreviousCloseIsPositiveReturnsTrueWhenGreaterThanZero()
    {
        var sut = new ChangeFromPreviousClose(110, 100);

        var isPositive = sut.IsPositive;

        Assert.That(isPositive, Is.True);
    }

    [Test]
    public void ChangeFromPreviousCloseIsPositiveReturnsTrueWhenZero()
    {
        var sut = new ChangeFromPreviousClose(100, 100);

        var isPositive = sut.IsPositive;

        Assert.That(isPositive, Is.True);
    }

    [Test]
    public void ChangeFromPreviousCloseIsPositiveReturnsFalseWhenNegative()
    {
        var sut = new ChangeFromPreviousClose(90, 100);

        var isPositive = sut.IsPositive;

        Assert.That(isPositive, Is.False);
    }

    [Test]
    public void ChangeFromPreviousCloseToStringFormatsTwoDecimals()
    {
        var sut = new ChangeFromPreviousClose(110, 100);

        var formatted = sut.ToString();

        Assert.That(formatted, Is.EqualTo("10.00\u202f%"));
    }

    [Test]
    public void ChangeFromPreviousClosePercentageReturnsInfinityWhenPreviousCloseIsZero()
    {
        var sut = new ChangeFromPreviousClose(10, 0);

        var percentage = sut.Percentage;

        Assert.That(double.IsInfinity(percentage), Is.True);
    }

    [Test]
    public void ChangeFromPreviousClosePercentageReturnsNaNWhenRegularMarketPriceAndPreviousCloseAreZero()
    {
        var sut = new ChangeFromPreviousClose(0, 0);

        var percentage = sut.Percentage;

        Assert.That(double.IsNaN(percentage), Is.True);
    }
}
