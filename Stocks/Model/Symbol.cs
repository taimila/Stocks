// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.Model;

/// <summary>
/// Ticker symbol. Examples: AAPL, TSLA, META, ^GSPC, EUR=X.
/// </summary>
public sealed record Symbol
{
    public string Value { get; }

    public Symbol(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Symbol value cannot be empty.", nameof(value));

        Value = value.Trim().ToUpperInvariant();
    }

    public static bool TryCreate(string? value, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Symbol? symbol)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            symbol = null;
            return false;
        }

        symbol = new Symbol(value);
        return true;
    }

    public override string ToString()
    {
        return Value;
    }
}
