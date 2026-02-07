// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.Model;

public enum TickerRange
{
    Day,
    FiveDays,
    Month,
    ThreeMonths,
    SixMonths,
    Ytd,
    Year,
    TwoYears,
    FiveYears,
    TenYears,
    All,
}

public static class TickerRangeExtensions
{
    public static bool IsShort(this TickerRange range)
    {
        return range == TickerRange.Day || range == TickerRange.FiveDays;
    }

    public static string GetDisplayName(this TickerRange range) => range switch
    {
        TickerRange.Day => _("1 day"),
        TickerRange.FiveDays => _("5 days"),
        TickerRange.Month => _("1 month"),
        TickerRange.ThreeMonths => _("3 months"),
        TickerRange.SixMonths => _("6 months"),
        TickerRange.Year => _("1 year"),
        TickerRange.TwoYears => _("2 years"),
        TickerRange.FiveYears => _("5 years"),
        TickerRange.TenYears => _("10 years"),
        TickerRange.All => _("All"),
        TickerRange.Ytd => _("Year to date"),
        _ => range.ToString()
    };

    public static string GetShortDisplayName(this TickerRange range) => range switch
    {
        TickerRange.Day => _("1d"),
        TickerRange.FiveDays => _("5d"),
        TickerRange.Month => _("1m"),
        TickerRange.ThreeMonths => _("3m"),
        TickerRange.SixMonths => _("6m"),
        TickerRange.Year => _("1y"),
        TickerRange.TwoYears => _("2y"),
        TickerRange.FiveYears => _("5y"),
        TickerRange.TenYears => _("10y"),
        TickerRange.All => _("All"),
        TickerRange.Ytd => _("Ytd"),
        _ => range.ToString()
    };
}