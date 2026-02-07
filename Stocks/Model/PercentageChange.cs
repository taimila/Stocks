// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.Model;

public interface IPercentageChange
{
    public double Percentage { get; }
    public bool IsPositive  { get; }
}

public class ChangeBetweenTwoPrices(double startPrice, double endPrice) : IPercentageChange
{
    public double Percentage { get; private set; } = (endPrice - startPrice) / startPrice * 100;
    public bool IsPositive => Percentage >= 0;
    public override string ToString() => $"{Percentage:F2}\u202f%";
}

public class ChangeFromPreviousClose(double regularMarketPrice, double previousClose) : IPercentageChange
{
    public double Percentage { get; private set; } = (regularMarketPrice - previousClose) / previousClose * 100;
    public bool IsPositive => Percentage >= 0;
    public override string ToString() => $"{Percentage:F2}\u202f%";
}

