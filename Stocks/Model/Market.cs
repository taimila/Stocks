// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.Model;

public enum MarketStatus
{
    Unknown,
    Open,
    Closed
}

public class Market
{
    private TimeSpan? regularMarketOpenTime;
    private TimeSpan? regularMarketCloseTime;
    private string? exchangeTimezoneName;
    private int? exchangeGmtoffset;

    public virtual void Update(Result result)
    {
        if (!string.IsNullOrWhiteSpace(result.Meta.ExchangeTimezoneName))
        {
            exchangeTimezoneName = result.Meta.ExchangeTimezoneName;
        }

        exchangeGmtoffset = result.Meta.Gmtoffset;

        var regular = result.Meta.CurrentTradingPeriod?.Regular
            ?? result.Meta.TradingPeriods?.FirstOrDefault()?.FirstOrDefault();

        if (regular == null)
        {
            return;
        }

        if (TryGetLocalTimeOfDay(regular.Start, exchangeTimezoneName, regular.Gmtoffset, out var openTime) &&
            TryGetLocalTimeOfDay(regular.End, exchangeTimezoneName, regular.Gmtoffset, out var closeTime))
        {
            regularMarketOpenTime = openTime;
            regularMarketCloseTime = closeTime;
        }
    }

    public virtual MarketStatus Status 
    {
        get 
        {
            if (regularMarketOpenTime == null || regularMarketCloseTime == null)
            {
                return MarketStatus.Unknown;
            }

            if (!TryGetExchangeNow(out var nowExchange))
            {
                return MarketStatus.Unknown;
            }

            if (nowExchange.DayOfWeek == DayOfWeek.Saturday || nowExchange.DayOfWeek == DayOfWeek.Sunday)
            {
                return MarketStatus.Closed;
            }

            var nowTime = nowExchange.TimeOfDay;
            var openTime = regularMarketOpenTime.Value;
            var closeTime = regularMarketCloseTime.Value;

            var isOpen = openTime <= closeTime
                ? nowTime >= openTime && nowTime <= closeTime
                : nowTime >= openTime || nowTime <= closeTime;

            return isOpen ? MarketStatus.Open : MarketStatus.Closed;
        }
    }

    private bool TryGetExchangeNow(out DateTimeOffset nowExchange)
    {
        var nowUtc = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(exchangeTimezoneName) && TryGetTimeZone(exchangeTimezoneName, out var timezone))
        {
            nowExchange = TimeZoneInfo.ConvertTime(nowUtc, timezone);
            return true;
        }

        if (exchangeGmtoffset.HasValue)
        {
            nowExchange = nowUtc.ToOffset(TimeSpan.FromSeconds(exchangeGmtoffset.Value));
            return true;
        }

        nowExchange = default;
        return false;
    }

    private static bool TryGetLocalTimeOfDay(long unixSeconds, string? timezoneName, int? gmtoffset, out TimeSpan localTime)
    {
        var utc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        if (!string.IsNullOrWhiteSpace(timezoneName) && TryGetTimeZone(timezoneName, out var timezone))
        {
            localTime = TimeZoneInfo.ConvertTime(utc, timezone).TimeOfDay;
            return true;
        }

        if (gmtoffset.HasValue)
        {
            localTime = utc.ToOffset(TimeSpan.FromSeconds(gmtoffset.Value)).TimeOfDay;
            return true;
        }

        localTime = default;
        return false;
    }

    private static bool TryGetTimeZone(string timezoneName, out TimeZoneInfo timezone)
    {
        try
        {
            timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneName);
            return true;
        }
        catch
        {
            timezone = TimeZoneInfo.Utc;
            return false;    
        }
    }
}
