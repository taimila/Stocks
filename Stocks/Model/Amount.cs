// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.Model;

public struct Amount(double marketPrice, string currencyCode, int numberOfDecimals)
{
    private readonly int numberOfDecimals = numberOfDecimals;

    public double Price { get; private set; } = marketPrice;
    public Currency Currency { get; private set; } = string.IsNullOrWhiteSpace(currencyCode) ? Currency.Unknown() : new Currency(currencyCode);

    // TODO: This is hackish and should be cleaned up. This should basically be static function
    // not an instance function. However, usecase of this at the moment does not know number of
    // decimals and currency that should be used.
    public readonly string Format(double price, bool includeCurrency = true)
    {
        var formattedPrice = price.ToString($"F{numberOfDecimals}");
        if (!includeCurrency)
        {
            return formattedPrice;
        }

        var currencyText = Currency.ToString();
        if (string.IsNullOrEmpty(currencyText))
        {
            return formattedPrice;
        }

        return Currency.GetPlacement() switch
        {
            CurrencyPlacement.Before => $"{currencyText}\u202f{formattedPrice}",
            _ => $"{formattedPrice}\u202f{currencyText}"
        };
    }

    public readonly string ToStringWithCurrency() => Format(Price, includeCurrency: true);
    public readonly string ToStringWithoutCurrency() => Format(Price, includeCurrency: false);
}
