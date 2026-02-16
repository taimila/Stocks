// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;

namespace Stocks.Model;

public class TickerFetcher
{
    readonly string baseUrl = "https://query1.finance.yahoo.com/v8/finance/chart/";
    readonly HttpClient client;

    public TickerFetcher(AppSettings settings, HttpClient httpClient)
    {
        this.client = httpClient;
        this.client.DefaultRequestHeaders.Add("user-agent", settings.UserAgent);
    }

    public virtual async Task<Result> Fetch(string symbol, TickerRange range)
    {
        var url = baseUrl + symbol + "?" + GetRangeQueryParameterFor(range);

        try
        {
            var json = await client.GetStringAsync(url);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var dto = JsonSerializer.Deserialize<ChartResponse>(json, options);
            return dto!.Chart.Result!.First();
        }
        catch(Exception e)
        {
            var msg = $"Failed to fetch or parse data from: {url}";
            Console.WriteLine(e);
            throw new TickerFetchFailedException($"Failed to fetch or parse data from: {url}", e);
        }
    }

    public virtual async Task<List<SearchResult>> SearchTickers(string searchTerm)
    {
        var url = "https://query2.finance.yahoo.com/v1/finance/search?q=" + Uri.EscapeDataString(searchTerm);

        try
        {
            var json = await client.GetStringAsync(url);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var dto = JsonSerializer.Deserialize<YahooSearchResults>(json, options);
            return dto!.Quotes.ToList();
        }
        catch(Exception e)
        {
            Console.WriteLine(e);
            return [];
        }
    }

    private string GetRangeQueryParameterFor(TickerRange range)
    {
        static string Query(string r, string i) => $"range={r}&interval={i}";
        
        // Use more fine grained interval at the beginning of the year
        var ytdInterval = (DateTime.Now - new DateTime(DateTime.Now.Year, 1, 1)).Days > 80 ? "1d" : "60m";

        return (range) switch
        {
            TickerRange.Day => Query("1d", "2m"),
            TickerRange.FiveDays => Query("5d", "15m"),
            TickerRange.Month => Query("1mo", "60m"),
            TickerRange.ThreeMonths => Query("3mo", "60m"),
            TickerRange.SixMonths => Query("6mo", "1d"),
            TickerRange.Year => Query("1y", "1d"),
            TickerRange.TwoYears => Query("2y", "1d"),
            TickerRange.FiveYears => Query("5y", "1wk"),
            TickerRange.TenYears => Query("10y", "1mo"),
            TickerRange.All => Query("max", "1mo"),
            TickerRange.Ytd => Query("ytd", ytdInterval),
            _ => Query("1d", "2m"),
        };
    }
}

public class TickerFetchFailedException(string message, Exception inner) : Exception(message, inner) { }

public record YahooSearchResults(
    List<SearchResult> Quotes
);

public record SearchResult(
    string Shortname,
    string Symbol,
    string Longname,
    string ExchDisp
);

public record ChartResponse(
    ChartData Chart
);

public record ChartData(
    IReadOnlyList<Result>? Result,
    object? Error
);

public record Result(
    Meta Meta,
    long[] Timestamp,
    Indicators Indicators
);

public record Meta(
    string Currency,
    string Symbol,
    string ExchangeName,
    string FullExchangeName,
    string InstrumentType,
    long? FirstTradeDate,
    long? RegularMarketTime,
    int Gmtoffset,
    string Timezone,

    string? ExchangeTimezoneName,
    double RegularMarketPrice,
    double? PreviousClose,
    double ChartPreviousClose,
    double RegularMarketDayHigh,
    double RegularMarketDayLow,
    long? RegularMarketVolume,

    string? LongName,
    string? ShortName,

    int? PriceHint,
    int? Scale,
    bool? HasPrePostMarketData,
    CurrentTradingPeriod? CurrentTradingPeriod,
    IReadOnlyList<IReadOnlyList<TradingPeriod>>? TradingPeriods,
    string? DataGranularity,
    string? Range,
    IReadOnlyList<string>? ValidRanges
);

public record CurrentTradingPeriod(
    TradingPeriod? Pre,
    TradingPeriod? Regular,
    TradingPeriod? Post
);

public record TradingPeriod(
    string Timezone,
    long Start,
    long End,
    int Gmtoffset
);

public record Indicators(
    IReadOnlyList<Quote>? Quote,
    IReadOnlyList<AdjustedClose>? AdjClose
);

public record AdjustedClose(
    double?[]? AdjClose
);

public record Quote(
    double?[]? Open,
    double?[]? High,
    double?[]? Low,
    double?[]? Close,
    long?[]? Volume
);
