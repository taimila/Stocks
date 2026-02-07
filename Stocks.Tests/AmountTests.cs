using NUnit.Framework;
using Stocks.Model;

namespace Stocks.Tests;

public class AmountTests
{
    [Test]
    public void PriceReturnsConstructorValue()
    {
        var sut = new Amount(12.5, "USD", 2);

        var price = sut.Price;

        Assert.That(price, Is.EqualTo(12.5));
    }

    [Test]
    public void CurrencyDefaultsToUnknownWhenCodeIsEmpty()
    {
        var sut = new Amount(1, "", 2);

        var code = sut.Currency.Code;

        Assert.That(code, Is.EqualTo("Unknown"));
    }

    [Test]
    public void CurrencyUsesProvidedCode()
    {
        var sut = new Amount(1, "EUR", 2);

        var code = sut.Currency.Code;

        Assert.That(code, Is.EqualTo("EUR"));
    }

    [Test]
    public void ToStringWithCurrencyPlacesSymbolBeforeForUsd()
    {
        var sut = new Amount(12.5, "USD", 2);

        var formatted = sut.ToStringWithCurrency();

        Assert.That(formatted, Is.EqualTo("$ 12.50"));
    }

    [Test]
    public void ToStringWithCurrencyPlacesSymbolAfterForEur()
    {
        var sut = new Amount(12.5, "EUR", 2);

        var formatted = sut.ToStringWithCurrency();

        Assert.That(formatted, Is.EqualTo("12.50 €"));
    }

    [Test]
    public void ToStringWithCurrencyUsesCodeWhenSymbolIsUnknown()
    {
        var sut = new Amount(12.5, "XYZ", 2);

        var formatted = sut.ToStringWithCurrency();

        Assert.That(formatted, Is.EqualTo("12.50 XYZ"));
    }

    [Test]
    public void ToStringWithCurrencyOmitsSymbolForUnknownCurrency()
    {
        var sut = new Amount(12.5, "", 2);

        var formatted = sut.ToStringWithCurrency();

        Assert.That(formatted, Is.EqualTo("12.50"));
    }

    [Test]
    public void ToStringWithoutCurrencyUsesSpecifiedDecimals()
    {
        var sut = new Amount(12.3456, "USD", 3);

        var formatted = sut.ToStringWithoutCurrency();

        Assert.That(formatted, Is.EqualTo("12.346"));
    }
}
