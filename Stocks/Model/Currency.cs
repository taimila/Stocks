// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.Model;

public enum CurrencyPlacement
{
    Before,
    After
}

public record Currency(string Code)
{
    public override string ToString() => (Code) switch
    {
        "USD" => "$",
        "EUR" => "€", 
        "GBP" => "£", 
        "INR" => "₹", 
        "JPY" => "¥",
        "CNY" => "¥", 
        "KRW" => "₩",
        "RUB" => "₽",
        "Unknown" => "",
        _ => Code
    };

    public CurrencyPlacement GetPlacement() => (Code) switch
    {
        "USD" => CurrencyPlacement.Before,
        "GBP" => CurrencyPlacement.Before,
        "INR" => CurrencyPlacement.Before,
        "JPY" => CurrencyPlacement.Before,
        "CNY" => CurrencyPlacement.Before,
        "KRW" => CurrencyPlacement.Before,
        _ => CurrencyPlacement.After
    };

    public static Currency Unknown() => new("Unknown");
}