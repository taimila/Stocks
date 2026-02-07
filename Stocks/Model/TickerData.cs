// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.Model;

public class TickerData
{
    public DateTime TimeStamp = DateTime.Now;
    public TickerRange Range { get; init; } = TickerRange.Day;
    public double PreviousClose { get; init; } = 0;

    // Price    
    public Amount MarketPrice { get; init; } = new Amount(0, "", 2);
    public Amount MarketDayHigh { get; init; } = new Amount(0, "", 2);
    public Amount MarketDayLow { get; init; } = new Amount(0, "", 2);
    public Amount MarketDayOpen { get; init; } = new Amount(0, "", 2);

    public IPercentageChange PercentageChange { get; init; } = new ChangeBetweenTwoPrices(1,1);
    public DataPoint[] DataPoints { get; init; } = [];

    public bool IsPositive => PercentageChange.IsPositive;
}

public record DataPoint(
    DateTime Timestamp,
    double Open,
    double High,
    double Low,
    double Close,
    //double AdjustedClose,
    long Volume
);