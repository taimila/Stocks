using Stocks.Model;

namespace Stocks.Tests;

public sealed class SymbolTests
{
    [Test]
    public void ConstructorNormalizesValue()
    {
        var sut = new Symbol(" aapl ");

        Assert.That(sut.Value, Is.EqualTo("AAPL"));
    }

    [Test]
    public void EqualityUsesNormalizedValue()
    {
        Assert.That(new Symbol("aapl"), Is.EqualTo(new Symbol(" AAPL ")));
    }

    [Test]
    public void ConstructorThrowsWhenValueIsBlank()
    {
        Assert.Throws<ArgumentException>(() => new Symbol(" "));
    }

    [Test]
    public void TryCreateReturnsFalseWhenValueIsBlank()
    {
        var result = Symbol.TryCreate(" ", out var symbol);

        Assert.That(result, Is.False);
        Assert.That(symbol, Is.Null);
    }
}
